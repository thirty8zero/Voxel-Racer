using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelDistantSceneryValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Distant Scenery")]
        public static void Run()
        {
            var root=new GameObject("Temporary Distant Scenery Test"); root.SetActive(false);
            var cameraRoot=new GameObject("Temporary Scenery Camera");
            var track=ScriptableObject.CreateInstance<VoxelTrackDefinition>();
            var source=AssetDatabase.LoadAssetAtPath<VoxelScenerySet>(VoxelDesertSceneryBuilder.SetPath);
            var set=UnityEngine.Object.Instantiate(source);
            var oldRandom=UnityEngine.Random.state;
            try
            {
                track.scenerySet=set; set.distantSceneryEnabled=true; set.distantSceneryDistance=200; set.distantPropsPerTile=7;
                var road=root.AddComponent<EndlessVoxelRoad>(); road.enabled=false; road.trackDefinition=track; road.groundWidth=700; road.turnChancePerSegment=0;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(EndlessVoxelRoad).GetField("turnRandom",flags).SetValue(road,new System.Random(17));
                // Path-only setup: no scene geometry, gameplay objects or active mission are changed.
                typeof(EndlessVoxelRoad).GetMethod("AppendSegment",flags).Invoke(road,new object[]{false});
                var scenery=root.AddComponent<VoxelDistantScenery>(); scenery.enabled=false;
                var camera=cameraRoot.AddComponent<Camera>(); camera.enabled=false;
                camera.farClipPlane=250; camera.fieldOfView=60; camera.aspect=20f/9;
                camera.transform.position=new Vector3(0,8,-12); camera.transform.rotation=Quaternion.Euler(18,0,0);
                scenery.Prepare(camera);
                int cached=scenery.CachedPropCount, forward=scenery.VisiblePropCount;
                Check(cached>300 && forward>0 && forward<cached,"Distant scenery/frustum population failed");
                camera.transform.rotation=Quaternion.Euler(18,180,0);
                scenery.Prepare(camera);
                int rear=scenery.VisiblePropCount;
                Check(scenery.CachedPropCount==cached && rear>0,"Turning around must reveal already cached scenery");
                camera.transform.position+=Vector3.forward*512;
                scenery.Prepare(camera);
                Check(scenery.CachedPropCount>0 && scenery.CachedPropCount<cached*2,"World tiles did not recycle within bounded coverage");
                set.distantSceneryEnabled=false; scenery.Prepare(camera);
                Check(scenery.CachedPropCount==0 && scenery.VisiblePropCount==0,"Disabling scenery should clear draw data");
                Debug.Log("PASS: "+cached+" cached props around camera, "+forward+" visible forward and "+rear+" after 180-degree turn; cache retained during orbit, bounded streaming and disable controls verified.");
            }
            finally
            {
                UnityEngine.Random.state=oldRandom;
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraRoot);
                UnityEngine.Object.DestroyImmediate(track); UnityEngine.Object.DestroyImmediate(set);
            }
        }
        private static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
    }
}
