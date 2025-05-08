using UnityEngine;

/// <summary>
/// ScriptableObject that defines properties of an artifact
/// </summary>
[CreateAssetMenu(fileName = "New Artifact", menuName = "Game/Artifact")]
public class ArtifactData : ScriptableObject
{
    [Header("Basic Info")]
    [SerializeField] private int id;
    [SerializeField] private string artifactName;
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private int cost;

    [Header("Effect Properties")]
    [SerializeField] private ArtifactEffectType effectType;
    [SerializeField] private int effectMagnitude;
    [SerializeField] private ArtifactTriggerType triggerType;

    // Public accessors
    public int Id => id;
    public string ArtifactName => artifactName;
    public string Description => description;
    public Sprite Icon => icon;
    public int Cost => cost;
    public ArtifactEffectType EffectType => effectType;
    public int EffectMagnitude => effectMagnitude;
    public ArtifactTriggerType TriggerType => triggerType;
}

/// <summary>
/// Types of effects an artifact can have
/// </summary>
public enum ArtifactEffectType
{
    None,
    IncreaseDamage,
    IncreaseHealing,
    ReduceDamageTaken,
    MaxHealthBoost,
    MaxEnergyBoost,
    StartTurnEnergyBoost,
    ExtraCardDraw,
    ReduceCardCost,
    ImproveCardDraw
}

/// <summary>
/// When an artifact's effect is triggered
/// </summary>
public enum ArtifactTriggerType
{
    Passive,
    OnCombatStart,
    OnTurnStart,
    OnTurnEnd,
    OnCardPlay,
    OnDamageTaken,
    OnHealing
} 