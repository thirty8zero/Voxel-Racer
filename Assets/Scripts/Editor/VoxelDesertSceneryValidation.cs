using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelDesertSceneryValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Desert Scenery")]
        public static void Run()
        {
            var set=AssetDatabase.LoadAssetAtPath<VoxelScenerySet>(VoxelDesertSceneryBuilder.SetPath);
            Check(set!=null && set.entries.Length==14,"Missing scenery entries");
            int maxTriangles=0;
            foreach(var entry in set.entries)
            {
                var renderers=entry.prefab.GetComponentsInChildren<MeshRenderer>();
                Check(renderers.Length==1 && renderers[0].sharedMaterials.Length==1,"Scenery should use one renderer/material");
                Check(entry.prefab.GetComponentsInChildren<Collider>().Length==0,"Decorative prop contains a collider");
                var mesh=entry.prefab.GetComponent<MeshFilter>().sharedMesh;
                Check(mesh!=null && mesh.vertexCount>0,"Missing scenery mesh");
                Check(mesh.bounds.min.y>=-.001f,"Model origin should rest on ground");
                maxTriangles=Mathf.Max(maxTriangles,mesh.triangles.Length/3);
            }
            var root=new GameObject("Temporary Scenery Validation"); root.SetActive(false);
            var track=ScriptableObject.CreateInstance<VoxelTrackDefinition>();
            var random=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(917);
                track.scenerySet=set;
                var road=root.AddComponent<EndlessVoxelRoad>(); road.enabled=false; road.trackDefinition=track;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                Type dataType=typeof(EndlessVoxelRoad).GetNestedType("RoadSegment",BindingFlags.NonPublic);
                var create=typeof(EndlessVoxelRoad).GetMethod("CreateScenerySet",flags);
                for(int n=0;n<4;n++)
                {
                    var data=Activator.CreateInstance(dataType,true);
                    dataType.GetField("startPosition").SetValue(data,new Vector3(0,0,n*road.segmentLength));
                    var segment=new GameObject("Test Segment"); segment.transform.SetParent(root.transform,false);
                    create.Invoke(road,new object[]{segment.transform,data});
                }
                var instances=root.GetComponentsInChildren<VoxelSceneryInstance>(true);
                Check(instances.Length>=20,"Scenery did not populate road sections");
                for(int i=0;i<instances.Length;i++)
                {
                    var item=instances[i];
                    Check(Mathf.Abs(item.transform.position.x)-item.radius>=road.roadWidth*.5f,"Scenery intrudes into road");
                    for(int j=0;j<i;j++)
                    {
                        Vector3 delta=item.transform.position-instances[j].transform.position; delta.y=0;
                        Check(delta.magnitude+.001f>=item.radius+instances[j].radius,"Scenery footprints overlap");
                    }
                }
                var invalid=ScriptableObject.CreateInstance<VoxelScenerySet>();
                try { Check(invalid.Choose()==null,"Empty set should be safe"); } finally { UnityEngine.Object.DestroyImmediate(invalid); }
                Debug.Log("PASS: 14 single-renderer props, maximum "+maxTriangles+" triangles per model; "+instances.Length+" instances across 4 road sections, road clearance and spacing verified; empty set safe.");
            }
            finally { UnityEngine.Random.state=random; UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(track); }
        }
        private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    }
}
