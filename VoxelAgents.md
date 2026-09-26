# Voxel Racer — content and systems reference

Last reviewed: 2026-09-26. Paths below are relative to the repository root.

This is a navigation and content-authoring guide, not a replacement for the current source or assets. Read `AGENTS.md` first. The user frequently changes tuning values in Unity: inspect the current asset before editing, and preserve unrelated values. Update this guide when adding a content family or changing a core workflow.

## Start here

- Unity version: `ProjectSettings/ProjectVersion.txt` (currently 6000.3.22f1). Rendering uses URP.
- Runtime scripts: `Assets/Scripts/`; editor builders, inspectors, previews and validation: `Assets/Scripts/Editor/`.
- Tunings/catalogues: `Assets/Resources/`. Authored models: `Assets/Prefabs/`, with boss assets also under `Assets/Resources/Bosses/`.
- Scenes in build settings: `Assets/Scenes/MainMenu.unity`, `CarSelect.unity`, `SampleScene.unity` (race), `RepairUpgrade.unity`.
- Race assembly starts in `VoxelRacerBootstrap.cs`. Many game objects and UI elements are created at runtime; a scene hierarchy alone is not the full implementation.
- Follow the existing blocky/voxel vehicle style, hollow body construction and mobile performance constraints.
- Retain `.meta` files/GUIDs. Prefer updating an existing prefab/asset over deleting and recreating it.
- Inspector convention: **vertical, independently expandable Odin foldouts**, with units, min/max pairs and conditional fields. Avoid horizontal tabs.
- DOTween is installed. Prefer it selectively for new presentation/UI work; existing gameplay movement and camera shake have their own controllers. Do not animate a transform concurrently with a controller that owns it.

## Quick routing table

| Task | Read first | Asset or integration point |
|---|---|---|
| Player handling, lanes, damage | `VoxelCarController.cs`, `VoxelCarTuning.cs` | Selected `VoxelCarDefinition.tuning`; global fallback `Assets/Resources/VoxelCarTuning.asset` |
| New playable car | `VoxelCarDefinition.cs`, `VoxelCarSelectionState.cs` | `Assets/Resources/Cars/`, `Assets/Prefabs/Cars/` |
| New upgrade | Matching `*Tuning.cs` and `*UpgradeState.cs` | Shop controller, run-state reset/apply, fit provider; recipe below |
| Track/campaign | `VoxelTrackDefinition.cs`, `VoxelTrackSequence.cs`, `VoxelTrackProgressState.cs` | `Assets/Resources/Tracks/`, `Assets/Resources/VoxelTrackSequence.asset` |
| Road geometry | `EndlessVoxelRoad.cs`, `VoxelRoadTuning.cs`, `VoxelTrackPose.cs` | Track's road tuning |
| Civilian/enemy spawning | `VoxelObstacleSpawner.cs`, `VoxelObstacleCarTuning.cs` | Track's traffic tuning; explicit enemy selection in spawner |
| Civilian model/paint | `VoxelObstacleCar.cs`, `VoxelTrafficPaint.cs` | `TrafficCarTuning.asset`, `CivilianVanTuning.asset` under `Resources/EnemyVehicles` |
| Enemy behaviour/damage | `VoxelEnemyCar.cs`, `VoxelEnemyVehicleTuning.cs` | `Resources/EnemyVehicles/BlackInterceptorTuning.asset`, `BI_MineLayerTuning.asset` |
| Boss | `VoxelBossEncounter.cs`, `VoxelBossDefinition.cs`, `VoxelBossAttackDefinition.cs`, `VoxelBossEncounterSettings.cs` | `Resources/Bosses/RedVanBoss.asset`, attack tunings under `Resources/Bosses/`, track reference in `Resources/Tracks/TrackBoss01.asset` |
| Mission points/multiplier | `VoxelMissionProgress.cs`, `VoxelMissionTuning.cs` | Track's mission tuning; enemy destruction score is on enemy tuning |
| Crate rewards | `VoxelDestructionRewards.cs` | Shared `Assets/Resources/DestructionRewards.asset` |
| Garage/UI | `VoxelRepairUpgradeSceneController.cs`, `VoxelGarageUi.cs` | `RepairUpgrade` scene and `Resources/RepairUpgradeTuning.asset` |
| Scenery/environment | `VoxelScenerySet.cs`, `VoxelDistantScenery.cs`, `VoxelHorizonMountains.cs` | Track environment + `Resources/Scenery/DesertScenerySet.asset` |
| Camera | `VoxelCameraFollow.cs`, `VoxelCameraTuning.cs` | `Resources/VoxelCameraTuning.asset` |
| Inspectors | `Editor/VoxelOdinTuningLayout.cs`, `VoxelContentOdinLayout.cs`, `VoxelTrackOdinLayout.cs` | Editor-only attribute processors and custom editors |

