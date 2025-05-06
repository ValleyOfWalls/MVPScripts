using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using FishNet.Managing;

public class NetworkPet : NetworkBehaviour
{
    // Link to the owner (could be a NetworkPlayer instance or the connection)
    // If a pet is always tied to a specific NetworkPlayer, you might sync the NetworkPlayer's ObjectId
    public readonly SyncVar<uint> OwnerPlayerObjectId = new SyncVar<uint>();

    public readonly SyncVar<string> PetName = new SyncVar<string>();

    public readonly SyncVar<int> MaxHealth = new SyncVar<int>();
    public readonly SyncVar<int> MaxEnergy = new SyncVar<int>();

    public readonly SyncVar<int> CurrentHealth = new SyncVar<int>();
    public readonly SyncVar<int> CurrentEnergy = new SyncVar<int>();

    public readonly SyncVar<string> CurrentStatuses = new SyncVar<string>();

    [SerializeField] public List<Card> StarterDeckPrefabs = new List<Card>();

    public readonly SyncList<int> currentDeckCardIds = new SyncList<int>();
    public readonly SyncList<int> playerHandCardIds = new SyncList<int>(); // "PlayerHand" for pet refers to its own hand
    public readonly SyncList<int> discardPileCardIds = new SyncList<int>();

    [SerializeField] public Transform PetHandTransform; // Assign in prefab inspector
    [SerializeField] public Transform DeckTransform; // Assign in prefab inspector
    [SerializeField] public Transform DiscardTransform; // Assign in prefab inspector

    private GameManager gameManager;

    public override void OnStartServer()
    {
        base.OnStartServer();
        gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            MaxHealth.Value = gameManager.PetMaxHealth.Value;
            MaxEnergy.Value = gameManager.PetMaxEnergy.Value;
        }
        else Debug.LogWarning("GameManager not found in NetworkPet.OnStartServer. Max stats will use defaults.");
        
        if(MaxHealth.Value == 0) MaxHealth.Value = 50; 
        if(MaxEnergy.Value == 0) MaxEnergy.Value = 2;
        if(PetName.Value == null) PetName.Value = "DefaultPetName";

        CurrentHealth.Value = MaxHealth.Value;
        CurrentEnergy.Value = MaxEnergy.Value;
        InitializeDeck();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Client-side initialization for pet visuals or UI linked to this pet
    }

    [Server]
    private void InitializeDeck()
    {
        currentDeckCardIds.Clear();
        playerHandCardIds.Clear();
        discardPileCardIds.Clear();

        if (StarterDeckPrefabs != null)
        {
            foreach (Card cardPrefab in StarterDeckPrefabs)
            {
                if (cardPrefab != null)
                {
                    currentDeckCardIds.Add(cardPrefab.CardId);
                }
            }
        }
        Debug.Log($"Pet {PetName.Value} (ID: {ObjectId}) deck initialized with {currentDeckCardIds.Count} cards.");
        // ShuffleDeck();
    }

    [Server]
    public void SetOwnerPlayer(NetworkPlayer ownerPlayer)
    {
        if (ownerPlayer != null)
        {
            OwnerPlayerObjectId.Value = (uint) ownerPlayer.ObjectId;
        }
        else
        {
            OwnerPlayerObjectId.Value = 0U;
        }
    }

    public NetworkPlayer GetOwnerPlayer(NetworkManager networkManager = null)
    {
        if (OwnerPlayerObjectId.Value == 0U) return null;
        NetworkManager nm = networkManager ?? FishNet.InstanceFinder.NetworkManager;
        if (nm == null) return null;
        
        NetworkObject playerNob = null;
        // Try to get from ServerManager if server is running, otherwise from ClientManager
        if (nm.IsServerStarted && nm.ServerManager != null && nm.ServerManager.Objects != null)
        {
            nm.ServerManager.Objects.Spawned.TryGetValue((int)OwnerPlayerObjectId.Value, out playerNob);
        }
        else if (nm.IsClientStarted && nm.ClientManager != null && nm.ClientManager.Objects != null)
        {
            nm.ClientManager.Objects.Spawned.TryGetValue((int)OwnerPlayerObjectId.Value, out playerNob);
        }

        if (playerNob != null)
        {
            return playerNob.GetComponent<NetworkPlayer>();
        }
        return null;
    }

    [Server]
    public void TakeDamage(int amount)
    {
        CurrentHealth.Value -= amount;
        if (CurrentHealth.Value <= 0)
        {
            CurrentHealth.Value = 0;
            Debug.Log($"Pet {PetName.Value} has been defeated.");
        }
    }

    [Server]
    public void Heal(int amount)
    {
        CurrentHealth.Value += amount;
        if (CurrentHealth.Value > MaxHealth.Value) CurrentHealth.Value = MaxHealth.Value;
    }

    [Server]
    public void ChangeEnergy(int amount)
    {
        CurrentEnergy.Value += amount;
        if (CurrentEnergy.Value < 0) CurrentEnergy.Value = 0;
        if (CurrentEnergy.Value > MaxEnergy.Value) CurrentEnergy.Value = MaxEnergy.Value;
    }

    [Server]
    public void ReplenishEnergy(){
        CurrentEnergy.Value = MaxEnergy.Value;
    }
} 