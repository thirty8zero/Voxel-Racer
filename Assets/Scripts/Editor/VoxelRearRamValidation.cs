using System;
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
    public static class VoxelRearRamValidation
    {
        private const string Pending = "VoxelRacer.RearRamValidation";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Call(object owner, string method, params object[] args) => VoxelPsychoBugValidation.Call(owner, method, args);
        private static void Set(object owner, string field, object value) => VoxelPsychoBugValidation.Set(owner, field, value);
        private static void Property(object owner, string name, object value) => VoxelPsychoBugValidation.Property(owner, name, value);
        private static void Check(bool value, string message) => VoxelPsychoBugValidation.Check(value, message);

        [MenuItem("Tools/Voxel Racer/Validate Rear Ram Separation")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run from Edit Mode.");
            RunChecks(false);
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }
        [MenuItem("Tools/Voxel Racer/Validate Rear Ram Separation in Play Mode")]
        public static void RunPlayMode()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Start in Edit Mode.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += () =>
            {
                try { RunChecks(true); }
                catch(Exception e) { Directory.CreateDirectory("Temp/RearRam"); File.WriteAllText("Temp/RearRam/PlayValidation.txt", "FAIL: " + e); Debug.LogException(e); }
                finally { SessionState.SetBool(Pending, false); EditorApplication.isPlaying = false; }
            };
        }

        private static void RunChecks(bool playMode)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = playMode ? SceneManager.CreateScene("Temporary rear ram validation") :
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var random = UnityEngine.Random.state;
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            try
            {
                VoxelRacerBootstrap.ReloadGeneratedMaterials();
                foreach (string name in new[] { "BlackInterceptorTuning", "BI_MineLayerTuning", "PsychoBugEnemyTuning" })
                foreach (float dt in new[] { 1f / 30f, 1f / 60f, 1f / 120f, .25f })
                foreach (bool playerFirst in new[] { true, false }) CheckEnemy(name, dt, playerFirst);
                CheckBlockedBug();
                if (playMode) { CheckWreck(false); CheckWreck(true); }
                Directory.CreateDirectory("Temp/RearRam");
                File.WriteAllText("Temp/RearRam/" + (playMode ? "PlayValidation.txt" : "Validation.txt"),
                    "PASS (" + (playMode ? "Play Mode" : "Edit Mode") + " controlled simulation): real rear-impact handlers for Black Interceptor, mine layer and Psycho Bug; swept crossing including boost/recoil offsets; forward bounce; body separation throughout continued boost motion at 30/60/120 FPS and a 250ms long frame in both Update orders; no duplicate damage during contact; lane-change release. " +
                    (playMode ? "Active boost damage multiplier, 50% faster recovery with synchronized counter-steering, captured timing after boost expiry, ordinary restart timing, lethal enemy and civilian wreck momentum/separation also passed. " : "Ordinary recovery timing and synchronized counter-steering also passed. ") + "Temporary objects/tuning, scene and RNG restored.");
                Debug.Log("PASS rear ram separation and bounce.");
            }
            finally
            {
                UnityEngine.Random.state = random;
                SceneManager.SetActiveScene(previous);
                if (playMode) SceneManager.UnloadSceneAsync(scene); else EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void CheckEnemy(string asset, float dt, bool playerFirst)
        {
            using var f = new VoxelPsychoBugValidation.Fixture();
            var tuning = Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/" + asset));
            var go = new GameObject("Rear ram " + asset); go.transform.SetParent(f.Root.transform, false);
            var enemy = go.AddComponent<VoxelEnemyCar>(); enemy.enabled = false;
            VoxelBoostTuning boostTuning = null;
            try
            {
                tuning.vehicleHealth = 1000; tuning.playerDamageVoxelsMin = tuning.playerDamageVoxelsMax = 1;
                f.Player.topSpeed = 35; Property(f.Player, "CurrentSpeed", 75f);
                if (Application.isPlaying)
                {
                    boostTuning = Object.Instantiate(Resources.Load<VoxelBoostTuning>("Boost/BoostBottleUpgradeTuning"));
                    var boost = f.Root.AddComponent<VoxelBoostController>(); boost.Configure(f.Player, boostTuning);
                    Property(boost, "IsBoosting", true);
                }
                enemy.Configure(f.Player, f.Traffic, tuning, f.Road, f.Player.TrackDistance + 10f, 0f);
                // A long boosted step crosses completely through the original contact envelope.
                Property(f.Player, "TrackDistance", f.Player.TrackDistance + 20f);
                Set(f.Player, "boostForwardOffset", 4f); Call(f.Player, "ApplyTrackPose");
                Call(enemy, "CheckVehicleContact"); Call(enemy, "ResolveRearContact");
                var wobble = enemy.GetComponent<VoxelRearRamWobble>();
                Check(wobble != null && wobble.enabled, "Rear impact did not start recovery wobble");
                CheckRecoverySteering(wobble, enemy.transform, f.Player);
                Check(Body(f.Player.transform).max.z + .1f <= Body(enemy.transform).min.z,
                    "Bodies still overlap on the impact frame: " + asset);
                float multiplier = boostTuning != null ? f.Root.GetComponent<VoxelBoostController>().RamDamageMultiplier : 1f;
                Check(Mathf.Abs(enemy.CurrentHealth - (1000f - tuning.playerRamDamage * multiplier)) < .001f,
                    "Rear impact damage missing/duplicated: " + asset);
                Check(enemy.DriveSpeed >= 83f, "Victim did not bounce ahead of boosted player: " + asset);
                float health = enemy.CurrentHealth;
                int frame = 0;
                for (int i = 0; i < Mathf.CeilToInt(4.2f / dt); i++)
                {
                    if (playerFirst) MovePlayer();
                    Set(enemy, "trackDistance", enemy.TrackDistance + 20f * dt);
                    Call(enemy, "ApplyTrackPose"); Call(enemy, "CheckVehicleContact");
                    if (!playerFirst) MovePlayer();
                    Call(enemy, "ResolveRearContact");
                    if (wobble.enabled) Call(wobble, "Advance", dt);
                    Check(Body(f.Player.transform).max.z + .1f <= Body(enemy.transform).min.z,
                        "Boost passed through " + asset + ", dt=" + dt + ", playerFirst=" + playerFirst);
                    Check(enemy.CurrentHealth == health, "Rear contact applied duplicate damage");
                }
                Set(f.Player, "ramLateralOffset", 8f); Call(f.Player, "ApplyTrackPose");
                float before = enemy.TrackDistance; Call(enemy, "ResolveRearContact");
                Property(f.Player, "TrackDistance", f.Player.TrackDistance + 30f); Call(f.Player, "ApplyTrackPose");
                Call(enemy, "ResolveRearContact"); Check(enemy.TrackDistance == before, "Rear guard pulled a victim after lane escape");

                void MovePlayer()
                {
                    Property(f.Player, "TrackDistance", f.Player.TrackDistance + 75f * dt);
                    Set(f.Player, "boostForwardOffset", 4f + Mathf.Sin(frame++ * .2f));
                    Call(f.Player, "ApplyTrackPose");
                }
            }
            finally { Release(go); Object.DestroyImmediate(go); Object.DestroyImmediate(tuning); if(boostTuning!=null) Object.DestroyImmediate(boostTuning); }
        }

        private static void CheckWreck(bool civilian)
        {
            using var f = new VoxelPsychoBugValidation.Fixture();
            var tuning = Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"));
            var boostTuning = Object.Instantiate(Resources.Load<VoxelBoostTuning>("Boost/BoostBottleUpgradeTuning"));
            var go = new GameObject("Rear ram wreck"); go.transform.SetParent(f.Root.transform, false);
            try
            {
                // Recovery speed follows active boost even when the damage bonus is zero.
                boostTuning.boostRamDamageBonusPercent = boostTuning.boostRamDamageWithPloughBonusPercent = 0f;
                var boost = f.Root.AddComponent<VoxelBoostController>(); boost.Configure(f.Player, boostTuning);
                Property(boost, "IsBoosting", true);
                Property(f.Player, "CurrentSpeed", 75f);
                tuning.vehicleHealth = 1; tuning.playerDamageVoxelsMin = tuning.playerDamageVoxelsMax = 1;
                tuning.maximumExplosionVoxelRemovalPercent = 0f;
                if (civilian)
                {
                    f.Traffic.playerDamageVoxelsMin = f.Traffic.playerDamageVoxelsMax = 1;
                    var car = go.AddComponent<VoxelObstacleCar>(); car.enabled = false;
                    car.Configure(f.Player, f.Traffic, true, f.Road, f.Player.TrackDistance + 3f, 0f, 20f);
                    Call(car, "HitCar", new Vector3?(Vector3.forward)); Call(car, "LateUpdate");
                    CheckRecoverySteering(go.GetComponent<VoxelRearRamWobble>(), go.transform, f.Player);
                    Check(Vector3.Dot((Vector3)typeof(VoxelObstacleCar).GetField("velocity", Private).GetValue(car), Vector3.forward) >= 83f,
                        "Civilian wreck did not inherit boost momentum");
                }
                else
                {
                    var enemy = go.AddComponent<VoxelEnemyCar>(); enemy.enabled = false;
                    enemy.Configure(f.Player, f.Traffic, tuning, f.Road, f.Player.TrackDistance + 10f, 0f);
                    Property(f.Player, "TrackDistance", f.Player.TrackDistance + 8f); Call(f.Player, "ApplyTrackPose");
                    Call(enemy, "CheckVehicleContact"); Call(enemy, "ResolveRearContact");
                    Check(enemy.CurrentHealth == 0 && (Vector3)typeof(VoxelEnemyCar).GetField("velocity", Private).GetValue(enemy) is Vector3 v && v.z >= 83f,
                        "Lethal rear hit did not launch the wreck ahead");
                }
                Check(Body(f.Player.transform).max.z + .1f <= Body(go.transform).min.z, "Rear-hit wreck intersects player body");
            }
            finally { Release(go); Object.DestroyImmediate(go); Object.DestroyImmediate(tuning); Object.DestroyImmediate(boostTuning); }
        }

        private static void CheckBlockedBug()
        {
            using var f = new VoxelPsychoBugValidation.Fixture();
            f.EnemyTuning.playerDamageVoxelsMin=f.EnemyTuning.playerDamageVoxelsMax=1;
            f.Spawn(0f,20f); Property(f.Player,"CurrentSpeed",75f);
            var tuning=Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"));
            var go=new GameObject("Blocked rear push QA");go.transform.SetParent(f.Root.transform,false);
            var leader=go.AddComponent<VoxelEnemyCar>();leader.enabled=false;
            try
            {
                leader.Configure(f.Player,f.Traffic,tuning,f.Road,f.Enemy.TrackDistance+
                    f.EnemyTuning.collisionHalfLength+tuning.collisionHalfLength+f.Behaviour.trafficClearance+.1f,0f);
                Set(leader,"currentSpeed",0f);
                ((System.Collections.IList)typeof(VoxelEnemyCar).GetField("psychoTraffic",Private).GetValue(f.Enemy)).Add(leader);
                Property(f.Player,"TrackDistance",f.Player.TrackDistance+30f);Call(f.Player,"ApplyTrackPose");
                Call(f.Enemy,"CheckVehicleContact");Call(f.Enemy,"ResolveRearContact");
                Check(Body(f.Player.transform).max.z+.1f<=Body(f.Enemy.transform).min.z,"Blocked Bug let boost pass through its rear");
                Check(Body(f.Enemy.transform).max.z<Body(leader.transform).min.z,"Rear shove pushed the Bug through its traffic blocker");
            }
            finally { Release(go);Object.DestroyImmediate(go);Object.DestroyImmediate(tuning); }
        }

        private static void CheckRecoverySteering(VoxelRearRamWobble wobble, Transform car, VoxelCarController rammer)
        {
            var boost = rammer.GetComponentInParent<VoxelBoostController>();
            bool boosted = boost != null && boost.isActiveAndEnabled && boost.IsBoosting;
            float speed = boosted ? 1.5f : 1f;
            // Boost ending after contact must not slow an already-started recovery.
            if (boosted) Property(boost, "IsBoosting", false);
            Transform body=(Transform)typeof(VoxelRearRamWobble).GetField("body",Private).GetValue(wobble);
            Quaternion rest=body.localRotation;
            var pivots=car.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Rear Recovery Front Steering").ToArray();
            Check(pivots.Length==2,"Recovery must steer both front wheels only");
            Transform wheel=pivots[0].GetChild(0);
            wheel.Rotate(Vector3.right,35f,Space.Self);Quaternion spin=wheel.localRotation;
            Call(wobble,"Advance",.2f / speed);Quaternion first=body.localRotation;
            CheckCounterSteer();
            Call(wobble,"Advance",.08f / speed);
            Check(Quaternion.Angle(first,body.localRotation)>1f,"Recovery paused instead of flowing toward its next angle");
            Call(wobble,"Advance",.22f / speed);CheckCounterSteer();
            Check(Mathf.DeltaAngle(0,(Quaternion.Inverse(rest)*body.localRotation).eulerAngles.y)<0,"Recovery did not reach the opposite angle");
            Check(Quaternion.Angle(spin,wheel.localRotation)<.01f,"Counter-steering overwrote wheel axle spin");
            Call(wobble,"Advance",1.45f - .5f / speed);
            Check(wobble.enabled != boosted, "Recovery must finish by 1.45s only when boosted at impact");
            if (wobble.enabled) Call(wobble,"Advance",.7f);
            Check(!wobble.enabled && Quaternion.Angle(rest,body.localRotation)<.01f && pivots.All(p=>Quaternion.Angle(p.localRotation,Quaternion.identity)<.01f),
                "Recovery did not straighten the body and both front wheels");
            // The next non-boosted hit must reset to ordinary timing.
            wobble.Play(rammer);
            Call(wobble, "Advance", 1.45f);
            Check(wobble.enabled, "Non-boosted restart retained accelerated recovery timing");
            if (boosted) Property(boost, "IsBoosting", true);
            wobble.Play(rammer);

            void CheckCounterSteer()
            {
                float yaw=Mathf.DeltaAngle(0,(Quaternion.Inverse(rest)*body.localRotation).eulerAngles.y);
                foreach(var pivot in pivots)
                {
                    float steering=Mathf.DeltaAngle(0,pivot.localEulerAngles.y);
                    Check(yaw*steering<0 && yaw*(yaw+steering)<0,"Front wheels did not counter-steer against body yaw");
                }
            }
        }

        private static Bounds Body(Transform root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.GetComponentInParent<VoxelEnemyHealthBar>() == null).ToArray();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        private static void Release(GameObject root)
        {
            foreach (var bar in root.GetComponentsInChildren<VoxelEnemyHealthBar>(true)) Call(bar, "OnDestroy");
        }
    }
}
