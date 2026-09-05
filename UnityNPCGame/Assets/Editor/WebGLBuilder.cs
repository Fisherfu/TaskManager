#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NPCGame.EditorTools
{
    /// <summary>
    /// Build entry point for CI (game-ci passes this as -executeMethod). Generates the
    /// sample scene first so a fresh clone can build without anyone opening the Editor.
    /// </summary>
    public static class WebGLBuilder
    {
        private const string DefaultBuildPath = "build/WebGL/WebGL";

        public static void Build()
        {
            SceneBuilder.EnsureSampleScene();

            string buildPath = ResolveBuildPath();
            string[] scenes = GetEnabledScenes();

            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuilder] No enabled scenes in build settings - aborting.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[WebGLBuilder] Building {scenes.Length} scene(s) to '{buildPath}'.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGLBuilder] Succeeded: {summary.totalSize} bytes at '{summary.outputPath}'.");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[WebGLBuilder] Failed with result '{summary.result}' ({summary.totalErrors} error(s)).");
                EditorApplication.Exit(1);
            }
        }

        private static string[] GetEnabledScenes()
        {
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && File.Exists(scene.path))
                {
                    scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }

        /// <summary>
        /// game-ci hands custom build methods the output path via -customBuildPath.
        /// Fall back to the path the workflow uploads so a local run still works.
        /// </summary>
        private static string ResolveBuildPath()
        {
            string[] args = System.Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-customBuildPath")
                {
                    string value = args[i + 1].TrimEnd('/', '\\');
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            return Path.GetFullPath(DefaultBuildPath);
        }
    }
}
#endif
