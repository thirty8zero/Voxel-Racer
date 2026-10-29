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
    public static class VoxelPsychoBugValidation
    {
        internal const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static object Call(object target,string method,params object[] args) => target.GetType().GetMethod(method,Private).Invoke(target,args);
        internal static void Set(object target,string field,object value) => target.GetType().GetField(field,Private).SetValue(target,value);
        internal static void Property(object target,string property,object value) => target.GetType().GetProperty(property).SetValue(target,value);
        internal static void Check(bool value,string message) { if(!value)throw new InvalidOperationException(message); }

        internal sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly VoxelCarController Player;
            internal readonly EndlessVoxelRoad Road;
            internal readonly VoxelPsychoBugTuning Behaviour;
            internal readonly VoxelEnemyVehicleTuning EnemyTuning;
            internal readonly VoxelObstacleCarTuning Traffic;
            internal VoxelEnemyCar Enemy;
            internal Fixture()
            {
                Root=new GameObject("Temporary Psycho Bug QA");
                var car=new GameObject("Player QA");car.transform.SetParent(Root.transform,false);
                Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab,car.transform);
                Player=car.AddComponent<VoxelCarController>();Player.enabled=false;Player.topSpeed=20;Player.debrisVoxelsPerDamagedVoxel=0;
                Set(Player,"currentLane",2);Set(Player,"previousLane",2);
                Player.SetLaneLayout(5,3);Player.ResetIntegrityBaseline();Property(Player,"CurrentSpeed",20f);Property(Player,"TrackDistance",100f);
                var roadRoot=new GameObject("Road QA");roadRoot.transform.SetParent(Root.transform,false);
                Road=roadRoot.AddComponent<EndlessVoxelRoad>();Road.enabled=false;Road.laneCount=5;Road.roadWidth=15;
                Road.minimumCactiPerSegment=Road.maximumCactiPerSegment=0;Road.turnChancePerSegment=0;Road.segmentCount=8;
                Road.BuildInitialRoad();Player.SetTrack(Road,100);Call(Player,"ApplyTrackPose");
                Traffic=Object.Instantiate(VoxelObstacleCarTuning.Load());
                // Counter-ram health/recoil is exercised; debris emission is covered by live checks.
                Traffic.obstacleDamageVoxelsMin=Traffic.obstacleDamageVoxelsMax=0;
                EnemyTuning=Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/PsychoBugEnemyTuning"));
                Behaviour=Object.Instantiate(EnemyTuning.psychoBug);EnemyTuning.psychoBug=Behaviour;
                Behaviour.feintChance=0;Behaviour.sideRamChance=1;Behaviour.initialRamChance=1;Behaviour.damageEvasionChance=1;
                Behaviour.minimumAttackCooldown=Behaviour.maximumAttackCooldown=.1f;
                Behaviour.sideRamDamageMin=Behaviour.sideRamDamageMax=3;
            }
            internal void Spawn(float offset,float gap=0)
            {
                Set(Player,"ramLateralOffset",0f);Set(Player,"ramForwardOffset",0f);Set(Player,"ramResponseStartedAt",-1f);
                if(Enemy!=null) { Call(Enemy,"OnDisable");ReleaseBars(Enemy.gameObject);Object.DestroyImmediate(Enemy.gameObject); }
                var go=new GameObject("Psycho Bug Enemy QA");go.transform.SetParent(Root.transform,false);
                Enemy=go.AddComponent<VoxelEnemyCar>();Enemy.enabled=false;
                Enemy.Configure(Player,Traffic,EnemyTuning,Road,Player.TrackDistance+gap,offset);
                Set(Enemy,"currentSpeed",20f);Call(Enemy,"OnEnable");
            }
            internal void Step(float dt,bool contacts=true)
            {
                Property(Player,"TrackDistance",Player.TrackDistance+Player.CurrentSpeed*dt);Call(Player,"ApplyTrackPose");
                Call(Enemy,"AdvancePsychoBug",dt);Call(Enemy,"ApplyTrackPose");
                if(contacts)Call(Enemy,"CheckVehicleContact");
            }
            internal void Wait(PsychoBugPhase phase,float timeout=12)
            {
                for(float t=0;t<timeout && Enemy.PsychoPhase!=phase;t+=.02f) Step(.02f);
                Check(Enemy.PsychoPhase==phase,"Did not reach "+phase+"; current="+Enemy.PsychoPhase+", gap="+(Enemy.TrackDistance-Player.TrackDistance)+", speed="+Enemy.DriveSpeed+", lane="+Enemy.LaneOffset);
            }
            public void Dispose()
            {
                if(Enemy!=null)Call(Enemy,"OnDisable");ReleaseBars(Root);Object.DestroyImmediate(Root);
                Object.DestroyImmediate(Traffic);Object.DestroyImmediate(EnemyTuning);Object.DestroyImmediate(Behaviour);
            }
        }
        private static void ReleaseBars(GameObject root)
        {
            foreach(var bar in root.GetComponentsInChildren<VoxelEnemyHealthBar>(true))Call(bar,"OnDestroy");
        }

        [MenuItem("Tools/Voxel Racer/Validate Psycho Bug Enemy")]
        public static void Run() => RunChecks(true);

        [MenuItem("Tools/Voxel Racer/Validate Psycho Bug Behaviour")]
        public static void RunBehaviour() => RunChecks(false);

        [MenuItem("Tools/Voxel Racer/Validate Psycho Bug Ram Alignment")]
        public static void RunRamAlignment()
        {
            Check(!Application.isPlaying,"Run editor validation outside Play Mode.");
            var active=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var random=UnityEngine.Random.state;
            using var saved=new VoxelGarageUpgradeDockValidation.SavedState();
            try { VoxelRacerBootstrap.ReloadGeneratedMaterials(); CheckRamAlignment(); }
            finally { UnityEngine.Random.state=random;SceneManager.SetActiveScene(active);EditorSceneManager.CloseScene(scene,true); }
        }

        private static void CheckRamAlignment()
        {
            using var f=new Fixture();
            EditorUtility.CopySerialized(Resources.Load<VoxelPsychoBugTuning>("EnemyVehicles/PsychoBugBehaviour"),f.Behaviour);
            f.Behaviour.shootingWindows=false;f.Behaviour.damageEvasion=false;f.Behaviour.weaveChance=0;f.Behaviour.feintChance=0;
            f.Behaviour.sideRamDamageMin=f.Behaviour.sideRamDamageMax=0;
            f.Road.laneCount=3;f.Road.roadWidth=16;f.Player.topSpeed=35;
            Set(f.Player,"currentLane",1);Set(f.Player,"previousLane",1);f.Player.SetLaneLayout(3,16f/3);
            var report=new System.Text.StringBuilder();
            var failures=new System.Collections.Generic.List<string>();
            foreach(float dt in new[]{1f/120,1f/60,1f/30,1f/15,.25f})
            foreach(float speed in new[]{35f,20f,14.1f})
            foreach(float side in new[]{-16f/3,16f/3})
            foreach(float lead in new[]{200f,-30f})
            foreach(bool playerFirst in new[]{true,false})
            {
                Property(f.Player,"TrackDistance",100f);Property(f.Player,"CurrentSpeed",speed);
                f.Spawn(side,lead);Set(f.Enemy,"currentSpeed",speed);
                Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);
                float minimum=lead,maximum=lead,warningGap=999,slamGap=999,time=0;bool sawWarning=false,sawSlam=false;
                bool alignedWarning=true,alignedSlam=true;
                while(time<8 && (!sawWarning || f.Enemy.PsychoPhase==PsychoBugPhase.Warning || f.Enemy.PsychoPhase==PsychoBugPhase.Slamming))
                {
                    float previous=f.Enemy.TrackDistance;
                    if(!playerFirst)Call(f.Enemy,"Update");
                    Property(f.Player,"TrackDistance",f.Player.TrackDistance+f.Player.CurrentSpeed*dt);Call(f.Player,"ApplyTrackPose");
                    if(playerFirst)Call(f.Enemy,"Update");
                    Check(f.Enemy.TrackDistance==previous,"Bug moved during Update instead of waiting for player movement");
                    // Production calls this from LateUpdate, after either Update ordering.
                    Call(f.Enemy,"AdvancePsychoBug",dt);Call(f.Enemy,"ApplyTrackPose");
                    float gap=f.Enemy.TrackDistance-f.Player.CollisionTrackPosition.y;
                    if(f.Enemy.PsychoPhase==PsychoBugPhase.Aligning){minimum=Mathf.Min(minimum,gap);maximum=Mathf.Max(maximum,gap);}
                    if(f.Enemy.PsychoPhase==PsychoBugPhase.Warning)
                    {
                        if(!sawWarning)warningGap=gap;sawWarning=true;
                        alignedWarning &= Mathf.Abs(gap)<=f.Behaviour.alignmentTolerance+.01f && Mathf.Abs(f.Enemy.DriveSpeed-speed)<.01f;
                    }
                    if(f.Enemy.PsychoPhase==PsychoBugPhase.Slamming)
                    {
                        if(!sawSlam)slamGap=gap;sawSlam=true;
                        alignedSlam &= Mathf.Abs(gap)<=f.Behaviour.alignmentTolerance+.01f;
                    }
                    time+=dt;
                }
                bool passed=sawWarning && sawSlam && alignedWarning && alignedSlam &&
                    (lead>0 ? minimum>=-.01f : maximum<=.01f);
                string line=(passed?"PASS":"FAIL")+": dt="+dt.ToString("0.000")+", speed="+speed+", side="+side+", lead="+lead+", playerFirst="+playerFirst+
                    ", gap range="+minimum.ToString("0.00")+".."+maximum.ToString("0.00")+", warning="+warningGap.ToString("0.00")+", slam="+slamGap.ToString("0.00")+", phase="+f.Enemy.PsychoPhase;
                report.AppendLine(line);if(!passed)failures.Add(line);
            }
            Directory.CreateDirectory("Temp/PsychoBug");File.WriteAllText("Temp/PsychoBug/RamAlignment.txt",report.ToString());
            Check(failures.Count==0,"Ram alignment failures: "+string.Join("\n",failures));
            foreach(float speed in new[]{0f,35f})
            foreach(float side in new[]{-16f/3,16f/3})
            {
                Property(f.Player,"CurrentSpeed",20f);f.Spawn(side);Call(f.Enemy,"BeginPsychoRam");
                Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Slamming);
                int integrity=f.Player.RemainingIntegrityVoxels;
                Property(f.Player,"CurrentSpeed",speed);f.Step(.25f);
                Check(f.Enemy.PsychoPhase==PsychoBugPhase.Recovering && f.Player.RemainingIntegrityVoxels==integrity &&
                    Mathf.Abs(f.Enemy.LaneOffset-f.Player.CollisionTrackPosition.x)>=f.EnemyTuning.collisionHalfWidth+f.Behaviour.playerCollisionHalfWidth,
                    "A long-frame speed change delivered a front/rear hit instead of aborting the side slam");
            }
            File.AppendAllText("Temp/PsychoBug/RamAlignment.txt","PASS: abrupt acceleration/braking during a committed slam cancels before lateral contact, both sides, 250ms step.\n");
            Debug.Log("PASS Psycho Bug ram alignment: both sides, ahead/behind, speeds 14.1/20/35, 15/30/60/120 FPS and 250ms steps, either Update order.");
        }

        private static void RunChecks(bool validateAssets)
        {
            Check(!Application.isPlaying,"Run editor validation outside Play Mode.");
            var active=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var random=UnityEngine.Random.state;
            using var saved=new VoxelGarageUpgradeDockValidation.SavedState();
            Fixture f=null;
            try
            {
                VoxelRacerBootstrap.ReloadGeneratedMaterials();
                if(validateAssets)ValidateAssets();
                else CheckInspector(Resources.Load<VoxelPsychoBugTuning>("EnemyVehicles/PsychoBugBehaviour"));
                f=new Fixture();
                f.Behaviour.shootingWindows=false;
                foreach(float side in new[]{-3f,3f})
                {
                    f.Spawn(side);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);f.Wait(PsychoBugPhase.Warning);
                    Check(f.Enemy.LaneOffset*side>0,"Attack did not stay on its tested side");
                    float initial=Mathf.Abs(f.Enemy.LaneOffset-f.Player.CurrentLaneOffset);
                    int integrity=f.Player.RemainingIntegrityVoxels;
                    int normalDamage=f.Player.damageVoxelsPerHit;
                    for(int i=0;i<45;i++)f.Step(.02f);
                    Check(f.Enemy.PsychoPhase==PsychoBugPhase.Warning && f.Player.RemainingIntegrityVoxels==integrity,"Warning shortened or applied early damage");
                    Check(Mathf.Abs(f.Enemy.LaneOffset-f.Player.CurrentLaneOffset)>initial+.1f,"Warning did not visibly pull away");
                    f.Wait(PsychoBugPhase.Slamming,1);Set(f.Player,"nextDamageTime",-1f);
                    f.Wait(PsychoBugPhase.Recovering,1);
                    Check(f.Player.RemainingIntegrityVoxels==integrity-3,"Side slam did not apply exactly one configured hit");
                    Check(f.Player.damageVoxelsPerHit==normalDamage,"Slam left a temporary player damage override");
                    int damaged=f.Player.RemainingIntegrityVoxels;
                    for(int i=0;i<15;i++)f.Step(.02f);
                    Check(f.Player.RemainingIntegrityVoxels==damaged,"Recovery repeated attack damage");
                    Set(f.Player,"ramLateralOffset",0f);
                }
                f.Spawn(3);Call(f.Enemy,"BeginPsychoRam");
                Property(f.Player,"TrackDistance",f.Player.TrackDistance+20);int before=f.Player.RemainingIntegrityVoxels;
                f.Step(.02f);Check(f.Enemy.PsychoPhase==PsychoBugPhase.Recovering && f.Player.RemainingIntegrityVoxels==before,"Boost escape was tracked or damaged");
                f.Spawn(3);Call(f.Enemy,"BeginPsychoRam");
                Call(f.Enemy,"RamByPlayer",new Vector3?(Vector3.right));
                Check(f.Enemy.PsychoPhase==PsychoBugPhase.Staggered,"Player ram did not interrupt windup");
                f.Spawn(3);f.Behaviour.feintChance=1;Call(f.Enemy,"BeginPsychoRam");
                f.Wait(PsychoBugPhase.Recovering,2);Check(f.Enemy.PsychoPhase!=PsychoBugPhase.Slamming,"Feint launched a slam");f.Behaviour.feintChance=0;

                f.Behaviour.shootingWindows=true;f.Behaviour.minimumShootingDuration=f.Behaviour.maximumShootingDuration=1;
                f.Behaviour.damageEvasion=false;
                f.Spawn(0,14);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.PositioningForShot);f.Wait(PsychoBugPhase.Exposed,3);float lane=f.Enemy.LaneOffset;
                float health=f.Enemy.CurrentHealth;
                f.Enemy.TakeHostileProjectileHit(null,1,f.Enemy.transform.position,Vector3.forward);
                Check(f.Enemy.CurrentHealth==health-1,"Exposed enemy could not take bullet damage");
                for(int i=0;i<40;i++)f.Step(.02f);
                Check(f.Enemy.PsychoPhase==PsychoBugPhase.Exposed && Mathf.Abs(f.Enemy.LaneOffset-lane)<.001f,"Shot window dodged or ended early");
                f.Wait(PsychoBugPhase.Warning,12);
                f.Spawn(0,20);f.Behaviour.sideRamEnabled=false;f.Behaviour.weaveChance=0;
                f.Behaviour.damageEvasion=true;f.Behaviour.damageReactionDelay=.5f;
                Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Hunting);
                f.Enemy.TakeHostileProjectileHit(null,1,f.Enemy.transform.position,Vector3.forward);
                f.Step(.2f,false);Check(Mathf.Abs(f.Enemy.LaneOffset)<.001f,"Damage evasion ignored reaction delay");
                f.Step(.4f,false);Check(Mathf.Abs(f.Enemy.LaneOffset)>.1f,"Delayed damage evasion did not change lane");
                f.Behaviour.sideRamEnabled=true;
                f.Behaviour.damageEvasion=false;
                CheckTraffic(f);
                CheckReservation(f);
                CheckAggression();
                CheckActiveMovement();
                CheckOncomingAndApproachSpeed();
                CheckEnemySpeedFloors();
                CheckFastRamReturn();
                CheckRamOpeningAndSlowdown();
                CheckRamAlignment();
                CheckLowSpeedAndBarrels();
                CheckBlockedMergeAndReturn();
                // Explicit opt-out retains optional stationary/reverse positioning.
                f.Behaviour.fastRamReturn=false;
                f.Behaviour.minimumAlignmentSpeed=f.Behaviour.minimumRamSpeed=f.Behaviour.minimumDrivingSpeed=0;
                Property(f.Player,"CurrentSpeed",0f);f.Spawn(3,30);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);Set(f.Enemy,"currentSpeed",0f);
                float track=f.Enemy.TrackDistance;for(int i=0;i<25;i++)f.Step(.02f,false);
                Check(f.Enemy.TrackDistance>=track && f.Enemy.DriveSpeed>=0,"Reverse disabled still reversed");
                f.Behaviour.allowReverse=true;f.Step(.1f,false);Check(f.Enemy.DriveSpeed<0,"Configured reverse did not work");
                Directory.CreateDirectory("Temp/PsychoBug");File.WriteAllText("Temp/PsychoBug/Validation.txt",
                    "PASS (Edit Mode): " + (validateAssets ? "Model, authored meshes/materials, all enemy pools and editable Odin foldouts. " : "Behaviour-only validation; asset/pool registration not checked. ") +
                    "Left/right one-second warning and pull-away, single configured swept slam damage, recovery, player-ram interruption, boost escape, feint, brief shot window and health damage; forward/back positioning, optional reverse, traffic corridor/oncoming/rear clearance, following speed cap, blocked attack and attacker reservation. Authored defaults approach from 200m on a three-lane 16m road, repeat three natural rams, maintain independent cruise when player brakes, and trigger a passing-alignment ram with random attack chance zero. Hit reactions interrupt exposure, dodge both road edges, survive repeated hits/in-progress merges, accelerate when no lane is available, respect obstacles/cooldown and preserve counter-ram stagger. Shooting placement times out promptly; repeated attacks return within five seconds. Temporary scene/state/random/currency/purchases restored.");
                File.AppendAllText("Temp/PsychoBug/Validation.txt", " Low-speed alignment/committed-ram cancellation, shooting forward floor, editable thresholds, braking escape, real barrel avoidance, long-frame emergency clearance, blocked-road stopping and destroyed-barrel release also passed.");
                File.AppendAllText("Temp/PsychoBug/Validation.txt", " New checks passed: 14m/s deliberate approach floor, slow-player breakaway without repeated alignment, actual civilian oncoming prediction beyond the old horizon, retreat from an unsafe unfinished merge, swept diagonal clearance and head-on separation in either vehicle Update order including long frames.");
                File.AppendAllText("Temp/PsychoBug/Validation.txt", " Ordinary enemy minimum speed passed at spawn/approach/engagement, post-ram stopped/slow/fast player matching, editable/zero floors and lane-change burst; Psycho Bug follows a floor-limited Black Interceptor without crawling when the player is stopped. See Temp/EnemyMinimumSpeed/Validation.txt.");
                File.AppendAllText("Temp/PsychoBug/Validation.txt", " Blocked player-lane merges return to a centre with weaving disabled; blocked original lanes select the next clear centre; emergency-buffer contact permits separation without crossing through traffic. Aggressive authored 200m approaches reach real warning/slam/damage at 35, 20 and 14.1m/s, including progress beyond the former timeout.");
                File.AppendAllText("Temp/PsychoBug/Validation.txt", " Opening ram chance is rolled once per spawn, with 0%/50%/100% checks and delayed deliberate/passing attacks. Editable 50m slowdown range and player braking/acceleration approaches pass from ahead/behind on both sides at 30/120 FPS and 250ms steps. See RamOpeningAndSlowdown.txt.");
                Debug.Log("PASS Psycho Bug " + (validateAssets ? "model, integration and " : "") + "behaviour checks (Edit Mode).");
            }
            catch(Exception e){Directory.CreateDirectory("Temp/PsychoBug");File.WriteAllText("Temp/PsychoBug/Validation.txt","FAIL: "+e);throw;}
            finally { f?.Dispose();UnityEngine.Random.state=random;SceneManager.SetActiveScene(active);EditorSceneManager.CloseScene(scene,true); }
        }
        private static void CheckAggression()
        {
            using var f=new Fixture();
            // Exercise the shipped values and the first track's wider lanes/spawn lead.
            EditorUtility.CopySerialized(Resources.Load<VoxelPsychoBugTuning>("EnemyVehicles/PsychoBugBehaviour"),f.Behaviour);
            f.Behaviour.initialRamChance=1;f.Behaviour.feintChance=0;f.Behaviour.damageEvasion=false;
            f.Road.laneCount=3;f.Road.roadWidth=16;
            f.Player.topSpeed=32;Property(f.Player,"CurrentSpeed",32f);
            Set(f.Player,"currentLane",1);Set(f.Player,"previousLane",1);f.Player.SetLaneLayout(3,16f/3);Call(f.Player,"ApplyTrackPose");
            f.Spawn(16f/3,200);
            Set(f.Enemy,"currentSpeed",32f);
            Check(f.Enemy.PsychoPhase==PsychoBugPhase.Hunting,"Spawn still waits for an exposure window");
            f.Wait(PsychoBugPhase.Warning,18);
            for(int attack=0;attack<3;attack++)
            {
                f.Wait(PsychoBugPhase.Slamming,1.2f);Set(f.Player,"nextDamageTime",-1f);
                f.Wait(PsychoBugPhase.Recovering,1);
                Check(Mathf.Abs(f.Enemy.LaneOffset)>4,"Successful ram did not restore its adjacent recovery lane");
                // This deterministic clock advances AI, not Time.time-based player recoil; settle that recoil between attacks.
                Set(f.Player,"ramLateralOffset",0f);Set(f.Player,"ramForwardOffset",0f);Set(f.Player,"ramResponseStartedAt",-1f);
                Call(f.Player,"ApplyTrackPose");
                if(attack<2)f.Wait(PsychoBugPhase.Warning,5);
            }
            f.Behaviour.sideRamEnabled=false;f.Behaviour.shootingWindows=false;
            f.Player.topSpeed=20;
            Property(f.Player,"CurrentSpeed",0f);f.Spawn(16f/3,-40);
            for(int i=0;i<50;i++)f.Step(.02f,false);
            Check(f.Enemy.DriveSpeed>18,"Player braking dragged down independent cruise speed");
            f.Behaviour.sideRamEnabled=true;f.Behaviour.sideRamChance=0;f.Behaviour.ramOnPassingAlignment=true;
            f.Behaviour.weaveChance=0;f.Behaviour.minimumAttackCooldown=f.Behaviour.maximumAttackCooldown=0;
            Property(f.Player,"CurrentSpeed",20f);f.Spawn(16f/3,-3);
            Property(f.Player,"CurrentSpeed",16f);
            f.Wait(PsychoBugPhase.Warning,5);f.Wait(PsychoBugPhase.Slamming,1.2f);Set(f.Player,"nextDamageTime",-1f);
            f.Wait(PsychoBugPhase.Recovering,1);
            f.Behaviour.ramOnPassingAlignment=false;f.Spawn(16f/3,0);
            for(int i=0;i<10;i++)f.Step(.02f,false);
            Check(f.Enemy.PsychoPhase==PsychoBugPhase.Hunting,"Disabled passing-alignment trigger still attacked");
        }
        private static void CheckActiveMovement()
        {
            using var f=new Fixture();
            EditorUtility.CopySerialized(Resources.Load<VoxelPsychoBugTuning>("EnemyVehicles/PsychoBugBehaviour"),f.Behaviour);
            f.Behaviour.initialRamChance=1;f.Behaviour.feintChance=0;f.Behaviour.weaveChance=0;
            // A hit must break even the deliberate shooting window and choose the only
            // in-bounds escape at either road edge; test with the actual authored delay.
            foreach(float side in new[]{-3f,0f,3f})
            {
                f.Spawn(side,14);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Exposed);
                Set(f.Enemy,"psychoExposureDuration",10f);
                f.Enemy.TakeHostileProjectileHit(null,1,f.Enemy.transform.position,Vector3.forward);
                for(int i=0;i<4;i++)f.Step(.02f,false);
                Check(f.Enemy.PsychoPhase==PsychoBugPhase.Exposed && Mathf.Abs(f.Enemy.LaneOffset-side)<.001f,
                    "Damage reaction skipped its configured delay");
                // Sustained fire cannot reset the reaction timer every time a bullet lands.
                f.Enemy.TakeHostileProjectileHit(null,1,f.Enemy.transform.position,Vector3.forward);
                for(int i=0;i<5;i++)f.Step(.02f,false);
                Check(f.Enemy.PsychoPhase==PsychoBugPhase.Hunting && Mathf.Abs(f.Enemy.LaneOffset-side)>.1f &&
                    f.Enemy.DriveSpeed>20,"Exposure hit did not promptly dodge and accelerate at lane "+side);
                f.Enemy.TakeHostileProjectileHit(null,1,f.Enemy.transform.position,Vector3.forward);
                Check(float.IsPositiveInfinity((float)typeof(VoxelEnemyCar).GetField("psychoEvasionAt",Private).GetValue(f.Enemy)),
                    "Evasion cooldown permitted another immediate reaction");
                f.Wait(PsychoBugPhase.Warning,5);
            }
            // Damage during a merge must react without waiting to settle in the original lane.
            f.Spawn(0,20);Set(f.Enemy,"targetLaneOffset",3f);f.Step(.04f,false);
            f.Enemy.TakeHostileProjectileHit(null,1,f.Enemy.transform.position,Vector3.forward);
            for(int i=0;i<8;i++)f.Step(.02f,false);
            Check(float.IsPositiveInfinity((float)typeof(VoxelEnemyCar).GetField("psychoEvasionAt",Private).GetValue(f.Enemy)) &&
                f.Enemy.DriveSpeed>20,"An unfinished merge suppressed the hit reaction");

            f.Spawn(0,90);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.PositioningForShot);
            for(int i=0;i<105;i++)f.Step(.02f,false);
            Check(f.Enemy.PsychoPhase!=PsychoBugPhase.PositioningForShot && f.Enemy.PsychoPhase!=PsychoBugPhase.Exposed,
                "Unreachable shooting position still delays attacks for the full approach timeout");

            f.Spawn(3,14);Call(f.Enemy,"RamByPlayer",new Vector3?(Vector3.right));
            f.Enemy.TakeHostileProjectileHit(null,1,f.Enemy.transform.position,Vector3.forward);f.Step(.2f,false);
            Check(f.Enemy.PsychoPhase==PsychoBugPhase.Staggered,"Projectile escape shortened a counter-ram stagger");

            f.Road.laneCount=1;f.Road.roadWidth=3;f.Player.SetLaneLayout(1,3);
            Set(f.Player,"currentLane",0);Set(f.Player,"previousLane",0);Call(f.Player,"ApplyTrackPose");
            f.Spawn(0,14);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Exposed);Set(f.Enemy,"psychoExposureDuration",10f);
            f.Enemy.TakeHostileProjectileHit(null,1,f.Enemy.transform.position,Vector3.forward);
            for(int i=0;i<10;i++)f.Step(.02f,false);
            Check(Mathf.Abs(f.Enemy.LaneOffset)<.001f && f.Enemy.DriveSpeed>20 && f.Enemy.PsychoPhase==PsychoBugPhase.Hunting,
                "No safe adjacent lane failed to fall back to acceleration");
            var plain=Object.Instantiate(f.EnemyTuning);plain.psychoBug=null;
            var go=new GameObject("Escape blocker QA");go.transform.SetParent(f.Root.transform,false);
            var blocker=go.AddComponent<VoxelEnemyCar>();blocker.enabled=false;
            try
            {
                blocker.Configure(f.Player,f.Traffic,plain,f.Road,f.Enemy.TrackDistance+8,0);Set(blocker,"currentSpeed",0f);
                Set(f.Enemy,"psychoNextScan",0f);
                for(int i=0;i<5;i++)f.Step(.04f,false);
                Check(f.Enemy.TrackDistance<=blocker.TrackDistance-f.EnemyTuning.collisionHalfLength-plain.collisionHalfLength,
                    "Acceleration escape crossed a blocking vehicle");
                Check(f.Enemy.DriveSpeed<20,"Acceleration escape ignored traffic braking");
            }
            finally { Call(blocker,"OnDisable");ReleaseBars(go);Object.DestroyImmediate(go);Object.DestroyImmediate(plain); }
        }

        private static void CheckOncomingAndApproachSpeed()
        {
            using var f=new Fixture();
            f.Behaviour.weaveChance=0;f.Behaviour.shootingWindows=false;
            foreach(float playerSpeed in new[]{0f,6f,8f,13.9f})
            {
                Property(f.Player,"CurrentSpeed",playerSpeed);f.Spawn(3,8);
                Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);
                for(int i=0;i<50;i++)
                {
                    f.Step(.02f,false);
                    Check(f.Enemy.PsychoPhase!=PsychoBugPhase.Aligning && f.Enemy.PsychoPhase!=PsychoBugPhase.Warning,
                        "Bug repeatedly aligned with a player below the approach-speed guard");
                    Check(f.Enemy.DriveSpeed>=19.99f,"Bug crawled to match a slow player");
                }
            }
            Property(f.Player,"CurrentSpeed",20f);f.Spawn(3,12);
            f.Behaviour.fastRamReturn=false;
            f.Behaviour.minimumDrivingSpeed=6f;
            for(int i=0;i<100 && f.Enemy.PsychoPhase!=PsychoBugPhase.Warning;i++)
            {
                f.Step(.02f,false);
                Check(f.Enemy.DriveSpeed>=f.Behaviour.minimumRamSpeed-.01f,"Clear-road ram return drove below its launch floor");
            }
            f.Wait(PsychoBugPhase.Warning,4);
            f.Behaviour.minimumDrivingSpeed=20f;
            f.Behaviour.fastRamReturn=true;

            f.Road.laneCount=3;f.Road.roadWidth=16;f.Player.SetLaneLayout(3,16f/3);
            Set(f.Player,"currentLane",1);Set(f.Player,"previousLane",1);Call(f.Player,"ApplyTrackPose");
            f.Behaviour.sideRamEnabled=false;f.Spawn(0,40);
            var go=new GameObject("Actual oncoming traffic QA");go.transform.SetParent(f.Root.transform,false);
            var oncoming=go.AddComponent<VoxelObstacleCar>();oncoming.enabled=false;
            try
            {
                oncoming.Configure(f.Player,f.Traffic,false,f.Road,f.Enemy.TrackDistance+80,16f/3,25f);
                f.Step(.001f,false);
                Check(!(bool)Call(f.Enemy,"PsychoRouteSafe",16f/3,true),
                    "Actual oncoming civilian just beyond the old prediction horizon permitted a merge");
                // A new threat appears during an unfinished merge; retreat to the original lane.
                Set(f.Enemy,"laneOffset",.5f);Set(f.Enemy,"targetLaneOffset",16f/3);
                Set(f.Enemy,"psychoNextDecision",0f);f.Step(.1f,false);
                Check(Mathf.Abs(f.Enemy.LaneOffset)<.1f,"Unsafe unfinished merge did not retreat to its clear lane");

                // The final sweep must reject a diagonal step even when its endpoint has
                // already passed the car. Forward-only and endpoint checks miss this path.
                Vector2 start=new(0,f.Enemy.TrackDistance);
                Set(oncoming,"trackDistance",start.y+18);Set(f.Enemy,"laneOffset",16f/3);Set(f.Enemy,"trackDistance",start.y+30);
                Call(f.Enemy,"ResolvePsychoTrafficStep",start,1f);
                Vector2 relativeStart=start-new Vector2(oncoming.LaneOffset,oncoming.TrackDistance);
                Vector2 relativeEnd=new Vector2(f.Enemy.LaneOffset-oncoming.LaneOffset,f.Enemy.TrackDistance-oncoming.TrackDistance);
                Vector2 size=new(f.EnemyTuning.collisionHalfWidth+oncoming.TrafficHalfWidth,
                    f.EnemyTuning.collisionHalfLength+oncoming.TrafficHalfLength);
                Check(!VoxelVehicleCollision.Sweep(relativeStart,relativeEnd,size,out _),"Diagonal emergency clearance crossed a civilian");

                // Head-on traffic must remain separated for both update orders, including a
                // long frame. Merely stopping the Bug still lets the civilian pass through it.
                f.Behaviour.weaveThroughTraffic=false;
                foreach(bool civilianFirst in new[]{false,true})
                {
                    f.Spawn(16f/3,60);Set(oncoming,"laneOffset",16f/3);Set(oncoming,"trackDistance",f.Enemy.TrackDistance+60);
                    for(int i=0;i<40;i++)
                    {
                        float dt=i==4?.5f:.1f;
                        Vector2 before=new(f.Enemy.LaneOffset-oncoming.LaneOffset,f.Enemy.TrackDistance-oncoming.TrackDistance);
                        if(civilianFirst) AdvanceCivilian();
                        f.Step(dt,false);
                        if(!civilianFirst) AdvanceCivilian();
                        Vector2 after=new(f.Enemy.LaneOffset-oncoming.LaneOffset,f.Enemy.TrackDistance-oncoming.TrackDistance);
                        Check(!VoxelVehicleCollision.Sweep(before,after,size,out _),"Head-on civilian crossed the Bug; civilian first="+civilianFirst);
                        void AdvanceCivilian()
                        {
                            float speed=(float)Call(oncoming,"GetSafeTrafficSpeed",25f,dt);
                            Set(oncoming,"travelSpeed",speed);Set(oncoming,"trackDistance",oncoming.TrackDistance-speed*dt);
                            Call(oncoming,"ApplyTrackPose");
                        }
                    }
                    Check(f.Enemy.DriveSpeed<.1f,"Fully blocked head-on corridor did not stop the Bug safely");
                }
            }
            finally { ReleaseBars(go);Object.DestroyImmediate(go); }
        }

        [MenuItem("Tools/Voxel Racer/Validate Enemy Minimum Speeds")]
        public static void RunMinimumSpeeds() => RunBehaviour();

        private static void CheckEnemySpeedFloors()
        {
            using var f = new Fixture();
            var tuning = Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"));
            var go = new GameObject("Black Interceptor Minimum Speed QA");
            go.transform.SetParent(f.Root.transform, false);
            var black = go.AddComponent<VoxelEnemyCar>(); black.enabled = false;
            try
            {
                Check(tuning.minimumDrivingSpeed == 20f, "Authored Black Interceptor minimum must be 20m/s");
                Property(f.Player, "CurrentSpeed", 0f);
                black.Configure(f.Player, f.Traffic, tuning, f.Road, f.Player.TrackDistance + 200f, 0f);
                Check(black.DriveSpeed >= 20f, "Black Interceptor spawned below its minimum speed");
                foreach (float gap in new[] { 200f, 40f, 0f })
                {
                    Set(black, "trackDistance", f.Player.TrackDistance + gap);
                    Check((float)Call(black, "GetCurrentDriveSpeed") >= 20f, "An ordinary driving phase ignored its minimum");
                }
                Call(black, "BeginRamResponse", false, Vector3.right);
                foreach (float playerSpeed in new[] { 0f, 1f, 8f, 19f, 20f, 35f })
                {
                    Property(f.Player, "CurrentSpeed", playerSpeed);
                    Check(Mathf.Approximately((float)Call(black, "GetCurrentDriveSpeed"), Mathf.Max(20f, playerSpeed)),
                        "Post-ram speed matching ignored the minimum or capped a faster player");
                }
                Property(f.Player, "CurrentSpeed", 0f);
                tuning.minimumDrivingSpeed = 8f;
                Check((float)Call(black, "GetCurrentDriveSpeed") == 8f, "Edited minimum speed was ignored");
                tuning.minimumDrivingSpeed = 0f;
                Check((float)Call(black, "GetCurrentDriveSpeed") == 0f, "Zero did not disable the minimum speed");
                tuning.minimumDrivingSpeed = 20f;
                Property(f.Player, "CurrentSpeed", 35f);
                Set(black, "laneChangeSpeedBoostUntil", Time.time + 10f);
                Check(Mathf.Approximately((float)Call(black, "GetCurrentDriveSpeed"), 35f * (1f + tuning.laneChangeSpeedBoostMultiplier)),
                    "Minimum speed changed the evasive speed burst");
                Set(black, "laneChangeSpeedBoostUntil", -1f);
                // Rear impacts accelerate toward 110% of normal engine-adjusted maximum, independent of braking.
                f.Player.SetEnginePerformance(20f, 0f);
                Property(f.Player, "CurrentSpeed", 0f); Set(black, "currentSpeed", 20f);
                Call(black, "BeginRamResponse", true, Vector3.forward);
                Check((float)typeof(VoxelEnemyCar).GetField("rearRamEscapeUntil", Private).GetValue(black) > Time.time,
                    "Rear ram did not start the acceleration response");
                float expectedEscape = f.Player.EffectiveTopSpeed * 1.1f;
                float firstStep = (float)Call(black, "GetRearRamEscapeSpeed", .01f);
                Check(firstStep > 20f && firstStep < expectedEscape, "Rear ram did not accelerate smoothly");
                for(int i=0;i<30;i++) Set(black, "currentSpeed", (float)Call(black, "GetRearRamEscapeSpeed", .02f));
                Check(Mathf.Abs(black.DriveSpeed - expectedEscape) < .001f, "Rear ram ignored 110% engine-adjusted top speed");
                Property(f.Player, "CurrentSpeed", 8f);
                Check(Mathf.Abs((float)Call(black, "GetRearRamEscapeSpeed", .1f) - expectedEscape) < .001f,
                    "Rear escape slowed when the player braked");
                Set(black, "laneChangeSpeedBoostUntil", Time.time + 10f);
                Check(Mathf.Abs((float)Call(black, "GetCurrentDriveSpeed") - expectedEscape) < .001f,
                    "Rear escape stacked an unrelated evasive multiplier");
                Set(black, "laneChangeSpeedBoostUntil", -1f); Set(black, "rearRamEscapeUntil", -1f);
                Call(black, "BeginRamResponse", false, Vector3.right);
                Check((float)typeof(VoxelEnemyCar).GetField("rearRamEscapeUntil", Private).GetValue(black) < 0,
                    "Side impact incorrectly started rear escape");
                Set(black, "speedMatchUntil", -1f);
                f.Player.SetEnginePerformance(0f, 0f);
                Property(f.Player, "CurrentSpeed", 0f);
                f.Behaviour.sideRamEnabled = false; f.Behaviour.shootingWindows = false;
                f.Behaviour.weaveThroughTraffic = false;
                f.Spawn(0f, 85f);
                Set(black, "trackDistance", f.Player.TrackDistance + 100f);
                float startBlack = black.TrackDistance, startBug = f.Enemy.TrackDistance;
                foreach (float seconds in new[] { 1f / 30f, 1f / 60f, 1f / 120f })
                {
                    for (int i = 0; i < Mathf.RoundToInt(1f / seconds); i++)
                    {
                        float speed = (float)Call(black, "GetCurrentDriveSpeed");
                        Set(black, "currentSpeed", speed);
                        Set(black, "trackDistance", black.TrackDistance + speed * seconds);
                        f.Step(seconds, false);
                        Check(black.DriveSpeed >= 20f && f.Enemy.DriveSpeed >= 19.99f,
                            "Stopped player caused the Black Interceptor or following Bug to crawl");
                    }
                }
                Check(black.TrackDistance >= startBlack + 59f && f.Enemy.TrackDistance >= startBug + 59f,
                    "Clear-road enemies failed to move away from the stopped player");
                Directory.CreateDirectory("Temp/EnemyMinimumSpeed");
                File.WriteAllText("Temp/EnemyMinimumSpeed/Validation.txt",
                    "PASS (Edit Mode): authored Black Interceptor 20m/s minimum at spawn, approach and engagement; side-ram matching with player at 0/1/8/19/20/35m/s; edited/zero minimum; uncapped evasive burst; rear-ram smooth acceleration to 110% engine-adjusted maximum, braking independence, no evasive stacking and no rear escape from a side hit; Psycho Bug following Black Interceptor with stopped player at 30/60/120 FPS. Cloned tuning, temporary objects and state restored.");
            }
            finally
            {
                Call(black, "OnDisable"); ReleaseBars(go); Object.DestroyImmediate(go); Object.DestroyImmediate(tuning);
            }
        }

        private static void CheckFastRamReturn()
        {
            using var f = new Fixture();
            f.Player.topSpeed = 35f;
            f.Behaviour.shootingWindows=false; f.Behaviour.damageEvasion=false; f.Behaviour.weaveChance=0;
            f.Behaviour.fastRamReturn=true;
            Directory.CreateDirectory("Temp/PsychoBug");
            File.WriteAllText("Temp/PsychoBug/FastRamReturn.txt", "");
            foreach(float seconds in new[]{1f/30f,1f/60f,1f/120f})
            foreach(float playerSpeed in new[]{35f,20f,14.1f})
            foreach(float side in new[]{-3f,3f})
            {
                Property(f.Player,"CurrentSpeed",playerSpeed); f.Spawn(side,200f);
                Set(f.Enemy,"currentSpeed",playerSpeed); Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);
                float elapsed=0; bool reversed=false;
                while(elapsed<6.5f && f.Enemy.PsychoPhase!=PsychoBugPhase.Warning)
                {
                    f.Step(seconds,false); elapsed+=seconds; reversed|=f.Enemy.DriveSpeed<0;
                }
                Check(f.Enemy.PsychoPhase==PsychoBugPhase.Warning && reversed,
                    "Fast 200m ram return failed at "+playerSpeed+"m/s, dt="+seconds+"; phase="+f.Enemy.PsychoPhase+", gap="+(f.Enemy.TrackDistance-f.Player.TrackDistance)+", speed="+f.Enemy.DriveSpeed);
                float gapAtWarning = f.Enemy.TrackDistance - f.Player.CollisionTrackPosition.y;
                Check(Mathf.Abs(gapAtWarning)<=f.Behaviour.alignmentTolerance &&
                    Mathf.Abs(f.Enemy.DriveSpeed-playerSpeed)<=f.Behaviour.ramSpeedTolerance,
                    "Fast return began warning before matching position and speed: gap="+gapAtWarning+", speed="+f.Enemy.DriveSpeed+", player="+playerSpeed);
                File.AppendAllText("Temp/PsychoBug/FastRamReturn.txt", "PASS: "+Mathf.RoundToInt(1f/seconds)+" FPS, player "+playerSpeed+"m/s, side "+side+", 200m return to warning in "+elapsed.ToString("0.00")+"s.\n");
            }
        }

        private static void CheckRamOpeningAndSlowdown()
        {
            using var f=new Fixture();
            EditorUtility.CopySerialized(Resources.Load<VoxelPsychoBugTuning>("EnemyVehicles/PsychoBugBehaviour"),f.Behaviour);
            f.Behaviour.shootingWindows=false;f.Behaviour.weaveChance=0;f.Behaviour.damageEvasion=false;f.Behaviour.feintChance=0;
            f.Player.topSpeed=35;Property(f.Player,"CurrentSpeed",35f);f.Spawn(3);
            var report=new System.Text.StringBuilder();
            // Reuse the temporary car to test one isolated opening roll, independent
            // of the spawn/model code's other random draws.
            int openingRams=0;
            f.Behaviour.initialRamChance=.5f;
            for(int seed=0;seed<128;seed++)
            {
                UnityEngine.Random.InitState(seed);bool expected=UnityEngine.Random.value<.5f;
                UnityEngine.Random.InitState(seed);Call(f.Enemy,"ConfigurePsychoBug");
                float deadline=(float)typeof(VoxelEnemyCar).GetField("psychoNextAttack",Private).GetValue(f.Enemy);
                Check(Mathf.Approximately(deadline,expected?f.Behaviour.minimumAttackCooldown:
                    Mathf.Max(f.Behaviour.minimumAttackCooldown,f.Behaviour.initialRamDelay)),"Opening chance was not rolled once on configuration");
                if(expected)openingRams++;
            }
            Check(openingRams>40 && openingRams<88,"50% opening roll did not produce both behaviours");
            report.AppendLine("PASS: 50% opening chance, "+openingRams+" immediate openings across 128 deterministic seeds.");
            foreach(bool passing in new[]{false,true})
            {
                f.Behaviour.initialRamChance=0;f.Behaviour.initialRamDelay=3;
                f.Behaviour.sideRamChance=passing?0:1;f.Behaviour.ramOnPassingAlignment=passing;
                f.Spawn(3);Set(f.Enemy,"currentSpeed",35f);
                // Keep passing alignment available during the delay so neither path
                // can hide a per-decision reroll or bypass the opening deadline.
                for(int i=0;i<29;i++)
                {
                    Set(f.Enemy,"trackDistance",f.Player.CollisionTrackPosition.y);Set(f.Enemy,"currentSpeed",35f);
                    f.Step(.1f,false);
                    Check(f.Enemy.PsychoPhase==PsychoBugPhase.Hunting,"A non-ram opening attacked before its delay");
                }
                Set(f.Enemy,"trackDistance",f.Player.CollisionTrackPosition.y);Set(f.Enemy,"currentSpeed",35f);
                f.Step(.11f,false);Check(f.Enemy.PsychoPhase==PsychoBugPhase.Aligning,"Opening delay suppressed later attacks");
            }
            f.Behaviour.initialRamChance=1;f.Behaviour.sideRamChance=1;f.Behaviour.ramOnPassingAlignment=true;
            f.Spawn(3,200);f.Step(.6f,false);
            Check(f.Enemy.PsychoPhase==PsychoBugPhase.Aligning,"100% opening chance failed to start an approach");
            report.AppendLine("PASS: 0%/100% endpoints, deliberate and passing-alignment delay gates, normal attacks resume after delay.");
            f.Spawn(3,100);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);
            f.Behaviour.ramReturnSlowdownDistance=50;
            float near=(float)Call(f.Enemy,"GetPsychoRamApproachSpeed",25f,.02f);
            f.Behaviour.ramReturnSlowdownDistance=0;
            float noSlowdown=(float)Call(f.Enemy,"GetPsychoRamApproachSpeed",25f,.02f);
            Check(near>noSlowdown+10,"50m slowdown range did not reduce closing speed at 25m");
            f.Behaviour.ramReturnSlowdownDistance=100;
            Check((float)Call(f.Enemy,"GetPsychoRamApproachSpeed",25f,.02f)>near,"Edited slowdown distance was ignored");
            f.Behaviour.ramReturnSlowdownDistance=50;
            // Brake or accelerate while the Bug is closing, including a low-rate
            // frame. Alignment must survive a moving speed target without passing.
            foreach(float dt in new[]{1f/120,1f/30,.25f})
            foreach(float side in new[]{-3f,3f})
            foreach(float lead in new[]{200f,-30f})
            foreach(bool brake in new[]{true,false})
            {
                Property(f.Player,"CurrentSpeed",brake?35f:20f);f.Spawn(side,lead);
                Set(f.Enemy,"currentSpeed",f.Player.CurrentSpeed);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);
                bool changed=false;float minimum=lead,maximum=lead,elapsed=0;
                while(elapsed<9 && f.Enemy.PsychoPhase!=PsychoBugPhase.Warning)
                {
                    float gap=f.Enemy.TrackDistance-f.Player.CollisionTrackPosition.y;
                    if(Mathf.Abs(gap)<=50)changed=true;
                    if(changed)Property(f.Player,"CurrentSpeed",Mathf.MoveTowards(f.Player.CurrentSpeed,brake?14.1f:35f,12f*dt));
                    f.Step(dt,false);elapsed+=dt;gap=f.Enemy.TrackDistance-f.Player.CollisionTrackPosition.y;
                    minimum=Mathf.Min(minimum,gap);maximum=Mathf.Max(maximum,gap);
                }
                Check(changed && f.Enemy.PsychoPhase==PsychoBugPhase.Warning && (lead>0?minimum>=-.01f:maximum<=.01f),
                    "Changing-speed approach passed the player or missed warning: dt="+dt+", lead="+lead+", braking="+brake+", gap="+(f.Enemy.TrackDistance-f.Player.CollisionTrackPosition.y));
                report.AppendLine("PASS: dt="+dt.ToString("0.000")+", side="+side+", lead="+lead+", braking="+brake+", warning in "+elapsed.ToString("0.00")+"s, gap range "+minimum.ToString("0.00")+".."+maximum.ToString("0.00")+"m.");
            }
            Directory.CreateDirectory("Temp/PsychoBug");File.WriteAllText("Temp/PsychoBug/RamOpeningAndSlowdown.txt",report.ToString());
        }

        private static void CheckLowSpeedAndBarrels()
        {
            using var f=new Fixture();
            f.Behaviour.shootingWindows=false;f.Behaviour.damageEvasion=false;
            f.Behaviour.weaveChance=0;
            Property(f.Player,"CurrentSpeed",0f);f.Spawn(3);Set(f.Enemy,"currentSpeed",0f);
            Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);
            int integrity=f.Player.RemainingIntegrityVoxels;float distance=f.Enemy.TrackDistance;
            for(int i=0;i<50;i++)f.Step(.02f);
            Check(f.Enemy.DriveSpeed>=f.Behaviour.minimumDrivingSpeed && f.Enemy.TrackDistance>distance+4 &&
                f.Enemy.PsychoPhase!=PsychoBugPhase.Aligning && f.Player.RemainingIntegrityVoxels==integrity,
                "Stopped player trapped the Bug in stationary alignment");
            foreach(var phase in new[]{PsychoBugPhase.Warning,PsychoBugPhase.Slamming})
            {
                Property(f.Player,"CurrentSpeed",20f);f.Spawn(3);Call(f.Enemy,"BeginPsychoRam");
                Call(f.Enemy,"SetPsychoPhase",phase);Set(f.Enemy,"currentSpeed",5f);f.Step(.02f);
                Check(f.Enemy.PsychoPhase==PsychoBugPhase.Recovering && f.Player.RemainingIntegrityVoxels==integrity,
                    "Low-speed "+phase+" continued its ram");
            }
            f.Spawn(3);Call(f.Enemy,"BeginPsychoRam");Property(f.Player,"CurrentSpeed",0f);f.Step(.02f);
            Check(f.Enemy.PsychoPhase==PsychoBugPhase.Warning && f.Enemy.DriveSpeed>19,
                "Player braking changed a healthy committed launch speed");
            for(int i=0;i<25;i++)f.Step(.02f);
            Check(f.Player.RemainingIntegrityVoxels==integrity && f.Enemy.PsychoPhase!=PsychoBugPhase.Slamming,
                "Braking escape was followed by the committed ram");
            f.Spawn(0,14);Set(f.Enemy,"currentSpeed",0f);
            Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Exposed);Set(f.Enemy,"psychoExposureDuration",2f);
            for(int i=0;i<25;i++)f.Step(.02f,false);
            Check(f.Enemy.PsychoPhase==PsychoBugPhase.Exposed && f.Enemy.DriveSpeed>=5.99f,
                "Shooting window parked beside a stopped player");
            f.Behaviour.minimumAlignmentSpeed=0;f.Behaviour.minimumRamSpeed=12;Property(f.Player,"CurrentSpeed",8f);f.Spawn(3);
            Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);f.Step(.02f,false);
            Check(f.Enemy.PsychoPhase==PsychoBugPhase.Recovering,"Edited ram threshold was ignored");

            // Actual three-lane first-track geometry: barrels should cause an early safe merge.
            f.Road.laneCount=3;f.Road.roadWidth=16;f.Player.SetLaneLayout(3,16f/3);
            Set(f.Player,"currentLane",1);Set(f.Player,"previousLane",1);Call(f.Player,"ApplyTrackPose");
            Property(f.Player,"CurrentSpeed",20f);f.Spawn(0,40);
            var go=new GameObject("Barrel avoidance QA");go.transform.SetParent(f.Root.transform,false);
            var drums=go.AddComponent<VoxelFuelDrumObstacle>();drums.enabled=false;
            try
            {
                drums.Configure(f.Player,f.Road,Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/FuelDrums"),
                    f.Enemy.TrackDistance+18,0);
                f.Behaviour.sideRamEnabled=false;f.Behaviour.weaveThroughTraffic=true;
                f.Step(.001f,false);
                Check(!(bool)Call(f.Enemy,"PsychoRouteSafe",0f,false),"Forward barrel group was invisible to route prediction");
                for(int i=0;i<120;i++)
                {
                    f.Step(.02f,false);
                    Check(Mathf.Abs(f.Enemy.LaneOffset-drums.LaneOffset)>=f.EnemyTuning.collisionHalfWidth+2 ||
                        drums.TrackDistance-f.Enemy.TrackDistance>=f.EnemyTuning.collisionHalfLength+2+f.Behaviour.trafficClearance-.01f,
                        "Bug drove through the forward barrel clearance");
                }
                Check(Mathf.Abs(f.Enemy.LaneOffset)>4,"Bug did not dart into a clear lane around barrels; lane="+f.Enemy.LaneOffset+", speed="+f.Enemy.DriveSpeed+", gap="+(drums.TrackDistance-f.Enemy.TrackDistance)+", target="+f.Enemy.GetType().GetField("targetLaneOffset",Private).GetValue(f.Enemy));
                f.Spawn(0,40);Set(drums,"trackDistance",f.Enemy.TrackDistance+8);
                f.Behaviour.weaveThroughTraffic=false;
                Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Exposed);Set(f.Enemy,"psychoExposureDuration",10f);
                // A long frame must stop at the buffer even if acceleration/braking cannot react in time.
                f.Behaviour.braking=.01f;f.Step(.5f,false);
                Check(drums.TrackDistance-f.Enemy.TrackDistance>=f.EnemyTuning.collisionHalfLength+2+f.Behaviour.trafficClearance-.01f,
                    "Long-frame braking tunneled into barrels");
                for(int i=0;i<30;i++)f.Step(.02f,false);
                Check(f.Enemy.DriveSpeed<.1f,"Minimum driving speed overrode a blocked road");
                Set(drums,"hasExploded",true);
                Check((bool)Call(f.Enemy,"PsychoRouteSafe",0f,false),"Exploded barrels left a phantom road blocker");
            }
            finally { ReleaseBars(go);Object.DestroyImmediate(go); }
        }
        private static void CheckBlockedMergeAndReturn()
        {
            using var f=new Fixture();
            f.Behaviour.sideRamEnabled=false;f.Behaviour.shootingWindows=false;f.Behaviour.weaveChance=0;
            f.Behaviour.weaveThroughTraffic=false;
            // Reproduce a merge whose predicted route is blocked by the player,
            // rather than traffic. The old code refused fallback until settled.
            f.Spawn(3);Set(f.Enemy,"laneOffset",1.5f);Set(f.Enemy,"targetLaneOffset",0f);
            for(int i=0;i<30;i++)f.Step(.02f,false);
            Check(Mathf.Abs(f.Enemy.LaneOffset-3)<.04f,"Player-blocked merge stayed between lanes with weaving disabled");

            var plain=Object.Instantiate(f.EnemyTuning);plain.psychoBug=null;
            var sourceObject=new GameObject("Blocked original lane QA");sourceObject.transform.SetParent(f.Root.transform,false);
            var source=sourceObject.AddComponent<VoxelEnemyCar>();source.enabled=false;
            var destinationObject=new GameObject("Blocked destination lane QA");destinationObject.transform.SetParent(f.Root.transform,false);
            var destination=destinationObject.AddComponent<VoxelEnemyCar>();destination.enabled=false;
            try
            {
                f.Spawn(0,40);Set(f.Enemy,"laneOffset",1.5f);Set(f.Enemy,"targetLaneOffset",3f);
                source.Configure(f.Player,f.Traffic,plain,f.Road,f.Enemy.TrackDistance+6,0);Set(source,"currentSpeed",0f);
                destination.Configure(f.Player,f.Traffic,plain,f.Road,f.Enemy.TrackDistance+16,3);Set(destination,"currentSpeed",0f);
                for(int i=0;i<45;i++)
                {
                    var previous=new Vector2(f.Enemy.LaneOffset,f.Enemy.TrackDistance);
                    f.Step(.02f,false);
                    foreach(var blocker in new[]{source,destination})
                    {
                        var origin=new Vector2(blocker.LaneOffset,blocker.TrackDistance);
                        Check(!VoxelVehicleCollision.Sweep(previous-origin,new Vector2(f.Enemy.LaneOffset,f.Enemy.TrackDistance)-origin,
                            new Vector2(f.EnemyTuning.collisionHalfWidth+plain.collisionHalfWidth,f.EnemyTuning.collisionHalfLength+plain.collisionHalfLength),out _),
                            "Lane recovery crossed a blocked vehicle");
                    }
                }
                Check(Mathf.Abs(f.Enemy.LaneOffset-6)<.04f,"Blocked original/destination lanes did not select the next clear centre");
                destination.gameObject.SetActive(false);
                f.Spawn(0,40);Set(f.Enemy,"laneOffset",1.5f);Set(f.Enemy,"targetLaneOffset",3f);Set(f.Enemy,"currentSpeed",0f);
                Set(source,"trackDistance",f.Enemy.TrackDistance+f.EnemyTuning.collisionHalfLength+plain.collisionHalfLength+.01f);
                for(int i=0;i<30;i++)f.Step(.02f,false);
                Check(f.Enemy.LaneOffset>=2.96f && Mathf.Abs(f.Enemy.LaneOffset-(float)Call(f.Enemy,"NearestPsychoLane",f.Enemy.LaneOffset))<.04f,
                    "Emergency safety buffer trapped a separating car between lanes; lane="+f.Enemy.LaneOffset+", speed="+f.Enemy.DriveSpeed);
                Check((float)typeof(VoxelEnemyCar).GetMethod("PsychoStepFraction",BindingFlags.Static|BindingFlags.NonPublic)
                    .Invoke(null,new object[]{new Vector2(-1,-1),new Vector2(4,1),new Vector2(3,3)})==0,
                    "Separating exception allowed a step through an occupied vehicle");
            }
            finally {ReleaseBars(sourceObject);ReleaseBars(destinationObject);Object.DestroyImmediate(sourceObject);Object.DestroyImmediate(destinationObject);Object.DestroyImmediate(plain);}

            EditorUtility.CopySerialized(Resources.Load<VoxelPsychoBugTuning>("EnemyVehicles/PsychoBugBehaviour"),f.Behaviour);
            f.Behaviour.initialRamChance=1;f.Behaviour.feintChance=0;f.Behaviour.damageEvasion=false;f.Behaviour.sideRamDamageMin=f.Behaviour.sideRamDamageMax=3;
            f.Road.laneCount=3;f.Road.roadWidth=16;f.Player.topSpeed=35;
            Set(f.Player,"currentLane",1);Set(f.Player,"previousLane",1);f.Player.SetLaneLayout(3,16f/3);
            Directory.CreateDirectory("Temp/PsychoBug");File.WriteAllText("Temp/PsychoBug/RamApproachMetrics.txt","");
            foreach(float speed in new[]{35f,20f,14.1f})
            {
                Property(f.Player,"CurrentSpeed",speed);f.Spawn(16f/3,200);Set(f.Enemy,"currentSpeed",speed);
                float minimum=speed;
                for(int i=0;i<100;i++){f.Step(.02f,false);minimum=Mathf.Min(minimum,f.Enemy.DriveSpeed);}
                Check(minimum<=Mathf.Max(f.Behaviour.minimumRamSpeed,speed*f.Behaviour.ramReturnSpeedFraction)+.1f,
                    "Bug did not brake aggressively from its 200m lead at player speed "+speed);
                f.Wait(PsychoBugPhase.Warning,speed>=35?8:speed>=20?16:28);
                float approachSeconds=(float)typeof(VoxelEnemyCar).GetField("psychoClock",Private).GetValue(f.Enemy);
                int integrity=f.Player.RemainingIntegrityVoxels;f.Wait(PsychoBugPhase.Slamming,1.2f);Set(f.Player,"nextDamageTime",-1f);
                f.Wait(PsychoBugPhase.Recovering,1);
                Check(f.Player.RemainingIntegrityVoxels==integrity-3,"Natural 200m approach failed to deliver a ram at player speed "+speed);
                File.AppendAllText("Temp/PsychoBug/RamApproachMetrics.txt","Player "+speed+"m/s, 200m lead: warning in "+approachSeconds.ToString("0.00")+"s; minimum return speed "+minimum.ToString("0.00")+"m/s; ram removed 3 integrity voxels.\n");
            }
        }

        private static void ValidateAssets()
        {
            var tuning=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(VoxelPsychoBugBuilder.TuningPath);
            Check(tuning!=null && tuning.displayName=="Psycho Bug Enemy" && tuning.psychoBug!=null && tuning.mineLayer==null,"Missing new enemy tuning/module");
            var model=tuning.modelPrefab;var civilian=AssetDatabase.LoadAssetAtPath<GameObject>(VoxelCivilianBeetleBuilder.PrefabPath);
            Check(model!=civilian && model.GetComponent<VoxelTrafficPaint>()==null && civilian.GetComponent<VoxelTrafficPaint>()!=null,"Civilian source changed or enemy still randomized paint");
            var renderers=model.GetComponentsInChildren<MeshRenderer>();
            Check(renderers.Length<800 && model.GetComponentsInChildren<Collider>().Length==0,"Enemy exceeds model budget or adds colliders");
            Check(renderers.Where(r=>r.name=="Roof voxel").All(r=>r.sharedMaterial.color.maxColorComponent<.1f),"Body is not black");
            Check(renderers.Count(r=>r.name=="Wheel spike")==4 && renderers.Count(r=>r.name=="Door spike")==12 &&
                model.GetComponentsInChildren<Transform>().Count(t=>t.name=="Obstacle Voxel Wheel")==4,"Spikes/wheel pivots missing");
            Check(renderers.Where(r=>r.name=="Door spike").All(r=>r.transform.parent.GetComponent<MeshRenderer>()!=null),"Door spikes do not inherit door damage");
            foreach(var r in renderers.Where(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null))
                Check(AssetDatabase.Contains(r.GetComponent<MeshFilter>().sharedMesh),"Unsaved procedural mesh");
            foreach(string guid in AssetDatabase.FindAssets("t:VoxelObstacleCarTuning").Concat(AssetDatabase.FindAssets("t:VoxelTrackDefinition")).Distinct())
            foreach(var traffic in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<VoxelObstacleCarTuning>())
                Check(traffic.enemyVehicleSpawns.Count(e=>e!=null && e.vehicle==tuning)==1 && !traffic.CivilianVehiclePool.Contains(tuning),"Pool registration wrong: "+traffic.name);
            CheckInspector(tuning.psychoBug);
        }
        private static void CheckInspector(VoxelPsychoBugTuning tuning)
        {
            using(var tree=PropertyTree.Create(tuning))
            {
                tree.UpdateTree();var props=tree.EnumerateTree(true).ToArray();
                foreach(var field in typeof(VoxelPsychoBugTuning).GetFields().Where(f=>!f.IsStatic))
                    Check(props.Any(p=>p.Name==field.Name && p.Attributes.OfType<FoldoutGroupAttribute>().Any()),"Missing tuning foldout: "+field.Name);
            }
        }
        private static void CheckTraffic(Fixture f)
        {
            f.Spawn(0,20);
            var blockerObject=new GameObject("Traffic clearance QA");blockerObject.transform.SetParent(f.Root.transform,false);
            var blocker=blockerObject.AddComponent<VoxelEnemyCar>();blocker.enabled=false;
            var plain=Object.Instantiate(f.EnemyTuning);plain.psychoBug=null;
            try
            {
                blocker.Configure(f.Player,f.Traffic,plain,f.Road,f.Enemy.TrackDistance,3);Set(blocker,"currentSpeed",20f);
                Set(f.Enemy,"psychoNextScan",0f);f.Step(.001f,false);
                Check(!(bool)Call(f.Enemy,"PsychoRouteSafe",3f,true),"Lane merge passed through adjacent traffic");
                Set(blocker,"trackDistance",f.Enemy.TrackDistance-8);Set(blocker,"currentSpeed",35f);
                Check(!(bool)Call(f.Enemy,"PsychoRouteSafe",3f,true),"Merge ignored fast rear traffic");
                Set(blocker,"trackDistance",f.Enemy.TrackDistance+15);Set(blocker,"currentSpeed",-20f);
                Check(!(bool)Call(f.Enemy,"PsychoRouteSafe",3f,true),"Merge ignored oncoming traffic");
                Set(blocker,"laneOffset",0f);Set(blocker,"targetLaneOffset",0f);Set(blocker,"trackDistance",f.Enemy.TrackDistance+5);Set(blocker,"currentSpeed",5f);
                Check((float)Call(f.Enemy,"LimitPsychoTrafficSpeed",25f)<=5.01f,"Following limiter failed");
                blocker.gameObject.SetActive(false);Check((bool)Call(f.Enemy,"PsychoRouteSafe",3f,true),"Inactive traffic blocked clear lane");
            }
            finally { ReleaseBars(blockerObject);Object.DestroyImmediate(blockerObject);Object.DestroyImmediate(plain); }
        }
        private static void CheckReservation(Fixture f)
        {
            f.Spawn(3);Call(f.Enemy,"BeginPsychoRam");
            var otherObject=new GameObject("Second Psycho Bug QA");otherObject.transform.SetParent(f.Root.transform,false);
            var other=otherObject.AddComponent<VoxelEnemyCar>();other.enabled=false;
            try
            {
                other.Configure(f.Player,f.Traffic,f.EnemyTuning,f.Road,f.Player.TrackDistance,-3);Call(other,"OnEnable");
                Check(!(bool)Call(other,"CanReservePsychoAttack"),"Two simultaneous attacks ignored limit");
                Call(f.Enemy,"FinishPsychoAttack",false);
                Check((bool)Call(other,"CanReservePsychoAttack"),"Finished attack retained reservation");
            }
            finally {Call(other,"OnDisable");ReleaseBars(otherObject);Object.DestroyImmediate(otherObject);}
        }
    }
}
