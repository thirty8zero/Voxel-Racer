using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelHorizonRoadValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Call(object owner, string name) => owner.GetType().GetMethod(name, Private).Invoke(owner, null);

        [MenuItem("Tools/Voxel Racer/Road Horizon/Validate")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run road horizon validation in Edit Mode.");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary road horizon QA"); SceneManager.MoveGameObjectToScene(root, scene);
            var random = UnityEngine.Random.state;
            string[] names = { "roadMaterial", "shoulderMaterial", "lineMaterial", "groundMaterial" };
            var fields = names.Select(n => typeof(VoxelRacerBootstrap).GetField(n, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
            var saved = fields.Select(f => f.GetValue(null)).ToArray();
            var materials = names.Select(n => new Material(Shader.Find(n == "roadMaterial" ? "Voxel Racer/Road Asphalt" : "Universal Render Pipeline/Lit"))).ToArray();
            materials[1].color = Color.white;
            int scenarios = 0;
            try
            {
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, materials[i]);
                foreach (var track in VoxelTrackSequence.Load().tracks)
                {
                    string before = EditorJsonUtility.ToJson(track.roadTuning);
                    CheckRoad(root.transform, track.roadTuning);
                    Check(before == EditorJsonUtility.ToJson(track.roadTuning), "Validation changed authored road settings.");
                    scenarios++;
                }
                var tight = ScriptableObject.CreateInstance<VoxelRoadTuning>();
                try
                {
                    tight.turnChancePerSegment = 1; tight.minimumTurnAngle = tight.maximumTurnAngle = 45;
                    tight.minimumStraightSegmentsBetweenTurns = 0; tight.maximumTrackHeading = 90;
                    tight.curveDegreesPerSlice = 15; tight.roadWidth = 20;
                    CheckRoad(root.transform, tight); scenarios++;
                }
                finally { Object.DestroyImmediate(tight); }
                foreach (var shader in new[] { "Voxel Racer/Mountain Card", "Voxel Racer/Horizon Sun", "Voxel Racer/Horizon Road" })
                    Check(!ShaderUtil.ShaderHasError(Shader.Find(shader)), "Horizon shader has errors: " + shader);
                Directory.CreateDirectory("Temp/RoadHorizon");
                string report = $"PASS (Edit Mode): {scenarios} authored/tight-curved road cases; continuation on actual path; " +
                    "matching road/shoulder edges at handoff; detailed geometry/collider count unchanged; one distant renderer/three " +
                    "submeshes, no colliders or shadow casting; normal/UV/triangle budget; no gameplay RNG consumed; zero warmed " +
                    "refresh allocations; cached mesh reused across handoffs; enable/disable; preview clone ownership; seed rebuild " +
                    "and native mesh cleanup; horizon shaders compile. Source settings, material caches and RNG restored.";
                File.WriteAllText("Temp/RoadHorizon/Validation.txt", report); Debug.Log(report);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory("Temp/RoadHorizon"); File.WriteAllText("Temp/RoadHorizon/Validation.txt", "FAIL: " + e); throw;
            }
            finally
            {
                Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]);
                foreach (var material in materials) Object.DestroyImmediate(material);
                UnityEngine.Random.state = random;
            }
        }

        private static void CheckRoad(Transform root, VoxelRoadTuning tuning)
        {
            var go = new GameObject("Road case"); go.transform.SetParent(root, false);
            try
            {
                var road = go.AddComponent<EndlessVoxelRoad>(); road.enabled = false;
                tuning.ApplyTo(road); road.minimumCactiPerSegment = road.maximumCactiPerSegment = 0;
                road.randomizeTurnSeedEachRun = false; road.horizonRoadEnabled = false;
                road.BuildInitialRoad();
                var detailed = road.GetComponentsInChildren<VoxelRoadStripMesh>().Select(s => s.GetComponent<MeshFilter>().sharedMesh).ToArray();
                int colliders = road.GetComponentsInChildren<Collider>().Length;
                var distances = new[] { 0f, 100f, 195f, 350f, 600f, 900f };
                var poses = distances.Select(road.Evaluate).ToArray();
                road.horizonRoadEnabled = true;
                var refresh = (Action)Delegate.CreateDelegate(typeof(Action), road, typeof(EndlessVoxelRoad).GetMethod("RefreshHorizonRoad", Private));
                var random = UnityEngine.Random.state; refresh();
                Check(random.Equals(UnityEngine.Random.state), "Distant road consumed gameplay randomness.");
                for (int i = 0; i < distances.Length; i++)
                    Check(Vector3.Distance(road.Evaluate(distances[i]).position, poses[i].position) < .0001f,
                        "Enabling distant road changed the generated route.");
                var distant = go.transform.Find("Distant Road");
                var mesh = distant.GetComponent<MeshFilter>().sharedMesh;
                var ownedMaterials = distant.GetComponent<MeshRenderer>().sharedMaterials;
                Check(ownedMaterials[0].shader.name == "Voxel Racer/Horizon Road" &&
                    Mathf.Approximately(ownedMaterials[0].GetFloat("_HorizonFadeEnd"), road.horizonRoadDistance * .94f) &&
                    ownedMaterials[1].shader.name == "Universal Render Pipeline/Lit" &&
                    ownedMaterials[2].shader.name == "Universal Render Pipeline/Lit",
                    "Asphalt fade or original white/yellow marking shaders are missing.");
                Check(distant.GetComponentsInChildren<MeshRenderer>().Length == 1 && mesh.subMeshCount == 3,
                    "Distant road must have one renderer with three material batches.");
                Check(distant.GetComponentsInChildren<Collider>().Length == 0 && road.GetComponentsInChildren<Collider>().Length == colliders,
                    "Distant road added physics geometry.");
                Check(distant.GetComponent<MeshRenderer>().shadowCastingMode == ShadowCastingMode.Off &&
                    !distant.GetComponent<MeshRenderer>().receiveShadows, "Distant road adds shadow work.");
                Check(mesh.vertexCount < 8000 && mesh.triangles.Length / 3 < 5000 && mesh.normals.All(n => n.y > .999f),
                    "Distant road exceeds geometry budget or has incorrect winding.");
                Check(detailed.SequenceEqual(road.GetComponentsInChildren<VoxelRoadStripMesh>().Select(s => s.GetComponent<MeshFilter>().sharedMesh)),
                    "Distant road changed detailed road meshes.");
                CheckSeam(road);
                refresh(); refresh();
                long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) refresh();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
                Check(allocated == 0, "Stationary cached refresh allocated " + allocated + " bytes.");
                road.horizonRoadEnabled = false; refresh(); Check(!distant.gameObject.activeSelf, "Disable switch ignored.");
                road.horizonRoadEnabled = true; refresh(); Check(distant.gameObject.activeSelf, "Re-enable failed.");
                Check(distant.GetComponent<MeshFilter>().sharedMesh == mesh, "Toggle replaced the reusable mesh.");
                var clone = Object.Instantiate(go, root);
                Call(clone.GetComponent<EndlessVoxelRoad>(), "RefreshHorizonRoad");
                Object.DestroyImmediate(clone);
                Check(mesh != null, "Destroying a preview clone released the source mesh.");

                var carObject = new GameObject("Advance anchor"); carObject.transform.SetParent(go.transform);
                var car = carObject.AddComponent<VoxelCarController>(); car.enabled = false;
                car.SetTrack(road, 0); road.SetTarget(car.transform);
                var visible = Field<IList>(road, "visibleSegments");
                var path = Field<IList>(road, "pathSegments");
                var type = path[0].GetType();
                var create = typeof(EndlessVoxelRoad).GetMethod("CreateSegmentVisual", Private);
                for (int step = 1; step <= 8; step++)
                {
                    var old = visible[0]; visible.RemoveAt(0);
                    Object.DestroyImmediate(((Transform)type.GetField("visual").GetValue(old)).gameObject);
                    int nextIndex = (int)type.GetField("index").GetValue(visible[visible.Count - 1]) + 1;
                    var next = path[nextIndex];
                    type.GetField("visual").SetValue(next, create.Invoke(road, new[] { next })); visible.Add(next);
                    car.SetTrack(road, step * road.segmentLength + 1);
                    refresh(); CheckSeam(road);
                    Check(distant.GetComponent<MeshFilter>().sharedMesh == mesh, "Chunk handoff allocated a replacement native mesh.");
                }
                road.turnSeed++; Call(road, "RebuildRoadVisuals"); CheckSeam(road);
                Check(distant.GetComponent<MeshFilter>().sharedMesh == mesh, "Seed rebuild leaked the reusable horizon mesh.");
                Object.DestroyImmediate(go);
                Check(mesh == null, "Road destruction leaked its owned distant mesh.");
                Check(ownedMaterials.All(m => m == null), "Road destruction leaked its owned fade materials.");
            }
            finally { if (go != null) Object.DestroyImmediate(go); }
        }

        private static void CheckSeam(EndlessVoxelRoad road)
        {
            var visible = Field<IList>(road, "visibleSegments");
            var last = visible[visible.Count - 1];
            float distance = (float)last.GetType().GetField("startDistance").GetValue(last) + road.segmentLength;
            var pose = road.Evaluate(distance);
            var tail = road.transform.Find("Distant Road");
            var vertices = tail.GetComponent<MeshFilter>().sharedMesh.vertices;
            foreach (float offset in new[] { -road.roadWidth * .5f, road.roadWidth * .5f })
            {
                var expected = pose.position + pose.right * offset - Vector3.up * .025f;
                Check(vertices.Any(v => Vector3.Distance(tail.TransformPoint(v), expected) < .002f),
                    "Distant road does not meet the actual detailed-road boundary on a bend.");
            }
            var target = Field<VoxelCarController>(road, "targetController");
            Check(Field<float>(road, "horizonEnd") >= (target != null ? target.TrackDistance : 0f) + road.horizonRoadDistance,
                "Horizon coverage fell short after a chunk handoff.");
        }
    }
}
