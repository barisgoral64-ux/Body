# Minik Düello: Arkadaşlarla Oyna

4+ yaş için eğitici mini oyunlar, ebeveyn kontrollü arkadaş sistemi ve arkadaşla gerçek zamanlı 1v1 / birlikte oynama.
Güvenlik önce: serbest sohbet yok, kişisel veri yok, sosyal özellikler ebeveyn onayıyla ve varsayılan **kapalı**.

> **Durum:** Oyun kodu ve sunucu tamamlandı ve testlerle doğrulandı. **Play Store'a yüklenmeye hazır DEĞİL**: Unity'de/cihazda denenmedi,
> sanatçı varlıkları ve ses kayıtları yok, hukuki inceleme gerekli. Ayrıntı: [`docs/12-play-store-release.md`](docs/12-play-store-release.md).

## İçerik
| Klasör | İçerik |
|---|---|
| `docs/` | GDD, mimari, DB şeması, akışlar, ekranlar, güvenlik/gizlilik, yayın listesi, test durumu |
| `server/` | Node.js + TypeScript: REST + WebSocket, sunucu otoriter oda motoru, PostgreSQL |
| `client/` | Unity (C#) istemci: 100 bölüm, 18 oyun türü, sosyal, ebeveyn, çok oyunculu |
| `protocol/` | Sunucu↔istemci sözleşme fixture'ları (gerçek sunucu mesajlarından üretilir) |

## Hızlı başlangıç
```bash
# Sunucu (yerel, bellek içi depo)
cd server && npm ci && npm run dev          # http://localhost:3000/health
# Sunucu + PostgreSQL
docker compose up --build                    # (kökten; bu ortamda Docker daemon çalışmadığı için Dockerfile/compose DENENMEDİ; derlenmiş sunucu (`npm run build && npm start`) gerçek PostgreSQL ile denendi)

# Testler
cd server && npm test                                                       # bellek
TEST_DATABASE_URL=postgres://postgres@localhost:5432/minik_test npm test    # PostgreSQL
dotnet test client/Tools/LogicTests      # istemci mantığı
dotnet build client/Tools/CompileCheck   # Unity kodu derleme denetimi

# Bölüm verisini yenile (sunucu kataloğu → Unity)
cd server && npm run export-levels
```
**Unity:** `client/` klasörünü Unity 2022.3 LTS ile açın; `Window > General > Test Runner > EditMode`. Menü: `Minik Düello > GameConfig oluştur veya seç` ile `Resources/GameConfig` oluşturun ve geliştirme adreslerini ayarlayın. Uygulama herhangi bir sahnede otomatik başlar (`Bootstrapper`).

## Yapılar
- 100 bölüm / 10 dünya: veri tabanlı (`levels.json`), yumuşak zorluk eğrisi + adaptif kolaylaştırma, hiçbir "kaybettin" yok.
- Çok oyunculu: davet → oda → 3-2-1-BAŞLA! → turlar; rekabet ve "Birlikte Başaralım"; kopma sonrası 15 sn içinde kaldığı yerden devam.
- Ödüller: yıldız/coin/sticker/karakter/kostüm; günlük ödül (seri cezası yok); mağaza yalnızca oyunda kazanılan coin.
- Ebeveyn paneli: PIN, sosyal ayarlar, süre sınırı, arkadaş kaldırma/engelleme, hesap silme.
- Sürüm ve güncelleme: sürüm ana menüde ve ebeveyn panelinde; sunucudan zorunlu/isteğe bağlı güncelleme politikası ([`docs/15`](docs/15-versioning-and-updates.md)).
