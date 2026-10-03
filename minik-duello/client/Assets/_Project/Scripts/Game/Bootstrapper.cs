using MinikDuello.Core;
using MinikDuello.Domain.Levels;
using MinikDuello.Infra;
using MinikDuello.Services;
using MinikDuello.Services.Api;
using MinikDuello.Services.Managers;
using MinikDuello.Services.Realtime;
using MinikDuello.Services.Save;
using MinikDuello.UI;
using UnityEngine;

namespace MinikDuello.Game
{
    /// <summary>
    /// Uygulamanın tek giriş noktası (composition root). Hangi sahne açık olursa olsun otomatik başlar:
    /// config → log → altyapı → servisler → UI → denetleyici. Elle sahne kurulumu gerekmez.
    /// </summary>
    public sealed class Bootstrapper : MonoBehaviour
    {
        private const string ConfigResourceName = "GameConfig";
        private const string LevelsResourceName = "levels";
        private const int TargetFrameRate = 60;

        private static bool launched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (launched) return;
            launched = true;
            new GameObject("Bootstrapper", typeof(Bootstrapper));
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = TargetFrameRate;

            GameConfig config = LoadConfig();
            AppSettings settings = config.ToSettings();
            Log.Configure(settings.LogLevel, new UnityLogSink());
            ServiceLocator.Clear();
            EventBus.Clear();
            ServiceLocator.Register(settings);

            var textAsset = Resources.Load<TextAsset>(LevelsResourceName);
            if (textAsset == null)
            {
                Log.Error("Boot", "Resources/levels.json bulunamadı. 'npm run export-levels' ile üretin.");
                return;
            }

            // Altyapı
            gameObject.AddComponent<MainThreadDispatcher>();
            var scheduler = Child("Scheduler").AddComponent<UnityScheduler>();
            var http = Child("Http").AddComponent<UnityHttpTransport>();
            var store = new PlayerPrefsStore();
            var clock = new UnityClock();
            var socket = new ClientWebSocketTransport();
            ServiceLocator.Register<IScheduler>(scheduler);
            ServiceLocator.Register<IClock>(clock);
            ServiceLocator.Register<IKeyValueStore>(store);

            // Servisler
            var connectivity = new ConnectivityState();
            var auth = new AuthSession(settings, http, store);
            var api = new ApiClient(settings, http, auth, scheduler, connectivity);
            var save = new SaveManager(store);
            LevelCatalog catalog = LevelCatalog.FromJson(textAsset.text, settings.TotalLevels, settings.LevelsPerWorld);
            var levels = new LevelManager(catalog, save, api, new DifficultyManager());
            var player = new PlayerManager(api, save, auth);
            var rewards = new RewardManager(api, save, player);
            var characters = new CharacterManager(save);
            var friends = new FriendManager(api);
            var parent = new ParentControlManager(api);
            var limiter = new PlaytimeLimiter(save, clock);
            var realtime = new RealtimeClient(settings, socket, auth, scheduler);
            var match = new MatchSession(settings, realtime, auth, scheduler);

            var audio = Child("Audio").AddComponent<AudioManager>();
            audio.Initialize(save, parent);

            ServiceLocator.Register(connectivity);
            ServiceLocator.Register(auth);
            ServiceLocator.Register(api);
            ServiceLocator.Register(save);
            ServiceLocator.Register(levels);
            ServiceLocator.Register(player);
            ServiceLocator.Register(rewards);
            ServiceLocator.Register(characters);
            ServiceLocator.Register(friends);
            ServiceLocator.Register(parent);
            ServiceLocator.Register(limiter);
            ServiceLocator.Register(realtime);
            ServiceLocator.Register(match);
            ServiceLocator.Register(audio);
            ServiceLocator.Register<IVoiceService>(audio);

            // Arayüz ve akış
            var ui = Child("UI").AddComponent<UIManager>();
            ServiceLocator.Register(ui);
            ui.Initialize(settings);
            Child("App").AddComponent<AppController>().Initialize(settings);

            Log.Info("Boot", "Başlatıldı. Ortam: " + settings.Environment);
        }

        private void OnDestroy() => launched = false;

        private GameObject Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go;
        }

        private static GameConfig LoadConfig()
        {
            var config = Resources.Load<GameConfig>(ConfigResourceName);
            if (config != null) return config;

            // Varlık yoksa güvenli varsayılanlarla (Development) devam edilir.
            Debug.LogWarning("Resources/GameConfig bulunamadı; varsayılan ayarlar kullanılıyor.");
            return ScriptableObject.CreateInstance<GameConfig>();
        }
    }
}
