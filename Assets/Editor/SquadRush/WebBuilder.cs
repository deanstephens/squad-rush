using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SquadRush.EditorTools
{
    /// <summary>Configures and runs the WebGL (GitHub Pages) build. Menu: SquadRush > Build Web, or headless via PerformBuild.</summary>
    public static class WebBuilder
    {
        public const string OutputDir = "Build/Web";

        public static void ConfigureWebSettings()
        {
            // GitHub Pages does not send Content-Encoding for .gz/.br, so ship uncompressed files.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = "PROJECT:Mobile";
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetIl2CppCompilerConfiguration(UnityEditor.Build.NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Release);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("SquadRush/Build Web")]
        public static void PerformBuild()
        {
            ConfigureWebSettings();
            Directory.CreateDirectory(OutputDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity", "Assets/Scenes/Arena.unity" },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[WebBuilder] {s.result} in {s.totalTime.TotalSeconds:0}s, {s.totalSize / (1024 * 1024)} MB, errors={s.totalErrors}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
