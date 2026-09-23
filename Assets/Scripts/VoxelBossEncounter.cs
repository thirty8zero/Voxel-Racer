using UnityEngine;
namespace VoxelRacer
{
    public sealed class VoxelBossEncounter : MonoBehaviour
    {
        public enum Stage { Traffic, Clearing, Boss, Complete, Failed }
        public Stage CurrentStage {get;private set;}
        public VoxelEnemyCar Boss {get;private set;}
        public float BossDistance => Boss!=null && player!=null ? Mathf.Max(0,Boss.TrackDistance-player.CollisionTrackPosition.y) : 0;
        public bool BossGettingAway => CurrentStage==Stage.Boss && BossDistance>=settings.WarningDistance;
        private VoxelBossSettings settings;
        private VoxelCarController player;
        private EndlessVoxelRoad road;
        private VoxelObstacleSpawner spawner;
        private VoxelMissionProgress mission;
        private VoxelStartCountdown countdown;
        private VoxelObstacleCarTuning traffic;
        private VoxelEnemyVehicleTuning enemy;
        private VoxelMineLayerTuning mines;
        private float elapsed;
        public void Configure(VoxelBossSettings value,VoxelCarController target,EndlessVoxelRoad path,
            VoxelObstacleSpawner trafficSpawner,VoxelMissionProgress progress,VoxelStartCountdown start)
        {
            settings=value ?? new VoxelBossSettings();player=target;road=path;spawner=trafficSpawner;mission=progress;countdown=start;
            CurrentStage=Stage.Traffic;elapsed=0;mission.SetBossEncounter(true);
            traffic=Instantiate(spawner.obstacleCarTuning);
            traffic.obstacleCarSpawnChance=1;traffic.enemyCarSpawnChance=0;
            traffic.oppositeDirectionChance=settings.oncomingTrafficChance;
            traffic.minimumObjectsPerWave=Mathf.Max(1,settings.minimumVehiclesPerWave);
            traffic.maximumObjectsPerWave=Mathf.Max(traffic.minimumObjectsPerWave,settings.maximumVehiclesPerWave);
            traffic.minimumWaveObjectDistanceOffset=Mathf.Max(1,settings.minimumVehicleSpacing);
            traffic.maximumWaveObjectDistanceOffset=Mathf.Max(traffic.minimumWaveObjectDistanceOffset,settings.maximumVehicleSpacing);
            spawner.obstacleCarTuning=traffic;
            spawner.minimumSpawnInterval=Mathf.Max(.1f,settings.minimumWaveInterval);
            spawner.maximumSpawnInterval=Mathf.Max(spawner.minimumSpawnInterval,settings.maximumWaveInterval);
        }
        private void Update()
        {
            if(player==null || player.IsDestroyed || mission.IsComplete || mission.IsFailed || (countdown!=null && !countdown.IsComplete)) return;
            AdvanceEncounter(Time.deltaTime);
            CheckEscape();
        }
        internal void CheckEscape()
        {
            if(CurrentStage!=Stage.Boss || Boss==null || Boss.CurrentHealth<=0 || mission.IsComplete || mission.IsFailed) return;
            if(BossDistance<settings.FailureDistance) return;
            CurrentStage=Stage.Failed;
            mission.FailBossEncounter();
            if(Application.isPlaying)
            {
                var failure=FindFirstObjectByType<VoxelPlayerDeathScreen>();
                if(failure==null) {failure=gameObject.AddComponent<VoxelPlayerDeathScreen>();failure.Configure(player);}
                failure.ShowBossEscaped();
            }
        }
        private void OnGUI()
        {
            if(!Application.isPlaying || !BossGettingAway || player.IsDestroyed || VoxelPlayerDeathScreen.IsShowing) return;
            var style=new GUIStyle(GUI.skin.label) { font=VoxelHudStyles.HudFont,fontSize=26,alignment=TextAnchor.MiddleCenter };
            style.normal.textColor=Color.Lerp(new Color(1f,.7f,.1f),Color.red,.5f+.5f*Mathf.Sin(Time.unscaledTime*6));
            GUI.Label(new Rect((Screen.width-540)*.5f,112,540,34),$"BOSS GETTING AWAY — {Mathf.FloorToInt(BossDistance)}m / {settings.FailureDistance:0}m",style);
        }
        internal void AdvanceEncounter(float seconds)
        {
            elapsed+=Mathf.Max(0,seconds);
            if(CurrentStage==Stage.Traffic && elapsed>=settings.trafficDuration)
            {
                CurrentStage=Stage.Clearing;elapsed=0;spawner.StopSpawning();
            }
            if(CurrentStage==Stage.Clearing && elapsed>=settings.roadClearDuration && !HasRemainingTraffic()) SpawnBoss();
        }
        private bool HasRemainingTraffic() => spawner.GetComponentInChildren<VoxelObstacleCar>()!=null;
        public void SpawnBoss()
        {
            if(CurrentStage==Stage.Boss || CurrentStage==Stage.Complete || CurrentStage==Stage.Failed) return;
            // Never remove vehicles to make room, including when an editor command requests a spawn.
            if(HasRemainingTraffic()) return;
            spawner.StopSpawning();
            enemy=Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"));
            enemy.displayName=settings.bossName;
            enemy.modelPrefab=settings.bossPrefab!=null?settings.bossPrefab:Resources.Load<GameObject>("Bosses/RedTransitBoss");
            enemy.vehicleHealth=Mathf.Max(1,settings.health);enemy.voxelHealth=Mathf.Max(.01f,settings.voxelHealth);
            enemy.playerDamageVoxelsMin=settings.playerCollisionDamageMin;enemy.playerDamageVoxelsMax=settings.playerCollisionDamageMax;
            enemy.laneChangeChance=0;enemy.destructionScore=0;
            float width=spawner.laneWidth;
            enemy.collisionHalfWidth=width*.9f+1f;
            enemy.collisionHalfLength=2.65f*(width*1.8f/2.23f);
            if(settings.minesEnabled)
            {
                mines=Instantiate(settings.mineTuning!=null ? settings.mineTuning : Resources.Load<VoxelMineLayerTuning>("EnemyVehicles/MineLayerAttackTuning"));
                mines.dropChance=settings.mineDropChance;mines.dropInterval=settings.mineDropInterval;
                mines.armingDelay=settings.mineArmingDelay;mines.lifetime=settings.mineLifetime;
                if(settings.mineTuning==null)
                {
                    mines.playerDamageVoxelsMin=settings.mineDamageMin;mines.playerDamageVoxelsMax=settings.mineDamageMax;
                    mines.explosionScale=settings.mineExplosionScale;
                }
                mines.postDropLaneChangeChance=0;enemy.mineLayer=mines;
            }
            else enemy.mineLayer=null;
            float distance=player.CollisionTrackPosition.y+Mathf.Clamp(Mathf.Max(settings.minimumDistanceAhead,settings.maximumDistanceAhead),1,settings.WarningDistance*.8f);
            road.EnsurePathCovers(distance+30);
            var go=new GameObject(settings.bossName);go.transform.SetParent(spawner.transform,false);
            Boss=go.AddComponent<VoxelEnemyCar>();
            Boss.ConfigureBoss(settings,width,spawner.laneCount);
            Boss.Configure(player,traffic,enemy,road,distance,0);
            Boss.Defeated+=BossDefeated;
            mission.ShowBoss(Boss,settings.bossName);
            CurrentStage=Stage.Boss;
        }
        private void BossDefeated(VoxelEnemyCar defeated)
        {
            if(defeated!=Boss || CurrentStage!=Stage.Boss) return;
            CurrentStage=Stage.Complete;mission.CompleteBossEncounter();
        }
        private void OnDestroy()
        {
            if(Boss!=null) Boss.Defeated-=BossDefeated;
            foreach(var asset in new Object[]{traffic,enemy,mines}) if(asset!=null)
            {if(Application.isPlaying)Destroy(asset);else DestroyImmediate(asset);}
        }
    }
}
