using UnityEngine;
using UnityEditor;
using System.IO;

#if UNITY_EDITOR
namespace Combat
{
    public class PrefabGenerator : MonoBehaviour
    {
        [Header("Generation Settings")]
        [SerializeField] private string outputPath = "Assets/Prefabs/Combat";
        
        [Header("Card Prefab")]
        [SerializeField] private Sprite cardBackground;
        [SerializeField] private Sprite attackIcon;
        [SerializeField] private Sprite skillIcon;
        [SerializeField] private Sprite powerIcon;
        
        [Header("Pet Prefab")]
        [SerializeField] private Sprite petSprite;
        
        [MenuItem("Tools/Combat/Generate Combat Prefabs")]
        public static void GeneratePrefabs()
        {
            PrefabGenerator generator = FindFirstObjectByType<PrefabGenerator>();
            if (generator != null)
            {
                generator.GenerateAllPrefabs();
            }
            else
            {
                Debug.LogError("PrefabGenerator component not found in scene!");
            }
        }
        
        // Generate all the prefabs needed for combat
        public void GenerateAllPrefabs()
        {
            // Create output directory if it doesn't exist
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            
            // Generate the combat prefabs
            GameObject cardPrefab = GenerateCardPrefab();
            GameObject petPrefab = GeneratePetPrefab();
            GameObject playerHandPrefab = GeneratePlayerHandPrefab();
            GameObject combatPlayerPrefab = GenerateCombatPlayerPrefab();
            GameObject combatManagerPrefab = GenerateCombatManagerPrefab();
            
            // Save prefabs to disk
            SavePrefab(cardPrefab, "CardPrefab");
            SavePrefab(petPrefab, "PetPrefab");
            SavePrefab(playerHandPrefab, "PlayerHandPrefab");
            SavePrefab(combatPlayerPrefab, "CombatPlayerPrefab");
            SavePrefab(combatManagerPrefab, "CombatManagerPrefab");
            
            Debug.Log("All combat prefabs generated successfully!");
        }
        
        // Generate the card prefab
        private GameObject GenerateCardPrefab()
        {
            // Create the card GameObject
            GameObject cardObj = new GameObject("CardPrefab");
            
            // Add required components
            Canvas canvas = cardObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            
            // Canvas Scaler for consistent UI sizing
            cardObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            cardObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            cardObj.AddComponent<CanvasGroup>();
            
            // Add the Card component
            Card card = cardObj.AddComponent<Card>();
            
            // Create the card visuals
            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(cardObj.transform, false);
            UnityEngine.UI.Image bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImage.rectTransform.sizeDelta = new Vector2(200, 300);
            if (cardBackground != null)
                bgImage.sprite = cardBackground;
            else
                bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            // Card Name
            GameObject nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(cardObj.transform, false);
            TMPro.TextMeshProUGUI nameText = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
            nameText.rectTransform.sizeDelta = new Vector2(180, 40);
            nameText.rectTransform.anchoredPosition = new Vector2(0, 110);
            nameText.alignment = TMPro.TextAlignmentOptions.Center;
            nameText.fontSize = 24;
            nameText.text = "Card Name";
            
            // Description Text
            GameObject descObj = new GameObject("DescriptionText");
            descObj.transform.SetParent(cardObj.transform, false);
            TMPro.TextMeshProUGUI descText = descObj.AddComponent<TMPro.TextMeshProUGUI>();
            descText.rectTransform.sizeDelta = new Vector2(180, 80);
            descText.rectTransform.anchoredPosition = new Vector2(0, 0);
            descText.alignment = TMPro.TextAlignmentOptions.Center;
            descText.fontSize = 18;
            descText.text = "Card description goes here.";
            
            // Cost Text
            GameObject costObj = new GameObject("CostText");
            costObj.transform.SetParent(cardObj.transform, false);
            TMPro.TextMeshProUGUI costText = costObj.AddComponent<TMPro.TextMeshProUGUI>();
            costText.rectTransform.sizeDelta = new Vector2(40, 40);
            costText.rectTransform.anchoredPosition = new Vector2(-80, 130);
            costText.alignment = TMPro.TextAlignmentOptions.Center;
            costText.fontSize = 24;
            costText.text = "1";
            
            // Card Type Icon
            GameObject iconObj = new GameObject("TypeIcon");
            iconObj.transform.SetParent(cardObj.transform, false);
            UnityEngine.UI.Image iconImage = iconObj.AddComponent<UnityEngine.UI.Image>();
            iconImage.rectTransform.sizeDelta = new Vector2(40, 40);
            iconImage.rectTransform.anchoredPosition = new Vector2(80, 130);
            iconImage.color = Color.red; // Default to attack color
            
            // Set up references in the Card component
            card.name = "CardPrefab";
            
            return cardObj;
        }
        
