using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Destruction Rewards", fileName = "DestructionRewards")]
    public sealed class VoxelDestructionRewards : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            public VoxelStaticObstacleDefinition source;
            [Range(0, 1)] public float chance = .25f;
            [Min(0)] public float multiplierBonus = .1f;
            [Min(10)] public int minimumCash = 10;
            [Min(10)] public int maximumCash = 50;
            [Min(5)] public int minimumTimeSeconds = 10;
            [Min(5)] public int maximumTimeSeconds = 20;
        }

        public struct Prize { public float multiplier; public int cash, timeSeconds; }

        public Entry[] entries = System.Array.Empty<Entry>();

        public Prize Roll(VoxelStaticObstacleDefinition source, float randomValue, float prizeChoice, float cashRoll)
        {
            if (source == null) return default;
            foreach (var entry in entries)
                if (entry != null && entry.source == source)
                {
                    if (entry.chance <= 0 || (entry.chance < 1 && randomValue >= entry.chance)) return default;
                    if (prizeChoice < 1f / 3f) return new Prize { multiplier = Mathf.Max(0, entry.multiplierBonus) };
                    if (prizeChoice >= 2f / 3f)
                    {
                        int timeMin = Mathf.Max(1, Mathf.RoundToInt(entry.minimumTimeSeconds / 5f));
                        int timeMax = Mathf.Max(timeMin, Mathf.RoundToInt(entry.maximumTimeSeconds / 5f));
                        int timeStep = Mathf.Min(timeMax - timeMin, Mathf.FloorToInt(Mathf.Clamp01(cashRoll) * (timeMax - timeMin + 1)));
                        return new Prize { timeSeconds = (timeMin + timeStep) * 5 };
                    }
                    int min = Mathf.Max(1, Mathf.RoundToInt(entry.minimumCash / 10f));
                    int max = Mathf.Max(min, Mathf.RoundToInt(entry.maximumCash / 10f));
                    int step = Mathf.Min(max - min, Mathf.FloorToInt(Mathf.Clamp01(cashRoll) * (max - min + 1)));
                    return new Prize { cash = (min + step) * 10 };
                }
            return default;
        }

        private void OnValidate()
        {
            foreach (var entry in entries)
                if (entry != null)
                {
                    entry.minimumCash = Mathf.Max(10, Mathf.RoundToInt(entry.minimumCash / 10f) * 10);
                    entry.maximumCash = Mathf.Max(entry.minimumCash, Mathf.RoundToInt(entry.maximumCash / 10f) * 10);
                    entry.minimumTimeSeconds = Mathf.Max(5, Mathf.RoundToInt(entry.minimumTimeSeconds / 5f) * 5);
                    entry.maximumTimeSeconds = Mathf.Max(entry.minimumTimeSeconds, Mathf.RoundToInt(entry.maximumTimeSeconds / 5f) * 5);
                }
        }

        public static void ReportDestroyed(VoxelStaticObstacleDefinition source, Vector3 position)
        {
            var mission = VoxelMissionProgress.Active;
            if (mission == null || mission.IsComplete) return;
            var rewards = Resources.Load<VoxelDestructionRewards>("DestructionRewards");
            if (rewards == null) return;
            var prize = rewards.Roll(source, Random.value, Random.value, Random.value);
            float oldMultiplier = mission.EffectiveTimeBonusMultiplier;
            float oldTime = mission.RemainingTime;
            if (prize.multiplier > 0) mission.ChangeMultiplier(prize.multiplier, "BOX PRIZE", position);
            mission.AddBonusCash(prize.cash);
            if (prize.timeSeconds > 0) mission.AddBonusTime(prize.timeSeconds, position);
            if (prize.cash > 0)
                mission.Breakdown.Add(VoxelMissionBreakdown.Group.CrateRewards, "$" + prize.cash + " cash", 1);
            if (prize.multiplier > 0)
                mission.Breakdown.Add(VoxelMissionBreakdown.Group.CrateRewards,
                    "+" + prize.multiplier.ToString("0.00") + "x multiplier (applied " +
                    (mission.EffectiveTimeBonusMultiplier - oldMultiplier).ToString("0.00") + "x)", 1);
            if (prize.timeSeconds > 0)
                mission.Breakdown.Add(VoxelMissionBreakdown.Group.CrateRewards,
                    "+" + prize.timeSeconds + "s time (applied " + (mission.RemainingTime-oldTime).ToString("0") + "s)", 1);
            if (prize.cash > 0) VoxelScorePopup.Show(position + Vector3.up * 1.5f, prize.cash, VoxelScorePopup.Style.BonusCash);
        }
    }
}
