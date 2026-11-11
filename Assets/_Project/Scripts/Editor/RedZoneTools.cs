using System.IO;
using RedZone.Game;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace RedZone.EditorTools
{
    /// <summary>
    /// The scene is generated (Wilkins Pest Control's rule: change this builder, not the scene by hand). It holds only
    /// a camera, the event system and the [RedZone] object; everything else is built at runtime by GameUI.
    /// Menu "Red Zone": rebuild the scene, build WebGL (itch.io) and a Windows build for the robot playtest.
    /// </summary>
    public static class RedZoneTools
    {
        public const string ScenePath = "Assets/_Project/Scenes/RedZone.unity";
        const string Status = "Builds/build_status.txt";

        [MenuItem("Red Zone/Rebuild Scene")]
        public static void RebuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            var c = cam.GetComponent<Camera>();
            c.orthographic = true; c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.039f, 0.051f, 0.102f);
            cam.transform.position = new Vector3(0, 0, -10);

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().deselectOnBackgroundClick = false;

            new GameObject("[RedZone]", typeof(GameFlow));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[RedZone] scene rebuilt: " + ScenePath);
        }

        static void Common()
        {
            if (!File.Exists(ScenePath)) RebuildScene();
            PlayerSettings.companyName = "Bobagi";
            PlayerSettings.productName = "Red Zone";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.SplashScreen.show = false;
        }

        [MenuItem("Red Zone/Build WebGL")]
        public static void BuildWebGL()
        {
            Common();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            // speed: a lean wasm (smaller download, faster start), Master IL2CPP, the GPU's performance profile
            PlayerSettings.SetIl2CppCompilerConfiguration(UnityEditor.Build.NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.stripEngineCode = true;
            Build(BuildTarget.WebGL, "Builds/WebGL");
        }

        [MenuItem("Red Zone/Build Windows")]
        public static void BuildWindows()
        {
            Common();
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/RedZone.exe");
        }

        static void Build(BuildTarget target, string path)
        {
            Directory.CreateDirectory("Builds");
            File.WriteAllText(Status, "building " + target + " " + System.DateTime.Now.ToString("s") + "\n");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = path, target = target, options = BuildOptions.None,
            });
            var s = report.summary;
            File.AppendAllText(Status, s.result + " " + s.totalSize / 1024 / 1024 + " MB, errors " + s.totalErrors + ", warnings " + s.totalWarnings + ", " + s.totalTime + "\n");
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) File.AppendAllText(Status, "ERR " + m.content + "\n");
            Debug.Log("[RedZone] build " + target + " " + s.result);
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }

    /// <summary>Import rules by folder: music streams as Vorbis, effects are small and decompressed on load.</summary>
    public sealed class RedZoneAudioImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/_Project/Audio")) return;
            var imp = (AudioImporter)assetImporter;
            var s = imp.defaultSampleSettings;
            if (assetPath.Contains("/Music/"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
            }
            else
            {
                imp.forceToMono = true;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
            }
            imp.defaultSampleSettings = s;
        }
    }
}
