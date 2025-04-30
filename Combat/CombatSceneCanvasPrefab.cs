using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Combat
{
    // This script provides guidance on setting up the CombatSceneCanvas prefab in the Unity editor
    public class CombatSceneCanvasPrefab : MonoBehaviour
    {
        // This script shouldn't be included in the actual prefab
        // It's just a reference for setting up the hierarchy in the Unity Editor
        
        /* Recommended Hierarchy:
         * 
         * CombatSceneCanvas (GameObject with CombatSceneCanvas.cs component)
         * └── Canvas (Canvas component with CanvasScaler, GraphicRaycaster)
         *     ├── BattleInfoPanel
         *     │   ├── BattleInfoText (TextMeshProUGUI - shows "{player} vs {opponent}'s Pet")
         *     │   └── NextBattleButton (Button)
         *     │       └── ButtonText (TextMeshProUGUI - "View Next Battle")
         *     ├── BattleViewContainer (Contains combat view elements)
         *     │   ├── PlayerArea (For player cards, stats, etc.)
         *     │   ├── PetArea (For opponent pet)
         *     │   └── BattlefieldArea (Middle area where combat animations happen)
         *     └── EndTurnButton (Button - Only visible for player's own combat)
         *
         * Setup Instructions:
         * 1. Create a new GameObject and name it "CombatSceneCanvas"
         * 2. Add the CombatSceneCanvas.cs script to this GameObject
         * 3. Create a Canvas as a child and set up the hierarchy as shown above
         * 4. In the CombatSceneCanvas component inspector, assign:
         *    - canvasRoot = Canvas GameObject
         *    - battleInfoText = BattleInfoText component
         *    - nextBattleButton = NextBattleButton component
         *    - battleViewContainer = BattleViewContainer GameObject
         * 5. Create a prefab from this GameObject
         * 6. Assign the prefab to the combatSceneCanvasPrefab field in CombatManager
         */
        
        private void Awake()
        {
            // Display a warning and destroy this script
            Debug.LogWarning("CombatSceneCanvasPrefab is only for editor reference. Remove this script from actual prefabs.");
            Destroy(this);
        }
    }
} 