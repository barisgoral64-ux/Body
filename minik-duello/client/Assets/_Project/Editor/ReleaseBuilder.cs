using System;
using System.IO;
using MinikDuello.Infra;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MinikDuello.Editor
{
    /// <summary>
    /// Play Store sürümü için tekrarlanabilir derleme. Komut satırı:
    ///   Unity -batchmode -quit -projectPath client -executeMethod MinikDuello.Editor.ReleaseBuilder.BuildAndroidRelease
    /// Gerekli ortam değişkenleri: MINIK_PACKAGE, MINIK_API_URL (https), MINIK_WS_URL (wss),
    /// imza için MINIK_KEYSTORE_PATH, MINIK_KEYSTORE_PASS, MINIK_KEY_ALIAS, MINIK_KEY_ALIAS_PASS.
    /// Yer tutucu adres/paket adıyla üretim derlemesi YAPILMAZ.
    /// </summary>
    public static class ReleaseBuilder
    {
        private const string ConfigPath = "Assets/_Project/Resources/GameConfig.asset";
        private const string ScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string OutputPath = "build/Android/MinikDuello.aab";
        private const string PlaceholderHost = "example.invalid";
        private const string PlaceholderPackage = "com.example.minikduello";

        [MenuItem("Minik Düello/GameConfig oluştur veya seç")]
        public static void SelectOrCreateConfig()
        {
            Selection.activeObject = EnsureConfig();
        }

        public static GameConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config != null) return config;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        public static void BuildAndroidRelease()
        {
            try
            {
                Run();
            }
            catch (Exception exception)
            {
                Debug.LogError("Derleme başarısız: " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void Run()
        {
            string package = Require("MINIK_PACKAGE");
            string api = Require("MINIK_API_URL");
            string ws = Require("MINIK_WS_URL");
            if (package == PlaceholderPackage) throw new InvalidOperationException("MINIK_PACKAGE yer tutucu olamaz.");
            if (!api.StartsWith("https://", StringComparison.Ordinal) || !ws.StartsWith("wss://", StringComparison.Ordinal))
                throw new InvalidOperationException("Üretim adresleri https:// ve wss:// olmalı (şifreli bağlantı).");
            if (api.Contains(PlaceholderHost) || ws.Contains(PlaceholderHost))
                throw new InvalidOperationException("Yer tutucu adresle üretim derlemesi yapılmaz.");

            GameConfig config = EnsureConfig();
            config.environment = MinikDuello.Core.AppEnvironment.Production;
            config.productionApiUrl = api;
            config.productionWsUrl = ws;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            ConfigureAndroid(package);
            EnsureScene();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = BuildTarget.Android,
                locationPathName = OutputPath,
                options = BuildOptions.None // Development/Autoconnect profiler KAPALI
            };
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Derleme sonucu: " + report.summary.result);
            Debug.Log("AAB hazır: " + OutputPath + " (" + report.summary.totalSize + " bayt)");
        }

        private static void ConfigureAndroid(string package)
        {
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.companyName = Environment.GetEnvironmentVariable("MINIK_COMPANY") ?? "Minik Studio";
            PlayerSettings.productName = "Minik Düello";
            PlayerSettings.SetApplicationIdentifier(android, package);
            PlayerSettings.bundleVersion = Environment.GetEnvironmentVariable("MINIK_VERSION") ?? "1.0.0";
            if (int.TryParse(Environment.GetEnvironmentVariable("MINIK_BUILD"), out int code)) PlayerSettings.Android.bundleVersionCode = code;

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            // Hedef API: kurulu en yeni SDK. Mağaza gereksinimi (güncel hedef API) yayın öncesi ayrıca doğrulanmalıdır.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.forceInternetPermission = true;

            string keystore = Require("MINIK_KEYSTORE_PATH");
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Require("MINIK_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = Require("MINIK_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = Require("MINIK_KEY_ALIAS_PASS");
        }

        private static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static string Require(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value)) throw new InvalidOperationException("Ortam değişkeni eksik: " + name);
            return value;
        }
    }
}
