using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using DG.Tweening; // Added DOTween

// --- ADDED: Helper Struct for Other Fights UI Sorting ---
struct PlayerStatusInfo
{
    public string Nickname;
    public int PlayerHP;
    public int TurnNum;
    public int OppPetHP;
    public int Score;
}
// --- END ADDED ---

public class CombatUIManager
{
    private GameManager gameManager;
    private GameObject combatRootInstance; // Added: Reference to the root combat UI GameObject
    
    // UI References
    private TextMeshProUGUI playerNameText;
    private Slider playerHealthSlider;
    private TextMeshProUGUI playerHealthText;
    private TextMeshProUGUI playerBlockText;
    private TextMeshProUGUI energyText;
    private TextMeshProUGUI opponentPetNameText;
    private Slider opponentPetHealthSlider;
    private TextMeshProUGUI opponentPetHealthText;
    private TextMeshProUGUI opponentPetBlockText;
    private TextMeshProUGUI opponentPetIntentText;
    private TextMeshProUGUI ownPetNameText;
    private Slider ownPetHealthSlider;
    private TextMeshProUGUI ownPetHealthText;
    private TextMeshProUGUI ownPetBlockText;
    private TextMeshProUGUI ownPetDotText;
    private GameObject playerHandPanel;
    private TextMeshProUGUI deckCountText;
    private TextMeshProUGUI discardCountText;
    private TextMeshProUGUI playerDotText;
    private TextMeshProUGUI comboCountText;
    private Button endTurnButton;
    private TextMeshProUGUI opponentPetDotText;
    private HandPanelHoverManager handPanelHoverManager;
    private GameObject opponentPetHandPanel;
    
    // --- ADDED: HoT UI Fields ---
    private TextMeshProUGUI playerHotText;
    private TextMeshProUGUI ownPetHotText;
    private TextMeshProUGUI opponentPetHotText;
    // --- END ADDED ---
    
    // --- ADDED: Strength UI Fields ---
    private TextMeshProUGUI playerStrengthText;
    private TextMeshProUGUI ownPetStrengthText;
    private TextMeshProUGUI opponentPetStrengthText;
    // --- END ADDED ---
    
    // Other Fights UI
    private GameObject othersStatusArea;
    private TextMeshProUGUI otherPlayerStatusTemplate;
    
    // Hand Layout Parameters
    private float cardSpacing = 100f;
    private float tiltAnglePerCard = 5f;
    private float offsetYPerCard = 10f;
    private float curveFactor = 0.5f;

    // --- ADDED: Debug UI elements ---
    private TextMeshProUGUI debugStatusText;
    private Coroutine currentDebugTextCoroutine;
    // --- END ADDED ---

    public CombatUIManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void InitializeUIReferences(GameObject combatInstance)
    {
        if (combatInstance == null) return;
        this.combatRootInstance = combatInstance; // Store the root instance

        Transform topArea = combatInstance.transform.Find("TopArea");
        Transform playerArea = combatInstance.transform.Find("PlayerArea");

        if (topArea == null || playerArea == null)
        {
            Debug.LogError("Could not find TopArea or PlayerArea in CombatCanvas!");
            return;
        }

        // Find opponent pet area
        Transform opponentAreaContainer = topArea.Find("OpponentPetAreaContainer");
        Transform opponentArea = opponentAreaContainer?.Find("OpponentPetArea");
        opponentPetNameText = opponentArea?.Find("OpponentPetNameText")?.GetComponent<TextMeshProUGUI>();
        opponentPetHealthSlider = opponentArea?.Find("OpponentPetHealthSlider")?.GetComponent<Slider>();
        opponentPetHealthText = opponentArea?.Find("OpponentPetHealthText")?.GetComponent<TextMeshProUGUI>();
        opponentPetBlockText = opponentArea?.Find("OpponentPetBlockText")?.GetComponent<TextMeshProUGUI>();
        opponentPetIntentText = opponentArea?.Find("OpponentPetIntentText")?.GetComponent<TextMeshProUGUI>();
        
        // Find Opponent Pet Hand Panel
        opponentPetHandPanel = topArea.Find("OpponentPetAreaContainer/OpponentPetHandPanel")?.gameObject;
        if (opponentPetHandPanel == null) 
        {
            Debug.LogWarning("OpponentPetHandPanel GameObject not found under TopArea/OpponentPetAreaContainer.");
        }
        
        // Hide the opponent intent text as it's currently unused
        if (opponentPetIntentText != null)
        {
            opponentPetIntentText.gameObject.SetActive(false); 
        }
        
        // Find own pet area
        Transform ownPetAreaContainer = topArea.Find("OwnPetAreaContainer");
        Transform ownPetArea = ownPetAreaContainer?.Find("OwnPetArea");
        ownPetNameText = ownPetArea?.Find("OwnPetNameText")?.GetComponent<TextMeshProUGUI>();
        ownPetHealthSlider = ownPetArea?.Find("OwnPetHealthSlider")?.GetComponent<Slider>();
        ownPetHealthText = ownPetArea?.Find("OwnPetHealthText")?.GetComponent<TextMeshProUGUI>();
        ownPetBlockText = ownPetArea?.Find("OwnPetBlockText")?.GetComponent<TextMeshProUGUI>();
        
        // Find Other Fights UI Elements
        Transform othersStatusAreaTransform = topArea.Find("OthersStatusArea");
        if (othersStatusAreaTransform != null)
        {
            othersStatusArea = othersStatusAreaTransform.gameObject;
            Transform templateTransform = othersStatusAreaTransform.Find("OtherPlayerStatusTemplate");
            if (templateTransform != null)
            {
                otherPlayerStatusTemplate = templateTransform.GetComponent<TextMeshProUGUI>();
                if (otherPlayerStatusTemplate != null)
                {
                    otherPlayerStatusTemplate.gameObject.SetActive(false); // Ensure template is initially hidden
                }
                else
                {
                     Debug.LogError("OtherPlayerStatusTemplate TextMeshProUGUI component not found!");
                }
            }
            else
            {
                Debug.LogError("OtherPlayerStatusTemplate GameObject not found under OthersStatusArea!");
            }
        }
        else
        {
            Debug.LogWarning("OthersStatusArea GameObject not found in TopArea.");
        }

        // Find elements within PlayerArea
        Transform statsRow = playerArea.Find("StatsRow");
        playerNameText = statsRow?.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
        playerHealthSlider = statsRow?.Find("PlayerHealthSlider")?.GetComponent<Slider>();
        playerHealthText = statsRow?.Find("PlayerHealthText")?.GetComponent<TextMeshProUGUI>();
        playerBlockText = statsRow?.Find("PlayerBlockText")?.GetComponent<TextMeshProUGUI>();
        energyText = statsRow?.Find("EnergyText")?.GetComponent<TextMeshProUGUI>();
        comboCountText = statsRow?.Find("ComboCountText")?.GetComponent<TextMeshProUGUI>(); // Keep Combo Count separate for now
        
        Transform handPanelTransform = playerArea.Find("PlayerHandPanel");
        playerHandPanel = handPanelTransform?.gameObject;
        
        if (playerHandPanel != null)
        {
            handPanelHoverManager = playerHandPanel.GetComponent<HandPanelHoverManager>();
            if (handPanelHoverManager == null)
            {
                 Debug.LogError("HandPanelHoverManager component not found on PlayerHandPanel!");
            }
        }
        
        Transform bottomBar = playerArea.Find("BottomBar");
        deckCountText = bottomBar?.Find("DeckCountText")?.GetComponent<TextMeshProUGUI>();
        discardCountText = bottomBar?.Find("DiscardCountText")?.GetComponent<TextMeshProUGUI>();
        endTurnButton = bottomBar?.Find("EndTurnButton")?.GetComponent<Button>();
        
        // Validate critical findings 
        if (playerHandPanel == null || opponentPetNameText == null || endTurnButton == null)
            Debug.LogError("One or more critical Combat UI elements not found in CombatCanvasPrefab!");
        else 
            Debug.Log("Successfully found critical combat UI elements.");
    }
    
