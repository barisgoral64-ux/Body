# 2. Teknik Mimari

## 2.1 Teknoloji Seçimi ve Gerekçe
| Katman | Seçim | Neden |
|---|---|---|
| İstemci | Unity 2022 LTS+ / C# | Tek kod tabanı iOS+Android, 2D performans; ekranlar ve görseller ilk kullanımda üretilir (lazy), havuzlama ve prosedürel sprite önbelleği |
| Gerçek zamanlı | WebSocket (JSON mesaj, şema sürümlü) | Basit, sıra tabanlı/hafif oyunlar için yeterli, düşük bant genişliği |
| REST API | Fastify (Node.js, TypeScript) | Hesap, arkadaşlık, kayıt, ebeveyn paneli |
| Veritabanı | PostgreSQL | İlişkisel bütünlük (arkadaşlık, envanter) |
| Presence/Oda durumu | Süreç içi (tek örnek) | **Şu an tek sunucu örneği**. Yatay ölçekleme için Redis (presence TTL, oda durumu, pub/sub) planlıdır; yapılmadı. |
| Doğrulama | Zod | Her gelen mesaj sunucuda şema doğrulanır |
| Kimlik | Anonim cihaz hesabı + JWT; ebeveyn PIN'i ayrı | E-posta/telefon toplanmaz |

Sıra tabanlı mini oyunlar için fizik senkronizasyonu gerekmediğinden Photon/Mirror gibi motor yerine **kendi server-authoritative WebSocket katmanımız** seçildi: tam kontrol, çocuk güvenliği kuralları (ör. hazır mesaj beyaz listesi) sunucuda uygulanır.

## 2.2 Katmanlar (Clean Architecture)
```
Presentation (UI, Scenes, Animations, Audio)
      ↓ yalnızca interface kullanır
Application (Managers, Use-cases: LevelManager, FriendManager, MatchManager…)
      ↓
Domain (saf modeller + kurallar: LevelConfig, Room, Score, FriendRequest…)
      ↑ implemente edilir
Infrastructure (HttpClient, WebSocketClient, SaveStorage, Unity PlayerPrefs, Logger)
```
Bağımlılık yönü içe doğrudur. Domain Unity'ye bağımlı değildir (test edilebilir, `asmdef` ile ayrılır). Sunucu tarafı aynı ayrımı izler: `domain/` → `services/` → `transport/` → `infra/`.

## 2.3 Temel Teknik Kararlar
- **Config-driven içerik:** Bölümler `levels.json` (sunucu Levels tablosu + istemci Addressables kopyası). Kod değil veri.
- **Mini oyun eklentisi:** `IMiniGame` + `MiniGameFactory` + `GameType` enum; yeni oyun = yeni sınıf + config.
- **Service Locator + interface:** Manager'lar `IService` arayüzleriyle kayıt edilir; testte sahte (mock) servis verilir.
- **Event Bus:** Gevşek bağlı UI güncellemesi (yıldız değişti, arkadaş geldi).
- **Ortam ayrımı:** `Development` / `Production`; URL, log seviyesi, hile korumaları config'ten.
- **Log:** Seviyeli (Debug/Info/Warn/Error), üretimde kişisel veri içermez; oyuncu ID maskelenir.
- **Hata modeli:** `Result<T>` + sabit hata kodları; UI kullanıcıya ikonlu çocuk dostu mesaj gösterir.
- **Magic number yok:** Tüm sayılar `GameConfig` / sunucu `config`'te.
- **Performans:** Sprite atlas, object pooling (yıldız/konfeti/kartlar), Addressables lazy load, ASTC/ETC2 doku sıkıştırma, OGG ses, GC-allocation'sız oyun döngüsü.

## 2.4 Güvenlik Yaklaşımı
- Privacy-by-design: gerçek isim, doğum tarihi, konum, e-posta, telefon alınmaz.
- Server-authoritative: skor, round, süre, ödül sunucuda hesaplanır; istemci yalnızca girdi (`answer{choiceId}`) gönderir.
- Rate limit (REST + WS), mesaj boyutu limiti, şema doğrulama, idempotent istek anahtarları.
- Ebeveyn PIN'i: Argon2/scrypt hash; deneme sınırı + bekleme.
- Hazır mesaj beyaz listesi sunucuda; serbest string kabul edilmez.
- JWT kısa ömürlü + refresh; cihaz başına oturum.
- Loglarda token/PIN yok; audit log ebeveyn işlemleri için.
