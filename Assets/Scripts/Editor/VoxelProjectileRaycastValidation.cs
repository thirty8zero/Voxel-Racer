using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace VoxelRacer.Editor
{
    public static class VoxelProjectileRaycastValidation
    {
        private delegate int Query(Vector3 origin, Vector3 direction, float distance);
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Voxel Racer/Validate Bullet Raycast Allocations")]
        public static void Run()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Run bullet raycast validation from Edit Mode.");
            var bufferField = typeof(VoxelProjectile).GetField("projectileHits", Static);
            var query = (Query)Delegate.CreateDelegate(typeof(Query), typeof(VoxelProjectile).GetMethod("RaycastProjectileSegment", Static));
            var savedBuffer = bufferField.GetValue(null);
            bool savedTriggers = Physics.queriesHitTriggers;
            var activeScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var origin = new Vector3(10000, 10000, 10000);
            try
            {
                // An additive QA scene uses the normal Physics query path without
                // touching objects in the user's scenes or persistent tuning.
                var root = new GameObject("Temporary bullet raycast QA");
                SceneManager.MoveGameObjectToScene(root, scene);
                bufferField.SetValue(null, new RaycastHit[64]);
                var colliders = new BoxCollider[129];
                for (int i = 0; i < colliders.Length; i++)
                {
                    var go = new GameObject("Ray target " + i);
                    go.transform.SetParent(root.transform);
                    go.transform.position = origin + Vector3.forward * (2f + i * .12f);
                    colliders[i] = go.AddComponent<BoxCollider>();
                    colliders[i].size = Vector3.one * .05f;
                    go.SetActive(i < 64);
                }
                Physics.SyncTransforms();

                void Compare(Vector3 start, Vector3 direction, float distance, int expectedCount)
                {
                    var oldHits = Physics.RaycastAll(start, direction, distance);
                    int count = query(start, direction, distance);
                    var buffer = (RaycastHit[])bufferField.GetValue(null);
                    Check(count == oldHits.Length && count == expectedCount, "Raycast hit coverage changed.");
                    var oldSorted = oldHits.OrderBy(h => h.collider.GetInstanceID()).ToArray();
                    var newSorted = buffer.Take(count).OrderBy(h => h.collider.GetInstanceID()).ToArray();
                    for (int i = 0; i < count; i++)
                        Check(oldSorted[i].collider == newSorted[i].collider &&
                            Mathf.Abs(oldSorted[i].distance - newSorted[i].distance) < .0001f &&
                            Vector3.Distance(oldSorted[i].point, newSorted[i].point) < .0001f,
                            "Raycast collider, distance or impact point changed.");
                    if (count > 0)
                        Check(oldHits.OrderBy(h => h.distance).First().collider == buffer.Take(count).OrderBy(h => h.distance).First().collider,
                            "Buffer saturation changed the closest obstruction.");
                }

                Compare(origin, Vector3.forward, 30, 64); // Exactly full must also retry.
                Check(((RaycastHit[])bufferField.GetValue(null)).Length > 64, "Exact-capacity query did not grow/retry.");
                foreach (var collider in colliders) collider.gameObject.SetActive(true);
                Physics.SyncTransforms();
                Compare(origin, Vector3.forward, 30, 129); // Multiple buffer expansions.
                Compare(origin, Vector3.forward, 2.5f, 5); // Finite shot segment.
                Compare(origin + Vector3.right * 20, Vector3.forward, 30, 0); // Ignore stale entries.

                for (int i = 8; i < colliders.Length; i++) colliders[i].gameObject.SetActive(false);
                colliders[0].isTrigger = true;
                colliders[1].gameObject.layer = 2; // Ignore Raycast remains excluded.
                Physics.SyncTransforms();
                Physics.queriesHitTriggers = true;
                Compare(origin, Vector3.forward, 30, 7);
                Physics.queriesHitTriggers = false;
                Compare(origin, Vector3.forward, 30, 6);
                Compare(origin + Vector3.forward * 4, Vector3.back, 5, 6);
                colliders[2].enabled = false;
                Physics.SyncTransforms();
                Compare(origin, Vector3.forward, 30, 5);
                colliders[2].enabled = true;
                Physics.SyncTransforms();

                const int iterations = 10000;
                for (int i = 0; i < 100; i++)
                { query(origin, Vector3.forward, 30); Physics.RaycastAll(origin, Vector3.forward, 30); }
                var watch = new Stopwatch();
                // Measure GC.Alloc event counts, matching Unity's installed
                // performance-test package. The marker Value is nanoseconds,
                // not bytes; the Mono byte counter misses these native-created arrays.
                using var allocations = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                    ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame |
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                Check(allocations.Valid, "Unity GC.Alloc recorder is unavailable.");
                int oldTotal = 0, newTotal = 0;
                watch.Start();
                allocations.Start();
                for (int i = 0; i < iterations; i++) oldTotal += Physics.RaycastAll(origin, Vector3.forward, 30).Length;
                allocations.Stop();
                long oldAllocations = allocations.Count > 0 ? allocations.GetSample(0).Count : 0;
                watch.Stop(); double oldMs = watch.Elapsed.TotalMilliseconds;
                allocations.Reset();
                watch.Restart();
                allocations.Start();
                for (int i = 0; i < iterations; i++) newTotal += query(origin, Vector3.forward, 30);
                allocations.Stop();
                long newAllocations = allocations.Count > 0 ? allocations.GetSample(0).Count : 0;
                watch.Stop(); double newMs = watch.Elapsed.TotalMilliseconds;
                Check(oldTotal == newTotal && newTotal == 6 * iterations, "Allocation comparison queried different targets.");
                Check(oldAllocations == iterations && newAllocations == 0, "Warmed bullet physics query allocation events: old=" + oldAllocations + ", new=" + newAllocations + ".");
                Directory.CreateDirectory("Temp");
                string report = "PASS (Edit Mode): RaycastAll parity for exact/full/overflow buffers, closest obstruction, collider IDs, impact points/distances, finite segments, empty results after dense hits, reverse direction, global trigger policy, Ignore Raycast layer and disabled colliders.\n" +
                    iterations + " warmed six-hit physics queries: RaycastAll " + oldAllocations + " managed allocation events; reusable buffer " + newAllocations + " allocation events (Unity GC.Alloc recorder).\n" +
                    "Editor query timings: RaycastAll " + oldMs.ToString("F2") + " ms; reusable buffer " + newMs.ToString("F2") + " ms. These isolated timings are not a device/game FPS benchmark.\n" +
                    "User scenes, global trigger policy and prior shared buffer restored; no source assets changed.";
                File.WriteAllText("Temp/BulletRaycastValidation.txt", report);
                Debug.Log(report);
            }
            finally
            {
                Physics.queriesHitTriggers = savedTriggers;
                bufferField.SetValue(null, savedBuffer);
                EditorSceneManager.CloseScene(scene, true);
                if (activeScene.IsValid() && activeScene.isLoaded) SceneManager.SetActiveScene(activeScene);
            }
        }
    }
}
