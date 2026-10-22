using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelMissileStutterValidation
    {
        [MenuItem("Tools/Voxel Racer/Diagnostics/Measure Missile Query and Headlight CPU")]
        public static void RunAfter() => Run("After");
        public static void RunBefore() => Run("Before");
        public static void RunBeforeImpact() => Run("BeforeImpact");
        private static void Run(string version)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run from Edit Mode.");
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary missile stutter CPU comparison");
            SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = new Vector3(10000, 1000, 10000);
            var track = Object.Instantiate(Resources.Load<VoxelTrackDefinition>("Tracks/Track01")); track.nightTime = true;
            var player = new GameObject("QA player").AddComponent<VoxelCarController>(); player.transform.SetParent(root.transform, false);
            player.enabled = false; player.topSpeed = 0;
            try
            {
                foreach (int i in Enumerable.Range(0, 8))
                {
                    var go = new GameObject("Authored missile target " + i); go.transform.SetParent(root.transform, false);
                    var enemy = go.AddComponent<VoxelEnemyCar>(); enemy.enabled = false;
                    var tuning = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/" + (i % 2 == 0 ? "BlackInterceptorTuning" : "TrafficCarTuning"));
                    enemy.Configure(player, VoxelObstacleCarTuning.Load(), tuning, null, 30 + i * 12, 0);
                    go.transform.localPosition = new Vector3((i % 3 - 1) * 5, 0, 30 + i * 12);
                    VoxelNightHeadlights.Attach(go, track);
                }
                var targets = root.GetComponentsInChildren<VoxelMissileTarget>();
                var ready = typeof(VoxelMissileTarget).GetField("ready", BindingFlags.Instance | BindingFlags.NonPublic);
                var visuals = root.GetComponentsInChildren<VoxelEnemyCar>().Select(c => c.transform.GetChild(0)).ToArray();
                var camera = new GameObject("QA camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform, false);
                camera.enabled = false; camera.transform.localPosition = new Vector3(-7, 6, -12);
                camera.transform.LookAt(root.transform.position + Vector3.forward * 40);
                Vector3 start = root.transform.position + Vector3.up * .65f;
                void Query() => VoxelMissileTarget.Trace(start, Vector3.forward, .75f, out _, out _, out _, out _, 1.5f);
                var frame = typeof(VoxelMissileTarget).GetField("lastPrepareFrame", BindingFlags.Instance | BindingFlags.NonPublic);
                void Reset()
                {
                    foreach (var target in targets)
                    { ready.SetValue(target, false); frame?.SetValue(target, -1); }
                }
                double Measure(Action action, int count, out long bytes)
                {
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    var timer = Stopwatch.StartNew();
                    for (int i = 0; i < count; i++) action();
                    timer.Stop(); bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                    return timer.Elapsed.TotalMilliseconds / count;
                }
                Query(); Reset();
                double cold = Measure(Query, 1, out long coldBytes);
                double warm = Measure(Query, 200, out long warmBytes);
                var effects = root.GetComponentsInChildren<VoxelNightHeadlights>();
                foreach (var effect in effects) effect.enabled = false;
                double lightsOff = Measure(Query, 200, out _);
                foreach (var effect in effects) effect.enabled = true;
                VoxelNightHeadlights.SubmitDraws(camera, false);
                double headlights = Measure(() => VoxelNightHeadlights.SubmitDraws(camera, false), 200, out long headlightBytes);
                // A newly spawned visual may still be too small to mark the old cache ready.
                visuals[6].localScale = visuals[7].localScale = Vector3.one * .01f; Reset(); Query();
                double growing = Measure(Query, 20, out long growingBytes);
                double growingFrame = Measure(() => { Reset(); Query(); }, 20, out _);
                double unrelatedBlast = Measure(() => VoxelMissileTarget.Blast(start - Vector3.forward * 50,
                    1.8f, 35f, Vector3.forward), 20, out _);
                ValidateEnvelopes(root.transform, targets[0]);
                string report = $"{version}: Editor CPU microbenchmark, eight authored models / {root.GetComponentsInChildren<MeshRenderer>(true).Length} renderers.\n" +
                    $"Cold missile Trace: {cold:F3} ms / {coldBytes} managed bytes.\nWarmed Trace (headlights present): {warm:F3} ms/query / {warmBytes} bytes across 200.\n" +
                    $"Warmed Trace (headlight components disabled): {lightsOff:F3} ms/query.\n" +
                    $"Headlight matrix preparation only: {headlights:F3} ms/update / {headlightBytes} bytes across 200.\n" +
                    $"Trace while two targets grow from 1% scale: {growing:F3} ms/query / {growingBytes} bytes across 20.\n" +
                    $"Forced full envelope refresh (all eight targets, includes reflection invalidation): {growingFrame:F3} ms/query.\n" +
                    $"Explosion well outside all target envelopes: {unrelatedBlast:F3} ms/blast (no debris or damage).\n" +
                    "PASS transformed-envelope corner parity and same-frame zero-to-full boss entrance refresh. " +
                    "Excludes missile/effect instantiation, GPU rendering, shader compilation, debris physics and full scene/frame costs; does not establish the cause of every observed editor stall. " +
                    "The cold figure forces all eight cached memberships to rebuild their bounds after a JIT/membership warmup. Allocation counters are diagnostic only on this editor. No tuning assets or game flow changed.";
                Directory.CreateDirectory("Temp/MissileStutter"); File.WriteAllText("Temp/MissileStutter/" + version + ".txt", report);
                UnityEngine.Debug.Log(report);
            }
            finally
            {
                foreach (var effect in root.GetComponentsInChildren<VoxelNightHeadlights>(true))
                    typeof(VoxelNightHeadlights).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effect, null);
                foreach (var bar in root.GetComponentsInChildren<VoxelEnemyHealthBar>(true))
                    typeof(VoxelEnemyHealthBar).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bar, null);
                EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(track); UnityEngine.Random.state = random;
            }
        }

        private static void ValidateEnvelopes(Transform root, VoxelMissileTarget target)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var prepare = typeof(VoxelMissileTarget).GetMethod("Prepare", flags);
            var ready = typeof(VoxelMissileTarget).GetField("ready", flags);
            var frame = typeof(VoxelMissileTarget).GetField("lastPrepareFrame", flags);
            var boundsField = typeof(VoxelMissileTarget).GetField("localBounds", flags);
            var scale = root.localScale; var rotation = root.rotation;
            try
            {
                root.localScale = new Vector3(1.2f, .7f, 1.8f); root.rotation = Quaternion.Euler(0, 65, 0);
                ready.SetValue(target, false); frame.SetValue(target, -1); prepare.Invoke(target, null);
                var actual = (Bounds)boundsField.GetValue(target);
                bool first = true; Bounds expected = default;
                foreach (var r in target.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.GetComponentInParent<VoxelEnemyHealthBar>() != null) continue;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = r.localBounds.center + Vector3.Scale(r.localBounds.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var point = target.transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                        if (first) { expected = new Bounds(point, Vector3.zero); first = false; } else expected.Encapsulate(point);
                    }
                }
                if ((actual.min - expected.min).magnitude > .01f || (actual.max - expected.max).magnitude > .01f)
                    throw new InvalidOperationException("Fast missile bounds differ from the original transformed-corner envelope.");
            }
            finally { root.localScale = scale; root.rotation = rotation; }
            var tuning = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>();
            try
            {
                var boss = VoxelMissileValidation.MakeTarget(root, tuning, root.position + Vector3.forward * 150);
                var model = new GameObject("Growing boss model").transform; model.SetParent(boss.transform, false);
                foreach (var renderer in boss.GetComponentsInChildren<MeshRenderer>()) renderer.transform.SetParent(model, true);
                var entrance = model.gameObject.AddComponent<VoxelBossEntrance>(); entrance.Configure(1f);
                var growing = boss.GetComponent<VoxelMissileTarget>();
                prepare.Invoke(growing, null);
                var initial = (Bounds)boundsField.GetValue(growing);
                VoxelMissileValidation.Call(entrance, "Advance", .5f); prepare.Invoke(growing, null);
                var half = (Bounds)boundsField.GetValue(growing);
                if (half.size.sqrMagnitude <= initial.size.sqrMagnitude || (bool)ready.GetValue(growing))
                    throw new InvalidOperationException("Growing boss envelope was frozen or finalized early.");
                VoxelMissileValidation.Call(entrance, "Advance", .5f); prepare.Invoke(growing, null);
                if (!((Bounds)boundsField.GetValue(growing)).Contains(boss.transform.InverseTransformPoint(model.TransformPoint(new Vector3(2, 2, 2)))) || !(bool)ready.GetValue(growing))
                    throw new InvalidOperationException("Completed boss entrance did not refresh/finalize its envelope.");
                Object.DestroyImmediate(boss.gameObject);
            }
            finally { Object.DestroyImmediate(tuning); }
        }
    }
}
