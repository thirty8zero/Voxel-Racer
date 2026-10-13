using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelEnemyVehiclePoolValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        private static VoxelEnemyVehicleSpawnEntry Entry(VoxelEnemyVehicleTuning vehicle, float weight = 1f) =>
            new VoxelEnemyVehicleSpawnEntry { vehicle = vehicle, spawnWeight = weight };
        private static void ReleasePreviewBars(GameObject owner)
        {
            // Non-ExecuteAlways behaviours do not receive every runtime lifecycle
            // callback in Edit Mode. Simulate material disposal before removing QA objects.
            foreach(var bar in owner.GetComponentsInChildren<VoxelEnemyHealthBar>(true))
                typeof(VoxelEnemyHealthBar).GetMethod("OnDestroy",Private).Invoke(bar,null);
        }

        [MenuItem("Tools/Voxel Racer/Validate Enemy Vehicle Pools")]
        public static void Run()
        {
            Check(!Application.isPlaying,"Run enemy pool validation outside Play Mode.");
            var random = UnityEngine.Random.state;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary Enemy Pool QA");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var traffic = Object.Instantiate(VoxelObstacleCarTuning.Load());
            var interceptor = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning");
            var mineLayer = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BI_MineLayerTuning");
            var extra = Object.Instantiate(interceptor);
            var invalid = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>();
            try
            {
                Check(interceptor.modelPrefab!=null && mineLayer.modelPrefab!=null,"Missing initial enemy models.");
                UnityEngine.Random.InitState(21841);
                extra.displayName="Additional pooled enemy";
                foreach(var only in new[]{interceptor,mineLayer,extra})
                {
                    traffic.enemyVehicleSpawns=new[]{null,Entry(invalid),Entry(only)};
                    for(int i=0;i<200;i++) Check(traffic.ChooseEnemyVehicle()==only,"An excluded enemy was selected.");
                }
                traffic.enemyVehicleSpawns=new[]{Entry(interceptor),null,Entry(mineLayer),Entry(invalid),Entry(extra)};
                var choices=new[]{interceptor,mineLayer,extra}; var counts=new int[3];
                for(int i=0;i<9000;i++)
                {
                    int index=Array.IndexOf(choices,traffic.ChooseEnemyVehicle());
                    Check(index>=0,"Null or invalid pool entry selected."); counts[index]++;
                }
                Check(counts.All(c=>c>2700 && c<3300),"Enemy pool selection is not uniform.");
                foreach(var empty in new[]{null,Array.Empty<VoxelEnemyVehicleSpawnEntry>(),new[]{null,Entry(invalid)},
                    new[]{Entry(interceptor,0),Entry(mineLayer,-1),Entry(extra,float.NaN),Entry(interceptor,float.PositiveInfinity)}})
                {
                    traffic.enemyVehicleSpawns=empty;
                    Check(traffic.ChooseEnemyVehicle()==null,"Empty/invalid pool must disable ordinary enemies.");
                }

                // Fractional weights, disabled entries and invalid models must all
                // affect the same relative draw used by the real spawner.
                traffic.enemyVehicleSpawns=new[]{Entry(interceptor,.25f),Entry(mineLayer,.75f),Entry(extra,0),Entry(invalid,1000),null};
                int weightedFirst=0;
                for(int i=0;i<12000;i++)
                {
                    var chosen=traffic.ChooseEnemyVehicle();
                    Check(chosen==interceptor || chosen==mineLayer,"A disabled/invalid weighted entry was selected.");
                    if(chosen==interceptor) weightedFirst++;
                }
                Check(weightedFirst>2700 && weightedFirst<3300,"Fractional weights did not produce a 1:3 distribution.");
                traffic.enemyVehicleSpawns=new[]{Entry(interceptor,1),Entry(interceptor,2),Entry(mineLayer,1)};
                int duplicates=0;
                for(int i=0;i<12000;i++) if(traffic.ChooseEnemyVehicle()==interceptor) duplicates++;
                Check(duplicates>8700 && duplicates<9300,"Duplicate entries did not combine their weights.");
                traffic.enemyVehicleSpawns=new[]{Entry(interceptor,float.MaxValue),Entry(mineLayer,float.MaxValue)};
                int largeWeights=0;
                for(int i=0;i<4000;i++) if(traffic.ChooseEnemyVehicle()==interceptor) largeWeights++;
                Check(largeWeights>1800 && largeWeights<2200,"Large finite weights overflowed selection.");

                // Legacy migration preserves order, duplicate and empty references,
                // then retires the old field so an intentionally emptied pool stays empty.
                var legacy=typeof(VoxelObstacleCarTuning).GetField("legacyEnemyVehiclePool",Private);
                var migrate=typeof(VoxelObstacleCarTuning).GetMethod("MigrateEnemyVehiclePool",Private);
                traffic.enemyVehicleSpawns=null;
                legacy.SetValue(traffic,new[]{interceptor,null,mineLayer,interceptor});
                Check((bool)migrate.Invoke(traffic,null),"Legacy pool did not migrate.");
                Check(traffic.enemyVehicleSpawns.Select(e=>e.vehicle).SequenceEqual(new[]{interceptor,null,mineLayer,interceptor}) &&
                    traffic.enemyVehicleSpawns.All(e=>e.spawnWeight==1),"Migration changed legacy entries or equal chances.");
                traffic.enemyVehicleSpawns[0].spawnWeight=3;
                Check(!(bool)migrate.Invoke(traffic,null) && traffic.enemyVehicleSpawns[0].spawnWeight==3,"Migration reset an authored weight.");
                traffic.enemyVehicleSpawns=Array.Empty<VoxelEnemyVehicleSpawnEntry>();
                Check(traffic.ChooseEnemyVehicle()==null,"An emptied migrated pool was repopulated.");

                var playerObject=new GameObject("QA player");playerObject.transform.SetParent(root.transform);
                var player=playerObject.AddComponent<VoxelCarController>();player.enabled=false;
                var spawner=root.AddComponent<VoxelObstacleSpawner>();spawner.enabled=false;
                spawner.SetTarget(player);spawner.obstacleCarTuning=traffic;spawner.laneCount=1;
                // Stale scene references must never leak types excluded by the track.
                spawner.enemyCarTuning=interceptor;spawner.mineLayerEnemyTuning=mineLayer;
                traffic.obstacleCarSpawnChance=1;traffic.enemyCarSpawnChance=1;
                var spawn=typeof(VoxelObstacleSpawner).GetMethod("SpawnObject",Private);
                foreach(var only in choices)
                {
                    traffic.enemyVehicleSpawns=new[]{Entry(interceptor,0),Entry(mineLayer,0),Entry(extra,0),Entry(only,2)};spawn.Invoke(spawner,new object[]{null,65f});
                    var enemy=spawner.GetComponentInChildren<VoxelEnemyCar>();
                    Check(enemy!=null && enemy.Tuning==only && enemy.CurrentHealth==only.vehicleHealth,"Actual spawn ignored the track pool or durability.");
                    Check(enemy.GetComponentsInChildren<MeshRenderer>().Length>0,"Pooled enemy model was not built.");
                    var bar=enemy.GetComponentInChildren<VoxelEnemyHealthBar>();
                    Check(bar!=null,"Pooled enemy health bar missing.");
                    var materials=bar.GetComponentsInChildren<Renderer>().Select(r=>r.sharedMaterial).ToArray();
                    bar.SetHealth(.25f);
                    Check(materials.Any(m=>m.GetColor("_BaseColor")==Color.Lerp(only.healthBarEmptyColour,only.healthBarFullColour,.25f)),
                        "Health bar colour no longer tracks health.");
                    if(only==mineLayer) Check(enemy.Tuning.mineLayer!=null,"Mine-layer attack configuration lost.");
                    spawn.Invoke(spawner,new object[]{null,85f});
                    Check(spawner.GetComponentsInChildren<VoxelEnemyCar>().Length==1,"Enemy pool bypassed lane occupancy.");
                    ReleasePreviewBars(enemy.gameObject);Object.DestroyImmediate(enemy.gameObject);
                    Check(materials.All(m=>m==null),"Temporary health bar materials leaked after removal.");
                }
                traffic.enemyVehicleSpawns=new[]{Entry(interceptor,0),Entry(mineLayer,0)};
                spawn.Invoke(spawner,new object[]{null,65f});
                Check(spawner.GetComponentInChildren<VoxelEnemyCar>()==null && spawner.GetComponentInChildren<VoxelObstacleCar>()!=null,
                    "Zero-weight enemy pool must allow civilian traffic without ordinary enemies.");
                Object.DestroyImmediate(spawner.GetComponentInChildren<VoxelObstacleCar>().gameObject);
                traffic.enemyVehicleSpawns=new[]{Entry(interceptor)};traffic.enemyCarSpawnChance=0;
                spawn.Invoke(spawner,new object[]{null,65f});
                Check(spawner.GetComponentInChildren<VoxelEnemyCar>()==null,"Enemy Spawn Chance zero was ignored.");

                var tracks=AssetDatabase.FindAssets("t:VoxelTrackDefinition")
                    .Select(g=>AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
                foreach(var tuning in tracks.Select(t=>t.obstacleCarTuning).Append(VoxelObstacleCarTuning.Load()).Where(t=>t!=null).Distinct())
                {
                    string before=EditorJsonUtility.ToJson(tuning);
                    var serialized=new SerializedObject(tuning);
                    var pool=serialized.FindProperty("enemyVehicleSpawns");
                    Check(pool.isArray,"Enemy pool is not Unity-serialized.");
                    for(int i=0;i<pool.arraySize;i++)
                        Check(pool.GetArrayElementAtIndex(i).FindPropertyRelative("vehicle")!=null &&
                            pool.GetArrayElementAtIndex(i).FindPropertyRelative("spawnWeight")!=null,"Pool entry is missing its serialized vehicle/weight pair.");
                    using(var tree=PropertyTree.Create(tuning))
                    {
                        tree.UpdateTree();
                        var property=tree.EnumerateTree(true).First(p=>p.Name=="enemyVehicleSpawns");
                        Check(property.Attributes.OfType<FoldoutGroupAttribute>().Any(a=>a.GroupName=="Enemy Vehicles"),"Enemy pool is missing its Inspector foldout.");
                    }
                    for(int i=0;i<100;i++)
                    {
                        var chosen=tuning.ChooseEnemyVehicle();
                        Check(chosen==null || tuning.enemyVehicleSpawns.Any(e=>e!=null && e.vehicle==chosen && e.spawnWeight>0),"A track selected an enemy outside its enabled pool.");
                    }
                    Check(before==EditorJsonUtility.ToJson(tuning),"Validation changed a track's tuning.");
                }
                Directory.CreateDirectory("Temp");
                File.WriteAllText("Temp/EnemyVehiclePoolValidation.txt",
                    "PASS (Edit Mode): single/arbitrary types, default equal selection, fractional 1:3 weighted selection, combined duplicate weights, zero/negative/nonfinite weights, large finite weights, null/missing models, empty/all-disabled pools, idempotent legacy migration preserving order/references/default weights, actual weighted spawner/model/durability/mine-layer integration, stale-reference exclusion, occupied lanes, civilian fallback, spawn-chance zero, serialized vehicle/weight pairs, Inspector foldout and unchanged asset data. Random state restored.");
                Debug.Log("PASS Enemy Vehicle Pools: selection, actual spawning, lane occupancy and track Inspector integration.");
            }
            finally
            {
                ReleasePreviewBars(root);Object.DestroyImmediate(root);Object.DestroyImmediate(traffic);Object.DestroyImmediate(extra);Object.DestroyImmediate(invalid);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Random.state=random;
            }
        }
    }
}
