using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using FishNet.Managing;

/// <summary>
/// Represents a pet entity in the networked game with health, energy, cards, and other game-related stats.
/// Attach to: The NetworkPetPrefab GameObject that is instantiated for each connected client's pet.
/// </summary>
public class NetworkPet : NetworkBehaviour
{
    // Link to the owner (could be a NetworkPlayer instance or the connection)
    // If a pet is always tied to a specific NetworkPlayer, you might sync the NetworkPlayer's ObjectId
    public readonly SyncVar<uint> OwnerPlayerObjectId = new SyncVar<uint>();

    public readonly SyncVar<string> PetName = new SyncVar<string>();

    [Header("Health & Energy (Editable in Inspector for Debugging)")]
    [SerializeField] private int _maxHealth = 50;
    [SerializeField] private int _maxEnergy = 2;
    [SerializeField] private int _currentHealth = 50;
    [SerializeField] private int _currentEnergy = 2;

    // SyncVars backed by serialized fields for easier debugging
    public readonly SyncVar<int> MaxHealth = new SyncVar<int>();
    public readonly SyncVar<int> MaxEnergy = new SyncVar<int>();
    public readonly SyncVar<int> CurrentHealth = new SyncVar<int>();
    public readonly SyncVar<int> CurrentEnergy = new SyncVar<int>();

    public readonly SyncVar<string> CurrentStatuses = new SyncVar<string>();

    public readonly SyncList<int> currentDeckCardIds = new SyncList<int>();
    public readonly SyncList<int> playerHandCardIds = new SyncList<int>(); // "PlayerHand" for pet refers to its own hand
    public readonly SyncList<int> discardPileCardIds = new SyncList<int>();

    private GameManager gameManager;
    
    // Reference to RelationshipManager for client ID tracking
    private RelationshipManager relationshipManager;
    
    // Extension properties for backward compatibility
    public Transform PetHandTransform => GetComponent<NetworkPetUI>()?.GetPetHandTransform();
    public Transform DeckTransform => GetComponent<NetworkPetUI>()?.GetDeckTransform();
    public Transform DiscardTransform => GetComponent<NetworkPetUI>()?.GetDiscardTransform();
    
    // Quick access to client ID
    public int OwnerClientId 
    { 
        get 
        {
            if (relationshipManager != null)
                return relationshipManager.OwnerClientId;
                
            if (Owner != null)
                return Owner.ClientId;
                
            return -1;
        }
    }
    
    // Quick access to server ownership status
    public bool IsOwnedByServer
    {
        get
        {
            if (relationshipManager != null)
                return relationshipManager.IsOwnedByServer;
                
            if (Owner != null)
                return Owner.IsHost || Owner.ClientId == 0;
                
            return false;
        }
    }
    
