using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public enum VoxelUpgradeDashboardSort { NameAscending, NameDescending, PriceAscending, PriceDescending, Category }

    /// <summary>One row per tuning, rather than one row per fit slot. Uses the existing extensible fit catalogue.</summary>
    public sealed class VoxelUpgradeDashboardEntry
    {
        public readonly ScriptableObject Asset;
        public readonly VoxelUpgradeFitEntry[] Fits;
        public VoxelUpgradeDashboardEntry(ScriptableObject asset, IEnumerable<VoxelUpgradeFitEntry> fits)
        { Asset = asset; Fits = fits.ToArray(); }

        public ScriptableObject PriceAsset => Asset is VoxelMissileLauncherTuning missile ? missile.weapon : Asset;
        public string PriceField => Asset is VoxelArmorTuning ? "panelPurchasePrice" : "purchasePrice";
        public string Name
        {
            get
            {
                if (PriceAsset == null) return Asset.name;
                using var serialized = new SerializedObject(PriceAsset);
                return serialized.FindProperty("displayName")?.stringValue ?? Asset.name;
            }
        }
        public int? Price
        {
            get
            {
                if (PriceAsset == null) return null;
                using var serialized = new SerializedObject(PriceAsset);
                var price = serialized.FindProperty(PriceField);
                return price != null && price.propertyType == SerializedPropertyType.Integer ? price.intValue : null;
            }
        }
        public bool SetPrice(int value)
        {
            if (PriceAsset == null) return false;
            using var serialized = new SerializedObject(PriceAsset);
            var price = serialized.FindProperty(PriceField);
            if (price == null || price.propertyType != SerializedPropertyType.Integer) return false;
            price.intValue = Mathf.Max(0, value);
            return serialized.ApplyModifiedProperties();
        }
        public string Category => Asset switch
        {
            VoxelArmorTuning => "Armour",
            VoxelGunTuning => "Guns",
            VoxelMissileLauncherTuning => "Missiles",
            VoxelBoostUpgradeTuning => "Boost",
            VoxelEngineUpgradeTuning => "Engines",
            VoxelPerformanceWheelTuning or VoxelWheelSpikeTuning => "Wheels",
            VoxelPloughTuning => "Ploughs",
            _ => ObjectNames.NicifyVariableName(Asset.GetType().Name.Replace("Voxel", "").Replace("Tuning", ""))
        };
        public string PriceUnit => Asset switch
        {
            VoxelArmorTuning => "per door panel",
            VoxelGunTuning => "per gun",
            VoxelMissileLauncherTuning => "per launcher",
            VoxelPerformanceWheelTuning or VoxelWheelSpikeTuning => "set of four",
            _ => "per upgrade"
        };
        private static string N(float value) => value.ToString("0.##");
        private static string Weapon(VoxelGunTuning gun)
        {
            if (gun == null) return "Assign a weapon tuning to configure damage, firing and price.";
            string firing = gun.projectileKind == VoxelProjectileKind.Missile
                ? N(gun.SecondsPerShot) + "s between shots"
                : N(gun.shotsPerSecond) + " shots/s";
            return N(gun.damagePerBullet) + " damage/projectile; " + firing + "; " + gun.bulletsPerShot +
                " projectile(s)/shot; " + N(gun.maximumRange) + "m range. " +
                (gun.areaOfEffectRadius > 0 ? N(gun.areaOfEffectRadius) + "m blast radius. " : "") +
                (gun.ammunitionPerStage == 0 ? "Unlimited ammo." : gun.ammunitionPerStage + " ammo/stage.");
        }
        public string Description => Asset switch
        {
            VoxelArmorTuning armor => "Protects each door with " + armor.voxelHitPoints +
                " HP per armour voxel. Each panel is purchased separately.",
            VoxelGunTuning gun => Weapon(gun),
            VoxelMissileLauncherTuning missile => "Independent left/right roof launchers. " + Weapon(missile.weapon),
            VoxelBoostUpgradeTuning boost => boost.bottleCount + " independently recharging boost charge(s); +" +
                N(boost.boostSpeed) + "m/s for " + N(boost.boostLength) + "s; " + N(boost.rechargeCooldownLength) +
                "s recharge; " + N(boost.boostAccelerationMultiplier) + "x acceleration. Ram damage +" +
                N(boost.boostRamDamageBonusPercent) + "% (with plough: +" + N(boost.boostRamDamageWithPloughBonusPercent) + "%).",
            VoxelEngineUpgradeTuning engine => "+" + N(engine.topSpeedBonusPercent) + "% top speed; +" +
                N(engine.accelerationBonusPercent) + "% acceleration. Includes engine, exhausts and rear spoiler.",
            VoxelPerformanceWheelTuning wheels => "+" + N(wheels.accelerationBonusPercent) + "% acceleration; +" +
                N(wheels.laneChangeBonusPercent) + "% lane-change speed; +" + N(wheels.brakingBonusPercent) + "% braking deceleration.",
            VoxelWheelSpikeTuning spikes => "+" + N(spikes.sideRamDamageBonusPercent) + "% side-ram damage. Level " +
                spikes.upgradeLevel + ": replaces lower levels; bonuses do not stack.",
            VoxelPloughTuning plough => "+" + N(plough.impactDamageBonusPercent) + "% front-ram damage; " +
                N(plough.playerDamageReductionPercent) + "% less player damage on protected front impacts.",
            _ => "Select this upgrade to inspect and edit its tuning values."
        };

        public bool Matches(string search) => string.IsNullOrWhiteSpace(search) ||
            (Name + " " + Category + " " + Description + " " + Asset.name)
            .IndexOf(search.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static class VoxelUpgradeDashboardCatalog
    {
        public static string[] Categories(IEnumerable<VoxelUpgradeDashboardEntry> entries) => entries
            .Select(e => e.Category).Distinct().OrderBy(c => c).ToArray();

        public static IEnumerable<VoxelUpgradeDashboardEntry> Sort(IEnumerable<VoxelUpgradeDashboardEntry> entries,
            VoxelUpgradeDashboardSort order)
        {
            var names = System.StringComparer.OrdinalIgnoreCase;
            var categories = order == VoxelUpgradeDashboardSort.Category ? Categories(entries) : null;
            var sorted = order switch
            {
                VoxelUpgradeDashboardSort.Category => entries.OrderBy(e => System.Array.IndexOf(categories, e.Category))
                    .ThenBy(e => e.Name, names),
                VoxelUpgradeDashboardSort.NameDescending => entries.OrderByDescending(e => e.Name, names),
                VoxelUpgradeDashboardSort.PriceAscending => entries.OrderBy(e => !e.Price.HasValue)
                    .ThenBy(e => e.Price).ThenBy(e => e.Name, names),
                VoxelUpgradeDashboardSort.PriceDescending => entries.OrderBy(e => !e.Price.HasValue)
                    .ThenByDescending(e => e.Price).ThenBy(e => e.Name, names),
                _ => entries.OrderBy(e => e.Name, names)
            };
            return sorted.ThenBy(e => AssetDatabase.GetAssetPath(e.Asset), System.StringComparer.Ordinal);
        }

        public static List<VoxelUpgradeDashboardEntry> Discover() => VoxelUpgradeFitCatalog.Discover()
            .Where(e => e.Asset != null).GroupBy(e => e.Asset)
            .Select(g => new VoxelUpgradeDashboardEntry(g.Key, g))
            .OrderBy(e => e.Category).ThenBy(e => e.Name).ToList();
    }
}
