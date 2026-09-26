using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelBossValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target,string name,object value) => target.GetType().GetField(name,Private).SetValue(target,value);
        private static object Call(object target,string name,params object[] args) => target.GetType().GetMethod(name,Private).Invoke(target,args);
        private static void Check(bool value,string message) { if(!value) throw new Exception(message); }

        [MenuItem("Tools/Voxel Racer/Validate Red Van Boss")]
        public static void Run()
        {
            if(EditorApplication.isPlaying) throw new Exception("Run validation outside Play mode.");
            var random=UnityEngine.Random.state;
            var previousMission=VoxelMissionProgress.Active;
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root=new GameObject("Temporary boss validation");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var missionTuning=Object.Instantiate(VoxelMissionTuning.Load());
            var settings=ScriptableObject.CreateInstance<VoxelBossDefinition>();
            settings.bossPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(VoxelRedVanBossBuilder.PrefabPath);
            var mineAttack=ScriptableObject.CreateInstance<VoxelBossMineAttackTuning>();
            try
            {
                var playerObject=new GameObject("Player");playerObject.transform.SetParent(root.transform);
                var player=playerObject.AddComponent<VoxelCarController>();player.enabled=false;
                var piece=GameObject.CreatePrimitive(PrimitiveType.Cube);piece.transform.SetParent(player.transform);
                player.ResetIntegrityBaseline();
                Set(player,"<CurrentSpeed>k__BackingField",30f);
                var mission=root.AddComponent<VoxelMissionProgress>();mission.Configure(missionTuning);
                missionTuning.completionCurrencyAward=0;
                var road=root.AddComponent<EndlessVoxelRoad>();road.enabled=false;road.segmentCount=3;
                road.minimumCactiPerSegment=road.maximumCactiPerSegment=0;
                var spawner=root.AddComponent<VoxelObstacleSpawner>();spawner.obstacleCarTuning=VoxelObstacleCarTuning.Load();
                spawner.laneCount=4;spawner.laneWidth=3;spawner.SetTarget(player);
                var encounterSettings=new VoxelBossEncounterSettings { radarInterceptorSpawnChance=100, roadClearDuration=2 };
                mineAttack.dropChance=1;
                mineAttack.mineTuning=AssetDatabase.LoadAssetAtPath<VoxelMineLayerTuning>(VoxelBossMineBuilder.TuningPath);
                settings.attacks=new VoxelBossAttackDefinition[]{mineAttack};
                var encounter=root.AddComponent<VoxelBossEncounter>();encounter.enabled=false;
                encounter.Configure(settings,encounterSettings,player,road,spawner,mission,null);
                Check(mission.IsBossEncounter && encounter.CurrentStage==VoxelBossEncounter.Stage.Traffic,"Approach did not start");
                Check(spawner.obstacleCarTuning!=VoxelObstacleCarTuning.Load() && spawner.obstacleCarTuning.enemyCarSpawnChance==0,"Traffic isolation failed");
                VoxelMissionProgress.ReportEnemyVoxelDamage(100000);
                Check(!mission.IsComplete,"Score prematurely completed boss mission");
                Call(encounter,"AdvanceEncounter",500f);
                Check(encounter.CurrentStage==VoxelBossEncounter.Stage.Traffic,"Approach is still timed");
                Call(spawner,"SpawnObject",road,65f);
                var radar=spawner.GetComponentInChildren<VoxelObstacleCar>();
                Check(radar!=null && radar.IsEnemyTraffic,"Radar objective did not spawn");
                Check(radar.CurrentHealth==5 && radar.GetComponentInChildren<VoxelRadarDish>()!=null,"Weak tuning or radar dish missing");
                Call(spawner,"SpawnObject",road,100f);
                Check(spawner.GetComponentsInChildren<VoxelObstacleCar>().Count(c=>c.IsEnemyTraffic)==1,"Duplicate radar spawned");
                Object.DestroyImmediate(radar.gameObject);
                Check(encounter.CurrentStage==VoxelBossEncounter.Stage.Traffic,"Despawn completed objective");
                Call(spawner,"SpawnObject",road,140f);
                radar=spawner.GetComponentsInChildren<VoxelObstacleCar>().First(c=>c.IsEnemyTraffic);
                radar.TakeProjectileHit(null,1f,radar.transform.position,Vector3.forward);
                Check(radar.CurrentHealth==4 && encounter.CurrentStage==VoxelBossEncounter.Stage.Traffic,"Nonlethal hit completed objective");
                // Exercise the destruction notification without spawning play-mode-only debris in an editor scene.
                Call(radar,"ReportDestroyed",true);
                Check(radar.CurrentHealth==0,"Destruction left health");
                foreach(var civilian in spawner.GetComponentsInChildren<VoxelObstacleCar>())
                    if(civilian!=radar) Object.DestroyImmediate(civilian.gameObject);
                Object.DestroyImmediate(radar.gameObject);
                Check(encounter.CurrentStage==VoxelBossEncounter.Stage.Clearing && !spawner.enabled,"Traffic did not stop");
                var lastTraffic=new GameObject("Last civilian traffic");lastTraffic.transform.SetParent(spawner.transform);
                lastTraffic.AddComponent<VoxelObstacleCar>().enabled=false;
                Call(encounter,"AdvanceEncounter",2f);
                Call(encounter,"AdvanceEncounter",60f);
                encounter.SpawnBoss();
                Check(encounter.CurrentStage==VoxelBossEncounter.Stage.Clearing && encounter.Boss==null && lastTraffic.activeSelf,"Traffic was removed or boss spawned before road cleared");
                Object.DestroyImmediate(lastTraffic);
                Call(encounter,"AdvanceEncounter",0f);
                var boss=encounter.Boss;
                Check(boss!=null && boss.IsBoss && boss.CurrentHealth==settings.health &&
                    Mathf.Approximately(boss.Tuning.playerRamDamage,settings.playerRamDamage),"Boss spawn, health or player ram damage failed");
                if(mineAttack.mineTuning!=null)
                {
                    Check(boss.Tuning.mineLayer!=mineAttack.mineTuning,"Boss mutates shared mine tuning");
                    Check(boss.Tuning.mineLayer.minePrefab==mineAttack.mineTuning.minePrefab &&
                        boss.Tuning.mineLayer.playerDamageVoxelsMin==mineAttack.mineTuning.playerDamageVoxelsMin &&
                        boss.Tuning.mineLayer.playerDamageVoxelsMax==mineAttack.mineTuning.playerDamageVoxelsMax,
                        "Boss mine model or independent damage was overridden");
                }
                Check(boss.GetComponentInChildren<VoxelTrafficPaint>()==null,"Boss can be recoloured by traffic");
                Check(boss.GetComponentsInChildren<Transform>().Count(t=>t.name.EndsWith("Mine Dispenser"))==2,"Twin dispensers missing");
                Check(boss.GetComponentsInChildren<MeshRenderer>().Length<1250,"Boss mesh piece budget exceeded");
                Check((int)Call(boss,"GetRamVoxelLimit",200)==80 && (int)Call(boss,"GetRamVoxelLimit",20)==20,"Boss ram voxel cap failed");
                var entrance=boss.GetComponentInChildren<VoxelBossEntrance>();
                Check(entrance!=null && entrance.transform.localScale==Vector3.zero,"Boss entrance does not start at zero");
                Vector3 bossPosition=boss.transform.position;
                Call(entrance,"Advance",settings.entranceDuration*.5f);
                float halfway=entrance.transform.localScale.x;
                Check(halfway>0 && halfway<3f*1.8f/2.23f,"Boss entrance has no intermediate scale");
                Call(entrance,"Advance",settings.entranceDuration);
                Check(!entrance.enabled && boss.transform.localScale==Vector3.one && boss.transform.position==bossPosition,"Entrance changes gameplay root or never completes");
                float scale=boss.transform.GetChild(0).localScale.x;
                Check(Mathf.Abs(scale*2.23f-5.4f)<.01f,"Boss does not occupy two lanes");
                Set(boss,"trackDistance",settings.minimumDistanceAhead);
                Check((float)Call(boss,"GetBossDriveSpeed")>30,"Boss does not retreat at minimum distance");
                Set(boss,"trackDistance",settings.maximumDistanceAhead);
                Check((float)Call(boss,"GetBossDriveSpeed")<player.EffectiveTopSpeed,"Boss does not approach at maximum distance");
                Set(boss,"trackDistance",settings.catchUpResumeDistance);
                Set(player,"<CurrentSpeed>k__BackingField",0f);
                float pullAway=(float)Call(boss,"GetBossDriveSpeed");
                Check(pullAway>player.EffectiveTopSpeed,"Boss keeps braking after catch-up ends");
                Set(player,"<CurrentSpeed>k__BackingField",player.EffectiveTopSpeed);
                Check(Mathf.Approximately(pullAway,(float)Call(boss,"GetBossDriveSpeed")),"Player braking lowers boss combat target speed");
                float chaseGap=settings.maximumDistanceAhead;
                Set(boss,"currentSpeed",player.EffectiveTopSpeed);
                for(int step=0;step<3000;step++)
                {
                    Set(boss,"trackDistance",chaseGap);
                    float speed=(float)Call(boss,"AdvanceBossSpeed",.02f);
                    Set(boss,"currentSpeed",speed);
                    chaseGap+=(speed-player.EffectiveTopSpeed)*.02f;
                    Check(chaseGap>=settings.minimumDistanceAhead-1,"Steady-speed player catches boss and must brake");
                }
                Set(boss,"trackDistance",settings.minimumDistanceAhead);
                Set(boss,"currentSpeed",0f);
                Check(Mathf.Approximately((float)Call(boss,"AdvanceBossSpeed",.1f),settings.acceleration*.1f),"Boss acceleration rate incorrect");
                Set(boss,"currentSpeed",100f);
                Check(Mathf.Approximately((float)Call(boss,"AdvanceBossSpeed",.1f),100f-settings.braking*.1f),"Boss braking rate incorrect");
                Check(Mathf.Approximately((float)Call(boss,"AdvanceBossSpeed",0f),100f),"Boss speed changes while paused");
                Set(boss,"currentSpeed",0f);
                float desired=(float)Call(boss,"GetBossDriveSpeed");
                Check(desired>player.EffectiveTopSpeed,"Boss cannot burst ahead of an unboosted player");
                Check(Mathf.Approximately((float)Call(boss,"AdvanceBossSpeed",100f),desired),"Boss acceleration overshoots target speed");
                for(int i=0;i<20;i++)
                {
                    Set(boss,"nextBossLaneChange",-1f);Call(boss,"UpdateBossLaneChange");
                    float next=(float)typeof(VoxelEnemyCar).GetField("targetLaneOffset",Private).GetValue(boss);
                    Check(Mathf.Abs(next)<=3,"Boss targeted an off-road lane pair");Set(boss,"laneOffset",next);
                }
                Set(boss,"nextBossLaneChange",float.PositiveInfinity);Call(boss,"UpdateBossLaneChange");
                float pause=(float)typeof(VoxelEnemyCar).GetField("nextBossLaneChange",Private).GetValue(boss)-Time.time;
                Check(pause>=settings.minimumLaneChangeInterval && pause<=settings.maximumLaneChangeInterval,"Lane pause not scheduled after arrival");
                Set(boss,"nextMineTime",-1f);Call(boss,"UpdateMineLayer");
                var mines=root.GetComponentsInChildren<VoxelRoadMine>();
                Check(mines.Length==2,"Mine drop was not paired");
                Check(Mathf.Abs(Vector3.Distance(mines[0].transform.position,mines[1].transform.position)-3)<.01f,"Mines are not one lane apart");
                var effects=boss.GetComponent<VoxelVehicleDamageEffects>();
                foreach(float health in new[]{2000f,1500f,1000f})
                {
                    Set(boss,"<CurrentHealth>k__BackingField",health);Call(effects,"LateUpdate");
                    Check(effects.SmokeActive==(health<=1500) && effects.FireActive==(health<=1000),"Boss smoke/fire threshold incorrect");
                }
                mission.CompleteBossEncounter();Check(!mission.IsComplete,"Live boss can complete mission");
                Set(boss,"trackDistance",149f);Call(encounter,"CheckEscape");
                Check(!encounter.BossGettingAway && !mission.IsFailed,"Warning before 150m");
                Set(boss,"trackDistance",150f);Call(encounter,"CheckEscape");
                Check(encounter.BossGettingAway && !mission.IsFailed,"Warning missing at 150m");
                Set(boss,"trackDistance",199.9f);Call(encounter,"CheckEscape");
                Check(!mission.IsFailed,"Failed before 200m");
                Set(player,"<CurrentSpeed>k__BackingField",0f);
                float stoppedSpeed=(float)Call(boss,"GetBossDriveSpeed");
                Set(player,"<CurrentSpeed>k__BackingField",100f);
                Check(stoppedSpeed>0 && Mathf.Approximately(stoppedSpeed,(float)Call(boss,"GetBossDriveSpeed")),"Catch-up speed follows player boost/stopping");
                Set(boss,"trackDistance",200f);Call(encounter,"CheckEscape");
                Check(mission.IsFailed && !mission.IsComplete && encounter.CurrentStage==VoxelBossEncounter.Stage.Failed,"200m escape did not fail mission");
                Set(boss,"<CurrentHealth>k__BackingField",0f);mission.CompleteBossEncounter();
                Check(!mission.IsComplete,"Escaped boss awarded completion");
                mission.Configure(missionTuning);mission.SetBossEncounter(true);mission.ShowBoss(boss,"Test Boss");
                Set(encounter,"<CurrentStage>k__BackingField",VoxelBossEncounter.Stage.Boss);
                Set(boss,"<CurrentHealth>k__BackingField",0f);Call(encounter,"BossDefeated",boss);
                Check(mission.IsComplete && encounter.CurrentStage==VoxelBossEncounter.Stage.Complete,"Boss defeat did not complete mission");
                Check(mission.Percent==1,"Completion progress incorrect");
                Debug.Log("PASS boss: isolated traffic tuning, timed phases, score gating, prefab size/paint/dispensers, lane pairs, distance oscillation, paired mines, smoke/fire and defeat completion.");
            }
            finally
            {
                Object.DestroyImmediate(root);Object.DestroyImmediate(missionTuning);Object.DestroyImmediate(settings);Object.DestroyImmediate(mineAttack);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                typeof(VoxelMissionProgress).GetField("<Active>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,previousMission);
                UnityEngine.Random.state=random;
            }
        }

        [MenuItem("Tools/Voxel Racer/Render Red Van Boss")]
        public static void Render()
        {
            for(int side=0;side<2;side++)
            {
                var preview=new PreviewRenderUtility();
                try
                {
                    preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VoxelRedVanBossBuilder.PrefabPath)));
                    preview.camera.transform.position=new Vector3(6,3.4f,side==0?7.8f:-7.8f);
                    preview.camera.transform.LookAt(new Vector3(0,1,0));
                    preview.camera.fieldOfView=36;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.17f,.2f,.23f);
                    preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,side==0?25:155,0);
                    preview.lights[1].intensity=1.3f;preview.lights[1].transform.rotation=Quaternion.Euler(30,side==0?210:20,0);
                    preview.ambientColor=new Color(.45f,.45f,.45f);
                    preview.BeginStaticPreview(new Rect(0,0,1280,800));preview.Render(true);
                    var image=preview.EndStaticPreview();System.IO.File.WriteAllBytes("Temp/RedVanBoss"+side+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
                finally {preview.Cleanup();}
            }
        }
    }
}