    private void Awake()
    {
        relationshipManager = GetComponent<RelationshipManager>();
        if (relationshipManager == null)
        {
            relationshipManager = gameObject.AddComponent<RelationshipManager>();
            Debug.Log($"Added RelationshipManager to pet {gameObject.name}");
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        // Use serialized fields as initial values
        MaxHealth.Value = _maxHealth;
        MaxEnergy.Value = _maxEnergy;
        CurrentHealth.Value = _currentHealth;
        CurrentEnergy.Value = _currentEnergy;
        
        // Override with GameManager values if available
        gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            MaxHealth.Value = gameManager.PetMaxHealth.Value;
            MaxEnergy.Value = gameManager.PetMaxEnergy.Value;
            CurrentHealth.Value = MaxHealth.Value;
            CurrentEnergy.Value = MaxEnergy.Value;
        }
        else Debug.LogWarning("GameManager not found in NetworkPet.OnStartServer. Using serialized field values.");
        
        // Set defaults if GameManager didn't override and serialized fields are zero
        if(MaxHealth.Value == 0) MaxHealth.Value = 50; 
        if(MaxEnergy.Value == 0) MaxEnergy.Value = 2;
        
        // Initialize pet name based on allied player or a default
        SetPetNameBasedOnOwner();
        
        // Synchronize serialized fields with SyncVars
        _maxHealth = MaxHealth.Value;
        _maxEnergy = MaxEnergy.Value;
        _currentHealth = CurrentHealth.Value;
        _currentEnergy = CurrentEnergy.Value;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // Register with EntityVisibilityManager if available
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager != null)
        {
            EntityVisibilityManager entityVisManager = gamePhaseManager.GetComponent<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.RegisterPet(this);
            }
            else
            {
                Debug.LogWarning($"NetworkPet: EntityVisibilityManager not found on GamePhaseManager for {PetName.Value}");
            }
        }
        else
        {
            // Try to find EntityVisibilityManager directly as fallback
            EntityVisibilityManager entityVisManager = FindFirstObjectByType<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.RegisterPet(this);
            }
            else
            {
                Debug.LogWarning($"NetworkPet: Neither GamePhaseManager nor EntityVisibilityManager found for {PetName.Value}");
            }
        }
        
        // Client-side initialization for pet visuals or UI linked to this pet
        
        // Set up listener for pet name changes
        PetName.OnChange += OnPetNameChanged;
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // Unregister from EntityVisibilityManager when despawned
        GamePhaseManager gamePhaseManager = GamePhaseManager.Instance;
        if (gamePhaseManager != null)
        {
            EntityVisibilityManager entityVisManager = gamePhaseManager.GetComponent<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.UnregisterPet(this);
            }
        }
        else
        {
            // Try to find EntityVisibilityManager directly as fallback
            EntityVisibilityManager entityVisManager = FindFirstObjectByType<EntityVisibilityManager>();
            if (entityVisManager != null)
            {
                entityVisManager.UnregisterPet(this);
            }
        }
        
        // Remove name change listener
        PetName.OnChange -= OnPetNameChanged;
    }
    
    /// <summary>
    /// Called when the pet's name changes
    /// </summary>
    private void OnPetNameChanged(string oldName, string newName, bool asServer)
    {
        Debug.Log($"Pet name changed from {oldName} to {newName} (asServer: {asServer})");
        
        // Update UI components that display the pet's name
        var petUI = GetComponent<NetworkPetUI>();
        if (petUI != null)
        {
            petUI.UpdateNameDisplay();
        }
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
        // Update serialized field
        _currentHealth = CurrentHealth.Value;
    }

    [Server]
    public void Heal(int amount)
    {
        CurrentHealth.Value += amount;
        if (CurrentHealth.Value > MaxHealth.Value) CurrentHealth.Value = MaxHealth.Value;
        // Update serialized field
        _currentHealth = CurrentHealth.Value;
    }

    [Server]
    public void ChangeEnergy(int amount)
    {
        CurrentEnergy.Value += amount;
        if (CurrentEnergy.Value < 0) CurrentEnergy.Value = 0;
        if (CurrentEnergy.Value > MaxEnergy.Value) CurrentEnergy.Value = MaxEnergy.Value;
        // Update serialized field
        _currentEnergy = CurrentEnergy.Value;
    }

    [Server]
    public void ReplenishEnergy(){
        CurrentEnergy.Value = MaxEnergy.Value;
        // Update serialized field
        _currentEnergy = CurrentEnergy.Value;
    }

    [Server]
    public void IncreaseMaxHealth(int amount)
    {
        MaxHealth.Value += amount;
        CurrentHealth.Value += amount;
        // Update serialized fields
        _maxHealth = MaxHealth.Value;
        _currentHealth = CurrentHealth.Value;
    }

    [Server]
    public void IncreaseMaxEnergy(int amount)
    {
        MaxEnergy.Value += amount;
        CurrentEnergy.Value += amount;
        // Update serialized fields
        _maxEnergy = MaxEnergy.Value;
        _currentEnergy = CurrentEnergy.Value;
    }
    
    /// <summary>
    /// Notifies clients that a card has been played by this pet (legacy version without instance ID)
    /// </summary>
    [ObserversRpc]
    public void NotifyCardPlayed(int cardId)
    {
        if (!IsClientInitialized) return;
        
        Debug.Log($"Pet NotifyCardPlayed (legacy) received on client for card ID {cardId}, IsOwner: {IsOwner}, IsLocalPet: {IsClientInitialized && IsOwner}");
        
        // Check if this object is still valid before proceeding
        if (!IsSpawned) 
        {
            Debug.LogWarning($"NotifyCardPlayed: NetworkPet object is no longer spawned. Ignoring card removal for card {cardId}");
            return;
        }
        
        // On client side, get the CardSpawner and tell it to remove the card
        CardSpawner cardSpawner = GetComponent<CardSpawner>();
        if (cardSpawner != null)
        {
            // Only remove cards from pets that belong to this client or in situations where this client needs to visualize a pet's move
            if (IsOwner || IsClientOnlyInitialized || FishNet.InstanceFinder.IsHostStarted)
            {
                try
                {
                    // Use the instance-aware method to find and remove the correct card
                    cardSpawner.OnServerConfirmCardPlayed(cardId);
                    Debug.Log($"Server confirmed card {cardId} played by pet - removing from display");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Exception when removing pet card {cardId}: {ex.Message}");
                }
            }
            else
            {
                Debug.Log($"Ignoring pet card removal for non-owner client. Card: {cardId}, Pet: {PetName.Value}, ObjectId: {ObjectId}");
            }
        }
        else
        {
            Debug.LogError("NetworkPet.NotifyCardPlayed: CardSpawner component not found.");
        }
    }

    /// <summary>
    /// Notifies clients that a card has been played by this pet (with instance ID for precise card tracking)
    /// </summary>
    [ObserversRpc]
    public void NotifyCardPlayed(int cardId, string cardInstanceId)
    {
        if (!IsClientInitialized) return;
        
        Debug.Log($"Pet NotifyCardPlayed received on client for card ID {cardId}, Instance ID {cardInstanceId}, IsOwner: {IsOwner}, IsLocalPet: {IsClientInitialized && IsOwner}");
        
        // Check if this object is still valid before proceeding
        if (!IsSpawned) 
        {
            Debug.LogWarning($"NotifyCardPlayed: NetworkPet object is no longer spawned. Ignoring card removal for card {cardId}");
            return;
        }
        
        // On client side, get the CardSpawner and tell it to remove the card
        CardSpawner cardSpawner = GetComponent<CardSpawner>();
        if (cardSpawner != null)
        {
            // Only remove cards from pets that belong to this client or in situations where this client needs to visualize a pet's move
            if (IsOwner || IsClientOnlyInitialized || FishNet.InstanceFinder.IsHostStarted)
            {
                try
                {
                    // Use the instance-specific method for server-confirmed card removal
                    cardSpawner.OnServerConfirmCardPlayed(cardId, cardInstanceId);
                    Debug.Log($"Server confirmed card {cardId} (Instance: {cardInstanceId}) played by pet - removing from display");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Exception when removing pet card {cardId} (Instance: {cardInstanceId}): {ex.Message}");
                }
            }
            else
            {
                Debug.Log($"Ignoring pet card removal for non-owner client. Card: {cardId}, Pet: {PetName.Value}, ObjectId: {ObjectId}");
            }
        }
        else
        {
            Debug.LogError("NetworkPet.NotifyCardPlayed: CardSpawner component not found.");
        }
    }

    /// <summary>
    /// Sets the pet's name based on its owner player's connection ID
    /// </summary>
    [Server]
    private void SetPetNameBasedOnOwner()
    {
        // Try to get owner player first from OwnerPlayerObjectId
        NetworkPlayer ownerPlayer = GetOwnerPlayer();
        
        // If direct owner lookup fails, try using RelationshipManager 
        if (ownerPlayer == null)
        {
            relationshipManager = GetComponent<RelationshipManager>();
            if (relationshipManager != null && relationshipManager.Ally != null)
            {
                ownerPlayer = relationshipManager.Ally as NetworkPlayer;
            }
        }
        
        // If still no owner, try finding through FightManager
        if (ownerPlayer == null && FightManager.Instance != null)
        {
            ownerPlayer = FightManager.Instance.GetOpponentForPet(this);
        }
        
        int clientId = -1;
        if (ownerPlayer != null && ownerPlayer.Owner != null)
        {
            clientId = ownerPlayer.Owner.ClientId;
            // Set name based on owner's connection ID
            PetName.Value = $"Pet ({clientId})";
            Debug.Log($"Set pet name to {PetName.Value} based on owner Player {ownerPlayer.PlayerName.Value} with Connection ID {clientId}");
        }
        else if (relationshipManager != null)
        {
            // Use RelationshipManager's client ID as backup
            clientId = relationshipManager.OwnerClientId;
            PetName.Value = $"Pet ({clientId})";
            Debug.Log($"Set pet name to {PetName.Value} based on RelationshipManager.OwnerClientId: {clientId}");
        }
        else
        {
            // If we couldn't find the owner, use a default name with indicator
            PetName.Value = "Pet (No Owner)";
            Debug.LogWarning("Could not find owner player for pet - using default name");
        }
        
        // Also log additional connection info for debugging
        if (relationshipManager != null)
        {
            Debug.Log($"Pet {PetName.Value} RelationshipManager info: OwnerClientId: {relationshipManager.OwnerClientId}, IsOwnedByServer: {relationshipManager.IsOwnedByServer}");
        }
    }
    
    /// <summary>
    /// Updates the pet's name when its owner changes
    /// </summary>
    [Server]
    public void UpdatePetName()
    {
        SetPetNameBasedOnOwner();
    }

    /// <summary>
    /// Simulates a card being played by the pet
    /// </summary>
    [Server]
    public void PlayCard(int cardId)
    {
        // Make sure the pet has the card in its hand first
        CombatHand petHand = GetComponent<CombatHand>();
        if (petHand == null || !petHand.HasCard(cardId))
        {
            Debug.LogWarning($"Pet {PetName.Value} tried to play card {cardId} but it's not in their hand");
            return;
        }
        
        // Get the card data
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null)
        {
            Debug.LogWarning($"Pet {PetName.Value} tried to play card with ID {cardId} but it doesn't exist in the database");
            return;
        }
        
        // Check if the pet has enough energy
        if (CurrentEnergy.Value < cardData.EnergyCost)
        {
            Debug.LogWarning($"Pet {PetName.Value} tried to play card {cardData.CardName} but doesn't have enough energy");
            return;
        }
        
        // Get the correct target based on the card's target type
        NetworkBehaviour target = null;
        bool targetFound = false;
        
        switch (cardData.TargetType)
        {
            case CardTargetType.Self:
                // Target self (the pet itself)
                target = this;
                targetFound = true;
                break;
                
            case CardTargetType.Opponent:
                // Target opponent player
                if (FightManager.Instance != null)
                {
                    NetworkPlayer opponent = FightManager.Instance.GetOpponentForPet(this);
                    if (opponent != null)
                    {
                        target = opponent;
                        targetFound = true;
                    }
                }
                break;
                
            case CardTargetType.Ally:
                // Target pet's owner player
                NetworkPlayer owner = GetOwnerPlayer();
                if (owner != null)
                {
                    target = owner;
                    targetFound = true;
                }
                break;
                
            case CardTargetType.Random:
                // Randomly choose between opponent player and self
                if (Random.value < 0.5f)
                {
                    // Target self
                    target = this;
                    targetFound = true;
                }
                else
                {
                    // Target opponent player
                    if (FightManager.Instance != null)
                    {
                        NetworkPlayer opponent = FightManager.Instance.GetOpponentForPet(this);
                        if (opponent != null)
                        {
                            target = opponent;
                            targetFound = true;
                        }
                    }
                }
                break;
        }
        
        if (!targetFound || target == null)
        {
            Debug.LogError($"Pet {PetName.Value} couldn't find a target for card {cardData.CardName} with target type {cardData.TargetType}");
            return;
        }
        
        // Make sure the target has an effect manager
        EffectManager targetEffectManager = target.GetComponent<EffectManager>();
        if (targetEffectManager == null)
        {
            Debug.LogError($"Pet {PetName.Value} tried to play card {cardData.CardName} but target {target.name} doesn't have an EffectManager");
            return;
        }
        
        // Apply the card effect to the target
        targetEffectManager.ApplyEffect(target, cardData);
        
        // Deduct energy cost
        ChangeEnergy(-cardData.EnergyCost);
        
        // Generate a unique instance ID for tracking on clients
        string cardInstanceId = $"{cardId}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        
        // Move card to the discard pile
        HandManager handManager = GetComponent<HandManager>();
        if (handManager != null)
        {
            handManager.MoveCardToDiscard(cardId);
        }
        
        // Notify clients that a card was played
        NotifyCardPlayed(cardId, cardInstanceId);
        
        // Notify the combat manager that a card was played
        CombatManager.Instance?.NotifyCardPlayed((uint)ObjectId, (uint)target.ObjectId, cardId, cardInstanceId, null);
        
        Debug.Log($"Pet {PetName.Value} played card {cardData.CardName} targeting {target.name}");
    }

    /// <summary>
    /// Tries to play a card and returns the success status, card instance ID, and target
    /// </summary>
    [Server]
    public bool TryPlayCard(int cardId, out string cardInstanceId, out NetworkBehaviour target)
    {
        cardInstanceId = string.Empty;
        target = null;
        
        // Make sure the pet has the card in its hand first
        CombatHand petHand = GetComponent<CombatHand>();
        if (petHand == null || !petHand.HasCard(cardId))
        {
            Debug.LogWarning($"Pet {PetName.Value} tried to play card {cardId} but it's not in their hand");
            return false;
        }
        
        // Get the card data
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null)
        {
            Debug.LogWarning($"Pet {PetName.Value} tried to play card with ID {cardId} but it doesn't exist in the database");
            return false;
        }
        
        // Check if the pet has enough energy
        if (CurrentEnergy.Value < cardData.EnergyCost)
        {
            Debug.LogWarning($"Pet {PetName.Value} tried to play card {cardData.CardName} but doesn't have enough energy");
            return false;
        }
        
        // Get the correct target based on the card's target type
        NetworkBehaviour cardTarget = null;
        bool targetFound = false;
        
        switch (cardData.TargetType)
        {
            case CardTargetType.Self:
                // Target self (the pet itself)
                cardTarget = this;
                targetFound = true;
                break;
                
            case CardTargetType.Opponent:
                // Target opponent player
                if (FightManager.Instance != null)
                {
                    NetworkPlayer opponent = FightManager.Instance.GetOpponentForPet(this);
                    if (opponent != null)
                    {
                        cardTarget = opponent;
                        targetFound = true;
                    }
                }
                break;
                
            case CardTargetType.Ally:
                // Target pet's owner player
                NetworkPlayer owner = GetOwnerPlayer();
                if (owner != null)
                {
                    cardTarget = owner;
                    targetFound = true;
                }
                break;
                
            case CardTargetType.Random:
                // Randomly choose between opponent player and self
                if (Random.value < 0.5f)
                {
                    // Target self
                    cardTarget = this;
                    targetFound = true;
                }
                else
                {
                    // Target opponent player
                    if (FightManager.Instance != null)
                    {
                        NetworkPlayer opponent = FightManager.Instance.GetOpponentForPet(this);
                        if (opponent != null)
                        {
                            cardTarget = opponent;
                            targetFound = true;
                        }
                    }
                }
                break;
        }
        
        if (!targetFound || cardTarget == null)
        {
            Debug.LogError($"Pet {PetName.Value} couldn't find a target for card {cardData.CardName} with target type {cardData.TargetType}");
            return false;
        }
        
        // Make sure the target has an effect manager
        EffectManager targetEffectManager = cardTarget.GetComponent<EffectManager>();
        if (targetEffectManager == null)
        {
            Debug.LogError($"Pet {PetName.Value} tried to play card {cardData.CardName} but target {cardTarget.name} doesn't have an EffectManager");
            return false;
        }
        
        // Apply the card effect to the target
        targetEffectManager.ApplyEffect(cardTarget, cardData);
        
        // Deduct energy cost
        ChangeEnergy(-cardData.EnergyCost);
        
        // Generate a unique instance ID for tracking on clients
        cardInstanceId = $"{cardId}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        
        // Set the output target
        target = cardTarget;
        
        // Move card to the discard pile
        HandManager handManager = GetComponent<HandManager>();
        if (handManager != null)
        {
            handManager.MoveCardToDiscard(cardId);
        }
        
        // Notify clients that a card was played
        NotifyCardPlayed(cardId, cardInstanceId);
        
        // Notify the combat manager that a card was played
        CombatManager.Instance?.NotifyCardPlayed((uint)ObjectId, (uint)cardTarget.ObjectId, cardId, cardInstanceId, null);
        
        Debug.Log($"Pet {PetName.Value} played card {cardData.CardName} targeting {cardTarget.name}");
        return true;
    }
} 