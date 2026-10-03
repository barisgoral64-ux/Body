# 10. Geliştirme Roadmap'i

| Faz | İçerik | Çıktı | Doğrulama |
|---|---|---|---|
| 1 | Proje mimarisi | İskelet, config, log, ortam, domain modelleri, servis arayüzleri | Backend derleme + test |
| 2 | Ana menü | Sahneler, UIManager, SafeArea | Unity |
| 3 | Oyuncu profili | PlayerManager, profil ekranı | |
| 4 | Bölüm sistemi | LevelConfig, levels.json (100), LevelManager, DifficultyManager | Birim test |
| 5 | İlk mini oyun | Renk eşleştirme | |
| 6 | Ödül sistemi | RewardManager, günlük ödül | |
| 7 | Karakter sistemi | 8 karakter + slotlar | |
| 8 | Backend bağlantısı | HttpClient, ortam URL | Entegrasyon testi |
| 9 | Oyuncu hesabı | Anonim kayıt, JWT, kullanıcı adı/kod | |
| 10 | Arkadaş kodu | Kod üretimi/arama | |
| 11 | Arkadaş isteği | İstek servis + Parent Gate | |
| 12 | Arkadaş listesi | Liste + presence | |
| 13 | Game Invite | Davet akışı | |
| 14 | Multiplayer Room | Oda durum makinesi | |
| 15 | 1v1 oyun | Round, puan, sonuç | Sunucu simülasyon testi |
| 16 | Co-op | Birlikte Başaralım | |
| 17 | Reconnect | Grace süresi | |
| 18 | Parent Controls | Panel, engelleme, süre | |
| 19 | Güvenlik testleri | Exploit senaryoları, rate limit, fuzz | |
| 20 | Optimizasyon / release | Profil, atlas, mağaza uyumu, hukuk onayı | |

Kural: her faz sonunda çalışan, testli, commit'lenmiş bir durum.

## Durum (dürüst özet)
Kod tarafında FAZ 1-19 tamamlandı; FAZ 20 (optimizasyon/yayın hazırlığı) kısmen: derleme betiği, CI, Docker ve yayın kontrol listesi hazır, **cihaz profili, sanatçı varlıkları, ses kayıtları ve hukuki onay yok**.
Ayrıntı: `docs/12-play-store-release.md` ve `docs/14-testing.md`.
