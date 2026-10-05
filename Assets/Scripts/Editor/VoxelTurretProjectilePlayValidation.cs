using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Fires real turret volleys at the collider-free player prefab.</summary>
    public static class VoxelTurretProjectilePlayValidation
    {
        private const string Pending = "VoxelRacer.TurretProjectileCheck";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject root, visual, wall;
        private static VoxelCarController player;
        private static VoxelRoadsideTurret turret;
        private static VoxelRoadsideTurretTuning tuning;
        private static VoxelMissionTuning missionTuning;
        private static VoxelMissionProgress mission, previousMission;
        private static float previousScale, started, stageStarted;
        private static int initialIntegrity, phase, originalDamage, cash;
        private static bool ownsState, baseline;
        private static UnityEngine.Random.State random;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Turret Player Hits in Play Mode")]
        public static void Run() => Start(false);
        public static void RunBaseline() => Start(true);
        private static void Start(bool reproduce)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start from Edit Mode.");
            SessionState.SetBool(Pending+".Background",Application.runInBackground);
            SessionState.SetBool(Pending+".RestoreBackground",true);
            SessionState.SetBool(Pending+".Baseline",reproduce);
            SessionState.SetBool(Pending,true);
            Application.runInBackground=true;EditorApplication.isPlaying=true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending+".RestoreBackground",false))
            {
                Application.runInBackground=SessionState.GetBool(Pending+".Background",false);
                SessionState.SetBool(Pending+".RestoreBackground",false);
            }
            if(!SessionState.GetBool(Pending,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=Setup;
            if(state==PlayModeStateChange.ExitingPlayMode)
            {EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);Cleanup();}
        }

        private static void Check(bool value,string message) {if(!value)throw new InvalidOperationException(message);}
        private static void Setup()
        {
            try
            {
                previousScale=Time.timeScale;previousMission=VoxelMissionProgress.Active;
                cash=VoxelCurrencyState.Balance;random=UnityEngine.Random.state;ownsState=true;
                Time.timeScale=1;baseline=SessionState.GetBool(Pending+".Baseline",false);
                root=new GameObject("Temporary turret projectile QA");root.transform.position=new Vector3(10000,20,10000);
                missionTuning=ScriptableObject.CreateInstance<VoxelMissionTuning>();missionTuning.requiredPoints=1000000;
                mission=root.AddComponent<VoxelMissionProgress>();mission.Configure(missionTuning);
                var car=new GameObject("Collider-free player QA");car.transform.SetParent(root.transform,false);
                visual=Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab,car.transform);
                player=car.AddComponent<VoxelCarController>();player.enabled=false;player.debrisVoxelsPerDamagedVoxel=0;
                originalDamage=player.damageVoxelsPerHit;player.ResetIntegrityBaseline();initialIntegrity=player.RemainingIntegrityVoxels;
                Check(initialIntegrity>100 && car.GetComponentsInChildren<Collider>().Length==0,"Fixture must use the real collider-free player car");
                tuning=Object.Instantiate(Resources.Load<VoxelRoadsideTurretTuning>("EnemyTurrets/RoadsideTurretTuning"));
                tuning.playerDamageVoxels=3;tuning.bulletsPerVolley=1;tuning.projectileLifetime=.4f;
                var gun=new GameObject("Actual turret muzzle QA");gun.transform.SetParent(root.transform,false);
                turret=gun.AddComponent<VoxelRoadsideTurret>();turret.enabled=false;
                typeof(VoxelRoadsideTurret).GetField("target",Private).SetValue(turret,player);
                typeof(VoxelRoadsideTurret).GetField("tuning",Private).SetValue(turret,tuning);
                typeof(VoxelRoadsideTurret).GetMethod("BuildVisuals",Private).Invoke(turret,null);
                var muzzle=(Transform)typeof(VoxelRoadsideTurret).GetField("muzzleAnchor",Private).GetValue(turret);
                Check(muzzle!=null && Vector3.Distance(gun.transform.InverseTransformPoint(muzzle.position),new Vector3(0,1.18f,1.63f))<.001f && muzzle.lossyScale==Vector3.one,"New turret must retain original bullet height/origin and an unscaled muzzle");
                Check(gun.GetComponentsInChildren<MeshRenderer>().Length==1 && gun.GetComponentsInChildren<Collider>().Length==0,"Actual turret must use the combined collider-free prefab");
                phase=0;started=Time.realtimeSinceStartup;Fire(false);EditorApplication.update+=Tick;
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }

        private static void Fire(bool right,float heightOffset=0,bool intactSurface=false)
        {
            float longitudinal=0;
            if(intactSurface)
            {
                var query=typeof(VoxelCarController).GetMethod("TryFindHostileProjectileHit",Private);
                bool found=false;
                foreach(float offset in new[]{.5f,-.5f,1f,-1f,1.5f,-1.5f,2f,-2f})
                {
                    var args=new object[]{player.transform.TransformPoint(new Vector3(right?8:-8,1.18f,offset)),
                        player.transform.TransformDirection(right?Vector3.left:Vector3.right),16f,Vector3.zero,0f};
                    if(!(bool)query.Invoke(player,args))continue;
                    longitudinal=offset;found=true;break;
                }
                Check(found,"No intact body surface for the obstruction/completion test");
            }
            turret.transform.position=player.transform.TransformPoint(new Vector3(right?8:-8,heightOffset,longitudinal));
            turret.transform.rotation=Quaternion.LookRotation(player.transform.TransformDirection(right?Vector3.left:Vector3.right),player.transform.up);
            Physics.SyncTransforms();
            typeof(VoxelRoadsideTurret).GetMethod("FireVolley",Private).Invoke(turret,null);
            foreach(var shot in Object.FindObjectsByType<VoxelHostileProjectile>(FindObjectsSortMode.None))
                if((VoxelCarController)typeof(VoxelHostileProjectile).GetField("target",Private).GetValue(shot)==player)
                    shot.transform.SetParent(root.transform,true);
            stageStarted=Time.realtimeSinceStartup;
        }
        private static void ExpectedLoss(int amount)
        {
            Check(player.RemainingIntegrityVoxels==initialIntegrity-amount,"Expected "+amount+" lost voxels, got "+(initialIntegrity-player.RemainingIntegrityVoxels));
            Check(player.damageVoxelsPerHit==originalDamage,"Turret hit left the player's normal damage setting changed");
            Check(mission.Points==0 && Mathf.Approximately(mission.EffectiveTimeBonusMultiplier,1),"Hostile damage awarded score or multiplier");
        }

        private static void Tick()
        {
            if(!Application.isPlaying || EditorApplication.isPaused)return;
            if(Time.realtimeSinceStartup-started>20){Finish("FAIL: turret validation timed out.");return;}
            if(Time.realtimeSinceStartup-stageStarted<.55f)return;
            try
            {
                switch(phase)
                {
                    case 0:
                        if(baseline)
                        {
                            Check(player.RemainingIntegrityVoxels==initialIntegrity,"Baseline no longer reproduces the miss");
                            Finish("REPRODUCED: a real turret volley crosses the main player model at muzzle height, but applies no damage because the prefab has zero physics colliders.");return;
                        }
                        ExpectedLoss(3);
                        CheckMeshQueries();
                        player.transform.localRotation=Quaternion.Euler(0,37,0);Fire(true);break;
                    case 1:
                        ExpectedLoss(6);
                        wall=new GameObject("Closer turret obstruction QA");wall.transform.SetParent(root.transform);
                        wall.transform.position=player.transform.TransformPoint(new Vector3(-3,1.18f,0));wall.transform.rotation=player.transform.rotation;
                        wall.AddComponent<BoxCollider>().size=new Vector3(.5f,4,4);Fire(false);break;
                    case 2:
                        ExpectedLoss(6);
                        wall.transform.position=player.transform.TransformPoint(new Vector3(4,1.18f,0));Fire(false,intactSurface:true);break;
                    case 3:
                        ExpectedLoss(9);wall.SetActive(false);Fire(false,5);break;
                    case 4:
                        ExpectedLoss(9);
                        foreach(var r in player.GetComponentsInChildren<MeshRenderer>())r.enabled=false;
                        Fire(false);break;
                    case 5:
                        ExpectedLoss(9);
                        Object.DestroyImmediate(visual);
                        visual=Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab,player.transform);
                        player.ResetIntegrityBaseline();initialIntegrity=player.RemainingIntegrityVoxels;
                        Fire(false);break;
                    case 6:
                        ExpectedLoss(3);Time.timeScale=0;Fire(true);break;
                    case 7:
                        ExpectedLoss(3);Time.timeScale=1;stageStarted=Time.realtimeSinceStartup;break;
                    case 8:
                        ExpectedLoss(6);
                        Check(Mathf.Approximately(mission.Breakdown.Total(VoxelMissionBreakdown.Group.IntegrityLost),15),"Turret integrity damage missing from breakdown");
                        missionTuning.requiredPoints=1;VoxelMissionProgress.ReportEnemyVoxelDamage();
                        Check(mission.IsComplete,"Completion fixture failed");Fire(false,intactSurface:true);break;
                    case 9:
                        Check(player.RemainingIntegrityVoxels==initialIntegrity-6,"Turret bullet damaged player after mission completion");
                        Check(root.GetComponentsInChildren<VoxelHostileProjectile>().Length==0,"Completed mission retained hostile bullets");
                        Finish("PASS: actual turret muzzle volleys hit the collider-free player from both sides and a rotated heading, remove exactly the configured voxels, restore normal damage settings and report integrity losses without score; nearer walls block shots, farther walls do not, high shots and disabled renderers miss, cache rebuild follows model replacement, pause freezes bullets, completion cancels them. Temporary objects, tuning clones, cash, mission state, random state and clocks restored.");return;
                }
                phase++;
            }
            catch(Exception e){Finish("FAIL: "+e);}
        }

        private static void CheckMeshQueries()
        {
            var query=typeof(VoxelCarController).GetMethod("TryFindHostileProjectileHit",Private);
            Check(query!=null,"Player mesh hit query missing");
            Vector3 start=player.transform.TransformPoint(new Vector3(8,1.18f,0));
            Vector3 direction=player.transform.TransformDirection(Vector3.left);
            var args=new object[]{start,direction,16f,Vector3.zero,0f};
            Check((bool)query.Invoke(player,args),"Finite segment mesh query missed player");
            float distance=(float)args[4];Vector3 point=(Vector3)args[3];args[2]=distance-.01f;
            Check(!(bool)query.Invoke(player,args),"Mesh query hit beyond its segment");
            args[0]=point+direction*.001f;args[2]=.01f;
            Check((bool)query.Invoke(player,args),"Bullet starting on a voxel surface missed");
            args[0]=start+Vector3.up*10;args[2]=16f;
            Check(!(bool)query.Invoke(player,args),"Mesh query hit an overhead miss");
            args[0]=start;player.gameObject.SetActive(false);
            Check(!(bool)query.Invoke(player,args),"Inactive player caught a shot");player.gameObject.SetActive(true);
        }

        private static void Finish(string report)
        {
            EditorApplication.update-=Tick;Directory.CreateDirectory("Temp");
            File.WriteAllText(baseline?"Temp/TurretProjectileBaseline.txt":"Temp/TurretProjectilePlayValidation.txt",report);
            if(report.StartsWith("FAIL"))Debug.LogError(report);else Debug.Log(report);
            Cleanup();EditorApplication.isPlaying=false;
        }
        private static void Cleanup()
        {
            if(root!=null)Object.DestroyImmediate(root);
            if(tuning!=null)Object.DestroyImmediate(tuning);if(missionTuning!=null)Object.DestroyImmediate(missionTuning);
            root=null;player=null;turret=null;tuning=null;missionTuning=null;mission=null;wall=null;visual=null;
            if(!ownsState)return;
            typeof(VoxelMissionProgress).GetField("<Active>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,previousMission);
            Time.timeScale=previousScale;UnityEngine.Random.state=random;
            VoxelCurrencyState.Reset();VoxelCurrencyState.Add(cash);ownsState=false;
        }
    }
}
