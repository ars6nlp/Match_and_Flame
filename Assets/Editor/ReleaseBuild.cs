#if UNITY_ANDROID
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Релизная сборка AAB для обновления в Google Play, подписанная upload-ключом.
/// Пароли keystore берутся из переменных окружения MF_KEYSTORE_PASS и MF_KEY_PASS (или из полей Player Settings,
/// если их ввели в открытом редакторе) и в проект не записываются.
/// До сборки проверяется: ссылка на политику не заглушка, рекламные ID боевые, versionCode больше опубликованного,
/// пакет и keystore те же, targetSdk ≥ 35. Символы IL2CPP кладутся внутрь AAB (Play Console подхватит сам) и zip рядом.
/// Итог и разбор — Builds/release-build.txt рядом с AAB.
/// Запуск (Unity закрыт): Unity.exe -batchmode -quit -projectPath &lt;проект&gt; -buildTarget Android -executeMethod ReleaseBuild.Aab -logFile &lt;лог&gt;
/// </summary>
public static class ReleaseBuild
{
    /// <summary>Последний versionCode, загруженный в Play Console (5 = версия 4.1, отправлена на проверку 24.10.2026). Новая сборка должна быть больше.</summary>
    public const int PublishedVersionCode = 5;
    public const string Package = "com.aixdev.matchandflame";
    // Папка Builds рядом с папкой проекта (../Builds)
    private static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds")).Replace('\\', '/');
    private const string GoogleTestPublisher = "ca-app-pub-3940256099942544";

    [MenuItem("Tools/Match & Flame/Build/Release AAB for Google Play")]
    public static void Aab()
    {
        var log = new StringBuilder("# Релизная сборка AAB\n\n");
        string ksPass = Environment.GetEnvironmentVariable("MF_KEYSTORE_PASS");
        string keyPass = Environment.GetEnvironmentVariable("MF_KEY_PASS");
        if (string.IsNullOrEmpty(ksPass)) ksPass = PlayerSettings.Android.keystorePass;
        if (string.IsNullOrEmpty(keyPass)) keyPass = string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass) ? ksPass : PlayerSettings.Android.keyaliasPass;

        var target = NamedBuildTarget.Android;
        string version = PlayerSettings.bundleVersion;
        int code = PlayerSettings.Android.bundleVersionCode;
        string problems = "";
        void Need(bool ok, string what) { if (!ok) problems += "- " + what + "\n"; }
        Need(!AppLinks.PrivacyPolicyIsPlaceholder, $"Ссылка на политику конфиденциальности — заглушка ({AppLinks.PrivacyPolicyUrl}). Впишите адрес в Assets/Scripts/AppLinks.cs");
        Need(!AdIds.Banner.StartsWith(GoogleTestPublisher) && !AdIds.Interstitial.StartsWith(GoogleTestPublisher) && !AdIds.Rewarded.StartsWith(GoogleTestPublisher), "В AdIds.cs тестовые ID рекламы");
        Need(code > PublishedVersionCode, $"versionCode {code} не больше опубликованного {PublishedVersionCode}");
        Need(PlayerSettings.GetApplicationIdentifier(target) == Package, $"Пакет {PlayerSettings.GetApplicationIdentifier(target)} ≠ {Package}");
        Need((int)PlayerSettings.Android.targetSdkVersion >= 35, $"targetSdk {PlayerSettings.Android.targetSdkVersion} < 35");
        Need((int)PlayerSettings.Android.minSdkVersion >= 24, $"minSdk {PlayerSettings.Android.minSdkVersion} < 24: «Автоматическая защита» Google Play требует Android 7.0+ (Player → Other Settings → Minimum API Level)");
        Need(PlayerSettings.Android.useCustomKeystore && File.Exists(PlayerSettings.Android.keystoreName), $"Нет keystore: {PlayerSettings.Android.keystoreName}");
        Need(!string.IsNullOrEmpty(ksPass), "Нет пароля keystore: задайте MF_KEYSTORE_PASS (и MF_KEY_PASS, если пароль ключа другой)");
        log.AppendLine($"Версия {version}, versionCode {code} (опубликован {PublishedVersionCode}), пакет {PlayerSettings.GetApplicationIdentifier(target)}, targetSdk {PlayerSettings.Android.targetSdkVersion}, minSdk {PlayerSettings.Android.minSdkVersion}");
        log.AppendLine($"Keystore {PlayerSettings.Android.keystoreName}, alias {PlayerSettings.Android.keyaliasName}, backend {PlayerSettings.GetScriptingBackend(target)}, архитектуры {PlayerSettings.Android.targetArchitectures}\n");
        if (problems.Length > 0) { Finish(log.AppendLine("## Сборка не начата\n\n" + problems), false); return; }

        Directory.CreateDirectory(OutDir);
        string outPath = $"{OutDir}/MatchFlame-{version}-v{code}.aab";
        bool prevAab = EditorUserBuildSettings.buildAppBundle, prevDev = EditorUserBuildSettings.development;
        var prevLevel = UnityEditor.Android.UserBuildSettings.DebugSymbols.level;
        var prevFormat = UnityEditor.Android.UserBuildSettings.DebugSymbols.format;
        BuildReport rep = null;
        try
        {
            PlayerSettings.Android.keystorePass = ksPass;   // только в памяти редактора, в ProjectSettings не сохраняется
            PlayerSettings.Android.keyaliasPass = keyPass;
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;
            UnityEditor.Android.UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;
            UnityEditor.Android.UserBuildSettings.DebugSymbols.format = Unity.Android.Types.DebugSymbolFormat.Zip | Unity.Android.Types.DebugSymbolFormat.IncludeInBundle;
            rep = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = outPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
        }
        catch (Exception e) { log.AppendLine("ИСКЛЮЧЕНИЕ: " + e); }
        finally
        {
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            EditorUserBuildSettings.buildAppBundle = prevAab;
            EditorUserBuildSettings.development = prevDev;
            UnityEditor.Android.UserBuildSettings.DebugSymbols.level = prevLevel;
            UnityEditor.Android.UserBuildSettings.DebugSymbols.format = prevFormat;
        }
        bool ok = rep != null && rep.summary.result == BuildResult.Succeeded && File.Exists(outPath);
        if (rep != null)
        {
            var s = rep.summary;
            log.AppendLine($"## Результат: {s.result}\n\nОшибок {s.totalErrors}, предупреждений {s.totalWarnings}, время {s.totalTime:hh\\:mm\\:ss}");
            log.AppendLine($"Файл: {outPath} ({(File.Exists(outPath) ? new FileInfo(outPath).Length / 1048576f : 0f):0.0} МБ)\n");
            foreach (var step in rep.steps)
                foreach (var m in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                    log.AppendLine($"- [{m.type}] {step.name}: {m.content.Split('\n')[0]}");
        }
        Finish(log, ok);
    }

    private static void Finish(StringBuilder log, bool ok)
    {
        Directory.CreateDirectory(OutDir);
        File.WriteAllText($"{OutDir}/release-build.txt", log.ToString());
        Debug.Log("[Polish] ReleaseBuild " + (ok ? "OK" : "FAILED") + "\n" + log);
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }
}
#endif
