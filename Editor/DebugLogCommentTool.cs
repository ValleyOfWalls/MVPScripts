using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MVPScripts.Editor
{
    public class DebugLogCommentTool : EditorWindow
    {
        private string targetFolderPath = "Assets/MVPScripts";
        private bool createBackups = true;
        private bool processSubfolders = true;
        private Vector2 scrollPosition;
        private List<string> processedFiles = new List<string>();
        private List<string> errorFiles = new List<string>();
        private bool showResults = false;
        
        [MenuItem("Tools/Debug Log Comment Tool")]
        public static void ShowWindow()
        {
            GetWindow<DebugLogCommentTool>("Debug Log Comment Tool");
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Debug Log Comment Tool", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
                         EditorGUILayout.HelpBox(
                 "This tool will:\n" +
                 "1. Create backups of all C# scripts (if enabled)\n" +
                 "2. Comment out Debug.Log statements using /* */ block comments\n" +
                 "3. Only comment the Debug.Log statement itself, preserving other code on the same line\n" +
                 "4. Process all subfolders if enabled\n" +
                 "5. Handle multiple Debug.Log statements on the same line",
                 MessageType.Info);
            
            GUILayout.Space(10);
            
            // Settings
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            
            targetFolderPath = EditorGUILayout.TextField("Target Folder Path:", targetFolderPath);
            createBackups = EditorGUILayout.Toggle("Create Backups", createBackups);
            processSubfolders = EditorGUILayout.Toggle("Process Subfolders", processSubfolders);
            
            GUILayout.Space(10);
            
            // Action buttons
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Preview Changes", GUILayout.Height(30)))
            {
                PreviewChanges();
            }
            
            if (GUILayout.Button("Process Files", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Confirm Processing", 
                    "This will modify your script files. Make sure you have backups or version control!\n\nProceed?", 
                    "Yes", "Cancel"))
                {
                    ProcessFiles();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("Restore from Backups"))
            {
                RestoreFromBackups();
            }
            
            GUILayout.Space(10);
            
            // Results display
            if (showResults)
            {
                EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                
                if (processedFiles.Count > 0)
                {
                    EditorGUILayout.LabelField($"Successfully Processed ({processedFiles.Count}):", EditorStyles.boldLabel);
                    foreach (string file in processedFiles)
                    {
                        EditorGUILayout.LabelField($"✓ {file}", EditorStyles.miniLabel);
                    }
                }
                
                if (errorFiles.Count > 0)
                {
                    EditorGUILayout.LabelField($"Errors ({errorFiles.Count}):", EditorStyles.boldLabel);
                    foreach (string file in errorFiles)
                    {
                        EditorGUILayout.LabelField($"✗ {file}", EditorStyles.miniLabel);
                    }
                }
                
                EditorGUILayout.EndScrollView();
            }
        }
        
        private void PreviewChanges()
        {
            processedFiles.Clear();
            errorFiles.Clear();
            
            string[] scriptFiles = GetScriptFiles();
            
            Debug.Log($"=== DEBUG LOG COMMENT TOOL - PREVIEW ===");
            Debug.Log($"Found {scriptFiles.Length} C# files to analyze");
            
            int totalChanges = 0;
            
            foreach (string filePath in scriptFiles)
            {
                try
                {
                    string content = File.ReadAllText(filePath);
                    var changes = FindDebugLogStatements(content);
                    
                                         if (changes.Count > 0)
                     {
                         int totalMatches = changes.Sum(c => c.Matches.Count);
                         Debug.Log($"\n{filePath} - {totalMatches} Debug.Log statements found:");
                         foreach (var change in changes)
                         {
                             Debug.Log($"  Line {change.LineNumber}: {change.OriginalLine.Trim()}");
                             foreach (var match in change.Matches)
                             {
                                 Debug.Log($"    -> {match.Value}");
                             }
                         }
                         processedFiles.Add($"{Path.GetFileName(filePath)} ({totalMatches} changes)");
                         totalChanges += totalMatches;
                     }
                }
                catch (Exception ex)
                {
                    errorFiles.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
            
            Debug.Log($"\n=== PREVIEW SUMMARY ===");
            Debug.Log($"Files with Debug.Log statements: {processedFiles.Count}");
            Debug.Log($"Total Debug.Log statements to comment: {totalChanges}");
            Debug.Log($"Files with errors: {errorFiles.Count}");
            
            showResults = true;
        }
        
        private void ProcessFiles()
        {
            processedFiles.Clear();
            errorFiles.Clear();
            
            string[] scriptFiles = GetScriptFiles();
            
            Debug.Log($"=== DEBUG LOG COMMENT TOOL - PROCESSING ===");
            Debug.Log($"Processing {scriptFiles.Length} C# files...");
            
            int totalChanges = 0;
            int filesModified = 0;
            
            foreach (string filePath in scriptFiles)
            {
                try
                {
                    string content = File.ReadAllText(filePath);
                    var changes = FindDebugLogStatements(content);
                    
                                         if (changes.Count > 0)
                     {
                         int totalMatches = changes.Sum(c => c.Matches.Count);
                         
                         // Create backup if enabled
                         if (createBackups)
                         {
                             CreateBackup(filePath);
                         }
                         
                         // Apply changes
                         string modifiedContent = ApplyChanges(content, changes);
                         File.WriteAllText(filePath, modifiedContent);
                         
                         processedFiles.Add($"{Path.GetFileName(filePath)} ({totalMatches} changes)");
                         totalChanges += totalMatches;
                         filesModified++;
                         
                         Debug.Log($"Modified {filePath} - {totalMatches} Debug.Log statements commented out");
                     }
                }
                catch (Exception ex)
                {
                    errorFiles.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                    Debug.LogError($"Error processing {filePath}: {ex.Message}");
                }
            }
            
            // Refresh the asset database
            AssetDatabase.Refresh();
            
            Debug.Log($"\n=== PROCESSING COMPLETE ===");
            Debug.Log($"Files modified: {filesModified}");
            Debug.Log($"Total Debug.Log statements commented: {totalChanges}");
            Debug.Log($"Files with errors: {errorFiles.Count}");
            
            showResults = true;
            
            EditorUtility.DisplayDialog("Processing Complete", 
                $"Successfully processed {filesModified} files.\n" +
                $"Commented out {totalChanges} Debug.Log statements.\n" +
                $"Errors: {errorFiles.Count}\n\n" +
                "Check the Console for detailed results.", "OK");
        }
        
        private void RestoreFromBackups()
        {
            string backupFolder = Path.Combine(Application.dataPath, "MVPScripts_Backups");
            
            if (!Directory.Exists(backupFolder))
            {
                EditorUtility.DisplayDialog("No Backups Found", 
                    "No backup folder found. Backups are created in Assets/MVPScripts_Backups/", "OK");
                return;
            }
            
            if (!EditorUtility.DisplayDialog("Restore from Backups", 
                "This will restore all files from the backup folder, overwriting current files.\n\nProceed?", 
                "Yes", "Cancel"))
            {
                return;
            }
            
            try
            {
                string[] backupFiles = Directory.GetFiles(backupFolder, "*.cs.backup", SearchOption.AllDirectories);
                int restoredCount = 0;
                
                foreach (string backupFile in backupFiles)
                {
                    string relativePath = backupFile.Substring(backupFolder.Length + 1);
                    relativePath = relativePath.Replace(".backup", "");
                    string originalFile = Path.Combine(targetFolderPath, relativePath);
                    
                    if (File.Exists(originalFile))
                    {
                        File.Copy(backupFile, originalFile, true);
                        restoredCount++;
                    }
                }
                
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("Restore Complete", 
                    $"Restored {restoredCount} files from backups.", "OK");
                    
                Debug.Log($"Restored {restoredCount} files from backups");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Restore Failed", 
                    $"Error during restore: {ex.Message}", "OK");
                Debug.LogError($"Error during restore: {ex.Message}");
            }
        }
        
        private string[] GetScriptFiles()
        {
            SearchOption searchOption = processSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.GetFiles(targetFolderPath, "*.cs", searchOption)
                           .Where(f => !f.EndsWith(".backup"))
                           .ToArray();
        }
        
        private List<DebugLogChange> FindDebugLogStatements(string content)
        {
            var changes = new List<DebugLogChange>();
            string[] lines = content.Split('\n');
            
            // Regex pattern to match Debug.Log statements and capture the full statement
            // This pattern looks for Debug.Log, Debug.LogError, Debug.LogWarning, etc.
            string pattern = @"(Debug\.(Log|LogError|LogWarning|LogException)\s*\([^)]*\)\s*;?)";
            Regex debugLogRegex = new Regex(pattern, RegexOptions.IgnoreCase);
            
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                
                // Skip if line is already commented out
                if (line.TrimStart().StartsWith("//"))
                    continue;
                
                // Find all Debug.Log matches in this line
                MatchCollection matches = debugLogRegex.Matches(line);
                
                if (matches.Count > 0)
                {
                    changes.Add(new DebugLogChange
                    {
                        LineNumber = i + 1,
                        OriginalLine = line,
                        Matches = matches.Cast<Match>().ToList()
                    });
                }
            }
            
            return changes;
        }
        
        private string ApplyChanges(string content, List<DebugLogChange> changes)
        {
            string[] lines = content.Split('\n');
            
            foreach (var change in changes)
            {
                int lineIndex = change.LineNumber - 1;
                if (lineIndex >= 0 && lineIndex < lines.Length)
                {
                    string line = lines[lineIndex];
                    string modifiedLine = line;
                    
                    // Process matches in reverse order to maintain string positions
                    var sortedMatches = change.Matches.OrderByDescending(m => m.Index).ToList();
                    
                    foreach (Match match in sortedMatches)
                    {
                        // Comment out just the Debug.Log statement
                        string beforeDebug = modifiedLine.Substring(0, match.Index);
                        string debugStatement = match.Value;
                        string afterDebug = modifiedLine.Substring(match.Index + match.Length);
                        
                        // Replace the Debug statement with commented version
                        modifiedLine = beforeDebug + "/* " + debugStatement + " */" + afterDebug;
                    }
                    
                    lines[lineIndex] = modifiedLine;
                }
            }
            
            return string.Join("\n", lines);
        }
        
        private void CreateBackup(string filePath)
        {
            try
            {
                string backupFolder = Path.Combine(Application.dataPath, "MVPScripts_Backups");
                
                // Create backup directory structure
                string relativePath = Path.GetRelativePath(targetFolderPath, filePath);
                string backupPath = Path.Combine(backupFolder, relativePath + ".backup");
                string backupDir = Path.GetDirectoryName(backupPath);
                
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }
                
                // Copy file to backup location
                File.Copy(filePath, backupPath, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create backup for {filePath}: {ex.Message}");
            }
        }
        
        private class DebugLogChange
        {
            public int LineNumber { get; set; }
            public string OriginalLine { get; set; }
            public List<Match> Matches { get; set; } = new List<Match>();
        }
    }
} 