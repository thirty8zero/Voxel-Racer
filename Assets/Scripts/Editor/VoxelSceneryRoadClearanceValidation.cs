using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelSceneryRoadClearanceValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Tools/Voxel Racer/Road Horizon/Validate Streaming Road Clearance")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
            var scene = EditorSceneManager.NewPreviewScene();
            var random = UnityEngine.Random.state;
            var source = Resources.Load<VoxelTrackDefinition>("Tracks/Track04");
            int checkedCards = 0, overlaps = 0;
            string first = null;
            try
            {
                foreach (int seed in new[] { 100, 173, 7, 19 })
                {
                    var root = new GameObject("Temporary Track04 clearance QA"); root.SetActive(false);
                    SceneManager.MoveGameObjectToScene(root, scene);
                    try
                    {
                        var road = root.AddComponent<EndlessVoxelRoad>(); road.enabled = false;
                        road.trackDefinition = source; road.SetTuning(source.roadTuning); road.turnSeed = seed;
                        typeof(EndlessVoxelRoad).GetField("turnRandom", Private).SetValue(road, new System.Random(seed));
                        typeof(EndlessVoxelRoad).GetMethod("AppendSegment", Private).Invoke(road, new object[] { false });
                        var scenery = root.AddComponent<VoxelDistantScenery>(); scenery.enabled = false;
                        var camera = new GameObject("Clearance Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                        camera.enabled = false; camera.farClipPlane = 1000; camera.fieldOfView = 58; camera.aspect = 16f / 9;
                        var player = new GameObject("Clearance anchor").AddComponent<VoxelCarController>();
                        player.transform.SetParent(root.transform); player.enabled = false; road.SetTarget(player.transform);
                        // A full independent road reference extends beyond the cache's
                        // original capture range, including road encountered later.
                        var reference = new List<Vector3>();
                        for (float distance = -3000; distance <= 6000; distance += 2f) reference.Add(road.Evaluate(distance).position);
                        reference.Sort((a, b) => a.z.CompareTo(b.z));
                        var batches = typeof(VoxelDistantScenery).GetField("horizonBatches", Private);
                        for (int step = 0; step <= 20; step++)
                        {
                            float distance = step * 96f;
                            player.SetTrack(road, distance);
                            var pose = road.Evaluate(distance);
                            camera.transform.position = pose.position - pose.forward * 11f - pose.right * 8.5f + Vector3.up * 11f;
                            camera.transform.LookAt(pose.position + pose.forward * 30f);
                            scenery.Prepare(camera);
                            foreach (var batch in (Dictionary<Mesh, List<Matrix4x4>>)batches.GetValue(scenery))
                            foreach (var matrix in batch.Value)
                            {
                                Vector3 position = matrix.GetColumn(3);
                                checkedCards++;
                                float footprint = batch.Key.bounds.extents.magnitude * ((Vector3)matrix.GetColumn(0)).magnitude;
                                float clearance = road.roadWidth * .5f + source.scenerySet.groundCover.roadClearance + footprint;
                                if (!NearRoad(position, clearance, reference)) continue;
                                overlaps++;
                                first ??= "seed=" + seed + ", focus=" + distance + ", plant=" + position;
                            }
                        }
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                Directory.CreateDirectory("Temp/RoadSceneryClearance");
                string report = "Checked " + checkedCards + " visible card footprints across Track04 seeds 100/173/7/19 and 1,920m of streaming per seed; road-clearance violations=" + overlaps + (first != null ? ". First: " + first : ".");
                File.WriteAllText("Temp/RoadSceneryClearance/Validation.txt", (overlaps == 0 ? "PASS: " : "FAIL: ") + report);
                if (overlaps != 0) throw new InvalidOperationException(report);
                Debug.Log("PASS: " + report);
            }
            finally { UnityEngine.Random.state = random; EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static bool NearRoad(Vector3 position, float clearance, List<Vector3> points)
        {
            int low = 0, high = points.Count;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (points[middle].z < position.z - clearance) low = middle + 1; else high = middle;
            }
            for (int i = low; i < points.Count && points[i].z <= position.z + clearance; i++)
            {
                Vector3 delta = points[i] - position; delta.y = 0;
                if (delta.sqrMagnitude < clearance * clearance) return true;
            }
            return false;
        }
    }
}
