# CardTargetingSystem Prefab Setup

This prefab is needed for the card targeting and effects system. Follow these steps to create it:

## 1. Create the Prefab

1. Create a new empty GameObject in your scene
2. Name it `CardTargetingSystem`
3. Add these components:
   - `NetworkObject` component
   - `CardTargetingSystem` script

## 2. Configure References (Optional)

You can optionally configure:
- `targetingLine`: Assign a LineRenderer component (will be auto-created if not assigned)
- `targetHighlightPrefab`: A GameObject that will appear when hovering over valid targets
- `validTargetColor`: Color for valid targets (default is green)
- `invalidTargetColor`: Color for invalid targets (default is red)
- `lineWidth`: Width of the targeting line (default is 0.05)

## 3. Create a Prefab

1. Drag the configured GameObject into your Project panel under `Prefabs/Combat/`
2. Save it as `CardTargetingSystemPrefab`

## 4. Assign to CombatManager

1. Open your scene with the CombatManager
2. Select the CombatManager GameObject
3. Assign the CardTargetingSystemPrefab to the `cardTargetingSystemPrefab` field

## Important Notes

- The CardTargetingSystem is created automatically by the CombatManager if it doesn't exist
- Only one instance should exist in your scene at any time (it's a singleton)
- The system works with the new Card effects implementation
- Make sure your cards have valid CardData with appropriate effects and target types 