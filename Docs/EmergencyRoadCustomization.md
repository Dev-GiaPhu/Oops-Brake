# Emergency Road — Editor Customization Guide

## Main authoring assets

- `Assets/Scenes/Menu.unity`: garage, settings and controls preview.
- `Assets/Scenes/Game.unity`: complete road/city/gameplay authoring preview.
- `Assets/EmergencyRoad/Resources/EmergencyRoadCatalog.asset`: central replaceable asset catalog.
- `Assets/EmergencyRoad/Resources/EmergencyRoadPostFX.asset`: Bloom, ACES, color grade, exposure and vignette.
- `Assets/EmergencyRoad/Resources/GrassGround.mat`: grass color and surface response.
- `Assets/EmergencyRoad/Resources/SoilGround.mat`: narrow soil-border material.
- `Assets/EmergencyRoad/Resources/EmergencyRoadVFX.mat`: shared URP particle material used by road dust and motor impact.

## Prefabs you can replace

Open `EmergencyRoadCatalog.asset` in the Inspector:

- `Player Vehicles`: garage/player models. Index 0 is the free default vehicle.
- `Traffic Vehicles`: crossroad traffic models.
- `Road Prefabs`: keep the first entry as the straight three-lane module (`Road_1`).
- `Crossroad Prefabs`: decorative intersection modules.
- `Obstacle Prefabs`: boxes, cones, trash and barriers. Runtime normalizes each prefab by type; the gameplay hitbox stays at 86% of a lane while small props keep believable visual proportions.
- `Decoration Prefabs`: buildings. Runtime places the nearest building edge outside the measured road bound plus setback.
- `Nature Prefabs`: roadside trees.
- `Street Decoration Prefabs`: lights, hydrants, fences and signs.

After changing prefab lists, run `Tools > Emergency Road > Build Game` to regenerate both authored scenes.

## Exact layout controls

Select `GAME SCENE AUTHORING` in `Game.unity`:

- `Lane Width`: lane-center spacing used by player, coins, obstacles and traffic.
- `Chunk Spacing`: generated from the measured `Road_1` renderer length.
- `Preview Root`: visual Edit Mode hierarchy. Runtime hides this preview and composes the endless version from the same catalog/settings.

Measured values are stored in the catalog:

- `Road Length`: exact renderer length with a tiny seam overlap.
- `Road Half Width`: used to order sidewalk lights, grass/tree zone and building setback.

## Audio replacement

The catalog exposes optional slots:

- `Music Clip`
- `UI Click Clip`
- `Coin Clip`
- `Horn Clip`
- `Crash Clip`

Drag imported `.wav` or `.ogg` clips into these fields. Empty fields use generated fallback audio. Volume is controlled by Menu Settings and saved in the player profile.

## VFX and rendering

- Edit `EmergencyRoadPostFX.asset` for bloom/exposure/color grading.
- Edit `EmergencyRoadVFX.mat` to change dust and explosion rendering. Keep a URP particle-compatible shader to avoid pink particles.
- Sun, fog and camera HDR/FXAA are configured in `EmergencyRoadGame.BuildLightingAndCamera`.
- Motor warning color/width/timing and tracking are in `MotorRushDirector`.
- Motor speed, delayed tracking, weave and final lock distance are in `MotorRushHazard`.
- Crash deformation is `EmergencyVehicleController.Crumple`; crash camera shake and `Crash Clip` are triggered together.

## Weather, rain and global wetness

Open `Game.unity` and select `WEATHER SYSTEM - SCENE AUTHORED`. The whole weather setup is visible and editable in the Hierarchy:

- `Rain VFX - EDIT PARTICLE HERE`: authored Particle System for drop count, size, wind noise and color.
- `Lightning Flash Light - EDIT HERE`: the directional flash light used by lightning.
- `Rain Loop Audio - Drop Clip Here`: 2D looping rain AudioSource.
- `Thunder One Shot Audio - Drop Clips On Parent`: 2D thunder AudioSource.
- On the parent `EmergencyWeatherSystem`, drag a loop into `Rain Loop` and one or more clips into `Thunder Clips`.
- `Day Rain Chance` and `Night Rain Chance` control random weather separately for day/night.
- `Clear Duration Range`, `Rain Duration Range` and `Transition Duration` control scheduling and smooth fades.
- `Puddle Amount`, `Wet Darkening`, `Puddle World Scale` and `Ripple Strength` control the procedural wet look.

`EmergencyGlobalWetness.mat` is a URP full-screen material. It reconstructs world position and normals so roads, vehicles, buildings and props become wet without replacing their individual materials. Horizontal surfaces receive procedural puddles and animated rain rings. The renderer pass automatically skips clear/dry frames, so clear weather has no full-screen/depth-normal cost.

Puddle noise uses `EmergencyRoadGame.Distance` as its road-space offset. Because gameplay simulates forward travel by moving chunks backward, this compensating offset keeps puddles and ripples fixed to each road chunk instead of sliding across its mesh. The offset stops automatically on pause and game over with the gameplay distance.

If a renderer or scene reference is removed, run `Tools > Emergency Road > Weather > Install Or Repair Weather System`. This repairs both PC/mobile renderer features and missing scene components without replacing custom particle or audio settings that are still assigned.

## Gameplay tuning

In `EmergencyRoadGame.cs`:

- `StartSpeed` / `MaxSpeed`: speed range.
- `VehicleLength`: safety-spacing basis.
- `ShouldSpawnObstacle`: obstacle gap grows from 11.5m to 17.5m as speed rises, with a two-vehicle-length hard minimum.
- `SpawnGameplay`: every row blocks one or two lanes, always leaves one or two valid lanes, and guides the player with coins.
- `SpawnObstacle`: weighted obstacle mix (68% stopped traffic vehicles, 24% barriers, 8% cones/boxes/trash and other props).
- `FitObstacleToLane`: per-type visual normalization and the 86%-lane collision footprint.

`SIDE COLLISION` can only be changed in Menu Settings. When enabled, an attempted A/D lane change into an obstacle beside the player produces the same bump response as pressing beyond the road edge.

## Folder layout

- `Editor/`: scene/catalog generation only.
- `Scripts/`: runtime source kept in place to preserve existing Unity GUID references.
- `Resources/`: runtime-loaded catalog, materials and post-processing profile.
- `Audio/`: recommended drop location for replacement clips.
- `Prefabs/`: recommended location for game-owned prefab variants.
- `Materials/`: recommended location for additional game-owned materials.
- `VFX/`: recommended location for particle prefabs or textures.

Do not edit generated scene YAML manually. Use the Inspector and rebuild command.