Unless a directory is included, script names in this document refer to `Assets/Scripts/`.

## Player cars, damage and persistence

The primary player model is `Assets/Prefabs/Cars/SpyCar2PlayerCar.prefab`; its catalogue entry is `Assets/Resources/Cars/SpyCar2PlayerCar.asset`.

`VoxelCarSelectionState.LoadDefinitions()` discovers available `VoxelCarDefinition` assets from `Resources/Cars`, sorted by `selectionOrder`. Selection is stored by asset name in PlayerPrefs (`VoxelRacer.SelectedCar`). Without a matching saved selection, the first available entry is used. Renaming catalogue assets can therefore affect saved selection.

A car definition connects display name, selection availability/order, visual prefab, preview image and handling tuning. Player behaviour is **not all in VoxelCarTuning**: boost, weapons, upgrades, camera, mission rules and repair presentation have separate settings.

- `VoxelCarRunState`: damage and upgrade continuity between race and garage; `BeginNewRun` resets each upgrade family. Preserve hierarchy conventions used when capturing/restoring damage.
- `VoxelCurrencyState`: shared currency. Purchase through the upgrade state API, not by creating only a visual model.
- `VoxelCarController`: integrity, damage, lane movement, boost/ram offsets and effective driving stats.
- `VoxelIndestructiblePart`: marker on a part or ancestor excludes it from relevant destructible renderer scans. Keep structural chassis, engine and supports beneath this marker.
- `VoxelArmorVoxel`: armour hit points; `VoxelWheelIntegrity`: wheel damage constraints. Do not bypass these when adding damage sources.
- Armour voxels add to overall integrity; extra armour hit points do not simply become extra voxel counts. Left and right door armour are separate purchases.
- Prefer `CollisionTrackPosition` for vehicle interaction: raw `TrackDistance` alone misses player boost/ram offsets.
- `VoxelVehicleCollision.Sweep` handles fast relative motion. Preserve swept checks so boosting/reversing does not tunnel through targets.
- New damage sources should use `ApplyDamage` and a meaningful source label so mission breakdown and damage effects remain connected.
- Enemy health and per-voxel health are separate. Indestructible visuals must not keep a fully stripped boss alive; see `CheckBossBodyDestroyed`.

### New playable car recipe

1. Author a hollow visual prefab; keep +Z forward and use existing vehicle scale/conventions as the baseline.
2. Add structural parts beneath `VoxelIndestructiblePart`; avoid adding individual physics colliders to every voxel.
3. Create a `VoxelCarDefinition` under `Resources/Cars`, assign its prefab and handling tuning, and set availability/order.
4. Check selection, race creation, garage preview, integrity, damage restoration and all compatible upgrades.
5. Menu featured cars are a separate selection in `Resources/MainMenuTuning.asset`; adding a car catalogue entry does not automatically feature it there.

## Upgrade authoring — required workflow

**Every new car upgrade must appear and fit correctly in Tools > Voxel Racer > Upgrade Fit Preview.** This is a completion requirement in `AGENTS.md`.