    public void InitializeUIState()
    {
        if (playerNameText) playerNameText.text = PhotonNetwork.LocalPlayer.NickName;
        
        if (opponentPetNameText)
        {
            Player opponent = gameManager.GetPlayerManager().GetOpponentPlayer();
            if (opponent != null)
            {
                // Check if the opponent has a custom pet name property
                string opponentPetName = null;
                if (opponent.CustomProperties.TryGetValue(PhotonManager.PET_NAME_PROPERTY, out object petNameObj))
                {
                    opponentPetName = petNameObj as string;
                }
                
                // Use the custom pet name if available, otherwise fall back to the default format
                opponentPetNameText.text = !string.IsNullOrEmpty(opponentPetName) ? opponentPetName : $"{opponent.NickName}'s Pet";
            }
            else
            {
                opponentPetNameText.text = "Opponent Pet";
            }
        }
        
        if (ownPetNameText) ownPetNameText.text = gameManager.GetPlayerManager().GetLocalPetName();
    }
    
    public void UpdateHealthUI()
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        // Player Health & Block
        int currentPlayerHealth = playerManager.GetLocalPlayerHealth();
        int effectivePlayerMaxHealth = playerManager.GetEffectivePlayerMaxHealth();
        if (playerHealthSlider) {
             // Ensure max health is not zero to avoid division errors
             playerHealthSlider.value = (effectivePlayerMaxHealth > 0) ? (float)currentPlayerHealth / effectivePlayerMaxHealth : 0;
        }
        if (playerHealthText) playerHealthText.text = $"{currentPlayerHealth} / {effectivePlayerMaxHealth}";
        if (playerBlockText) 
        {
            // Start building the status string
            System.Text.StringBuilder playerStatus = new System.Text.StringBuilder();
            playerStatus.Append($"Block: {playerManager.GetLocalPlayerBlock()}");

            // Append active status effects
            int weakTurns = playerManager.GetPlayerWeakTurns();
            if (weakTurns > 0) playerStatus.Append($" | Weak: {weakTurns}t");

            int breakTurns = playerManager.GetPlayerBreakTurns();
            if (breakTurns > 0) playerStatus.Append($" | Break: {breakTurns}t");
            
            // --- ADDED: Append DoT, HoT, Strength to Player status ---
            int playerDotTurns = playerManager.GetPlayerDotTurns();
            if (playerDotTurns > 0) playerStatus.Append($" | DoT: {playerManager.GetPlayerDotDamage()} ({playerDotTurns}t)");
            
            int playerHotTurns = playerManager.GetPlayerHotTurns();
            if (playerHotTurns > 0) playerStatus.Append($" | Regen: {playerManager.GetPlayerHotAmount()} ({playerHotTurns}t)");
            
            int playerStrength = playerManager.GetPlayerStrength();
            if (playerStrength != 0) playerStatus.Append($" | Strength: {playerStrength:+0;-#}");

            // --- ADDED: Thorns status for Player ---
            int playerThorns = playerManager.GetPlayerThorns();
            if (playerThorns > 0) playerStatus.Append($" | Thorns: {playerThorns}");
            // --- END ADDED ---

            // Crit Chance
            int critChancePlayer = playerManager.GetPlayerEffectiveCritChance();
            playerStatus.Append($" | Crit: {critChancePlayer}%"); 

            playerBlockText.text = playerStatus.ToString();
        }

        // Own Pet Health & Block
        int currentPetHealth = playerManager.GetLocalPetHealth();
        int effectivePetMaxHealth = playerManager.GetEffectivePetMaxHealth();
        if (ownPetHealthSlider) {
            ownPetHealthSlider.value = (effectivePetMaxHealth > 0) ? (float)currentPetHealth / effectivePetMaxHealth : 0;
        }
        if (ownPetHealthText) ownPetHealthText.text = $"{currentPetHealth} / {effectivePetMaxHealth}";
        
