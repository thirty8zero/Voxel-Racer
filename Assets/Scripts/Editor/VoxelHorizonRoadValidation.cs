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
                CheckOpeningBlend(root.transform);
                foreach (var shader in new[] { "Voxel Racer/Mountain Card", "Voxel Racer/Horizon Sun", "Voxel Racer/Horizon Road" })
                    Check(!ShaderUtil.ShaderHasError(Shader.Find(shader)), "Horizon shader has errors: " + shader);
                Directory.CreateDirectory("Temp/RoadHorizon");
                string report = $"PASS (Edit Mode): {scenarios} authored/tight-curved road cases; continuation on actual path; " +
                    "matching road/shoulder edges at handoff; detailed geometry/collider count unchanged; one distant renderer/three " +
                    "submeshes, no colliders or shadow casting; normal/UV/triangle budget; no gameplay RNG consumed; zero warmed " +
                    "refresh allocations; cached mesh reused across handoffs; enable/disable; preview clone ownership; seed rebuild " +
                    "and native mesh cleanup; countdown bend planning, gradual joined strips and final colliders; " +
                    "horizon shaders compile. Source settings, material caches and RNG restored.";
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

        private static void CheckOpeningBlend(Transform root)
        {
            var go = new GameObject("Countdown turn case");
            go.transform.SetParent(root, false);
            try
            {
                var road = go.AddComponent<EndlessVoxelRoad>();
                road.enabled = false;
                road.trackDefinition = Resources.Load<VoxelTrackDefinition>("Tracks/Track01");
                road.trackDefinition.roadTuning.ApplyTo(road);
                road.minimumCactiPerSegment = road.maximumCactiPerSegment = 0;
                road.turnChancePerSegment = 1f;
                road.minimumTurnAngle = road.maximumTurnAngle = 24f;
                Call(road, "BeginStraightOpening");
                road.BuildInitialRoad();

                const float launch = 78f;
                const float firstBend = 135f;
                const float bendSample = 150f;
                var launchPose = road.Evaluate(launch);
                var plan = typeof(EndlessVoxelRoad).GetMethod("PlanOpeningTurns", Private);
                var blend = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), road,
                    typeof(EndlessVoxelRoad).GetMethod("BlendOpeningTurns", Private));
                var refresh = (Action)Delegate.CreateDelegate(typeof(Action), road,
                    typeof(EndlessVoxelRoad).GetMethod("RefreshHorizonRoad", Private));
                var strip = go.GetComponentsInChildren<VoxelRoadStripMesh>()
                    .First(piece => piece.name == "Road" && Mathf.Abs(piece.transform.position.z - firstBend) < .001f);
                var mesh = strip.GetComponent<MeshFilter>().sharedMesh;
                var straight = mesh.vertices;
                var collider = strip.GetComponent<MeshCollider>();
                var horizon = go.transform.Find("Distant Road").GetComponent<MeshFilter>().sharedMesh;
                var scenery = go.AddComponent<VoxelDistantScenery>();
                var cameraObject = new GameObject("Countdown scenery camera");
                cameraObject.transform.SetParent(go.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.farClipPlane = 760f;
                scenery.Prepare(camera);
                var tilesField = typeof(VoxelDistantScenery).GetField("tiles", Private);
                var horizonTilesField = typeof(VoxelDistantScenery).GetField("horizonTiles", Private);
                var activeTiles = tilesField.GetValue(scenery);
                var activeHorizonTiles = horizonTilesField.GetValue(scenery);
                plan.Invoke(road, new object[] { launch, 52f });
                Check(Mathf.Abs(road.Evaluate(bendSample).forward.x) < .001f && mesh.vertices.SequenceEqual(straight),
                    "Planning at 3 changed visible road immediately.");
                var evaluateTarget = (Func<float, VoxelTrackPose>)Delegate.CreateDelegate(typeof(Func<float, VoxelTrackPose>), road,
                    typeof(EndlessVoxelRoad).GetMethod("EvaluateOpeningTarget", Private));
                var sampleDistances = new[] { -50f, 48f, 135f, 150f, 455f, 1700f };
                var plannedPoses = sampleDistances.Select(evaluateTarget).ToArray();
                Check(Vector3.Angle(Vector3.forward, plannedPoses[3].forward) > 11.9f,
                    "Scenery could not sample the final bend before the road began to blend.");

                scenery.Prepare(camera);
                var pendingField = typeof(VoxelDistantScenery).GetField("openingScenery", Private);
                var pending = pendingField.GetValue(scenery);
                Check(pending != null, "Countdown did not start preparing the final scenery.");
                var build = pending.GetType().GetField("Build");
                for (int i = 0; i < 240 && build.GetValue(pending) != null; i++)
                    scenery.Prepare(camera);
                Check(build.GetValue(pending) == null && activeTiles == tilesField.GetValue(scenery) &&
                    activeHorizonTiles == horizonTilesField.GetValue(scenery), "Preparing scenery replaced the active tiles early or failed to complete.");

                float previousAngle = 0f;
                foreach (float progress in new[] { .25f, .5f, .75f, 1f })
                {
                    blend(progress);
                    refresh();
                    float angle = Vector3.Angle(Vector3.forward, road.Evaluate(bendSample).forward);
                    Check(angle > previousAngle + .1f, "Countdown curve did not blend monotonically.");
                    previousAngle = angle;
                    Check(road.Evaluate(launch).position == launchPose.position &&
                        Mathf.Abs(road.Evaluate(firstBend - .01f).forward.x) < .001f,
                        "Countdown moved the road under the car or turned before the launch buffer.");
                    Check(strip.GetComponent<MeshFilter>().sharedMesh == mesh &&
                        !mesh.vertices.SequenceEqual(straight), "Countdown did not deform the live road strip in place.");
                    Check((collider.sharedMesh == mesh) == (progress == 1f), "Countdown cooked road collision before the final shape.");
                    CheckSeam(road);
                }

                var finishedVertices = mesh.vertices;
                var finishedPose = road.Evaluate(bendSample);
                camera.transform.position = Vector3.forward * 52f;
                scenery.Prepare(camera);
                Check(pendingField.GetValue(scenery) == null &&
                    tilesField.GetValue(scenery) == pending.GetType().GetField("Tiles").GetValue(pending) &&
                    horizonTilesField.GetValue(scenery) == pending.GetType().GetField("HorizonTiles").GetValue(pending) &&
                    scenery.CachedPropCount > 0 && scenery.CachedHorizonClumpCount > 0,
                    "The settled road did not reuse both prepared scenery caches.");
                blend(1f);
                refresh();
                Check(Mathf.Abs(previousAngle - 12f) < .01f && mesh.vertices.SequenceEqual(finishedVertices) &&
                    road.Evaluate(bendSample).position == finishedPose.position &&
                    go.transform.Find("Distant Road").GetComponent<MeshFilter>().sharedMesh == horizon,
                    "Final road differed from the authored bend or changed again at handoff.");
                for (int i = 0; i < sampleDistances.Length; i++)
                {
                    var actual = road.Evaluate(sampleDistances[i]);
                    Check(actual.position == plannedPoses[i].position && actual.forward == plannedPoses[i].forward,
                        "The final road did not match the scenery plan, including segments appended during the countdown.");
                }
            }
            finally { Object.DestroyImmediate(go); }
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
