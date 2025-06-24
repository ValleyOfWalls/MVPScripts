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
                 "2. Comment out ONLY Debug.Log statements (NOT warnings, errors, or exceptions)\n" +
                 "3. Use /* */ block comments to preserve other code on the same line\n" +
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
                         int totalMatches = changes.Sum(c => c.DebugLogPositions.Count);
                         Debug.Log($"\n{filePath} - {totalMatches} Debug.Log statements found:");
                         foreach (var change in changes)
                         {
                             Debug.Log($"  Line {change.LineNumber}: {change.OriginalLine.Trim()}");
                             foreach (var position in change.DebugLogPositions)
                             {
                                 Debug.Log($"    -> {position.Statement}");
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
                         int totalMatches = changes.Sum(c => c.DebugLogPositions.Count);
                         
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
            
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                
                // Skip if line is already commented out
                if (line.TrimStart().StartsWith("//"))
                    continue;
                
                // Find all Debug.Log matches in this line using a more robust method
                var matches = FindDebugLogInLine(line);
                
                if (matches.Count > 0)
                {
                    changes.Add(new DebugLogChange
                    {
                        LineNumber = i + 1,
                        OriginalLine = line,
                        DebugLogPositions = matches
                    });
                }
            }
            
            return changes;
        }
        
        private List<DebugLogPosition> FindDebugLogInLine(string line)
        {
            var positions = new List<DebugLogPosition>();
            
            // Only look for Debug.Log (not warnings, errors, or exceptions)
            string[] debugMethods = { "Debug.Log(" };
            
            foreach (string method in debugMethods)
            {
                int searchStart = 0;
                while (true)
                {
                    int startIndex = line.IndexOf(method, searchStart, StringComparison.OrdinalIgnoreCase);
                    if (startIndex == -1)
                        break;
                    
                    // Find the matching closing parenthesis
                    int parenCount = 1;
                    int currentIndex = startIndex + method.Length;
                    bool inString = false;
                    bool inChar = false;
                    bool escaped = false;
                    
                    while (currentIndex < line.Length && parenCount > 0)
                    {
                        char c = line[currentIndex];
                        
                        if (escaped)
                        {
                            escaped = false;
                        }
                        else if (c == '\\')
                        {
                            escaped = true;
                        }
                        else if (!inChar && c == '"' && !escaped)
                        {
                            inString = !inString;
                        }
                        else if (!inString && c == '\'' && !escaped)
                        {
                            inChar = !inChar;
                        }
                        else if (!inString && !inChar)
                        {
                            if (c == '(')
                                parenCount++;
                            else if (c == ')')
                                parenCount--;
                        }
                        
                        currentIndex++;
                    }
                    
                    // If we found a complete Debug.Log statement
                    if (parenCount == 0)
                    {
                        // Check if there's a semicolon right after
                        int endIndex = currentIndex;
                        if (endIndex < line.Length && line[endIndex] == ';')
                            endIndex++;
                        
                        positions.Add(new DebugLogPosition
                        {
                            StartIndex = startIndex,
                            EndIndex = endIndex,
                            Statement = line.Substring(startIndex, endIndex - startIndex)
                        });
                        
                        searchStart = endIndex;
                    }
                    else
                    {
                        // Incomplete statement, skip this occurrence
                        searchStart = startIndex + method.Length;
                    }
                }
            }
            
            return positions;
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
                    
                    // Process positions in reverse order to maintain string positions
                    var sortedPositions = change.DebugLogPositions.OrderByDescending(p => p.StartIndex).ToList();
                    
                    foreach (var position in sortedPositions)
                    {
                        // Comment out just the Debug.Log statement
                        string beforeDebug = modifiedLine.Substring(0, position.StartIndex);
                        string debugStatement = position.Statement;
                        string afterDebug = modifiedLine.Substring(position.EndIndex);
                        
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
            public List<DebugLogPosition> DebugLogPositions { get; set; } = new List<DebugLogPosition>();
        }
        
        private class DebugLogPosition
        {
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public string Statement { get; set; }
        }
    }
} 