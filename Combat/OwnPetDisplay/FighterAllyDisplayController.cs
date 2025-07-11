using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using System.Collections;

public enum FighterSide
{
    Left,   // Typically the player
    Right   // Typically the opponent pet
}

/// <summary>
/// Universal controller for fighter ally display functionality.
/// Can display ally UI for both left fighters (players) and right fighters (pets).
/// This replaces the OwnPetViewController with a more flexible system.
/// Attach to: Ally display UI prefabs in the combat canvas
/// </summary>
public class FighterAllyDisplayController : MonoBehaviour
{
    [Header("Fighter Configuration")]
    [SerializeField] private FighterSide fighterSide = FighterSide.Left;
    [SerializeField] private bool autoDetectFighter = true;
    
    [Header("Component References")]
    [SerializeField] private OwnPetVisualDisplay visualDisplay;
    [SerializeField] private OwnPetStatsDisplay statsDisplay;
    [SerializeField] private OwnPetStatusEffectsDisplay statusEffectsDisplay;
    [SerializeField] private OwnPetCardDropZone cardDropZone;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogEnabled = true;
    
    // Current entities being displayed
    private NetworkEntity currentDisplayedFighter;
    private NetworkEntity currentDisplayedAlly;
    
    // Managers
    private FightManager fightManager;
    private EntityVisibilityManager entityVisibilityManager;
    
    private void Awake()
    {
        Debug.Log($"ALLY_SETUP: FighterAllyDisplayController.Awake() called for {fighterSide} side");
        
        ValidateComponents();
    }
    
