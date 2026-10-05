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
    public static class VoxelCivilianJettaValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
        [MenuItem("Tools/Voxel Racer/Validate Civilian Jetta")]
        public static void Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Run outside Play Mode.");
            var random=UnityEngine.Random.state;
            var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Temporary Jetta validation");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var traffic=Object.Instantiate(VoxelObstacleCarTuning.Load());
            try
            {
                var jetta=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(VoxelCivilianJettaBuilder.TuningPath);
                var hatch=Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning");
                Check(jetta!=null&&jetta.modelPrefab!=null,"Jetta assets missing");
                Check(jetta.vehicleHealth==hatch.vehicleHealth&&jetta.voxelHealth==hatch.voxelHealth&&jetta.destroyedLifetime==hatch.destroyedLifetime&&jetta.explosionVoxelCount==hatch.explosionVoxelCount,"Standard civilian durability/effects not inherited");
                var model=Object.Instantiate(jetta.modelPrefab,root.transform);ValidateShell(model,jetta);
                int pieceCount=model.GetComponentsInChildren<MeshRenderer>().Length;Object.DestroyImmediate(model);
                int pools=0;
                foreach(string guid in AssetDatabase.FindAssets("t:VoxelTrackDefinition").Concat(AssetDatabase.FindAssets("t:VoxelObstacleCarTuning")).Distinct())
                    foreach(var pool in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<VoxelObstacleCarTuning>())
                    {Check(pool.civilianVehiclePool!=null&&pool.civilianVehiclePool.Count(v=>v==jetta)==1,"Jetta missing/duplicated in "+pool.name);pools++;}
                Check(traffic.civilianVehiclePool.All(v=>v!=null&&v.modelPrefab!=null)&&traffic.civilianVehiclePool.Distinct().Count()==traffic.civilianVehiclePool.Length,"Fallback must contain valid unique civilian models");
                int[] counts=new int[traffic.civilianVehiclePool.Length];UnityEngine.Random.InitState(1812);
                for(int i=0;i<counts.Length*3000;i++)counts[Array.IndexOf(traffic.civilianVehiclePool,traffic.ChooseCivilianVehicle(out _))]++;
                Check(counts.All(n=>n>2700&&n<3300),"Civilian pool does not select all models equally");
                traffic.civilianVehiclePool=new[]{jetta};traffic.paintColours=new[]{Color.magenta};
                var player=root.AddComponent<VoxelCarController>();player.enabled=false;
                foreach(bool same in new[]{true,false})
                {
                    var go=new GameObject("Jetta traffic test");go.transform.SetParent(root.transform,false);
                    try
                    {
                        var car=go.AddComponent<VoxelObstacleCar>();car.enabled=false;car.Configure(player,traffic,same,null,70,3,12);
                        Check(!car.IsEnemyTraffic&&car.TravelsWithPlayer==same&&car.EnemyTuning==jetta&&car.TravelSpeed==12,"Jetta is not configured as standard civilian traffic");
                        Check(car.TrafficHalfLength==jetta.collisionHalfLength&&(float)typeof(VoxelObstacleCar).GetField("collisionHalfWidth",Private).GetValue(car)==jetta.collisionHalfWidth,"Collision envelope not used");
                        var marker=go.GetComponentInChildren<VoxelTrafficPaint>();var pieces=go.GetComponentsInChildren<MeshRenderer>();
                        foreach(var r in pieces)
                        {
                            var properties=new MaterialPropertyBlock();r.GetPropertyBlock(properties);
                            if(r.sharedMaterial==marker.bodyMaterial)Check(properties.GetColor("_BaseColor")==Color.magenta,"Body paint was not varied");
                            else Check(properties.isEmpty,"Fixed grille accent, wheels, glass or trim was tinted");
                        }
                        var wheels=(Transform[])typeof(VoxelObstacleCar).GetField("modelWheels",Private).GetValue(car);
                        Check(wheels.Length==4&&wheels.All(w=>w.GetComponentInParent<VoxelIndestructiblePart>()==null),"Four destructible rotating wheels missing");
                        var effects=go.GetComponent<VoxelVehicleDamageEffects>();Check(effects!=null&&go.transform.Find("Damage Smoke")!=null&&go.transform.Find("Damage Fire")!=null,"Smoke/fire components missing");
                        var body=pieces.First(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null);
                        car.TakeHostileProjectileHit(body.transform,.25f,body.bounds.center,Vector3.forward);
                        Check(Mathf.Approximately(car.CurrentHealth,jetta.vehicleHealth-.25f),"Real civilian bullet health damage failed");
                        // Check health-driven thresholds without producing editor debris.
                        typeof(VoxelObstacleCar).GetField("<CurrentHealth>k__BackingField",Private).SetValue(car,jetta.vehicleHealth*.7f);
                        typeof(VoxelVehicleDamageEffects).GetMethod("LateUpdate",Private).Invoke(effects,null);
                        Check(effects.SmokeActive&&!effects.FireActive,"Civilian smoke threshold changed");
                        typeof(VoxelObstacleCar).GetField("<CurrentHealth>k__BackingField",Private).SetValue(car,jetta.vehicleHealth*.4f);
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
                string report="PASS: "+pieceCount+" total pieces within 650 budget; hollow closed body/cabin/roof, four destructible wheel roots, three protected axle/driveline pieces, zero colliders, paint-only tint, collision envelope, same/oncoming civilians, real bullet health damage, smoke/fire thresholds, voxel destruction; "+pools+" traffic pools contain Jetta once; "+counts.Length*3000+" equal-choice samples: "+string.Join(", ",counts)+". Edit Mode; preview scene/random state restored.";
                Directory.CreateDirectory("Temp/Jetta");File.WriteAllText("Temp/Jetta/Validation.txt",report);Debug.Log(report);
            }
            finally{Object.DestroyImmediate(traffic);EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Random.state=random;}
        }
        private static void ValidateShell(GameObject model,VoxelEnemyVehicleTuning tuning)
        {
            var pieces=model.GetComponentsInChildren<MeshRenderer>();
            Check(pieces.Length>400&&pieces.Length<=VoxelCivilianJettaBuilder.PieceBudget,"Voxel count outside budget: "+pieces.Length);
            Check(model.GetComponentsInChildren<Collider>().Length==0,"Per-voxel colliders found");
            Check(pieces.All(r=>r.sharedMaterial!=null),"Missing material");
            Check(pieces.Count(r=>r.GetComponentInParent<VoxelIndestructiblePart>()!=null)==3,"Protected driveline must contain three pieces");
            Check(pieces.All(r=>r.bounds.min.x>=-tuning.collisionHalfWidth&&r.bounds.max.x<=tuning.collisionHalfWidth&&r.bounds.min.z>=-tuning.collisionHalfLength&&r.bounds.max.z<=tuning.collisionHalfLength),"Model exceeds collision envelope");
            Check(!pieces.Any(r=>r.bounds.Contains(new Vector3(0,.75f,0))||r.bounds.Contains(new Vector3(0,1.35f,-.35f))),"Interior filled with hidden voxels");
            var cabin=pieces.Where(r=>r.name=="Cabin pillar voxel"||r.name=="Side window voxel"||r.name=="Windscreen voxel"||r.name=="Rear screen voxel").ToArray();
            for(float y=1.06f;y<=1.54f;y+=.025f)for(int angle=0;angle<360;angle+=5)
            {
                Vector3 direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                Check(cabin.Any(r=>r.bounds.IntersectRay(new Ray(new Vector3(0,y,-.35f),direction),out float d)&&d<2),"Cabin corner gap at "+y+" / "+angle);
            }
            for(float x=-.85f;x<=.85f;x+=.10f)for(float z=-1.07f;z<=.30f;z+=.10f)
                Check(pieces.Where(r=>r.name=="Roof voxel").Any(r=>r.bounds.IntersectRay(new Ray(new Vector3(x,1.35f,z),Vector3.up),out float d)&&d<.3f),"Roof gap");
            foreach(int side in new[]{-1,1})for(float z=-.65f;z<=.60f;z+=.08f)for(float y=.43f;y<=1.12f;y+=.04f)
                Check(pieces.Where(r=>r.name=="Side body voxel").Any(r=>r.bounds.IntersectRay(new Ray(new Vector3(0,y,z),Vector3.right*side),out float d)&&d<1.05f),"Door shell gap");
            foreach(int end in new[]{-1,1})for(float x=-.85f;x<=.85f;x+=.1f)for(float y=.43f;y<=1.12f;y+=.04f)
                Check(pieces.Where(r=>r.name=="End body voxel").Any(r=>r.bounds.IntersectRay(new Ray(new Vector3(x,y,0),Vector3.forward*end),out float d)&&d<2.2f),"End shell gap");
            foreach(var name in new[]{"Bonnet voxel","Boot lid voxel"})
            {
                float from=name=="Bonnet voxel"?.75f:-2.0f,to=name=="Bonnet voxel"?2.0f:-1.4f;
                for(float x=-.85f;x<=.85f;x+=.1f)for(float z=from;z<=to;z+=.1f)
                    Check(pieces.Where(r=>r.name==name).Any(r=>r.bounds.IntersectRay(new Ray(new Vector3(x,.8f,z),Vector3.up),out float d)&&d<.35f),"Bonnet/boot gap");
            }
        }
    }
}
