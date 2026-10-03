# 11. Güvenlik ve Gizlilik: gerçekte ne uygulandı

Bu doküman yalnızca **koda girmiş ve testle doğrulanmış** önlemleri listeler. Hukuki uyum ayrıca gerektir (bkz. `12-play-store-release.md`).

## Gizlilik (privacy-by-design)
| Kural | Uygulama | Doğrulama |
|---|---|---|
| Gerçek isim/e-posta/telefon/doğum tarihi/konum alınmaz | Veritabanında bu alanlar yok; hesap anonim cihaz kimliğiyle | Şema + `ParentControlManager`/`AuthSession` testleri |
| Cihaz kimliği ham saklanmaz | HMAC-SHA256 özeti, **JWT'den ayrı** `DEVICE_SECRET` ile | `config` testi (üretimde zorunlu ve farklı) |
| Serbest metin yok | WebSocket şeması katı: yalnızca 6 sabit hazır mesaj | `fixtures`, `game`, `e2e` testleri |
| Kullanıcı adı serbest değil | Beyaz listeden sistem üretir (`MutluPanda27`) | `domain` testi |
| Loglarda token/PIN yok | `token, pin, pinHash, authorization…` maskelenir; oyuncu ID kısaltılır | `infra` testi |
| Hesap silme (veri silme hakkı) | Ebeveyn PIN'i ile `POST /v1/parent/delete-account`; tüm tablolar cascade | `security` testi (bellek + PostgreSQL) |
| Sosyal özellikler varsayılan KAPALI | `defaultParentSettings` hepsi `false` | `domain` testi |

## Çocuk güvenliği
- **Yetişkin kapısı** (istemci): toplama sorusu, 3 yanlışta 30 sn kilit. *Bu bir güvenlik sınırı değil, çocuğu yanlışlıkla engelleyen bir sürtünmedir.*
- **Ebeveyn PIN'i** (sunucu): scrypt, 5 yanlışta kilit, **kilit süresi ardışık kilitlenmede 3 kat büyür (24 saat sınırı)**; PIN yokken sosyal özellik açılamaz.
- **Karşılıklı arkadaşlık**: tek satır `(a<b)` ile tek taraflı kayıt imkânsız; istek → kabul.
- **Numaralandırma koruması**: arkadaş koduyla hesap bulma günlük 20 deneme; bulunamayan/kapalı/engelli alıcı için **aynı nötr yanıt**; yalnızca kodla arama (kullanıcı adıyla yok).
- **Engelleme** iki yönlü: istek, davet, mesaj, presence görünürlüğü kapanır; ortak oda kapanır.
- **Presence gizliliği**: yalnızca arkadaş + iki tarafın ebeveyn izni açıkken gerçek durum; aksi hâlde "offline" (gizlilik ile çevrimdışı ayırt edilemez).

## Sunucu otoriter ağ güvenliği
- Skor/süre/ödül yalnızca sunucuda; istemci yalnızca `choiceId` gönderir. İnsan altı hızda hız bonusu yok.
- Tur başına tek cevap (çoklu seçimde nesne başına ilk alan kazanır); eski `roundId`, üye olmayan, geçersiz seçim reddedilir.
- Bağlantı kopunca 15 sn grace; `resumeToken` zamanlama-güvenli karşılaştırma; kopan ceza almaz, asgari ödül.
- Mesajlar: boyut sınırı (2 KB), katı şema, mesaj/sn sınırı, flood'da bağlantı kesme, ilk mesaj `auth` zorunlu, IP başına 8 eşzamanlı bağlantı.
- Bölüm sonucu: skor ≤ hedef, süre ≥ tur sayısı × 1,5 sn, kilitli bölüm reddi, tekrar oynayarak coin çiftçiliği yok.
- Yarış durumları: oyuncu başına kilit (süreç içi + PostgreSQL danışma kilidi); atomik `spendCoins`; testler kilidi kaldırınca **başarısız olur** (mutasyon kontrolü yapıldı).
- JWT: bağımlılıksız HS256, algoritma sabit (`alg=none` reddedilir), tür ayrımı (access/refresh), süre kontrolü.
- HTTP: IP başına hız sınırı, `trustProxy` yapılandırılabilir, gövde sınırı, `nosniff`, `no-store`, iç hata ayrıntısı istemciye sızmaz.

## Bilinen sınırlar (dürüstçe)
1. **Tek sunucu örneği**: odalar ve presence süreç içinde. Yatay ölçekleme için Redis + oda sahipliği gerekir (yapılmadı). Yük testi yapılmadı.
2. **Token depolama**: istemcide `PlayerPrefs` (şifresiz). Yayın öncesi Android Keystore / iOS Keychain önerilir.
3. **Anonim hesap spam'i**: IP başına hız sınırı var, cihaz doğrulaması (Play Integrity / App Attest) yok.
4. **Yetişkin kapısı** istemci tarafıdır; sunucu yalnızca PIN ve ebeveyn ayarlarını zorunlu kılar.
5. **COPPA "doğrulanabilir ebeveyn onayı"** yöntemi uygulanmadı; hukuki danışmanlık gerekir.
6. Bağımlılık taraması: üretim bağımlılıklarında bilinen açık yok (`npm audit --omit=dev`); geliştirme bağımlılıkları (test aracı) ayrıdır.
