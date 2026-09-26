using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelRadarProjectileValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Radar Projectile Hits")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play mode.");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary radar projectile validation");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.transform.position = new Vector3(10000,10000,10000);
                var tuning = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/RadarInterceptorTuning");
                var car = Object.Instantiate(tuning.modelPrefab, root.transform).AddComponent<VoxelObstacleCar>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(VoxelObstacleCar).GetMethod("OnEnable",flags).Invoke(car,null);
                typeof(VoxelObstacleCar).GetField("<EnemyTuning>k__BackingField", flags).SetValue(car,tuning);
                typeof(VoxelObstacleCar).GetField("<CurrentHealth>k__BackingField", flags).SetValue(car,5f);
                if(car.GetComponentsInChildren<Collider>().Length != 0) throw new Exception("Fixture should test collider-free model");
                foreach(float angle in new[] {0f, 180f, 37f})
                {
                    car.transform.localRotation = Quaternion.Euler(0,angle,0);
                    Vector3 start = car.transform.TransformPoint(new Vector3(0,.7f,-6));
                    Vector3 direction = car.transform.forward;
                    if(!VoxelObstacleCar.TryFindProjectileHit(start,direction,12,out var target,out var voxel,out var point,out float distance) || target!=car)
                        throw new Exception("Radar bullet missed at angle " + angle);
                    if(VoxelObstacleCar.TryFindProjectileHit(start,direction,distance-.01f,out _,out _,out _,out _))
                        throw new Exception("Bullet hit beyond its segment");
                    if(VoxelObstacleCar.TryFindProjectileHit(start+Vector3.up*5,direction,12,out _,out _,out _,out _))
                        throw new Exception("Bullet above vehicle hit");
                    voxel.gameObject.SetActive(false);
                    if(VoxelObstacleCar.TryFindProjectileHit(start,direction,12,out _,out var next,out _,out _) && next==voxel)
                        throw new Exception("Removed voxel caught bullet");
                    voxel.gameObject.SetActive(true);
                }
                // Exercise health application without creating runtime debris in the editor.
                car.TakeProjectileHit(null,1,car.transform.position,Vector3.forward);
                if(car.CurrentHealth!=4) throw new Exception("Radar health did not decrease");
                typeof(VoxelObstacleCar).GetField("hasBeenHit",flags).SetValue(car,true);
                if(VoxelObstacleCar.TryFindProjectileHit(car.transform.TransformPoint(new Vector3(0,.7f,-6)),car.transform.forward,12,out _,out _,out _,out _))
                    throw new Exception("Destroyed radar still catches bullets");
                Debug.Log("PASS radar projectile detection: forward/oncoming/rotated, finite range, vertical misses, inactive voxels, health and wreck exclusion.");
            }
            finally
            {
                foreach(var car in root.GetComponentsInChildren<VoxelObstacleCar>(true))
                    typeof(VoxelObstacleCar).GetMethod("OnDisable",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(car,null);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
