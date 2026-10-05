using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Authors the static hazard prefab; gameplay only instantiates the saved mesh.</summary>
    public static class VoxelRoadsideTurretModelBuilder
    {
        public const string Folder = "Assets/Resources/EnemyTurrets/Model";
        public const string PrefabPath = Folder + "/RoadsideTurretModel.prefab";
        private static readonly List<Vector3> Vertices = new();
        private static readonly List<Vector2> Uvs = new();
        private static readonly List<int>[] Triangles = { new(), new(), new(), new(), new(), new() };
        private const int Red = 0, DarkRed = 1, Steel = 2, LightSteel = 3, Dark = 4, White = 5;

        [MenuItem("Tools/Voxel Racer/Build Roadside Turret Model")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Build outside Play Mode.");
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            Material[] materials = {
                Mat("TurretRed", new Color(.72f,.025f,.045f), .2f),
                Mat("TurretRedShadow", new Color(.32f,.018f,.027f), .2f),
                Mat("TurretGunmetal", new Color(.28f,.32f,.30f), .5f),
                Mat("TurretSteel", new Color(.53f,.58f,.55f), .45f),
                Mat("TurretRecess", new Color(.035f,.045f,.04f), .2f),
                Mat("TurretMarking", new Color(.84f,.86f,.79f), .15f)
            };
            Vertices.Clear(); Uvs.Clear(); foreach (var list in Triangles) list.Clear();

            // Four splayed articulated legs. Faceted caps and bevels retain the voxel silhouette.
            for (int i = 0; i < 4; i++)
            {
                float angle = 45 + i * 90;
                var rotation = Quaternion.Euler(0, angle, 0);
                Vector3 radial = rotation * Vector3.forward;
                Vector3 tangent = rotation * Vector3.right;
                Vector3 hip = radial * .44f + Vector3.up * .50f;
                Vector3 knee = radial * .78f + Vector3.up * .43f;
                Vector3 ankle = radial * 1.03f + Vector3.up * .17f;
                Box(radial * 1.06f + Vector3.up * .08f, new Vector3(.38f,.16f,.55f), rotation, Steel, .05f);
                Box(radial * 1.14f + Vector3.up * .165f, new Vector3(.30f,.035f,.30f), rotation, LightSteel, .025f);
                Beam(hip, knee, .29f, .31f, Red);
                Beam(knee, ankle, .31f, .34f, Red);
                Beam(knee - radial * .06f, ankle - radial * .06f, .18f, .20f, DarkRed);
                Disc(hip, tangent, .16f, .30f, Red);
                foreach(int capSide in new[]{-1,1}) Disc(hip + tangent * (.16f*capSide), tangent, .085f, .025f, Steel);
                Disc(knee, tangent, .14f, .36f, DarkRed);
                foreach(int capSide in new[]{-1,1}) Disc(knee + tangent * (.19f*capSide), tangent, .095f, .035f, LightSteel);
                Disc(ankle, tangent, .10f, .36f, Steel);
            }
            Disc(new Vector3(0,.48f,0), Vector3.up, .49f, .10f, Dark);
            Disc(new Vector3(0,.59f,0), Vector3.up, .55f, .17f, Red);
            Disc(new Vector3(0,.695f,0), Vector3.up, .48f, .045f, LightSteel);
            Disc(new Vector3(0,.738f,0), Vector3.up, .40f, .05f, Dark);
            foreach (float x in new[] { -.13f, .13f })
                Box(new Vector3(x,.61f,.526f), new Vector3(.07f,.12f,.025f), Quaternion.identity, White, .008f);

            // Two red cheeks support the horizontal cannon, leaving a dark hinge recess.
            Box(new Vector3(0,.86f,-.08f), new Vector3(.40f,.24f,.37f), Quaternion.identity, Dark, .03f);
            foreach (int side in new[] { -1, 1 })
            {
                Box(new Vector3(side*.285f,.86f,-.08f), new Vector3(.15f,.31f,.44f), Quaternion.Euler(-12,0,0), Red, .055f);
                Disc(new Vector3(side*.37f,.99f,-.08f), Vector3.right, .105f, .035f, Steel);
            }
            // Wide rear housing, stepped side covers, smaller breech and an octagonal muzzle.
            Box(new Vector3(0,1.18f,-.22f), new Vector3(.83f,.48f,1.26f), Quaternion.identity, Steel, .075f);
            Box(new Vector3(0,1.18f,.40f), new Vector3(.65f,.38f,.35f), Quaternion.identity, LightSteel, .075f);
            Box(new Vector3(0,1.18f,-.862f), new Vector3(.67f,.34f,.025f), Quaternion.identity, Dark, .045f);
            foreach (int side in new[] { -1, 1 })
            {
                Box(new Vector3(side*.43f,1.16f,-.26f), new Vector3(.045f,.22f,.50f), Quaternion.identity, LightSteel, .025f);
                foreach (float z in new[] { -.68f, -.18f })
                    Box(new Vector3(side*.428f,1.32f,z), new Vector3(.025f,.035f,.04f), Quaternion.identity, Dark, .005f);
                for (int vent = 0; vent < 3; vent++)
                    Box(new Vector3(side*.421f,1.19f,-.64f+vent*.09f), new Vector3(.022f,.13f,.028f), Quaternion.identity, Dark, .003f);
            }
            Box(new Vector3(0,1.428f,.25f), new Vector3(.39f,.025f,.32f), Quaternion.identity, White, .025f);
            Box(new Vector3(0,1.454f,.20f), new Vector3(.09f,.03f,.09f), Quaternion.identity, Dark, .01f);
            Box(new Vector3(0,1.472f,.22f), new Vector3(.045f,.013f,.035f), Quaternion.identity, Red, .003f);
            Disc(new Vector3(0,1.18f,.62f), Vector3.forward, .15f, .17f, Dark);
            Disc(new Vector3(0,1.18f,.97f), Vector3.forward, .085f, .62f, Steel);
            Disc(new Vector3(0,1.18f,1.30f), Vector3.forward, .13f, .14f, LightSteel);
            // Hollow muzzle sleeve: the dark bore is recessed behind an actual opening.
            Ring(new Vector3(0,1.18f,1.48f), .18f, .103f, .30f, Steel, LightSteel);
            Disc(new Vector3(0,1.18f,1.35f), Vector3.forward, .101f, .008f, Dark);

            var mesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(Folder + "/RoadsideTurretMesh.asset");
            if (mesh == null) { mesh = new UnityEngine.Mesh(); AssetDatabase.CreateAsset(mesh, Folder + "/RoadsideTurretMesh.asset"); }
            mesh.Clear(); mesh.name = "RoadsideTurretMesh";
            mesh.SetVertices(Vertices); mesh.SetUVs(0, Uvs); mesh.subMeshCount = materials.Length;
            for (int i = 0; i < materials.Length; i++) mesh.SetTriangles(Triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var root = new GameObject("Roadside Turret Model");
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = materials;
                var muzzle = new GameObject("Turret Muzzle"); muzzle.transform.SetParent(root.transform, false);
                // Matches the previous projectile origin (.93 + .70) and exactly the same firing height.
                muzzle.transform.localPosition = new Vector3(0,1.18f,1.63f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(root); }
            Validate(); Render();
        }

        private static Material Mat(string name, Color colour, float metal)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material; // Preserve later authored material edits.
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", colour); material.SetFloat("_Metallic", metal); material.SetFloat("_Smoothness", .28f);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static void Face(int material, params Vector3[] points)
        {
            int start = Vertices.Count;
            for (int i=0;i<points.Length;i++) { Vertices.Add(points[i]); Uvs.Add(new Vector2(i==1||i==2?1:0,i>=2?1:0)); }
            for(int i=1;i<points.Length-1;i++) Triangles[material].AddRange(new[]{start,start+i,start+i+1});
        }

        private static Vector3[] Profile(float width, float height, float bevel)
        {
            float x=width*.5f,y=height*.5f,b=Mathf.Min(bevel,Mathf.Min(x,y)*.8f);
            return new[]{new Vector3(-x+b,-y,0),new Vector3(x-b,-y,0),new Vector3(x,-y+b,0),new Vector3(x,y-b,0),new Vector3(x-b,y,0),new Vector3(-x+b,y,0),new Vector3(-x,y-b,0),new Vector3(-x,-y+b,0)};
        }

        private static void Extrude(Vector3 centre, Quaternion rotation, Vector3[] profile, float length, int material)
        {
            int count=profile.Length;var back=new Vector3[count];var front=new Vector3[count];
            for(int i=0;i<count;i++) { back[i]=centre+rotation*(profile[i]-Vector3.forward*length*.5f);front[i]=centre+rotation*(profile[i]+Vector3.forward*length*.5f); }
            // Profile is CCW seen from +Z. Rear faces -Z; front faces +Z.
            Array.Reverse(back);Face(material,back);Face(material,front);
            for(int i=0;i<count;i++) {int next=(i+1)%count;Vector3 a=centre+rotation*(profile[i]-Vector3.forward*length*.5f),b=centre+rotation*(profile[next]-Vector3.forward*length*.5f);Face(material,a,b,b+rotation*Vector3.forward*length,a+rotation*Vector3.forward*length);}
        }

        private static void Box(Vector3 centre, Vector3 size, Quaternion rotation, int material, float bevel) => Extrude(centre,rotation,Profile(size.x,size.y,bevel),size.z,material);
        private static void Beam(Vector3 a,Vector3 b,float width,float depth,int material) => Box((a+b)*.5f,new Vector3(width,depth,Vector3.Distance(a,b)+.10f),Quaternion.LookRotation(b-a),material,.035f);
        private static Vector3[] Octagon(float radius)
        {
            var profile=new Vector3[8];for(int i=0;i<8;i++){float angle=(22.5f+i*45f)*Mathf.Deg2Rad;profile[i]=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*radius;}return profile;
        }
        private static void Disc(Vector3 centre,Vector3 axis,float radius,float length,int material) => Extrude(centre,Quaternion.LookRotation(axis),Octagon(radius),length,material);
        private static void Ring(Vector3 centre,float outer,float inner,float length,int body,int rim)
        {
            var o=Octagon(outer);var n=Octagon(inner);Vector3 back=centre-Vector3.forward*length*.5f,front=centre+Vector3.forward*length*.5f;
            for(int i=0;i<8;i++){int j=(i+1)%8;Face(body,back+o[i],back+o[j],front+o[j],front+o[i]);Face(body,back+n[j],back+n[i],front+n[i],front+n[j]);Face(rim,front+o[i],front+o[j],front+n[j],front+n[i]);Face(body,back+o[j],back+o[i],back+n[i],back+n[j]);}
        }

        [MenuItem("Tools/Voxel Racer/Validate Roadside Turret Model")]
        public static void Validate()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab==null)throw new InvalidOperationException("Build the turret first.");
            var muzzle=prefab.transform.Find("Turret Muzzle");
            if(muzzle==null || Vector3.Distance(muzzle.localPosition,new Vector3(0,1.18f,1.63f))>.0001f || muzzle.localScale!=Vector3.one || muzzle.localRotation!=Quaternion.identity)
                throw new InvalidOperationException("Turret muzzle must preserve its original firing plane and unit scale.");
            var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;
            if(prefab.GetComponentsInChildren<Collider>(true).Length!=0 || prefab.GetComponentsInChildren<MeshRenderer>(true).Length!=1 || mesh.vertexCount>10000 || mesh.bounds.min.y<-.001f || mesh.bounds.max.y>1.50f)
                throw new InvalidOperationException("Turret collider, geometry budget or height validation failed.");
            Directory.CreateDirectory("Temp/Turret");
            File.WriteAllText("Temp/Turret/ModelValidation.txt","PASS: saved turret prefab, one renderer/six shared materials, no colliders, muzzle (0, 1.18, 1.63) with unit scale/+Z aim; grounded feet and similar original height. Vertices: "+mesh.vertexCount+". Height: "+mesh.bounds.max.y.ToString("0.000")+"m.");
            Debug.Log(File.ReadAllText("Temp/Turret/ModelValidation.txt"));
        }

        [MenuItem("Tools/Voxel Racer/Render Roadside Turret Model")]
        public static void Render()
        {
            Directory.CreateDirectory("Temp/Turret");
            Vector3[] views={new(4,2.7f,5),new(-4,2.7f,-5),new(5,1.4f,0),new(0,1.4f,6)};
            string[] names={"Front","Rear","Side","FrontStraight"};
            for(int i=0;i<views.Length;i++)
            {
                var preview=new PreviewRenderUtility();
                try
                {
                    preview.AddSingleGO(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
                    preview.camera.transform.position=views[i];preview.camera.transform.LookAt(new Vector3(0,.72f,.18f));preview.camera.fieldOfView=30;
                    preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.11f,.14f,.17f);
                    preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,25,0);preview.lights[1].intensity=1.3f;preview.lights[1].transform.rotation=Quaternion.Euler(30,210,0);preview.ambientColor=new Color(.45f,.45f,.45f);
                    preview.BeginStaticPreview(new Rect(0,0,1100,850));preview.Render(true);var image=preview.EndStaticPreview();File.WriteAllBytes("Temp/Turret/"+names[i]+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
                finally{preview.Cleanup();}
            }
        }
    }
}
