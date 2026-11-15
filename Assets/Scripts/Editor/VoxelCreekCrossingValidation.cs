using System;
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
    public static class VoxelCreekCrossingValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate and Render Creek Crossings")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run from Edit Mode.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var track = Object.Instantiate(Resources.Load<VoxelTrackDefinition>("Tracks/Track01"));
            var random = UnityEngine.Random.state;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var bootstrap = typeof(VoxelRacerBootstrap);
            var fields = bootstrap.GetFields(flags).Where(field => field.FieldType == typeof(Material)).ToArray();
            var saved = fields.Select(field => (Material)field.GetValue(null)).ToArray();
            try
            {
                SceneManager.SetActiveScene(scene);
                bootstrap.GetMethod("CreateMaterials", flags).Invoke(null, new object[] { track });
                track.creekCrossingChancePercent = 0f;
                var dry = Build(track, 0f, 16);
                Check(Crossings(dry).Length == 0, "The 0% setting spawned a creek.");
                var dryRoad = dry.GetComponentsInChildren<VoxelRoadStripMesh>().Select(strip => strip.GetComponent<MeshCollider>().sharedMesh.bounds).ToArray();
                Mesh flatGround = dry.transform.Find("Continuous Brown Ground").GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(dry.gameObject);

                track.creekCrossingChancePercent = 100f;
                var road = Build(track, 0f, 16, firstWaterRoll: .99);
                var material = Resources.Load<Material>("Scenery/CreekSurfaces");
                Check(material != null && material.shader.name == "Voxel Racer/Creek Surfaces" &&
                    !ShaderUtil.ShaderHasError(material.shader), "Creek shader or material did not import.");
                var crossings = Crossings(road);
                Check(crossings.Length == 3, "Expected crossings in sections 4, 10 and 16 at 100%.");
                for (int i = 0; i < crossings.Length; i++)
                    Check(crossings[i].parent.GetSiblingIndex() == 4 + i * 6,
                        "Expected exactly five intervening road sections at 100%.");
                CheckTurretSpawns(track);
                Check(dryRoad.SequenceEqual(road.GetComponentsInChildren<VoxelRoadStripMesh>()
                    .Select(strip => strip.GetComponent<MeshCollider>().sharedMesh.bounds)), "Creeks changed road collider geometry.");
                foreach (var creek in crossings)
                {
                    Check(creek.GetComponentsInChildren<Collider>().Length == 0, "Creek scenery added a collider.");
                    var mesh = creek.Find("Creek Banks, Water and Fences").GetComponent<MeshFilter>().sharedMesh;
                    Check(mesh.bounds.min.y < -.8f && mesh.bounds.max.y > .8f &&
                        mesh.bounds.extents.z < road.segmentLength * .5f, "Creek escaped its section or lacked depth or fencing.");
                    float sceneryEdge = road.groundWidth * .5f;
                    Check(mesh.bounds.min.x <= -sceneryEdge && mesh.bounds.max.x >= sceneryEdge,
                        "The creek stopped short of the scenery edges.");
                }
                var levels = ReadLevels(crossings[0], road.roadWidth);
                var fullChannel = crossings[0].Find("Creek Banks, Water and Fences").GetComponent<MeshFilter>().sharedMesh;
                var fullWater = WaterVertices(fullChannel).ToArray();
                Check(fullWater.Any(vertex => Mathf.Abs(vertex.x - fullChannel.bounds.min.x) < .01f) &&
                    fullWater.Any(vertex => Mathf.Abs(vertex.x - fullChannel.bounds.max.x) < .01f),
                    "A nearly full creek must carry water to both scenery edges.");
                var layout = BankOutline(crossings[0]);
                var positions = crossings.Select(creek => creek.position).ToArray();
                Check(!layout.SequenceEqual(BankOutline(crossings[1])), "Different sections reproduced the same creek layout.");
                typeof(EndlessVoxelRoad).GetMethod("RebuildRoadVisuals", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(road, null);
                crossings = Crossings(road);
                Check(positions.SequenceEqual(crossings.Select(creek => creek.position)) &&
                    ReadLevels(crossings[0], road.roadWidth) == levels, "Rebuilding the same road rerolled its creek.");
                Check(HasGroundCutout(road, crossings[0].position), "Ground still covers the creek centre.");
                CheckTerrainBrightness(road, crossings[0], flatGround);
                Object.DestroyImmediate(road.gameObject);

                var curved = Build(track, 1f);
                Check(Crossings(curved).Length == 0, "A curved segment spawned a creek.");
                Object.DestroyImmediate(curved.gameObject);

                // A new road with the same track seed keeps the layout and rerolls both levels.
                road = Build(track, 0f, 4, true, firstWaterRoll: .45);
                var crossing = Crossings(road).Single();
                var nextLevels = ReadLevels(crossing, road.roadWidth);
                Check(crossing.position == positions[0] && layout.SequenceEqual(BankOutline(crossing)),
                    "The same track seed changed the crossing location or layout.");
                Check(levels.bed != nextLevels.bed && levels.water - nextLevels.water > .2f,
                    "The same track seed must support visibly different water heights.");
                Render(road, crossing.position);
                Object.DestroyImmediate(road.gameObject);
                road = Build(track, 0f, 4, true, firstWaterRoll: .08);
                crossing = Crossings(road).Single();
                Check(crossing.position == positions[0] && layout.SequenceEqual(BankOutline(crossing)) &&
                    float.IsNegativeInfinity(ReadLevels(crossing, road.roadWidth).water) &&
                    HasGroundCutout(road, crossing.position),
                    "A zero fill must keep the creek bed and cutout without generating any water.");
                Object.DestroyImmediate(road.gameObject);
                road = Build(track, 0f, 14, true);
                var pair = Crossings(road);
                Check(pair.Length == 2 && Mathf.Approximately(Vector3.Distance(pair[0].position, pair[1].position), road.segmentLength * 6f),
                    "The preview must have exactly five road sections between the two crossings.");
                Render(road, (pair[0].position + pair[1].position) * .5f, showSpacing: true);
                Debug.Log("PASS: creek odds, spacing, seeded layouts, dry beds, visibly different water heights, stable rebuilds, turret clearance, road colliders, terrain brightness and preview renders.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                SceneManager.SetActiveScene(previous);
                for (int i = 0; i < fields.Length; i++)
                {
                    var current = (Material)fields[i].GetValue(null);
                    if (current != null && !saved.Contains(current) && !EditorUtility.IsPersistent(current))
                        Object.DestroyImmediate(current);
                    fields[i].SetValue(null, saved[i]);
                }
                Object.DestroyImmediate(track);
                UnityEngine.Random.state = random;
            }
        }

        private static EndlessVoxelRoad Build(VoxelTrackDefinition track, float turnChance, int count = 8, bool scenery = false,
            double? firstWaterRoll = null)
        {
            var road = new GameObject("Creek preview road").AddComponent<EndlessVoxelRoad>();
            road.enabled = false;
            road.trackDefinition = track;
            track.roadTuning.ApplyTo(road);
            road.turnChancePerSegment = turnChance;
            road.minimumStraightSegmentsBetweenTurns = 0;
            road.maximumTrackHeading = 180f;
            road.segmentCount = count;
            if (firstWaterRoll.HasValue)
            {
                // Section 4 is the first eligible straight. Use a reproducible run seed for each fixture.
                int seed = 0;
                for (;; seed++)
                {
                    var levels = new System.Random(unchecked(seed ^ 4 * 73856093));
                    levels.NextDouble();
                    if (Math.Abs(levels.NextDouble() - firstWaterRoll.Value) < .005)
                        break;
                }
                typeof(EndlessVoxelRoad).GetField("creekLevelSeed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(road, seed);
            }
            if (!scenery) track.scenerySet = null;
            else track.scenerySet = Resources.Load<VoxelTrackDefinition>("Tracks/Track01").scenerySet;
            road.BuildInitialRoad();
            return road;
        }

        private static Transform[] Crossings(EndlessVoxelRoad road) =>
            road.GetComponentsInChildren<Transform>().Where(child => child.name == "Creek Crossing").ToArray();

        private static (float bed, float water) ReadLevels(Transform crossing, float roadWidth)
        {
            var channel = crossing.Find("Creek Banks, Water and Fences").GetComponent<MeshFilter>().sharedMesh;
            return (channel.vertices.Where(vertex => Mathf.Abs(vertex.x) <= roadWidth * .5f + .56f)
                .Min(vertex => vertex.y), WaterVertices(channel).Select(vertex => vertex.y)
                    .DefaultIfEmpty(float.NegativeInfinity).First());
        }

        private static Vector3[] BankOutline(Transform crossing) =>
            crossing.Find("Ground Around Creek").GetComponent<MeshFilter>().sharedMesh.vertices;

        private static System.Collections.Generic.IEnumerable<Vector3> WaterVertices(Mesh mesh) =>
            mesh.vertices.Zip(mesh.colors, (vertex, color) => (vertex, color))
                .Where(pair => pair.color.a > .5f).Select(pair => pair.vertex);

        private static void CheckTurretSpawns(VoxelTrackDefinition track)
        {
            var road = Build(track, 0f, 3);
            var owner = new GameObject("Creek turret spawn validation");
            try
            {
                var player = owner.AddComponent<VoxelCarController>();
                var tuning = Resources.Load<VoxelRoadsideTurretTuning>("EnemyTurrets/RoadsideTurretTuning");
                var spawn = typeof(VoxelRoadsideTurret).GetMethod("Spawn", BindingFlags.Static | BindingFlags.NonPublic);
                float length = road.segmentLength;
                void SpawnAt(float sectionsFromOrigin, VoxelObstacleSpawner obstacles = null) =>
                    spawn.Invoke(null, new object[] { owner.transform, player, road, tuning, sectionsFromOrigin * length, obstacles });

                Check(Crossings(road).Length == 0, "The turret test must start before any creek streams in.");
                SpawnAt(14f); // Section 16. Resolve this first to exercise out-of-order lookup.
                SpawnAt(2f);  // Section 4.
                SpawnAt(8f);  // Section 10.
                SpawnAt(7.49f);
                SpawnAt(8.5f);

                var obstacles = owner.AddComponent<VoxelObstacleSpawner>();
                var oil = new GameObject("Obstacle in the first dry section").AddComponent<VoxelOilSlickObstacle>();
                oil.transform.SetParent(owner.transform);
                oil.Configure(player, road, Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/OilSlick"),
                    3f * length, 0f, 3f);
                SpawnAt(2f, obstacles);
                SpawnAt(3f, obstacles);
                var distances = owner.GetComponentsInChildren<VoxelRoadsideTurret>().Select(turret => turret.TrackDistance).ToArray();
                float[] expected = { 15f, 3f, 9f, 7.49f, 8.5f, 4f };
                Check(distances.Length == expected.Length &&
                    distances.Zip(expected, (actual, section) => Mathf.Approximately(actual, section * length)).All(match => match) &&
                    Crossings(road).Length == 0,
                    "Turrets must move to the next clear section after a planned creek and still respect obstacle clearance.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(road.gameObject);
            }
        }

        private static void CheckTerrainBrightness(EndlessVoxelRoad road, Transform crossing, Mesh flatGround)
        {
            // Fix the camera and world-space sample points while changing the ground triangulation.
            const int layer = 31;
            var ground = road.transform.Find("Continuous Brown Ground").GetComponent<MeshFilter>();
            var bank = crossing.Find("Ground Around Creek");
            ground.gameObject.layer = bank.gameObject.layer = layer;
            var camera = new GameObject("Terrain fog comparison").AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            var background = new Color(1f, 0f, 1f);
            camera.backgroundColor = background;
            camera.farClipPlane = 1200f;
            camera.transform.position = crossing.position + new Vector3(-34f, 9f, -31f);
            camera.transform.LookAt(crossing.position + Vector3.left * 23f);
            var samples = new[] { new Vector3(-24.13f, -.145f, -7.81f), new Vector3(-24.13f, -.145f, -10.12f),
                new Vector3(-30.13f, -.145f, -13.12f) };
            bool fog = RenderSettings.fog;
            FogMode mode = RenderSettings.fogMode;
            float start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance;
            Color color = RenderSettings.fogColor;
            var cutout = ground.sharedMesh;
            using var target = new RenderTargetScope(camera, 320, 180);
            try
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 12f;
                RenderSettings.fogEndDistance = 90f;
                RenderSettings.fogColor = new Color(.67f, .39f, .25f);

                ground.sharedMesh = flatGround;
                bank.gameObject.SetActive(false);
                target.Capture();
                var reference = samples.Select(offset => target.Sample(crossing.position + offset)).ToArray();
                Check(reference.All(pixel => pixel != background), "The reference terrain did not render at the sample points.");

                ground.sharedMesh = cutout;
                bank.gameObject.SetActive(true);
                target.Capture();
                CompareSamples();

                var refresh = typeof(EndlessVoxelRoad).GetMethod("RefreshCreekGround", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(Vector3) }, null);
                Vector3 position = ground.transform.position;
                refresh.Invoke(road, new object[] { position + new Vector3(32f, 0f, 32f) });
                target.Capture();
                CompareSamples();
                refresh.Invoke(road, new object[] { position });

                // At mid-reveal, the same fixed camera should see both the old terrain and
                // the creek, with no pixels falling through to the camera background.
                var creek = crossing.GetComponent("VoxelCreekCrossing");
                const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                creek.GetType().GetMethod("BeginReveal", privateInstance).Invoke(creek, null);
                var cover = crossing.Find("Creek Reveal Ground").gameObject;
                var channel = crossing.Find("Creek Banks, Water and Fences").gameObject;
                cover.layer = channel.layer = layer;
                var reveal = creek.GetType().GetMethod("ApplyReveal", privateInstance);
                var channelPoint = crossing.position + new Vector3(-24f, -.145f, 0f);
                target.Capture();
                var hidden = target.SamplePatch(channelPoint, 7);
                reveal.Invoke(creek, new object[] { .5f });
                target.Capture();
                var midway = target.SamplePatch(channelPoint, 7);
                reveal.Invoke(creek, new object[] { 1f });
                target.Capture();
                var visible = target.SamplePatch(channelPoint, 7);
                var changed = Enumerable.Range(0, hidden.Length)
                    .Where(i => Vector4.Distance(hidden[i], visible[i]) > .06f).ToArray();
                Check(changed.Length > 20 &&
                    changed.Count(i => Vector4.Distance(hidden[i], midway[i]) < .025f) > changed.Length / 4 &&
                    changed.Count(i => Vector4.Distance(visible[i], midway[i]) < .025f) > changed.Length / 4 &&
                    midway.All(pixel => pixel != background),
                    "The reveal did not blend from solid ground to creek with continuous coverage.");

                void CompareSamples()
                {
                    for (int i = 0; i < samples.Length; i++)
                    {
                        Color actual = target.Sample(crossing.position + samples[i]);
                        Color expected = reference[i];
                        float difference = Mathf.Max(Mathf.Abs(actual.r - expected.r),
                            Mathf.Abs(actual.g - expected.g), Mathf.Abs(actual.b - expected.b));
                        Check(difference <= .025f, $"Terrain brightness shifted at {samples[i]}: {expected} to {actual}.");
                    }
                }
            }
            finally
            {
                ground.sharedMesh = cutout;
                bank.gameObject.SetActive(true);
                RenderSettings.fog = fog;
                RenderSettings.fogMode = mode;
                RenderSettings.fogStartDistance = start;
                RenderSettings.fogEndDistance = end;
                RenderSettings.fogColor = color;
                ground.gameObject.layer = bank.gameObject.layer = 0;
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        private sealed class RenderTargetScope : IDisposable
        {
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly Texture2D image;

            public RenderTargetScope(Camera camera, int width, int height)
            {
                this.camera = camera;
                target = RenderTexture.GetTemporary(width, height, 24);
                camera.targetTexture = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
            }

            public void Capture()
            {
                var previous = RenderTexture.active;
                try
                {
                    // URP updates fog globals on the first render after changing RenderSettings.
                    camera.Render();
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                }
                finally { RenderTexture.active = previous; }
            }

            public Color Sample(Vector3 worldPoint)
            {
                Vector3 point = camera.WorldToScreenPoint(worldPoint);
                return image.GetPixel((int)point.x, (int)point.y);
            }

            public Color[] SamplePatch(Vector3 worldPoint, int radius)
            {
                Vector3 point = camera.WorldToScreenPoint(worldPoint);
                int width = radius * 2 + 1;
                return image.GetPixels((int)point.x - radius, (int)point.y - radius, width, width);
            }

            public void Dispose()
            {
                if (camera != null) camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image);
            }
        }

        private static bool HasGroundCutout(EndlessVoxelRoad road, Vector3 point)
        {
            var ground = road.transform.Find("Continuous Brown Ground");
            var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            Vector3 local = ground.InverseTransformPoint(point);
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                float ab = Vector3.Cross(b - a, local - a).y;
                float bc = Vector3.Cross(c - b, local - b).y;
                float ca = Vector3.Cross(a - c, local - c).y;
                if (ab >= 0f && bc >= 0f && ca >= 0f) return false;
            }
            return true;
        }

        private static void Render(EndlessVoxelRoad road, Vector3 crossing, bool showSpacing = false)
        {
            var camera = new GameObject("Creek preview camera").AddComponent<Camera>();
            camera.transform.position = crossing + (showSpacing ? new Vector3(-115f, 190f, -36f) : new Vector3(-9f, 20f, -31f));
            camera.transform.LookAt(crossing + Vector3.forward * 1f);
            camera.orthographic = showSpacing;
            camera.orthographicSize = 84f;
            camera.fieldOfView = 52f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.70f, .43f, .42f);
            camera.cullingMask = 1 << 31;
            foreach (var child in road.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
            var sun = new GameObject("Creek preview sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            var target = RenderTexture.GetTemporary(1440, 810, 24);
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Temp/CreekCrossings");
                string name = showSpacing ? "Five Sections Apart" : "Crossing";
                File.WriteAllBytes($"Temp/CreekCrossings/{name}.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
