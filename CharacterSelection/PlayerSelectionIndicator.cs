using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Manages visual indicators showing which players have selected a specific character or pet.
/// Displays colored borders for multiple player selections in Mario Kart style.
/// </summary>
public class PlayerSelectionIndicator : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float borderWidth = 4f;
    [SerializeField] private float animationDuration = 0.3f;
    
    // Player selection tracking
    private Dictionary<string, Color> selectedPlayers = new Dictionary<string, Color>();
    private List<GameObject> borderObjects = new List<GameObject>();
    private bool isInitialized = false;
    
    public void Initialize()
    {
        if (isInitialized) return;
        
        // Ensure we have a RectTransform
        if (GetComponent<RectTransform>() == null)
        {
            Debug.LogWarning("PlayerSelectionIndicator: Missing RectTransform component");
            return;
        }
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Add a player selection with their unique color
    /// </summary>
    public void AddPlayerSelection(string playerID, Color playerColor)
    {
        if (!isInitialized) Initialize();
        
        if (selectedPlayers.ContainsKey(playerID))
        {
            // Update existing selection color
            selectedPlayers[playerID] = playerColor;
        }
        else
        {
            // Add new selection
            selectedPlayers.Add(playerID, playerColor);
        }
        
        UpdateVisualIndicators();
    }
    
    /// <summary>
    /// Remove a specific player's selection
    /// </summary>
    public void RemovePlayerSelection(string playerID)
    {
        if (selectedPlayers.ContainsKey(playerID))
        {
            selectedPlayers.Remove(playerID);
            UpdateVisualIndicators();
        }
    }
    
    /// <summary>
    /// Clear all player selections except the specified player (used for updates)
    /// </summary>
    public void ClearAllExcept(string preservePlayerID)
    {
        Color preservedColor = Color.white;
        bool hasPreservedPlayer = selectedPlayers.ContainsKey(preservePlayerID);
        
        if (hasPreservedPlayer)
        {
            preservedColor = selectedPlayers[preservePlayerID];
        }
        
        selectedPlayers.Clear();
        
        if (hasPreservedPlayer)
        {
            selectedPlayers.Add(preservePlayerID, preservedColor);
        }
        
        UpdateVisualIndicators();
    }
    
    /// <summary>
    /// Clear all player selections
    /// </summary>
    public void ClearAll()
    {
        selectedPlayers.Clear();
        UpdateVisualIndicators();
    }
    
    private void UpdateVisualIndicators()
    {
        // Clear existing border objects
        ClearBorderObjects();
        
        if (selectedPlayers.Count == 0) return;
        
        // Create borders for each selected player
        int playerIndex = 0;
        foreach (var kvp in selectedPlayers)
        {
            CreatePlayerBorder(kvp.Value, playerIndex);
            playerIndex++;
        }
    }
    
    private void ClearBorderObjects()
    {
        foreach (GameObject borderObj in borderObjects)
        {
            if (borderObj != null)
            {
                DestroyImmediate(borderObj);
            }
        }
        borderObjects.Clear();
    }
    
    private void CreatePlayerBorder(Color playerColor, int playerIndex)
    {
        RectTransform parentRect = GetComponent<RectTransform>();
        if (parentRect == null) return;
        
        // Create border object
        GameObject borderObject = new GameObject($"PlayerBorder_{playerIndex}");
        borderObject.transform.SetParent(transform, false);
        
        RectTransform borderRect = borderObject.AddComponent<RectTransform>();
        
        // Position border with slight offset for multiple players
        float offset = playerIndex * 2f; // Small offset for stacking
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
        borderRect.anchoredPosition = new Vector2(offset, -offset);
        
        // Add Image component for the border
        Image borderImage = borderObject.AddComponent<Image>();
        borderImage.color = playerColor;
        
        // Create border effect using UI outline or custom shader
        Outline outline = borderObject.AddComponent<Outline>();
        outline.effectColor = playerColor;
        outline.effectDistance = new Vector2(borderWidth, borderWidth);
        outline.useGraphicAlpha = true;
        
        // Alternative: Create a border using a transparent center
        CreateBorderEffect(borderObject, playerColor);
        
        borderObjects.Add(borderObject);
        
        // Animate in
        AnimateBorderIn(borderObject);
    }
    
    private void CreateBorderEffect(GameObject borderObject, Color borderColor)
    {
        RectTransform borderRect = borderObject.GetComponent<RectTransform>();
        Image borderImage = borderObject.GetComponent<Image>();
        
        // Create a simple colored border by creating 4 separate border pieces
        // Top border
        CreateBorderPiece(borderObject, "TopBorder", borderColor, 
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -borderWidth));
        
        // Bottom border  
        CreateBorderPiece(borderObject, "BottomBorder", borderColor,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, borderWidth));
        
        // Left border
        CreateBorderPiece(borderObject, "LeftBorder", borderColor,
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(borderWidth, 0));
        
        // Right border
        CreateBorderPiece(borderObject, "RightBorder", borderColor,
            new Vector2(1, 0), new Vector2(1, 1), new Vector2(-borderWidth, 0));
        
        // Make main image transparent
        borderImage.color = new Color(borderColor.r, borderColor.g, borderColor.b, 0f);
    }
    
    private void CreateBorderPiece(GameObject parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        GameObject piece = new GameObject(name);
        piece.transform.SetParent(parent.transform, false);
        
        RectTransform pieceRect = piece.AddComponent<RectTransform>();
        pieceRect.anchorMin = anchorMin;
        pieceRect.anchorMax = anchorMax;
        pieceRect.sizeDelta = sizeDelta;
        pieceRect.anchoredPosition = Vector2.zero;
        
        Image pieceImage = piece.AddComponent<Image>();
        pieceImage.color = color;
    }
    
    private void AnimateBorderIn(GameObject borderObject)
    {
        if (borderObject == null) return;
        
        // Simple scale animation
        Vector3 originalScale = borderObject.transform.localScale;
        borderObject.transform.localScale = Vector3.zero;
        
        // Use LeanTween if available, otherwise simple coroutine
        StartCoroutine(AnimateScale(borderObject, originalScale));
    }
    
    private System.Collections.IEnumerator AnimateScale(GameObject target, Vector3 targetScale)
    {
        if (target == null) yield break;
        
        float elapsed = 0f;
        Vector3 startScale = Vector3.zero;
        
        while (elapsed < animationDuration && target != null)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationDuration;
            
            // Ease out animation
            progress = 1f - Mathf.Pow(1f - progress, 3f);
            
            target.transform.localScale = Vector3.Lerp(startScale, targetScale, progress);
            yield return null;
        }
        
        if (target != null)
        {
            target.transform.localScale = targetScale;
        }
    }
    
    private void OnDestroy()
    {
        ClearBorderObjects();
    }
    
    #region Public Properties
    
    /// <summary>
    /// Get the number of players who have selected this item
    /// </summary>
    public int SelectedPlayerCount => selectedPlayers.Count;
    
    /// <summary>
    /// Check if a specific player has selected this item
    /// </summary>
    public bool IsSelectedBy(string playerID)
    {
        return selectedPlayers.ContainsKey(playerID);
    }
    
    /// <summary>
    /// Get all player IDs who have selected this item
    /// </summary>
    public List<string> GetSelectedPlayerIDs()
    {
        return new List<string>(selectedPlayers.Keys);
    }
    
    #endregion
} 