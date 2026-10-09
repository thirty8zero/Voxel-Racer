using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelRoadShoulderValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
        private static Mesh[] Meshes(EndlessVoxelRoad road) => road.GetComponentsInChildren<VoxelRoadStripMesh>().Select(s=>s.GetComponent<MeshFilter>().sharedMesh).ToArray();

        [MenuItem("Tools/Voxel Racer/Validate and Render Road Shoulder Joins")]
        public static void Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Run road shoulder validation in Edit Mode.");
            Directory.CreateDirectory("Temp/RoadShoulders");
            var scene=EditorSceneManager.NewPreviewScene();var random=UnityEngine.Random.state;
            string[] materialNames={"roadMaterial","shoulderMaterial","lineMaterial","groundMaterial"};
            var fields=materialNames.Select(n=>typeof(VoxelRacerBootstrap).GetField(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
            var saved=fields.Select(f=>f.GetValue(null)).ToArray();
            var materials=new[]{new Material(Shader.Find("Voxel Racer/Road Asphalt")),new Material(Shader.Find("Universal Render Pipeline/Lit")),
                new Material(Shader.Find("Universal Render Pipeline/Lit")),new Material(Shader.Find("Universal Render Pipeline/Lit"))};
            materials[0].color=new Color(.29f,.29f,.32f);materials[1].color=Color.white;
            materials[2].color=new Color(1,.78f,.16f);materials[3].color=new Color(.34f,.09f,.055f);
            var root=new GameObject("Road shoulder QA");SceneManager.MoveGameObjectToScene(root,scene);
            int joins=0,rays=0,scenarios=0;
            try
            {
                for(int i=0;i<fields.Length;i++)fields[i].SetValue(null,materials[i]);
                Check(!ShaderUtil.ShaderHasError(materials[0].shader),"Asphalt shader has compile errors");
                // Include tight/coarse left/right bends, consecutive turns and every authored mission.
                var settings=new List<VoxelRoadTuning>();
                var straight=ScriptableObject.CreateInstance<VoxelRoadTuning>();straight.turnChancePerSegment=0;settings.Add(straight);
                var tight=ScriptableObject.CreateInstance<VoxelRoadTuning>();tight.turnChancePerSegment=1;tight.minimumTurnAngle=tight.maximumTurnAngle=45;
                tight.minimumStraightSegmentsBetweenTurns=0;tight.maximumTrackHeading=90;tight.curveDegreesPerSlice=15;tight.roadWidth=20;tight.segmentCount=12;settings.Add(tight);
                settings.AddRange(VoxelTrackSequence.Load().tracks.Select(t=>t.roadTuning));
                try
                {
                    foreach(var tuning in settings)
                    {
                        var go=new GameObject("Road case "+scenarios);go.transform.SetParent(root.transform,false);
                        var road=go.AddComponent<EndlessVoxelRoad>();road.enabled=false;
                        tuning.ApplyTo(road);road.minimumCactiPerSegment=road.maximumCactiPerSegment=0;
                        road.BuildInitialRoad();
                        var segments=go.transform.Cast<Transform>().Where(t=>t.name=="Road Segment").ToArray();
                        Check(segments.Length==road.segmentCount,"Road chunk count changed");
                        Transform previous=null;
                        foreach(var segment in segments)
                        {
                            Check(segment.GetComponentsInChildren<VoxelRoadStripMesh>().Length==3,"Each chunk needs exactly three road surface renderers");
                            var asphalt=segment.Find("Road");var left=segment.Find("Left Shoulder");var right=segment.Find("Right Shoulder");
                            var properties=new MaterialPropertyBlock();asphalt.GetComponent<MeshRenderer>().GetPropertyBlock(properties);
                            Check(properties.GetFloat("_UseRoadUV")==1f,"Asphalt does not use sampled metre coordinates");
                            CheckEdges(asphalt,left,-road.roadWidth*.5f);CheckEdges(asphalt,right,road.roadWidth*.5f);
                            foreach(var strip in new[]{asphalt,left,right})
                            {
                                var mesh=strip.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
                                var uv=mesh.uv;var normals=mesh.normals;
                                Check(mesh.triangles.Length<=12*((vertices.Length-8)/16)*3,"Road triangle count exceeds old box budget");
                                int slices=(vertices.Length-8)/16;
                                for(int s=0;s<slices;s++)
                                {
                                    int v=s*16;
                                    Check(normals.Skip(v).Take(4).All(n=>n.y>.999f),"Top surface winding is wrong");
                                    for(int sample=1;sample<=3;sample++)
                                    {
                                        float t=sample*.25f;
                                        Vector3 a=Vector3.Lerp(vertices[v],vertices[v+1],t),b=Vector3.Lerp(vertices[v+3],vertices[v+2],t);
                                        var centre=strip.TransformPoint((a+b)*.5f);
                                        Check(strip.GetComponent<MeshCollider>().Raycast(new Ray(centre+Vector3.up*2,Vector3.down),out var hit,3) &&
                                            Mathf.Abs(hit.point.y-centre.y)<.0001f,"Road/shoulder collider has a hole");rays++;
                                    }
                                    if(s+1<slices)
                                    {
                                        Check(Vector3.Distance(vertices[v+1],vertices[v+16])<.0001f && Vector3.Distance(vertices[v+2],vertices[v+19])<.0001f,
                                            "Gap inside a road strip");joins++;
                                    }
                                }
                                if(previous!=null)
                                {
                                    var end=EndPoints(previous.Find(strip.name),false);
                                    var start=EndPoints(strip,true);
                                    Check(end.All(p=>start.Any(q=>Vector3.Distance(p,q)<.0002f)),"Gap between road chunks");joins++;
                                    var prevUv=previous.Find(strip.name).GetComponent<MeshFilter>().sharedMesh.uv;
                                    Check(Mathf.Abs(prevUv.Max(p=>p.y)-uv.Min(p=>p.y))<.0002f,"Asphalt patch coordinates jump at chunk boundary");
                                }
                            }
                            previous=segment;
                        }
                        // Make a matching old-box reference and render the exact same bend before/after.
                        if(tuning==tight)
                        {
                            float distance=road.segmentLength*2;
                            var centre=road.Evaluate(distance).position;
                            var before=OldBoxes(road,root.transform,materials);
                            Render(before,"Before",centre);Render(go,"After",centre);
                            Object.DestroyImmediate(before);
                        }
                        var owned=Meshes(road);
                        var clone=Object.Instantiate(go,root.transform);Object.DestroyImmediate(clone);
                        Check(owned.All(m=>m!=null),"Preview clone released live road meshes");
                        var path=(IList)typeof(EndlessVoxelRoad).GetField("pathSegments",Private).GetValue(road);
                        var visible=(IList)typeof(EndlessVoxelRoad).GetField("visibleSegments",Private).GetValue(road);
                        path.Clear();visible.Clear();road.RebuildSegmentCache();
                        Check(owned.All(m=>m==null) && Meshes(road).Length==road.segmentCount*3,"Cache rebuild leaked meshes or duplicated strips");
                        owned=Meshes(road);Object.DestroyImmediate(go);
                        Check(owned.All(m=>m==null),"Road chunk destruction leaked generated meshes");scenarios++;
                    }
                }
                finally {Object.DestroyImmediate(straight);Object.DestroyImmediate(tight);}
                File.WriteAllText("Temp/RoadShoulders/Validation.txt",$"PASS: {scenarios} road cases (straight, tight/coarse consecutive bends, all missions), {joins} matching slice/chunk joins and {rays} road/shoulder collision samples; shared asphalt/shoulder boundaries, continuous metre UVs, upward normals, three renderers per chunk and triangle budget, preview cloning, cache rebuild and native mesh cleanup. Matching before/after renders captured; authored tuning/run state untouched.");
                Debug.Log(File.ReadAllText("Temp/RoadShoulders/Validation.txt"));
            }
            catch(Exception e){File.WriteAllText("Temp/RoadShoulders/Validation.txt","FAIL: "+e);throw;}
            finally
            {
                Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);
                for(int i=0;i<fields.Length;i++)fields[i].SetValue(null,saved[i]);
                foreach(var material in materials)Object.DestroyImmediate(material);UnityEngine.Random.state=random;
            }
        }
        private static void CheckEdges(Transform road,Transform shoulder,float offset)
        {
            var mesh=road.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var uv=mesh.uv;
            var shoulderMesh=shoulder.GetComponent<MeshFilter>().sharedMesh;var sv=shoulderMesh.vertices;var suv=shoulderMesh.uv;
            for(int i=0;i<vertices.Length;i++)if(Mathf.Abs(vertices[i].y)<.0001f && Mathf.Abs(uv[i].x-offset)<.0001f)
            {
                var p=road.TransformPoint(vertices[i]);p.y=0;bool found=false;
                for(int j=0;j<sv.Length;j++)if(Mathf.Abs(sv[j].y-.01f)<.0001f && Mathf.Abs(suv[j].x-offset)<.0001f && Mathf.Abs(suv[j].y-uv[i].y)<.0001f)
                {var q=shoulder.TransformPoint(sv[j]);q.y=0;if(Vector3.Distance(p,q)<.0001f)found=true;}
                Check(found,"Shoulder does not share the asphalt boundary");
            }
        }
        private static Vector3[] EndPoints(Transform strip,bool start)
        {
            var mesh=strip.GetComponent<MeshFilter>().sharedMesh;var uv=mesh.uv;var vertices=mesh.vertices;
            float distance=start?uv.Min(p=>p.y):uv.Max(p=>p.y),height=vertices.Max(v=>v.y);
            return vertices.Where((v,i)=>Mathf.Abs(v.y-height)<.0001f && Mathf.Abs(uv[i].y-distance)<.0001f).Select(strip.TransformPoint).Distinct().ToArray();
        }
        private static GameObject OldBoxes(EndlessVoxelRoad road,Transform parent,Material[] materials)
        {
            var go=new GameObject("Previous box joins");go.transform.SetParent(parent,false);
            var path=(IList)typeof(EndlessVoxelRoad).GetField("pathSegments",Private).GetValue(road);
            foreach(var data in path.Cast<object>().Take(road.segmentCount))
            {
                var type=data.GetType();float start=(float)type.GetField("startDistance").GetValue(data),angle=(float)type.GetField("turnAngle").GetValue(data);
                int slices=Mathf.Abs(angle)<.001f?1:Mathf.Max(2,Mathf.CeilToInt(Mathf.Abs(angle)/road.curveDegreesPerSlice));
                float length=road.segmentLength/slices;
                for(int s=0;s<slices;s++)
                {
                    var pose=road.Evaluate(start+(s+.5f)*length);float depth=length+(slices>1?.65f:0);
                    Box("Road",pose,0,-.14f,new Vector3(road.roadWidth,.28f,depth),materials[0]);
                    Box("Shoulder",pose,-road.roadWidth*.5f-.275f,-.08f,new Vector3(.55f,.18f,depth),materials[1]);
                    Box("Shoulder",pose,road.roadWidth*.5f+.275f,-.08f,new Vector3(.55f,.18f,depth),materials[1]);
                }
            }
            foreach(var dash in road.GetComponentsInChildren<Transform>().Where(t=>t.name=="Lane Dash"))Object.Instantiate(dash.gameObject,go.transform,true);
            Object.Instantiate(road.transform.Find("Continuous Brown Ground").gameObject,go.transform,true);
            return go;
            void Box(string name,VoxelTrackPose pose,float offset,float height,Vector3 scale,Material material)
            {
                var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(go.transform,false);
                box.transform.SetPositionAndRotation(pose.position+pose.right*offset+Vector3.up*height,pose.rotation);box.transform.localScale=scale;
                box.GetComponent<MeshRenderer>().sharedMaterial=material;
                var properties=new MaterialPropertyBlock();properties.SetVector("_RoadCoordinates",new Vector4(road.roadWidth,scale.z,Vector3.Dot(pose.position,pose.forward),0));
                box.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
            }
        }
        private static void Render(GameObject source,string name,Vector3 centre)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var clone=Object.Instantiate(source);
                // Unity does not copy renderer property blocks when cloning a preview hierarchy.
                var from=source.GetComponentsInChildren<MeshRenderer>(true);var to=clone.GetComponentsInChildren<MeshRenderer>(true);
                for(int i=0;i<from.Length;i++)
                {var block=new MaterialPropertyBlock();from[i].GetPropertyBlock(block);to[i].SetPropertyBlock(block);}
                preview.AddSingleGO(clone);preview.camera.orthographic=true;preview.camera.orthographicSize=29;
                preview.camera.transform.position=centre+new Vector3(-20,40,-38);preview.camera.transform.LookAt(centre);
                preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=1000;preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.camera.backgroundColor=new Color(.34f,.09f,.055f);preview.ambientColor=new Color(.7f,.7f,.7f);
                preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(50,-30,0);preview.lights[1].intensity=.6f;
                preview.BeginStaticPreview(new Rect(0,0,1400,900));preview.Render(true);
                var image=preview.EndStaticPreview();File.WriteAllBytes("Temp/RoadShoulders/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally {preview.Cleanup();}
        }
    }
}
