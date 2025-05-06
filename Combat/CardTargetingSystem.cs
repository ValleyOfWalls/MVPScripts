using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
using UnityEngine.UI;
using FishNet;
//
namespace Combat
{
    public class CardTargetingSystem : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private LineRenderer targetingLine;
        [SerializeField] private GameObject targetHighlightPrefab;
        
        [Header("Settings")]
        [SerializeField] private Color validTargetColor = Color.green;
        [SerializeField] private Color invalidTargetColor = Color.red;
        [SerializeField] private float lineWidth = 0.05f;
        
        // Internal state
        private Card currentDraggedCard;
        private Vector3 dragStartPosition;
        private ICombatant currentHoveredTarget;
        private GameObject currentTargetHighlight;
        private bool isTargeting = false;
        
        // List of valid targets for the current card
        private List<ICombatant> validTargets = new List<ICombatant>();
        
        // Singleton instance
        public static CardTargetingSystem Instance { get; private set; }
        //
        private void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            // Initialize targeting line if not assigned
            if (targetingLine == null)
            {
                targetingLine = gameObject.AddComponent<LineRenderer>();
                targetingLine.startWidth = lineWidth;
                targetingLine.endWidth = lineWidth;
                targetingLine.positionCount = 2;
                targetingLine.enabled = false;
                
                // Set default material if none exists
                if (targetingLine.material == null)
                {
                    targetingLine.material = new Material(Shader.Find("Sprites/Default"));
                }
            }
        }
        
        public void StartTargeting(Card card, Vector3 startPosition)
        {
            if (card == null) return;
            
            currentDraggedCard = card;
            dragStartPosition = startPosition;
            isTargeting = true;
            
            // Find all valid targets for this card
            FindValidTargets(card);
            
            // Show targeting line
            if (targetingLine != null)
            {
                targetingLine.enabled = true;
                targetingLine.SetPosition(0, dragStartPosition);
                targetingLine.SetPosition(1, dragStartPosition);
                targetingLine.startColor = validTargetColor;
                targetingLine.endColor = validTargetColor;
            }
            
            // Create highlight for valid targets
            HighlightValidTargets(true);
        }
        
        public void UpdateTargeting(Vector3 currentPosition)
        {
            if (!isTargeting) return;
            
            // Update line renderer position
            if (targetingLine != null)
            {
                targetingLine.SetPosition(1, currentPosition);
            }
            
            // Check if we're hovering over a valid target
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D[] hits = Physics2D.RaycastAll(ray.origin, ray.direction);
            
            ICombatant newTarget = null;
            
            foreach (RaycastHit2D hit in hits)
            {
                // Check for combatant components
                CombatPet petTarget = hit.collider.GetComponent<CombatPet>();
                if (petTarget != null)
                {
                    newTarget = petTarget;
                    break;
                }
                
                CombatPlayer playerTarget = hit.collider.GetComponent<CombatPlayer>();
                if (playerTarget != null)
                {
                    newTarget = playerTarget;
                    break;
                }
            }
            
            // If target changed, update highlight
            if (newTarget != currentHoveredTarget)
            {
                UpdateTargetHighlight(newTarget);
            }
        }
        
        public bool EndTargeting(Vector3 endPosition)
        {
            if (!isTargeting) return false;
            
            Debug.Log("[CardTargetingSystem] EndTargeting called.");

            // --- Perform final target check at drop position --- 
            Ray ray = Camera.main.ScreenPointToRay(endPosition); // Use endPosition (mouse position at drop)
            RaycastHit2D[] hits = Physics2D.RaycastAll(ray.origin, ray.direction);
            
            ICombatant finalTarget = null;
            GameObject hitObject = null;
            int finalTargetInstanceID = -1; // Added for logging

            foreach (RaycastHit2D hit in hits)
            {
                hitObject = hit.collider.gameObject;
                // Check for combatant components on the hit object
                CombatPet petTarget = hit.collider.GetComponent<CombatPet>();
                if (petTarget != null)
                {
                    finalTarget = petTarget;
                    finalTargetInstanceID = (finalTarget as Component).GetInstanceID(); // Get ID
                    Debug.Log($"[CardTargetingSystem] Final raycast hit Pet: {hitObject.name} (InstanceID: {finalTargetInstanceID})");
                    break;
                }
                
                CombatPlayer playerTarget = hit.collider.GetComponent<CombatPlayer>();
                if (playerTarget != null)
                {
                    finalTarget = playerTarget;
                    finalTargetInstanceID = (finalTarget as Component).GetInstanceID(); // Get ID
                    Debug.Log($"[CardTargetingSystem] Final raycast hit Player: {hitObject.name} (InstanceID: {finalTargetInstanceID})");
                    break;
                }
            }
            
            if (finalTarget == null && hitObject != null)
            {
                 Debug.Log($"[CardTargetingSystem] Final raycast hit object '{hitObject.name}' but it has no ICombatant.");
            }
            else if (finalTarget == null)
            {
                 Debug.Log("[CardTargetingSystem] Final raycast hit nothing relevant.");
            }
            // --- End final target check --- 

            // Check contains before logging
            bool targetIsInList = finalTarget != null && validTargets.Contains(finalTarget);
            Debug.Log($"[CardTargetingSystem] Target Found: {(finalTarget != null ? (finalTarget as Component).name : "None")} (InstanceID: {finalTargetInstanceID}), Target In validTargets List: {targetIsInList}");

            bool isValidTarget = targetIsInList; // Use the calculated check
            Debug.Log($"[CardTargetingSystem] Overall IsValid: {isValidTarget}");
            
            // Apply card effect if dropped on valid target
            if (isValidTarget && currentDraggedCard != null)
            {
                Debug.Log($"[CardTargetingSystem] Valid target found. Requesting server to apply '{currentDraggedCard.CardName}'. Source: {(currentDraggedCard.Owner as Component).name}, Target: {(finalTarget as Component).name}");

                // --- Add check for client state and arguments ---
                // --- Check 'this' and ClientManager first ---
                Debug.Log($"[CardTargetingSystem] Checking 'this' NetworkObject: IsNull? {this.NetworkObject == null}, IsSpawned? {this.NetworkObject?.IsSpawned}");
                Debug.Log($"[CardTargetingSystem] Checking ClientManager: IsNull? {ClientManager == null}, Started? {ClientManager?.Started}");

                if (ClientManager != null && ClientManager.Started)
                {
                    // --- Null Check Arguments BEFORE calling RPC --- 
                    ICombatant sourceCombatant = currentDraggedCard.Owner;
                    CardData cardData = currentDraggedCard.Data;
                    NetworkObject sourceNO = GetNetworkObjectFromCombatant(sourceCombatant);
                    NetworkObject targetNO = GetNetworkObjectFromCombatant(finalTarget);

                    if (cardData == null)
                    {
                        Debug.LogError($"[CardTargetingSystem] Cannot apply effect: CardData is null for {currentDraggedCard.name}");
                        isValidTarget = false;
                    }
                    else if (sourceCombatant == null)
                    {
                        Debug.LogError($"[CardTargetingSystem] Cannot apply effect: Source Combatant (Owner) is null for {cardData.cardName}");
                        isValidTarget = false;
                    }
                    else if (finalTarget == null)
                    {
                         Debug.LogError($"[CardTargetingSystem] Cannot apply effect: Final Target is null for {cardData.cardName}");
                         isValidTarget = false;
                    }
                    else if (sourceNO == null)
                    {
                        Debug.LogError($"[CardTargetingSystem] Cannot apply effect: Source NetworkObject is null for Source: {(sourceCombatant as Component).name} when playing {cardData.cardName}");
                        isValidTarget = false;
                    }
                    else if (targetNO == null)
                    {
                         Debug.LogError($"[CardTargetingSystem] Cannot apply effect: Target NetworkObject is null for Target: {(finalTarget as Component).name} when playing {cardData.cardName}");
                         isValidTarget = false;
                    }
                    else
                    {    // --- All checks passed, send RPC --- 
                        
                        // --- Final Sanity Check Logging --- 
                        Debug.Log($"[CardTargetingSystem] Pre-RPC Check: sourceNO valid? {sourceNO != null}, sourceNO.IsSpawned? {sourceNO?.IsSpawned}, sourceNO.ObjectId? {sourceNO?.ObjectId}");
                        Debug.Log($"[CardTargetingSystem] Pre-RPC Check: targetNO valid? {targetNO != null}, targetNO.IsSpawned? {targetNO?.IsSpawned}, targetNO.ObjectId? {targetNO?.ObjectId}");
                        Debug.Log($"[CardTargetingSystem] Pre-RPC Check: cardData valid? {cardData != null}, cardData.cardName? {cardData?.cardName}");
                        // --- End Sanity Check --- 

                        // Request the server to apply the card effect
                        CmdApplyCardEffect(
                            sourceNO,
                            targetNO, 
                            cardData.cardName
                        );
                    }
                }
                else if (ClientManager == null)
                {
                     Debug.LogError("[CardTargetingSystem] Cannot send CmdApplyCardEffect because ClientManager is NULL!");
                     isValidTarget = false;
                }
                else // ClientManager exists but is not Started
                {
                    Debug.LogError("[CardTargetingSystem] Cannot send CmdApplyCardEffect because ClientManager is not started!");
                    isValidTarget = false; // Mark as invalid if we can't send RPC
                }
                // --- End check --- 
            }
            else if (currentDraggedCard != null)
            {
                 Debug.Log($"[CardTargetingSystem] Target invalid or card is null. Card will return to hand.");
            }
            
            // Clean up
            if (targetingLine != null)
            {
                targetingLine.enabled = false;
            }
            
            // Remove highlights
            HighlightValidTargets(false);
            if (currentTargetHighlight != null)
            {
                Destroy(currentTargetHighlight);
                currentTargetHighlight = null;
            }
            
            // Reset state
            currentDraggedCard = null;
            currentHoveredTarget = null; // Reset hovered target too
            isTargeting = false;
            validTargets.Clear(); // Clear valid targets list
            
            return isValidTarget; // Return whether the play was initiated
        }
        
        private void FindValidTargets(Card card)
        {
            validTargets.Clear();
            Debug.Log("[CardTargetingSystem] Finding valid targets..."); // Log start
            
            if (card == null || card.Data == null) 
            {
                Debug.Log("[CardTargetingSystem] Card or CardData is null.");
                return;
            }
            
            // Get the owner of the card
            ICombatant cardOwner = card.Owner;
            if (cardOwner == null) 
            {
                Debug.Log("[CardTargetingSystem] Card Owner (ICombatant) is null. Attempting to find owner through alternative means...");
                
                // Fallback 1: Try to get the owner through the hand
                PlayerHand playerHand = card.GetComponentInParent<PlayerHand>();
                if (playerHand != null && playerHand.NetworkPlayer != null && playerHand.NetworkPlayer.CombatPlayer != null)
                {
                    cardOwner = playerHand.NetworkPlayer.CombatPlayer;
                    Debug.Log($"[CardTargetingSystem] Found owner through PlayerHand: {(cardOwner as Component).name}");
                }
                
                // Fallback 2: Try to get the local player's combat player as owner
                if (cardOwner == null && IsClient)
                {
                    // Find the local player
                    NetworkPlayer localPlayer = null;
                    NetworkPlayer[] allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
                    foreach (NetworkPlayer player in allPlayers)
                    {
                        if (player.IsOwner)
                        {
                            localPlayer = player;
                            break;
                        }
                    }
                    
                    if (localPlayer != null && localPlayer.CombatPlayer != null)
                    {
                        cardOwner = localPlayer.CombatPlayer;
                        Debug.Log($"[CardTargetingSystem] Using local player as card owner: {(cardOwner as Component).name}");
                    }
                }
                
                // If we still don't have an owner, we can't continue
                if (cardOwner == null)
                {
                    Debug.LogError("[CardTargetingSystem] Failed to find card owner through fallbacks. Cannot determine valid targets.");
                    return;
                }
            }
            
            Debug.Log($"[CardTargetingSystem] Card Owner: {(cardOwner as Component).name} (InstanceID: {(cardOwner as Component).GetInstanceID()})");
            
            // Get all combatants in the scene
            List<ICombatant> allCombatants = new List<ICombatant>();
            
            // Add all CombatPets
            CombatPet[] pets = FindObjectsByType<CombatPet>(FindObjectsSortMode.None);
            allCombatants.AddRange(pets);
            Debug.Log($"[CardTargetingSystem] Found {pets.Length} CombatPets.");
            
            // Add all CombatPlayers
            CombatPlayer[] players = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
            allCombatants.AddRange(players);
            Debug.Log($"[CardTargetingSystem] Found {players.Length} CombatPlayers.");
            
            // Determine valid targets based on card targeting rules
            foreach (CardEffect effect in card.Data.cardEffects)
            {
                Debug.Log($"[CardTargetingSystem] Checking effect: {effect.effectType} with TargetType: {effect.targetType}");
                switch (effect.targetType)
                {
                    case TargetType.Self:
                        // Can target self (card owner)
                        if (!validTargets.Contains(cardOwner))
                        {
                            validTargets.Add(cardOwner);
                            Debug.Log($" - Added Self: {(cardOwner as Component).name} (InstanceID: {(cardOwner as Component).GetInstanceID()})");
                        }
                        break;
                        
                    case TargetType.SingleEnemy:
                    case TargetType.AllEnemies: // Logic is same for finding potential targets
                        // Find enemies of the card owner
                        foreach (ICombatant combatant in allCombatants)
                        {
                            bool isEnemy = IsEnemy(cardOwner, combatant);
                            if (isEnemy && !validTargets.Contains(combatant))
                            {
                                validTargets.Add(combatant);
                                Debug.Log($" - Added Enemy: {(combatant as Component).name} (InstanceID: {(combatant as Component).GetInstanceID()})");
                            }
                            else if (!isEnemy && combatant != cardOwner)
                            {
                                //Debug.Log($" - Skipping Ally: {(combatant as Component).name}");
                            }
                        }
                        break;
                        
                    case TargetType.SingleAlly:
                    case TargetType.AllAllies: // Logic is same for finding potential targets
                        // Find allies of the card owner (excluding self)
                        foreach (ICombatant combatant in allCombatants)
                        {
                            if (combatant != cardOwner && !IsEnemy(cardOwner, combatant) && !validTargets.Contains(combatant))
                            {
                                validTargets.Add(combatant);
                                Debug.Log($" - Added Ally: {(combatant as Component).name} (InstanceID: {(combatant as Component).GetInstanceID()})");
                            }
                            else if (IsEnemy(cardOwner, combatant))
                            {
                                //Debug.Log($" - Skipping Enemy: {(combatant as Component).name}");
                            }
                        }
                        break;
                }
            }
            Debug.Log($"[CardTargetingSystem] Found {validTargets.Count} total valid targets.");
        }
        
        private bool IsEnemy(ICombatant source, ICombatant target)
        {
            if (source == null || target == null)
            {
                Debug.LogError($"[CardTargetingSystem] IsEnemy check received null source or target. Source null: {source == null}, Target null: {target == null}");
                return false;
            }
            
            if (source == target) return false;
            
            // If one is a player and one is a pet, check ownership
            CombatPlayer sourcePlayer = source as CombatPlayer;
            CombatPet targetPet = target as CombatPet;
            CombatPet sourcePet = source as CombatPet;
            CombatPlayer targetPlayer = target as CombatPlayer;

            // Log helpful debug information
            string sourceType = source.GetType().Name;
            string targetType = target.GetType().Name;
            int sourceID = (source as Component)?.GetInstanceID() ?? -1;
            int targetID = (target as Component)?.GetInstanceID() ?? -1;
            
            Debug.Log($"[CardTargetingSystem] IsEnemy check: Source=[{sourceType}:{sourceID}], Target=[{targetType}:{targetID}]");

            if (sourcePlayer != null && targetPet != null)
            {
                if (targetPet.ReferencePet == null)
                {
                    Debug.LogWarning($"[CardTargetingSystem] Target pet has null ReferencePet: {targetPet.name}");
                    return true; // Assume enemy if we can't determine
                }
                
                bool isEnemy = targetPet.ReferencePet.PlayerOwner != sourcePlayer.NetworkPlayer;
                Debug.Log($"[CardTargetingSystem] Player-Pet check: {sourcePlayer.name} vs {targetPet.name}, isEnemy={isEnemy}");
                return isEnemy;
            }
            else if (sourcePet != null && targetPlayer != null)
            {
                if (sourcePet.ReferencePet == null)
                {
                    Debug.LogWarning($"[CardTargetingSystem] Source pet has null ReferencePet: {sourcePet.name}");
                    return true; // Assume enemy if we can't determine
                }
                
                bool isEnemy = sourcePet.ReferencePet.PlayerOwner != targetPlayer.NetworkPlayer;
                Debug.Log($"[CardTargetingSystem] Pet-Player check: {sourcePet.name} vs {targetPlayer.name}, isEnemy={isEnemy}");
                return isEnemy;
            }
            
            // Check if both are Pets or both are Players
            if ((source is CombatPet && target is CombatPet) || (source is CombatPlayer && target is CombatPlayer))
            {
                // Try to compare owners if both are pets
                if (source is CombatPet sourcePet2 && target is CombatPet targetPet2 &&
                    sourcePet2.ReferencePet != null && targetPet2.ReferencePet != null)
                {
                    bool sameOwner = sourcePet2.ReferencePet.PlayerOwner == targetPet2.ReferencePet.PlayerOwner;
                    Debug.Log($"[CardTargetingSystem] Pet-Pet check with owners: {sourcePet2.name} vs {targetPet2.name}, sameOwner={sameOwner}");
                    return !sameOwner; // If same owner, not enemies
                }
                
                 // If they are the same type but not the same instance, they are enemies
                 bool isEnemy = true;
                 Debug.Log($"[CardTargetingSystem] Same type check: {sourceType} vs {targetType}, isEnemy={isEnemy}");
                 return isEnemy; 
            }
            
            // Fallback if types are mixed in an unexpected way or references are missing
            Debug.LogWarning($"[CardTargetingSystem] IsEnemy check fallback for types {source.GetType()} and {target.GetType()}. Assuming enemy.");
            return true; 
        }
        
        private void HighlightValidTargets(bool highlight)
        {
            // Add visual highlight to all valid targets
            foreach (ICombatant target in validTargets)
            {
                // Updated to work with Image component
                if (target is MonoBehaviour targetMono)
                {
                    Image image = targetMono.GetComponentInChildren<Image>(); // Check children too
                    if (image == null) image = targetMono.GetComponent<Image>(); // Check on the object itself

                    if (image != null)
                    {
                        image.color = highlight ? new Color(1f, 1f, 0.8f, image.color.a) : new Color(1f, 1f, 1f, image.color.a); // Tint color, keep alpha
                    }
                    else
                    {
                         Debug.LogWarning($"HighlightValidTargets: Could not find Image component on valid target {(target as Component).name}");
                    }
                }
            }
        }
        
        private void UpdateTargetHighlight(ICombatant newTarget)
        {
            // Remove old highlight
            if (currentTargetHighlight != null)
            {
                Destroy(currentTargetHighlight);
                currentTargetHighlight = null;
            }
            
            currentHoveredTarget = newTarget; // Update the currently hovered target
            
            // Create new highlight if target is valid
            if (newTarget != null && validTargets.Contains(newTarget) && targetHighlightPrefab != null)
            {
                if (newTarget is MonoBehaviour targetMono)
                {
                    // Use the Image's transform if available, otherwise the MonoBehaviour's transform
                    Transform parentTransform = targetMono.transform;
                    Image targetImage = targetMono.GetComponentInChildren<Image>() ?? targetMono.GetComponent<Image>();
                    if(targetImage != null) parentTransform = targetImage.transform;

                    currentTargetHighlight = Instantiate(targetHighlightPrefab, parentTransform.position, Quaternion.identity);
                    currentTargetHighlight.transform.SetParent(parentTransform); // Parent to the image/target transform
                }
            }
            
            // Update line color based on target validity
            if (targetingLine != null)
            {
                bool isValid = newTarget != null && validTargets.Contains(newTarget);
                targetingLine.endColor = isValid ? validTargetColor : invalidTargetColor;
            }
        }
        
        private NetworkObject GetNetworkObjectFromCombatant(ICombatant combatant)
        {
            if (combatant is NetworkBehaviour networkBehaviour)
            {
                return networkBehaviour.NetworkObject;
            }
            return null;
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void CmdApplyCardEffect(NetworkObject sourceObject, NetworkObject targetObject, string cardName)
        {
            Debug.Log($"[Server] CmdApplyCardEffect received. Card: {cardName}, Source: {sourceObject.name}, Target: {targetObject.name}");
            
            // Get combatants from network objects
            ICombatant source = sourceObject.GetComponent<ICombatant>();
            ICombatant target = targetObject.GetComponent<ICombatant>();
            
            if (source == null || target == null)
            {
                Debug.LogError($"[Server] Invalid source ({sourceObject.name}) or target ({targetObject.name}) for card effect {cardName}");
                return;
            }
            
            // Find the card data
            CardData cardData = DeckManager.Instance.FindCardByName(cardName);
            if (cardData == null)
            {
                Debug.LogError($"[Server] Cannot find card data for {cardName}");
                return;
            }
            
            // Check Energy on Server
            bool hasEnoughEnergy = true;
            if (source is CombatPlayer player)
            {
                 if (player.CurrentEnergy < cardData.manaCost)
                 {
                     Debug.LogWarning($"[Server] Player {player.NetworkPlayer.GetSteamName()} does not have enough energy for {cardName}. Has: {player.CurrentEnergy}, Needs: {cardData.manaCost}");
                     hasEnoughEnergy = false;
                     // TODO: Send notification back to client about insufficient energy?
                 }
            }
            
            if (hasEnoughEnergy)
            {
                Debug.Log($"[Server] Applying card effect for {cardName}...");
                // Apply card effects (This method already has logging)
                CardEffectProcessor.ApplyCardEffects(cardData, source, target);
                
                // If source is a CombatPlayer, spend energy
                if (source is CombatPlayer p)
                {
                    p.SyncEnergy.Value -= cardData.manaCost;
                    Debug.Log($"[Server] Player {p.NetworkPlayer.GetSteamName()} spent {cardData.manaCost} energy. Remaining: {p.SyncEnergy.Value}");
                }
                
                // Notify the card was played (e.g., for achievements, quests, etc.)
                // Ensure CombatManager instance is available
                if (CombatManager.Instance != null)
                {
                    CombatManager.Instance.NotifyCardPlayed(cardName, source, target);
                }
                else
                {
                    Debug.LogWarning("[Server] CombatManager.Instance is null, cannot notify card played.");
                }
                
                // *** Log before checking the source player's references ***
                if (source is CombatPlayer spCheck) // Use different variable name to avoid conflict
                {
                     Debug.Log($"[Server] Checking source player: {spCheck.name}. NetworkPlayer is null? {spCheck.NetworkPlayer == null}");
                     if(spCheck.NetworkPlayer != null) 
                     {
                          Debug.Log($"[Server] Source player's NetworkPlayer: {spCheck.NetworkPlayer.name}. PlayerHand is null? {spCheck.NetworkPlayer.PlayerHand == null}");
                     }
                }
                else
                {
                     Debug.Log($"[Server] Source is not a CombatPlayer ({source.GetType().Name}), skipping card removal from hand.");
                }
                
                // Find and remove the played card 
                if (source is CombatPlayer sourcePlayer && sourcePlayer.NetworkPlayer != null)
                {
                    // Get the player's hand
                    PlayerHand playerHand = sourcePlayer.NetworkPlayer.PlayerHand;
                    if (playerHand != null)
                    {
                        // --- ADD INSTANCE ID LOG --- 
                        Debug.Log($"[Server CmdApplyCardEffect] Checking PlayerHand Instance ID: {playerHand.GetInstanceID()} for player {sourcePlayer.NetworkPlayer.GetSteamName()}");
                        
                        // Get a copy of the list
                        List<Card> cardsInHand = playerHand.GetCardsInHand();
                        Debug.Log($"[Server] Checking hand for card '{cardName}'. Server hand list count: {cardsInHand.Count}"); // Log count
                        
                        for (int i = 0; i < cardsInHand.Count; i++)
                        {
                            Card card = cardsInHand[i];
                            // Log each card being checked
                            string serverCardName = (card != null) ? card.CardName : "NULL_CARD";
                            Debug.Log($"[Server] Checking index {i}: Server Card Name = '{serverCardName}', Looking for = '{cardName}'"); 
                                                        
                            if (card != null && card.CardName == cardName)
                            {
                                // Remove the card from hand using existing method
                                int cardIndex = i;
                                Debug.Log($"[Server] Match found! Calling ServerRemoveCard for '{cardName}' at index {cardIndex}");
                                playerHand.ServerRemoveCard(cardName);
                                
                                // Log removed separately, though ServerRemoveCard should now log sending RPCs
                                //Debug.Log($"[Server] Removed card '{cardName}' from player's hand at index {cardIndex}");
                                break;
                            }
                        }
                        
                        // Log if loop finishes without finding the card
                        if (cardsInHand.Count == 0) 
                        {
                             Debug.LogWarning($"[Server] Loop finished. Card '{cardName}' was NOT found in server's hand list copy.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Server] Could not find PlayerHand for {sourcePlayer.NetworkPlayer.GetSteamName()} to remove played card");
                    }
                }
            }
            else
            {
                 Debug.Log($"[Server] Skipping card effect application for {cardName} due to insufficient energy.");
            }
        }
    }
} 