using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelCivilianKombiValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
        [MenuItem("Tools/Voxel Racer/Validate Civilian Kombi")]
        public static void Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Run outside Play Mode.");
            var random=UnityEngine.Random.state;
            var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Temporary Kombi validation");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var traffic=Object.Instantiate(VoxelObstacleCarTuning.Load());
            try
            {
                var kombi=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(VoxelCivilianKombiBuilder.TuningPath);
                var hatch=Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning");
                Check(kombi!=null&&kombi.modelPrefab!=null,"Kombi assets missing");
                Check(kombi.vehicleHealth==hatch.vehicleHealth&&kombi.voxelHealth==hatch.voxelHealth&&kombi.destroyedLifetime==hatch.destroyedLifetime&&kombi.explosionVoxelCount==hatch.explosionVoxelCount,"Standard civilian durability/effects not inherited");
                var model=Object.Instantiate(kombi.modelPrefab,root.transform);ValidateShell(model,kombi);
                int pieceCount=model.GetComponentsInChildren<MeshRenderer>().Length;Object.DestroyImmediate(model);
                int pools=0;
                foreach(string guid in AssetDatabase.FindAssets("t:VoxelTrackDefinition").Concat(AssetDatabase.FindAssets("t:VoxelObstacleCarTuning")).Distinct())
                    foreach(var pool in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<VoxelObstacleCarTuning>())
                    {Check(pool.civilianVehiclePool!=null&&pool.civilianVehiclePool.Count(v=>v==kombi)==1,"Kombi missing/duplicated in "+pool.name);pools++;}
                Check(traffic.civilianVehiclePool.All(v=>v!=null&&v.modelPrefab!=null)&&traffic.civilianVehiclePool.Distinct().Count()==traffic.civilianVehiclePool.Length,"Fallback must contain valid unique civilian models");
                int[] counts=new int[traffic.civilianVehiclePool.Length];UnityEngine.Random.InitState(1812);
                for(int i=0;i<counts.Length*3000;i++)counts[Array.IndexOf(traffic.civilianVehiclePool,traffic.ChooseCivilianVehicle(out _))]++;
                Check(counts.All(n=>n>2700&&n<3300),"Civilian pool does not select all models equally");
                traffic.civilianVehiclePool=new[]{kombi};traffic.paintColours=new[]{Color.magenta};
                var player=root.AddComponent<VoxelCarController>();player.enabled=false;
                foreach(bool same in new[]{true,false})
                {
                    var go=new GameObject("Kombi traffic test");go.transform.SetParent(root.transform,false);
                    try
                    {
                        var car=go.AddComponent<VoxelObstacleCar>();car.enabled=false;car.Configure(player,traffic,same,null,70,3,12);
                        Check(!car.IsEnemyTraffic&&car.TravelsWithPlayer==same&&car.EnemyTuning==kombi&&car.TravelSpeed==12,"Kombi is not configured as standard civilian traffic");
                        Check(car.TrafficHalfLength==kombi.collisionHalfLength&&(float)typeof(VoxelObstacleCar).GetField("collisionHalfWidth",Private).GetValue(car)==kombi.collisionHalfWidth,"Collision envelope not used");
                        var marker=go.GetComponentInChildren<VoxelTrafficPaint>();var pieces=go.GetComponentsInChildren<MeshRenderer>();
                        foreach(var r in pieces)
                        {
                            var properties=new MaterialPropertyBlock();r.GetPropertyBlock(properties);
                            if(r.sharedMaterial==marker.bodyMaterial)Check(properties.GetColor("_BaseColor")==Color.magenta,"Lower body paint was not varied");
                            else Check(properties.isEmpty,"Cream upper body, wheels, glass or trim was tinted");
                        }
                        var wheels=(Transform[])typeof(VoxelObstacleCar).GetField("modelWheels",Private).GetValue(car);
                        Check(wheels.Length==4&&wheels.All(w=>w.GetComponentInParent<VoxelIndestructiblePart>()==null),"Four destructible rotating wheels missing");
                        var effects=go.GetComponent<VoxelVehicleDamageEffects>();Check(effects!=null&&go.transform.Find("Damage Smoke")!=null&&go.transform.Find("Damage Fire")!=null,"Smoke/fire components missing");
                        var body=pieces.First(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null);
                        car.TakeHostileProjectileHit(body.transform,.25f,body.bounds.center,Vector3.forward);
                        Check(Mathf.Approximately(car.CurrentHealth,kombi.vehicleHealth-.25f),"Real civilian bullet health damage failed");
                        // Check health-driven thresholds without producing editor debris.
                        typeof(VoxelObstacleCar).GetField("<CurrentHealth>k__BackingField",Private).SetValue(car,kombi.vehicleHealth*.7f);
                        typeof(VoxelVehicleDamageEffects).GetMethod("LateUpdate",Private).Invoke(effects,null);
                        Check(effects.SmokeActive&&!effects.FireActive,"Civilian smoke threshold changed");
                        typeof(VoxelObstacleCar).GetField("<CurrentHealth>k__BackingField",Private).SetValue(car,kombi.vehicleHealth*.4f);
                        typeof(VoxelVehicleDamageEffects).GetMethod("LateUpdate",Private).Invoke(effects,null);
                        Check(effects.SmokeActive&&effects.FireActive,"Civilian fire threshold changed");
                        typeof(VoxelObstacleCar).GetField("<EnemyTuning>k__BackingField",Private).SetValue(car,null);
                        var damage=typeof(VoxelObstacleCar).GetMethod("ApplyVoxelDamage",Private);var style=Enum.ToObject(damage.GetParameters()[3].ParameterType,0);
                        int removed=(int)damage.Invoke(car,new object[]{new Vector3(0,1,3),Vector3.back,10,style});
                        Check(removed==10&&go.GetComponentsInChildren<MeshRenderer>().Length==pieceCount-10,"Detachable civilian voxel damage failed");
                        Check((int)damage.Invoke(car,new object[]{new Vector3(0,1,3),Vector3.back,10,style})==10&&go.GetComponentsInChildren<MeshRenderer>().Length==pieceCount-20,"Removed voxels were selected twice");
                    }
                    finally{Object.DestroyImmediate(go);}
                }
                string report="PASS: "+pieceCount+" total pieces within 650 budget; hollow closed body/cabin/roof, four destructible wheel roots, three protected axle/driveline pieces, zero colliders, two-tone paint-only tint, collision envelope, same/oncoming civilians, real bullet health damage, smoke/fire thresholds, voxel destruction; "+pools+" traffic pools contain Kombi once; "+counts.Length*3000+" equal-choice samples: "+string.Join(", ",counts)+". Edit Mode; preview scene/random state restored.";
                Directory.CreateDirectory("Temp/Kombi");File.WriteAllText("Temp/Kombi/Validation.txt",report);Debug.Log(report);
            }
            finally{Object.DestroyImmediate(traffic);EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Random.state=random;}
        }
        private static void ValidateShell(GameObject model,VoxelEnemyVehicleTuning tuning)
        {
            var pieces=model.GetComponentsInChildren<MeshRenderer>();
            Check(pieces.Length>400&&pieces.Length<=VoxelCivilianKombiBuilder.PieceBudget,"Voxel count outside budget: "+pieces.Length);
            Check(model.GetComponentsInChildren<Collider>().Length==0,"Per-voxel colliders found");
            Check(pieces.All(r=>r.sharedMaterial!=null),"Missing material");
            Check(pieces.Count(r=>r.GetComponentInParent<VoxelIndestructiblePart>()!=null)==3,"Protected driveline must contain three pieces");
            Check(pieces.All(r=>r.bounds.min.x>=-tuning.collisionHalfWidth&&r.bounds.max.x<=tuning.collisionHalfWidth&&r.bounds.min.z>=-tuning.collisionHalfLength&&r.bounds.max.z<=tuning.collisionHalfLength),"Model exceeds collision envelope");
            Check(!pieces.Any(r=>r.bounds.Contains(new Vector3(0,.75f,0))||r.bounds.Contains(new Vector3(0,1.60f,0))),"Interior filled with hidden voxels");
            var shell=pieces.Where(r=>r.name.EndsWith("body voxel")||r.name.EndsWith("window voxel")||r.name=="Split windscreen voxel").ToArray();
            for(float y=.96f;y<=1.88f;y+=.04f)for(int angle=0;angle<360;angle+=5)
            {
                Vector3 direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                Check(shell.Any(r=>r.bounds.IntersectRay(new Ray(new Vector3(0,y,0),direction),out float d)&&d<2.8f),"Cabin/body corner gap at "+y+" / "+angle);
            }
            for(float x=-.8f;x<=.8f;x+=.1f)for(float z=-1.90f;z<=1.75f;z+=.1f)
                Check(pieces.Where(r=>r.name=="Cream roof voxel").Any(r=>r.bounds.IntersectRay(new Ray(new Vector3(x,1.60f,z),Vector3.up),out float d)&&d<.5f),"Roof gap");
        }
    }
}
