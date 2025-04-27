using UnityEngine;
using System;
using Steamworks;

public class SteamManager : MonoBehaviour
{
    private static SteamManager s_instance;
    public static SteamManager Instance { get { return s_instance; } }

    [SerializeField] private bool initializeOnAwake = true;
    [SerializeField] private uint appId = 480; // Default to Spacewar (Valve's example app)

    private bool m_initialized = false;
    public bool Initialized { get { return m_initialized; } }

    private void Awake()
    {
        // Only one instance of SteamManager at a time
        if (s_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;
        DontDestroyOnLoad(gameObject);

        if (initializeOnAwake)
        {
            try
            {
                // Check if the game needs to be restarted via Steam - important for retail builds
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
                {
                    Debug.Log("Restarting app via Steam...");
                    Application.Quit();
                    return;
                }

                Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error initializing Steam: {e.Message}");
            }
        }
    }

    public bool Initialize()
    {
        if (m_initialized)
            return true;

        try
        {
            // If Steam is not running or the app can't connect to Steam, SteamAPI_Init will return false
            m_initialized = SteamAPI.Init();
            if (!m_initialized)
            {
                Debug.LogWarning("SteamAPI_Init failed. Steam is not running or you don't own the app.");
                return false;
            }
        }
        catch (DllNotFoundException e)
        {
            Debug.LogError($"Steam DLL not found: {e.Message}");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Steam initialization error: {e.Message}");
            return false;
        }

         Debug.Log("Steam Initialized Successfully!");
        return true;
    }

    private void OnApplicationQuit()
    {
        if (m_initialized)
        {
             Debug.Log("Shutting down Steam API...");
            SteamAPI.Shutdown();
            m_initialized = false;
        }
    }

    private void Update()
    {
        if (m_initialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    // Get the current user's SteamID
    public ulong GetSteamID()
    {
        if (!m_initialized)
            return 0;
            
        return SteamUser.GetSteamID().m_SteamID;
    }

    // Get the current user's name
    public string GetPlayerName()
    {
        if (!m_initialized)
            return "Player"; // Return default if not initialized
            
        return SteamFriends.GetPersonaName();
    }
} 