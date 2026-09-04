using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public static class BuildPlayerHelper
{
    [MenuItem("Stone Skipping/📦 Build Windows Standalone (EXE)")]
    public static void BuildWindowsEXE()
    {
        ResourceSyncTool.SyncResources();

        string outputDir = Path.GetFullPath("Builds/Windows");
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        string buildPath = Path.Combine(outputDir, "StoneSkipping.exe");
        string[] scenes = { "Assets/Scenes/SampleScene.unity" };

        // 📱 모바일 세로 9:16 스마트 스냅 창모드 설정 (조절 시 9:16 비율 실시간 자동 보정)
        PlayerSettings.defaultScreenWidth = 540;
        PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        Debug.Log($"🚀 [BuildPlayerHelper] Windows EXE 빌드 시작: {buildPath}");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"✅ [BuildPlayerHelper] Windows 빌드 대성공! 파일: {buildPath} (크기: {summary.totalSize / (1024f * 1024f):F1} MB, 소요시간: {summary.totalTime.TotalSeconds:F1}초)");
            EditorUtility.RevealInFinder(buildPath);
        }
        else
        {
            Debug.LogError($"❌ [BuildPlayerHelper] Windows 빌드 실패: {summary.result}");
        }
    }

    [MenuItem("Stone Skipping/📱 Build Android (APK)")]
    public static void BuildAndroidAPK()
    {
        ExecuteAndroidBuild(false);
    }

    [MenuItem("Stone Skipping/🚀 Build and Run Android (APK to Device)")]
    public static void BuildAndRunAndroidAPK()
    {
        ExecuteAndroidBuild(true);
    }

    private static void ExecuteAndroidBuild(bool autoRun)
    {
        ResourceSyncTool.SyncResources();

        string outputDir = Path.GetFullPath("Builds/Android");
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        string buildPath = Path.Combine(outputDir, "StoneSkipping.apk");
        string[] scenes = { "Assets/Scenes/SampleScene.unity" };

        // 📱 1. 안드로이드 기본 설정 (패키지명, 세로 모드 고정, APK 생성 모드)
        PlayerSettings.companyName = "DefaultCompany";
        PlayerSettings.productName = "StoneSkipping";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.DefaultCompany.StoneSkipping");

        // 세로 화면(Portrait) 고정
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        // AAB(구글 플레이 번들) 대신 바로 설치 가능한 APK 생성
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26; // Android 8.0+
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.Android,
            options = autoRun ? BuildOptions.AutoRunPlayer : BuildOptions.None
        };

        Debug.Log($"🚀 [BuildPlayerHelper] Android APK 빌드 시작 {(autoRun ? "(기기 자동 실행 모드)" : "")}: {buildPath}");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"✅ [BuildPlayerHelper] Android APK 빌드 대성공! 파일: {buildPath} (크기: {summary.totalSize / (1024f * 1024f):F1} MB, 소요시간: {summary.totalTime.TotalSeconds:F1}초)");
            EditorUtility.RevealInFinder(buildPath);
        }
        else
        {
            Debug.LogError($"❌ [BuildPlayerHelper] Android APK 빌드 실패: {summary.result}");
        }
    }

    [MenuItem("Stone Skipping/🍎 Build iOS (Xcode Project)")]
    public static void BuildIOSProject()
    {
        ResourceSyncTool.SyncResources();

        string outputDir = Path.GetFullPath("Builds/iOS");
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        string[] scenes = { "Assets/Scenes/SampleScene.unity" };

        // 📱 iOS 기본 설정 (Bundle ID, 세로 모드 고정, Target iOS 13.0+)
        PlayerSettings.companyName = "DefaultCompany";
        PlayerSettings.productName = "StoneSkipping";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.DefaultCompany.StoneSkipping");

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.targetOSVersionString = "13.0";

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputDir,
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };

        Debug.Log($"🚀 [BuildPlayerHelper] iOS Xcode 프로젝트 빌드 시작: {outputDir}");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"✅ [BuildPlayerHelper] iOS 빌드 성공! 폴더: {outputDir} (소요시간: {summary.totalTime.TotalSeconds:F1}초)");
            EditorUtility.RevealInFinder(outputDir);
        }
        else
        {
            Debug.LogError($"❌ [BuildPlayerHelper] iOS 빌드 실패: {summary.result}");
        }
    }
}
