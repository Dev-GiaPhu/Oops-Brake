# Unity Project Context

<!-- unity-onboarding:generated:start -->
Last analyzed: 2026-08-05 at commit `8d17457` (feature branch `codex/endless-emergency-racer`).

## Project summary

- Confirmed Unity project root: `D:/FPT Polytechnic/github/Racing 3D Polygon`.
- Unity 6000.0.70f1; Universal Render Pipeline 17.0.4.
- Input System 1.19.0 and uGUI 2.0.0 are installed. Gameplay uses keyboard A/D, Space, and Escape through the Input System.
- Single-player, MonoBehaviour-centric arcade endless driving game. No networking usage was found.

## Assets and startup

- Primary three-lane straight module: `Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Roads/Road_1.prefab`.
- Decorative intersections: `Road_Crossroads_*` in the same road-prefab directory.
- Playable garage assets: seven prefabs under `Prefabs/Vehicles/Emergency_Vehicles`.
- `Menu` is build index 0 and `Game` is build index 1. Both scenes contain serialized Edit Mode authoring hierarchies with cameras, lighting, vehicle/road/city previews and UI roots.
- `EmergencyRoadProjectBuilder` discovers imported prefabs and generates the catalog plus authored scenes. `EmergencyRoadBootstrap` subscribes to `SceneManager.sceneLoaded` so every menu/game reload composes its runtime controller reliably.

## Architecture and persistence

- `EmergencyRoadMenu`: garage, unlock/select flow, settings, controls and scene entry.
- `EmergencyRoadGame`: world recycling, score, coins, obstacles, cross traffic, pause and game-over flow.
- Environment authoring measures `Road_1` bounds (`roadLength`/`roadHalfWidth`) and builds seamless grass, sidewalk lighting, tree belts, set-back buildings and full horizontal cross streets. URP Bloom/ACES/color grading and linear distance fog hide streaming edges.
- `MotorRushDirector`: curved tracking warning path followed by a smooth diagonal/weaving motorcycle rush; player impact ends the run while obstacle impact disables collision and plays a temporary tumble/burst.
- `EmergencyVehicleController`: lane input and funny deformation feedback.
- `EmergencyRoadProfile`: versioned JSON PlayerPrefs profile; owns coins, unlocks, selection, high score and audio volumes.
- `EmergencyRoadAudio`: persistent lightweight music/SFX service with generated fallback tones because no audio assets were found.

## Validation and tooling

- Unity Test Framework 1.6.0 is installed; no project tests existed during onboarding.
- Unity Editor is running locally. No callable Unity MCP provider was available in this Codex session; compilation/Console evidence is read from the Editor log.
- The imported city pack is currently untracked in Git and is treated as user-provided content.

## Important constraints and unknowns

- Desktop keyboard is the current target; mobile touch input and gamepad navigation are not implemented.
- Commercial release still requires confirmation that the imported asset pack's license permits distribution.
- Target platform/build artifact and performance budgets were not specified.
<!-- unity-onboarding:generated:end -->