    private void Start()
    {
        FindManagers();
        SubscribeToEvents();
        UpdateDisplayedEntities();
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    private void ValidateComponents()
    {
        Debug.Log($"ALLY_SETUP: {fighterSide} side - Validating components:");
        Debug.Log($"ALLY_SETUP: {fighterSide} side - VisualDisplay: {(visualDisplay != null ? "FOUND" : "MISSING")}");
        Debug.Log($"ALLY_SETUP: {fighterSide} side - StatsDisplay: {(statsDisplay != null ? "FOUND" : "MISSING")}");
        Debug.Log($"ALLY_SETUP: {fighterSide} side - StatusEffectsDisplay: {(statusEffectsDisplay != null ? "FOUND" : "MISSING")}");
        Debug.Log($"ALLY_SETUP: {fighterSide} side - CardDropZone: {(cardDropZone != null ? "FOUND" : "MISSING")}");
        
        if (visualDisplay == null)
            Debug.LogWarning($"ALLY_SETUP: {fighterSide} side: VisualDisplay component not assigned");
        
        if (statsDisplay == null)
            Debug.LogWarning($"ALLY_SETUP: {fighterSide} side: StatsDisplay component not assigned");
        
        if (statusEffectsDisplay == null)
            Debug.LogWarning($"ALLY_SETUP: {fighterSide} side: StatusEffectsDisplay component not assigned");
        
        if (cardDropZone == null)
            Debug.LogWarning($"ALLY_SETUP: {fighterSide} side: CardDropZone component not assigned");
    }
    
    private void FindManagers()
    {
        Debug.Log($"ALLY_SETUP: {fighterSide} side - Finding managers...");
        
        if (fightManager == null)
            fightManager = FightManager.Instance;
        
        if (entityVisibilityManager == null)
            entityVisibilityManager = EntityVisibilityManager.Instance;
        
        Debug.Log($"ALLY_SETUP: {fighterSide} side - FightManager: {(fightManager != null ? "FOUND" : "NOT FOUND")}");
        Debug.Log($"ALLY_SETUP: {fighterSide} side - EntityVisibilityManager: {(entityVisibilityManager != null ? "FOUND" : "NOT FOUND")}");
        
        if (fightManager == null)
            Debug.LogWarning($"ALLY_SETUP: {fighterSide} side: FightManager not found");
    }
    
    private void SubscribeToEvents()
    {
        if (fightManager != null)
        {
            fightManager.OnFightsChanged += OnFightsChanged;
        }
    }
    
    private void UnsubscribeFromEvents()
    {
        if (fightManager != null)
        {
            fightManager.OnFightsChanged -= OnFightsChanged;
        }
    }
    
    private void OnFightsChanged(bool hasFights)
    {
        Debug.Log($"ALLY_SETUP: {fighterSide} side - Fights changed, hasFights: {hasFights}");
        
        UpdateDisplayedEntities();
    }
    
    /// <summary>
    /// Updates which fighter and ally are currently being displayed
    /// </summary>
    public void UpdateDisplayedEntities()
    {
        Debug.Log($"ALLY_SETUP: {fighterSide} side - UpdateDisplayedEntities() called");
        
        // Try to find managers if not available
        if (fightManager == null)
        {
            Debug.Log($"ALLY_SETUP: {fighterSide} side - FightManager is null, trying to find it...");
            FindManagers();
        }
        
        if (fightManager == null)
        {
            Debug.Log($"ALLY_SETUP: {fighterSide} side - FightManager not available, will retry in a moment");
            
            StartCoroutine(RetryFindingFightManager());
            return;
        }
        
        // Get the appropriate fighter and their ally based on side
        NetworkEntity fighter = null;
        NetworkEntity ally = null;
        
        if (fighterSide == FighterSide.Left)
        {
            fighter = fightManager.ViewedLeftFighter;
            Debug.Log($"ALLY_SETUP: {fighterSide} side - ViewedLeftFighter: {(fighter?.EntityName.Value ?? "null")} (ID: {fighter?.ObjectId ?? 0})");
            if (fighter != null)
            {
                ally = fightManager.GetAlly(fighter);
                Debug.Log($"ALLY_SETUP: {fighterSide} side - GetAlly() returned: {(ally?.EntityName.Value ?? "null")} (ID: {ally?.ObjectId ?? 0})");
            }
        }
        else // FighterSide.Right
        {
            fighter = fightManager.ViewedRightFighter;
            Debug.Log($"ALLY_SETUP: {fighterSide} side - ViewedRightFighter: {(fighter?.EntityName.Value ?? "null")} (ID: {fighter?.ObjectId ?? 0})");
            if (fighter != null)
            {
                ally = fightManager.GetAlly(fighter);
                Debug.Log($"ALLY_SETUP: {fighterSide} side - GetAlly() returned: {(ally?.EntityName.Value ?? "null")} (ID: {ally?.ObjectId ?? 0})");
            }
        }
        
        Debug.Log($"ALLY_SETUP: {fighterSide} side - Final result - Fighter: {(fighter?.EntityName.Value ?? "null")}, Ally: {(ally?.EntityName.Value ?? "null")}");
        
        if (ally == null)
        {
            Debug.Log($"ALLY_SETUP: {fighterSide} side - No ally found for fighter, hiding display");
            
            SetDisplayedAlly(null);
            SetViewVisible(false);
            return;
        }
        
        // Update the displayed ally
        Debug.Log($"ALLY_SETUP: {fighterSide} side - Setting displayed ally to: {ally.EntityName.Value}");
        SetDisplayedAlly(ally);
        SetViewVisible(true);
    }
    
    private IEnumerator RetryFindingFightManager()
    {
        int retryCount = 0;
        int maxRetries = 10;
        
        while (retryCount < maxRetries)
        {
            yield return new WaitForSeconds(0.3f);
            
            if (fightManager != null)
            {
                if (debugLogEnabled)
                    Debug.Log($"[ALLY_DISPLAY] {fighterSide} side: Found FightManager on retry {retryCount}");
                
                UpdateDisplayedEntities();
                yield break;
            }
            
            fightManager = FightManager.Instance;
            retryCount++;
        }
        
        if (debugLogEnabled)
            Debug.Log($"[ALLY_DISPLAY] {fighterSide} side: FightManager not found after {maxRetries} retries");
        
        SetViewVisible(false);
    }
    
    /// <summary>
    /// Sets the ally to be displayed and updates all components
    /// </summary>
    private void SetDisplayedAlly(NetworkEntity ally)
    {
        Debug.Log($"ALLY_SETUP: {fighterSide} side - SetDisplayedAlly() called with: {(ally != null ? ally.EntityName.Value + " (ID: " + ally.ObjectId + ")" : "null")}");
        
        // Update current ally reference
        currentDisplayedAlly = ally;
        
        if (ally != null)
        {
            // Update all display components
            if (visualDisplay != null)
            {
                visualDisplay.SetPet(ally);
                Debug.Log($"ALLY_SETUP: {fighterSide} side - visualDisplay.SetPet() called for {ally.EntityName.Value}");
            }
            else
            {
                Debug.LogWarning($"ALLY_SETUP: {fighterSide} side - visualDisplay is null, cannot set pet");
            }
            
            if (statsDisplay != null)
            {
                statsDisplay.SetPet(ally);
                Debug.Log($"ALLY_SETUP: {fighterSide} side - statsDisplay.SetPet() called for {ally.EntityName.Value}");
            }
            else
            {
                Debug.LogWarning($"ALLY_SETUP: {fighterSide} side - statsDisplay is null, cannot set pet");
            }
            
            if (statusEffectsDisplay != null)
            {
                statusEffectsDisplay.SetPet(ally);
                Debug.Log($"ALLY_SETUP: {fighterSide} side - statusEffectsDisplay.SetPet() called for {ally.EntityName.Value}");
            }
            else
            {
                Debug.LogWarning($"ALLY_SETUP: {fighterSide} side - statusEffectsDisplay is null, cannot set pet");
            }
            
            if (cardDropZone != null)
            {
                cardDropZone.SetTargetPet(ally);
                Debug.Log($"ALLY_SETUP: {fighterSide} side - cardDropZone.SetTargetPet() called for {ally.EntityName.Value}");
            }
            else
            {
                Debug.LogWarning($"ALLY_SETUP: {fighterSide} side - cardDropZone is null, cannot set target pet");
            }
        }
        else
        {
            Debug.Log($"ALLY_SETUP: {fighterSide} side - Clearing all display components (ally is null)");
            // Clear all display components
            if (visualDisplay != null) visualDisplay.SetPet(null);
            if (statsDisplay != null) statsDisplay.SetPet(null);
            if (statusEffectsDisplay != null) statusEffectsDisplay.SetPet(null);
            if (cardDropZone != null) cardDropZone.SetTargetPet(null);
        }
    }
    
    /// <summary>
    /// Shows or hides the entire ally display
    /// </summary>
    public void SetViewVisible(bool visible)
    {
        Debug.Log($"ALLY_SETUP: {fighterSide} side - SetViewVisible({visible}) called");
        Debug.Log($"ALLY_SETUP: {fighterSide} side - GameObject: {gameObject.name}, currently active: {gameObject.activeSelf}");
        Debug.Log($"ALLY_SETUP: {fighterSide} side - Parent: {(transform.parent != null ? transform.parent.name : "null")}, parent active: {(transform.parent != null ? transform.parent.gameObject.activeSelf : false)}");
        
        gameObject.SetActive(visible);
        
        Debug.Log($"ALLY_SETUP: {fighterSide} side - Ally display visibility set to {visible}, now active: {gameObject.activeSelf}");
        Debug.Log($"ALLY_SETUP: {fighterSide} side - activeInHierarchy: {gameObject.activeInHierarchy}");
    }
    
    /// <summary>
    /// Gets the currently displayed ally
    /// </summary>
    public NetworkEntity GetCurrentDisplayedAlly()
    {
        return currentDisplayedAlly;
    }
    
    /// <summary>
    /// Gets the currently displayed fighter
    /// </summary>
    public NetworkEntity GetCurrentDisplayedFighter()
    {
        return currentDisplayedFighter;
    }
    
    /// <summary>
    /// Forces an update of the display (for external callers)
    /// </summary>
    public void ForceUpdate()
    {
        UpdateDisplayedEntities();
    }
    
    /// <summary>
    /// Configures which side this controller represents
    /// </summary>
    public void SetFighterSide(FighterSide side)
    {
        fighterSide = side;
        Debug.Log($"ALLY_SETUP: FighterSide set to {side}");
        
        // Check visibility status after a delay to see if something is disabling us
        StartCoroutine(CheckVisibilityAfterDelay());
    }
    
    private IEnumerator CheckVisibilityAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        Debug.Log($"ALLY_SETUP: {fighterSide} side - DELAYED CHECK - GameObject active: {gameObject.activeSelf}, activeInHierarchy: {gameObject.activeInHierarchy}");
        Debug.Log($"ALLY_SETUP: {fighterSide} side - DELAYED CHECK - Parent active: {(transform.parent != null ? transform.parent.gameObject.activeSelf : false)}");
        
        yield return new WaitForSeconds(2f);
        Debug.Log($"ALLY_SETUP: {fighterSide} side - DELAYED CHECK 2 - GameObject active: {gameObject.activeSelf}, activeInHierarchy: {gameObject.activeInHierarchy}");
    }
} 