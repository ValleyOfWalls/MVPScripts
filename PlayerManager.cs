using UnityEngine;
using FishNet.Connection;
using System.Collections.Generic;

public class PlayerManager
{
    private GameManager gameManager;
    private int startingPlayerHealth;
    private int startingPetHealth;
    private int startingEnergy;
    
    private NetworkPlayer localNetworkPlayer;
    private StatusEffectManager statusEffectManager;
    
    public PlayerManager(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize(int startingPlayerHealth, int startingPetHealth, int startingEnergy)
    {
        this.startingPlayerHealth = startingPlayerHealth;
        this.startingPetHealth = startingPetHealth;
        this.startingEnergy = startingEnergy;
    }
    
    public NetworkPlayer GetLocalNetworkPlayer()
    {
        if (localNetworkPlayer != null)
            return localNetworkPlayer;
            
        foreach (NetworkPlayer player in NetworkPlayer.Players)
        {
            if (player.IsOwner)
            {
                localNetworkPlayer = player;
                return player;
            }
        }
        
        return null;
    }
    
    public void ToggleLocalPlayerReady()
    {
        NetworkPlayer player = GetLocalNetworkPlayer();
        if (player != null)
        {
            player.ToggleReady();
        }
    }
    
    public bool AreAllPlayersReady()
    {
        if (NetworkPlayer.Players.Count == 0)
            return false;
            
        foreach (NetworkPlayer player in NetworkPlayer.Players)
        {
            if (!player.GetIsReady())
                return false;
        }
        
        return true;
    }
    
    public StatusEffectManager GetStatusEffectManager()
    {
        if (statusEffectManager == null)
            statusEffectManager = new StatusEffectManager();
            
        return statusEffectManager;
    }
    
    // Methods to be added later for gameplay functionality
    public void ResetHealth()
    {
        // Reset player health
    }
    
    public void TakeDamage(int amount)
    {
        // Handle player taking damage
    }
    
    public void HealPlayer(int amount)
    {
        // Handle player healing
    }
    
    public void AddBlock(int amount)
    {
        // Add block to player
    }
    
    public int GetCurrentHealth()
    {
        return startingPlayerHealth; // Placeholder
    }
    
    public int GetMaxHealth()
    {
        return startingPlayerHealth;
    }
    
    public int GetPetHealth()
    {
        return startingPetHealth; // Placeholder
    }
    
    public int GetMaxPetHealth()
    {
        return startingPetHealth;
    }
    
    public int GetEnergy()
    {
        return startingEnergy; // Placeholder
    }
    
    public int GetMaxEnergy()
    {
        return startingEnergy;
    }
} 