| Family | Main tuning / state | Existing assets |
|---|---|---|
| Door armour | `VoxelArmorTuning`, `VoxelArmorUpgradeState`, `VoxelArmorVoxel` | `Resources/Armor/DoorArmorTuning.asset`, `Prefabs/Upgrades/DoorArmorPanel.prefab` |
| Guns | `VoxelGunTuning`, `VoxelGunUpgradeState`, `VoxelGunMount` | `Resources/Weapons/BasicHoodGunTuning.asset`, `LongBarrelHoodGunTuning.asset`; `Prefabs/Weapons/` |
| Wheel spikes | `VoxelWheelSpikeTuning`, `VoxelWheelSpikeUpgradeState` | `Resources/Upgrades/WheelSpikeTuning.asset`, `Prefabs/Upgrades/WheelSpike.prefab` |
| Performance wheels | `VoxelPerformanceWheelTuning`, `VoxelPerformanceWheelUpgradeState` | `Resources/Upgrades/PerformanceWheelTuning.asset`, `Prefabs/Upgrades/PerformanceWheel.prefab` |
| Boost bottle | `VoxelBoostUpgradeTuning`, `VoxelBoostUpgradeState` | `Resources/Boost/BoostBottleUpgradeTuning.asset`, `Prefabs/Upgrades/BoostBottle.prefab` |
| Engine | `VoxelEngineUpgradeTuning`, `VoxelEngineUpgradeState` | `Resources/Upgrades/V6EngineUpgradeTuning.asset`, `Prefabs/Upgrades/V6Engine.prefab` |

1. Inspect the closest existing family, its loading path and purchase state. Several families use fixed `Resources.Load` paths or single purchase flags; a new asset does not automatically create another shop option.
2. Create a model prefab and tuning asset with price, compatibility, mounting and performance/damage fields.
3. Use a shared visual mounting helper for gameplay and preview. Preserve current local transforms, wheel pivots, exhaust ownership and gun muzzle positions.
4. Wire purchase button/card, affordability, owned/unavailable state, stats refresh and visual refresh into `VoxelRepairUpgradeSceneController` and relevant partial UI files. The shop currently has explicit cards; it is not the automatic fit catalogue.
5. Integrate run reset in `VoxelCarRunState.BeginNewRun`, and apply the upgrade wherever race/garage cars are constructed. Verify damage preservation during a purchase.
6. For a new family, implement `IVoxelUpgradeFitProvider` in `Assets/Scripts/Editor`. See `VoxelUpgradeFitCatalog.cs` and `VoxelEngineFitProvider.cs`.
7. A provider's `Discover()` returns entries with `Asset`, `Label`, `Fits` and `Build`. `TypeCache` discovers providers; `VoxelUpgradeFitCatalog.Assets<T>()` discovers assets. Do not add a hard-coded preview-window list.
8. Preview `Build` modifies only its supplied temporary car. Never spend currency, mark purchases, change run damage or edit source assets during preview.
9. Provide meaningful per-side/per-slot entries. Check isolated/combined upgrades, both sides, incompatible cars, body-hidden and upgrades-only views.
10. Run the relevant family validation and inspect the actual model from multiple angles.

Engine exhausts belong to the engine. Each tip carries `VoxelEngineExhaustOutlet`; its local **+Z points out of the pipe**. Boost effects must use these outlets, including multiple outlets after an engine upgrade. Do not hard-code a single world-space flame position.

## Tracks, traffic and enemy variants

New tracks must be assigned to the ordered `tracks` array in `Assets/Resources/VoxelTrackSequence.asset`. Creating an asset under `Resources/Tracks` alone does not add it to the campaign. `VoxelTrackProgressState` handles sequence position and advancement; the sequence has a loop option.

`VoxelTrackDefinition` references road, traffic and mission tunings plus environment and optional boss settings. Its custom editor maintains embedded road/traffic subassets and legacy migration. When duplicating tracks, verify which tunings are embedded versus shared; do not unintentionally edit another track through a shared reference.

Road positions use lane offsets and distance along `EndlessVoxelRoad`/`VoxelTrackPose`. World Z is not a substitute on turning roads. Current lane count comes from road tuning; dynamically appearing/disappearing lanes were discussed but are not an established implemented system here.

### Civilians

