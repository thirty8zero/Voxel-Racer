using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelMissionCrateRewardValidation
    {
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        private static void Equal(float actual, float expected, string message) =>
            Check(Mathf.Abs(actual - expected) < .001f, message + $": expected {expected}, got {actual}.");
        private static bool Empty(VoxelDestructionRewards.Prize prize) =>
            prize.cash == 0 && prize.multiplier == 0 && prize.timeSeconds == 0;

        [MenuItem("Tools/Voxel Racer/Validate Mission Crate Rewards")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run mission crate reward checks in Edit Mode.");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary mission crate reward QA");
            SceneManager.MoveGameObjectToScene(root, scene);
            var previous = VoxelMissionProgress.Active;
            var random = UnityEngine.Random.state;
            int cash = VoxelCurrencyState.Balance;
            int track = VoxelTrackProgressState.CurrentTrackIndex;
            var a = ScriptableObject.CreateInstance<VoxelMissionTuning>();
            var b = ScriptableObject.CreateInstance<VoxelMissionTuning>();
            var copy = ScriptableObject.CreateInstance<VoxelMissionTuning>();
            var unknownSource = ScriptableObject.CreateInstance<VoxelStaticObstacleDefinition>();
            var legacy = Resources.Load<VoxelDestructionRewards>("DestructionRewards");
            string legacyBefore = EditorJsonUtility.ToJson(legacy);
            try
            {
                var source = Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/VoxelBox");
                var entry = new VoxelDestructionRewards.Entry { source = source, chance = 1f, multiplierBonus = .5f };
                VoxelDestructionRewards.Prize Roll(float chanceRoll = 0, float choice = 0, float amount = 0, bool time = true) =>
                    VoxelDestructionRewards.Roll(new[] { entry }, source, chanceRoll, choice, amount, time);
                Equal(Roll(choice: 1f / 6f).multiplier, .5f, "Default multiplier third");
                Equal(Roll(choice: .5f).cash, 10, "Default cash third");
                Equal(Roll(choice: 5f / 6f).timeSeconds, 10, "Default time third");
                entry.chance = .25f;
                Check(!Empty(Roll(.2499f)) && Empty(Roll(.25f)), "Chance boundary changed.");
                entry.chance = 0;
                Check(Empty(Roll()), "Zero chance awarded a prize.");
                entry.chance = 1;
                Check(!Empty(Roll(1)), "Guaranteed prize missed at RNG endpoint.");
                Check(Empty(VoxelDestructionRewards.Roll(null, source, 0, 0, 0)) &&
                    Empty(VoxelDestructionRewards.Roll(Array.Empty<VoxelDestructionRewards.Entry>(), source, 0, 0, 0)) &&
                    Empty(VoxelDestructionRewards.Roll(new[] { entry }, unknownSource, 0, 0, 0)) &&
                    Empty(VoxelDestructionRewards.Roll(new[] { entry }, null, 0, 0, 0)), "Missing/empty sources awarded a prize.");
                foreach (bool time in new[] { true, false })
                {
                    int multiplier = 0, money = 0, seconds = 0;
                    for (int i = 0; i < 1200; i++)
                    {
                        var prize = Roll(choice: (i + .5f) / 1200, time: time);
                        if (prize.multiplier > 0) multiplier++;
                        if (prize.cash > 0) money++;
                        if (prize.timeSeconds > 0) seconds++;
                    }
                    Check(multiplier == (time ? 400 : 600) && money == (time ? 400 : 600) && seconds == (time ? 400 : 0),
                        "Default equal odds or expired-time redistribution changed.");
                }
                entry.cashWeight = 3; entry.timeWeight = 0;
                int weightedCash = Enumerable.Range(0, 1200).Count(i => Roll(choice: (i + .5f) / 1200).cash > 0);
                Equal(weightedCash, 900, "1:3:0 weights must award cash in three quarters of successful rolls");
                entry.multiplierWeight = 0; entry.cashWeight = 0;
                Check(Empty(Roll()), "All-zero weights awarded a prize.");
                entry.timeWeight = 1;
                Equal(Roll(choice: 0).timeSeconds, 10, "Time-only lower choice boundary");
                Equal(Roll(choice: 1, amount: 1).timeSeconds, 20, "Time-only upper range boundary");
                Check(Empty(Roll(time: false)), "Time-only mission revived an expired timer.");
                entry.timeWeight = 0; entry.cashWeight = 1;
                Equal(Roll(choice: 1, amount: 1).cash, 50, "Cash-only upper choice/range boundary");
                for (int i = 0; i < 100; i++) Check(Roll(amount: i / 99f).cash % 10 == 0, "Cash lost 10-unit increments.");
                entry.cashWeight = 0; entry.timeWeight = 1;
                for (int i = 0; i < 100; i++) Check(Roll(amount: i / 99f).timeSeconds % 5 == 0, "Time lost 5-second increments.");

                a.crateRewards = new[] { new VoxelDestructionRewards.Entry { source = source, chance = 1,
                    multiplierWeight = 0, cashWeight = 1, timeWeight = 0, minimumCash = 10, maximumCash = 10 } };
                b.crateRewards = new[] { new VoxelDestructionRewards.Entry { source = source, chance = 1,
                    multiplierWeight = 0, cashWeight = 1, timeWeight = 0, minimumCash = 70, maximumCash = 70 } };
                var mission = root.AddComponent<VoxelMissionProgress>(); mission.enabled = false;
                typeof(VoxelMissionProgress).GetProperty("Active").SetValue(null, mission);
                mission.Configure(a);
                VoxelDestructionRewards.ReportDestroyed(source, Vector3.zero);
                Equal(mission.BonusCashEarned, 10, "Mission A did not use its own cash prize");
                mission.Configure(b);
                VoxelDestructionRewards.ReportDestroyed(source, Vector3.zero);
                Equal(mission.BonusCashEarned, 70, "Mission B did not use its own cash prize");
                a.crateRewards[0].minimumCash = a.crateRewards[0].maximumCash = 20;
                Equal(b.crateRewards[0].minimumCash, 70, "Mission reward entries share mutable state");
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(b), copy);
                Equal(copy.crateRewards[0].minimumCash, 70, "Mission reward serialization lost prize values");
                Check(copy.crateRewards[0].source == source, "Mission reward serialization lost source reference.");
                a.crateRewards = Array.Empty<VoxelDestructionRewards.Entry>();
                mission.Configure(a); VoxelDestructionRewards.ReportDestroyed(source, Vector3.zero);
                Equal(mission.BonusCashEarned, 0, "Empty mission rewards fell back to the shared asset");
                a.crateRewards = new[] { new VoxelDestructionRewards.Entry { source = source, chance = 1,
                    multiplierWeight = 1, cashWeight = 0, timeWeight = 0, multiplierBonus = .5f } };
                a.maximumTimeMultiplier = 1.2f;
                mission.Configure(a); VoxelDestructionRewards.ReportDestroyed(source, Vector3.zero);
                Equal(mission.EffectiveTimeBonusMultiplier, 1.2f, "Mission multiplier cap changed");
                Equal(mission.Breakdown.Total(VoxelMissionBreakdown.Group.MultiplierGained), .2f, "Breakdown did not record applied multiplier");
                a.crateRewards[0].multiplierWeight = 0; a.crateRewards[0].timeWeight = 1;
                a.crateRewards[0].minimumTimeSeconds = a.crateRewards[0].maximumTimeSeconds = 10;
                mission.Configure(a); float before = mission.RemainingTime;
                VoxelDestructionRewards.ReportDestroyed(source, Vector3.zero);
                Equal(mission.RemainingTime, before + 10, "Mission-local time reward did not extend timer");
                mission.AdvanceBonusClock(mission.RemainingTime + 1);
                VoxelDestructionRewards.ReportDestroyed(source, Vector3.zero);
                Equal(mission.RemainingTime, 0, "Time reward revived an expired timer");
                mission.Configure(b); typeof(VoxelMissionProgress).GetProperty("IsComplete").SetValue(mission, true);
                VoxelDestructionRewards.ReportDestroyed(source, Vector3.zero);
                Equal(mission.BonusCashEarned, 0, "Completed mission accepted a crate reward");
                mission.Configure(b); typeof(VoxelMissionProgress).GetProperty("IsFailed").SetValue(mission, true);
                VoxelDestructionRewards.ReportDestroyed(source, Vector3.zero);
                Equal(mission.BonusCashEarned, 0, "Failed mission accepted a crate reward");
                Equal(mission.Points, 0, "Crate prizes changed mission score");
                Check(VoxelCurrencyState.Balance == cash && VoxelTrackProgressState.CurrentTrackIndex == track,
                    "Crate reward validation changed the wallet or campaign progress.");
                Check(EditorJsonUtility.ToJson(legacy) == legacyBefore, "Shared migration reference was changed.");
                using (var tree = PropertyTree.Create(b))
                {
                    string beforeInspector = EditorJsonUtility.ToJson(b);
                    tree.UpdateTree();
                    var property = tree.EnumerateTree(true).First(p => p.Name == "crateRewards");
                    Check(property.Attributes.OfType<FoldoutGroupAttribute>().Any(g => g.GroupName == "Crate Rewards"),
                        "Mission Inspector has no Crate Rewards foldout.");
                    Check(beforeInspector == EditorJsonUtility.ToJson(b), "Inspector changed reward values.");
                }
                const string report = "PASS (Edit Mode): independent mission chance/prizes; weighted prize types and zero-weight disabling; " +
                    "original equal odds and expired-time redistribution; chance/source boundaries; empty rewards disable prizes " +
                    "without shared fallback; cash/time ranges and increments; actual ReportDestroyed cash/multiplier/time paths; " +
                    "multiplier cap, timer expiry, completed/failed gates, breakdown; mission serialization/source reference; " +
                    "Crate Rewards Inspector foldout; global asset, cash, campaign and RNG preserved.";
                Directory.CreateDirectory("Temp/MissionCrateRewards");
                File.WriteAllText("Temp/MissionCrateRewards/Validation.txt", report);
                Debug.Log(report);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(copy); Object.DestroyImmediate(unknownSource);
                EditorSceneManager.ClosePreviewScene(scene);
                typeof(VoxelMissionProgress).GetProperty("Active").SetValue(null, previous);
                UnityEngine.Random.state = random;
            }
        }
    }
}
