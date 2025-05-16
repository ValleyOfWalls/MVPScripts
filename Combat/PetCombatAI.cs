using UnityEngine;
using FishNet.Object;
using System.Collections;

/// <summary>
/// Handles AI behavior for pets during combat
/// Attach to: NetworkEntity prefabs of type Pet
/// </summary>
public class PetCombatAI : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkEntity petEntity;
    [SerializeField] private HandManager handManager;

    // Track turn state
    private bool hasFinishedTurn = false;
    public bool HasFinishedTurn => hasFinishedTurn;

    private void Awake()
    {
        // Get required components
        if (petEntity == null) petEntity = GetComponent<NetworkEntity>();
        if (handManager == null) handManager = GetComponent<HandManager>();

        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (petEntity == null)
            Debug.LogError($"PetCombatAI on {gameObject.name}: Missing NetworkEntity component");
        if (handManager == null)
            Debug.LogError($"PetCombatAI on {gameObject.name}: Missing HandManager component");
    }

    /// <summary>
    /// Executes the pet's turn in combat
    /// </summary>
    [Server]
    public IEnumerator TakeTurn()
    {
        if (!IsServerInitialized) yield break;

        hasFinishedTurn = false;

        // TODO: Implement actual pet AI logic here
        // For now, just wait a short time to simulate thinking
        yield return new WaitForSeconds(1.0f);

        hasFinishedTurn = true;
    }

    /// <summary>
    /// Resets the turn state when a new round begins
    /// </summary>
    [Server]
    public void ResetTurnState()
    {
        if (!IsServerInitialized) return;
        hasFinishedTurn = false;
    }
} 