# Project reference

Read [VoxelAgents.md](VoxelAgents.md) for core systems, content authoring workflows, asset locations and validation tools before creating or extending game content. Keep that reference current when changing these integration points.

# Upgrade development

Every new car upgrade must be available in **Tools > Voxel Racer > Upgrade Fit Preview** before the work is considered complete.

- Existing armour, gun and wheel-spike tuning asset types are discovered automatically. Check the new asset appears and mounts correctly on compatible cars.
- For a new upgrade family, implement `IVoxelUpgradeFitProvider` in `Assets/Scripts/Editor` (see `VoxelUpgradeFitCatalog.cs`). Providers are discovered automatically; do not add a hard-coded window list.
- Share mounting/rotation code between gameplay and the preview. Preview builders may modify only the supplied temporary car; never change purchases, cash, damage, source prefabs or tuning assets.
- Supply meaningful per-side/per-slot choices and compatibility checks. Verify individual and combined fit, both sides, and the body-hidden/upgrades-only views.
- Preserve existing shop, integrity, damage and run-persistence behaviour when extracting shared mounting helpers.
