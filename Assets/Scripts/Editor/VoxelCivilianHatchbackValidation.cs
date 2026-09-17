using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelCivilianHatchbackValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Civilian Hatchback")]
        public static void Run()
        {
            var root=new GameObject("Temporary civilian validation");
            var random=UnityEngine.Random.state;
            var tuning=UnityEngine.Object.Instantiate(VoxelObstacleCarTuning.Load());
            try
            {
                tuning.semiTrailerSpawnChance=0;
                tuning.paintColours=new[]{Color.magenta};
                var player=root.AddComponent<VoxelCarController>();player.enabled=false;
                var go=new GameObject("Traffic test");go.transform.SetParent(root.transform);
                var car=go.AddComponent<VoxelObstacleCar>();car.enabled=false;
                car.Configure(player,tuning,true,null,70,3,12);
                Check(car.TravelsWithPlayer && car.TravelSpeed==12 && car.TrackDistance==70,"Traffic configuration changed");
                var paint=go.GetComponentInChildren<VoxelTrafficPaint>();
                Check(paint!=null,"Authored model was not spawned");
                var renderers=go.GetComponentsInChildren<MeshRenderer>();
                Check(renderers.Length>400 && renderers.Length<1000,"Unexpected piece count "+renderers.Length);
                Check(go.GetComponentsInChildren<Collider>().Length==0,"Per-piece colliders present");
                int painted=0,unpainted=0;
                foreach(var r in renderers)
                {
                    var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);
                    if(r.sharedMaterial==paint.bodyMaterial) {Check(block.GetColor("_BaseColor")==Color.magenta,"Body colour not applied");painted++;}
                    else {Check(block.isEmpty,"Trim or lights were recoloured");unpainted++;}
                }
                Check(painted>200 && unpainted>100,"Missing paint/trim separation");
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var wheels=(Transform[])typeof(VoxelObstacleCar).GetField("modelWheels",flags).GetValue(car);
                Check(wheels.Length==4,"Nested wheels not registered");
                // Exercise actual damage selection with debris disabled for this edit-mode check.
                typeof(VoxelObstacleCar).GetField("<EnemyTuning>k__BackingField",flags).SetValue(car,null);
                var damage=typeof(VoxelObstacleCar).GetMethod("ApplyVoxelDamage",flags);
                var style=Enum.ToObject(damage.GetParameters()[3].ParameterType,0);
                int removed=(int)damage.Invoke(car,new object[]{new Vector3(0,.8f,3),Vector3.back,10,style});
                Check(removed==10 && go.GetComponentsInChildren<MeshRenderer>().Length==renderers.Length-10,"Nested body damage failed");
                damage.Invoke(car,new object[]{new Vector3(0,.8f,3),Vector3.back,10,style});
                Check(go.GetComponentsInChildren<MeshRenderer>().Length==renderers.Length-20,"Inactive damage pieces counted again");
                Debug.Log("PASS civilian prefab: "+renderers.Length+" pieces, four wheel roots, existing traffic setup, paint-only tinting, nested damage and no per-piece colliders.");
            }
            finally {UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(tuning);UnityEngine.Random.state=random;}
        }
        private static void Check(bool value,string message) {if(!value) throw new Exception(message);}
    }
}
