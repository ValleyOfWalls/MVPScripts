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
                Debug.Log("[CardTargetingSystem] Card Owner (ICombatant) is null.");
                return;
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
            if (source == target) return false;
            
            // If one is a player and one is a pet, check ownership
            CombatPlayer sourcePlayer = source as CombatPlayer;
            CombatPet targetPet = target as CombatPet;
            CombatPet sourcePet = source as CombatPet;
            CombatPlayer targetPlayer = target as CombatPlayer;

            if (sourcePlayer != null && targetPet != null)
            {
                // If target pet's reference owner matches source player's network player, they are NOT enemies
                return targetPet.ReferencePet?.PlayerOwner != sourcePlayer.NetworkPlayer;
            }
            else if (sourcePet != null && targetPlayer != null)
            {
                // If source pet's reference owner matches target player's network player, they are NOT enemies
                return sourcePet.ReferencePet?.PlayerOwner != targetPlayer.NetworkPlayer;
            }
            
            // Check if both are Pets or both are Players
            if ((source is CombatPet && target is CombatPet) || (source is CombatPlayer && target is CombatPlayer))
            {
                 // If they are the same type but not the same instance, they are enemies
                 return true; 
            }
            
            // Fallback if types are mixed in an unexpected way or references are missing
             Debug.LogWarning($"IsEnemy check fallback for types {source.GetType()} and {target.GetType()}. Assuming enemy.");
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
                
                // *** NEW CODE: Find and remove the played card ***
                if (source is CombatPlayer sourcePlayer && sourcePlayer.NetworkPlayer != null)
                {
                    // Get the player's hand
                    PlayerHand playerHand = sourcePlayer.NetworkPlayer.PlayerHand;
                    if (playerHand != null)
                    {
                        // Find and remove the played card from hand
                        Card playedCard = null;
                        List<Card> cardsInHand = playerHand.GetCardsInHand();
                        
                        for (int i = 0; i < cardsInHand.Count; i++)
                        {
                            Card card = cardsInHand[i];
                            if (card != null && card.CardName == cardName)
                            {
                                playedCard = card;
                                // Remove the card from hand using existing method
                                int cardIndex = i;
                                playerHand.ServerRemoveCard(cardName);
                                
                                Debug.Log($"[Server] Removed card '{cardName}' from player's hand at index {cardIndex}");
                                break;
                            }
                        }
                        
                        // If we found the card but it wasn't removed through ServerRemoveCard
                        if (playedCard != null && playedCard.NetworkObject != null && playedCard.NetworkObject.IsSpawned)
                        {
                            Debug.Log($"[Server] Despawning card GameObject '{playedCard.name}'");
                            InstanceFinder.ServerManager.Despawn(playedCard.gameObject);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Server] Could not find PlayerHand for {sourcePlayer.NetworkPlayer.GetSteamName()} to remove played card");
                    }
                }
                // *** END NEW CODE ***
            }
            else
            {
                 Debug.Log($"[Server] Skipping card effect application for {cardName} due to insufficient energy.");
            }
        }
    }
} 