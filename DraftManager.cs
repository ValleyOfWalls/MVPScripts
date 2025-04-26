using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

public class DraftManager
{
    private GameManager gameManager;
    private int optionsPerDraft;

    public void Initialize(GameManager gameManager, int optionsPerDraft)
    {
        this.gameManager = gameManager;
        this.optionsPerDraft = optionsPerDraft;
    }

    public void StartDraft()
    {
        Debug.Log("Starting Draft Phase");
        gameManager.GetGameStateManager().TransitionToDraft();
        gameManager.GetPlayerManager().SetCombatEndedForLocalPlayer(false);

        // Master Client generates and distributes draft options
        if (PhotonNetwork.IsMasterClient)
        {
            gameManager.GetCardManager().InitializeDraftState(optionsPerDraft);
        }
        else
        {
            CardManager cardManager = gameManager.GetCardManager();
            cardManager.UpdateLocalDraftStateFromRoomProps();
            cardManager.UpdateLocalDraftPicksFromRoomProps();
            cardManager.UpdateLocalDraftOrderFromRoomProps();
        }
    }

    public void HandleOptionSelected(int selectedOptionId)
    {
        Debug.Log($"Local player selected option ID: {selectedOptionId}");
        gameManager.GetCardManager().HandlePlayerDraftPick(selectedOptionId);
    }

    public void HandleRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        CardManager cardManager = gameManager.GetCardManager();
        
        if (propertiesThatChanged.ContainsKey(CardManager.DRAFT_PLAYER_QUEUES_PROP))
        {
            Debug.Log("Draft packs updated in room properties.");
            cardManager.UpdateLocalDraftStateFromRoomProps();
        }
        
        if (propertiesThatChanged.ContainsKey(CardManager.DRAFT_PICKS_MADE_PROP))
        {
            Debug.Log("Draft picks made updated in room properties.");
            cardManager.UpdateLocalDraftPicksFromRoomProps();
        }
        
        if (propertiesThatChanged.ContainsKey(CardManager.DRAFT_ORDER_PROP))
        {
            cardManager.UpdateLocalDraftOrderFromRoomProps();
        }

