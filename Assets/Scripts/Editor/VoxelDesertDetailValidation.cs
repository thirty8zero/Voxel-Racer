using System;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelDesertDetailValidation
    {
        public const string PreviewPath="Temp/DesertDetails/Roadside.png";
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        [MenuItem("Tools/Voxel Racer/Validate Desert Foliage and Tall Cacti")]
        public static void Run()
        {
            var set=AssetDatabase.LoadAssetAtPath<VoxelScenerySet>(VoxelDesertSceneryBuilder.SetPath);
            var tuning=AssetDatabase.LoadAssetAtPath<VoxelDesertFoliageTuning>(VoxelDesertDetailBuilder.TuningPath);
            Check(set.groundCover==tuning && tuning.variants.Length==16,"Foliage integration missing");
            var importer=(TextureImporter)AssetImporter.GetAtPath(VoxelDesertDetailBuilder.Folder+"/DesertFoliageAtlas.png");
            Check(!importer.isReadable && importer.mipmapEnabled && importer.mipMapsPreserveCoverage && importer.maxTextureSize==1024,"Atlas import is not optimized");
            Check(importer.GetPlatformTextureSettings("Android").format==TextureImporterFormat.ASTC_6x6,"Mobile atlas format incorrect");
            Check(tuning.material.enableInstancing && tuning.material.shader.name=="Voxel Racer/Desert Foliage","Instanced cutout material missing");
            foreach(var message in ShaderUtil.GetShaderMessages(tuning.material.shader))Check(message.severity!=UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error,message.message);
            foreach(var mesh in tuning.variants)
                Check(mesh!=null && mesh.vertexCount==8 && mesh.triangles.Length==12 && mesh.subMeshCount==1,"Billboards exceed four-triangle budget");
            using(var tree=PropertyTree.Create(tuning))
            {
                tree.UpdateTree();var properties=tree.EnumerateTree(true).ToArray();
                foreach(var field in typeof(VoxelDesertFoliageTuning).GetFields().Where(f=>!f.IsStatic))
                    Check(properties.Any(p=>p.Name==field.Name && p.Attributes.OfType<FoldoutGroupAttribute>().Any()),"Missing Inspector group: "+field.Name);
            }
            int cactusTriangles=0;
            foreach(var entry in set.entries.Where(e=>e.prefab!=null && e.prefab.name.StartsWith("SaguaroGrand")))
            {
                var mesh=entry.prefab.GetComponent<MeshFilter>().sharedMesh;
                Check(mesh.bounds.size.y>7 && mesh.bounds.size.y<10,"Tall cactus has wrong height");
                Check(mesh.triangles.Length/3<5000 && entry.prefab.GetComponentsInChildren<MeshRenderer>().Length==1 &&
                    entry.prefab.GetComponentsInChildren<Collider>().Length==0,"Cactus rendering budget/structure incorrect");
                cactusTriangles=Mathf.Max(cactusTriangles,mesh.triangles.Length/3);
            }
            Check(set.entries.Count(e=>e.prefab!=null && e.prefab.name.StartsWith("SaguaroGrand"))==3,"Missing tall cactus variants");
            var random=UnityEngine.Random.state;var preview=new PreviewRenderUtility();
            var root=new GameObject("Temporary Desert Detail Preview");Texture2D shot=null;
            try
            {
                UnityEngine.Random.InitState(2781);VoxelRacerBootstrap.ReloadGeneratedMaterials();preview.AddSingleGO(root);
                var road=root.AddComponent<EndlessVoxelRoad>();road.enabled=false;
                var track=Resources.Load<VoxelTrackDefinition>("Tracks/Track01");
                if(track.roadTuning!=null)track.roadTuning.ApplyTo(road);
                road.tuning=null;road.trackDefinition=track;road.segmentCount=8;road.turnChancePerSegment=.45f;road.turnSeed=173;
                road.BuildInitialRoad();
                var path=(System.Collections.ICollection)typeof(EndlessVoxelRoad).GetField("pathSegments",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(road);
                Check(path.Count==road.segmentCount,"Foliage sampling appended path-only segments during road construction");
                var covers=root.GetComponentsInChildren<VoxelDesertGroundCover>();
                Check(covers.Length==8 && covers.Sum(c=>c.ClumpCount)>700,"Roadside foliage is missing or too sparse");
                foreach(var cover in covers)
                {
                    var renderer=cover.GetComponent<MeshRenderer>();var mesh=cover.GetComponent<MeshFilter>().sharedMesh;
                    Check(renderer.sharedMaterial==tuning.material && renderer.shadowCastingMode==ShadowCastingMode.Off &&
                        !renderer.receiveShadows && mesh.triangles.Length/3==cover.ClumpCount*4,"Segment foliage batching incorrect");
                    foreach(var vertex in mesh.vertices)
                    {
                        var p=cover.transform.TransformPoint(vertex);float closest=float.PositiveInfinity;
                        for(float d=-20;d<road.segmentLength*road.segmentCount+20;d+=2)
                        {var delta=road.Evaluate(d).position-p;delta.y=0;closest=Mathf.Min(closest,delta.sqrMagnitude);}
                        Check(closest>road.roadWidth*road.roadWidth*.25f,"Foliage intersects the curved road");
                    }
                }
                var scenery=root.AddComponent<VoxelDistantScenery>();scenery.enabled=false;
                preview.camera.transform.position=new Vector3(0,10,-14);preview.camera.transform.LookAt(new Vector3(0,1,43));
                preview.camera.fieldOfView=55;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=260;
                preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.5f,.29f,.17f);
                preview.lights[0].intensity=1.15f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-35,0);
                preview.lights[1].intensity=.35f;preview.ambientColor=new Color(.45f,.45f,.45f);
                scenery.Prepare(preview.camera);int cached=scenery.CachedPropCount,visible=scenery.VisiblePropCount,batches=scenery.VisibleBatchCount;
                Check(cached>500 && visible>50 && batches<=set.entries.Length+16,"Distant instancing groups invalid");
                preview.camera.transform.Rotate(0,180,0,Space.World);scenery.Prepare(preview.camera);
                Check(scenery.CachedPropCount==cached && scenery.VisiblePropCount>0,"Scenery vanishes behind orbiting camera");
                preview.camera.transform.LookAt(new Vector3(0,1,43));
                preview.BeginStaticPreview(new Rect(0,0,1600,1000));scenery.SubmitDraws(preview.camera);preview.Render(true);shot=preview.EndStaticPreview();
                Directory.CreateDirectory("Temp/DesertDetails");File.WriteAllBytes(PreviewPath,shot.EncodeToPNG());
                string report="PASS: 16 four-triangle crossed cards, one 1024 atlas with mips/alpha-coverage and Android ASTC 6x6; no shader errors; editable Odin foldouts; three 7–10m single-mesh cacti, max "+cactusTriangles+" triangles. "+covers.Sum(c=>c.ClumpCount)+" near clumps across "+covers.Length+" recycled segment batches, no foliage shadows/colliders and curved-road clearance. "+cached+" distant props cached, "+visible+" visible across "+batches+" mesh/material groups; rear/orbit coverage retained. Source prefabs/settings and gameplay state untouched by validation. This is editor verification, not a target-phone frame-time measurement.";
                File.WriteAllText("Temp/DesertDetails/Validation.txt",report);Debug.Log(report);
            }
            finally{if(shot!=null)Object.DestroyImmediate(shot);preview.Cleanup();if(root!=null)Object.DestroyImmediate(root);UnityEngine.Random.state=random;}
        }
    }
}
