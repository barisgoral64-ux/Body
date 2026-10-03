# 8. Proje Klasör Yapısı (gerçek)

```
minik-duello/
├─ README.md  docker-compose.yml  .github/workflows/ (ci.yml, unity-android.yml*)
├─ docs/                              # Tasarım, güvenlik, yayın, test dokümanları
├─ protocol/                          # Sunucu↔istemci sözleşme fixture'ları (gerçek mesajlardan)
├─ server/                            # Node.js + TypeScript (Fastify, ws, pg, zod)
│  ├─ migrations/001_init.sql         # PostgreSQL şeması
│  ├─ scripts/ (export-levels.ts, migrate.ts)
│  ├─ src/
│  │  ├─ config/    (constants.ts, env.ts)        # magic number yok, ortam ayrımı
│  │  ├─ domain/    (saf kurallar: puan, seviye kataloğu, ödül, arkadaş isteği, kimlik…)
│  │  ├─ game/      (tasks.ts, gameRoom.ts, roomService.ts)   # sunucu otoriter oda motoru
│  │  ├─ services/  (auth, player, parentControl, friend, friendRequest, presence, multiplayer,
│  │  │              reward, level, leaderboard, save)
│  │  ├─ infra/     (store arayüzü, memoryStore, pgStore, migrate, logger, mutex, rateLimiter, runtime)
│  │  ├─ security/  (tokens.ts: HS256 JWT)
│  │  ├─ transport/ (http/routes.ts, ws/gateway.ts)
│  │  └─ protocol.ts, container.ts, app.ts, main.ts
│  └─ test/                           # 111 test (bellek: 109 + 2 yalnız PostgreSQL)
└─ client/                            # Unity 2022.3 LTS
   ├─ Packages/manifest.json  ProjectSettings/ProjectVersion.txt
   ├─ Tools/                          # Unity olmadan doğrulama
   │  ├─ LogicTests/   (net8: Core+Domain+Services+ParentControls + EditMode testleri çalışır)
   │  ├─ CompileCheck/ (net472: Unity'ye bağlı tüm kod gerçek UnityEngine'e karşı derlenir)
   │  └─ UguiStub/     (uGUI imza taslağı; YALNIZCA derleme denetimi için, Unity'ye girmez)
   └─ Assets/_Project/
      ├─ Resources/levels.json        # sunucu kataloğundan üretilir
      ├─ Editor/ReleaseBuilder.cs     # AAB derleme betiği
      ├─ Tests/EditMode/
      └─ Scripts/                     # her klasör kendi asmdef'i
         ├─ Core/          (saf: Result, Log, EventBus, ServiceLocator, AppSettings, ScreenStack…)
         ├─ Domain/        (saf: Levels, Rounds [18 oyun türü üreticisi, labirent, eşleştirme/sıralama mantığı], Net [DTO+protokol], Rewards)
         ├─ ParentControls/(saf: yetişkin kapısı)
         ├─ Services/      (saf: Api, Realtime [RealtimeClient, MatchSession], Managers, Save)
         ├─ Infra/         (Unity: UnityWebRequest, ClientWebSocket, PlayerPrefs, zamanlayıcı, ses, GameConfig)
         ├─ UI/            (Unity: UIManager, 21 ekran, oyun görünümleri, avatar/görsel fabrikası)
         └─ Game/          (Unity: Bootstrapper, AppController)
```
\* `unity-android.yml` denenmemiş şablondur.

Asmdef bağımlılık yönü: `Core ← Domain ← Services ← Infra/UI ← Game`. Core/Domain/Services/ParentControls `noEngineReferences` ile Unity'den bağımsızdır ve .NET'te test edilir.
