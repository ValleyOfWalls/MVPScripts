using UnityEngine;
// using UnityEngine.UI; // Slider is no longer used
using TMPro; // For TextMeshProUGUI
using FishNet.Object; // For NetworkBehaviour

[RequireComponent(typeof(NetworkPet))]
public class PetStatDisplay : MonoBehaviour
{
    [Header("UI References (Assign in Prefab)")]
    [SerializeField] private TextMeshProUGUI petNameText;
    [SerializeField] private TextMeshProUGUI petHealthText;
    // [SerializeField] private Slider petHealthSlider; // Removed
    [SerializeField] private TextMeshProUGUI petEnergyText;
    // [SerializeField] private Slider petEnergySlider; // Removed

    private NetworkPet networkPet;

    private void Awake()
    {
        networkPet = GetComponent<NetworkPet>();
        if (networkPet == null)
        {
            Debug.LogError("PetStatDisplay: NetworkPet component not found on this GameObject.", this);
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        if (networkPet.IsClientInitialized)
        {
            SubscribeToChanges();
            InitialUISetup();
        }
        else
        {
            SubscribeToChanges();
            InitialUISetup();
        }
    }
    
    private void SubscribeToChanges()
    {
        networkPet.PetName.OnChange += OnPetNameChanged;
        networkPet.CurrentHealth.OnChange += OnCurrentHealthChanged;
        networkPet.MaxHealth.OnChange += OnMaxHealthChanged;
        networkPet.CurrentEnergy.OnChange += OnCurrentEnergyChanged;
        networkPet.MaxEnergy.OnChange += OnMaxEnergyChanged;
    }

    private void OnDestroy()
    {
        if (networkPet == null) return;

        networkPet.PetName.OnChange -= OnPetNameChanged;
        networkPet.CurrentHealth.OnChange -= OnCurrentHealthChanged;
        networkPet.MaxHealth.OnChange -= OnMaxHealthChanged;
        networkPet.CurrentEnergy.OnChange -= OnCurrentEnergyChanged;
        networkPet.MaxEnergy.OnChange -= OnMaxEnergyChanged;
    }

    private void InitialUISetup()
    {
        OnPetNameChanged(networkPet.PetName.Value, networkPet.PetName.Value, false);
        UpdateHealthUI();
        UpdateEnergyUI();
    }

    private void OnPetNameChanged(string prev, string next, bool asServer)
    {
        if (petNameText != null)
        {
            petNameText.text = next;
        }
    }

    private void OnCurrentHealthChanged(int prev, int next, bool asServer)
    {
        UpdateHealthUI();
    }

    private void OnMaxHealthChanged(int prev, int next, bool asServer)
    {
        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        if (networkPet == null) return;

        int current = networkPet.CurrentHealth.Value;
        int max = networkPet.MaxHealth.Value;

        if (petHealthText != null)
        {
            petHealthText.text = $"HP: {current} / {max}";
        }
        // Slider logic removed
    }

    private void OnCurrentEnergyChanged(int prev, int next, bool asServer)
    {
        UpdateEnergyUI();
    }

    private void OnMaxEnergyChanged(int prev, int next, bool asServer)
    {
        UpdateEnergyUI();
    }

    private void UpdateEnergyUI()
    {
        if (networkPet == null) return;

        int current = networkPet.CurrentEnergy.Value;
        int max = networkPet.MaxEnergy.Value;

        if (petEnergyText != null)
        {
            petEnergyText.text = $"EN: {current} / {max}";
        }
        // Slider logic removed
    }
} 