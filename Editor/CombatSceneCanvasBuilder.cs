using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace Combat
{
    public class CombatSceneCanvasBuilder
    {
        [MenuItem("Tools/Combat/Create Combat Scene Canvas")]
        public static void CreateCombatCanvas()
        {
            // --- Root Object ---
            GameObject combatSceneCanvasGO = new GameObject("CombatSceneCanvas");
            // Note: We don't add CombatSceneCanvas.cs as it's just a guide/placeholder

            // --- Canvas Child ---
            GameObject canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(combatSceneCanvasGO.transform);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; // Default render mode
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // --- Battle Info Panel ---
            GameObject battleInfoPanelGO = CreateRectTransformPanel("BattleInfoPanel", canvasGO.transform);

            // --- Battle Info Text ---
            GameObject battleInfoTextGO = new GameObject("BattleInfoText");
            battleInfoTextGO.transform.SetParent(battleInfoPanelGO.transform);
            TextMeshProUGUI battleInfoText = battleInfoTextGO.AddComponent<TextMeshProUGUI>();
            battleInfoText.text = "{player} vs {opponent}'s Pet";
            battleInfoText.alignment = TextAlignmentOptions.Center;
            // Basic RectTransform setup (adjust as needed)
            SetupRectTransform(battleInfoTextGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 50));


            // --- Next Battle Button ---
            GameObject nextBattleButtonGO = CreateButton("NextBattleButton", battleInfoPanelGO.transform, "View Next Battle");
            // Add icon or visual indicator for cycling
            Image buttonImage = nextBattleButtonGO.GetComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.9f); // Blue color for visibility
            // Position the button to the right of the battle info text
            RectTransform buttonRect = nextBattleButtonGO.GetComponent<RectTransform>();
            SetupRectTransform(buttonRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(150, 40));
            buttonRect.anchoredPosition = new Vector2(-80, 0); // Offset from right edge


            // --- Battle View Container ---
            GameObject battleViewContainerGO = CreateRectTransformPanel("BattleViewContainer", canvasGO.transform);
            // Set anchors/stretch for the container if it should fill a part of the screen
            // SetupRectTransform(battleViewContainerGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20), new Vector2(20, 80)); // Example stretch with padding


            // --- Player Area ---
            GameObject playerAreaGO = CreateRectTransformPanel("PlayerArea", battleViewContainerGO.transform);

            // --- Pet Area ---
            GameObject petAreaGO = CreateRectTransformPanel("PetArea", battleViewContainerGO.transform);

            // --- Battlefield Area ---
            GameObject battlefieldAreaGO = CreateRectTransformPanel("BattlefieldArea", battleViewContainerGO.transform);

            // --- End Turn Button ---
            GameObject endTurnButtonGO = CreateButton("EndTurnButton", canvasGO.transform, "End Turn");
            // Adjust position as needed, perhaps bottom-right
             SetupRectTransform(endTurnButtonGO.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(160, 30), new Vector2(-10, 10)); // Example bottom right

            Debug.Log("CombatSceneCanvas hierarchy created in the current scene. Remember to adjust layout, styling, and create a prefab.");
             // Optional: Select the created root object
            Selection.activeGameObject = combatSceneCanvasGO;

            // --- Add Combat Scene Canvas Component ---
            // This is the script we created to manage cycling through battles
            combatSceneCanvasGO.AddComponent<Combat.CombatSceneCanvas>();
        }

        // Helper to create a basic panel GameObject with a RectTransform
        private static GameObject CreateRectTransformPanel(string name, Transform parent)
        {
            GameObject panelGO = new GameObject(name);
            panelGO.AddComponent<RectTransform>();
            panelGO.transform.SetParent(parent);
            // Reset local scale often needed when parenting UI
            panelGO.transform.localScale = Vector3.one;
            return panelGO;
        }

        // Helper to create a basic Button with TextMeshPro text
        private static GameObject CreateButton(string name, Transform parent, string buttonText)
        {
            // Default Controls.CreateButton uses standard Text, we want TMP
             // So we create elements manually

             // Create Button GameObject
            GameObject buttonGO = new GameObject(name);
            buttonGO.transform.SetParent(parent);
            buttonGO.transform.localScale = Vector3.one;
            // Add Image for button background (optional, but typical)
            buttonGO.AddComponent<Image>();
            Button button = buttonGO.AddComponent<Button>();

             // Create Text GameObject for the button text
            GameObject textGO = new GameObject("ButtonText");
            textGO.transform.SetParent(buttonGO.transform);
            textGO.transform.localScale = Vector3.one;

            TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>();
            tmpText.text = buttonText;
            tmpText.alignment = TextAlignmentOptions.Center;
            tmpText.color = Color.black; // Default text color

            // Set Text Anchor Presets to stretch
            SetupRectTransform(textGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);


             // Basic RectTransform for button itself (adjust as needed)
            SetupRectTransform(buttonGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(160, 30));


             // Set the target graphic for the button interaction
            button.targetGraphic = buttonGO.GetComponent<Image>();


            return buttonGO;
        }

         // Helper to setup RectTransform properties
        private static void SetupRectTransform(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2? anchoredPosition = null)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            if (anchoredPosition.HasValue)
            {
                rt.anchoredPosition = anchoredPosition.Value;
            }
            else // Center based on anchors if no specific position given and not stretching
            {
                 if(anchorMin == anchorMax)
                     rt.anchoredPosition = Vector2.zero;
            }
        }
         // Overload for stretching (sets offsetMin/Max instead of sizeDelta)
        private static void SetupRectTransform(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
        {
             rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.offsetMin = offsetMin; // Distance from left/bottom anchors
            rt.offsetMax = offsetMax; // Negative distance from top/right anchors
            rt.anchoredPosition = Vector2.zero; // Position relative to anchors
            rt.sizeDelta = Vector2.zero; // Size relative to anchors (zero when stretching)
        }
    }
} 