        // Generate the pet prefab
        private GameObject GeneratePetPrefab()
        {
            // Create the pet GameObject
            GameObject petObj = new GameObject("PetPrefab");
            
            // Add required components
            SpriteRenderer renderer = petObj.AddComponent<SpriteRenderer>();
            if (petSprite != null)
                renderer.sprite = petSprite;
                
            petObj.AddComponent<FishNet.Object.NetworkObject>();
            Pet pet = petObj.AddComponent<Pet>();
            
            // Add health UI
            GameObject healthObj = new GameObject("HealthText");
            healthObj.transform.SetParent(petObj.transform, false);
            healthObj.transform.localPosition = new Vector3(0, 1.5f, 0);
            TMPro.TextMeshProUGUI healthText = healthObj.AddComponent<TMPro.TextMeshProUGUI>();
            healthText.fontSize = 24;
            healthText.alignment = TMPro.TextAlignmentOptions.Center;
            healthText.text = "HP: 100/100";
            
            // Add defend icon
            GameObject defendObj = new GameObject("DefendIcon");
            defendObj.transform.SetParent(petObj.transform, false);
            defendObj.transform.localPosition = new Vector3(1.2f, 0, 0);
            SpriteRenderer defendRenderer = defendObj.AddComponent<SpriteRenderer>();
            // Use a shield sprite if available, or create a simple circle
            defendRenderer.sprite = null; // Replace with shield sprite
            defendObj.SetActive(false);
            
            return petObj;
        }
        
        // Generate the player hand prefab
        private GameObject GeneratePlayerHandPrefab()
        {
            // Create the player hand GameObject
            GameObject handObj = new GameObject("PlayerHandPrefab");
            
            // Add required components
            handObj.AddComponent<FishNet.Object.NetworkObject>();
            PlayerHand hand = handObj.AddComponent<PlayerHand>();
            
            // Create card parent transform
            GameObject cardParent = new GameObject("CardParent");
            cardParent.transform.SetParent(handObj.transform, false);
            
            return handObj;
        }
        
        // Generate the combat player prefab
        private GameObject GenerateCombatPlayerPrefab()
        {
            // Create the combat player GameObject
            GameObject playerObj = new GameObject("CombatPlayerPrefab");
            
            // Add required components
            playerObj.AddComponent<FishNet.Object.NetworkObject>();
            CombatPlayer player = playerObj.AddComponent<CombatPlayer>();
            
            // Create UI elements
            GameObject nameObj = new GameObject("PlayerNameText");
            nameObj.transform.SetParent(playerObj.transform, false);
            TMPro.TextMeshProUGUI nameText = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
            nameText.fontSize = 24;
            nameText.alignment = TMPro.TextAlignmentOptions.Center;
            nameText.text = "Player Name";
            
            GameObject energyObj = new GameObject("EnergyText");
            energyObj.transform.SetParent(playerObj.transform, false);
            energyObj.transform.localPosition = new Vector3(0, -0.5f, 0);
            TMPro.TextMeshProUGUI energyText = energyObj.AddComponent<TMPro.TextMeshProUGUI>();
            energyText.fontSize = 20;
            energyText.alignment = TMPro.TextAlignmentOptions.Center;
            energyText.text = "Energy: 3/3";
            
            GameObject buttonObj = new GameObject("EndTurnButton");
            buttonObj.transform.SetParent(playerObj.transform, false);
            buttonObj.transform.localPosition = new Vector3(2, 0, 0);
            UnityEngine.UI.Button button = buttonObj.AddComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.Image buttonImage = buttonObj.AddComponent<UnityEngine.UI.Image>();
            
            GameObject buttonTextObj = new GameObject("ButtonText");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            TMPro.TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            buttonText.fontSize = 18;
            buttonText.alignment = TMPro.TextAlignmentOptions.Center;
            buttonText.text = "End Turn";
            
            return playerObj;
        }
        
        // Generate the combat manager prefab
        private GameObject GenerateCombatManagerPrefab()
        {
            // Create the combat manager GameObject
            GameObject managerObj = new GameObject("CombatManagerPrefab");
            
            // Add required components
            managerObj.AddComponent<FishNet.Object.NetworkObject>();
            CombatManager manager = managerObj.AddComponent<CombatManager>();
            
            return managerObj;
        }
        
        // Save a prefab to disk
        private void SavePrefab(GameObject obj, string name)
        {
            // Create the full path
            string path = Path.Combine(outputPath, name + ".prefab");
            
            // Create the prefab
            bool success;
            PrefabUtility.SaveAsPrefabAsset(obj, path, out success);
            
            if (success)
                Debug.Log($"Prefab {name} saved successfully at {path}");
            else
                Debug.LogError($"Failed to save prefab {name}");
            
            // Destroy the temporary GameObject
            DestroyImmediate(obj);
        }
    }
}
#endif 