- `VoxelObstacleCar` owns driving phases, collisions, near misses and civilian damage behaviour.
- `CivilianHatchback.prefab` and `CivilianTransitVan.prefab` live in `Assets/Prefabs/Cars/`.
- `VoxelTrafficPaint` identifies paint; preserve material/colour override handling so bodies vary without recolouring glass, lights or trim.
- Wheels use the `Obstacle Voxel Wheel` transform naming convention for rotation.
- The van replaces the old truck role. Serialized fields such as `semiTrailerSpawnChance` and `semiTrailerEnemyTuning` intentionally retain legacy names; the Inspector labels them as Van. Do not rename serialized fields without migration.
- Spawn clearance and following-speed limiting prevent overlap. `VoxelObstacleSpawner.TryFindCivilianLane` checks longitudinal room; `VoxelObstacleCar` maintains an active-traffic registry and 1.5m following margin in addition to vehicle lengths.
- New larger vehicles need appropriate collision half-width/length and spawn spacing, not only a larger visual scale.

### Ordinary enemies / mine layer

- Model prefab and durability/behaviour live on `VoxelEnemyVehicleTuning`; runtime controller is `VoxelEnemyCar`.
- `destructionScore` is per enemy type. Do not assign all enemy destruction points through mission tuning.
- Spawner selection currently explicitly chooses interceptor or mine layer. A new enemy tuning asset needs spawn-selection/bootstrap wiring; there is no generic weighted enemy catalogue yet.
- `VoxelMineLayerTuning` controls mine prefab, chance/interval, damage and post-drop lane-change chance/delay. `VoxelRoadMine` handles arming, swept contact, explosion and lifetime.
- Use existing builders as examples: `Editor/VoxelEnemySedanBuilder.cs`, `VoxelMineLayerBuilder.cs`, `VoxelCivilianHatchbackBuilder.cs`, `VoxelCivilianVanBuilder.cs`.
- Preserve damage smoke/fire via `VoxelVehicleDamageEffects`. Enemy/civilian defaults start smoke after 25% damage and fire after 50%; player thresholds are 50% health for smoke and 25% health for fire. Inspect live settings before changing them.

## Red Van Boss

Primary assets: `Resources/Bosses/RedTransitBoss.prefab`, `Resources/Tracks/TrackBoss01.asset`, `Resources/Bosses/VanditoBossMineTuning.asset` and `VanditoBossMine.prefab`.

Tracks select a reusable `VoxelBossDefinition` asset through their `boss` reference; assigning one enables the boss encounter. The Track keeps only its `bossEncounter` traffic approach settings. Boss identity, durability, base player ram damage, movement and escape behavior live in the boss definition. Its `attacks` list references independent `VoxelBossAttackDefinition` assets, such as `VoxelBossSpikeAttackTuning` and `VoxelBossMineAttackTuning`. Attack assets hold tuning; runtime attack state remains on the boss controller.

The Vandito definition is `Resources/Bosses/RedVanBoss.asset`. Its spike and mine modules are `RedVanSpikeAttack.asset` and `RedVanMineAttack.asset`. The `VoxelVanditoBossAppearanceBuilder` keeps its window trim black, colors its wheels and side belt red, adds black wheel rivets, and uses hand-set slanted red pixel glyphs on each cargo side, with each letter linked to a damageable cargo voxel. The spike module `dodgeLaneFraction` sets how far the player must move sideways before a spike attempt counts as a dodge; corner overlap beyond that margin is ignored, while closer body contact can still register as a ram. Reuse a boss definition across tracks when its behavior is the same; make another definition or attack tuning asset for intentional variants. Add a new attack behavior as a runtime module and a corresponding `VoxelBossAttackDefinition` tuning asset, then add that asset to the boss definition attack loadout. The track encounter settings configure only the traffic lead-in (Radar Interceptor chance, wave composition/spacing and road-clear delay).
- `VoxelBossEncounter` stages: traffic approach, natural traffic clearance, boss, completion/failure. Traffic is allowed to leave naturally; do not restore the old accelerated cleanup or forced removal.
- Traffic approach ends only when the Radar Interceptor is destroyed. `radarInterceptorSpawnChance` is a 1�100% slider under Boss / Traffic Approach (default 10%). One target is active at a time; a missed/despawned target can reappear. Destruction stops queued/new spawns; surviving traffic clears naturally before boss entry.
- Radar assets: `Prefabs/Cars/RadarInterceptor.prefab`, `Resources/EnemyVehicles/RadarInterceptorTuning.asset`. The model copies Black Interceptor with a roof dish animated by `VoxelRadarDish`. `Editor/VoxelRadarInterceptorBuilder` builds/renders it. It has 5 health and uses `VoxelObstacleCar` with an enemy tuning override for civilian driving and enemy destruction rewards, without civilian damage penalties. Only `VoxelBossEncounter.Configure` enables its spawner path; normal missions never select it.
- Approach HUD says Destroy the Radar Interceptor; boss phase shows configured name and health. Score does not complete a boss mission: defeating the boss does.
- Spawn distance is `Clamp(Max(minimumDistanceAhead, maximumDistanceAhead), 1, WarningDistance * .8)`, added to player collision-track distance. It is not a separate spawn-distance field. With warning 150m the cap is 120m.
- Visual scaling spans roughly two lanes. Lane moves use valid two-lane pairs. Warning/failure distances and catch-up behaviour are configurable. Do not reintroduce ordinary enemy distance culling for the boss.
- Mine tuning supplies model/collision/damage/explosion; Track boss settings override drop timing/chance, arming and lifetime. Without a mine tuning reference, legacy fallback damage settings are used.
- Boss structural mesh, spikes and guides are indestructible. Door/window voxels remain destructible.

