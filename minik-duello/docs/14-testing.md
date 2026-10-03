# 14. Test ve doğrulama: neyin doğrulandığı, neyin doğrulanmadığı

## Çalıştırma
```bash
# Sunucu (bellek içi depo)
cd server && npm ci && npm run typecheck && npm test

# Sunucu (gerçek PostgreSQL ile aynı testler; test başına ayrı şema)
TEST_DATABASE_URL=postgres://postgres@localhost:5432/minik_test npm test

# İstemci saf C# katmanı (Core/Domain/Services) — .NET 8 ile gerçekten çalışır
dotnet test client/Tools/LogicTests

# İstemci Unity'ye bağlı kod — gerçek UnityEngine 2021.3 modüllerine karşı DERLENİR (çalıştırılmaz)
dotnet build client/Tools/CompileCheck

# Sunucu↔istemci sözleşme fixture'larını yenilemek (sunucu değişince):
cd server && UPDATE_FIXTURES=1 npx vitest run test/fixtures.test.ts
```

## Mevcut sonuçlar (bu oturumda)
- Sunucu: **111 test** (bellek içi depoda 109 + yalnız PostgreSQL için 2) hem bellek içi depoda hem **gerçek PostgreSQL 16**'da geçti (e2e WebSocket oyunu, yarış koşulları, hesap silme, FK zinciri dahil). Üretim derlemesi gerçek PostgreSQL ile ayağa kalkıp `/health` ve kayıt yanıtı verdi.
- İstemci mantığı: **116 test** geçti (100 bölümün tamamı için 20 tohumla tur üretimi, labirent bağlantısı, eşleştirme/sıralama mantığı, API yeniden deneme/token yenileme, offline kuyruk, gerçek zamanlı yeniden bağlanma ve maça devam, protokol sözleşmesi).
- İstemci Unity kodu: tüm betikler UnityEngine 2021.3 modüllerine karşı **hatasız derlendi**.
- Mutasyon kontrolleri: kilit kaldırılınca yarış testleri, geri çekilme bozulunca yeniden bağlanma testi başarısız oldu (testler gerçekten bir şey denetliyor).

## Doğrulanmayanlar (yayın öncesi yapılmalı)
| Konu | Neden önemli |
|---|---|
| **Docker imajı / compose** | Bu ortamda Docker daemon yok; `Dockerfile` ve `docker-compose.yml` derlenmedi (sunucunun derlenmiş sürümü doğrudan PostgreSQL ile denendi) |
| **GitHub Actions iş akışları** | Çalıştırılmadı; komutlar yerelde ayrı ayrı çalıştırıldı |
| **Unity derleme betiği (`ReleaseBuilder`)** | UnityEditor API'leri bu ortamda derlenemez; gözle gözden geçirildi, çalıştırılmadı |
| **Unity'de açılış, derleme ve Test Runner** | uGUI için gerçek paket yerine imza taslağı kullanıldı; gerçek Unity'de küçük API farkları çıkabilir |
| **Gerçek cihaz testi** (düşük/orta Android, tablet, 16:9–20:9, çentik) | Yerleşim, dokunma hedefleri, sürükle-bırak hissi, 60 FPS, bellek ölçülmedi |
| **Görsel kalite** | Tüm çizimler prosedürel yer tutucu; sanatçı varlıkları yok |
| **Ses** | Efektler üretilmiş tonlar; sesli yönerge kayıtları yok (`Resources/Voice/{anahtar}` ile eklenir) |
| **Yük/dayanıklılık testi** | Eşzamanlı oda sayısı, bellek sızıntısı, uzun süreli bağlantı ölçülmedi |
| **Gerçek ağ koşulları** | Paket kaybı, mobil ağ geçişi, uygulama arka plana alınınca yeniden bağlanma cihazda denenmedi |
| **Güvenlik denetimi** | Bağımsız sızma testi yapılmadı; yalnızca kendi birim/entegrasyon testlerimiz var |
| **Erişilebilirlik** | Ekran okuyucu, renk körlüğü gerçek kullanıcıyla denenmedi (renklerde şekil ipucu eklendi) |
| **Çocuk kullanılabilirliği** | 4+ yaş çocuklarla kullanılabilirlik testi yapılmadı |
