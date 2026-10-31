using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelBichoLocoValidation
    {
        internal const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, All).Invoke(target, args);
        internal static void Set(object target, string field, object value) => target.GetType().GetField(field, All).SetValue(target, value);
        internal static void Property(object target, string property, object value) => target.GetType().GetProperty(property, All).SetValue(target, value);
        internal static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        internal static void Equal(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .02f, message + ": " + actual + " != " + expected);

        internal sealed class Fixture : IDisposable
        {
            internal readonly Scene Scene;
            internal readonly GameObject Root;
            internal readonly VoxelCarController Player;
            internal readonly EndlessVoxelRoad Road;
            internal readonly VoxelMissionProgress Mission;
            internal readonly VoxelMissionTuning MissionTuning;
            internal readonly VoxelBossDefinition Definition;
            internal readonly VoxelBichoLocoTuning Settings;
            internal readonly VoxelPsychoBugTuning BodyBehaviour;
            internal readonly VoxelEnemyVehicleTuning Tuning;
            internal readonly VoxelEnemyCar Enemy;
            internal readonly VoxelObstacleCarTuning Traffic;
            internal VoxelBichoLocoRig Rig => Enemy.HopperRig;
            internal MeshRenderer Body => Enemy.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null && r.GetComponentInParent<VoxelEnemyHealthBar>() == null);
            internal Fixture()
            {
                Scene = EditorSceneManager.NewPreviewScene();
                Root = new GameObject("Temporary El Bicho Loco QA"); SceneManager.MoveGameObjectToScene(Root, Scene);
                var playerGo = new GameObject("Player QA"); playerGo.transform.SetParent(Root.transform, false);
                Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab, playerGo.transform);
                Player = playerGo.AddComponent<VoxelCarController>(); Player.enabled = false; Player.topSpeed = 32;
                Player.debrisVoxelsPerDamagedVoxel = 0; Set(Player, "nextDamageTime", float.PositiveInfinity);
                Player.SetLaneLayout(4, 5); Player.ResetIntegrityBaseline(); Property(Player, "CurrentSpeed", 32f);
                Road = Root.AddComponent<EndlessVoxelRoad>(); Road.enabled = false;
                Road.roadWidth = 20; Road.laneCount = 4; Road.segmentCount = 8; Road.turnChancePerSegment = 0;
                Road.minimumCactiPerSegment = Road.maximumCactiPerSegment = 0; Road.BuildInitialRoad();
                Player.SetTrack(Road, 100); Call(Player, "ApplyTrackPose");
                MissionTuning = Object.Instantiate(VoxelMissionTuning.Load()); MissionTuning.completionCurrencyAward = 0;
                Mission = Root.AddComponent<VoxelMissionProgress>(); Mission.Configure(MissionTuning); Mission.SetBossEncounter(true);
                Definition = Object.Instantiate(Resources.Load<VoxelBossDefinition>("Bosses/ElBichoLocoBoss")); Definition.entranceDuration = 0;
                Definition.health = 1000; // Controlled fixture; preserve edited authored health/split/movement values.
                Definition.acceleration = 60; Definition.braking = 40; Definition.minimumCruiseSpeedFraction = .6f;
                Definition.playerCollisionDamageMin = Definition.playerCollisionDamageMax = 0;
                Settings = Object.Instantiate(Definition.GetAttack<VoxelBichoLocoTuning>());
                Settings.combinedHopperHealthPercent = 40; Settings.approachClosingSpeed = 3;
                Settings.minimumApproachDistance = 28; Settings.retreatDistance = 160;
                Settings.minimumRetreatInterval = 28; Settings.maximumRetreatInterval = 38;
                Settings.retreatSpeedAdvantage = 18;
                BodyBehaviour = Object.Instantiate(Settings.bodyPhaseBehaviour ?? Resources.Load<VoxelPsychoBugTuning>("EnemyVehicles/PsychoBugBehaviour"));
                Settings.bodyPhaseBehaviour = BodyBehaviour;
                Definition.attacks = new VoxelBossAttackDefinition[] { Settings };
                Tuning = Definition.CreateRuntimeVehicleTuning(); Tuning.collisionHalfWidth = 5.5f;
                Tuning.collisionHalfLength = Definition.bodyCollisionHalfLength * 9f / Definition.referenceBodyWidth;
                Traffic = Object.Instantiate(VoxelObstacleCarTuning.Load());
                var enemyGo = new GameObject("El Bicho Loco QA"); enemyGo.transform.SetParent(Root.transform, false);
                Enemy = enemyGo.AddComponent<VoxelEnemyCar>(); Enemy.enabled = false;
                Enemy.ConfigureBoss(Definition, 5, 4); Enemy.Configure(Player, Traffic, Tuning, Road, 220, 0);
                Mission.ShowBoss(Enemy, Definition.bossName);
                foreach (var hopper in Rig.hoppers) Call(hopper, "UpdatePresentation", 0f, 0f);
            }
            public void Dispose()
            {
                Object.DestroyImmediate(Root); EditorSceneManager.ClosePreviewScene(Scene);
                foreach (var value in new Object[] { MissionTuning, Definition, Settings, BodyBehaviour, Tuning, Traffic }) Object.DestroyImmediate(value);
            }
        }

        [MenuItem("Tools/Voxel Racer/Bosses/Validate El Bicho Loco")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run the deterministic boss checks in Edit Mode.");
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            var random = UnityEngine.Random.state; var oldMission = VoxelMissionProgress.Active;
            var sourcePaths = new[] { VoxelBichoLocoBuilder.DefinitionPath, VoxelBichoLocoBuilder.AttackPath, VoxelBichoLocoBuilder.MinePath,
                VoxelBichoLocoBuilder.TrackPath, VoxelBichoLocoBuilder.BodyBehaviourPath,
                "Assets/Resources/EnemyVehicles/PsychoBugBehaviour.asset", VoxelRedVanBossBuilder.DefinitionPath, VoxelPsychoBugBuilder.PrefabPath };
            var before = sourcePaths.ToDictionary(p => p, File.ReadAllText);
            var report = new List<string>();
            try
            {
                VoxelRacerBootstrap.ReloadGeneratedMaterials();
                CheckAssets(); report.Add("PASS: model, direct brackets, four LL/RR support wheels and black/red roofs; independent mission, road/traffic/reward data and campaign entry.");
                CheckDamageAndLanes(); report.Add("PASS: 40% combined health / 10% per hopper, independent bars, gun/ram shielding, capped overkill, dead-slot exclusion, body unlock and legal lane ranges.");
                CheckMissiles(); report.Add("PASS: independent solid missile envelopes, zero/direct-radius/splash damage, body shield snapshot on final-hopper blast, no overflow and no phantom outer-lane blocker.");
                CheckLightsAndMines(); report.Add("PASS: one/off/sequential lamps, due mine belongs to lit hopper, outlet/lane alignment, dead-slot skipping and long-frame single-drop limit.");
                CheckPsychoBodyToggle();
                foreach (float step in new[] { 1f / 120, 1f / 60, 1f / 30, .25f })
                    foreach (float lane in new[] { -7.5f, -2.5f, 2.5f, 7.5f }) CheckPsychoBodyAttack(step, lane);
                foreach (float lane in new[] { -7.5f, -2.5f, 2.5f, 7.5f }) CheckPsychoBodyShooting(lane);
                report.Add("PASS: optional last-hopper Psycho phase, live toggle restoration without health/model reset, two-lane body bounds, shooting windows/damage evasion, safe side alignment and actual ram/recovery for all four player lanes at 120/60/30 FPS and 250ms steps.");
                foreach (float step in new[] { 1f / 120, 1f / 60, 1f / 30, .25f }) CheckMovement(step);
                report.Add("PASS: slow approach and repeated accelerated 160m retreats at 120/60/30 FPS and 250ms steps, within escape limits.");
                using (var f = new Fixture()) Capture(f);
                foreach (var path in sourcePaths) Check(File.ReadAllText(path) == before[path], "Validation changed source " + path);
                report.Add("PASS: source boss/attack/mine/track assets, original Bug/Vandito, run state and RNG preserved. Actual runtime render: Temp/BichoLoco/RuntimeRear.png.");
                Directory.CreateDirectory("Temp/BichoLoco"); File.WriteAllLines("Temp/BichoLoco/Validation.txt", report);
                Debug.Log(string.Join("\n", report));
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Temp/BichoLoco"); File.WriteAllText("Temp/BichoLoco/Validation.txt", string.Join("\n", report) + "\nFAIL: " + error);
                throw;
            }
            finally
            {
                UnityEngine.Random.state = random;
                typeof(VoxelMissionProgress).GetProperty("Active").SetValue(null, oldMission);
            }
        }

        private static void CheckAssets()
        {
            var track = AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(VoxelBichoLocoBuilder.TrackPath);
            var original = AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(VoxelRedVanBossBuilder.TrackPath);
            Check(track != null && track.boss.bossName == "El Bicho Loco" && track.raceSceneName == original.raceSceneName && track.isBossLevel, "Boss mission binding is missing.");
            Check(track.roadTuning.laneCount == 4 && track.roadTuning.roadWidth == 20 && track.roadTuning != original.roadTuning &&
                track.obstacleCarTuning != original.obstacleCarTuning && track.missionTuning != original.missionTuning, "Boss mission shares owned tunings or is not four lanes.");
            Equal(track.bossEncounter.radarInterceptorSpawnChance, original.bossEncounter.radarInterceptorSpawnChance, "Radar opening differs");
            Check(VoxelTrackSequence.Load().tracks.Count(t => t == track) == 1, "Campaign entry missing/duplicated.");
            var rig = track.boss.bossPrefab.GetComponent<VoxelBichoLocoRig>();
            Check(rig != null && rig.hoppers.Length == 4 && rig.hoppers.Select(h => h.slot).SequenceEqual(new[] { 0, 1, 2, 3 }), "Four numbered hoppers required.");
            Check(rig.roofLamp != null && rig.roofLamp.GetComponentInParent<VoxelIndestructiblePart>(true) != null &&
                Mathf.Abs(rig.roofLamp.transform.parent.localPosition.x) < .001f, "Centred protected roof beacon missing.");
            Check(rig.roofLampOn != null && rig.roofLampOn.IsKeywordEnabled("_EMISSION"), "Beacon emission is disabled.");
            Check(rig.roofLampOn == Resources.Load<Material>("CarMaterials/CarBrakeLights") &&
                (rig.roofLampOn.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive) != 0,
                "Boss beacons must retain the brake-light material's authored emission settings.");
            foreach (var hopper in rig.hoppers)
            {
                Check(Mathf.Sign(hopper.wheel.localPosition.x) == (hopper.slot < 2 ? -1 : 1), "Wrong side-mounted wheel on " + hopper.slot);
                Check(hopper.wheel.childCount == 3 && hopper.mineOutlet != null && hopper.roofLamp != null && hopper.lampOn != hopper.lampOff,
                    "Hopper wheel/outlet/beacon is incomplete.");
                Check(hopper.transform.GetComponentsInChildren<Transform>().Count(t => t.name == "Black roof") == 1, "Hopper has no black roof.");
                var lid = hopper.transform.Find("Black roof");
                Check(hopper.roofLamp.transform.localScale.x > lid.localScale.x * .3f &&
                    hopper.roofLamp.transform.localScale.y > lid.localScale.y * 2,
                    "Hopper beacon is still too small/flat.");
                Check(hopper.roofLamp.name == "Hopper Letter " + "LOCO"[hopper.slot], "Hopper letters do not spell LOCO from left to right.");
                var glyph = hopper.roofLamp.GetComponent<MeshFilter>().sharedMesh;
                Check(AssetDatabase.Contains(glyph) && Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(glyph)) ==
                    "HopperLetter" + "LOCO"[hopper.slot] && glyph.subMeshCount == 1,
                    "Hopper letter must use one saved extruded mesh.");
                Equal(hopper.roofLamp.bounds.max.y, rig.roofLamp.transform.parent.localPosition.y - .025f,
                    "Letter top must meet the Bug roofline");
                Equal(hopper.roofLamp.bounds.min.y, lid.GetComponent<MeshRenderer>().bounds.max.y,
                    "Letter must stand on the hopper roof");
                Check(hopper.lampOn == rig.roofLampOn, "Roof/hopper on materials differ.");
            }
        }

        private static void LaneRange(Fixture f, int minimum, int maximum)
        {
            var args = new object[] { 4, 0, 0 }; Call(f.Rig, "LanePairRange", args);
            Check((int)args[1] == minimum && (int)args[2] == maximum, "Illegal remaining-hopper lane range.");
            for (int i = 0; i < 20; i++)
            {
                Set(f.Enemy, "nextBossLaneChange", -1f); Call(f.Enemy, "UpdateBossLaneChange");
                float destination = (float)f.Enemy.GetType().GetField("targetLaneOffset", All).GetValue(f.Enemy);
                Check(destination >= (minimum - 1) * 5 - .01f && destination <= (maximum - 1) * 5 + .01f, "Lane selector exceeded available width.");
                Set(f.Enemy, "laneOffset", destination);
            }
        }

        private static void CheckDamageAndLanes()
        {
            using var f = new Fixture(); var body = f.Body;
            Equal(f.Enemy.CurrentHealth, 1000, "Total health");
            foreach (var h in f.Rig.hoppers) { Equal(h.CurrentHealth, 100, "Individual health"); Check(h.HealthBar != null, "Missing independent bar."); }
            f.Enemy.TakeProjectileHit(body.transform, 9000, body.transform.position, Vector3.forward);
            Equal(f.Enemy.CurrentHealth, 1000, "Body gun shield"); Check(body.gameObject.activeSelf, "Body voxel carved through shield.");
            f.Enemy.TakeMissileBlast(body.transform.position, 50, 9000, Vector3.forward, f.Enemy.GetComponentsInChildren<MeshRenderer>(), true);
            Equal(f.Enemy.CurrentHealth, 1000, "Body missile shield");
            Call(f.Enemy, "RamByPlayer", (object)(Vector3?)Vector3.forward); Equal(f.Enemy.CurrentHealth, 1000, "Body ram shield");
            LaneRange(f, 1, 1);
            var first = f.Rig.hoppers[0];
            Vector3 shot = first.transform.TransformPoint(first.LocalBounds.center) - f.Enemy.transform.forward * 12;
            Check(f.Enemy.TryGetNextProjectileVoxel(shot, f.Enemy.transform.forward, 20, out var hitPart) &&
                hitPart.GetComponentInParent<VoxelBichoHopper>() == first, "Bullet query missed the outer hopper.");
            f.Enemy.TakeProjectileHit(hitPart, 12, hitPart.position, Vector3.forward);
            Equal(first.CurrentHealth, 88, "Partial hopper hit"); Equal(f.Enemy.CurrentHealth, 988, "Aggregate partial hit");
            f.Enemy.TakeProjectileHit(first.transform, 10000, first.transform.position, Vector3.forward);
            Equal(f.Enemy.CurrentHealth, 900, "Hopper overkill spilled into body"); Check(!first.IsAlive && !first.HealthBar.gameObject.activeSelf, "Destroyed hopper/bar remains active.");
            Check(!first.roofLamp.gameObject.activeInHierarchy && f.Rig.hoppers[1].roofLamp.gameObject.activeInHierarchy,
                "Hopper death did not remove only its own letter.");
            f.Enemy.TakeProjectileHit(first.transform, 10, first.transform.position, Vector3.forward); Equal(f.Enemy.CurrentHealth, 900, "Dead hopper accepted damage");
            LaneRange(f, 0, 1);
            Call(f.Enemy, "ApplyBichoRam", f.Rig.hoppers[3], Vector3.forward);
            Check(!f.Rig.hoppers[3].IsAlive, "Hopper ram did not use ram damage."); Equal(f.Enemy.CurrentHealth, 800, "Hopper ram aggregate");
            LaneRange(f, 0, 2);
            f.Rig.hoppers[1].TakeDamage(10000, Vector3.zero, Vector3.forward, false);
            Check(f.Enemy.IsBodyShielded, "Body opened before all four hoppers died.");
            f.Rig.hoppers[2].TakeDamage(10000, Vector3.zero, Vector3.forward, false);
            Equal(f.Enemy.CurrentHealth, 600, "Body health reserve"); Check(!f.Enemy.IsBodyShielded, "Body stayed shielded after four deaths.");
            f.Enemy.TakeProjectileHit(body.transform, 1, body.transform.position, Vector3.forward);
            Equal(f.Enemy.CurrentHealth, 599, "Unlocked body did not take damage");
        }

        private static void CheckMissiles()
        {
            using var f = new Fixture(); var hopper = f.Rig.hoppers[0];
            Vector3 point = hopper.transform.TransformPoint(hopper.LocalBounds.center);
            Check(VoxelMissileTarget.Trace(point - f.Enemy.transform.forward * 12, f.Enemy.transform.forward, 20,
                out var target, out var voxel, out var hit, out var distance), "Outer hopper had no solid missile target.");
            Check(target.GetComponent<VoxelBichoHopper>() == hopper, "Outer missile hit body/another hopper.");
            VoxelMissileTarget.Blast(hit, 0, 0, Vector3.forward, target, voxel); Equal(hopper.CurrentHealth, 100, "Zero damage missile");
            f.Definition.gunHealthDamageMultiplier = 5; f.Definition.missileHealthDamageMultiplier = 2;
            VoxelMissileTarget.Blast(hit, 0, 10, Vector3.forward, target, voxel);
            Equal(hopper.CurrentHealth, 80, "Zero-radius missile used gun multiplier");
            f.Definition.missileHealthDamageMultiplier = 1;
            VoxelMissileTarget.Blast(f.Enemy.transform.position, 100, 10000, Vector3.forward);
            Check(!f.Rig.HasLiveHoppers, "Splash did not damage all independent hoppers.");
            Equal(f.Enemy.CurrentHealth, 600, "Final-hopper blast damaged the body too");
            Check(!VoxelMissileTarget.Trace(point - f.Enemy.transform.forward * 12, f.Enemy.transform.forward, 20,
                out _, out _, out _, out _), "Destroyed outer slot left a phantom missile envelope.");
        }

        private static void CheckLightsAndMines()
        {
            using var f = new Fixture(); f.Settings.lightStepDuration = .2f; f.Settings.lightOnFraction = .5f; f.Settings.mineDropInterval = .27f;
            Check(f.Rig.RoofLightIsLit && f.Rig.roofLamp.sharedMaterial == f.Rig.roofLampOn, "Roof beacon did not start with first hopper.");
            Call(f.Rig, "Advance", .11f, 0f, 0f); Check(!f.Rig.hoppers.Any(h => h.IsLit), "Lamp off interval missing.");
            Check(!f.Rig.RoofLightIsLit && f.Rig.roofLamp.sharedMaterial == f.Rig.roofLampOff, "Roof beacon missed hopper off interval.");
            Call(f.Rig, "Advance", .11f, 0f, 0f); Check(f.Rig.ActiveLightSlot == 1 && f.Rig.hoppers[1].IsLit, "Lamp sequence did not advance.");
            Call(f.Rig, "Advance", .06f, 0f, 0f); Check(f.Rig.MinesDropped == 1 && f.Rig.LastDropSlot == 1 && f.Rig.hoppers[1].IsLit, "Mine did not come from lit hopper.");
            var mine = f.Root.GetComponentsInChildren<VoxelRoadMine>().Single(); Equal(mine.LaneOffset, -2.5f, "Mine lane centre");
            f.Rig.hoppers[0].TakeDamage(1000, Vector3.zero, Vector3.forward, false);
            f.Rig.hoppers[3].TakeDamage(1000, Vector3.zero, Vector3.forward, false);
            for (int i = 0; i < 80; i++)
            {
                int previous = f.Rig.MinesDropped; Call(f.Rig, "Advance", .06f, 0f, 0f);
                Check(f.Rig.hoppers.Count(h => h.IsLit) <= 1, "Multiple lamps lit.");
                Check(f.Rig.RoofLightIsLit == f.Rig.hoppers.Any(h => h.IsLit), "Roof beacon drifted from hopper clock.");
                if (f.Rig.MinesDropped > previous) Check((f.Rig.LastDropSlot == 1 || f.Rig.LastDropSlot == 2) && f.Rig.hoppers[f.Rig.LastDropSlot].IsLit, "Dead/unlit slot dropped.");
            }
            int before = f.Rig.MinesDropped; Call(f.Rig, "Advance", 100f, 0f, 0f);
            Check(f.Rig.MinesDropped - before <= 1, "Long frame produced a backlog burst.");
            foreach (var hopper in f.Rig.hoppers.Where(h => h.IsAlive)) hopper.TakeDamage(10000, Vector3.zero, Vector3.forward, false);
            Check(!f.Rig.RoofLightIsLit && f.Rig.roofLamp.sharedMaterial == f.Rig.roofLampOff,
                "Roof beacon kept flashing after last hopper died.");
        }

        private static void CheckMovement(float step)
        {
            using var f = new Fixture(); float playerDistance = 100, gap = 120, speed = 32;
            bool retreated = false, closed = false; float peak = 0;
            for (float time = 0; time < 95; time += step)
            {
                Property(f.Player, "TrackDistance", playerDistance); Set(f.Enemy, "trackDistance", playerDistance + gap); Set(f.Enemy, "currentSpeed", speed);
                speed = (float)Call(f.Enemy, "AdvanceBossSpeed", step); gap += (speed - 32) * step; playerDistance += 32 * step;
                if (time < 4 && gap < 114) closed = true;
                if (gap >= 159) retreated = true;
                peak = Mathf.Max(peak, gap);
                Check(gap > 20 && gap < f.Definition.WarningDistance, "Approach/retreat left combat bounds at step " + step + ": " + gap);
            }
            Check(closed && retreated && peak < 168, "Approach/160m retreat missing or overshot at step " + step + ": peak=" + peak);
        }

        private static void DestroyHoppers(Fixture f)
        {
            foreach (var hopper in f.Rig.hoppers) hopper.TakeDamage(100000, hopper.transform.position, Vector3.forward, false);
        }

        private static void CheckPsychoBodyToggle()
        {
            using var f = new Fixture();
            f.Settings.usePsychoBehaviourAfterHoppers = true;
            for (int i = 0; i < 3; i++) f.Rig.hoppers[i].TakeDamage(10000, Vector3.zero, Vector3.forward, false);
            Check(!f.Enemy.IsPsychoBug && !f.Enemy.IsBichoPsychoPhase, "Psycho body phase began before the last hopper died.");
            f.Rig.hoppers[3].TakeDamage(10000, Vector3.zero, Vector3.forward, false);
            Check(f.Enemy.IsBoss && f.Enemy.IsPsychoBug && f.Enemy.IsBichoPsychoPhase && f.Tuning.psychoBug == null,
                "Body phase did not reuse AI while retaining boss identity/tuning.");
            Equal(f.Enemy.CurrentHealth, 600, "Phase transition reset health");
            for (int lane = 0; lane < 3; lane++) Equal((float)Call(f.Enemy, "PsychoLane", lane), (lane - 1) * 5, "Body pair grid");
            f.Settings.usePsychoBehaviourAfterHoppers = false; Call(f.Enemy, "RefreshBichoBodyPhase");
            Check(!f.Enemy.IsPsychoBug && !f.Enemy.IsBichoPsychoPhase && f.Enemy.IsBoss && !f.Rig.IsRetreating,
                "Toggle off failed to restore approach/retreat driving.");
            Equal(f.Enemy.CurrentHealth, 600, "Toggle off reset health");
            f.Settings.usePsychoBehaviourAfterHoppers = true; Call(f.Enemy, "RefreshBichoBodyPhase");
            Check(f.Enemy.IsBichoPsychoPhase && f.Rig.hoppers.All(h => !h.IsAlive), "Toggle on restored hoppers or failed to restart AI.");
            Equal(f.Enemy.CurrentHealth, 600, "Toggle on reset health");
        }

        private static void CheckPsychoBodyAttack(float step, float playerLane)
        {
            using var f = new Fixture(); f.Settings.usePsychoBehaviourAfterHoppers = true;
            var behaviour = f.BodyBehaviour;
            behaviour.shootingWindows = false; behaviour.damageEvasion = false; behaviour.weaveChance = 0;
            behaviour.sideRamChance = behaviour.initialRamChance = 1; behaviour.feintChance = 0;
            behaviour.minimumAttackCooldown = behaviour.maximumAttackCooldown = .1f;
            Property(f.Player, "CurrentLaneOffset", playerLane);
            Set(f.Enemy, "trackDistance", 180f); Set(f.Enemy, "currentSpeed", 32f);
            DestroyHoppers(f);
            Check((bool)Call(f.Enemy, "TryChoosePsychoSide", playerLane), "No safe boss side for player lane " + playerLane);
            float side = (float)f.Enemy.GetType().GetField("targetLaneOffset", All).GetValue(f.Enemy);
            Check(Mathf.Abs(side) <= 5 && Mathf.Abs(side - playerLane) >= f.Tuning.collisionHalfWidth + behaviour.playerCollisionHalfWidth,
                "Boss aligned over the player or outside the road.");
            bool warning = false, hit = false;
            for (float time = 0; time < 16 && !hit; time += step)
            {
                Property(f.Player, "TrackDistance", f.Player.TrackDistance + 32 * step); Call(f.Player, "ApplyTrackPose");
                Call(f.Enemy, "AdvancePsychoBug", step); Call(f.Enemy, "ApplyTrackPose"); Call(f.Enemy, "CheckVehicleContact");
                warning |= f.Enemy.PsychoPhase == PsychoBugPhase.Warning;
                hit = (bool)f.Enemy.GetType().GetField("psychoRamApplied", All).GetValue(f.Enemy);
                Check(Mathf.Abs(f.Enemy.LaneOffset) <= 5.01f, "Wide boss left the road during Psycho manoeuvre.");
            }
            Check(warning && hit && f.Enemy.PsychoPhase == PsychoBugPhase.Recovering,
                "Psycho body failed warning/ram/recovery: step=" + step + ", lane=" + playerLane + ", phase=" + f.Enemy.PsychoPhase);
            Equal(f.Enemy.CurrentHealth, 600, "Attacking changed boss health");
        }

        private static void CheckPsychoBodyShooting(float playerLane)
        {
            using var f = new Fixture(); f.Settings.usePsychoBehaviourAfterHoppers = true;
            var behaviour = f.BodyBehaviour;
            behaviour.sideRamEnabled = false; behaviour.weaveChance = 0; behaviour.damageEvasion = false;
            behaviour.shootingWindows = true; behaviour.maximumTimeBetweenShootingWindows = 0;
            behaviour.minimumShootingDuration = behaviour.maximumShootingDuration = 1;
            Property(f.Player, "CurrentLaneOffset", playerLane);
            Set(f.Enemy, "trackDistance", 114f); Set(f.Enemy, "currentSpeed", 32f); DestroyHoppers(f);
            for (float time = 0; time < 6 && f.Enemy.PsychoPhase != PsychoBugPhase.Exposed; time += .02f)
            {
                Property(f.Player, "TrackDistance", f.Player.TrackDistance + .64f); Call(f.Player, "ApplyTrackPose");
                Call(f.Enemy, "AdvancePsychoBug", .02f); Call(f.Enemy, "ApplyTrackPose");
            }
            Check(f.Enemy.PsychoPhase == PsychoBugPhase.Exposed, "Wide boss could not expose its body to player lane " + playerLane);
            behaviour.damageEvasion = true; behaviour.damageEvasionChance = 1; behaviour.damageReactionDelay = 0;
            f.Enemy.TakeProjectileHit(f.Body.transform, 1, f.Body.transform.position, Vector3.forward);
            Call(f.Enemy, "AdvancePsychoBug", .02f);
            float escape = (float)f.Enemy.GetType().GetField("psychoEscapeUntil", All).GetValue(f.Enemy);
            float clock = (float)f.Enemy.GetType().GetField("psychoClock", All).GetValue(f.Enemy);
            Check(escape > clock && f.Enemy.PsychoPhase != PsychoBugPhase.Exposed && Mathf.Abs(f.Enemy.LaneOffset) <= 5.01f,
                "Body damage did not interrupt exposure with legal Psycho evasion.");
            Equal(f.Enemy.CurrentHealth, 599, "Exposed body health damage");
        }

        private static void Capture(Fixture f)
        {
            var key = new GameObject("QA key light").AddComponent<Light>(); key.transform.SetParent(f.Root.transform, false);
            key.type = LightType.Directional; key.intensity = 2; key.transform.rotation = Quaternion.Euler(45, -30, 0);
            var camera = new GameObject("Bicho QA camera").AddComponent<Camera>(); camera.transform.SetParent(f.Root.transform, false);
            camera.enabled = false; camera.scene = f.Scene; camera.fieldOfView = 45;
            camera.transform.position = f.Enemy.transform.position + new Vector3(22, 18, -37);
            camera.transform.LookAt(f.Enemy.transform.position + new Vector3(0, 1, -3));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.2f, .24f, .3f);
            var rt = new RenderTexture(1440, 1000, 24); var image = new Texture2D(1440, 1000, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0); image.Apply();
                Directory.CreateDirectory("Temp/BichoLoco"); File.WriteAllBytes("Temp/BichoLoco/RuntimeRear.png", image.EncodeToPNG());
                // Exercise the actual shader with the mobile post-processing profile,
                // rather than accepting an enabled keyword on a cached material alone.
                key.intensity = .3f; key.color = new Color(.45f, .55f, 1);
                camera.allowHDR = true; VoxelMobilePostProcessing.Configure(camera);
                f.Settings.lightStepDuration = .2f; f.Settings.lightOnFraction = .5f;
                Set(f.Rig, "phaseTime", .15f); Call(f.Rig, "UpdateLights");
                int ReadGlow(string name)
                {
                    camera.Render(); RenderTexture.active = rt;
                    image.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0); image.Apply();
                    File.WriteAllBytes("Temp/BichoLoco/" + name + ".png", image.EncodeToPNG());
                    return image.GetPixels().Count(c => c.r > .75f && c.r > c.g * 2 && c.r > c.b * 2);
                }
                int offPixels = ReadGlow("BeaconsOff");
                for (int slot = 0; slot < 4; slot++)
                {
                    Property(f.Rig, "ActiveLightSlot", slot); Set(f.Rig, "phaseTime", .03f); Call(f.Rig, "UpdateLights");
                    Check(ReadGlow("BeaconsOn" + slot) > offPixels + 40,
                        "Beacon emission did not visibly brighten the rendered roof/hopper lenses for slot " + slot);
                }
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image); }
        }
    }
}
