using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class ScriptExporter
{
    [MenuItem("Tools/Export Script Names")]
    public static void ShowExportDialog()
    {
        string path = EditorUtility.OpenFolderPanel("Select Folder to Export Scripts From", "", "");

        if (!string.IsNullOrEmpty(path))
        {
            List<string> csFiles = GetCsFilesFromFolder(path);
            if (csFiles.Count > 0)
            {
                Debug.Log($"Found {csFiles.Count} C# files in {path} and its subfolders:\n{string.Join("\n", csFiles)}");
            }
            else
            {
                Debug.LogWarning($"No C# files found in {path} and its subfolders.");
            }
        }
        else
        {
            Debug.Log("Folder selection was cancelled.");
        }
    }

    private static List<string> GetCsFilesFromFolder(string folderPath)
    {
        List<string> fileNames = new List<string>();
        if (Directory.Exists(folderPath))
        {
            string[] files = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);
            foreach (string filePath in files)
            {
                fileNames.Add(Path.GetFileName(filePath));
            }
        }
        else
        {
            Debug.LogError("Selected path does not exist: " + folderPath);
        }
        return fileNames;
    }
} 