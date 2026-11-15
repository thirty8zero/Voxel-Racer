using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelHorizonRoadPlayValidation
    {
        private const string Pending = "VoxelRacer.RoadHorizonPlayCheck";
        private static GameObject root;
        private static EndlessVoxelRoad road;
        private static VoxelCarController car;
        private static VoxelRoadTuning tuning;
        private static VoxelTrackDefinition creekTrack;
        private static Mesh creekGround, firstCreek;
        private static MeshRenderer revealCover;
        private static Mesh revealMesh;
        private static bool sawPartialReveal;
        private static Mesh mesh;
        private static int frames, phase;
        private static float started, phaseStarted, frozenEnd, savedScale;
        private static bool savedBackground;
        private static Bounds frozenBounds;
        private static UnityEngine.Random.State random;
        private static float End() => (float)typeof(EndlessVoxelRoad).GetField("horizonEnd", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(road);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        [InitializeOnLoadMethod]
        private static void Hook()
        { EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("Tools/Voxel Racer/Road Horizon/Validate in Play Mode")]
        public static void Run()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Start from Edit Mode.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            { EditorApplication.update -= Tick; SessionState.SetBool(Pending, false); }
        }
        private static void Setup()
        {
            random = UnityEngine.Random.state; savedBackground = Application.runInBackground; savedScale = Time.timeScale;
            Application.runInBackground = true; Time.timeScale = 1;
            try
            {
                root = new GameObject("Temporary live road horizon QA");
                road = root.AddComponent<EndlessVoxelRoad>();
                tuning = Object.Instantiate(Resources.Load<VoxelTrackDefinition>("Tracks/Track01").roadTuning);
                creekTrack = ScriptableObject.CreateInstance<VoxelTrackDefinition>();
                creekTrack.creekCrossingChancePercent = 100f;
                road.trackDefinition = creekTrack;
                road.SetTuning(tuning); road.minimumCactiPerSegment = road.maximumCactiPerSegment = 0;
                road.randomizeTurnSeedEachRun = false; road.turnSeed = 173; road.horizonRoadEnabled = true;
                road.BuildInitialRoad();
                car = new GameObject("Advance anchor").AddComponent<VoxelCarController>();
                car.transform.SetParent(root.transform); car.enabled = false; car.SetTrack(road, 0); road.SetTarget(car.transform);
                road.GetComponent<VoxelDistantScenery>().enabled = false;
                mesh = road.transform.Find("Distant Road").GetComponent<MeshFilter>().sharedMesh;
                creekGround = road.transform.Find("Continuous Brown Ground").GetComponent<MeshFilter>().sharedMesh;
                firstCreek = road.GetComponentsInChildren<MeshFilter>().FirstCreekMesh();
                Check(firstCreek != null && creekGround.name == "Ground with Creek Cutouts",
                    "The initial straight sections did not create creek meshes and cutouts.");
                var groundRenderer = road.transform.Find("Continuous Brown Ground").GetComponent<MeshRenderer>();
                foreach (var renderer in road.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.name == "Ground Around Creek")
                        Check(renderer.sharedMaterial != null && renderer.sharedMaterial == groundRenderer.sharedMaterial,
                            "Creek terrain entered the road's fade pass instead of using the same opaque material as the ground.");
                started = Time.realtimeSinceStartup; frames = phase = 0;
                revealCover = null; revealMesh = null; sawPartialReveal = false;
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                Check(Time.realtimeSinceStartup - started < 15, "Live road horizon test timed out.");
                CheckCreekSpacing();
                if (phase == 0)
                {
                    if (revealCover == null && revealMesh == null && !sawPartialReveal)
                    {
                        revealCover = Array.Find(road.GetComponentsInChildren<MeshRenderer>(), renderer => renderer.name == "Creek Reveal Ground");
                        if (revealCover != null) revealMesh = revealCover.GetComponent<MeshFilter>().sharedMesh;
                    }
                    if (revealCover != null)
                    {
                        var properties = new MaterialPropertyBlock();
                        revealCover.GetPropertyBlock(properties);
                        float progress = properties.GetFloat("_CreekReveal");
                        var ground = road.transform.Find("Continuous Brown Ground").GetComponent<MeshRenderer>();
                        var features = revealCover.transform.parent.Find("Creek Banks, Water and Fences").GetComponent<MeshRenderer>();
                        features.GetPropertyBlock(properties);
                        Check(revealCover.sharedMaterial == ground.sharedMaterial &&
                            Mathf.Approximately(progress, properties.GetFloat("_CreekReveal")),
                            "Creek and temporary terrain must use the same reveal progress.");
                        sawPartialReveal |= progress > 0f && progress < 1f;
                    }
                    if (++frames < 45) { car.SetTrack(road, frames * 8f); return; }
                    var distant = road.transform.Find("Distant Road");
                    Check(distant.GetComponent<MeshFilter>().sharedMesh == mesh && distant.GetComponentsInChildren<Collider>().Length == 0,
                        "Streaming replaced the native mesh or added distant colliders.");
                    Check(End() > 800, "Real road Update did not move the horizon coverage through chunk recycling.");
                    Check(firstCreek == null && road.transform.Find("Continuous Brown Ground").GetComponent<MeshFilter>().sharedMesh == creekGround &&
                        road.GetComponentsInChildren<MeshFilter>().FirstCreekMesh() != null,
                        "Creek meshes did not recycle with their sections or the ground mesh was replaced.");
                    road.PreserveRoadForFinish(); frozenEnd = End(); frozenBounds = mesh.bounds;
                    car.SetTrack(road, car.TrackDistance + 150);
                    phaseStarted = Time.realtimeSinceStartup; phase = 1;
                }
                else if (phase == 1 && Time.realtimeSinceStartup - phaseStarted > 1.8f)
                {
                    Check(End() == frozenEnd && mesh.bounds == frozenBounds, "Finish preservation kept streaming or changed the distant mesh.");
                    Check(sawPartialReveal && revealCover == null && revealMesh == null &&
                        Array.TrueForAll(road.GetComponentsInChildren<MeshRenderer>(), renderer => renderer.name != "Creek Reveal Ground"),
                        "Streamed creeks did not ease in and release the temporary ground.");
                    Object.Destroy(root); root = null; phase = 2;
                }
                else if (phase == 2 && mesh == null && creekGround == null)
                    Finish("PASS (Play Mode): actual Update-driven road chunk recycling over 350m; one reusable distant mesh; " +
                        "horizon coverage advances; no distant colliders; finish preservation freezes the extension; " +
                        "creeks ease in with matching terrain coverage, recycle with their sections and reuse the cutout ground mesh; native meshes released after deferred destruction. Temporary objects/cloned tuning removed; " +
                        "clock/background/RNG restored, original editor scene retained.");
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void Finish(string report)
        {
            Directory.CreateDirectory("Temp/RoadHorizon"); File.WriteAllText("Temp/RoadHorizon/PlayValidation.txt", report);
            EditorApplication.update -= Tick;
            if (root != null) Object.Destroy(root);
            if (tuning != null) Object.Destroy(tuning);
            if (creekTrack != null) Object.Destroy(creekTrack);
            Application.runInBackground = savedBackground; Time.timeScale = savedScale; UnityEngine.Random.state = random;
            Debug.Log(report); EditorApplication.isPlaying = false;
        }

        private static Mesh FirstCreekMesh(this MeshFilter[] filters)
        {
            foreach (var filter in filters)
                if (filter.name == "Creek Banks, Water and Fences")
                    return filter.sharedMesh;
            return null;
        }

        private static void CheckCreekSpacing()
        {
            var sections = (System.Collections.IList)typeof(EndlessVoxelRoad)
                .GetField("pathSegments", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(road);
            var hasCreek = sections[0].GetType().GetField("hasCreek");
            int previous = -6;
            for (int i = 0; i < sections.Count; i++)
            {
                if ((bool?)hasCreek.GetValue(sections[i]) != true)
                    continue;
                Check(i - previous >= 6, "A streamed crossing broke the five-section gap.");
                previous = i;
            }
        }
    }
}
