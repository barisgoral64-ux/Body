# 8. Proje Klasör Yapısı

```
minik-duello/
├─ README.md
├─ docs/                          # Tasarım dokümanları (bu klasör)
├─ client/                        # Unity projesi
│  └─ Assets/
│     ├─ _Project/
│     │  ├─ Scripts/
│     │  │  ├─ Core/             # ServiceLocator, EventBus, Logger, Result, Environment, GameConfig
│     │  │  ├─ Game/             # GameManager, Bootstrapper, SceneLoader
│     │  │  ├─ Levels/           # LevelConfig, LevelManager, DifficultyManager
│     │  │  ├─ MiniGames/        # IMiniGame, MiniGameFactory, Colors/, Shapes/, Numbers/ …
│     │  │  ├─ Multiplayer/      # RoomManager, MatchManager, protokol modelleri
│     │  │  ├─ Networking/       # HttpClient, WebSocketClient, ReconnectPolicy
│     │  │  ├─ Friends/          # FriendManager, FriendRequest, GameInvite
│     │  │  ├─ Player/           # PlayerManager, PlayerProfile
│     │  │  ├─ Characters/       # CharacterManager, CosmeticSlot
│     │  │  ├─ Rewards/          # RewardManager, InventoryManager, DailyReward
│     │  │  ├─ UI/               # UIManager, Screens/, Components/, SafeAreaFitter
│     │  │  ├─ Audio/            # AudioManager, VoicePrompt
│     │  │  ├─ Animations/       # Confetti, StarBurst, Dance, HighFive
│     │  │  ├─ Database/         # LocalSave, SaveManager, CloudSyncService
│     │  │  ├─ Services/         # API istemci servisleri (Auth, Player, Friend…)
│     │  │  ├─ Security/         # TokenStore, şifreleme, giriş doğrulama
│     │  │  ├─ ParentControls/   # ParentControlManager, ParentGate, ParentSettings
│     │  │  └─ Utils/
│     │  ├─ Config/              # GameConfig.asset, levels.json
│     │  ├─ Scenes/              # Boot, Menu, Game, Lobby
│     │  ├─ Art/  Audio/  Prefabs/  Addressables/
│     │  └─ Tests/               # EditMode, PlayMode
│     └─ (Packages, ProjectSettings…)
└─ server/                        # Node.js + TypeScript
   ├─ package.json  tsconfig.json  vitest.config.ts  .env.example
   ├─ migrations/                 # SQL
   ├─ src/
   │  ├─ main.ts                  # Giriş noktası
   │  ├─ app.ts                   # Fastify uygulaması (testte yeniden kullanılır)
   │  ├─ config/                  # env, sabitler (magic number yok)
   │  ├─ infra/                   # logger, db, redis
   │  ├─ domain/                  # saf modeller + kurallar
   │  ├─ services/                # Auth, Player, Friend, FriendRequest, Multiplayer, Match, Room, Reward, Leaderboard, ParentControl, Save, Notification
   │  ├─ transport/
   │  │  ├─ http/                 # route'lar
   │  │  └─ ws/                   # WebSocket gateway, mesaj şemaları
   │  └─ shared/                  # hata kodları, Result
   └─ test/                       # birim + entegrasyon testleri
```

Her `Scripts/*` klasörü kendi `asmdef` dosyasına sahip olur: derleme süresi kısalır, bağımlılık yönü derleyici tarafından zorlanır.