### Spike slam

`VoxelEnemyCarSpikeAttack.cs` is a partial of `VoxelEnemyCar`; `VoxelBossSpikeRig` animates rear door hinges and the triple spike slide.

Sequence: Normal → Warning (doors open 180°, spikes extend; mines already disabled) → Braking → Holding on a miss → Retreating → Normal. A spike hit jumps immediately to Retreating. Lane choice stays locked during the attack so dodging is meaningful.

- Closing speed is relative to player speed and permits actual reverse motion during the slam. `spikeAttackClosingSpeed` caps it; `spikeAttackClosingResponse` controls approach response; braking and acceleration control speed changes.
- On a miss, `spikeAttackMissDistance` determines how far back to come (0 = alongside). A timeout still prevents a stuck approach.
- Swept collision detects fast hits. Post-movement body clearance keeps bodies separate even during boost/reverse movement; only spikes may intersect the player.
- One hit per attack, tunable voxel damage, source label Boss spike ram. `VoxelSpikeImpact` adds a large mesh-particle debris burst.
- Spikes retract and doors close together from retreat start. Normal behaviour resumes only once retreat distance is reached and the rig is closed.
- Attack interval resets after retreat ends. Chance rolls and lane-settling may delay the next attack; failed rolls wait another interval.
- Mines stay disabled through all attack stages and resume with a delay after retreat.

Useful incremental builders: `VoxelBossStructureBuilder.UpdatePrefab()`, `VoxelBossSpikeBuilder.UpdatePrefab()`, `VoxelBossEyesBuilder.UpdatePrefab()`. Prefer these for focused edits. The full `VoxelRedVanBossBuilder.Build()` recreates the visual from the civilian van and also touches track/sequence configuration; inspect its side effects before running it.

Rear eyes are red pixel geometry baked into existing glass voxel meshes, with a second material. They follow door damage/rotation without adding damage targets. `VoxelBossEyesBuilder` controls placement; use mesh APIs (`Clear`/`CombineMeshes`) to rebuild existing meshes so rendering updates reliably. Preview: `Temp/VanditoBossAngryEyes.png` after `Render()`.

## Missions, rewards and HUD

- `VoxelMissionProgress` owns points, timer, multiplier and success/failure reporting. Use its `Report*` APIs instead of directly editing counters.
- Enemy voxel destruction multiplier rewards are batched per **10 voxels**. Mission tuning controls the reward amount; do not mistake it for per-voxel reward.
- Civilian damage/destruction have separate penalties. Report an event once; avoid awarding a barrel multiplier again through generic enemy-voxel reporting.
- Time bonus is at risk until successful completion within the timer. Multiplier may fall to zero. Timer extensions cannot revive an expired timer.
- `VoxelDestructionRewards` is a shared asset, not a mission-local list. Eligible prize types are selected equally after the source's prize-chance roll. Cash uses steps of 10; time uses steps of 5. Read the asset for current chances/ranges.
- `VoxelMissionBreakdown` records six groups: progress gained, progress lost, integrity lost, multiplier gained, multiplier lost, crate rewards. Include meaningful source names in new reporting paths.
- `VoxelScorePopup`: damage/score and reward feedback. `VoxelMissionMultiplierHud`: multiplier feedback. Preserve travel-to-HUD animations, signed colours and grouped rewards.
- Mission-end UI: `VoxelPostRaceContinue` and `VoxelPostRaceBreakdown`. The right arrow opens breakdown, left returns; keep shared panel bounds and avoid overlap with integrity/Continue controls.
- Shared health dial: `VoxelCarIntegrityDisplay`. Keep its segmented style and clockwise depletion consistent between screens.

