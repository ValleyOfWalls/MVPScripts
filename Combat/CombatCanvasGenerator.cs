using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace Combat
{
    public class CombatCanvasGenerator : MonoBehaviour
    {
        [Header("Canvas Settings")]
        [SerializeField] private string outputPath = "Assets/Prefabs/Combat";
        
        #if UNITY_EDITOR
        [MenuItem("Tools/Combat/Generate Combat Canvas")]
        public static void GenerateCombatCanvas()
        {
            CombatCanvasGenerator generator = FindFirstObjectByType<CombatCanvasGenerator>();
            if (generator != null)
            {
                generator.GenerateCanvas();
            }
            else
            {
                Debug.LogError("CombatCanvasGenerator component not found in scene!");
            }
        }
        
        public void GenerateCanvas()
        {
            // Create output directory if it doesn't exist
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            
            // Create the canvas structure
            GameObject canvas = CreateCombatCanvas();
            
            // Save the canvas as a prefab
            SaveCanvasPrefab(canvas);
            
            Debug.Log("Combat canvas generated successfully!");
        }
        
        private GameObject CreateCombatCanvas()
        {
            // Create the root canvas object
            GameObject canvasObj = new GameObject("CombatSceneCanvas");
            
            // Add required components
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            CanvasGroup canvasGroup = canvasObj.AddComponent<CanvasGroup>();
            
            // Create the main sections of the canvas
            CreatePlayerArea(canvasObj.transform);
            CreateOpponentArea(canvasObj.transform);
            CreateCombatControlsArea(canvasObj.transform);
            CreateResultPanel(canvasObj.transform);
            
            return canvasObj;
        }
        
        private void CreatePlayerArea(Transform parent)
        {
            // Create player area container
            GameObject playerArea = new GameObject("PlayerArea");
            playerArea.transform.SetParent(parent, false);
            RectTransform rectTransform = playerArea.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 0.4f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Create pet container
            GameObject petContainer = new GameObject("PlayerPetContainer");
            petContainer.transform.SetParent(playerArea.transform, false);
            RectTransform petRect = petContainer.AddComponent<RectTransform>();
            petRect.anchorMin = new Vector2(0.3f, 0.1f);
            petRect.anchorMax = new Vector2(0.7f, 0.9f);
            petRect.offsetMin = Vector2.zero;
            petRect.offsetMax = Vector2.zero;
            
            // Create player card hand area
            GameObject handArea = new GameObject("PlayerHandArea");
            handArea.transform.SetParent(playerArea.transform, false);
            RectTransform handRect = handArea.AddComponent<RectTransform>();
            handRect.anchorMin = new Vector2(0.1f, 0);
            handRect.anchorMax = new Vector2(0.9f, 0.3f);
            handRect.offsetMin = Vector2.zero;
            handRect.offsetMax = Vector2.zero;
            
            // Create energy display
            GameObject energyDisplay = new GameObject("EnergyDisplay");
            energyDisplay.transform.SetParent(playerArea.transform, false);
            RectTransform energyRect = energyDisplay.AddComponent<RectTransform>();
            energyRect.anchorMin = new Vector2(0.05f, 0.4f);
            energyRect.anchorMax = new Vector2(0.2f, 0.6f);
            energyRect.offsetMin = Vector2.zero;
            energyRect.offsetMax = Vector2.zero;
            
            // Create energy text
            TextMeshProUGUI energyText = energyDisplay.AddComponent<TextMeshProUGUI>();
            energyText.text = "Energy: 3/3";
            energyText.fontSize = 24;
            energyText.alignment = TextAlignmentOptions.Center;
            energyText.color = Color.yellow;
        }
        
        private void CreateOpponentArea(Transform parent)
        {
            // Create opponent area container
            GameObject opponentArea = new GameObject("OpponentArea");
            opponentArea.transform.SetParent(parent, false);
            RectTransform rectTransform = opponentArea.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0.6f);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Create pet container
            GameObject petContainer = new GameObject("OpponentPetContainer");
            petContainer.transform.SetParent(opponentArea.transform, false);
            RectTransform petRect = petContainer.AddComponent<RectTransform>();
            petRect.anchorMin = new Vector2(0.3f, 0.1f);
            petRect.anchorMax = new Vector2(0.7f, 0.9f);
            petRect.offsetMin = Vector2.zero;
            petRect.offsetMax = Vector2.zero;
            
            // Create opponent status area
            GameObject statusArea = new GameObject("OpponentStatusArea");
            statusArea.transform.SetParent(opponentArea.transform, false);
            RectTransform statusRect = statusArea.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.75f, 0.1f);
            statusRect.anchorMax = new Vector2(0.95f, 0.3f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            
            // Add status text
            TextMeshProUGUI statusText = statusArea.AddComponent<TextMeshProUGUI>();
            statusText.text = "Opponent's Pet";
            statusText.fontSize = 20;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.white;
        }
        
        private void CreateCombatControlsArea(Transform parent)
        {
            // Create combat controls container
            GameObject controlsArea = new GameObject("CombatControlsArea");
            controlsArea.transform.SetParent(parent, false);
            RectTransform rectTransform = controlsArea.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0.4f);
            rectTransform.anchorMax = new Vector2(1, 0.6f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Create turn indicator text
            GameObject turnIndicator = new GameObject("TurnIndicator");
            turnIndicator.transform.SetParent(controlsArea.transform, false);
            RectTransform turnRect = turnIndicator.AddComponent<RectTransform>();
            turnRect.anchorMin = new Vector2(0.4f, 0.6f);
            turnRect.anchorMax = new Vector2(0.6f, 0.9f);
            turnRect.offsetMin = Vector2.zero;
            turnRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI turnText = turnIndicator.AddComponent<TextMeshProUGUI>();
            turnText.text = "YOUR TURN";
            turnText.fontSize = 24;
            turnText.alignment = TextAlignmentOptions.Center;
            turnText.color = Color.green;
            
            // Create end turn button
            GameObject endTurnButton = new GameObject("EndTurnButton");
            endTurnButton.transform.SetParent(controlsArea.transform, false);
            RectTransform buttonRect = endTurnButton.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.8f, 0.3f);
            buttonRect.anchorMax = new Vector2(0.95f, 0.7f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            
            Image buttonImage = endTurnButton.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.2f);
            
            Button button = endTurnButton.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(endTurnButton.transform, false);
            RectTransform textRect = buttonTextObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "END TURN";
            buttonText.fontSize = 18;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
        }
        
        private void CreateResultPanel(Transform parent)
        {
            // Create result panel (hidden by default)
            GameObject resultPanel = new GameObject("CombatResultPanel");
            resultPanel.transform.SetParent(parent, false);
            RectTransform rectTransform = resultPanel.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.25f, 0.25f);
            rectTransform.anchorMax = new Vector2(0.75f, 0.75f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            Image panelImage = resultPanel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            
            // Create result text
            GameObject resultTextObj = new GameObject("ResultText");
            resultTextObj.transform.SetParent(resultPanel.transform, false);
            RectTransform textRect = resultTextObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.1f, 0.6f);
            textRect.anchorMax = new Vector2(0.9f, 0.9f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI resultText = resultTextObj.AddComponent<TextMeshProUGUI>();
            resultText.text = "VICTORY";
            resultText.fontSize = 42;
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.color = Color.green;
            
            // Create continue button
            GameObject continueButton = new GameObject("ContinueButton");
            continueButton.transform.SetParent(resultPanel.transform, false);
            RectTransform buttonRect = continueButton.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.3f, 0.2f);
            buttonRect.anchorMax = new Vector2(0.7f, 0.4f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            
            Image buttonImage = continueButton.AddComponent<Image>();
            buttonImage.color = new Color(0.3f, 0.3f, 0.7f);
            
            Button button = continueButton.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(continueButton.transform, false);
            RectTransform textButtonRect = buttonTextObj.AddComponent<RectTransform>();
            textButtonRect.anchorMin = Vector2.zero;
            textButtonRect.anchorMax = Vector2.one;
            textButtonRect.offsetMin = Vector2.zero;
            textButtonRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "CONTINUE";
            buttonText.fontSize = 24;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            
            // Hide result panel by default
            resultPanel.SetActive(false);
        }
        
        private void SaveCanvasPrefab(GameObject canvas)
        {
            string path = Path.Combine(outputPath, "CombatSceneCanvas.prefab");
            
            bool success;
            PrefabUtility.SaveAsPrefabAsset(canvas, path, out success);
            
            if (success)
                Debug.Log($"Combat canvas prefab saved successfully at {path}");
            else
                Debug.LogError($"Failed to save combat canvas prefab");
            
            DestroyImmediate(canvas);
        }
        #endif
        
        // Runtime method to create the combat canvas
        public static GameObject CreateCombatCanvasAtRuntime()
        {
            // Create the root canvas object
            GameObject canvasObj = new GameObject("CombatSceneCanvas");
            
            // Add required components
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            canvasObj.AddComponent<CanvasGroup>();
            
            // Create player area
            GameObject playerArea = new GameObject("PlayerArea");
            playerArea.transform.SetParent(canvasObj.transform, false);
            RectTransform playerRect = playerArea.AddComponent<RectTransform>();
            playerRect.anchorMin = new Vector2(0, 0);
            playerRect.anchorMax = new Vector2(1, 0.4f);
            
            // Create opponent area
            GameObject opponentArea = new GameObject("OpponentArea");
            opponentArea.transform.SetParent(canvasObj.transform, false);
            RectTransform opponentRect = opponentArea.AddComponent<RectTransform>();
            opponentRect.anchorMin = new Vector2(0, 0.6f);
            opponentRect.anchorMax = new Vector2(1, 1);
            
            // Create combat controls area
            GameObject controlsArea = new GameObject("ControlsArea");
            controlsArea.transform.SetParent(canvasObj.transform, false);
            RectTransform controlsRect = controlsArea.AddComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0, 0.4f);
            controlsRect.anchorMax = new Vector2(1, 0.6f);
            
            // Add End Turn button
            GameObject buttonObj = new GameObject("EndTurnButton");
            buttonObj.transform.SetParent(controlsArea.transform, false);
            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.8f, 0.3f);
            buttonRect.anchorMax = new Vector2(0.95f, 0.7f);
            
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 0.2f);
            
            Button button = buttonObj.AddComponent<Button>();
            
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            RectTransform textRect = buttonTextObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            
            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = "END TURN";
            buttonText.fontSize = 18;
            buttonText.alignment = TextAlignmentOptions.Center;
            
            return canvasObj;
        }
    }
} 