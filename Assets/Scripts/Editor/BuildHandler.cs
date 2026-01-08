using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public class BuildHandler
{
    [MenuItem("Build/Windows Standalone")]
    public static void BuildWindowsEXE()
    {
        string buildPath = "Builds/RobotSim.exe";
        string buildFolder = Path.GetDirectoryName(buildPath);

        // Ensure build directory exists
        if (!Directory.Exists(buildFolder))
        {
            Directory.CreateDirectory(buildFolder);
        }

        // Define build options
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = new[] { "Assets/Scenes/SampleScene.unity" };
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
        buildPlayerOptions.options = BuildOptions.None;

        // Configure Player Settings for Windowed Mode
        PlayerSettings.resizableWindow = true;
        PlayerSettings.defaultIsFullScreen = false;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.runInBackground = true; 

        // Execute Build
        Debug.Log("[BuildHandler] Starting Windows Build...");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildHandler] Build Succeeded: {summary.totalSize} bytes");
            Debug.Log($"[BuildHandler] Output: {Path.GetFullPath(buildPath)}");
            
            // Open the build folder
            EditorUtility.RevealInFinder(buildPath);
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError("[BuildHandler] Build Failed");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    Debug.LogError($"[{step.name}] {msg.content}");
                }
            }
        }
    }
}
