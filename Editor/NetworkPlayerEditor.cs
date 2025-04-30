using UnityEngine;
using UnityEditor; // Required for Editor scripts
using System.Collections.Generic; // Required for List

// This attribute tells Unity to use this script to draw the Inspector for the NetworkPlayer class
[CustomEditor(typeof(NetworkPlayer))]
public class NetworkPlayerEditor : Editor
{
    // Private field to track foldout state
    private bool showPersistentDeck = true; 

    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields (like Steam Name, Steam ID, etc.)
        DrawDefaultInspector();

        // Get the NetworkPlayer component being inspected
        NetworkPlayer networkPlayer = (NetworkPlayer)target;

        // Only show the deck list if the application is running
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(); // Add some visual spacing
            
            // Use a foldout for better organization
            showPersistentDeck = EditorGUILayout.Foldout(showPersistentDeck, "Persistent Deck IDs");

            if (showPersistentDeck)
            {
                EditorGUI.indentLevel++; // Indent the list content
                
                // Display the current card count
                EditorGUILayout.LabelField("Card Count:", networkPlayer.persistentDeckCardIDs.Count.ToString());

                // Display each card ID in the list
                if (networkPlayer.persistentDeckCardIDs.Count > 0)
                {
                    for (int i = 0; i < networkPlayer.persistentDeckCardIDs.Count; i++)
                    {
                        EditorGUILayout.LabelField($"Card {i}:", networkPlayer.persistentDeckCardIDs[i]);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("(Deck is empty or not yet synced)");
                }
                
                EditorGUI.indentLevel--; // Reset indentation
            }
        }
        else
        {
            // Show a message when not playing
            EditorGUILayout.HelpBox("Persistent Deck contents are only visible during runtime.", MessageType.Info);
        }
    }
} 