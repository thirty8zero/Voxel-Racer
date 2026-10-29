using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Uses actual frame-driven enemy/projectile updates; the QA player has a controlled steady speed.</summary>
    public static class VoxelPsychoBugPlayValidation
    {
        private const string Pending="VoxelRacer.PsychoBugPlayQA";
        private static VoxelPsychoBugValidation.Fixture f;
        private static VoxelMissionProgress mission, previousMission;
        private static VoxelStartCountdown previousCountdown;
        private static VoxelPauseMenu pause,previousPause;
        private static VoxelMissionTuning missionTuning;
        private static VoxelGunTuning gun;
        private static VoxelObstacleCar oncoming;
        private static VoxelEnemyCar blackInterceptor;
        private static VoxelEnemyVehicleTuning blackTuning;
        private static float blackStartDistance;
        private static bool fastReturnReversed;
        private static float fastReturnStarted;
        private static Vector2 previousTrafficRelative;
        private static VoxelGarageUpgradeDockValidation.SavedState saved;
        private static Scene scene,previousScene;
        private static UnityEngine.Random.State random;
        private static float previousScale, lastGameTime, started, phaseStarted, warningStarted, warningLane, frozenDistance, frozenLane, frozenClock;
        private static int phase, integrity, initialIntegrity, lastFrame;
        private static bool shot, shotHit, sawSlam, ownsState, sawShotEscape;
        private static float shotAt;
        private static readonly HashSet<PsychoBugPhase> seen=new();
        private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
        private static void Check(bool value,string message)=>VoxelPsychoBugValidation.Check(value,message);
        private static object Call(object target,string method,params object[] args)=>VoxelPsychoBugValidation.Call(target,method,args);
        private static void Set(object target,string field,object value)=>VoxelPsychoBugValidation.Set(target,field,value);
        private static void Property(object target,string property,object value)=>VoxelPsychoBugValidation.Property(target,property,value);
        private static void Active(Type type,object value)=>type.GetField("<Active>k__BackingField",Static).SetValue(null,value);

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;
        }
        [MenuItem("Tools/Voxel Racer/Validate Psycho Bug in Play Mode")]
        public static void Run()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode,"Start this check in Edit Mode.");
            SessionState.SetBool(Pending+".Background",Application.runInBackground);
            SessionState.SetBool(Pending+".Restore",true);SessionState.SetBool(Pending,true);
            Application.runInBackground=true;EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending+".Restore",false))
            {Application.runInBackground=SessionState.GetBool(Pending+".Background",false);SessionState.SetBool(Pending+".Restore",false);}
            if(!SessionState.GetBool(Pending,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=Setup;
            if(state==PlayModeStateChange.ExitingPlayMode)
            {EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);Cleanup();}
        }
        private static void Setup()
        {
            try
            {
                random=UnityEngine.Random.state;previousScale=Time.timeScale;previousMission=VoxelMissionProgress.Active;
                previousPause=VoxelPauseMenu.Active;previousCountdown=VoxelStartCountdown.Active;
                saved=new VoxelGarageUpgradeDockValidation.SavedState();ownsState=true;
                Time.timeScale=1;Active(typeof(VoxelPauseMenu),null);Active(typeof(VoxelStartCountdown),null);
                previousScene=SceneManager.GetActiveScene();scene=SceneManager.CreateScene("Temporary Psycho Bug Play QA");SceneManager.SetActiveScene(scene);
                VoxelRacerBootstrap.ReloadGeneratedMaterials();f=new VoxelPsychoBugValidation.Fixture();
                missionTuning=ScriptableObject.CreateInstance<VoxelMissionTuning>();missionTuning.requiredPoints=1000000;missionTuning.timeLimitSeconds=1000;
                mission=f.Root.AddComponent<VoxelMissionProgress>();mission.Configure(missionTuning);
                f.Behaviour.minimumShootingDuration=f.Behaviour.maximumShootingDuration=1;
                f.Behaviour.feintChance=0;f.Behaviour.sideRamChance=1;f.Behaviour.weaveChance=0;
                f.Spawn(0,14);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.PositioningForShot);f.Enemy.enabled=true;initialIntegrity=f.Player.RemainingIntegrityVoxels;
                gun=ScriptableObject.CreateInstance<VoxelGunTuning>();gun.damagePerBullet=1;gun.projectileSpeed=100;gun.maximumRange=40;gun.areaOfEffectRadius=0;
                phase=0;shot=shotHit=sawSlam=sawShotEscape=false;seen.Clear();started=phaseStarted=Time.realtimeSinceStartup;lastGameTime=Time.time;lastFrame=-1;
                EditorApplication.update+=Tick;
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        private static void Tick()
        {
            if(!Application.isPlaying || EditorApplication.isPaused || lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;
            if(Time.realtimeSinceStartup-started>35){Finish("FAIL: timed out in phase "+phase+", enemy "+f?.Enemy?.PsychoPhase);return;}
            try
            {
                float dt=Time.time-lastGameTime;lastGameTime=Time.time;
                Property(f.Player,"TrackDistance",f.Player.TrackDistance+f.Player.CurrentSpeed*dt);
                Call(f.Player,"UpdateRamResponse");Call(f.Player,"ApplyTrackPose");
                seen.Add(f.Enemy.PsychoPhase);sawSlam|=f.Enemy.PsychoPhase==PsychoBugPhase.Slamming;
                switch(phase)
                {
                    case 0:
                        if(f.Enemy.PsychoPhase==PsychoBugPhase.Exposed && !shot)
                        {
                            var bullet=VoxelProjectile.Create(f.Enemy.transform.TransformPoint(new Vector3(0,.85f,-7)),f.Enemy.transform.forward,gun);
                            bullet.transform.SetParent(f.Root.transform,true);shot=true;shotAt=Time.time;
                        }
                        shotHit|=f.Enemy.CurrentHealth<f.EnemyTuning.vehicleHealth;
                        if(shotHit && f.Enemy.PsychoPhase==PsychoBugPhase.Hunting && Mathf.Abs(f.Enemy.LaneOffset)>.1f)
                        {
                            Check(Time.time-shotAt<.8f && f.Enemy.DriveSpeed>20,"Live projectile reaction was too slow or failed to accelerate");
                            sawShotEscape=true;
                        }
                        if(f.Enemy.PsychoPhase==PsychoBugPhase.Warning)
                        {
                            Check(shot && shotHit && sawShotEscape,"Actual gun bullet did not trigger an exposure escape");
                            warningStarted=Time.time;warningLane=Mathf.Abs(f.Enemy.LaneOffset);integrity=f.Player.RemainingIntegrityVoxels;Next();
                        }
                        break;
                    case 1:
                        Check(f.Player.RemainingIntegrityVoxels==integrity || f.Enemy.PsychoPhase==PsychoBugPhase.Recovering,"Applied damage before committed slam");
                        if(Time.time-warningStarted>.7f && Time.time-warningStarted<.95f)
                            Check(f.Enemy.PsychoPhase==PsychoBugPhase.Warning && Mathf.Abs(f.Enemy.LaneOffset)>warningLane+.1f,"One-second visible windup missing");
                        if(f.Enemy.PsychoPhase==PsychoBugPhase.Recovering)
                        {
                            Check(Time.time-warningStarted>=.98f && f.Player.RemainingIntegrityVoxels==integrity-3,"Natural side ram timing or damage wrong");
                            integrity=f.Player.RemainingIntegrityVoxels;Next();
                        }
                        break;
                    case 2:
                        Check(f.Player.RemainingIntegrityVoxels==integrity,"Recovery repeated a hit");
                        if(Time.realtimeSinceStartup-phaseStarted>.4f)
                        {f.Spawn(-3);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);f.Enemy.enabled=true;Next();}
                        break;
                    case 3:
                        if(f.Enemy.PsychoPhase==PsychoBugPhase.Recovering)
                        {
                            Check(f.Player.RemainingIntegrityVoxels==integrity-3,"Opposite-side live ram failed");
                            integrity=f.Player.RemainingIntegrityVoxels;f.Spawn(3);Call(f.Enemy,"BeginPsychoRam");f.Enemy.enabled=true;
                            // Real boost forward displacement uses the same collision position offset.
                            Set(f.Player,"boostForwardOffset",20f);Next();
                        }
                        break;
                    case 4:
                        if(Time.realtimeSinceStartup-phaseStarted<.2f)break;
                        Check(f.Enemy.PsychoPhase==PsychoBugPhase.Recovering && f.Player.RemainingIntegrityVoxels==integrity,"Boost escape was chased or hit");
                        Set(f.Player,"boostForwardOffset",0f);f.Spawn(3);Call(f.Enemy,"BeginPsychoRam");f.Enemy.enabled=true;
                        Call(f.Enemy,"RamByPlayer",new Vector3?(Vector3.right));
                        Check(f.Enemy.PsychoPhase==PsychoBugPhase.Staggered && f.Enemy.CurrentHealth<f.EnemyTuning.vehicleHealth,"Live player ram did not interrupt and damage enemy");
                        pause=f.Root.AddComponent<VoxelPauseMenu>();pause.Configure(f.Player);f.Player.enabled=true;pause.SetPaused(true);f.Player.enabled=false;
                        Check(VoxelPauseMenu.IsPaused && Time.timeScale==0,"Actual pause menu did not pause fixture");
                        frozenDistance=f.Enemy.TrackDistance;frozenLane=f.Enemy.LaneOffset;
                        frozenClock=(float)typeof(VoxelEnemyCar).GetField("psychoClock",VoxelPsychoBugValidation.Private).GetValue(f.Enemy);Next();
                        break;
                    case 5:
                        if(Time.realtimeSinceStartup-phaseStarted<.3f)break;
                        Check(f.Enemy.TrackDistance==frozenDistance && f.Enemy.LaneOffset==frozenLane &&
                            (float)typeof(VoxelEnemyCar).GetField("psychoClock",VoxelPsychoBugValidation.Private).GetValue(f.Enemy)==frozenClock,"Paused enemy moved or advanced attack timers");
                        pause.SetPaused(false);Property(mission,"IsComplete",true);Next();break;
                    case 6:
                        if(Time.realtimeSinceStartup-phaseStarted<.2f)break;
                        Check(f.Enemy.TrackDistance==frozenDistance && f.Enemy.LaneOffset==frozenLane,"Mission-complete enemy kept moving/attacking");
                        Check(sawSlam && seen.Contains(PsychoBugPhase.Exposed) && seen.Contains(PsychoBugPhase.Aligning),"Natural attack cycle skipped required phases");
                        Check(initialIntegrity-f.Player.RemainingIntegrityVoxels>=6,"Live attacks did not damage player body");
                        Property(mission,"IsComplete",false);f.Behaviour.sideRamEnabled=false;f.Behaviour.shootingWindows=false;
                        Property(f.Player,"CurrentSpeed",0f);f.Spawn(3,-40);f.Enemy.enabled=true;Next();break;
                    case 7:
                        if(Time.realtimeSinceStartup-phaseStarted<.5f)break;
                        Check(f.Enemy.DriveSpeed>18,"Live Bug mirrored stopped player speed");
                        f.Behaviour.sideRamEnabled=true;f.Behaviour.sideRamChance=0;f.Behaviour.weaveChance=0;
                        f.Behaviour.ramOnPassingAlignment=true;f.Behaviour.minimumAttackCooldown=f.Behaviour.maximumAttackCooldown=0;
                        Property(f.Player,"CurrentSpeed",20f);f.Spawn(3,-3);Property(f.Player,"CurrentSpeed",16f);f.Enemy.enabled=true;
                        integrity=f.Player.RemainingIntegrityVoxels;Next();break;
                    case 8:
                        if(f.Enemy.PsychoPhase==PsychoBugPhase.Warning){warningStarted=Time.time;Next();}
                        break;
                    case 9:
                        if(f.Enemy.PsychoPhase!=PsychoBugPhase.Recovering)break;
                        Check(Time.time-warningStarted>=.98f && f.Player.RemainingIntegrityVoxels==integrity-3,"Live slowdown opportunity missed its delayed side ram");
                        integrity=f.Player.RemainingIntegrityVoxels;
                        Property(f.Player,"CurrentSpeed",0f);f.Spawn(3);Set(f.Enemy,"currentSpeed",0f);
                        Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);f.Enemy.enabled=true;Next();break;
                    case 10:
                        if(Time.realtimeSinceStartup-phaseStarted<1f)break;
                        Check(f.Enemy.DriveSpeed>=f.Behaviour.minimumDrivingSpeed &&
                            f.Enemy.PsychoPhase!=PsychoBugPhase.Aligning && f.Player.RemainingIntegrityVoxels==integrity,
                            "Live alignment parked with the stopped player");
                        Property(f.Player,"CurrentSpeed",20f);f.Spawn(3);Call(f.Enemy,"BeginPsychoRam");
                        Set(f.Enemy,"currentSpeed",5f);f.Enemy.enabled=true;Next();break;
                    case 11:
                        if(Time.realtimeSinceStartup-phaseStarted<.2f)break;
                        Check(f.Enemy.PsychoPhase==PsychoBugPhase.Recovering && f.Enemy.DriveSpeed>5 &&
                            f.Player.RemainingIntegrityVoxels==integrity,"Live low-speed warning failed to cancel and accelerate away");
                        Property(f.Player,"CurrentSpeed",8f);f.Spawn(3,10);
                        Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);f.Enemy.enabled=true;Next();break;
                    case 12:
                        Check(f.Enemy.PsychoPhase!=PsychoBugPhase.Aligning && f.Enemy.PsychoPhase!=PsychoBugPhase.Warning &&
                            f.Enemy.DriveSpeed>=19.99f,"Live Bug slowed to match a player below its approach-speed guard");
                        if(Time.realtimeSinceStartup-phaseStarted<.8f)break;
                        Property(f.Player,"CurrentSpeed",0f);f.Behaviour.sideRamEnabled=false;f.Behaviour.shootingWindows=false;
                        f.Behaviour.weaveThroughTraffic=false;f.Spawn(3,60);f.Enemy.enabled=true;
                        var trafficObject=new GameObject("Live head-on traffic QA");trafficObject.transform.SetParent(f.Root.transform,false);
                        oncoming=trafficObject.AddComponent<VoxelObstacleCar>();
                        oncoming.Configure(f.Player,f.Traffic,false,f.Road,f.Enemy.TrackDistance+60,3,25f);
                        previousTrafficRelative=new Vector2(f.Enemy.LaneOffset-oncoming.LaneOffset,f.Enemy.TrackDistance-oncoming.TrackDistance);
                        Next();break;
                    case 13:
                        Vector2 trafficRelative=new(f.Enemy.LaneOffset-oncoming.LaneOffset,f.Enemy.TrackDistance-oncoming.TrackDistance);
                        Vector2 trafficSize=new(f.EnemyTuning.collisionHalfWidth+oncoming.TrafficHalfWidth,
                            f.EnemyTuning.collisionHalfLength+oncoming.TrafficHalfLength);
                        Check(!VoxelVehicleCollision.Sweep(previousTrafficRelative,trafficRelative,trafficSize,out _),
                            "Actual frame-driven oncoming civilian passed through the Bug");
                        previousTrafficRelative=trafficRelative;
                        if(Time.realtimeSinceStartup-phaseStarted<2.5f)break;
                        f.Enemy.enabled=false;
                        // A disabled controller is still an active road occupant. Remove
                        // this completed scenario's car from later return-route scans.
                        oncoming.gameObject.SetActive(false);
                        blackTuning=Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"));
                        var blackObject=new GameObject("Live Black Interceptor minimum-speed QA");blackObject.transform.SetParent(f.Root.transform,false);
                        blackInterceptor=blackObject.AddComponent<VoxelEnemyCar>();
                        blackInterceptor.Configure(f.Player,f.Traffic,blackTuning,f.Road,f.Player.TrackDistance+40f,-6f);
                        Call(blackInterceptor,"BeginRamResponse",true,Vector3.forward);
                        blackStartDistance=blackInterceptor.TrackDistance;Next();break;
                    case 14:
                        if(Time.realtimeSinceStartup-phaseStarted<.3f)break;
                        Check(blackInterceptor!=null && Mathf.Abs(blackInterceptor.DriveSpeed-f.Player.EffectiveTopSpeed*1.1f)<.01f && blackInterceptor.TrackDistance>blackStartDistance+4f,
                            "Live Black Interceptor did not accelerate to 110% top speed after a rear ram");
                        Property(f.Player,"CurrentSpeed",8f);Next();break;
                    case 15:
                        if(Time.realtimeSinceStartup-phaseStarted<.3f)break;
                        Check(Mathf.Abs(blackInterceptor.DriveSpeed-f.Player.EffectiveTopSpeed*1.1f)<.01f,"Rear escape tracked braking instead of maximum speed");
                        f.Player.SetEnginePerformance(25f,0f);
                        Property(f.Player,"CurrentSpeed",35f);Next();break;
                    case 16:
                        if(Time.realtimeSinceStartup-phaseStarted<.3f)break;
                        Check(Mathf.Abs(blackInterceptor.DriveSpeed-f.Player.EffectiveTopSpeed*1.1f)<.01f,"Rear escape ignored current engine-adjusted maximum speed");
                        Property(f.Player,"CurrentSpeed",0f);Next();break;
                    case 17:
                        if(Time.realtimeSinceStartup-phaseStarted<.3f)break;
                        Check(Mathf.Abs(blackInterceptor.DriveSpeed-f.Player.EffectiveTopSpeed*1.1f)<.01f,"Repeated player braking changed rear escape speed");
                        Next();break;
                    case 18:
                        // Escape uses scaled gameplay time; slow editor frames can advance less than wall time.
                        float escapeEnd=(float)typeof(VoxelEnemyCar).GetField("rearRamEscapeUntil",VoxelPsychoBugValidation.Private).GetValue(blackInterceptor);
                        if(Time.time<escapeEnd+.1f)break;
                        Check(Mathf.Abs(blackInterceptor.DriveSpeed-20f)<.01f,"Rear escape did not expire back to minimum-limited ordinary driving: speed="+blackInterceptor.DriveSpeed+", time="+Time.time+", end="+escapeEnd);
                        Object.Destroy(blackInterceptor.gameObject);blackInterceptor=null;
                        f.Player.SetEnginePerformance(0f,0f); f.Player.topSpeed=35f; Property(f.Player,"CurrentSpeed",20f);
                        f.Behaviour.fastRamReturn=true;f.Behaviour.sideRamEnabled=true;f.Behaviour.weaveThroughTraffic=true;
                        f.Spawn(3f,200f);Set(f.Enemy,"currentSpeed",20f);Call(f.Enemy,"SetPsychoPhase",PsychoBugPhase.Aligning);f.Enemy.enabled=true;
                        fastReturnReversed=false;fastReturnStarted=Time.time;integrity=f.Player.RemainingIntegrityVoxels;Next();break;
                    case 19:
                        fastReturnReversed|=f.Enemy.DriveSpeed<0;
                        Check(Time.time-fastReturnStarted<6.5f,"Live 200m fast ram return took too long; phase="+f.Enemy.PsychoPhase+
                            ", gap="+(f.Enemy.TrackDistance-f.Player.CollisionTrackPosition.y)+", speed="+f.Enemy.DriveSpeed+
                            ", game seconds="+(Time.time-fastReturnStarted)+", wall seconds="+(Time.realtimeSinceStartup-phaseStarted));
                        if(f.Enemy.PsychoPhase!=PsychoBugPhase.Warning)break;
                        Check(fastReturnReversed && Mathf.Abs(f.Enemy.DriveSpeed-f.Player.CurrentSpeed)<=f.Behaviour.ramSpeedTolerance,
                            "Live fast return skipped attack-only reverse or failed to settle to player speed");
                        warningStarted=Time.time;Next();break;
                    case 20:
                        if(f.Enemy.PsychoPhase!=PsychoBugPhase.Recovering)break;
                        Check(Time.time-warningStarted>=.98f && f.Player.RemainingIntegrityVoxels==integrity-3,
                            "Live fast return lost the full warning or actual side-ram damage");
                        Finish("PASS (Play Mode): actual frame-driven exposure, bullet escape, left/right side rams, full one-second warning, single swept damage, recovery, player counter-ram, pause, mission completion, stopped/slow-player breakaway, and blocked head-on traffic safety. Black Interceptor rear ram accelerates to 110% of engine-adjusted top speed, ignores player braking, updates for changed engine maximum and expires to ordinary minimum-limited driving. Psycho Bug returns from a 200m lead in under 6.5s using attack-only reverse, matches player speed, then completes a full warning and real damaging ram. Temporary scenes/objects/tuning, clocks, preferences, mission/campaign/currency/purchases and random state restored.");return;
                }
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }
        private static void Next(){phase++;phaseStarted=Time.realtimeSinceStartup;}
        private static void Finish(string report)
        {
            EditorApplication.update-=Tick;Directory.CreateDirectory("Temp/PsychoBug");File.WriteAllText("Temp/PsychoBug/PlayValidation.txt",report);
            if(report.StartsWith("FAIL"))Debug.LogError(report);else Debug.Log(report);
            Cleanup();EditorApplication.isPlaying=false;
        }
        private static void Cleanup()
        {
            if(pause!=null)pause.SetPaused(false);
            f?.Dispose();f=null;
            if(blackTuning!=null)Object.DestroyImmediate(blackTuning);blackTuning=null;blackInterceptor=null;
            if(gun!=null)Object.DestroyImmediate(gun);if(missionTuning!=null)Object.DestroyImmediate(missionTuning);
            if(previousScene.IsValid() && previousScene.isLoaded)SceneManager.SetActiveScene(previousScene);
            if(scene.IsValid() && scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
            if(!ownsState)return;
            Active(typeof(VoxelMissionProgress),previousMission);Active(typeof(VoxelPauseMenu),previousPause);Active(typeof(VoxelStartCountdown),previousCountdown);
            Time.timeScale=previousScale;UnityEngine.Random.state=random;saved?.Dispose();saved=null;ownsState=false;
        }
    }
}
