using UnityEngine;
using UnityEngine.EventSystems;

public class DraftDeckButtonHandler : MonoBehaviour, IPointerClickHandler
{
    // We could potentially cache this, but finding it on click ensures it works even if managers are initialized later.
    private GameStateManager gameStateManager; 

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("DraftDeckButtonHandler: OnPointerClick triggered!");

        // Find the GameStateManager instance
        if (gameStateManager == null)
        {
            // Access GameManager via its static Instance property
            if (GameManager.Instance != null)
            {
                Debug.Log("DraftDeckButtonHandler: GameManager.Instance is NOT null.");
                gameStateManager = GameManager.Instance.GetGameStateManager();
                if (gameStateManager == null)
                {
                    Debug.LogError("DraftDeckButtonHandler: GameManager.Instance was found, but GetGameStateManager() returned null!");
                }
            }
            else
            {
                Debug.LogError("DraftDeckButtonHandler: GameManager.Instance IS null!");
            }
        }

        // Call the method if found
        if (gameStateManager != null)
        {
            Debug.Log("DraftDeckButtonHandler: Found GameStateManager, calling ShowDraftPlayerDeck...");
            gameStateManager.ShowDraftPlayerDeck(); 
        }
        else
        {
            Debug.LogError("DraftDeckButtonHandler: Could not find GameStateManager instance!");
        }
    }
} 