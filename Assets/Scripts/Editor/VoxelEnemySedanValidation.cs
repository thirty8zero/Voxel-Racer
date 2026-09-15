using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelEnemySedanValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Black Interceptor Model")]
        public static void Run()
        {
            var enemyRoot=new GameObject("Temporary sedan test enemy");
            var random=UnityEngine.Random.state;
            try
            {
                var enemy=enemyRoot.AddComponent<VoxelEnemyCar>(); enemy.enabled=false;
                var tuning=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>("Assets/Resources/EnemyVehicles/BlackInterceptorTuning.asset");
                Check(tuning.modelPrefab!=null,"Prefab is not assigned");
                typeof(VoxelEnemyCar).GetField("<Tuning>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(enemy,tuning);
                typeof(VoxelEnemyCar).GetMethod("CreateModel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(enemy,new object[]{tuning});
                var model=enemyRoot.transform.GetChild(0);
                int count=model.GetComponentsInChildren<MeshRenderer>().Length;
                Check(count>500 && count<800,"Unexpected piece budget: "+count);
                Check(model.GetComponentsInChildren<Collider>().Length==0,"Unexpected per-piece physics colliders");
                var wheels=(Transform[])typeof(VoxelEnemyCar).GetField("modelWheels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(enemy);
                Check(wheels.Length==4,"Nested wheels were not found");
                Check(enemy.TryGetNextProjectileVoxel(new Vector3(0,.8f,-5),Vector3.forward,10,out Transform voxel),"Projectile did not find the new body");
                Check(voxel.IsChildOf(model),"Projectile selected something outside the model");
                voxel.gameObject.SetActive(false);
                Check(enemy.TryGetNextProjectileVoxel(new Vector3(0,.8f,-5),Vector3.forward,10,out Transform next) && next!=voxel,"Removed piece remained a projectile target");
                Debug.Log("Enemy sedan validation passed: "+count+" pieces, 4 rotating wheel roots, prefab spawning, projectile selection, removed-piece exclusion, no piece colliders.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyRoot);
                UnityEngine.Random.state=random;
            }
        }
        private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    }
}
