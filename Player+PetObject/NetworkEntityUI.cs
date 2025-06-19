using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Handles the UI visualization for NetworkEntity
/// Attach to: The same GameObject as NetworkEntity
/// </summary>
public class NetworkEntityUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkEntity entity;
    [SerializeField] private NetworkEntityDeck entityDeck;
    [SerializeField] private HandManager handManager;
    [SerializeField] private EffectHandler effectHandler;
    [SerializeField] private EntityTracker entityTracker;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private NetworkEntityAnimator entityAnimator;

    [Header("UI Elements")]
    [SerializeField] private Transform handTransform;
    [SerializeField] private Transform deckTransform;
    [SerializeField] private Transform discardTransform;

    [Header("Entity Visual Representation")]
    [SerializeField] private Image entityImage; // Main visual representation of the entity (legacy 2D)
    
    [Header("3D Model")]
    [SerializeField] private Transform entityModel; // 3D model representation of the entity
    [SerializeField] private Vector3 uiOffset = Vector3.up * 2f; // Offset for UI relative to model

    // Note: Stats UI (health, energy, effects, currency, etc.) are now handled by EntityStatsUIController
    // This NetworkEntityUI now focuses on entity positioning, visibility, and card management only

    private void Awake()
    {
        // Get required components
        if (entity == null) entity = GetComponent<NetworkEntity>();
        if (entityDeck == null) entityDeck = GetComponent<NetworkEntityDeck>();
        if (handManager == null) handManager = GetComponent<HandManager>();
        if (effectHandler == null) effectHandler = GetComponent<EffectHandler>();
        if (entityTracker == null) entityTracker = GetComponent<EntityTracker>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (entityAnimator == null) entityAnimator = GetComponent<NetworkEntityAnimator>();

        // Add CanvasGroup if not present
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Add DropZone component for card drag and drop targeting
        DropZone dropZone = GetComponent<DropZone>();
        if (dropZone == null)
        {
            dropZone = gameObject.AddComponent<DropZone>();
            Debug.Log($"NetworkEntityUI: Added DropZone component to {gameObject.name}");
        }

        ValidateComponents();

        // Default to hidden until game state determines visibility
        SetVisible(false);
    }

    private void ValidateComponents()
    {
        if (entity == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: Missing NetworkEntity component");
            return;
        }

        // Validate components based on entity type
        if (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet)
        {
            // Main entities should have NetworkEntityDeck but not HandManager
            if (entityDeck == null)
                Debug.LogError($"NetworkEntityUI on {gameObject.name}: Missing NetworkEntityDeck component");
            // HandManager should be on the Hand entity, not the main entity
        }
        else if (entity.EntityType == EntityType.PlayerHand || entity.EntityType == EntityType.PetHand)
        {
            // Hand entities should have HandManager but not NetworkEntityDeck
            if (handManager == null)
                Debug.LogError($"NetworkEntityUI on {gameObject.name}: Missing HandManager component");
            // NetworkEntityDeck should be on the main entity, not the hand entity
        }
    }

    private void OnEnable()
    {
        if (entityDeck != null)
        {
            entityDeck.OnDeckChanged += UpdateDeckDisplay;
        }

        if (effectHandler != null)
        {
            // Note: Effect display is now handled by EntityStatsUIController
            // effectHandler.OnEffectsChanged += UpdateEffectsDisplay;
        }

        if (entityTracker != null)
        {
            // Note: Stance change handling is now done by EntityStatsUIController
            // entityTracker.OnStanceChanged += OnStanceChanged;
        }

        // Note: Stats event subscriptions are now handled by EntityStatsUIController
        // NetworkEntityUI only handles visibility and card management
    }

    private void OnDisable()
    {
        if (entityDeck != null)
        {
            entityDeck.OnDeckChanged -= UpdateDeckDisplay;
        }

        if (effectHandler != null)
        {
            // Note: Effect display is now handled by EntityStatsUIController
            // effectHandler.OnEffectsChanged -= UpdateEffectsDisplay;
        }

        if (entityTracker != null)
        {
            // Note: Stance change handling is now done by EntityStatsUIController
            // entityTracker.OnStanceChanged -= OnStanceChanged;
        }

        // Note: Stats event unsubscriptions are now handled by EntityStatsUIController
    }

    private void Start()
    {
        UpdateDeckDisplay();
        UpdateDiscardDisplay();
        // Note: Entity stats UI updates are now handled by EntityStatsUIController
        // PositionUIRelativeToModel(); // Disabled: using prefab hierarchy positioning
    }
    
    private void LateUpdate()
    {
        // Disabled: UI positioning is handled by prefab hierarchy
        // If you need dynamic positioning, re-enable PositionUIRelativeToModel()
        // PositionUIRelativeToModel();
    }
    
    /// <summary>
    /// Positions the UI canvas relative to the 3D model
    /// </summary>
    private void PositionUIRelativeToModel()
    {
        if (entityModel == null || canvasGroup == null)
        {
            return;
        }
        
        // Check if this is a RectTransform to avoid positioning conflicts
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            return;
        }
        
        Vector3 newPosition = entityModel.position + uiOffset;
        
        // Only update if position actually changed significantly
        if (Vector3.Distance(transform.position, newPosition) > 0.001f)
        {
            transform.position = newPosition;
        }
    }

    public void SetVisible(bool visible)
    {
        // Control UI visibility
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1.0f : 0.0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
        
        // Control 3D model visibility
        if (entityModel != null)
        {
            entityModel.gameObject.SetActive(visible);
        }
        
        // Trigger animation when entity becomes visible
        if (visible && entityAnimator != null)
        {
            entityAnimator.OnEntityBecameVisible();
        }
    }

    // Note: All stat UI methods (UpdateEntityUI, UpdateHealthUI, UpdateEnergyUI, etc.) 
    // have been moved to EntityStatsUIController

    private void UpdateDeckDisplay()
    {
        // Note: Deck/discard counts are now displayed by EntityStatsUIController
        // NetworkEntityUI only manages card positioning, not display counts
    }

    private void UpdateDiscardDisplay()
    {
        // Note: Deck/discard counts are now displayed by EntityStatsUIController  
        // NetworkEntityUI only manages card positioning, not display counts
    }

    // Note: UpdateCurrencyDisplay, UpdateEffectsDisplay, and OnStanceChanged methods
    // have been moved to EntityStatsUIController

    // Public getters for transforms
    public Transform GetHandTransform() 
    {
        // Only Hand entities should have hand transforms
        if (entity != null && (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet))
        {
            // Main entities don't have hand transforms - they're on the Hand entity
            Debug.LogWarning($"NetworkEntityUI on {gameObject.name}: Main entity {entity.EntityType} should not have hand transform. Use RelationshipManager to find Hand entity.");
            return null;
        }

        if (handTransform == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: handTransform is null");
            return null;
        }
        return handTransform;
    }

    public Transform GetDeckTransform() 
    {
        // Only Hand entities should have deck transforms
        if (entity != null && (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet))
        {
            // Main entities don't have deck transforms - they're on the Hand entity
            Debug.LogWarning($"NetworkEntityUI on {gameObject.name}: Main entity {entity.EntityType} should not have deck transform. Use RelationshipManager to find Hand entity.");
            return null;
        }

        if (deckTransform == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: deckTransform is null");
            return null;
        }
        return deckTransform;
    }

    public Transform GetDiscardTransform() 
    {
        // Only Hand entities should have discard transforms
        if (entity != null && (entity.EntityType == EntityType.Player || entity.EntityType == EntityType.Pet))
        {
            // Main entities don't have discard transforms - they're on the Hand entity
            Debug.LogWarning($"NetworkEntityUI on {gameObject.name}: Main entity {entity.EntityType} should not have discard transform. Use RelationshipManager to find Hand entity.");
            return null;
        }

        if (discardTransform == null)
        {
            Debug.LogError($"NetworkEntityUI on {gameObject.name}: discardTransform is null");
            return null;
        }
        return discardTransform;
    }

    // Note: ShowDamagePreview and HideDamagePreview methods have been moved to EntityStatsUIController

    /// <summary>
    /// Gets the main visual image for this entity (for UI positioning purposes)
    /// </summary>
    public Image GetEntityImage()
    {
        return entityImage;
    }
    
    /// <summary>
    /// Gets the 3D model transform for this entity
    /// </summary>
    public Transform GetEntityModel()
    {
        return entityModel;
    }
    
    /// <summary>
    /// Sets the 3D model for this entity
    /// </summary>
    public void SetEntityModel(Transform model)
    {
        entityModel = model;
        PositionUIRelativeToModel();
    }
} 