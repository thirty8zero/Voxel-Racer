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
                    traffic.enemyVehiclePool=new[]{null,invalid,only};
                    for(int i=0;i<200;i++) Check(traffic.ChooseEnemyVehicle()==only,"An excluded enemy was selected.");
                }
                traffic.enemyVehiclePool=new[]{interceptor,null,mineLayer,invalid,extra};
                var choices=new[]{interceptor,mineLayer,extra}; var counts=new int[3];
                for(int i=0;i<9000;i++)
                {
                    int index=Array.IndexOf(choices,traffic.ChooseEnemyVehicle());
                    Check(index>=0,"Null or invalid pool entry selected."); counts[index]++;
                }
                Check(counts.All(c=>c>2700 && c<3300),"Enemy pool selection is not uniform.");
                foreach(var empty in new[]{null,Array.Empty<VoxelEnemyVehicleTuning>(),new[]{null,invalid}})
                {
                    traffic.enemyVehiclePool=empty;
                    Check(traffic.ChooseEnemyVehicle()==null,"Empty/invalid pool must disable ordinary enemies.");
                }

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
                    traffic.enemyVehiclePool=new[]{only};spawn.Invoke(spawner,new object[]{null,65f});
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
                traffic.enemyVehiclePool=Array.Empty<VoxelEnemyVehicleTuning>();
                spawn.Invoke(spawner,new object[]{null,65f});
                Check(spawner.GetComponentInChildren<VoxelEnemyCar>()==null && spawner.GetComponentInChildren<VoxelObstacleCar>()!=null,
                    "Empty enemy pool must allow civilian traffic without ordinary enemies.");
                Object.DestroyImmediate(spawner.GetComponentInChildren<VoxelObstacleCar>().gameObject);
                traffic.enemyVehiclePool=new[]{interceptor};traffic.enemyCarSpawnChance=0;
                spawn.Invoke(spawner,new object[]{null,65f});
                Check(spawner.GetComponentInChildren<VoxelEnemyCar>()==null,"Enemy Spawn Chance zero was ignored.");

                var tracks=AssetDatabase.FindAssets("t:VoxelTrackDefinition")
                    .Select(g=>AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
                foreach(var tuning in tracks.Select(t=>t.obstacleCarTuning).Append(VoxelObstacleCarTuning.Load()).Where(t=>t!=null).Distinct())
                {
                    string before=EditorJsonUtility.ToJson(tuning);
                    var serialized=new SerializedObject(tuning);
                    Check(serialized.FindProperty("enemyVehiclePool").isArray,"Enemy pool is not Unity-serialized.");
                    using(var tree=PropertyTree.Create(tuning))
                    {
                        tree.UpdateTree();
                        var property=tree.EnumerateTree(true).First(p=>p.Name=="enemyVehiclePool");
                        Check(property.Attributes.OfType<FoldoutGroupAttribute>().Any(a=>a.GroupName=="Enemy Vehicles"),"Enemy pool is missing its Inspector foldout.");
                    }
                    for(int i=0;i<100;i++)
                    {
                        var chosen=tuning.ChooseEnemyVehicle();
                        Check(chosen==null || tuning.enemyVehiclePool.Contains(chosen),"A track selected an enemy outside its pool.");
                    }
                    Check(before==EditorJsonUtility.ToJson(tuning),"Validation changed a track's tuning.");
                }
                Directory.CreateDirectory("Temp");
                File.WriteAllText("Temp/EnemyVehiclePoolValidation.txt",
                    "PASS (Edit Mode): single-type and arbitrary-type pools, uniform selection, null/missing-model entries, empty pool disables enemies, actual spawner/model/durability/mine-layer integration, stale-reference exclusion, occupied lane rejection, civilian fallback, spawn-chance zero, track Inspector foldout/serialization and unchanged asset data. Random state restored.");
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