## Presentation, scenery and effects

- Garage: `VoxelGarageEnvironment`, `VoxelGaragePanel`, `VoxelGarageUi`, `VoxelGarageCarRotation`. Player drag/swipe overrides automatic rotation; resume delay is part of that behaviour.
- Turntable: `VoxelCarTurntable`; menu: `VoxelMainMenuController`/`VoxelMainMenuTuning`. Check menu separately after changing the player visual; it is another construction path.
- Keep firing disabled in repair/upgrade screens. Respect gameplay/mission input gating when adding effects or controls.
- Camera shake remains in `VoxelCameraFollow`/`VoxelCameraTuning`. Do not put an unrelated tween directly on the camera transform while follow code writes it every frame.
- `VoxelScenerySet` entries define prefab, weight, scale, footprint, spacing and road clearance; weights are relative selection weights, not percentages.
- `VoxelDistantScenery` extends coverage beyond roadside placement and supports visibility as the camera rotates. Do not implement a permanently empty off-camera region that stays empty at mission end.
- Desert models: `Assets/Prefabs/Scenery/Desert/`; register additions in the relevant scenery set.
- Sky/mountains: `Editor/VoxelPixelSkyBuilder`, `VoxelHorizonMountains`, `VoxelHorizonSun`, Track environment fields. Test menu and race camera sky settings independently.
- Boost prefabs: `Resources/Effects/VoxelBoostExhaustFire.prefab` and `VoxelUpgradedBoostExhaustFire.prefab`; boost tuning lives under `Resources/Boost/`.
- Shared impact/destruction helpers: `VoxelFireEffects`, `VoxelDestructionExplosion`, `VoxelDebris`. Prefer shared materials, property blocks and particle systems over per-particle rigidbodies/material instances.

## Editor validation and efficient verification

Validation helpers live under `Assets/Scripts/Editor`. Read their entry methods and requirements before invoking; many use temporary objects/scenes and expect Edit Mode. Do not run every validation for a small cosmetic edit.

| Change | Useful validation classes |
|---|---|
| Upgrade fit / armour | `VoxelUpgradeFitValidation`, `VoxelArmorValidation` |
| Wheels / engines / boost | `VoxelPerformanceWheelValidation`, `VoxelV6EngineValidation`, `VoxelEngineExhaustValidation`, `VoxelBoostUpgradeValidation`, `VoxelBoostDrivingValidation` |
| Civilian/enemy content | `VoxelCivilianHatchbackValidation`, `VoxelCivilianVanValidation`, `VoxelEnemySedanValidation`, `VoxelEnemyScoreValidation` |
| Traffic / mines | `VoxelTrafficSpacingValidation`, `VoxelMineLayerValidation`, `VoxelMineLayerLaneChangeValidation` |
| Boss | `VoxelBossValidation`, `VoxelBossStructureValidation`, `VoxelBossSpikeValidation` |
| Rewards/UI | `VoxelLiveMultiplierValidation`, `VoxelMissionBreakdownValidation`, `VoxelGarageValidation`, `VoxelPlayerDamageEffectsValidation` |
| Scenery / Inspector | `VoxelDesertSceneryValidation`, `VoxelDistantSceneryValidation`, `VoxelTrackOdinValidation`, `VoxelOdinTuningValidation` |