        // Check Draft End Condition
        CheckDraftEndCondition();
    }

    public void CheckDraftEndCondition()
    {
        object queuesProp = null;
        bool queuesExist = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CardManager.DRAFT_PLAYER_QUEUES_PROP, out queuesProp);
        bool draftEnded = !queuesExist || queuesProp == null;
        string queuesJson = queuesProp as string;
        Debug.Log($"Checking draft end. queuesExist={queuesExist}, queuesProp='{queuesJson}'");

        if (!draftEnded && !string.IsNullOrEmpty(queuesJson))
        {
            try
            {
                var queuesDict = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(queuesJson);
                if (queuesDict == null)
                {
                    Debug.LogWarning("End Check: Deserialized queuesDict is null.");
                    draftEnded = true;
                }
                else if (queuesDict.Count == 0)
                {
                    Debug.Log("End Check: queuesDict count is 0.");
                    draftEnded = true;
                }
                else
                {
                    bool allEmpty = queuesDict.Values.All(q => q == null || q.Count == 0);
                    Debug.Log($"End Check: queuesDict count={queuesDict.Count}. All queues empty={allEmpty}");
                    if (allEmpty)
                    {
                        draftEnded = true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error checking draft end condition: {e.Message}");
                draftEnded = true; // Assume ended if state is corrupted
            }
        }
        else if (!draftEnded && string.IsNullOrEmpty(queuesJson))
        {
            Debug.Log("End Check: queuesProp exists but is null/empty string.");
            draftEnded = true;
        }

        if (draftEnded)
        {
            // Prevent multiple calls if EndDraftPhase was already triggered
            if (gameManager.GetGameStateManager().GetCurrentState() == GameState.Drafting)
            {
                Debug.Log("Draft queues property indicates draft ended. Calling EndDraftPhase.");
                EndDraftPhase();
            }
        }
    }

    public void EndDraftPhase()
    {
        Debug.Log("Ending Draft Phase. Preparing for next round...");
        gameManager.GetGameStateManager().HideDraftScreen();

        // For now, go directly back to combat setup
        if (PhotonNetwork.IsMasterClient)
        {
            gameManager.GetPlayerManager().PrepareNextCombatRound();
        }
    }

    public void ProcessPlayerPickedOption(int chosenOptionId, Photon.Realtime.Player sender)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        Debug.Log($"Processing: Player {sender.NickName} ({sender.ActorNumber}) picked Option ID {chosenOptionId}");

        if (gameManager.GetGameStateManager().GetCurrentState() != GameState.Drafting)
        {
            Debug.LogWarning("ProcessPlayerPickedOption received but not in Drafting state.");
            return;
        }

        Dictionary<int, List<string>> currentQueues = null;
        Dictionary<int, int> currentPicks = null;
        List<int> currentOrder = null;

        object queuesObj, picksObj, orderObj;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CardManager.DRAFT_PLAYER_QUEUES_PROP, out queuesObj) || 
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CardManager.DRAFT_PICKS_MADE_PROP, out picksObj) ||
            !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CardManager.DRAFT_ORDER_PROP, out orderObj))
        {
            Debug.LogError("ProcessPlayerPickedOption: Failed to retrieve current draft state from Room Properties.");
            return;
        }

        try
        {
            currentQueues = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>((string)queuesObj) ?? new Dictionary<int, List<string>>();
            currentPicks = JsonConvert.DeserializeObject<Dictionary<int, int>>((string)picksObj) ?? new Dictionary<int, int>();
            currentOrder = JsonConvert.DeserializeObject<List<int>>((string)orderObj) ?? new List<int>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ProcessPlayerPickedOption: Failed to deserialize draft state: {e.Message}");
            return;
        }

        int pickerActorNum = sender.ActorNumber;
        if (!currentQueues.ContainsKey(pickerActorNum) || currentQueues[pickerActorNum] == null || currentQueues[pickerActorNum].Count == 0)
        {
            Debug.LogWarning($"Player {pickerActorNum} sent pick but doesn't have a pack in their queue currently.");
            return;
        }

        List<string> pickerQueue = currentQueues[pickerActorNum];
        string packToProcessJson = pickerQueue[0];
        pickerQueue.RemoveAt(0);

        List<SerializableDraftOption> packToProcess = JsonConvert.DeserializeObject<List<SerializableDraftOption>>(packToProcessJson) ?? new List<SerializableDraftOption>();
        SerializableDraftOption pickedOption = packToProcess.FirstOrDefault(opt => opt.OptionId == chosenOptionId);

        if (pickedOption == null)
        {
            Debug.LogWarning($"Player {pickerActorNum} tried to pick invalid Option ID {chosenOptionId} from their current pack.");
            return;
        }

        packToProcess.Remove(pickedOption);
        Debug.Log($"Removed option {chosenOptionId} from pack for player {pickerActorNum}. Remaining in pack: {packToProcess.Count}. Remaining in queue: {pickerQueue.Count}");

        currentPicks[pickerActorNum] = currentPicks.ContainsKey(pickerActorNum) ? currentPicks[pickerActorNum] + 1 : 1;

        Hashtable propsToUpdate = new Hashtable();

        if (packToProcess.Count > 0)
        {
            int pickerIndex = currentOrder.IndexOf(pickerActorNum);
            if (pickerIndex == -1)
            {
                Debug.LogError($"ProcessPlayerPickedOption: Picker {pickerActorNum} not found in draft order!");
                return;
            }
            int nextPlayerIndex = (pickerIndex + 1) % currentOrder.Count;
            int nextPlayerActorNum = currentOrder[nextPlayerIndex];

            string remainingPackJson = JsonConvert.SerializeObject(packToProcess);
            
            List<string> nextPlayerQueue;
            if (!currentQueues.TryGetValue(nextPlayerActorNum, out nextPlayerQueue) || nextPlayerQueue == null)
            {
                nextPlayerQueue = new List<string>();
                currentQueues[nextPlayerActorNum] = nextPlayerQueue;
            }
            nextPlayerQueue.Add(remainingPackJson);
            
            Debug.Log($"Passing remaining {packToProcess.Count} options to player {nextPlayerActorNum}. Their queue size is now {nextPlayerQueue.Count}");
        }
        else
        {
            Debug.Log($"Pack processed for player {pickerActorNum} is now empty. Not passing.");
        }

        string finalQueuesJson = JsonConvert.SerializeObject(currentQueues);
        string finalPicksJson = JsonConvert.SerializeObject(currentPicks);
        propsToUpdate[CardManager.DRAFT_PLAYER_QUEUES_PROP] = finalQueuesJson;
        propsToUpdate[CardManager.DRAFT_PICKS_MADE_PROP] = finalPicksJson;
        
        Debug.Log($"Master Client setting properties. Queues JSON: {finalQueuesJson}, Picks JSON: {finalPicksJson}");
        PhotonNetwork.CurrentRoom.SetCustomProperties(propsToUpdate);
    }
}