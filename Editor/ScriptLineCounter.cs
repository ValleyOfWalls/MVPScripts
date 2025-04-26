using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class ScriptLineCounter
{
    [MenuItem("Tools/Report Script Line Counts (Scripts Folder Only)")]
    private static void ReportScriptLineCounts()
    {
        string assetsPath = Application.dataPath;
        string scriptsFolderPath = Path.Combine(assetsPath, "Scripts");

        if (!Directory.Exists(scriptsFolderPath))
        {
            Debug.LogWarning("Assets/Scripts folder not found.");
            return;
        }

        string[] scriptFiles = Directory.GetFiles(scriptsFolderPath, "*.cs", SearchOption.AllDirectories);

        if (scriptFiles.Length == 0)
        {
            Debug.Log("No C# script files found in the Assets/Scripts folder.");
            return;
        }

        Debug.Log($"--- Script Line Counts ({scriptFiles.Length} files in Assets/Scripts) ---");

        List<KeyValuePair<string, int>> fileLineCounts = new List<KeyValuePair<string, int>>();

        foreach (string filePath in scriptFiles)
        {
            try
            {
                int lineCount = File.ReadAllLines(filePath).Length;
                string relativePath = "Assets" + filePath.Substring(assetsPath.Length);
                relativePath = relativePath.Replace('\\', '/');
                fileLineCounts.Add(new KeyValuePair<string, int>(relativePath, lineCount));
            }
            catch (System.Exception ex)
            {
                string relativePathError = "Assets" + filePath.Substring(assetsPath.Length);
                relativePathError = relativePathError.Replace('\\', '/');
                Debug.LogWarning($"Could not process file {relativePathError}: {ex.Message}");
            }
        }

        fileLineCounts = fileLineCounts.OrderByDescending(pair => pair.Value).ToList();

        foreach (var pair in fileLineCounts)
        {
            Debug.Log($"{pair.Key}: {pair.Value} lines");
        }

        Debug.Log("--- End of Report ---");
    }
} 