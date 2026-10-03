# 3. Client–Server Mimarisi

```
┌────────────── Unity İstemci ──────────────┐        ┌────────────────── Sunucu ──────────────────┐
│ UI/Scenes  Audio  Animations              │        │  REST (Fastify)        WebSocket Gateway    │
│ Managers (Game, Level, Friend, Match…)    │ HTTPS  │  /auth /players        /ws  (JWT)           │
│ Infrastructure                            │───────▶│  /friends /requests    ├ presence             │
│  ├ HttpClient (retry, timeout, offline)   │        │  /parent /save         ├ invites              │
│  ├ WebSocketClient (reconnect/backoff)    │  WSS   │  /rewards /leaderboard └ rooms/matches        │
│  └ LocalSave (şifreli JSON)               │◀──────▶│                                            │
└───────────────────────────────────────────┘        │  Servisler (Auth, Player, Friend, Request,  │
                                                     │  Multiplayer, Match, Room, Reward,          │
                                                     │  Leaderboard, ParentControl, Save,          │
                                                     │  Notification)                              │
                                                     │          │                │                 │
                                                     │     PostgreSQL         Redis                │
                                                     └────────────────────────────────────────────┘
```

## 3.1 Sorumluluk Bölümü
| Konu | İstemci | Sunucu |
|---|---|---|
| Görev üretimi (hangi renk/şekil) | Göstermek | **Üretir** (seed), iki oyuncuya aynı gönderir |
| Cevap | `answer{roundId, choiceId, clientTs}` gönderir | Doğrular, **kendi saatiyle** süreyi ölçer |
| Skor / bonus | Gösterir | Hesaplar |
| Maç sonucu | Gösterir | Belirler, kaydeder |
| Ödül | Gösterir | Hesaplar ve envantere yazar |
| Presence | Heartbeat | Redis TTL ile durumu tutar, ebeveyn gizliliğini uygular |
| Tek oyunculu ilerleme | Oynar, sonucu bildirir | Makullük doğrulaması (süre/skor sınırı) sonra kaydeder |

## 3.2 WebSocket Mesaj Zarfı
```json
{ "v": 1, "type": "room.answer", "id": "uuid", "ts": 0, "payload": { } }
```
- `v`: protokol sürümü (uyumsuz istemci güncellemeye yönlendirilir).
- İstemci→Sunucu tipleri: `presence.heartbeat`, `invite.send`, `invite.respond`, `room.ready`, `room.answer`, `room.quickChat`, `room.leave`, `room.resume`.
- Sunucu→İstemci tipleri: `presence.update`, `invite.received`, `invite.resolved`, `room.state`, `room.countdown`, `room.round`, `room.roundResult`, `room.finished`, `room.opponentDisconnected`, `error`.

## 3.3 Bağlantı Kesilmesi
- WS: üssel geri çekilmeli (exponential backoff + jitter) yeniden bağlanma.
- Sunucu odada `disconnectedAt` işaretler, **grace period 15 sn** (config).
- İstemci "Arkadaşına yeniden bağlanıyoruz…" gösterir; `room.resume {roomId, resumeToken}` ile devam.
- Süre dolarsa maç `Finished(reason=disconnect)`; **kimse cezalandırılmaz**, o ana kadar kazanılan ödül verilir.

## 3.4 Çevrimdışı Tek Oyunculu
Tek oyunculu bölümler internetsiz oynanır. İlerleme `LocalSave`e yazılır, bağlantı gelince `SaveService` ile eşitlenir (çakışmada: yıldızlar için max, envanter için birleşim, coin için sunucu defter kaydı).
