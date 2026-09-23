using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace VoxelRacer.Editor
{
    public static class VoxelBoostDrivingValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Boost Driving")]
        public static void Run()
        {
            var root=new GameObject("Temporary boost driving validation");
            try
            {
                var car=root.AddComponent<VoxelCarController>();car.enabled=false;
                car.acceleration=10;car.topSpeed=30;car.brakingForce=20;
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var method=typeof(VoxelCarController).GetMethod("CalculateNextDriveSpeed",flags);
                float Step(float speed,bool brake=false)=>(float)method.Invoke(car,new object[]{speed,brake,.1f});
                Equal(Step(5),6,"Normal acceleration");
                car.SetBoostSpeedBonus(20,3);
                Equal(Step(5),8,"Boost below normal max speed");Equal(Step(30),33,"Boost above normal max speed");
                Equal(Step(49),50,"Boost speed capped");Equal(Step(10,true),8,"Braking unaffected");
                car.SetEnginePerformance(15,25);car.SetWheelPerformance(20,15,20);
                Equal(Step(5),9.5f,"Engine/tyre acceleration combines with boost");
                car.SetBoostSpeedBonus(0);Equal(Step(5),6.5f,"Boost end clears acceleration bonus");
                car.SetDrivingEnabled(false);Equal(Step(0),0,"Disabled driving cannot accelerate");
                typeof(VoxelCarController).GetField("boostForwardOffset",flags).SetValue(car,4f);
                typeof(VoxelCarController).GetField("ramForwardOffset",flags).SetValue(car,-1f);
                Equal(car.CollisionTrackPosition.y,car.TrackDistance+3,"Collision includes lunge and recoil");
                var bounds=new Vector2(1.35f,3.8f);
                Check(VoxelVehicleCollision.Sweep(new Vector2(0,-10),new Vector2(0,10),bounds,out var hit),"Fast rear crossing missed");
                Check(Vector3.Dot(VoxelVehicleCollision.ImpactDirection(hit,root.transform),Vector3.forward)>.99f,"Rear hit direction reversed after crossing");
                Check(VoxelVehicleCollision.Sweep(new Vector2(0,10),new Vector2(0,-10),bounds,out hit),"Oncoming crossing missed");
                Check(Vector3.Dot(VoxelVehicleCollision.ImpactDirection(hit,root.transform),Vector3.back)>.99f,"Oncoming hit direction incorrect");
                Check(!VoxelVehicleCollision.Sweep(new Vector2(3,-10),new Vector2(3,10),bounds,out hit),"Adjacent lane false collision");
                Check(VoxelVehicleCollision.Sweep(new Vector2(0,-6),new Vector2(0,-2),bounds,out hit),"Boost lunge missed");
                Check(!VoxelVehicleCollision.Sweep(new Vector2(0,-10),new Vector2(0,-8),bounds,out hit),"Distant false collision");
                Check(VoxelVehicleCollision.Sweep(new Vector2(3,0),Vector2.zero,bounds,out hit),"Lane change collision missed");
                Debug.Log("PASS boost driving: low/high speed acceleration, cap, braking, upgrades, reset, disabled driving, lunge/recoil collision position, rear/oncoming swept impacts, adjacent lanes and lane changes.");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private static void Equal(float a,float b,string message)=>Check(Mathf.Abs(a-b)<.001f,message);
        private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    }
}
