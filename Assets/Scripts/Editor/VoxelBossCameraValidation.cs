using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelBossCameraValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

        [MenuItem("Tools/Voxel Racer/Validate Boss Camera Avoidance")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
            var root = new GameObject("Temporary boss camera validation");
            var settings = Object.Instantiate(VoxelCameraTuning.Load());
            try
            {
                var carModel = Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab, root.transform);
                var player = carModel.AddComponent<VoxelCarController>(); player.enabled = false;
                player.laneWidth = 5f;
                var bossObject = new GameObject("Test boss"); bossObject.transform.SetParent(root.transform, false);
                var bossSettings = Resources.Load<VoxelBossDefinition>("Bosses/RedVanBoss");
                var model = Object.Instantiate(bossSettings.bossPrefab, bossObject.transform);
                model.transform.localScale *= 5f * 1.8f / 2.23f;
                var boss = bossObject.AddComponent<VoxelEnemyCar>();
                boss.ConfigureBoss(bossSettings, 5, 4);
                typeof(VoxelEnemyCar).GetProperty("CurrentHealth").SetValue(boss, 100f);
                var cameraObject = new GameObject("Test camera"); cameraObject.transform.SetParent(root.transform, false);
                var follow = cameraObject.AddComponent<VoxelCameraFollow>(); follow.target = player.transform; follow.tuning = settings;
                Vector3 Step(float dt, Quaternion heading) => (Vector3)Call(follow, "GetBossAttackCameraOffset", player,
                    player.transform.position, heading, settings.chaseOffset, dt);
                void Phase(VoxelEnemyCar.SpikeAttackPhase phase) => Call(boss, "SetSpikePhase", phase);
                void Reset(Vector3 position)
                {
                    boss.transform.position = position;
                    typeof(VoxelEnemyCar).GetField("trackDistance", Private).SetValue(boss, position.z);
                    typeof(VoxelEnemyCar).GetField("laneOffset", Private).SetValue(boss,
                        (Quaternion.Inverse(player.transform.rotation) * position).x);
                    typeof(VoxelCameraFollow).GetField("bossCameraBlend", Private).SetValue(follow, 0f);
                    follow.SetBossCameraTarget(boss);
                }

                Reset(new Vector3(-7.5f, 0, 0)); Phase(VoxelEnemyCar.SpikeAttackPhase.Normal);
                Check(Vector3.Distance(Step(1, Quaternion.identity), settings.chaseOffset) < .001f, "Normal driving camera changed");
                Phase(VoxelEnemyCar.SpikeAttackPhase.Warning);
                Vector3 halfway = Step(settings.bossAttackCameraSwingDuration * .5f, Quaternion.identity);
                Check(Mathf.Abs(halfway.x) < .01f && halfway.z < settings.chaseOffset.z, "Swing did not follow a rear orbit");
                Vector3 right = Step(settings.bossAttackCameraSwingDuration, Quaternion.identity);
                Check(Vector3.Distance(right, settings.bossAttackCameraOffset) < .001f, "Camera did not reach right view");
                Check(!(bool)Call(follow, "BossBlocksView", right, Vector3.up * .7f, Vector3.zero, settings.bossAttackCameraClearance), "Right view still occluded");
                Phase(VoxelEnemyCar.SpikeAttackPhase.Holding);
                Check(Vector3.Distance(Step(1, Quaternion.identity), right) < .001f, "Camera failed to hold attack view");
                Directory.CreateDirectory("Temp/BossCamera");
                model.GetComponent<VoxelBossSpikeRig>()?.SetPose(1, 1);
                Render(root, settings.chaseOffset, settings.chaseLookAhead, "Blocked");
                Render(root, right, settings.chaseLookAhead, "RightView");
                Phase(VoxelEnemyCar.SpikeAttackPhase.Retreating);
                Vector3 returning = Step(settings.bossAttackCameraReturnDuration * .25f, Quaternion.identity);
                Check(returning.x < right.x && returning.x > settings.chaseOffset.x, "Return snapped or failed to begin at retreat");
                Check(Vector3.Distance(Step(settings.bossAttackCameraReturnDuration, Quaternion.identity), settings.chaseOffset) < .001f, "Did not restore original camera");

                Reset(new Vector3(-7.5f, 0, 80)); Phase(VoxelEnemyCar.SpikeAttackPhase.Warning);
                Check(Vector3.Distance(Step(1, Quaternion.identity), settings.chaseOffset) < .001f,
                    "Camera swung before the boss reached the trigger distance");
                Reset(new Vector3(-7.5f, 0, 15)); Phase(VoxelEnemyCar.SpikeAttackPhase.Warning);
                Check(Vector3.Distance(Step(1, Quaternion.identity), right) < .001f,
                    "Camera did not swing inside the trigger distance when lane requirements were met");
                // A previously latched swing must release as soon as right-lane separation is lost.
                typeof(VoxelEnemyCar).GetField("laneOffset", Private).SetValue(boss, -4.9f);
                Check(Vector3.Distance(Step(1, Quaternion.identity), settings.chaseOffset) < .001f, "Leftward lane change left right camera latched");
                foreach (float separation in new[] { -5f, 0f, 4.9f, 5f, 10f })
                {
                    Reset(new Vector3(-7.5f, 0, 0));
                    typeof(VoxelEnemyCar).GetField("laneOffset", Private).SetValue(boss, -separation);
                    Vector3 expected = separation >= player.laneWidth ? right : settings.chaseOffset;
                    Check(Vector3.Distance(Step(1, Quaternion.identity), expected) < .001f,
                        "One-lane-right gate failed at separation " + separation);
                }
                Reset(new Vector3(7.5f, 0, 0));
                Check(Vector3.Distance(Step(1, Quaternion.identity), settings.chaseOffset) < .001f, "Unobstructed side unnecessarily swings");
                Reset(new Vector3(0, 0, 80));
                Check(Vector3.Distance(Step(1, Quaternion.identity), settings.chaseOffset) < .001f, "Distant head-on approach unnecessarily swings");
                foreach (float yaw in new[] { -65f, 40f, 140f })
                {
                    var heading = Quaternion.Euler(0, yaw, 0);
                    player.transform.rotation = heading; boss.transform.rotation = heading;
                    Reset(heading * new Vector3(-7.5f, 0, 0));
                    Check(Vector3.Distance(Step(1, heading), right) < .001f, "Curved-road view failed");
                }
                follow.SetBossCameraTarget(null);
                Check(Vector3.Distance(Step(1, Quaternion.identity), settings.chaseOffset) < .001f, "Removed boss left camera stuck");
                player.transform.rotation = boss.transform.rotation = Quaternion.identity;
                Reset(new Vector3(-7.5f, 0, 0)); settings.bossAttackCameraEnabled = false;
                Check(Vector3.Distance(Step(1, Quaternion.identity), settings.chaseOffset) < .001f, "Disable switch ignored");
                File.WriteAllText("Temp/BossCamera/Validation.txt", "PASS: unobstructed/normal/head-on views unchanged; obstructed attack and warning prediction swing right; clear alternate sight line; stable holding; smooth return at retreat; curved headings; removed boss; disable switch. Edit Mode simulation using actual boss model.");
                Debug.Log(File.ReadAllText("Temp/BossCamera/Validation.txt"));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(settings); }
        }

        private static void Render(GameObject source, Vector3 offset, float lookAhead, string name)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(Object.Instantiate(source));
                preview.camera.transform.position = offset;
                preview.camera.transform.LookAt(Vector3.forward * lookAhead + Vector3.up * .3f);
                preview.camera.fieldOfView = 58; preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 200;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.26f, .30f, .35f);
                preview.ambientColor = new Color(.5f, .5f, .5f);
                preview.lights[0].intensity = 1.6f; preview.lights[0].transform.rotation = Quaternion.Euler(40, 150, 0);
                preview.lights[1].intensity = 1; preview.lights[1].transform.rotation = Quaternion.Euler(30, 210, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 1280, 720)); preview.Render(true);
                var image = preview.EndStaticPreview(); File.WriteAllBytes("Temp/BossCamera/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
        }
    }
}
