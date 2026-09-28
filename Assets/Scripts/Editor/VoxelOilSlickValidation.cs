using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelOilSlickValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object value, string field, object data) => value.GetType().GetField(field, Private).SetValue(value, data);
        private static void Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, Private).Invoke(value, args);
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        private const string PlayPending = "VoxelRacer.OilValidationPending";
        private static GameObject playRoot;
        private static VoxelCarController playCar;
        private static VoxelOilSlickObstacle playOil;
        private static VoxelCameraFollow playCamera;
        private static float playStarted, playStartOffset, cameraYaw;
        private static bool sawSpin;
        private static int startingIntegrity;

        [InitializeOnLoadMethod]
        private static void HookPlayValidation()
        {
            EditorApplication.playModeStateChanged -= OnPlayState;
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        [MenuItem("Tools/Voxel Racer/Validate Oil Slick in Play Mode")]
        public static void RunPlayMode()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Start this check from Edit Mode.");
            SessionState.SetBool(PlayPending, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PlayPending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                try
                {
                    playRoot = new GameObject("Temporary oil Play Mode validation");
                    var road = CreateRoad(playRoot.transform);
                    var model = Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab, playRoot.transform);
                    playCar = model.AddComponent<VoxelCarController>();
                    playCar.SetLaneLayout(3, 16f/3f); playCar.SetTrack(road, -7);
                    playCar.topSpeed = 12; playCar.acceleration = 80;
                    playStartOffset = playCar.CurrentLaneOffset;
                    startingIntegrity = playCar.RemainingIntegrityVoxels;
                    playOil = new GameObject("Test slick").AddComponent<VoxelOilSlickObstacle>(); playOil.transform.SetParent(playRoot.transform);
                    playOil.Configure(playCar, road, Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/OilSlick"), 0, playStartOffset, 16f/3f);
                    playCamera = new GameObject("Test chase camera").AddComponent<VoxelCameraFollow>();
                    playCamera.transform.SetParent(playRoot.transform); playCamera.target = playCar.transform;
                    playStarted = Time.realtimeSinceStartup; sawSpin = false;
                    EditorApplication.update += CheckPlayMode;
                }
                catch (Exception error) { FinishPlayCheck("FAILED: " + error); }
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= CheckPlayMode;
                SessionState.SetBool(PlayPending, false);
            }
        }

        private static void CheckPlayMode()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try
            {
                Require(Time.realtimeSinceStartup - playStarted < 18f, "Timed out waiting for oil traversal.");
                if (!sawSpin && playCar.IsOilSpinning)
                {
                    sawSpin = true;
                    cameraYaw = playCamera.transform.eulerAngles.y;
                }
                if (sawSpin && playCar.IsOilSpinning)
                    Require(Mathf.Abs(Mathf.DeltaAngle(cameraYaw,playCamera.transform.eulerAngles.y)) < 8f, "Camera rotated with the oil spin.");
                if (!sawSpin || playCar.IsOilSpinning) return;
                Require(playOil.HasTriggered, "Runtime oil contact was not recorded.");
                Require(Mathf.Abs(Mathf.Abs(playCar.CurrentLaneOffset - playStartOffset) - 16f/3f) < .01f, "Runtime spin did not finish one lane away.");
                Require(playCar.RemainingIntegrityVoxels == startingIntegrity, "Oil caused direct integrity damage.");
                var marks = Object.FindFirstObjectByType<VoxelOilTireMarks>();
                Require(marks != null && marks.GetComponent<MeshFilter>().sharedMesh.vertexCount >= 64, "Runtime tire marks were not emitted.");
                Require(Vector3.Dot(playCar.transform.forward, Vector3.forward) > .99f, "Runtime car did not recover its forward heading.");
                FinishPlayCheck("PASSED: actual Update/LateUpdate swept oil contact, complete spin, adjacent lane, black wheel ribbons, zero integrity damage and stable chase camera.");
            }
            catch (Exception error) { FinishPlayCheck("FAILED: " + error); }
        }

        private static void FinishPlayCheck(string message)
        {
            EditorApplication.update -= CheckPlayMode;
            Directory.CreateDirectory("Temp/OilSlick");
            File.WriteAllText("Temp/OilSlick/PlayModeValidation.txt", message);
            if (message.StartsWith("PASSED")) Debug.Log(message); else Debug.LogError(message);
            if (playRoot != null) Object.Destroy(playRoot);
            EditorApplication.isPlaying = false;
        }

        [MenuItem("Tools/Voxel Racer/Validate Oil Slick")]
        public static void Run()
        {
            Require(!Application.isPlaying, "Run this validation in Edit Mode.");
            var random = UnityEngine.Random.state;
            var preview = new PreviewRenderUtility();
            var root = new GameObject("Temporary oil validation");
            preview.AddSingleGO(root);
            try
            {
                var road = CreateRoad(root.transform);
                var player = new GameObject("Test player").AddComponent<VoxelCarController>();
                player.transform.SetParent(root.transform);
                player.enabled = false;
                var oil = Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/OilSlick");
                Require(oil != null && oil.obstacleType == VoxelStaticObstacleType.OilSlick, "Oil asset missing/wrong type.");
                bool left = false, right = false;
                for (int lane = 0; lane < 3; lane++)
                for (int seed = 0; seed < 12; seed++)
                {
                    Set(player, "currentLane", lane); player.SetLaneLayout(3, 5f); player.SetTrack(road, 0f);
                    UnityEngine.Random.InitState(seed);
                    float start = player.CurrentLaneOffset;
                    Require(player.TryStartOilSpin(1f, 8f), "Spin rejected.");
                    Require(!player.TryStartOilSpin(1f, 8f), "Overlapping oil restarted the spin.");
                    float end = player.TargetLaneOffset;
                    Require(Mathf.Abs(Mathf.Abs(end - start) - 5f) < .001f, "Spin did not choose an adjacent lane.");
                    Require(end >= -5f && end <= 5f, "Spin left the road.");
                    if (lane == 1) { left |= end < start; right |= end > start; }
                    Call(player, "RequestLaneChange", lane);
                    Require(player.TargetLaneOffset == end, "Steering interrupted the spin.");
                    Call(player, "AdvanceOilSpin", .5f); Call(player, "ApplyTrackPose");
                    Require(Vector3.Dot(player.transform.forward, road.Evaluate(0).forward) < -.99f, "Halfway spin is not 180 degrees.");
                    Call(player, "AdvanceOilSpin", .5f); Call(player, "ApplyTrackPose");
                    Require(!player.IsOilSpinning && Mathf.Abs(player.CurrentLaneOffset - end) < .001f, "Spin did not settle in the chosen lane.");
                    Require(Vector3.Dot(player.transform.forward, road.Evaluate(0).forward) > .99f, "Spin did not finish facing forward.");
                }
                Require(left && right, "Middle-lane trials did not exercise both random directions.");
                Set(player, "currentLane", 0); player.SetLaneLayout(1, 5f); player.SetTrack(road, 0);
                Require(player.TryStartOilSpin(1, 8), "Single-lane fallback rejected.");
                Call(player, "AdvanceOilSpin", 1f);
                Require(Mathf.Approximately(player.CurrentLaneOffset, 0), "Single-lane road spun off-road.");
                Set(player, "currentLane", 1); player.SetLaneLayout(3, 5f); player.SetTrack(road, -12);
                var slick = new GameObject("Test oil").AddComponent<VoxelOilSlickObstacle>();
                slick.transform.SetParent(root.transform);
                slick.Configure(player, road, oil, 0, 0, 5);
                player.SetTrack(road, 12);
                Call(slick, "CheckPlayerContact");
                Require(slick.HasTriggered && player.IsOilSpinning, "Fast swept traversal missed the oil.");
                Require(slick.GetComponentsInChildren<Collider>().Length == 0, "Oil has a collider that could affect traffic.");
                Call(player, "AdvanceOilSpin", 2f);
                player.SetDrivingEnabled(false);
                Require(!player.TryStartOilSpin(1, 8), "Disabled driving accepted oil spin.");
                foreach (var track in Resources.LoadAll<VoxelTrackDefinition>("Tracks"))
                    Require(track.obstacleCarTuning.staticObstacleSpawns.Any(e => e != null && e.obstacle == oil && e.spawnWeight > 0), track.name + " is missing oil registration.");
                Require(!ShaderUtil.ShaderHasError(Shader.Find("Voxel Racer/Road Asphalt")), "Asphalt shader failed compilation.");
                Debug.Log("Oil validation passed: 36 lane/direction trials, 180/360 rotation, input lock, overlap rejection, single-lane fallback, swept fast contact, no traffic colliders, disabled driving, track registration and shader compilation.");
            }
            finally { preview.Cleanup(); UnityEngine.Random.state = random; }
        }

        private static EndlessVoxelRoad CreateRoad(Transform root)
        {
            typeof(VoxelRacerBootstrap).GetMethod("PrepareTrackMaterials", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Resources.Load<VoxelTrackDefinition>("Tracks/Track01") });
            var road = new GameObject("Preview road").AddComponent<EndlessVoxelRoad>();
            road.transform.SetParent(root);
            road.enabled = false; road.roadWidth = 16; road.laneCount = 3; road.segmentCount = 3;
            road.turnChancePerSegment = 0; road.minimumCactiPerSegment = road.maximumCactiPerSegment = 0;
            road.BuildInitialRoad();
            return road;
        }

        [MenuItem("Tools/Voxel Racer/Render Road and Oil Slick")]
        public static void Render()
        {
            var preview = new PreviewRenderUtility();
            var root = new GameObject("Road and oil preview");
            preview.AddSingleGO(root);
            try
            {
                var road = CreateRoad(root.transform);
                var oil = new GameObject("Oil slick").AddComponent<VoxelOilSlickObstacle>(); oil.transform.SetParent(root.transform);
                oil.Configure(null, road, Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/OilSlick"), 7, 0, 16f/3f);
                var hitOil = new GameObject("Oil beneath skid start").AddComponent<VoxelOilSlickObstacle>(); hitOil.transform.SetParent(root.transform);
                hitOil.Configure(null, road, Resources.Load<VoxelStaticObstacleDefinition>("StaticObstacles/OilSlick"), -7, 0, 16f/3f);
                var carModel = Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab, root.transform);
                var player = carModel.AddComponent<VoxelCarController>(); player.enabled = false;
                player.SetLaneLayout(3, 16f/3f); player.SetTrack(road, -7);
                player.TryStartOilSpin(1,8);
                var marks = new GameObject("Oil tire marks").AddComponent<VoxelOilTireMarks>(); marks.transform.SetParent(root.transform);
                marks.Configure(player,8);
                for (int i = 0; i <= 45; i++)
                {
                    // Simulate road-space movement and the actual wheel positions for the presentation frame.
                    typeof(VoxelCarController).GetProperty("TrackDistance").GetSetMethod(true).Invoke(player, new object[] { -7f + i * .17f });
                    if (i > 0) Call(player,"AdvanceOilSpin", 1f/60f);
                    Call(player,"ApplyTrackPose");
                    Set(marks,"lastSampleTime", -1f);
                    marks.Sample();
                }
                Call(marks,"Update");
                preview.camera.transform.position = new Vector3(15,20,-17);
                preview.camera.transform.LookAt(new Vector3(0,0,7));
                preview.camera.fieldOfView = 48; preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 180;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.3f,.32f,.37f);
                preview.lights[0].intensity = 1.4f; preview.lights[0].transform.rotation = Quaternion.Euler(45,-30,0);
                preview.lights[1].intensity = .7f; preview.lights[1].transform.rotation = Quaternion.Euler(35,140,0);
                preview.ambientColor = new Color(.55f,.55f,.55f);
                preview.BeginStaticPreview(new Rect(0,0,1280,900)); preview.Render(true);
                var image = preview.EndStaticPreview(); Directory.CreateDirectory("Temp/OilSlick");
                File.WriteAllBytes("Temp/OilSlick/RoadAndOil.png",image.EncodeToPNG()); Object.DestroyImmediate(image);
                Debug.Log("Rendered Temp/OilSlick/RoadAndOil.png using actual road, oil and tire mark geometry.");
            }
            finally { preview.Cleanup(); }
        }
    }
}