        // Construct Own Pet Block & Energy Text
        if (ownPetBlockText)
        {
            int petBlock = playerManager.GetLocalPetBlock();
            int petEnergy = -1; // Default to invalid value
            
            // Always try to read the latest value from properties
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, out object energyObj))
            {
                try 
                { 
                    petEnergy = (int)energyObj; 
                }
                catch 
                { 
                    Debug.LogWarning($"Failed to cast {CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP} to int.");
                    petEnergy = 0;
                } 
            }
            
            if (petEnergy < 0) // If property doesn't exist yet, use default
            {
                petEnergy = gameManager.GetStartingPetEnergy(); // Directly use starting energy if prop missing
            }
            
            System.Text.StringBuilder petStatus = new System.Text.StringBuilder();
            petStatus.Append($"Block: {petBlock} | Energy: {petEnergy}");

            // Append active status effects
            int petWeakTurns = playerManager.GetLocalPetWeakTurns();
            if (petWeakTurns > 0) petStatus.Append($" | Weak: {petWeakTurns}t");

            int petBreakTurns = playerManager.GetLocalPetBreakTurns();
            if (petBreakTurns > 0) petStatus.Append($" | Break: {petBreakTurns}t");
            
            // --- ADDED: Append DoT, HoT, Strength to Own Pet status ---
            int petDotTurns = playerManager.GetLocalPetDotTurns();
            if (petDotTurns > 0) petStatus.Append($" | DoT: {playerManager.GetLocalPetDotDamage()} ({petDotTurns}t)");

            int petHotTurns = playerManager.GetLocalPetHotTurns();
            if (petHotTurns > 0) petStatus.Append($" | Regen: {playerManager.GetLocalPetHotAmount()} ({petHotTurns}t)");

            int petStrength = playerManager.GetLocalPetStrength();
            if (petStrength != 0) petStatus.Append($" | Strength: {petStrength:+0;-#}");
            
            // --- ADDED: Thorns status for Own Pet ---
            int petThorns = playerManager.GetLocalPetThorns();
            if (petThorns > 0) petStatus.Append($" | Thorns: {petThorns}");
            // --- END ADDED ---
            
            // Crit Chance
            int critChancePet = playerManager.GetPetEffectiveCritChance(); // Get Own Pet Crit
            petStatus.Append($" | Crit: {critChancePet}%"); 
            ownPetBlockText.text = petStatus.ToString();
        }

        // Opponent Pet Health & Block
        int currentOpponentPetHealth = playerManager.GetOpponentPetHealth();
        int effectiveOpponentPetMaxHealth = playerManager.GetEffectiveOpponentPetMaxHealth();
        if (opponentPetHealthSlider) {
             opponentPetHealthSlider.value = (effectiveOpponentPetMaxHealth > 0) ? (float)currentOpponentPetHealth / effectiveOpponentPetMaxHealth : 0;
        }
        if (opponentPetHealthText) opponentPetHealthText.text = $"{currentOpponentPetHealth} / {effectiveOpponentPetMaxHealth}";
        
        // Construct Opponent Pet Block & Energy Text
        if (opponentPetBlockText) 
        {
            int oppPetBlock = playerManager.GetOpponentPetBlock();
            int oppPetEnergy = 0;
            
            // Read energy from local simulation for live updates
            PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
            if (petDeckManager != null) {
                oppPetEnergy = petDeckManager.GetOpponentPetEnergy();
            }
            
            System.Text.StringBuilder oppPetStatus = new System.Text.StringBuilder();
            oppPetStatus.Append($"Block: {oppPetBlock} | Energy: {oppPetEnergy}");
            
            // Append active status effects
            int weakTurns = playerManager.GetOpponentPetWeakTurns();
            if (weakTurns > 0) oppPetStatus.Append($" | Weak: {weakTurns}t");

            int breakTurns = playerManager.GetOpponentPetBreakTurns();
            if (breakTurns > 0) oppPetStatus.Append($" | Break: {breakTurns}t");
            
            // --- ADDED: Append DoT, HoT, Strength to Opponent Pet status ---
            int oppPetDotTurns = playerManager.GetOpponentPetDotTurns();
            if (oppPetDotTurns > 0) oppPetStatus.Append($" | DoT: {playerManager.GetOpponentPetDotDamage()} ({oppPetDotTurns}t)");

            int oppPetHotTurns = playerManager.GetOpponentPetHotTurns();
            if (oppPetHotTurns > 0) oppPetStatus.Append($" | Regen: {playerManager.GetOpponentPetHotAmount()} ({oppPetHotTurns}t)");

            int oppPetStrength = playerManager.GetOpponentPetStrength();
            if (oppPetStrength != 0) oppPetStatus.Append($" | Strength: {oppPetStrength:+0;-#}");
            
            // --- ADDED: Thorns status for Opponent Pet ---
            int oppPetThorns = playerManager.GetOpponentPetThorns();
            if (oppPetThorns > 0) oppPetStatus.Append($" | Thorns: {oppPetThorns}");
            // --- END ADDED ---
            
            // Crit Chance
            int critChanceOppPet = playerManager.GetOpponentPetEffectiveCritChance(); 
            oppPetStatus.Append($" | Crit: {critChanceOppPet}%"); 

            opponentPetBlockText.text = oppPetStatus.ToString();
        }
    }
    
    public void UpdateEnergyUI()
    {
        if (energyText != null)
        {
            energyText.text = $"Energy: {gameManager.GetPlayerManager().GetCurrentEnergy()} / {gameManager.GetPlayerManager().GetStartingEnergy()}";
        }
        else
        {
            Debug.LogWarning("UpdateEnergyUI: energyText reference is null!");
        }
    }
    
    public void UpdateDeckCountUI()
    {
        CardManager cardManager = gameManager.GetCardManager();
        if (deckCountText != null) deckCountText.text = $"Deck: {cardManager.GetDeckCount()}";
        if (discardCountText != null) discardCountText.text = $"Discard: {cardManager.GetDiscardCount()}";
    }
    
    public void UpdateStatusEffectsUI()
    {
        // This method is now redundant as status effects are displayed
        // within UpdateHealthUI. Keep the method signature for now 
        // in case other parts of the code call it, but clear its body.

        /* // Original Content - Now Handled in UpdateHealthUI
        PlayerManager playerManager = gameManager.GetPlayerManager();
        if (playerManager == null) return;

        // Player DoT
        if (playerDotText != null)
        {
            int dotTurns = playerManager.GetPlayerDotTurns();
            int dotDmg = playerManager.GetPlayerDotDamage();
            playerDotText.text = (dotTurns > 0) ? $"DoT: {dotDmg} ({dotTurns}t)" : "";
            playerDotText.gameObject.SetActive(dotTurns > 0);
        }

        // Player HoT
        if (playerHotText != null)
        {
            int hotTurns = playerManager.GetPlayerHotTurns();
            int hotHeal = playerManager.GetPlayerHotAmount();
            bool shouldBeActive = hotTurns > 0;
            Debug.Log($"[UpdateStatusEffectsUI] Player HoT: Turns={hotTurns}, Heal={hotHeal}, ShouldBeActive={shouldBeActive}");
            playerHotText.text = shouldBeActive ? $"Regen: {hotHeal} ({hotTurns}t)" : "";
            
            Debug.Log($"[UpdateStatusEffectsUI] Setting playerHotText GameObject Active: {shouldBeActive}"); 
            playerHotText.gameObject.SetActive(shouldBeActive);
        }
        else
        {
             Debug.LogWarning("[UpdateStatusEffectsUI] playerHotText is null!");
        }

        // Player Strength
        if (playerStrengthText != null)
        {
            int strength = playerManager.GetPlayerStrength();
            playerStrengthText.text = (strength != 0) ? $"Strength: {strength:+0;-#}" : ""; // Format to show +/- sign
            playerStrengthText.gameObject.SetActive(strength != 0);
        }

        // Combo Count
        if (comboCountText != null)
        {
            int combo = playerManager.GetCurrentComboCount();
            comboCountText.text = (combo > 0) ? $"Combo: {combo}" : "";
            comboCountText.gameObject.SetActive(combo > 0);
        }

        // Own Pet DoT
        if (ownPetDotText != null)
        {
            int dotTurns = playerManager.GetLocalPetDotTurns();
            int dotDmg = playerManager.GetLocalPetDotDamage();
            ownPetDotText.text = (dotTurns > 0) ? $"DoT: {dotDmg} ({dotTurns}t)" : "";
            ownPetDotText.gameObject.SetActive(dotTurns > 0);
        }

        // Own Pet HoT
        if (ownPetHotText != null)
        {
            int hotTurns = playerManager.GetLocalPetHotTurns();
            int hotHeal = playerManager.GetLocalPetHotAmount();
            ownPetHotText.text = (hotTurns > 0) ? $"Regen: {hotHeal} ({hotTurns}t)" : "";
            ownPetHotText.gameObject.SetActive(hotTurns > 0);
        }

        // Own Pet Strength
        if (ownPetStrengthText != null)
        {
            int strength = playerManager.GetLocalPetStrength();
            ownPetStrengthText.text = (strength != 0) ? $"Strength: {strength:+0;-#}" : "";
            ownPetStrengthText.gameObject.SetActive(strength != 0);
        }

        // Opponent Pet DoT
        if (opponentPetDotText != null)
        {
            int dotTurns = playerManager.GetOpponentPetDotTurns();
            int dotDmg = playerManager.GetOpponentPetDotDamage();
            opponentPetDotText.text = (dotTurns > 0) ? $"DoT: {dotDmg} ({dotTurns}t)" : "";
            opponentPetDotText.gameObject.SetActive(dotTurns > 0);
        }

        // Opponent Pet HoT
        if (opponentPetHotText != null)
        {
            int hotTurns = playerManager.GetOpponentPetHotTurns();
            int hotHeal = playerManager.GetOpponentPetHotAmount();
            opponentPetHotText.text = (hotTurns > 0) ? $"Regen: {hotHeal} ({hotTurns}t)" : "";
            opponentPetHotText.gameObject.SetActive(hotTurns > 0);
        }

        // Opponent Pet Strength
        if (opponentPetStrengthText != null)
        {
            int strength = playerManager.GetOpponentPetStrength();
            opponentPetStrengthText.text = (strength != 0) ? $"Strength: {strength:+0;-#}" : "";
            opponentPetStrengthText.gameObject.SetActive(strength != 0);
        }
        */
    }
    
    public void UpdateHandUI()
    {
        GameObject cardPrefab = gameManager.GetCardPrefab();
        PlayerManager playerManager = gameManager.GetPlayerManager();
        CardManager cardManager = gameManager.GetCardManager(); 
        List<CardData> currentHand = cardManager.GetHand();

        if (playerHandPanel == null || cardPrefab == null)
        {
            Debug.LogError("Cannot UpdateHandUI - PlayerHandPanel or CardPrefab is missing!");
            return;
        }

        // --- Refactored Logic --- 

        // 1. Get Existing GOs currently in the panel
        List<GameObject> existingGOs = new List<GameObject>();
        Dictionary<GameObject, CardData> currentGOMap = new Dictionary<GameObject, CardData>();
        foreach (Transform child in playerHandPanel.transform)
        {
            if (child.gameObject.name != "CardTemplate")
            {
                var handler = child.GetComponent<CardDragHandler>();
                if (handler != null && handler.cardData != null)
                {
                    existingGOs.Add(child.gameObject);
                    currentGOMap[child.gameObject] = handler.cardData;
                }
                else if (child.gameObject.activeSelf) // Only destroy if active & invalid
                {
                    Debug.LogWarning($"[UpdateHandUI] Destroying invalid/unexpected card GO: {child.name}");
                    Object.Destroy(child.gameObject);
                }
            }
            else
            {
                 child.gameObject.SetActive(false); // Keep template hidden
            }
        }

        // 2. Prepare for matching
        // List<System.Tuple<Vector3, Quaternion, Vector3>> targetTransforms = CalculateLayoutTransforms(currentHand.Count);
        List<System.Tuple<Vector3, Quaternion, Vector3>> targetTransforms = CalculateLayoutTransforms(currentHand.Count, playerHandPanel.GetComponent<RectTransform>()); // Pass panel rect
        List<GameObject> finalGOs = new List<GameObject>(currentHand.Count); // GOs in the final layout order
        List<GameObject> availableGOs = new List<GameObject>(existingGOs); // Copy list of GOs available to be reused
        bool[] handCardMatched = new bool[currentHand.Count]; // Track which hand slots have been filled

        // 3. Match and Animate/Create GOs
        // Prioritize matching existing GOs to their corresponding CardData first
        List<GameObject> reusedGOs = new List<GameObject>();
        for (int i = 0; i < currentHand.Count; i++)
        {
            CardData cardData = currentHand[i];
            GameObject goForThisCard = null;

            // Try find a matching, available GO
            int foundIndex = -1;
            for (int j = 0; j < availableGOs.Count; j++)
            {
                // Check if the GO's data matches the current hand card AND it hasn't been claimed by an earlier slot
                if (currentGOMap.ContainsKey(availableGOs[j]) && currentGOMap[availableGOs[j]] == cardData)
                {
                    goForThisCard = availableGOs[j];
                    foundIndex = j;
                    break; // Found the first available match for this card data
                }
            }

            if (goForThisCard != null)
            {
                availableGOs.RemoveAt(foundIndex); // Mark as used for this update cycle
                UpdateCardVisuals(goForThisCard, cardData, playerManager, cardManager); // Update visuals

                // Animate to new position
                RectTransform cardRect = goForThisCard.GetComponent<RectTransform>();
                System.Tuple<Vector3, Quaternion, Vector3> target = targetTransforms[i];
                DOTween.Kill(cardRect); // Kill potential previous tweens (like discard fade)
                goForThisCard.SetActive(true); // Ensure it's active
                // If it was fading out via discard, reset alpha
                 CanvasGroup cg = goForThisCard.GetComponent<CanvasGroup>();
                 if (cg != null) { 
                     DOTween.Kill(cg); // Kill fade tween
                     cg.alpha = 1f; 
                     cg.blocksRaycasts = true;
                 }

                cardRect.DOLocalMove(target.Item1, 0.3f).SetEase(Ease.OutQuad);
                cardRect.DOLocalRotateQuaternion(target.Item2, 0.3f).SetEase(Ease.OutQuad);
                CardDragHandler handler = goForThisCard.GetComponent<CardDragHandler>();
                if (handler != null) { handler.originalPosition = target.Item1; handler.originalRotation = target.Item2; handler.originalScale = target.Item3; handler.cardData = cardData; /*Ensure data is current*/ }
                
                handler.ResetState();

                // Add to final list in the correct order
                 if (finalGOs.Count > i) finalGOs[i] = goForThisCard; else finalGOs.Add(goForThisCard);
                 reusedGOs.Add(goForThisCard);
                 handCardMatched[i] = true;
            }
        }

        // 4. Create new GOs for any hand cards that didn't find a match
        for (int i = 0; i < currentHand.Count; i++)
        {
            if (handCardMatched[i]) continue; // Already handled by reuse

            CardData cardData = currentHand[i];
            GameObject newGO = Object.Instantiate(cardPrefab, playerHandPanel.transform);
            newGO.name = $"Card_{cardData.cardName}_{System.Guid.NewGuid()}";
            UpdateCardVisuals(newGO, cardData, playerManager, cardManager);
            CardDragHandler handler = newGO.GetComponent<CardDragHandler>();
            if (handler != null) handler.cardData = cardData;
            else Debug.LogError($"CardDragHandler missing on {newGO.name}!");
            newGO.SetActive(true);

            // Animate In
            System.Tuple<Vector3, Quaternion, Vector3> target = targetTransforms[i];
            AnimateCardDraw(newGO, i, target.Item1, target.Item2, target.Item3);
            
            // Add to final list in the correct order
            if (finalGOs.Count > i) finalGOs[i] = newGO; else finalGOs.Add(newGO);
        }

        // 5. Destroy Unused Existing GOs
        foreach (GameObject unusedGO in availableGOs) // These GOs were in the panel but are not in the current hand
        {
            // Check if the GO is currently animating its discard
            CardDragHandler unusedHandler = unusedGO?.GetComponent<CardDragHandler>();
            bool isCurrentlyDiscarding = unusedHandler != null && unusedHandler.isDiscarding;

            // --- MODIFIED: Only destroy if NOT null AND NOT currently discarding ---
            if (unusedGO != null && !isCurrentlyDiscarding) 
            {
                Debug.LogWarning($"[UpdateHandUI] Destroying unused GO: {unusedGO.name} (Card: {currentGOMap.GetValueOrDefault(unusedGO)?.cardName ?? "Unknown"})");
                 DOTween.Kill(unusedGO.transform, true); // Kill tweens immediately
                 Object.Destroy(unusedGO);
            }
            // --- END MODIFIED ---
            else if (isCurrentlyDiscarding)
            {
                 Debug.Log($"[UpdateHandUI] Skipping destruction of {unusedGO.name} because it is animating discard.");
            }
            // --- ADDED: Handle case where unusedGO might be null already ---
            else if (unusedGO == null)
            {
                // This might happen if it was destroyed elsewhere unexpectedly
                // Debug.LogWarning("[UpdateHandUI] Found a null entry in availableGOs during cleanup.");
            }
            // --- END ADDED ---
        }

        // 6. Update Hover Manager
        if (handPanelHoverManager != null)
        { 
            handPanelHoverManager.UpdateCardReferences(finalGOs);
        }
    }
    
    private IEnumerator DelayedHoverManagerUpdate(List<GameObject> cardGOs)
    {
        yield return new WaitForSeconds(0.4f); // Wait for reposition animations
        if (handPanelHoverManager != null)
        {
             handPanelHoverManager.UpdateCardReferences(cardGOs);
        }
    }
    
    private List<System.Tuple<Vector3, Quaternion, Vector3>> CalculateLayoutTransforms(int numCards, RectTransform panelRect)
    {
        List<System.Tuple<Vector3, Quaternion, Vector3>> transforms = new List<System.Tuple<Vector3, Quaternion, Vector3>>();
        if (numCards == 0 || panelRect == null) return transforms;

        // --- Dynamic Spacing Calculation ---
        float panelWidth = panelRect.rect.width;
        // Estimate card width (could be more precise if needed)
        float cardWidth = gameManager.GetCardPrefab()?.GetComponent<RectTransform>().rect.width ?? 100f; 
        // Target total width: leave some padding
        float targetTotalWidth = panelWidth * 0.9f; 
        // Calculate dynamic spacing
        float dynamicCardSpacing = cardSpacing; // Start with default
        if (numCards > 1) {
            // Calculate spacing needed to fit all cards, considering overlap
            // This is an approximation, might need tuning depending on desired overlap
            float requiredWidth = cardWidth * numCards * 0.7f; // Assume cards overlap significantly
            if (requiredWidth > targetTotalWidth)
            {
                 // Reduce spacing to fit
                 dynamicCardSpacing = (targetTotalWidth - cardWidth) / (numCards - 1);
            }
            else
            {
                // Use default spacing if enough room, but don't exceed panel width
                float defaultWidth = cardSpacing * (numCards - 1) + cardWidth;
                if (defaultWidth > targetTotalWidth)
                {
                    dynamicCardSpacing = (targetTotalWidth - cardWidth) / (numCards - 1);
                }
            }
        }
        dynamicCardSpacing = Mathf.Max(dynamicCardSpacing, cardWidth * 0.5f); // Ensure minimum overlap/spacing
        Debug.Log($"CalculateLayoutTransforms: PanelWidth={panelWidth}, NumCards={numCards}, CalculatedSpacing={dynamicCardSpacing}");
        // --- End Dynamic Spacing ---

        float middleIndex = (numCards - 1) / 2.0f;
        Vector3 baseScale = gameManager.GetCardPrefab()?.GetComponent<RectTransform>().localScale ?? Vector3.one;

        for (int i = 0; i < numCards; i++)
        {
            float offsetFromCenter = i - middleIndex;

            // Calculate Position using dynamic spacing
            float posX = offsetFromCenter * dynamicCardSpacing;
            posX += Mathf.Sign(offsetFromCenter) * Mathf.Pow(Mathf.Abs(offsetFromCenter), 2) * curveFactor * dynamicCardSpacing * 0.1f; // Curve adjusted by spacing
            float posY = -Mathf.Abs(offsetFromCenter) * offsetYPerCard; 
            posY -= Mathf.Pow(offsetFromCenter, 2) * offsetYPerCard * 0.1f; 

            // Calculate Rotation
            float angle = -offsetFromCenter * tiltAnglePerCard; 

            Vector3 position = new Vector3(posX, posY, 0);
            Quaternion rotation = Quaternion.Euler(0, 0, angle);
            Vector3 scale = baseScale; // Assuming uniform scale for now

            transforms.Add(System.Tuple.Create(position, rotation, scale));
        }
        return transforms;
    }
    
    private void AnimateCardDraw(GameObject cardGO, int index, Vector3 targetPos, Quaternion targetRot, Vector3 targetScale)
    {
        RectTransform cardRect = cardGO.GetComponent<RectTransform>();
        // --- ADDED: Get/Add CanvasGroup and disable interaction ---
        CanvasGroup canvasGroup = cardGO.GetComponent<CanvasGroup>() ?? cardGO.AddComponent<CanvasGroup>();
        if (cardRect == null) return;
        
        canvasGroup.blocksRaycasts = false; // Disable hover interaction during animation
        // --- END ADDED ---

        float drawAnimDuration = 0.4f;
        float staggerDelay = index * 0.08f;

        // Initial state (off-screen below, slightly smaller, zero rotation)
        cardRect.localPosition = targetPos + Vector3.down * 400f;
        cardRect.localRotation = Quaternion.identity;
        cardRect.localScale = targetScale * 0.7f;
        // --- ADDED: Start fully transparent for fade-in effect ---
        canvasGroup.alpha = 0f; 
        // --- END ADDED ---
        
        // Kill existing tweens just in case
        DOTween.Kill(cardRect);
        // --- ADDED: Kill canvas group tweens ---
        DOTween.Kill(canvasGroup);
        // --- END ADDED ---

        // Create Sequence
        Sequence drawSequence = DOTween.Sequence();
        drawSequence.SetTarget(cardRect); // Associate tween with the RectTransform
        drawSequence.AppendInterval(staggerDelay);
        // --- MODIFIED: Join fade-in with movement/rotation/scale ---
        drawSequence.Append(cardRect.DOLocalMove(targetPos, drawAnimDuration).SetEase(Ease.OutBack));
        drawSequence.Join(cardRect.DOLocalRotateQuaternion(targetRot, drawAnimDuration).SetEase(Ease.OutBack));
        drawSequence.Join(cardRect.DOScale(targetScale, drawAnimDuration).SetEase(Ease.OutBack));
        drawSequence.Join(canvasGroup.DOFade(1f, drawAnimDuration * 0.5f)); // Fade in quicker
        // --- END MODIFIED ---
        
        // Update handler's original transform once animation is complete
        drawSequence.OnComplete(() => {
            CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
            if (handler != null)
            {
                handler.originalPosition = targetPos;
                handler.originalRotation = targetRot;
                handler.originalScale = targetScale;
            }
            // --- ADDED: Re-enable interaction ---
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
             // --- END ADDED ---
        });

        drawSequence.Play();
    }
    
    private void UpdateCardVisuals(GameObject cardGO, CardData card, PlayerManager playerManager, CardManager cardManager)
    {
         bool isTempUpgrade = cardManager.IsCardTemporarilyUpgraded(card);

         Transform headerPanel = cardGO.transform.Find("HeaderPanel");
         Transform descPanel = cardGO.transform.Find("DescPanel");
         Transform artPanel = cardGO.transform.Find("ArtPanel");

         TextMeshProUGUI nameText = headerPanel?.Find("CardNameText")?.GetComponent<TextMeshProUGUI>();
         TextMeshProUGUI costText = headerPanel?.Find("CostText")?.GetComponent<TextMeshProUGUI>();
         TextMeshProUGUI descText = descPanel?.Find("CardDescText")?.GetComponent<TextMeshProUGUI>();
         Image cardBackground = cardGO.GetComponent<Image>(); 
         Image artImage = artPanel?.GetComponent<Image>(); // Get the Image component from ArtPanel

         if (nameText != null) 
         {
             nameText.text = card.cardName;
             if (isTempUpgrade) nameText.text += " (T+)"; 
         }
         
         if (costText != null)
         {
             int cardSpecificModifier = playerManager.GetLocalHandCostModifier(card);
             int effectiveCost = Mathf.Max(0, card.cost + cardSpecificModifier);
             costText.text = effectiveCost.ToString();
             
             // Cost color logic
             if (cardSpecificModifier > 0) costText.color = Color.red; 
             else if (cardSpecificModifier < 0) costText.color = Color.green; 
             else costText.color = Color.white; 
         }

         if (descText != null) descText.text = card.description;

         if (artImage != null)
         {
             if (card.cardArt != null)
             {
                 artImage.sprite = card.cardArt;
                 artImage.enabled = true; // Ensure it's visible if it was hidden
             }
             else
             {
                 artImage.enabled = false; // Hide the art panel if no sprite is assigned
             }
         }
        
         if (cardBackground != null) // Reset background color first
         {
            cardBackground.color = Color.white; // Or your default card background color
            if (isTempUpgrade)
            {
                 cardBackground.color = new Color(0.8f, 0.7f, 1.0f, cardBackground.color.a); // Light purple tint for temp upgrade
            }
         }
        
         // Ensure CardDragHandler has the correct CardData
         CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
         if (handler != null)
         {
             handler.cardData = card;
         }
         else
         {
             Debug.LogError($"CardDragHandler component not found on card GameObject: {cardGO.name}");
         }
    }
    
    public IEnumerator VisualizeOpponentPetCardPlay(CardData card)
    {
        if (card == null) yield break;
        Debug.Log($"Visualizing Opponent Pet playing: {card.cardName}");

        // Brief delay after popup before card appears
        yield return new WaitForSeconds(0.2f); 

        // Instantiate and Prepare Card Visual
        GameObject cardPrefab = gameManager.GetCardPrefab();
        GameObject combatCanvas = gameManager.GetGameStateManager().GetCombatInstance();
        if (cardPrefab == null || combatCanvas == null) 
        { 
            Debug.LogError("Cannot visualize pet card: CardPrefab or CombatCanvas missing!");
            yield break; 
        }
        
        GameObject cardGO = Object.Instantiate(cardPrefab, combatCanvas.transform); // Instantiate under canvas
        cardGO.name = $"OpponentPlayedCard_{card.cardName}";
        
        // Configure visuals
        UpdateCardVisuals(cardGO, card, gameManager.GetPlayerManager(), gameManager.GetCardManager()); 
        
        // Disable interaction for opponent card
        CardDragHandler dragHandler = cardGO.GetComponent<CardDragHandler>();
        if(dragHandler != null) dragHandler.enabled = false;
        CanvasGroup canvasGroup = cardGO.GetComponent<CanvasGroup>() ?? cardGO.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;

        // Position and Animate Card
        RectTransform cardRect = cardGO.GetComponent<RectTransform>();
        Vector3 startPosition = opponentPetBlockText != null ? opponentPetBlockText.transform.position : new Vector3(Screen.width * 0.8f, Screen.height * 0.8f, 0); // Start near opponent pet
        
        // --- REVISED: Use explicit primaryTargetWhenPlayedByPet field --- 
        Vector3 targetPosition;
        if (card.primaryTargetWhenPlayedByPet == OpponentPetTargetType.Player)
        {
            // Target player area (e.g., near player health bar)
            targetPosition = playerHealthSlider != null ? playerHealthSlider.transform.position : new Vector3(Screen.width * 0.5f, Screen.height * 0.2f, 0); 
            Debug.Log($"Card {card.cardName} targets Player (primaryTargetWhenPlayedByPet: {card.primaryTargetWhenPlayedByPet}). Animating towards player UI.");
        }
        else // Assumes Self
        {
            // Target opponent pet area (e.g., near opponent pet block text)
            targetPosition = opponentPetBlockText != null ? opponentPetBlockText.transform.position : new Vector3(Screen.width * 0.8f, Screen.height * 0.7f, 0);
            Debug.Log($"Card {card.cardName} targets Self (primaryTargetWhenPlayedByPet: {card.primaryTargetWhenPlayedByPet}). Animating towards opponent pet UI.");
        }
        // --- END REVISED ---
        
        // Offset slightly so it doesn't perfectly overlap UI
        targetPosition += new Vector3(0, 50f, 0); 
        
        float scaleFactor = 1.2f; // Slightly smaller scale than center animation
        float animDuration = 0.5f; // Slightly longer animation

        cardRect.position = startPosition;
        cardRect.localScale = Vector3.one * 0.5f;
        canvasGroup.alpha = 0f;
        
        // --- MODIFIED: Use DOTween for smoother animation ---
        Sequence animSequence = DOTween.Sequence();
        animSequence.SetTarget(cardRect); 

        // Fade in and scale up slightly while moving towards target
        animSequence.Append(canvasGroup.DOFade(1f, animDuration * 0.5f));
        animSequence.Join(cardRect.DOScale(Vector3.one * scaleFactor, animDuration).SetEase(Ease.OutQuad));
        animSequence.Join(cardRect.DOMove(targetPosition, animDuration).SetEase(Ease.OutQuad));

        // Pause at target
        animSequence.AppendInterval(0.6f); // Shorter pause than before

        // Fade out and Destroy
        animSequence.Append(canvasGroup.DOFade(0f, 0.3f).SetEase(Ease.InQuad));
        animSequence.OnComplete(() => {
            if (cardGO != null) Object.Destroy(cardGO);
            Debug.Log($"Finished visualizing: {card.cardName}");
        });

        animSequence.Play();

        // Wait for the animation sequence to finish before the coroutine ends
        yield return animSequence.WaitForCompletion();
        // --- END MODIFIED ---
    }
    
    public void UpdateOtherFightsUI()
    {
        if (othersStatusArea == null || otherPlayerStatusTemplate == null)
        {
            return;
        }

        // Clear previous entries (but keep the template itself)
        foreach (Transform child in othersStatusArea.transform)
        {
            if (child.gameObject != otherPlayerStatusTemplate.gameObject && child.gameObject.name != "OtherStatusTitle")
            {
                Object.Destroy(child.gameObject);
            }
        }

        // --- MODIFIED: Create a list to hold player status info for sorting ---
        List<PlayerStatusInfo> playerStatuses = new List<PlayerStatusInfo>();
        // --- END MODIFIED ---

        // Loop through players and gather their status
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.IsLocal) continue; // Skip self

            int oppPetHP = -1;
            int turnNum = -1;
            int playerHP = -1;
            int playerScore = 0; // Add score
            bool hpFound = false;
            bool turnFound = false;
            bool playerHpFound = false;
            bool scoreFound = false; // Add score flag

            if (player.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_OPP_PET_HP_PROP, out object hpObj))
            {
                try { oppPetHP = (int)hpObj; hpFound = true; } catch { /* Ignore */ }
            }
            if (player.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_TURN_PROP, out object turnObj))
            {
                 try { turnNum = (int)turnObj; turnFound = true; } catch { /* Ignore */ }
            }
            if (player.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP, out object playerHpObj))
            {
                 try { playerHP = (int)playerHpObj; playerHpFound = true; } catch { /* Ignore */ }
            }
            // --- ADDED: Get Player Score ---
            if (player.CustomProperties.TryGetValue(CombatStateManager.PLAYER_SCORE_PROP, out object scoreObj))
            {
                try { playerScore = (int)scoreObj; scoreFound = true; } catch { /* Ignore */ }
            }
            // --- END ADDED ---

            // Only display if we have valid data for all fields
            if (hpFound && turnFound && playerHpFound && scoreFound) // Check for scoreFound too
            {
                // --- MODIFIED: Add to list instead of instantiating directly ---
                playerStatuses.Add(new PlayerStatusInfo
                {
                    Nickname = player.NickName,
                    PlayerHP = playerHP,
                    TurnNum = turnNum,
                    OppPetHP = oppPetHP,
                    Score = playerScore
                });
                // --- END MODIFIED ---
            }
        }

        // --- ADDED: Sort the list by score descending ---
        playerStatuses.Sort((a, b) => b.Score.CompareTo(a.Score));
        // --- END ADDED ---

        // --- ADDED: Instantiate sorted entries ---
        foreach (var statusInfo in playerStatuses)
        {
            GameObject statusEntryGO = Object.Instantiate(otherPlayerStatusTemplate.gameObject, othersStatusArea.transform);
            statusEntryGO.name = $"Status_{statusInfo.Nickname}";
            TextMeshProUGUI statusText = statusEntryGO.GetComponent<TextMeshProUGUI>();
            
            if (statusText != null)
            {
                // Include Score in the text
                statusText.text = $"{statusInfo.Nickname} (Score: {statusInfo.Score}) | HP:{statusInfo.PlayerHP} | T{statusInfo.TurnNum}, OppHP: {statusInfo.OppPetHP}";
                statusEntryGO.SetActive(true);
            }
            else
            {
                Object.Destroy(statusEntryGO);
            }
        }
        // --- END ADDED ---
    }
    
    public void ClearAllHandCardObjects()
    {
        // Clear Player Hand
        if (playerHandPanel != null)
        {
            foreach (Transform child in playerHandPanel.transform)
            {
                if (child.gameObject.name != "CardTemplate")
                {
                     if (child.gameObject != null) {
                        DOTween.Kill(child.transform, true); // Kill tweens
                        Object.Destroy(child.gameObject);
                     }
                } else { child.gameObject.SetActive(false); }
            }
            Debug.Log("ClearAllHandCardObjects: Destroyed all card GameObjects in player hand panel");
        }

        // Clear Opponent Pet Hand
        if (opponentPetHandPanel != null)
        {
             foreach (Transform child in opponentPetHandPanel.transform)
            {
                if (child.gameObject.name != "CardTemplate")
                {
                     if (child.gameObject != null) {
                        DOTween.Kill(child.transform, true); // Kill tweens
                        Object.Destroy(child.gameObject);
                     }
                } else { child.gameObject.SetActive(false); }
            }
            Debug.Log("ClearAllHandCardObjects: Destroyed all card GameObjects in opponent pet hand panel");
        }
    }
    
    public GameObject GetPlayerHandPanel() => playerHandPanel;
    
    public void SetEndTurnButtonInteractable(bool interactable)
    {
        if (endTurnButton) endTurnButton.interactable = interactable;
    }
    
    public Button GetEndTurnButton() => endTurnButton;

    /// <summary>
    /// Call this method *BEFORE* removing the card data from the CardManager's hand list.
    /// It finds the associated GameObject and starts the discard animation.
    /// </summary>
    /// <param name="cardData">The data of the card to discard.</param>
    /// <param name="cardGOToDiscard">Optional specific GameObject instance to animate. If null, searches the hand panel.</param>
    public void TriggerDiscardAnimation(CardData cardData, GameObject cardGOToDiscard = null)
    {
        // If a specific GO is provided, use it directly
        if (cardGOToDiscard != null)
        {
            // --- ADDED: Check if the provided GO is actually suitable ---
            CardDragHandler handler = cardGOToDiscard.GetComponent<CardDragHandler>();
            CanvasGroup cg = cardGOToDiscard.GetComponent<CanvasGroup>();
            if (handler != null && handler.cardData == cardData && cardGOToDiscard.activeSelf && !handler.isDiscarding)
            {
                 Debug.Log($"[TriggerDiscardAnimation] Using provided GO {cardGOToDiscard.name} for {cardData.cardName}. Starting animation.");
                 // Set isDiscarding flag IMMEDIATELY
                 handler.isDiscarding = true;
                 Debug.Log($"[TriggerDiscardAnimation] Set isDiscarding = true for {cardGOToDiscard.name}");
                 // Start animation
                 gameManager.StartCoroutine(AnimateCardDiscardAndRemove(cardData, cardGOToDiscard));
                 return; // Successfully started animation on provided GO
            }
            else
            {
                // Provided GO was unsuitable (wrong data, inactive, already discarding, etc.)
                // Fall through to search logic below
                 Debug.LogWarning($"[TriggerDiscardAnimation] Provided GO {cardGOToDiscard.name} for {cardData.cardName} was unsuitable. Falling back to search.");
                 cardGOToDiscard = null; // Reset to null so search happens
            }
             // --- END ADDED ---
        }

        // --- MODIFIED: Search logic now only runs if cardGOToDiscard was null or unsuitable ---
        if (cardGOToDiscard == null)
        {
            Debug.Log($"[TriggerDiscardAnimation] Searching panel for {cardData.cardName} GO.");
            // Find the *first* active, non-animating GO matching the card data in the panel
            foreach (Transform child in playerHandPanel.transform)
            {
                if (child.gameObject.name == "CardTemplate") continue;

                CardDragHandler handler = child.GetComponent<CardDragHandler>();
                // CanvasGroup cg = child.GetComponent<CanvasGroup>(); // No longer needed here

                // Check if GO matches data, is active, and is not already discarding
                if (handler != null && handler.cardData == cardData && child.gameObject.activeSelf && !handler.isDiscarding)
                {
                    cardGOToDiscard = child.gameObject;
                    break; // Found a suitable GO to animate via search
                }
            }
        }
        // --- END MODIFIED ---


        if (cardGOToDiscard != null)
        {
            // --- MOVED: Logic from above ---
            Debug.Log($"[TriggerDiscardAnimation] Found GO {cardGOToDiscard.name} via search for {cardData.cardName}. Starting animation.");
            CardDragHandler handlerToDiscard = cardGOToDiscard.GetComponent<CardDragHandler>();
            if (handlerToDiscard != null)
            {
                handlerToDiscard.isDiscarding = true;
                Debug.Log($"[TriggerDiscardAnimation] Set isDiscarding = true for {cardGOToDiscard.name}");
                gameManager.StartCoroutine(AnimateCardDiscardAndRemove(cardData, cardGOToDiscard));
            }
            else
            {
                 Debug.LogWarning($"[TriggerDiscardAnimation] Could not find CardDragHandler on found {cardGOToDiscard.name} to set flag.");
                 // Should we still try to animate? Maybe just destroy?
                 Object.Destroy(cardGOToDiscard);
            }
             // --- END MOVED ---
        }
        else
        {
             // --- MODIFIED: Log message reflects search failure ---
            Debug.LogWarning($"[TriggerDiscardAnimation] Card {cardData.cardName} not found (or already animating discard) in hand panel via search.");
             // --- END MODIFIED ---
        }
    }

    private IEnumerator AnimateCardDiscardAndRemove(CardData cardData, GameObject cardGO)
    {
        if (cardGO == null) yield break; // GO already destroyed
        
        // Ensure cardGO is still active before starting animation
        if (!cardGO.activeSelf)
        {
            Debug.LogWarning($"[AnimateCardDiscardAndRemove] CardGO {cardGO.name} for {cardData.cardName} is inactive. Destroying immediately.");
            if (cardGO != null) Object.Destroy(cardGO); // Destroy if somehow still exists
            yield break;
        }

        RectTransform cardRect = cardGO.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = cardGO.GetComponent<CanvasGroup>() ?? cardGO.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false; // Disable interaction during animation

        float discardAnimDuration = 0.5f;
        float moveDistance = 300f;
        float randomAngle = Random.Range(-90f, 90f);
        Vector3 targetPosOffset = Quaternion.Euler(0, 0, Random.Range(-30f, 30f)) * Vector3.up * moveDistance;

        // Kill existing tweens
        DOTween.Kill(cardRect); 
        DOTween.Kill(canvasGroup);

        Sequence discardSequence = DOTween.Sequence();
        discardSequence.SetTarget(cardRect); // Associate tween

        // Playful burst animation
        discardSequence.Append(cardRect.DOLocalMove(cardRect.localPosition + targetPosOffset, discardAnimDuration).SetEase(Ease.OutQuad));
        discardSequence.Join(cardRect.DOLocalRotate(new Vector3(0, 0, randomAngle), discardAnimDuration).SetEase(Ease.OutQuad));
        discardSequence.Join(cardRect.DOScale(cardRect.localScale * 1.2f, discardAnimDuration * 0.4f).SetEase(Ease.OutQuad).SetLoops(2, LoopType.Yoyo)); // Optional pulse
        discardSequence.Insert(discardAnimDuration * 0.5f, canvasGroup.DOFade(0f, discardAnimDuration * 0.5f).SetEase(Ease.InQuad)); // Fade out in the second half

        // Set OnComplete to destroy the GameObject
        discardSequence.OnComplete(() => {
            if (cardGO != null) // Check if not already destroyed
            { 
                Object.Destroy(cardGO);
            }
        });

        discardSequence.Play();

        // Wait for the animation duration before the coroutine finishes (optional, but good practice)
        yield return new WaitForSeconds(discardAnimDuration);
    }

    /// <summary>
    /// Updates the UI for the Opponent Pet's hand.
    /// Instantiates/updates card GameObjects and disables interaction.
    /// </summary>
    public void UpdateOpponentPetHandUI()
    {
        if (opponentPetHandPanel == null) 
        { 
            // Debug.Log("UpdateOpponentPetHandUI: Opponent hand panel not found, skipping.");
            return; 
        }

        GameObject cardPrefab = gameManager.GetCardPrefab();
        // PlayerManager playerManager = gameManager.GetPlayerManager(); // Not needed directly for visuals?
        PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
        List<CardData> opponentHand = petDeckManager.GetOpponentPetHand();

        if (cardPrefab == null)
        {
            Debug.LogError("Cannot UpdateOpponentPetHandUI - CardPrefab is missing!");
            return;
        }
        
        // Simplified Logic compared to Player Hand (No drag handling complexity needed here)
        
        // 1. Clear existing card GOs (except template)
        foreach (Transform child in opponentPetHandPanel.transform)
        {
            if (child.gameObject.name != "CardTemplate")
            {
                DOTween.Kill(child, true); // Kill any animations
                Object.Destroy(child.gameObject);
            }
            else
            {
                child.gameObject.SetActive(false); // Keep template hidden
            }
        }
        
        // 2. Calculate layout transforms (reuse player hand logic, maybe scale down?)
        // TODO: Consider a separate layout function or scaling factor for opponent hand
        // List<System.Tuple<Vector3, Quaternion, Vector3>> targetTransforms = CalculateLayoutTransforms(opponentHand.Count);
        List<System.Tuple<Vector3, Quaternion, Vector3>> targetTransforms = CalculateLayoutTransforms(opponentHand.Count, opponentPetHandPanel.GetComponent<RectTransform>()); // Pass panel rect
        float opponentCardScaleFactor = 0.8f; // Example: Make opponent cards smaller

        // 3. Instantiate and position new card GOs
        for (int i = 0; i < opponentHand.Count; i++)
        {
            CardData cardData = opponentHand[i];
            GameObject newGO = Object.Instantiate(cardPrefab, opponentPetHandPanel.transform);
            newGO.name = $"OpponentCard_{cardData.cardName}_{i}"; // Simpler naming

            // Update visuals (Use playerManager context for cost mods? Opponent perspective?) 
            // For now, use local player manager for simplicity, assuming opponent pet cost mods aren't visualized
            UpdateCardVisuals(newGO, cardData, gameManager.GetPlayerManager(), gameManager.GetCardManager());
            
            // Disable interaction
            CardDragHandler dragHandler = newGO.GetComponent<CardDragHandler>();
            if (dragHandler != null) dragHandler.enabled = false;
            CanvasGroup canvasGroup = newGO.GetComponent<CanvasGroup>() ?? newGO.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false; // Disable raycasting
            
            // Apply calculated transform
            RectTransform cardRect = newGO.GetComponent<RectTransform>();
            System.Tuple<Vector3, Quaternion, Vector3> target = targetTransforms[i];
            cardRect.localPosition = target.Item1;
            cardRect.localRotation = target.Item2;
            cardRect.localScale = target.Item3 * opponentCardScaleFactor; // Apply scale factor
            
            newGO.SetActive(true);
        }
        // Optional: Add simple fade-in/pop-in animation later if desired
    }
    
    // --- ADDED: Getters for Area Transforms (for Target Effects) --- 
    public GameObject GetOpponentPetUIArea() => opponentPetNameText?.transform.parent.gameObject; // Assumes name text is direct child of the area GO
    public GameObject GetPlayerUIArea() => playerNameText?.transform.parent.gameObject; // Assumes name text is direct child of the stats row
    public GameObject GetOwnPetUIArea() => ownPetNameText?.transform.parent.gameObject; // Assumes name text is direct child of the area GO
    // --- END ADDED ---

    // --- ADDED: Getter for Combat Root Instance ---
    public GameObject GetCombatRootInstance() => combatRootInstance;
    // --- END ADDED ---

    // --- ADDED: Coroutine to delay sibling resorting ---
    private IEnumerator DelayedSiblingResort()
    {
        yield return null; // Wait one frame for GOs to be fully initialized?
        if (handPanelHoverManager != null)
        {
            // --- MODIFIED: Call public method directly ---
            handPanelHoverManager.ResortSiblingIndices();
            Debug.Log("[CombatUIManager] Called ResortSiblingIndices after delay.");
             // --- Removed reflection code ---
        }
    }
    // --- END ADDED ---

    // --- ADDED: Method to show debug mode status ---
    public void ShowDebugModeStatus(string debugModeName, bool isEnabled)
    {
        if (combatRootInstance == null) return;
        
        // Create debug status text if it doesn't exist
        if (debugStatusText == null)
        {
            GameObject debugTextGO = new GameObject("DebugStatusText");
            debugTextGO.transform.SetParent(combatRootInstance.transform, false);
            
            // Add components
            RectTransform rectTransform = debugTextGO.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(10, -10);
            rectTransform.sizeDelta = new Vector2(300, 50);
            
            // Add Text
            debugStatusText = debugTextGO.AddComponent<TextMeshProUGUI>();
            
            // Try to load the font from Resources
            TMP_FontAsset fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            
            // If font not found, try to find any font in the project
            if (fontAsset == null)
            {
                Debug.LogWarning("Could not load LiberationSans SDF font, using a fallback");
                fontAsset = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Length > 0 
                    ? Resources.FindObjectsOfTypeAll<TMP_FontAsset>()[0] 
                    : null;
            }
            
            // Apply font if found
            if (fontAsset != null)
            {
                debugStatusText.font = fontAsset;
            }
            
            debugStatusText.fontSize = 18;
            debugStatusText.color = Color.yellow;
            debugStatusText.alignment = TextAlignmentOptions.TopLeft;
            
            // Add background
            Image background = debugTextGO.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.7f);
            
            // Make sure it's displayed on top
            Canvas canvas = debugTextGO.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000; // High value to ensure it's on top
            
            debugTextGO.AddComponent<CanvasRenderer>();
        }
        
        // Update the text
        debugStatusText.text = $"{debugModeName}: {(isEnabled ? "ENABLED" : "DISABLED")}";
        debugStatusText.gameObject.SetActive(true);
        
        // Cancel previous fade coroutine if it's running
        if (currentDebugTextCoroutine != null)
        {
            gameManager.StopCoroutine(currentDebugTextCoroutine);
        }
        
        // Start new fade coroutine
        currentDebugTextCoroutine = gameManager.StartCoroutine(FadeOutDebugText(5f));
    }
    
    private IEnumerator FadeOutDebugText(float delay)
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(delay);
        
        // Fade out the text
        if (debugStatusText != null)
        {
            float duration = 1.0f;
            float startTime = Time.time;
            Color startColor = debugStatusText.color;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0);
            
            while (Time.time < startTime + duration)
            {
                float t = (Time.time - startTime) / duration;
                debugStatusText.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }
            
            debugStatusText.color = endColor;
            debugStatusText.gameObject.SetActive(false);
            debugStatusText.color = new Color(startColor.r, startColor.g, startColor.b, 1);
        }
        
        currentDebugTextCoroutine = null;
    }
    // --- END ADDED ---
} 