- For models, render/inspect a preview, then check mounting, hidden-body views and relevant damage/animation behaviour. Existing builder/validation `Render()` methods write into `Temp/`; these are disposable previews, not shipped assets.
- For tuning-only changes, preserve unrelated fields and verify the actual serialized asset, not only class defaults.
- For new Inspector families, update supported types and register an Odin editor. Preserve multi-object editing and Unity serialization. Editor-only attribute processors avoid coupling runtime types to Odin.
- Verify console errors after compilation. State whether checks were editor simulations or a real Play Mode run; do not conflate them.
- Use `git diff` to review scope. Unity may produce serialized formatting changes; do not revert unrelated user edits.

### Unity MCP notes

Discover available Unity tools rather than assuming a prior connection. `Unity_RunCommand` accepts `Code` and `Title`; code must implement an **internal** `CommandScript : IRunCommand` with `Execute(ExecutionResult result)`.

Call compiled editor helpers for complex checks. Dynamic MCP snippets may not resolve Odin editor assemblies directly; putting that work in a compiled editor helper avoids this. Fully qualify `UnityEngine.Mesh` in snippets if a tool namespace shadows `Mesh`.

Unity MCP often temporarily disappears during script recompilation. Wait briefly and retry; if it remains unavailable, ask the user to focus Unity and refresh assets. Do not report prefab generation or Unity validation as complete merely because source files were written.

## Before declaring new content complete

Confirm the asset exists, is referenced by the runtime selection/spawn/shop path, and appears in the relevant preview. Check compatibility, performance, damage/integrity, run persistence, animation/muzzle/exhaust attachment, UI presentation and only the validations relevant to the change. Preserve the user's current tuning and update this guide when the integration contract changes.

Vandito side lettering is built by `VoxelVanditoBossAppearanceBuilder`: pixels are clipped to each side body voxel and combined into its mesh with a second red material, matching the rear-eye approach. Surface positions come from actual panel bounds with a 0.002m offset. Both sides map text left-to-right for outside viewers; do not use floating per-letter renderers or mirrored glyphs. Rebuild with Tools > Voxel Racer > Update Vandito Boss Appearance.

### Boss voxel detail and ram removal

- Vandito now has 1,210 mesh pieces: 1,150 destructible and 60 protected (including 58 mirror-infill and wheel-guard pieces added after subdivision). `Editor/VoxelBossVoxelDetailBuilder` halves each destructible cube along its longest axis at authoring time. The `Boss voxel detail 2x` hierarchy marker prevents repeated subdivision. The full boss builder applies this before eyes and lettering; incremental Tools > Voxel Racer > Update Boss Voxel Detail preserves existing tuning and rebuilds the artwork.
- Rear-eye clipping uses actual glass bounds so each smaller panel carries only its portion of the logo. Side lettering likewise remains baked into individual panels.
- Boss Definition > Identity & Durability > Maximum Voxels Removed Per Ram caps body removal for both side and rear impacts (Vandito: 80). Health damage remains independently controlled by Player Ram Damage; ordinary enemies retain traffic tuning removal counts. The final death explosion still uses its separate debris budget.
- Boss/structure/spike editor validations and model previews passed after subdivision. Target-device frame-time profiling remains necessary before treating the higher count as performance-approved.

- `Editor/VoxelBossBodyFinishingBuilder` adds 6 mirror-front infill voxels and 52 stepped wheel-guard voxels. Tools > Voxel Racer > Update Boss Mirror Infill and Wheel Guards replaces only its own geometry group, with no per-piece colliders. Full boss builds apply it after subdivision and appearance; existing boss tuning is untouched.

- Player bullets also query `VoxelObstacleCar.TryFindProjectileHit` against intact local mesh bounds through the active traffic registry. This covers collider-free Radar Interceptors and civilians, respects each shot segment and compares the hit distance with physics obstructions. The renderer list is cached per car; disabled/destroyed parts and wrecks are excluded. Regression command: Tools > Voxel Racer > Validate Radar Projectile Hits (Edit Mode).

- Radar collision damage to the player uses `RadarInterceptorTuning` > Collision > Player Damage Voxels Min/Max (currently 1�5). `VoxelObstacleCar` honours the enemy-traffic override for this range; ordinary civilians still use track traffic damage. Do not confuse radar vehicle health with damage dealt to the player.
