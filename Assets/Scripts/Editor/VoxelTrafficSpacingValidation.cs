using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace VoxelRacer.Editor
{
    public static class VoxelTrafficSpacingValidation
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Set(object o,string field,object value)=>o.GetType().GetField(field,Flags).SetValue(o,value);
        private static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
        [MenuItem("Tools/Voxel Racer/Validate Civilian Traffic Spacing")]
        public static void Run()
        {
            var root=new GameObject("Temporary traffic spacing validation");
            var tuning=Object.Instantiate(VoxelObstacleCarTuning.Load());
            try
            {
                var player=root.AddComponent<VoxelCarController>();player.enabled=false;
                var spawner=root.AddComponent<VoxelObstacleSpawner>();spawner.enabled=false;spawner.laneCount=1;spawner.obstacleCarTuning=tuning;
                var a=new GameObject("Follower");a.transform.SetParent(root.transform);
                var b=new GameObject("Leader van");b.transform.SetParent(root.transform);
                var follower=a.AddComponent<VoxelObstacleCar>();var leader=b.AddComponent<VoxelObstacleCar>();
                follower.enabled=false;leader.enabled=false;
                foreach(var car in new[]{follower,leader}) {Set(car,"target",player);Set(car,"collisionHalfLength",car==leader?2.65f:2.3f);car.enabled=true;}
                foreach(var car in new[]{follower,leader}) typeof(VoxelObstacleCar).GetMethod("OnEnable",Flags).Invoke(car,null);
                var speedMethod=typeof(VoxelObstacleCar).GetMethod("GetSafeTrafficSpeed",Flags);
                foreach(bool same in new[]{true,false})
                {
                    float direction=same?1:-1;float position=0;
                    Set(follower,"travelsWithPlayer",same);Set(leader,"travelsWithPlayer",same);
                    Set(leader,"trackDistance",direction*10);
                    // A very fast follower must stop short of a stationary leader, even on a long frame.
                    Set(follower,"trackDistance",position);
                    float speed=(float)speedMethod.Invoke(follower,new object[]{100f,.5f});
                    position+=direction*speed*.5f;Set(follower,"trackDistance",position);
                    Check(Mathf.Abs(leader.TrackDistance-position)>=6.45f-.001f,"Follower penetrates van");
                    Check((float)speedMethod.Invoke(follower,new object[]{100f,.1f})<.001f,"Follower does not stop at safe gap");
                    Set(leader,"trackDistance",direction*12);
                    Check((float)speedMethod.Invoke(follower,new object[]{20f,.1f})>0,"Follower does not resume after leader moves");
                    Set(leader,"laneOffset",3f);
                    Check(Mathf.Approximately((float)speedMethod.Invoke(follower,new object[]{20f,.1f}),20f),"Adjacent lane unnecessarily blocks traffic");
                    Set(leader,"laneOffset",0f);
                }
                Set(follower,"travelsWithPlayer",true);Set(leader,"travelsWithPlayer",true);
                Set(follower,"trackDistance",100f);Set(leader,"trackDistance",130f);
                var choose=typeof(VoxelObstacleSpawner).GetMethod("TryFindCivilianLane",Flags);
                object[] blocked={true,101f,0f,0f};
                Check(!(bool)choose.Invoke(spawner,blocked),"Spawn allowed inside existing civilian");
                object[] clear={true,115f,0f,0f};
                Check((bool)choose.Invoke(spawner,clear),"Safely spaced spawn rejected");
                object[] opposing={false,115f,0f,0f};
                Check(!(bool)choose.Invoke(spawner,opposing),"Opposing traffic allowed into occupied lane");
                Debug.Log("PASS civilian spacing: fast followers, stationary/moving leaders, vans, both directions, adjacent lanes and spawn clearance.");
            }
            finally
            {
                foreach(var car in root.GetComponentsInChildren<VoxelObstacleCar>()) typeof(VoxelObstacleCar).GetMethod("OnDisable",Flags).Invoke(car,null);
                Object.DestroyImmediate(root);Object.DestroyImmediate(tuning);
            }
        }
    }
}
