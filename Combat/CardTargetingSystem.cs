using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;
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
            if (!IsOwner || card == null) return;
            
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
            if (!isTargeting || !IsOwner) return;
            
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
            if (!isTargeting || !IsOwner) return false;
            
            bool validTarget = currentHoveredTarget != null && validTargets.Contains(currentHoveredTarget);
            
            // Apply card effect if dropped on valid target
            if (validTarget && currentDraggedCard != null)
            {
                // Request the server to apply the card effect
                CmdApplyCardEffect(
                    GetNetworkObjectFromCombatant(currentDraggedCard.Owner), 
                    GetNetworkObjectFromCombatant(currentHoveredTarget),
                    currentDraggedCard.Data.cardName
                );
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
            currentHoveredTarget = null;
            isTargeting = false;
            
            return validTarget;
        }
        
        private void FindValidTargets(Card card)
        {
            validTargets.Clear();
            
            if (card == null || card.Data == null) return;
            
            // Get the owner of the card
            ICombatant cardOwner = card.Owner;
            if (cardOwner == null) return;
            
            // Get all combatants in the scene
            List<ICombatant> allCombatants = new List<ICombatant>();
            
            // Add all CombatPets
            CombatPet[] pets = FindObjectsByType<CombatPet>(FindObjectsSortMode.None);
            allCombatants.AddRange(pets);
            
            // Add all CombatPlayers
            CombatPlayer[] players = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
            allCombatants.AddRange(players);
            
            // Determine valid targets based on card targeting rules
            foreach (CardEffect effect in card.Data.cardEffects)
            {
                switch (effect.targetType)
                {
                    case TargetType.Self:
                        // Can target self (card owner)
                        if (!validTargets.Contains(cardOwner))
                            validTargets.Add(cardOwner);
                        break;
                        
                    case TargetType.SingleEnemy:
                        // Find enemies of the card owner
                        foreach (ICombatant combatant in allCombatants)
                        {
                            if (IsEnemy(cardOwner, combatant) && !validTargets.Contains(combatant))
                                validTargets.Add(combatant);
                        }
                        break;
                        
                    case TargetType.AllEnemies:
                        // All enemies are valid targets
                        foreach (ICombatant combatant in allCombatants)
                        {
                            if (IsEnemy(cardOwner, combatant) && !validTargets.Contains(combatant))
                                validTargets.Add(combatant);
                        }
                        break;
                        
                    case TargetType.SingleAlly:
                        // Find allies of the card owner
                        foreach (ICombatant combatant in allCombatants)
                        {
                            if (!IsEnemy(cardOwner, combatant) && !validTargets.Contains(combatant))
                                validTargets.Add(combatant);
                        }
                        break;
                        
                    case TargetType.AllAllies:
                        // All allies are valid targets
                        foreach (ICombatant combatant in allCombatants)
                        {
                            if (!IsEnemy(cardOwner, combatant) && !validTargets.Contains(combatant))
                                validTargets.Add(combatant);
                        }
                        break;
                }
            }
        }
        
        private bool IsEnemy(ICombatant source, ICombatant target)
        {
            if (source == target) return false;
            
            // If one is a player and one is a pet, check ownership
            if (source is CombatPlayer sourcePlayer && target is CombatPet targetPet)
            {
                return targetPet != sourcePlayer.NetworkPlayer.CombatPet;
            }
            else if (source is CombatPet sourcePet && target is CombatPlayer targetPlayer)
            {
                return sourcePet != targetPlayer.NetworkPlayer.CombatPet;
            }
            
            // If both same type, they're enemies
            return true;
        }
        
        private void HighlightValidTargets(bool highlight)
        {
            // Add visual highlight to all valid targets
            foreach (ICombatant target in validTargets)
            {
                if (target is MonoBehaviour targetMono)
                {
                    SpriteRenderer renderer = targetMono.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        if (highlight)
                        {
                            renderer.color = new Color(1f, 1f, 0.8f); // Slight yellow tint
                        }
                        else
                        {
                            renderer.color = Color.white; // Reset color
                        }
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
            
            currentHoveredTarget = newTarget;
            
            // Create new highlight if target is valid
            if (newTarget != null && validTargets.Contains(newTarget) && targetHighlightPrefab != null)
            {
                if (newTarget is MonoBehaviour targetMono)
                {
                    currentTargetHighlight = Instantiate(targetHighlightPrefab, targetMono.transform.position, Quaternion.identity);
                    currentTargetHighlight.transform.SetParent(targetMono.transform);
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
        
        [ServerRpc]
        private void CmdApplyCardEffect(NetworkObject sourceObject, NetworkObject targetObject, string cardName)
        {
            // Get combatants from network objects
            ICombatant source = sourceObject.GetComponent<ICombatant>();
            ICombatant target = targetObject.GetComponent<ICombatant>();
            
            if (source == null || target == null)
            {
                Debug.LogError("Invalid source or target for card effect");
                return;
            }
            
            // Find the card data
            CardData cardData = DeckManager.Instance.FindCardByName(cardName);
            if (cardData == null)
            {
                Debug.LogError($"Cannot find card data for {cardName}");
                return;
            }
            
            // Apply card effects
            CardEffectProcessor.ApplyCardEffects(cardData, source, target);
            
            // If source is a CombatPlayer, spend energy
            if (source is CombatPlayer player)
            {
                player.SyncEnergy.Value -= cardData.manaCost;
            }
            
            // Notify the card was played
            CombatManager.Instance.NotifyCardPlayed(cardName, source, target);
        }
    }
} 