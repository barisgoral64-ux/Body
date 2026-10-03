# 5. Multiplayer Akış Diyagramı

## 5.1 Davet → Oda → Maç
```mermaid
sequenceDiagram
    participant A as Oyuncu A (client)
    participant S as Sunucu
    participant B as Oyuncu B (client)
    A->>S: invite.send {receiverId, mode}
    S->>S: Kontroller: arkadaş mı? engel var mı? ebeveyn izinleri (A ve B)? B çevrimiçi mi?
    alt kontrol başarısız
        S-->>A: error {code}  (çocuğa nötr mesaj: "Şimdi oynayamıyor")
    else tamam
        S->>S: GameInvite(pending, expiresAt=+30s)
        S-->>B: invite.received "Mutlu Panda seninle oynamak istiyor!"
        B->>S: invite.respond {accept:true}
        S->>S: Room(Waiting) oluştur
        S-->>A: room.state
        S-->>B: room.state
        A->>S: room.ready
        B->>S: room.ready
        S->>S: Room(Ready → Playing)
        S-->>A: room.countdown 3,2,1
        S-->>B: room.countdown 3,2,1
        loop 5 round (veya co-op hedefi)
            S-->>A: room.round {roundId, task, deadline}
            S-->>B: room.round (aynı görev)
            A->>S: room.answer {roundId, choiceId}
            B->>S: room.answer {roundId, choiceId}
            S->>S: Doğrula, süre ölç, puan hesapla
            S-->>A: room.roundResult {scores}
            S-->>B: room.roundResult {scores}
        end
        S->>S: Finished: sonuç + ödül hesapla, DB'ye yaz
        S-->>A: room.finished {result, rewards}
        S-->>B: room.finished {result, rewards}
    end
```

## 5.2 Oda Durum Makinesi
```mermaid
stateDiagram-v2
    [*] --> Waiting
    Waiting --> Ready: iki oyuncu bağlı
    Waiting --> Finished: zaman aşımı / reddedildi
    Ready --> Playing: iki oyuncu hazır + geri sayım
    Playing --> Playing: sonraki round
    Playing --> Reconnecting: bir oyuncu koptu
    Reconnecting --> Playing: grace süresi içinde geri döndü
    Reconnecting --> Finished: süre doldu (güvenli bitiş)
    Playing --> Finished: son round
    Finished --> [*]
```

## 5.3 Round Sırası (Rekabet Modu)
1 Renk → 2 Şekil → 3 Sayı → 4 Hafıza → 5 Puzzle (15-30 sn)

## 5.4 Co-op ("Birlikte Başaralım")
Sunucu ortak hedef sayacı tutar (`teamTarget=10`). İki oyuncunun doğru cevapları toplanır. Hedef dolunca `Finished(success)`; süre dolarsa "Çok yaklaştınız!" ve yine takım ödülü verilir (daha az).

## 5.5 Exploit Kontrolleri
| Risk | Önlem |
|---|---|
| Sahte skor | Skor sadece sunucuda hesaplanır |
| Hız hilesi (anında cevap) | Sunucu round başlangıç zamanından süre ölçer; insan altı süre (config `minHumanResponseMs`) hız bonusunu sıfırlar |
| Aynı round'a çoklu cevap | Round başına ilk cevap geçerli, sonrası yok sayılır |
| Başkasının odasına girme | JWT oyuncu ID ↔ oda üyeliği kontrolü |
| Mesaj taşkını | Rate limit + hazır mesaj aralık sınırı |
| Replay / eski round | `roundId` eşleşmeli |
| Davet spam | Gönderen başına bekleme + aynı alıcıya bekleyen davet limiti |
| Kopma sömürüsü | Kopan oyuncu cezalandırılmaz, kazanan da hileyle kazanamaz: erken bitişte eşit minimum ödül |
