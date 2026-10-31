using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelUnderbodyArmorBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Upgrades/UnderbodyArmor.prefab";
        public const string MeshPath = "Assets/Prefabs/Upgrades/UnderbodyArmorMesh.asset";
        public const string TuningPath = "Assets/Resources/Upgrades/UnderbodyArmorTuning.asset";

        [MenuItem("Tools/Voxel Racer/Build Underbody Mine Armour Upgrade")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Build in Edit Mode.");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null) { mesh = new Mesh { name = "UnderbodyArmorMesh" }; AssetDatabase.CreateAsset(mesh, MeshPath); }
            var data = new Geometry();
            var root = new GameObject("Underbody Mine Armour"); root.AddComponent<VoxelIndestructiblePart>();
            try
            {
                Plate(root.transform, data, "Front Engine Plate", 1.80f, 1.44f, 1.22f);
                Plate(root.transform, data, "Front Centre Plate", .64f, 1.30f, 1.02f);
                Plate(root.transform, data, "Transmission Plate", -.57f, 1.22f, 1.26f);
                Plate(root.transform, data, "Rear Plate", -1.80f, 1.40f, 1.12f);
                // Small upturned bumper returns. The kit stays tucked inside the body
                // width and wheel envelope; only these lips peek out at low angles.
                data.Box(new Vector3(0, .205f, 2.39f), new Vector3(1.18f, .035f, .17f), Quaternion.Euler(-20, 0, 0), 0);
                data.Box(new Vector3(0, .205f, -2.34f), new Vector3(.50f, .035f, .15f), Quaternion.Euler(20, 0, 0), 0);
                mesh.Clear(); mesh.indexFormat = data.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(data.Vertices); mesh.SetNormals(data.Normals); mesh.subMeshCount = 3;
                for (int i = 0; i < 3; i++) mesh.SetTriangles(data.Triangles[i], i);
                mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
                var visual = new GameObject("Four Perforated Skid Plates"); visual.transform.SetParent(root.transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                visual.AddComponent<MeshRenderer>().sharedMaterials = new[] {
                    Material("UnderbodyArmourSteel", new Color(.46f,.48f,.50f), .45f),
                    Material("UnderbodyArmourMounts", new Color(.14f,.16f,.18f), .5f),
                    Material("UnderbodyArmourFasteners", new Color(.65f,.68f,.70f), .6f)
                };
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var tuning = AssetDatabase.LoadAssetAtPath<VoxelUnderbodyArmorTuning>(TuningPath);
                if (tuning == null)
                {
                    tuning = ScriptableObject.CreateInstance<VoxelUnderbodyArmorTuning>();
                    tuning.purchasePrice = 700; tuning.mineDamageReductionPercent = 30;
                    tuning.compatibleCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/SpyCar2PlayerCar.prefab");
                    AssetDatabase.CreateAsset(tuning, TuningPath);
                }
                tuning.armorPrefab = prefab; EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssetIfDirty(mesh); AssetDatabase.SaveAssetIfDirty(tuning);
                Debug.Log("Built four underbody skid plates with real service/vent openings, pressed ribs, folded returns, eight chassis mounts and underside hex fasteners; " +
                    data.Triangles.Sum(t => t.Count / 3) + " triangles, one renderer/three shared materials, no colliders or integrity. Existing tuning/material edits preserved.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Material Material(string name, Color colour, float metal)
        {
            string path = "Assets/Resources/CarMaterials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = colour, enableInstancing = true };
            material.SetFloat("_Metallic", metal); material.SetFloat("_Smoothness", .25f); AssetDatabase.CreateAsset(material, path); return material;
        }

        private static void Plate(Transform root, Geometry geometry, string name, float z, float width, float length)
        {
            var anchor = new GameObject(name).transform; anchor.SetParent(root, false); anchor.localPosition = new Vector3(0, .18f, z);
            float hw = width * .5f, hl = length * .5f;
            var outline = new[] { new Vector2(-hw + .10f, -hl), new Vector2(hw - .10f, -hl), new Vector2(hw, -hl + .10f),
                new Vector2(hw, hl - .10f), new Vector2(hw - .10f, hl), new Vector2(-hw + .10f, hl), new Vector2(-hw, hl - .10f), new Vector2(-hw, -hl + .10f) };
            var holes = new List<Rect>();
            // Real open slots and two service apertures, not black painted rectangles.
            foreach (int side in new[] { -1, 1 }) foreach (float row in new[] { -.22f, .22f })
                holes.Add(new Rect(side * width * .32f - .025f, row - .12f, .05f, .24f));
            foreach (float row in new[] { -.24f, .24f }) holes.Add(new Rect(-.11f, row - .055f, .22f, .11f));
            bool exhaustChannels = z < 1f;
            if (exhaustChannels)
                foreach (int side in new[] { -1, 1 })
                {
                    // Leave clearance for the standard pipe and both V6 pipes, including
                    // the downpipes. Channels stop ahead of the downpipe at world Z=.9.
                    float end = Mathf.Min(hl + .01f, .9f - z);
                    holes.Add(new Rect(side * .4f - .12f, -hl - .01f, .24f, end + hl + .01f));
                    if (z < -1.5f) holes.Add(new Rect(side < 0 ? -hw - .01f : .27f, -hl - .01f,
                        hw - .26f, -1.80f - z + hl + .01f));
                }
            var xs = new SortedSet<float> { -hw, hw }; var zs = new SortedSet<float> { -hl, hl };
            foreach (var hole in holes) { xs.Add(hole.xMin); xs.Add(hole.xMax); zs.Add(hole.yMin); zs.Add(hole.yMax); }
            var xx = xs.ToArray(); var zz = zs.ToArray();
            for (int ix = 0; ix < xx.Length - 1; ix++) for (int iz = 0; iz < zz.Length - 1; iz++)
            {
                var centre = new Vector2((xx[ix] + xx[ix + 1]) * .5f, (zz[iz] + zz[iz + 1]) * .5f);
                if (holes.Any(h => h.Contains(centre))) continue;
                var polygon = new List<Vector2> { new(xx[ix], zz[iz]), new(xx[ix + 1], zz[iz]), new(xx[ix + 1], zz[iz + 1]), new(xx[ix], zz[iz + 1]) };
                for (int e = 0; e < outline.Length && polygon.Count > 0; e++) polygon = Clip(polygon, outline[e], outline[(e + 1) % outline.Length]);
                geometry.Face(polygon.Select(p => new Vector3(p.x, .1625f, p.y + z)).ToArray(), 0);
                geometry.Face(polygon.AsEnumerable().Reverse().Select(p => new Vector3(p.x, .1975f, p.y + z)).ToArray(), 0);
                // Emit only actual exposed edges. Open exhaust notches may reach the
                // perimeter; do not leave a wall across the opening or in the muffler.
                for (int edge = 0; edge < polygon.Count; edge++)
                {
                    Vector2 a = polygon[edge], b = polygon[(edge + 1) % polygon.Count];
                    Vector2 d = b - a; if (d.sqrMagnitude < 1e-10f) continue;
                    Vector2 outside = (a + b) * .5f + new Vector2(d.y, -d.x).normalized * .0005f;
                    bool exposed = holes.Any(h => h.Contains(outside));
                    for (int j = 0; j < outline.Length && !exposed; j++)
                    {
                        Vector2 oa = outline[j], ob = outline[(j + 1) % outline.Length];
                        exposed = (ob.x - oa.x) * (outside.y - oa.y) - (ob.y - oa.y) * (outside.x - oa.x) < 0;
                    }
                    if (exposed) geometry.Face(new[] { new Vector3(a.x,.1975f,a.y+z),new Vector3(b.x,.1975f,b.y+z),
                        new Vector3(b.x,.1625f,b.y+z),new Vector3(a.x,.1625f,a.y+z) },0);
                }
            }
            // Folded side edges, shallow pressed ribs, and eight short supports meeting
            // the actual frame rails' underside at Y=.28m / X=+-.58m.
            foreach (int side in new[] { -1, 1 })
            {
                float mountZ = z < -1.5f ? -1.62f : z > 0 && exhaustChannels ? .95f : z;
                float rimZ = z < -1.5f ? -1.51f : z, rimLength = z < -1.5f ? .43f : length - .2f;
                geometry.Box(new Vector3(side * (hw - .025f), .207f, rimZ), new Vector3(.035f, .035f, rimLength), Quaternion.identity, 0);
                geometry.Box(new Vector3(side * (z < -1.5f ? .17f : .25f), .156f, z), new Vector3(.045f, .018f, length - .20f),
                    Quaternion.Euler(0, z < -1.5f ? 0 : side * 7, 0), 0);
                var mount = new GameObject(name + (side < 0 ? " Left Frame Mount" : " Right Frame Mount")).transform;
                mount.SetParent(root, false); mount.localPosition = new Vector3(side * .58f, .28f, mountZ);
                geometry.Box(new Vector3(side * .58f, .237f, mountZ), new Vector3(.10f, .086f, .14f), Quaternion.identity, 1);
                geometry.Box(new Vector3(side * .58f, .199f, mountZ), new Vector3(.16f, .015f, .20f), Quaternion.identity, 1);
                foreach (float end in new[] { -.38f, .38f })
                {
                    float boltZ = z + end * length;
                    float boltX = z < -1.5f ? .20f : exhaustChannels ? hw - .045f : hw - .11f;
                    geometry.Cylinder(new Vector3(side * boltX, .155f, boltZ), .040f, .016f, 8, 2);
                    geometry.Cylinder(new Vector3(side * boltX, .142f, boltZ), .027f, .014f, 6, 2);
                }
            }
            float bridgeZ = z < -1.5f ? -1.62f : z > 0 && exhaustChannels ? .95f : z;
            geometry.Box(new Vector3(0, .255f, bridgeZ), new Vector3(1.18f, .035f, .08f), Quaternion.identity, 1);
            geometry.Box(new Vector3(0, .22f, bridgeZ), new Vector3(.24f, .05f, .08f), Quaternion.identity, 1);
        }

        private static List<Vector2> Clip(List<Vector2> polygon, Vector2 a, Vector2 b)
        {
            var output = new List<Vector2>(); if (polygon.Count == 0) return output;
            float Distance(Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            Vector2 previous = polygon[polygon.Count - 1]; float d0 = Distance(previous);
            foreach (var current in polygon)
            {
                float d1 = Distance(current);
                if ((d0 >= 0) != (d1 >= 0)) output.Add(Vector2.Lerp(previous, current, d0 / (d0 - d1)));
                if (d1 >= 0) output.Add(current); previous = current; d0 = d1;
            }
            return output;
        }

        private sealed class Geometry
        {
            public readonly List<Vector3> Vertices = new(), Normals = new();
            public readonly List<int>[] Triangles = { new(), new(), new() };
            public void Face(Vector3[] polygon, int material)
            {
                for (int i = 1; i < polygon.Length - 1; i++)
                {
                    Vector3 normal = Vector3.Cross(polygon[i] - polygon[0], polygon[i + 1] - polygon[0]);
                    if (normal.sqrMagnitude < 1e-12f) continue; normal.Normalize();
                    int start = Vertices.Count; Vertices.Add(polygon[0]); Vertices.Add(polygon[i]); Vertices.Add(polygon[i + 1]);
                    Normals.Add(normal); Normals.Add(normal); Normals.Add(normal);
                    Triangles[material].Add(start); Triangles[material].Add(start + 1); Triangles[material].Add(start + 2);
                }
            }
            public void Box(Vector3 centre, Vector3 size, Quaternion rotation, int material)
            {
                Vector3 P(float x, float y, float z) => centre + rotation * Vector3.Scale(size * .5f, new Vector3(x, y, z));
                Face(new[] { P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1) },material);
                Face(new[] { P(-1,-1,1),P(1,-1,1),P(1,1,1),P(-1,1,1) },material);
                Face(new[] { P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),P(-1,-1,1) },material);
                Face(new[] { P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1) },material);
                Face(new[] { P(-1,-1,-1),P(-1,-1,1),P(-1,1,1),P(-1,1,-1) },material);
                Face(new[] { P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1) },material);
            }
            public void Cylinder(Vector3 centre, float radius, float height, int sides, int material)
            {
                var bottom = new Vector3[sides]; var top = new Vector3[sides];
                for (int i=0;i<sides;i++) {float a=i*Mathf.PI*2/sides;bottom[i]=centre+new Vector3(Mathf.Cos(a)*radius,-height*.5f,Mathf.Sin(a)*radius);top[i]=bottom[i]+Vector3.up*height;}
                Face(bottom,material);Face(top.Reverse().ToArray(),material);
                for(int i=0;i<sides;i++){int next=(i+1)%sides;Face(new[]{bottom[i],top[i],top[next],bottom[next]},material);}
            }
        }
    }
}
