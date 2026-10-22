using UnityEngine;

namespace VoxelRacer
{
    // The original asset remains available as a migration reference. Gameplay reads
    // the active mission's independent entries, never the shared resource asset.
    [CreateAssetMenu(menuName = "Voxel Racer/Legacy/Destruction Rewards", fileName = "DestructionRewards")]
    public sealed class VoxelDestructionRewards : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            public VoxelStaticObstacleDefinition source;
            [Range(0, 1)] public float chance = .25f;
            [Tooltip("Relative chance of a multiplier prize after a successful reward roll. Zero disables this prize type.")]
            [Min(0)] public float multiplierWeight = 1f;
            [Tooltip("Relative chance of a cash prize after a successful reward roll. Zero disables this prize type.")]
            [Min(0)] public float cashWeight = 1f;
            [Tooltip("Relative chance of a time prize. Excluded after the mission timer expires, with odds redistributed to the other enabled types.")]
            [Min(0)] public float timeWeight = 1f;
            [Min(0)] public float multiplierBonus = .1f;
            [Min(10)] public int minimumCash = 10;
            [Min(10)] public int maximumCash = 50;
            [Min(5)] public int minimumTimeSeconds = 10;
            [Min(5)] public int maximumTimeSeconds = 20;
        }

        public struct Prize { public float multiplier; public int cash, timeSeconds; }

        public Entry[] entries = System.Array.Empty<Entry>();

        public Prize Roll(VoxelStaticObstacleDefinition source, float randomValue, float prizeChoice, float cashRoll,
            bool timeRewardAvailable = true) => Roll(entries, source, randomValue, prizeChoice, cashRoll, timeRewardAvailable);

        public static Prize Roll(Entry[] rewards, VoxelStaticObstacleDefinition source, float randomValue,
            float prizeChoice, float amountRoll, bool timeRewardAvailable = true)
        {
            if (source == null || rewards == null) return default;
            foreach (var entry in rewards)
                if (entry != null && entry.source == source)
                {
                    if (entry.chance <= 0 || (entry.chance < 1 && randomValue >= entry.chance)) return default;
                    float multiplierWeight = Mathf.Max(0f, entry.multiplierWeight);
                    float cashWeight = Mathf.Max(0f, entry.cashWeight);
                    float timeWeight = timeRewardAvailable ? Mathf.Max(0f, entry.timeWeight) : 0f;
                    float totalWeight = multiplierWeight + cashWeight + timeWeight;
                    if (totalWeight <= 0f) return default;
                    float choice = Mathf.Clamp01(prizeChoice) * totalWeight;
                    if (multiplierWeight > 0f && (choice < multiplierWeight || (cashWeight <= 0f && timeWeight <= 0f)))
                        return new Prize { multiplier = Mathf.Max(0, entry.multiplierBonus) };
                    bool cashPrize = cashWeight > 0f && (choice < multiplierWeight + cashWeight || timeWeight <= 0f);
                    if (!cashPrize && timeWeight > 0f)
                    {
                        int timeMin = Mathf.Max(1, Mathf.RoundToInt(entry.minimumTimeSeconds / 5f));
                        int timeMax = Mathf.Max(timeMin, Mathf.RoundToInt(entry.maximumTimeSeconds / 5f));
                        int timeStep = Mathf.Min(timeMax - timeMin, Mathf.FloorToInt(Mathf.Clamp01(amountRoll) * (timeMax - timeMin + 1)));
                        return new Prize { timeSeconds = (timeMin + timeStep) * 5 };
                    }
                    int min = Mathf.Max(1, Mathf.RoundToInt(entry.minimumCash / 10f));
                    int max = Mathf.Max(min, Mathf.RoundToInt(entry.maximumCash / 10f));
                    int step = Mathf.Min(max - min, Mathf.FloorToInt(Mathf.Clamp01(amountRoll) * (max - min + 1)));
                    return new Prize { cash = (min + step) * 10 };
                }
            return default;
        }

        private void OnValidate() => SanitizeEntries(entries);

        internal static void SanitizeEntries(Entry[] rewards)
        {
            if (rewards == null) return;
            foreach (var entry in rewards)
                if (entry != null)
                {
                    entry.chance = Mathf.Clamp01(entry.chance);
                    entry.multiplierWeight = Mathf.Max(0f, entry.multiplierWeight);
                    entry.cashWeight = Mathf.Max(0f, entry.cashWeight);
                    entry.timeWeight = Mathf.Max(0f, entry.timeWeight);
                    entry.multiplierBonus = Mathf.Max(0f, entry.multiplierBonus);
                    entry.minimumCash = Mathf.Max(10, Mathf.RoundToInt(entry.minimumCash / 10f) * 10);
                    entry.maximumCash = Mathf.Max(entry.minimumCash, Mathf.RoundToInt(entry.maximumCash / 10f) * 10);
                    entry.minimumTimeSeconds = Mathf.Max(5, Mathf.RoundToInt(entry.minimumTimeSeconds / 5f) * 5);
                    entry.maximumTimeSeconds = Mathf.Max(entry.minimumTimeSeconds, Mathf.RoundToInt(entry.maximumTimeSeconds / 5f) * 5);
                }
        }

        public static void ReportDestroyed(VoxelStaticObstacleDefinition source, Vector3 position)
        {
            var mission = VoxelMissionProgress.Active;
            if (mission == null || mission.Tuning == null || mission.IsComplete || mission.IsFailed) return;
            var prize = Roll(mission.Tuning.crateRewards, source, Random.value, Random.value, Random.value, mission.TimeBonusAvailable);
            float oldMultiplier = mission.EffectiveTimeBonusMultiplier;
            float oldTime = mission.RemainingTime;
            if (prize.multiplier > 0) mission.ChangeMultiplier(prize.multiplier, "BOX PRIZE", position);
            mission.AddBonusCash(prize.cash);
            bool timeGranted = prize.timeSeconds > 0 && mission.AddBonusTime(prize.timeSeconds, position);
            if (prize.cash > 0)
                mission.Breakdown.Add(VoxelMissionBreakdown.Group.CrateRewards, "$" + prize.cash + " cash", 1);
            if (prize.multiplier > 0)
                mission.Breakdown.Add(VoxelMissionBreakdown.Group.CrateRewards,
                    "+" + prize.multiplier.ToString("0.00") + "x multiplier (applied " +
                    (mission.EffectiveTimeBonusMultiplier - oldMultiplier).ToString("0.00") + "x)", 1);
            if (timeGranted)
                mission.Breakdown.Add(VoxelMissionBreakdown.Group.CrateRewards,
                    "+" + prize.timeSeconds + "s time (applied " + (mission.RemainingTime-oldTime).ToString("0") + "s)", 1);
            if (prize.cash > 0) VoxelScorePopup.Show(position + Vector3.up * 1.5f, prize.cash, VoxelScorePopup.Style.BonusCash);
        }
    }
}
