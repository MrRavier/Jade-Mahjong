using System;
using System.IO;
using JadeMahjong.Networking;
using JadeMahjong.Runtime;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JadeMahjong.Editor
{
    [InitializeOnLoad]
    public static class JadeBuild
    {
        public const string ScenePath = "Assets/Scenes/JadeMahjong.unity";
        public const string DefaultApkPath = "Builds/Android/Jade-Mahjong.apk";

        static JadeBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !Application.isBatchMode)
                    CreateScene(false);
            };
        }

        [MenuItem("Jade Mahjong/Abrir-Criar cena")]
        public static void OpenOrCreateScene()
        {
            CreateScene(true);
        }

        [MenuItem("Jade Mahjong/Construir APK Android")]
        public static void BuildAndroid()
        {
            BuildAndroidAt(DefaultApkPath, BuildOptions.None);
        }

        public static void BuildAndroidFromCommandLine()
        {
            var path = CommandLineValue("-customBuildPath") ?? DefaultApkPath;
            BuildAndroidAt(path, BuildOptions.None);
        }

        private static void BuildAndroidAt(string outputPath, BuildOptions options)
        {
            CreateScene(false);
            ConfigurePlayer();
            var fullPath = Path.GetFullPath(outputPath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            var build = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = fullPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options
            };
            var report = BuildPipeline.BuildPlayer(build);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"Android build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            Debug.Log($"Jade Mahjong APK: {fullPath} ({report.summary.totalSize} bytes)");
        }

        private static void ConfigurePlayer()
        {
            ConfigureArtImporter("Assets/Resources/Art/Backgrounds/jade_palace.png", false);
            ConfigureArtImporter("Assets/Resources/Art/Characters/jade_emperor_sheet.png", true);
            ConfigureArtImporter("Assets/Resources/Art/App/jade_icon.png", false);
            PlayerSettings.companyName = "MrRavier";
            PlayerSettings.productName = "Jade Mahjong";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.mrravier.jademahjong");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.activeInputHandler = ActiveInputHandler.InputSystemPackage;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.forceInternetPermission = true;
            EditorUserBuildSettings.buildAppBundle = false;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Resources/Art/App/jade_icon.png");
            if (icon != null)
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { icon });
        }

        private static void CreateScene(bool focus)
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.055f, 0.07f);
            cameraObject.AddComponent<AudioListener>();

            var root = new GameObject("Jade Mahjong");
            var network = root.AddComponent<NetworkManager>();
            var transport = root.AddComponent<UnityTransport>();
            network.NetworkConfig.NetworkTransport = transport;
            root.AddComponent<LanSession>();
            root.AddComponent<JadeGameApp>();
            root.AddComponent<JadeHud>();

            ConfigurePlayer();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (focus)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Selection.activeGameObject = root;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        private static void ConfigureArtImporter(string path, bool readable)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            var changed = importer.isReadable != readable ||
                          importer.filterMode != FilterMode.Point ||
                          importer.mipmapEnabled ||
                          importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed)
                return;
            importer.isReadable = readable;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static string CommandLineValue(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }
            return null;
        }
    }
}
