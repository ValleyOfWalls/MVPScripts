using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class DraftService
{
    private GameManager gameManager;
    private List<CardData> allPlayerCardPool = new List<CardData>();
    private List<CardData> allPetCardPool = new List<CardData>();
    
    // Draft state
    private List<SerializableDraftOption> localCurrentPack = new List<SerializableDraftOption>();
    private Dictionary<int, DraftOption> localActiveOptionMap = new Dictionary<int, DraftOption>();
    private List<int> draftPlayerOrder = new List<int>();
    private Dictionary<int, int> draftPicksMade = new Dictionary<int, int>();
    private bool isWaitingForLocalPick = false;
    
    // Draft constants
    public const string DRAFT_PLAYER_QUEUES_PROP = "DraftQueues";
    public const string DRAFT_PICKS_MADE_PROP = "DraftPicks";
    public const string DRAFT_ORDER_PROP = "DraftOrder";
    
    public DraftService(GameManager gameManager, List<CardData> allPlayerCardPool, List<CardData> allPetCardPool)
    {
        this.gameManager = gameManager;
        this.allPlayerCardPool = new List<CardData>(allPlayerCardPool ?? new List<CardData>());
        this.allPetCardPool = new List<CardData>(allPetCardPool ?? new List<CardData>());
    }
    
    public void InitializeDraftState(int optionsPerDraft)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        Debug.Log("Master Client initializing draft state (creating queues)... ");
        
        Dictionary<int, List<string>> initialQueues = new Dictionary<int, List<string>>();
        Dictionary<int, int> initialPicks = new Dictionary<int, int>();
        int totalOptionsGenerated = 0;
        int optionIdCounter = 0;
        
        // Determine player order (simple example: based on ActorNumber)
        List<int> playerOrder = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).OrderBy(n => n).ToList();
        string playerOrderJson = JsonConvert.SerializeObject(playerOrder);
        
        int initialOptionsPerPack = optionsPerDraft;
        
        // Create one pack for each player
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            List<SerializableDraftOption> packForPlayer = new List<SerializableDraftOption>();
            Debug.Log($"Generating pack for player {player.NickName} ({player.ActorNumber})");
            
            for (int i = 0; i < initialOptionsPerPack; i++)
            {
                optionIdCounter++;
                SerializableDraftOption newOption = GenerateSingleDraftOption(optionIdCounter);
                if (newOption != null)
                {
                    packForPlayer.Add(newOption);
                    totalOptionsGenerated++;
                }
                else i--; // Try again if generation failed (e.g., pools empty)
            }
            
            // Shuffle the pack for this player
            System.Random packRng = new System.Random();
            packForPlayer = packForPlayer.OrderBy(a => packRng.Next()).ToList();
            
            // Add the serialized pack as the first element in the player's queue
            initialQueues[player.ActorNumber] = new List<string> { JsonConvert.SerializeObject(packForPlayer) };
            
            initialPicks[player.ActorNumber] = 0; // Initialize picks made to 0
        }
        
        // Serialize dictionaries for network
        string queuesJson = JsonConvert.SerializeObject(initialQueues);
        string picksJson = JsonConvert.SerializeObject(initialPicks);
        
        // Set initial Room Properties
        Hashtable initialRoomProps = new Hashtable
        {
            { DRAFT_PLAYER_QUEUES_PROP, queuesJson },
            { DRAFT_PICKS_MADE_PROP, picksJson },
            { DRAFT_ORDER_PROP, playerOrderJson }
        };
        
        Debug.Log($"Setting initial draft properties: {initialQueues.Count} queues generated ({totalOptionsGenerated} total options), Order: {string.Join(", ", playerOrder)}");
        PhotonNetwork.CurrentRoom.SetCustomProperties(initialRoomProps);
    }
    
    private SerializableDraftOption GenerateSingleDraftOption(int optionId)
    {
        // Increased range to potentially include Upgrade Card options
        int choiceType = Random.Range(0, 6); // 0: Player Add, 1: Pet Add, 2: Player Stat, 3: Pet Stat, 4: Player Upgrade, 5: Pet Upgrade
        
        // Try Upgrade Card (if applicable deck has upgradable cards)
        if (choiceType >= 4)
        {
            bool forPet = (choiceType == 5);
            DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
            PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
            List<CardData> deckToCheck = forPet ? petDeckManager.GetLocalPetDeck() : deckManager.GetDeck();
            
            // Find cards in the deck that HAVE an upgrade defined
            List<CardData> upgradableCards = deckToCheck.Where(card => card != null && card.upgradedVersion != null).ToList();
            
            if (upgradableCards.Count > 0)
            {
                CardData cardToUpgrade = upgradableCards[Random.Range(0, upgradableCards.Count)];
                DraftOption upgradeOption = DraftOption.CreateUpgradeCardOption(optionId, cardToUpgrade, forPet);
                if(upgradeOption != null) return SerializableDraftOption.FromDraftOption(upgradeOption);
            }
        }
        
        // Fallback / Original Logic: Try Add Card
        if (choiceType <= 1 || choiceType >= 4) // Also try adding if upgrade failed
        {
            // Determine if adding for pet (either explicitly chosen or player pool empty)
            bool forPetAdd = (choiceType == 1 || choiceType == 5) ?
                              (allPetCardPool.Count > 0) :
                              (allPlayerCardPool.Count == 0 && allPetCardPool.Count > 0);
            
            List<CardData> pool = forPetAdd ? allPetCardPool : allPlayerCardPool;
            if (pool.Count > 0)
            {
                CardData randomCard = pool[Random.Range(0, pool.Count)];
                return SerializableDraftOption.FromDraftOption(DraftOption.CreateAddCardOption(optionId, randomCard, forPetAdd));
            }
        }
        
        // Fallback: Upgrade Stat
        bool forPetStat = (choiceType == 3 || choiceType == 5);
        StatType stat = (StatType)Random.Range(0, System.Enum.GetValues(typeof(StatType)).Length);
        int amount = 0;
        if (stat == StatType.MaxHealth) amount = Random.Range(5, 11);
        else if (stat == StatType.StartingEnergy) amount = 1;
        
        if (amount > 0)
        {
            if(forPetStat && stat == StatType.StartingEnergy)
            {
                if(gameManager.GetPlayerManager().GetStartingEnergy() < 10) {
                    return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, StatType.StartingEnergy, amount, false));
                } else {
                    return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, StatType.MaxHealth, Random.Range(5, 11), true));
                }
            }
            return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, stat, amount, forPetStat));
        }
        
        // Final Fallback
        Debug.LogWarning("GenerateSingleDraftOption: Failed to generate desired option type, defaulting to Player Health upgrade.");
        return SerializableDraftOption.FromDraftOption(DraftOption.CreateUpgradeStatOption(optionId, StatType.MaxHealth, 5, false));
    }
    
    public void ApplyDraftChoice(DraftOption choice)
    {
        if (choice == null)
        {
            Debug.LogError("ApplyDraftChoice received a null choice!");
            return;
        }
        
        Debug.Log($"Applying draft choice: {choice.Description}");
        
        switch (choice.Type)
        {
            case DraftOptionType.AddPlayerCard:
                if (choice.CardToAdd != null)
                {
                    gameManager.GetCardManager().GetDeckManager().AddCardToPlayerDeck(choice.CardToAdd);
                    gameManager.UpdateDeckCountUI();
                    Debug.Log($"Added {choice.CardToAdd.cardName} to player deck.");
                }
                else { Debug.LogWarning("AddPlayerCard choice had null CardToAdd."); }
                break;
                
            case DraftOptionType.AddPetCard:
                if (choice.CardToAdd != null)
                {
                    gameManager.GetCardManager().GetPetDeckManager().AddCardToPetDeck(choice.CardToAdd);
                    Debug.Log($"Added {choice.CardToAdd.cardName} to local pet deck.");
                }
                else { Debug.LogWarning("AddPetCard choice had null CardToAdd."); }
                break;
                
            case DraftOptionType.UpgradePlayerStat:
                int startingPlayerHealth = gameManager.GetStartingPlayerHealth();
                int localPlayerHealth = gameManager.GetPlayerManager().GetLocalPlayerHealth();
                
                if (choice.StatToUpgrade == StatType.MaxHealth)
                {
                    gameManager.SetStartingPlayerHealth(startingPlayerHealth + choice.StatIncreaseAmount);
                    gameManager.GetPlayerManager().DamageLocalPlayer(-choice.StatIncreaseAmount); // Negative damage = healing
                    Debug.Log($"Upgraded Player Max Health by {choice.StatIncreaseAmount}. New base: {gameManager.GetStartingPlayerHealth()}");
                }
                else if (choice.StatToUpgrade == StatType.StartingEnergy)
                {
                    gameManager.SetStartingEnergy(gameManager.GetPlayerManager().GetStartingEnergy() + choice.StatIncreaseAmount);
                    gameManager.GetPlayerManager().SetCurrentEnergy(gameManager.GetPlayerManager().GetCurrentEnergy() + choice.StatIncreaseAmount);
                    Debug.Log($"Upgraded Player Starting Energy by {choice.StatIncreaseAmount}. New base: {gameManager.GetPlayerManager().GetStartingEnergy()}");
                }
                gameManager.UpdateHealthUI();
                gameManager.UpdateEnergyUI();
                break;
                
            case DraftOptionType.UpgradePetStat:
                int startingPetHealth = gameManager.GetStartingPetHealth();
                int localPetHealth = gameManager.GetPlayerManager().GetLocalPetHealth();
                
                if (choice.StatToUpgrade == StatType.MaxHealth)
                {
                    gameManager.SetStartingPetHealth(startingPetHealth + choice.StatIncreaseAmount);
                    gameManager.GetPlayerManager().DamageLocalPet(-choice.StatIncreaseAmount); // Negative damage = healing
                    Debug.Log($"Upgraded Pet Max Health by {choice.StatIncreaseAmount}. New base: {gameManager.GetStartingPetHealth()}");
                    Hashtable petProps = new Hashtable { { PlayerManager.PLAYER_BASE_PET_HP_PROP, gameManager.GetStartingPetHealth() } };
                    PhotonNetwork.LocalPlayer.SetCustomProperties(petProps);
                    Debug.Log($"Set {PlayerManager.PLAYER_BASE_PET_HP_PROP} to {gameManager.GetStartingPetHealth()} for local player.");
                }
                gameManager.UpdateHealthUI();
                break;
                
            case DraftOptionType.UpgradePlayerCard:
                if (choice.CardToUpgrade != null && choice.CardToUpgrade.upgradedVersion != null)
                {
                    DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                    List<CardData> deck = deckManager.GetDeck();
                    int indexToUpgrade = deck.FindIndex(card => card == choice.CardToUpgrade);
                    if (indexToUpgrade != -1)
                    {
                        CardData upgradedCard = choice.CardToUpgrade.upgradedVersion;
                        deck[indexToUpgrade] = upgradedCard;
                        deckManager.ShuffleDeck();
                        gameManager.UpdateDeckCountUI();
                        Debug.Log($"Upgraded player card {choice.CardToUpgrade.cardName} to {upgradedCard.cardName} in deck.");
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find player card '{choice.CardToUpgrade.cardName}' in deck to upgrade.");
                    }
                }
                else
                {
                    Debug.LogError($"UpgradePlayerCard choice failed: CardToUpgrade ({choice.CardToUpgrade?.cardName}) or its upgradedVersion was null.");
                }
                break;
                
            case DraftOptionType.UpgradePetCard:
                if (choice.CardToUpgrade != null && choice.CardToUpgrade.upgradedVersion != null)
                {
                    gameManager.GetCardManager().GetPetDeckManager().UpgradePetCard(choice.CardToUpgrade, choice.CardToUpgrade.upgradedVersion);
                }
                else
                {
                    Debug.LogError($"UpgradePetCard choice failed: CardToUpgrade ({choice.CardToUpgrade?.cardName}) or its upgradedVersion was null.");
                }
                break;
        }
    }
    
    public void UpdateLocalDraftStateFromRoomProps()
    {
        if (isWaitingForLocalPick)
        {
            Debug.Log("UpdateLocalDraftStateFromRoomProps: Skipping update, player is still picking (isWaitingForLocalPick=true).");
            return;
        }
        
        bool needsUIUpdate = false;
        localCurrentPack.Clear();
        localActiveOptionMap.Clear();
        bool nowHasPack = false;
        
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_PLAYER_QUEUES_PROP, out object queuesObj))
        {
            try
            {
                string queuesJson = queuesObj as string;
                Dictionary<int, List<string>> currentQueues = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(queuesJson) ?? new Dictionary<int, List<string>>();
                
                int localActorNum = PhotonNetwork.LocalPlayer.ActorNumber;
                if (currentQueues.ContainsKey(localActorNum) && currentQueues[localActorNum] != null && currentQueues[localActorNum].Count > 0)
                {
                    string nextPackJson = currentQueues[localActorNum][0]; // Peek at the first pack
                    List<SerializableDraftOption> serializablePack = JsonConvert.DeserializeObject<List<SerializableDraftOption>>(nextPackJson) ?? new List<SerializableDraftOption>();
                    
                    // Deserialize into full DraftOption, linking actual CardData
                    foreach (var serializableOption in serializablePack)
                    {
                        DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                        PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
                        
                        DraftOption fullOption = serializableOption.ToDraftOption(
                            allPlayerCardPool, 
                            allPetCardPool, 
                            deckManager.GetDeck(), 
                            petDeckManager.GetLocalPetDeck());
                            
                        if (fullOption != null)
                        {
                            localCurrentPack.Add(serializableOption);
                            localActiveOptionMap[serializableOption.OptionId] = fullOption;
                        }
                        else
                        {
                            Debug.LogError($"Failed to deserialize draft option ID {serializableOption.OptionId} ({serializableOption.Description}) - skipping.");
                        }
                    }
                    
                    if(localCurrentPack.Count > 0)
                    {
                        nowHasPack = true;
                        isWaitingForLocalPick = true;
                        needsUIUpdate = true;
                        Debug.Log($"Deserialized pack for local player. {localCurrentPack.Count} options. Setting isWaitingForLocalPick=true.");
                    }
                    else
                    {
                        Debug.LogWarning("Deserialized pack for local player, but it resulted in zero valid options after deserialization.");
                    }
                }
                else
                {
                    Debug.Log("No pack currently queued for local player.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to deserialize draft queues: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Room property {DRAFT_PLAYER_QUEUES_PROP} not found.");
        }
        
        if (needsUIUpdate)
        {
            Debug.Log("UpdateLocalDraftStateFromRoomProps triggering Draft UI update.");
            gameManager.GetGameStateManager().UpdateDraftUI(localCurrentPack, localActiveOptionMap, isWaitingForLocalPick);
        }
        else if (!nowHasPack && isWaitingForLocalPick)
        {
            // If we were waiting, but now have no pack, reset waiting flag
            Debug.Log("Resetting isWaitingForLocalPick as no pack was found.");
            isWaitingForLocalPick = false;
            gameManager.GetGameStateManager().UpdateDraftUI(localCurrentPack, localActiveOptionMap, isWaitingForLocalPick);
        }
    }
    
    public void UpdateLocalDraftPicksFromRoomProps()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_PICKS_MADE_PROP, out object picksObj))
        {
            try
            {
                string picksJson = picksObj as string;
                draftPicksMade = JsonConvert.DeserializeObject<Dictionary<int, int>>(picksJson) ?? new Dictionary<int, int>();
                Debug.Log($"Successfully deserialized draft picks made: {draftPicksMade.Count} players.");
            }
            catch(System.Exception e)
            {
                Debug.LogError($"Failed to deserialize draft picks: {e.Message}");
                draftPicksMade = new Dictionary<int, int>();
            }
        }
        else
        {
            Debug.LogWarning($"Room property {DRAFT_PICKS_MADE_PROP} not found.");
            draftPicksMade = new Dictionary<int, int>();
        }
    }
    
    public void UpdateLocalDraftOrderFromRoomProps()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(DRAFT_ORDER_PROP, out object orderObj))
        {
            try
            {
                string orderJson = orderObj as string;
                draftPlayerOrder = JsonConvert.DeserializeObject<List<int>>(orderJson) ?? new List<int>();
                Debug.Log($"Successfully deserialized draft order: {string.Join(", ", draftPlayerOrder)}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to deserialize draft order: {e.Message}");
                draftPlayerOrder = new List<int>();
            }
        }
        else
        {
            Debug.LogWarning($"Room property {DRAFT_ORDER_PROP} not found.");
            draftPlayerOrder = new List<int>();
        }
    }
    
    public void HandlePlayerDraftPick(int optionId)
    {
        Debug.Log("Handling player pick, sending to Master Client via RPC");
        isWaitingForLocalPick = false;
        
        // Apply the selection locally
        if (localActiveOptionMap.TryGetValue(optionId, out DraftOption chosenOption))
        {
            ApplyDraftChoice(chosenOption);
        }
        else
        {
            Debug.LogError($"Selected option ID {optionId} not found in localActiveOptionMap!");
        }
        
        // Clear local state
        localCurrentPack.Clear();
        localActiveOptionMap.Clear();
        
        // Update UI to show waiting state
        gameManager.GetGameStateManager().UpdateDraftUI(localCurrentPack, localActiveOptionMap, isWaitingForLocalPick);
        
        // Send RPC to Master Client
        gameManager.GetPhotonView().RPC("RpcPlayerPickedOption", RpcTarget.MasterClient, optionId);
    }
    
    // Getters
    public bool IsWaitingForLocalPick() => isWaitingForLocalPick;
    public void SetWaitingForLocalPick(bool value) => isWaitingForLocalPick = value;
    public Dictionary<int, DraftOption> GetLocalActiveOptionMap() => localActiveOptionMap;
    public List<SerializableDraftOption> GetLocalCurrentPack() => localCurrentPack;
    public List<int> GetDraftPlayerOrder() => draftPlayerOrder;
    public Dictionary<int, int> GetDraftPicksMade() => draftPicksMade;
}