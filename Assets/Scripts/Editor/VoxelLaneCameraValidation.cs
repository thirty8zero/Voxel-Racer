using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelLaneCameraValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);
        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Voxel Racer/Validate Lane Change Camera")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run lane camera validation in Edit Mode.");
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary lane camera validation");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var settings = Object.Instantiate(VoxelCameraTuning.Load());
            settings.playerVehicleImpactShakeDuration = settings.objectExplosionShakeDuration = 0;
            try
            {
                // Keep the authored duration/easing, including slow camera moves that
                // make the much faster steering animation especially visible.
                foreach (int fps in new[] { 30, 60, 120 })
                foreach (int direction in new[] { -1, 1 })
                foreach (var curve in new[] { new Vector2(0, 0), new Vector2(65, 30), new Vector2(-95, -30) })
                    CheckLaneMove(root, settings, fps, direction, curve.x, curve.y);
                CheckOffsetsAndSpin(root, settings);
                CheckTransformFallback(root, settings);
                const string report = "PASS: left/right lane changes at 30/60/120 FPS; steering yaw/roll " +
                    "cannot steer the camera at departure, arrival or recovery; straight and curved road headings; " +
                    "authored camera duration/easing and final lane centring; boost remains visible; " +
                    "ram recoil retained; oil-spin heading stability; non-car transform fallback. Edit Mode simulation.";
                Directory.CreateDirectory("Temp/LaneCamera");
                File.WriteAllText("Temp/LaneCamera/Validation.txt", report);
                Debug.Log(report);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static T Add<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>() ?? go.AddComponent<T>();
        }

        private static EndlessVoxelRoad CreatePath(Transform parent, float heading, float turn)
        {
            var road = Add<EndlessVoxelRoad>(parent, "Path data only");
            road.enabled = false;
            road.segmentLength = 1000f;
            // Seed real path data without generating scenery or touching scene assets.
            var segmentType = typeof(EndlessVoxelRoad).GetNestedType("RoadSegment", BindingFlags.NonPublic);
            var segments = (IList)typeof(EndlessVoxelRoad).GetField("pathSegments", Private).GetValue(road);
            var start = new VoxelTrackPose(Vector3.zero, heading);
            for (int index = 0; index < 2; index++)
            {
                var segment = Activator.CreateInstance(segmentType, true);
                segmentType.GetField("index").SetValue(segment, index);
                segmentType.GetField("startDistance").SetValue(segment, index * road.segmentLength);
                segmentType.GetField("startPosition").SetValue(segment, start.position);
                segmentType.GetField("startHeading").SetValue(segment, start.rotation.eulerAngles.y);
                segmentType.GetField("turnAngle").SetValue(segment, turn);
                segments.Add(segment);
                start = (VoxelTrackPose)typeof(EndlessVoxelRoad).GetMethod("EvaluateSegment", Private)
                    .Invoke(road, new[] { segment, (object)road.segmentLength });
            }
            return road;
        }

        private static VoxelCameraFollow CreateFollow(Transform parent, Transform target, VoxelCameraTuning settings)
        {
            var follow = Add<VoxelCameraFollow>(parent, "Camera");
            follow.enabled = false;
            follow.target = target;
            follow.tuning = settings;
            return follow;
        }

        private static void CheckLaneMove(GameObject root, VoxelCameraTuning settings, int fps,
            int direction, float heading, float turn)
        {
            var test = Add<Transform>(root.transform, "Lane movement case");
            try
            {
                var road = CreatePath(test, heading, turn);
                var car = Add<VoxelCarController>(test, "Car");
                car.enabled = false;
                car.SetLaneLayout(3, 3f);
                car.SetTrack(road, 10f);
                typeof(VoxelCarController).GetProperty("CurrentSpeed").SetValue(car, 20f);
                var follow = CreateFollow(test, car.transform, settings);
                Call(follow, "UpdateFollow", 0f);
                Call(car, "RequestLaneChange", 1 + direction);
                Check(Mathf.Abs(car.TargetLaneOffset - direction * 3f) < .001f,
                    "Validation did not start a lane change.");
                Call(follow, "UpdateFollow", 0f);
                float yaw = 0, roll = 0;
                float duration = settings.laneChangeCameraDuration;
                int frames = Mathf.CeilToInt(Mathf.Max(duration + .2f, 1f) * fps);
                for (int frame = 1; frame <= frames; frame++)
                {
                    float elapsed = (float)frame / fps;
                    Call(car, "AdvanceLaneChange", 1f / fps);
                    bool steering = Mathf.Abs(car.TargetLaneOffset - car.CurrentLaneOffset) > .01f;
                    float step = car.laneChangeVisualRotationSpeed / fps;
                    yaw = Mathf.MoveTowards(yaw, steering ? direction * car.laneChangeYawDegrees : 0f, step);
                    roll = Mathf.MoveTowards(roll, steering ? -direction * car.laneChangeBodyRollDegrees : 0f, step);
                    Set(car, "visualYaw", yaw);
                    Set(car, "visualRoll", roll);
                    typeof(VoxelCarController).GetProperty("TrackDistance").SetValue(car, 10f + elapsed * 20f);
                    Call(car, "ApplyTrackPose");
                    Set(follow, "laneChangeStartedAt", Time.time - elapsed);
                    Call(follow, "UpdateFollow", 1f / fps);

                    var pose = road.Evaluate(car.TrackDistance);
                    float lane = car.TargetLaneOffset * VoxelEasing.Evaluate(settings.laneChangeCameraEasing,
                        elapsed / Mathf.Max(.001f, duration));
                    Vector3 expected = pose.position + pose.rotation * (settings.chaseOffset + Vector3.right * lane);
                    Quaternion rotation = Quaternion.LookRotation(pose.forward * settings.chaseLookAhead +
                        Vector3.up * .3f - pose.rotation * settings.chaseOffset);
                    Check(Vector3.Distance(follow.transform.position, expected) < .002f,
                        $"Steering moved the camera off its eased road-relative path: {fps} FPS, direction {direction}, " +
                        $"road {heading}/{turn}, frame {frame}, yaw {yaw}, error {Vector3.Distance(follow.transform.position, expected):F4}m.");
                    Check(Quaternion.Angle(follow.transform.rotation, rotation) < .05f,
                        "Steering yaw/roll changed camera aim.");
                }
                Check(Mathf.Abs(car.CurrentLaneOffset - car.TargetLaneOffset) < .001f && Mathf.Abs(yaw) < .001f,
                    "Validation did not cover lane arrival and steering recovery.");
            }
            finally { Object.DestroyImmediate(test.gameObject); }
        }

        private static void CheckOffsetsAndSpin(GameObject root, VoxelCameraTuning settings)
        {
            var test = Add<Transform>(root.transform, "Offsets and oil spin");
            try
            {
                var road = CreatePath(test, 75f, 30f);
                var car = Add<VoxelCarController>(test, "Car");
                car.enabled = false;
                car.SetLaneLayout(4, 3f);
                car.SetTrack(road, 30f);
                Set(car, "boostForwardOffset", 7f);
                Set(car, "ramForwardOffset", -1f);
                Set(car, "ramLateralOffset", .4f);
                Call(car, "ApplyTrackPose");
                var follow = CreateFollow(test, car.transform, settings);
                Call(follow, "UpdateFollow", 0f);
                var pose = road.Evaluate(car.TrackDistance);
                var expected = pose.position + pose.rotation * (settings.chaseOffset +
                    new Vector3(car.CurrentLaneOffset + .4f, 0, -1));
                Check(Vector3.Distance(follow.transform.position, expected) < .002f,
                    "Boost cancellation or surviving ram recoil changed.");
                Quaternion rotation = follow.transform.rotation;
                Set(car, "oilSpinDuration", 2f);
                foreach (float spin in new[] { 0f, 45f, 180f, 315f, 360f })
                {
                    car.transform.rotation = pose.rotation * Quaternion.Euler(0, spin, 4);
                    Call(follow, "UpdateFollow", 0f);
                    Check(Vector3.Distance(follow.transform.position, expected) < .002f &&
                        Quaternion.Angle(follow.transform.rotation, rotation) < .05f, "Oil spin steered the camera.");
                }
            }
            finally { Object.DestroyImmediate(test.gameObject); }
        }

        private static void CheckTransformFallback(GameObject root, VoxelCameraTuning settings)
        {
            var target = Add<Transform>(root.transform, "Transform-only target");
            target.SetPositionAndRotation(new Vector3(10, 0, 20), Quaternion.Euler(0, 55, 0));
            var follow = CreateFollow(root.transform, target, settings);
            Call(follow, "UpdateFollow", 0f);
            Check(Vector3.Distance(follow.transform.position, target.position + target.rotation * settings.chaseOffset) < .002f,
                "Transform-only camera fallback changed.");
        }
    }
}
