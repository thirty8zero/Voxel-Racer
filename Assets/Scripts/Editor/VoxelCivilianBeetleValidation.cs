using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace VoxelRacer.Editor
{
    public static class VoxelCivilianBeetleValidation
    {
        private static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
        [MenuItem("Tools/Voxel Racer/Validate Civilian Beetle")]
        public static void Run()
        {
            var state=UnityEngine.Random.state;
            var root=new GameObject("Temporary Beetle validation");
            var traffic=Object.Instantiate(VoxelObstacleCarTuning.Load());
            try
            {
                var beetle=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>(VoxelCivilianBeetleBuilder.TuningPath);
                var hatch=AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>("Assets/Resources/EnemyVehicles/TrafficCarTuning.asset");
                Check(beetle!=null && beetle.vehicleHealth==hatch.vehicleHealth && beetle.voxelHealth==hatch.voxelHealth,"Civilian durability mismatch");
                var shell=Object.Instantiate(beetle.modelPrefab,root.transform);
                ValidateShell(shell);
                Object.DestroyImmediate(shell);
                foreach(var guid in AssetDatabase.FindAssets("t:VoxelTrackDefinition").Concat(AssetDatabase.FindAssets("t:VoxelObstacleCarTuning")).Distinct())
                    foreach(var t in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<VoxelObstacleCarTuning>())
                        Check(t.civilianVehiclePool!=null&&t.civilianVehiclePool.Length==3&&t.civilianVehiclePool.Contains(beetle)&&t.civilianVehiclePool.Distinct().Count()==3,"Pool registration missing: "+t.name);
                var counts=new int[3];UnityEngine.Random.InitState(1501);
                for(int i=0;i<9000;i++)counts[Array.IndexOf(traffic.civilianVehiclePool,traffic.ChooseCivilianVehicle(out _))]++;
                Check(counts.All(n=>n>2750&&n<3250),"Equal selection distribution failed");
                traffic.civilianVehiclePool=new[]{beetle};traffic.paintColours=new[]{Color.magenta};
                var player=root.AddComponent<VoxelCarController>();player.enabled=false;
                int count=0;
                foreach(bool same in new[]{true,false})
                {
                    var go=new GameObject("Beetle test");go.transform.SetParent(root.transform);
                    var car=go.AddComponent<VoxelObstacleCar>();car.enabled=false;car.Configure(player,traffic,same,null,70,3,12);
                    Check(!car.IsEnemyTraffic&&car.EnemyTuning==beetle&&car.TravelsWithPlayer==same&&car.TravelSpeed==12,"Normal civilian setup failed");
                    Check(car.TrafficHalfLength==beetle.collisionHalfLength,"Collision size not used");
                    var renderers=go.GetComponentsInChildren<MeshRenderer>();count=renderers.Length;
                    Check(count>300&&count<800,"Voxel count outside budget: "+count);
                    Check(go.GetComponentsInChildren<Collider>().Length==0,"Per-voxel colliders found");
                    var marker=go.GetComponentInChildren<VoxelTrafficPaint>();
                    foreach(var r in renderers)
                    {
                        var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);
                        if(r.sharedMaterial==marker.bodyMaterial)Check(block.GetColor("_BaseColor")==Color.magenta,"Paint tint missing");
                        else Check(block.isEmpty,"Trim tinted");
                    }
                    const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var wheels=(Transform[])typeof(VoxelObstacleCar).GetField("modelWheels",flags).GetValue(car);
                    Check(wheels.Length==4,"Wheel animation roots missing");
                    typeof(VoxelObstacleCar).GetField("<EnemyTuning>k__BackingField",flags).SetValue(car,null);
                    var method=typeof(VoxelObstacleCar).GetMethod("ApplyVoxelDamage",flags);
                    var style=Enum.ToObject(method.GetParameters()[3].ParameterType,0);
                    int removed=(int)method.Invoke(car,new object[]{go.transform.position+Vector3.forward*3,Vector3.back,10,style});
                    Check(removed==10&&go.GetComponentsInChildren<MeshRenderer>().Length==count-10,"Destructible shell failed");
                }
                string report="PASS: "+count+" pieces; closed sides/front/rear/windows/roof; hollow interior; same/oncoming civilian setup; four wheels; paint-only tint; collision size; voxel damage; all track pools; 9000 equal-choice samples: "+string.Join(", ",counts);
                Directory.CreateDirectory("Temp/Beetle");File.WriteAllText("Temp/Beetle/Validation.txt",report);Debug.Log(report);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(traffic);UnityEngine.Random.state=state;}
        }
        private static void ValidateShell(GameObject model)
        {
            var pieces=model.GetComponentsInChildren<MeshRenderer>();
            Check(pieces.Length<=646,"Shell repair increased the original voxel count");
            bool Covered(string name,Vector3 origin,Vector3 direction,float limit) => pieces.Where(r=>r.name==name)
                .Any(r=>r.bounds.IntersectRay(new Ray(origin,direction),out float d)&&d<=limit);
            foreach(int side in new[]{-1,1})
                for(float z=-.58f;z<=.58f;z+=.04f)
                    for(float y=.42f;y<=.90f;y+=.04f)
                        Check(Covered("Body voxel",new Vector3(side*2,y,z),Vector3.left*side,1.42f),"Door shell gap: "+new Vector3(side,y,z));
            foreach(int end in new[]{-1,1})
                for(float x=-.50f;x<=.50f;x+=.04f)
                    for(float y=.35f;y<=.69f;y+=.04f)
                        Check(Covered("Body voxel",new Vector3(x,y,end*3),Vector3.back*end,1.1f),"Front/rear body gap");
            // Rays leave the cabin through every screen and through the roof. A hole
            // cannot be masked by the far wall, wheels or unrelated trim.
            for(float y=1.04f;y<=1.57f;y+=.025f)
            {
                Check(Covered("Front screen voxel",new Vector3(0,y,-.35f),Vector3.forward,2),"Windscreen row gap at "+y);
                Check(Covered("Rear screen voxel",new Vector3(0,y,-.35f),Vector3.back,2),"Rear screen row gap at "+y);
                foreach(int side in new[]{-1,1})
                    for(float z=-.60f;z<=-.10f;z+=.04f)
                        Check(pieces.Where(r=>r.name=="Side window voxel"||r.name=="Cabin side voxel").Any(r=>
                            r.bounds.IntersectRay(new Ray(new Vector3(0,y,z),Vector3.right*side),out float d)&&d<.81f),"Side window gap");
                for(int angle=0;angle<360;angle+=5)
                {
                    Vector3 direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
                    Check(pieces.Where(r=>r.name=="Front screen voxel"||r.name=="Rear screen voxel"||r.name=="Side window voxel"||r.name=="Cabin side voxel")
                        .Any(r=>r.bounds.IntersectRay(new Ray(new Vector3(0,y,-.35f),direction),out float d)&&d<2),"Cabin corner gap at height/angle "+y+"/"+angle);
                }
            }
            for(float x=-.44f;x<=.44f;x+=.04f)
                for(float z=-.66f;z<=-.04f;z+=.04f)
                    Check(Covered("Roof voxel",new Vector3(x,1.4f,z),Vector3.up,.20f),"Roof cap gap");
            Check(!pieces.Any(r=>r.bounds.Contains(new Vector3(0,.65f,0))||r.bounds.Contains(new Vector3(0,1.25f,-.35f))),"Interior contains solid voxel filling");
            Check(pieces.Max(r=>r.bounds.max.y)<=1.60f,"Raised roof block remains");
        }
    }
}
