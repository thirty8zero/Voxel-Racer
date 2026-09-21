using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelCivilianVanValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Civilian Transit Van")]
        public static void Run()
        {
            var random=UnityEngine.Random.state;
            var root=new GameObject("Temporary van validation");
            var tuning=UnityEngine.Object.Instantiate(VoxelObstacleCarTuning.Load());
            try
            {
                tuning.semiTrailerSpawnChance=1;
                tuning.semiTrailerEnemyTuning=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(VoxelCivilianVanBuilder.TuningPath);
                tuning.paintColours=new[]{Color.magenta};
                var player=root.AddComponent<VoxelCarController>();player.enabled=false;
                foreach(bool sameDirection in new[]{true,false})
                {
                    var go=new GameObject("Van test");go.transform.SetParent(root.transform);
                    var car=go.AddComponent<VoxelObstacleCar>();car.enabled=false;
                    car.Configure(player,tuning,sameDirection,null,70,3,12);
                    Check(car.TravelsWithPlayer==sameDirection && car.TravelSpeed==12,"Civilian movement configuration changed");
                    Check(car.EnemyTuning==tuning.semiTrailerEnemyTuning,"Van tuning not selected");
                    var renderers=go.GetComponentsInChildren<MeshRenderer>();
                    Check(renderers.Length>300 && renderers.Length<650,"Piece budget exceeded: "+renderers.Length);
                    Check(go.GetComponentsInChildren<Collider>().Length==0,"Per-piece colliders present");
                    var paint=go.GetComponentInChildren<VoxelTrafficPaint>();Check(paint!=null,"Van paint marker missing");
                    foreach(var renderer in renderers)
                    {
                        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                        if(renderer.sharedMaterial==paint.bodyMaterial) Check(block.GetColor("_BaseColor")==Color.magenta,"Body tint failed");
                        else Check(block.isEmpty,"Trim was recoloured");
                    }
                    const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var wheels=(Transform[])typeof(VoxelObstacleCar).GetField("modelWheels",flags).GetValue(car);
                    Check(wheels.Length==4,"Four rotating wheel roots not found");
                    Check((float)typeof(VoxelObstacleCar).GetField("collisionHalfLength",flags).GetValue(car)==2.65f,"Old truck collision length retained");
                    Check(go.transform.Find("Damage Smoke").localPosition.z<2,"Smoke is outside bonnet");
                    typeof(VoxelObstacleCar).GetField("<EnemyTuning>k__BackingField",flags).SetValue(car,null);
                    var damage=typeof(VoxelObstacleCar).GetMethod("ApplyVoxelDamage",flags);
                    var style=Enum.ToObject(damage.GetParameters()[3].ParameterType,0);
                    int removed=(int)damage.Invoke(car,new object[]{new Vector3(0,1,3),Vector3.back,10,style});
                    Check(removed==10 && go.GetComponentsInChildren<MeshRenderer>().Length==renderers.Length-10,"Van destruction failed");
                    damage.Invoke(car,new object[]{new Vector3(0,1,3),Vector3.back,10,style});
                    Check(go.GetComponentsInChildren<MeshRenderer>().Length==renderers.Length-20,"Removed voxels reused");
                    Debug.Log("PASS van "+(sameDirection?"same direction":"oncoming")+": "+renderers.Length+" pieces, paint, wheels, damage, bounds and smoke origin.");
                }
                foreach(string guid in AssetDatabase.FindAssets("t:VoxelTrackDefinition"))
                {
                    var track=AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    Check(track.obstacleCarTuning.semiTrailerEnemyTuning==tuning.semiTrailerEnemyTuning,"Track still references truck: "+track.name);
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(tuning);UnityEngine.Random.state=random;}
        }
        private static void Check(bool value,string message) {if(!value) throw new Exception(message);}
    }
}
