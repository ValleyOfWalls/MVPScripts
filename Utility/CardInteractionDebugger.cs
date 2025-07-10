using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Utility script to diagnose and fix card interaction issues.
/// Add this to any GameObject in the scene to automatically fix card clicking problems.
/// </summary>
public class CardInteractionDebugger : MonoBehaviour
{
    [Header("Auto-Fix Options")]
    [SerializeField] private bool runDiagnosticOnStart = true;
    [SerializeField] private bool fixIssuesAutomatically = true;
    
    private void Start()
    {
        if (runDiagnosticOnStart)
        {
            DiagnoseSceneInteraction();
            
            if (fixIssuesAutomatically)
            {
                FixSceneInteractionIssues();
            }
        }
    }
    
    [ContextMenu("Diagnose Scene Interaction")]
    public void DiagnoseSceneInteraction()
    {
        Debug.Log("=== CARD INTERACTION DIAGNOSTIC ===");
        
        // Check EventSystem
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        Debug.Log($"EventSystem present: {eventSystem != null}");
        
        // Check for Canvas with GraphicRaycaster
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Debug.Log($"Total Canvases found: {canvases.Length}");
        
        foreach (Canvas canvas in canvases)
        {
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            Debug.Log($"Canvas: {canvas.gameObject.name} - GraphicRaycaster: {raycaster != null}");
        }
        
        // Check cards
        Card[] allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);
        Debug.Log($"Total Cards found: {allCards.Length}");
        
        int cardsWithDragDrop = 0;
        int cardsWithRaycastTarget = 0;
        
        foreach (Card card in allCards)
        {
            CardDragDrop dragDrop = card.GetComponent<CardDragDrop>();
            if (dragDrop != null) cardsWithDragDrop++;
            
            Image cardImage = card.GetComponent<Image>();
            if (cardImage == null && card.GetCardImage() != null)
                cardImage = card.GetCardImage();
            
            if (cardImage != null && cardImage.raycastTarget)
                cardsWithRaycastTarget++;
        }
        
        Debug.Log($"Cards with CardDragDrop: {cardsWithDragDrop}");
        Debug.Log($"Cards with raycastTarget enabled: {cardsWithRaycastTarget}");
        Debug.Log("=== END DIAGNOSTIC ===");
    }
    
    [ContextMenu("Fix Scene Interaction Issues")]
    public void FixSceneInteractionIssues()
    {
        Debug.Log("=== FIXING CARD INTERACTION ISSUES ===");
        
        // Ensure EventSystem exists
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            Debug.Log("Creating EventSystem...");
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }
        
        // Ensure Canvas has GraphicRaycaster
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log($"Added GraphicRaycaster to: {canvas.gameObject.name}");
            }
        }
        
        // Fix all cards
        Card[] allCards = FindObjectsByType<Card>(FindObjectsSortMode.None);
        int fixedCards = 0;
        
        foreach (Card card in allCards)
        {
            bool cardFixed = false;
            
            // Ensure Image component with raycastTarget
            Image cardImage = card.GetComponent<Image>();
            if (cardImage == null && card.GetCardImage() != null)
                cardImage = card.GetCardImage();
            
            if (cardImage == null)
            {
                cardImage = card.gameObject.AddComponent<Image>();
                cardImage.color = new Color(1, 1, 1, 0); // Transparent
                cardFixed = true;
            }
            
            if (!cardImage.raycastTarget)
            {
                cardImage.raycastTarget = true;
                cardFixed = true;
            }
            
            // Ensure CanvasGroup is properly configured
            CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = card.gameObject.AddComponent<CanvasGroup>();
                cardFixed = true;
            }
            
            if (!canvasGroup.interactable || !canvasGroup.blocksRaycasts)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                cardFixed = true;
            }
            
            if (cardFixed) fixedCards++;
        }
        
        Debug.Log($"Fixed {fixedCards} cards for interaction");
        Debug.Log("=== FIXES COMPLETED ===");
    }
} 