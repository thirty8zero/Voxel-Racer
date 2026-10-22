using System;
using System.Collections;
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
    public static class VoxelHorizonSceneryValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        [MenuItem("Tools/Voxel Racer/Road Horizon/Validate Ground Cover")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run in Edit Mode.");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary horizon ground cover QA"); root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var random = UnityEngine.Random.state;
            var source = Resources.Load<VoxelTrackDefinition>("Tracks/Track01");
            var track = Object.Instantiate(source);
            var set = Object.Instantiate(source.scenerySet);
            var foliage = Object.Instantiate(set.groundCover);
            string sourceJson = EditorJsonUtility.ToJson(source.scenerySet.groundCover);
            try
            {
                track.scenerySet = set; set.groundCover = foliage;
                foliage.horizonGroundCoverEnabled = true; foliage.horizonClumpsPerTile = 48;
                var road = root.AddComponent<EndlessVoxelRoad>(); road.enabled = false;
                road.trackDefinition = track; road.roadWidth = 16; road.groundWidth = 700;
                road.horizonRoadDistance = 760; road.turnChancePerSegment = .6f;
                typeof(EndlessVoxelRoad).GetField("turnRandom", Private).SetValue(road, new System.Random(173));
                typeof(EndlessVoxelRoad).GetMethod("AppendSegment", Private).Invoke(road, new object[] { false });
                var scenery = root.AddComponent<VoxelDistantScenery>(); scenery.enabled = false;
                var camera = new GameObject("Coverage camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.enabled = false; camera.farClipPlane = 1000; camera.fieldOfView = 58; camera.aspect = 16f / 9;
                camera.transform.position = new Vector3(-8.5f, 11, -11); camera.transform.LookAt(new Vector3(0, .3f, 14));
                var player = new GameObject("Streaming anchor").AddComponent<VoxelCarController>();
                player.transform.SetParent(root.transform); player.enabled = false; player.SetTrack(road, 0); road.SetTarget(player.transform);
                var rng = UnityEngine.Random.state;
                scenery.Prepare(camera);
                Check(rng.Equals(UnityEngine.Random.state), "Horizon scenery consumed gameplay RNG.");
                int cached = scenery.CachedHorizonClumpCount, visible = scenery.VisibleHorizonClumpCount;
                Check(cached > 1000 && cached < 60000 && visible > 100 && visible < cached, "Missing, unbounded or uncullable horizon cards.");
                Check(scenery.VisibleHorizonBatchCount <= foliage.variants.Length, "Ground cover is not grouped by shared mesh/atlas.");
                Check(root.GetComponentsInChildren<Collider>(true).Length <= 1 && root.GetComponentsInChildren<MeshFilter>(true).Length <= 1,
                    "Billboard extension created per-clump objects, meshes or colliders.");
                var batches = (Dictionary<Mesh, List<Matrix4x4>>)typeof(VoxelDistantScenery).GetField("horizonBatches", Private).GetValue(scenery);
                int farVisible = 0;
                foreach (var batch in batches.Values) foreach (var matrix in batch)
                {
                    Vector3 position = matrix.GetColumn(3);
                    if (Vector3.Distance(camera.transform.position, position) > 320) farVisible++;
                    float closest = float.PositiveInfinity;
                    for (float distance = -800; distance < 850; distance += 4)
                    { var delta = road.Evaluate(distance).position - position; delta.y = 0; closest = Mathf.Min(closest, delta.sqrMagnitude); }
                    Check(closest > Mathf.Pow(road.roadWidth * .5f + foliage.roadClearance, 2), "Horizon grass overlaps the curved road.");
                }
                Check(farVisible > 100, "No grass/bush cards extend beyond the old 320m scenery limit.");
                int closeCount = scenery.CachedPropCount;
                foliage.horizonGroundCoverEnabled = false; scenery.Prepare(camera);
                Check(scenery.CachedHorizonClumpCount == 0 && scenery.CachedPropCount == closeCount, "Horizon toggle changed existing scenery.");
                foliage.horizonGroundCoverEnabled = true; scenery.Prepare(camera);
                Check(scenery.CachedHorizonClumpCount == cached && scenery.VisibleHorizonClumpCount == visible, "Rebuilt horizon placement is not deterministic.");
                scenery.Prepare(camera);
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10; i++) scenery.Prepare(camera);
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Check(allocated == 0, "Cached scenery refresh allocated " + allocated + " bytes.");
                set.distantPropsPerTile = 0; scenery.Prepare(camera);
                Check(scenery.VisibleHorizonClumpCount > 0, "Turning off 3D props also disabled lightweight horizon ground cover.");
                for (int step = 1; step <= 6; step++)
                {
                    player.SetTrack(road, step * 128); camera.transform.position = player.transform.position + new Vector3(-8.5f, 11, -11);
                    camera.transform.LookAt(player.transform.position + Vector3.forward * 14); scenery.Prepare(camera);
                    Check(scenery.CachedHorizonClumpCount > 1000 && scenery.CachedHorizonClumpCount < 60000 && scenery.VisibleHorizonClumpCount > 0,
                        "Horizon strip failed to stream within bounded coverage.");
                }
                typeof(VoxelDistantScenery).GetMethod("OnDisable", Private).Invoke(scenery, null);
                Check(scenery.CachedHorizonClumpCount == 0 && scenery.VisibleHorizonClumpCount == 0, "Disabling scenery retained extension data.");
                Check(EditorJsonUtility.ToJson(source.scenerySet.groundCover) == sourceJson, "Validation changed source ground cover.");
                Check(!ShaderUtil.ShaderHasError(foliage.material.shader), "Foliage shader error.");
                var surface = Resources.Load<Material>("Scenery/RoadHorizonSurface");
                Check(surface != null && !ShaderUtil.ShaderHasError(surface.shader), "Missing build-referenced road fade shader.");
                Directory.CreateDirectory("Temp/RoadHorizonScenery");
                string report = $"PASS: {cached} cached four-triangle grass/bush cards, {visible} visible, {farVisible} beyond 320m; shared atlas/mesh instancing; curved-road clearance; no added meshes or physics; unchanged nearby scenery; deterministic toggle/rebuild and isolated RNG; zero warmed refresh allocations; bounded streaming across 768m; disable cleanup; foliage/road-fade shaders compile. Source assets restored. Phone GPU timing is not measured.";
                File.WriteAllText("Temp/RoadHorizonScenery/Validation.txt", report); Debug.Log(report);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(track); Object.DestroyImmediate(set); Object.DestroyImmediate(foliage);
                EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Random.state = random;
            }
        }
    }
}
