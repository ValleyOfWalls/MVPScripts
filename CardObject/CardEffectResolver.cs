using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;

/// <summary>
/// Resolves the effects of a card when played, coordinating between various subsystems.
/// Attach to: Card prefabs alongside Card, HandleCardPlay, and SourceAndTargetIdentifier.
/// </summary>
public class CardEffectResolver : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Card card;
    [SerializeField] private HandleCardPlay handleCardPlay;
    [SerializeField] private SourceAndTargetIdentifier sourceAndTargetIdentifier;
    
    // References to the services we need
    private DamageCalculator damageCalculator;
    
    private void Awake()
    {
        // Get required component references if not assigned
        if (card == null) card = GetComponent<Card>();
        if (handleCardPlay == null) handleCardPlay = GetComponent<HandleCardPlay>();
        if (sourceAndTargetIdentifier == null) sourceAndTargetIdentifier = GetComponent<SourceAndTargetIdentifier>();
        
        // Validate components
        ValidateComponents();
    }
    
    private void Start()
    {
        // Find the DamageCalculator (now on CombatManager)
        if (damageCalculator == null)
        {
            CombatManager combatManager = FindFirstObjectByType<CombatManager>();
            if (combatManager != null)
            {
                damageCalculator = combatManager.GetComponent<DamageCalculator>();
                if (damageCalculator == null)
                {
                    Debug.LogError($"CardEffectResolver on {gameObject.name}: Could not find DamageCalculator on CombatManager!");
                }
            }
            else
            {
                Debug.LogError($"CardEffectResolver on {gameObject.name}: Could not find CombatManager!");
            }
        }
    }
    
    private void ValidateComponents()
    {
        if (card == null)
            Debug.LogError($"CardEffectResolver on {gameObject.name}: Missing Card component!");
        
        if (handleCardPlay == null)
            Debug.LogError($"CardEffectResolver on {gameObject.name}: Missing HandleCardPlay component!");
        
        if (sourceAndTargetIdentifier == null)
            Debug.LogError($"CardEffectResolver on {gameObject.name}: Missing SourceAndTargetIdentifier component!");
    }
    
    /// <summary>
    /// Called by HandleCardPlay when the card is being played.
    /// </summary>
    public void ResolveCardEffect()
    {
        if (!IsOwner)
        {
            Debug.LogWarning($"CardEffectResolver: Cannot resolve effect, not network owner of card {gameObject.name}");
            return;
        }
        
        // Make sure we have source and target entities
        var sourceEntity = sourceAndTargetIdentifier.SourceEntity;
        var targetEntity = sourceAndTargetIdentifier.TargetEntity;
        
        if (sourceEntity == null || targetEntity == null)
        {
            Debug.LogError($"CardEffectResolver: Missing source or target entity for card {gameObject.name}");
            return;
        }
        
        // Get the card data
        CardData cardData = card.CardData;
        if (cardData == null)
        {
            Debug.LogError($"CardEffectResolver: No card data for card {gameObject.name}");
            return;
        }
        
        // Execute the card effect on the server
        CmdResolveEffect(sourceEntity.ObjectId, targetEntity.ObjectId, cardData.CardId);
    }
    
    [ServerRpc]
    private void CmdResolveEffect(int sourceEntityId, int targetEntityId, int cardDataId)
    {
        Debug.Log($"CardEffectResolver: Server resolving effect for card {cardDataId} from entity {sourceEntityId} to entity {targetEntityId}");
        
        // Find the entities by their network object IDs
        NetworkEntity sourceEntity = FindEntityById(sourceEntityId);
        NetworkEntity targetEntity = FindEntityById(targetEntityId);
        
        if (sourceEntity == null || targetEntity == null)
        {
            Debug.LogError($"CardEffectResolver: Could not find source or target entity with IDs {sourceEntityId}/{targetEntityId}");
            return;
        }
        
        // Get the card data from the database
        CardData cardData = CardDatabase.Instance.GetCardById(cardDataId);
        if (cardData == null)
        {
            Debug.LogError($"CardEffectResolver: Could not find card data for ID {cardDataId}");
            return;
        }
        
        // Process the effect based on the card type
        ProcessCardEffect(sourceEntity, targetEntity, cardData);
    }
    
    private void ProcessCardEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardData cardData)
    {
        // Process by effect type
        switch (cardData.EffectType)
        {
            case CardEffectType.Damage:
                ProcessDamageEffect(sourceEntity, targetEntity, cardData);
                break;
                
            case CardEffectType.Heal:
                ProcessHealEffect(sourceEntity, targetEntity, cardData);
                break;
                
            case CardEffectType.DrawCard:
                ProcessDrawCardEffect(sourceEntity, cardData);
                break;
                
            case CardEffectType.BuffStats:
                ProcessBuffEffect(sourceEntity, targetEntity, cardData, isDebuff: false);
                break;
                
            case CardEffectType.DebuffStats:
                ProcessBuffEffect(sourceEntity, targetEntity, cardData, isDebuff: true);
                break;
                
            case CardEffectType.ApplyStatus:
                ProcessStatusEffect(sourceEntity, targetEntity, cardData);
                break;
                
            default:
                Debug.LogWarning($"CardEffectResolver: Unhandled effect type {cardData.EffectType} for card {cardData.CardName}");
                break;
        }
    }
    
    private void ProcessDamageEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardData cardData)
    {
        // Get damage calculator from CombatManager
        if (damageCalculator == null)
        {
            CombatManager combatManager = FindFirstObjectByType<CombatManager>();
            damageCalculator = combatManager?.GetComponent<DamageCalculator>();
            
            if (damageCalculator == null)
            {
                Debug.LogError("CardEffectResolver: Could not find DamageCalculator on CombatManager!");
                return;
            }
        }
        
        // Calculate damage based on card and status effects
        int finalDamage = damageCalculator.CalculateDamage(sourceEntity, targetEntity, cardData);
        
        // Apply the damage through the life handler
        LifeHandler targetLifeHandler = targetEntity.GetComponent<LifeHandler>();
        if (targetLifeHandler != null)
        {
            targetLifeHandler.TakeDamage(finalDamage, sourceEntity);
        }
        else
        {
            Debug.LogError($"CardEffectResolver: Target entity {targetEntity.EntityName.Value} has no LifeHandler!");
        }
    }
    
    private void ProcessHealEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardData cardData)
    {
        // Implement heal logic here, similar to damage but using LifeHandler.Heal
        LifeHandler targetLifeHandler = targetEntity.GetComponent<LifeHandler>();
        if (targetLifeHandler != null)
        {
            int healAmount = cardData.Amount;
            // Consider healing modifiers from effects if needed
            targetLifeHandler.Heal(healAmount, sourceEntity);
        }
    }
    
    private void ProcessDrawCardEffect(NetworkEntity sourceEntity, CardData cardData)
    {
        // Implement card draw logic here
        HandManager handManager = sourceEntity.GetComponent<HandManager>();
        if (handManager != null)
        {
            handManager.DrawCards(); // Call with no parameters
            
            // Alternatively, it might have a different method name for drawing multiple cards
            // handManager.DrawMultipleCards(cardData.Amount);
        }
    }
    
    private void ProcessBuffEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardData cardData, bool isDebuff)
    {
        // Apply buff/debuff status effects to the target entity
        EffectHandler targetEffectHandler = targetEntity.GetComponent<EffectHandler>();
        if (targetEffectHandler != null)
        {
            // The exact effect and duration would be specified in the card data
            // For now, assuming a generic "Buff" or "Debuff" effect with the card's amount as potency
            string effectName = isDebuff ? "Debuff" : "Buff";
            int duration = 2; // Default duration in turns
            targetEffectHandler.AddEffect(effectName, cardData.Amount, duration, sourceEntity);
        }
    }
    
    private void ProcessStatusEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardData cardData)
    {
        // Apply status effects to the target entity
        EffectHandler targetEffectHandler = targetEntity.GetComponent<EffectHandler>();
        if (targetEffectHandler != null)
        {
            // For a real implementation, the card data would specify the effect name, potency, and duration
            // For now using generic values
            string effectName = "Status"; // This would come from more detailed card data
            int potency = cardData.Amount;
            int duration = 2; // Default duration in turns
            targetEffectHandler.AddEffect(effectName, potency, duration, sourceEntity);
        }
    }
    
    private NetworkEntity FindEntityById(int entityId)
    {
        NetworkObject netObj = null;
        
        if (IsServerInitialized)
        {
            FishNet.InstanceFinder.ServerManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        else if (IsClientInitialized)
        {
            FishNet.InstanceFinder.ClientManager.Objects.Spawned.TryGetValue(entityId, out netObj);
        }
        
        return netObj?.GetComponent<NetworkEntity>();
    }
    
    /// <summary>
    /// Directly resolves card effects on the server for AI-controlled entities
    /// </summary>
    [Server]
    public void ServerResolveCardEffect(NetworkEntity sourceEntity, NetworkEntity targetEntity, CardData cardData)
    {
        if (!IsServerInitialized)
        {
            Debug.LogError("CardEffectResolver: Cannot resolve effect on server - server not initialized");
            return;
        }
        
        Debug.Log($"CardEffectResolver: ServerResolveCardEffect for card {cardData.CardName} from {sourceEntity.EntityName.Value} to {targetEntity.EntityName.Value}");
        
        // Process the effect directly since we're already on the server
        ProcessCardEffect(sourceEntity, targetEntity, cardData);
    }
} 