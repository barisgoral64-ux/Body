# 9. Temel Sınıflar ve Servisler

## 9.1 İstemci (C#)
| Sınıf | Sorumluluk |
|---|---|
| `GameManager` | Uygulama yaşam döngüsü, servislerin başlatılması |
| `Bootstrapper` | Boot sahnesi: config yükle, servisleri kaydet, ana menüye geç |
| `SceneManager` (`SceneLoader`) | Sahne yükleme, yükleme ekranı |
| `UIManager` | Ekran yığını (push/pop), popup, Parent Gate yönlendirme |
| `AudioManager` | Müzik, efekt, `VoicePrompt` sesli yönerge, ebeveyn ses ayarı |
| `SaveManager` | Local + cloud save, çakışma çözümü |
| `PlayerManager` | Profil, seviye, yıldız |
| `LevelManager` | Bölüm yükleme, kilit, ilerleme |
| `DifficultyManager` | Bölüm → zorluk parametresi, adaptif ayarlama |
| `MiniGameFactory` / `IMiniGame` | Oyun türüne göre mini oyun üretimi |
| `RewardManager` / `InventoryManager` | Ödül hesaplama (tek oyunculu), envanter |
| `CharacterManager` | Karakter + kozmetik slotları |
| `FriendManager` | Liste, istek, davet, presence |
| `NetworkManager` | HTTP + WS bağlantı durumu, reconnect |
| `MatchManager` / `RoomManager` | Sunucu mesajlarını oyun durumuna çevirir (karar vermez) |
| `ParentControlManager` / `ParentGate` | Ayarlar, kapı, PIN, süre sınırı |
| Çekirdek yardımcılar | `ServiceLocator`, `EventBus`, `Log`, `Result<T>`, `GameConfig`, `AppEnvironment`, `SafeAreaFitter` |

## 9.2 Sunucu (TypeScript)
| Servis | Sorumluluk |
|---|---|
| `AuthService` | Anonim hesap, JWT üretimi/yenileme |
| `PlayerService` | Oyuncu, kullanıcı adı, arkadaş kodu üretimi |
| `FriendService` | Liste, kaldırma, engel etkisi |
| `FriendRequestService` | İstek yaşam döngüsü, süre dolumu |
| `MultiplayerService` | Davet akışı, WS oturumları, presence |
| `MatchService` | Round'lar, puanlama, sonuç |
| `RoomService` | Oda durum makinesi, reconnect penceresi |
| `RewardService` | Ödül hesabı, envanter, coin defteri |
| `LeaderboardService` | Haftalık arkadaşlar arası skor |
| `ParentControlService` | Ayarlar, PIN, engelleme, süre limiti |
| `SaveService` | Bulut kaydı ve eşitleme |
| `NotificationService` | Uygulama içi bildirim (push yok → çocuk verisi minimum) |

## 9.3 Domain Modelleri (sunucu + istemci birebir)
`Player`, `PlayerProfile`, `LevelConfig`, `PlayerProgress`, `FriendRequest`, `GameInvite`, `Room`, `Match`, `MatchResult`, `Reward`, `InventoryItem`, `ParentSettings`, `BlockedPlayer`, `QuickChatMessage` (kapalı küme).
