using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AutoTestRunner : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Enable this flag to run the automated test sequence.")]
    public bool enableAutoTesting = false;

    [Header("Time Delays (Seconds)")]
    [Tooltip("Time to wait before clicking the start screen button.")]
    public float delayBeforeStartScreenClick = 2f;
    [Tooltip("Time to wait after the lobby canvas is active before clicking the lobby start game button.")]
    public float delayBeforeLobbyClick = 5f;

    [Header("Button References")]
    [Tooltip("Drag the 'Start Button' GameObject from the initial start screen here.")]
    public Button startScreenStartButton; 

    [Tooltip("Drag the 'Start Game Button' GameObject from the Lobby UI here.")]
    public Button lobbyStartGameButton; 

    [Header("Canvas References")]
    [Tooltip("Drag the main Lobby Canvas GameObject here. The script will wait for this to be active.")]
    public GameObject lobbyCanvasGameObject; 

    void Start()
    {
        if (enableAutoTesting)
        {
            Debug.Log("AutoTestRunner: Automated testing enabled. Starting sequence...");
            StartCoroutine(AutoTestSequence());
        }
        else
        {
            Debug.Log("AutoTestRunner: Automated testing disabled.");
        }
    }

    private IEnumerator AutoTestSequence()
    {
        // --- Step 1: Click Start Screen Button ---
        Debug.Log($"AutoTestRunner: Waiting {delayBeforeStartScreenClick} seconds to click Start Screen Button.");
        yield return new WaitForSeconds(delayBeforeStartScreenClick);

        if (startScreenStartButton != null)
        {
            Debug.Log($"AutoTestRunner: Found Start Screen Button reference, invoking onClick.");
            startScreenStartButton.onClick.Invoke();
        }
        else
        {
            Debug.LogError($"AutoTestRunner: 'Start Screen Start Button' reference is not set in the Inspector.");
        }

        // --- Step 2: Wait for Lobby Canvas GameObject and Click Start Game Button ---
        Debug.Log("AutoTestRunner: Waiting for Lobby Canvas GameObject to be assigned and active...");
        yield return new WaitUntil(() =>
        {
            if (lobbyCanvasGameObject != null && lobbyCanvasGameObject.activeInHierarchy)
            {
                Debug.Log("AutoTestRunner: Lobby Canvas GameObject is assigned and active.");
                return true;
            }
            if (lobbyCanvasGameObject == null)
            {
                Debug.LogWarning("AutoTestRunner: Waiting for Lobby Canvas GameObject to be assigned in the Inspector...");
            }
            return false;
        });
        
        Debug.Log($"AutoTestRunner: Waiting {delayBeforeLobbyClick} seconds to click Lobby Start Game Button.");
        yield return new WaitForSeconds(delayBeforeLobbyClick);

        if (lobbyStartGameButton != null)
        {
            Debug.Log($"AutoTestRunner: Found Lobby Start Game Button reference, invoking onClick.");
            lobbyStartGameButton.onClick.Invoke();
        }
        else
        {
            Debug.LogError($"AutoTestRunner: 'Lobby Start Game Button' reference is not set in the Inspector.");
        }

        Debug.Log("AutoTestRunner: Automated test sequence finished.");
    }
} 