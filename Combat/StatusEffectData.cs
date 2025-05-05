using UnityEngine;

namespace Combat
{
    // Struct to represent status effect data
    [System.Serializable]
    public struct StatusEffectData
    {
        public int Value;    // Magnitude of the effect
        public int Duration; // Duration in turns
    }
} 