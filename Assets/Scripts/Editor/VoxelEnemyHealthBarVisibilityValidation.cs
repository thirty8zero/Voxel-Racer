using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelEnemyHealthBarVisibilityValidation
    {
        private const string Pending = "VoxelRacer.EnemyBarVisibilityCheck";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Scene previousScene, scene;
        private static GameObject root;
        private static VoxelEnemyCar enemy;
        private static VoxelEnemyHealthBar bar;
        private static Renderer[] renderers;
        private static Material[] materials;
        private static float started, savedScale;
        private static float pausedAlpha;
        private static bool savedBackground;
        private static UnityEngine.Random.State random;
        private static int phase;

        [InitializeOnLoadMethod]
        private static void Hook()
        { EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed; }

        [MenuItem("Tools/Voxel Racer/Validate Enemy Health Bar Visibility in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start from Edit Mode.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick; SessionState.SetBool(Pending, false);
                Time.timeScale = savedScale; Application.runInBackground = savedBackground;
            }
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        private static void Setup()
        {
            savedScale = Time.timeScale; savedBackground = Application.runInBackground; random = UnityEngine.Random.state;
            previousScene = SceneManager.GetActiveScene(); scene = SceneManager.CreateScene("Temporary enemy bar visibility QA");
            SceneManager.SetActiveScene(scene); Time.timeScale = 1; Application.runInBackground = true;
            try
            {
                foreach (var kind in new[] { "Lit", "Unlit" })
                {
                    var template = Resources.Load<Material>("Effects/HealthBar" + kind + "Fade");
                    Check(template != null && !ShaderUtil.ShaderHasError(template.shader) &&
                        template.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), "Health bar alpha variant is missing or invalid.");
                }
                root = new GameObject("Temporary health bar visibility test");
                var player = new GameObject("Player").AddComponent<VoxelCarController>(); player.transform.SetParent(root.transform);
                player.enabled = false; player.topSpeed = 0;
                enemy = new GameObject("Enemy").AddComponent<VoxelEnemyCar>(); enemy.transform.SetParent(root.transform);
                enemy.Configure(player, VoxelObstacleCarTuning.Load(), Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"), null, 250, 0);
                enemy.enabled = false; enemy.transform.position = new Vector3(0, 0, 250);
                bar = enemy.GetComponentInChildren<VoxelEnemyHealthBar>(); renderers = bar.GetComponentsInChildren<Renderer>();
                Check(bar.UsesDistanceVisibility && bar.VisibilityAlpha == 0 && renderers.All(r => !r.enabled), "Distant enemy bar flashed on at creation.");
                materials = ((IEnumerable)typeof(VoxelEnemyHealthBar).GetField("ownedMaterials", Private).GetValue(bar)).Cast<Material>()
                    .Concat(((IEnumerable)typeof(VoxelEnemyHealthBar).GetField("fadeMaterials", Private).GetValue(bar)).Cast<Material>()).ToArray();
                var spawnFade = enemy.gameObject.AddComponent<VoxelFadeIn>();
                var states = (IEnumerable)typeof(VoxelFadeIn).GetField("renderers", Private).GetValue(spawnFade);
                foreach (object state in states)
                {
                    var renderer = (MeshRenderer)state.GetType().GetField("renderer").GetValue(state);
                    Check(renderer.GetComponentInParent<VoxelEnemyHealthBar>() == null, "Vehicle spawn fade captured the independently fading bar.");
                }
                var generic = VoxelEnemyHealthBar.Create(root.transform, 3f, .2f, 1f, Color.red, Color.black, outlined: true);
                Check(!generic.UsesDistanceVisibility && generic.VisibilityAlpha == 1 && generic.GetComponentsInChildren<Renderer>().All(r => r.enabled),
                    "Generic/barrel health bars inherited the enemy range gate.");
                phase = 0; started = Time.realtimeSinceStartup; EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish("FAIL: " + error); }
        }
        private static void Tick()
        {
            float delay = phase == 0 ? 1.2f : phase == 1 || phase == 4 ? .15f : phase == 2 ? .15f : .5f;
            if (!Application.isPlaying || EditorApplication.isPaused || Time.realtimeSinceStartup - started < delay) return;
            try
            {
                switch (phase)
                {
                    case 0:
                        Check(bar.VisibilityAlpha == 0 && renderers.All(r => !r.enabled), "Spawn fade made the 250m bar visible.");
                        enemy.transform.position = new Vector3(0, 0, 150);
                        break;
                    case 1:
                        Check(bar.VisibilityAlpha > 0 && bar.VisibilityAlpha < 1 && renderers.All(r => r.enabled), "Enemy bar did not fade at the 150m boundary.");
                        Check(renderers.All(r => r.sharedMaterial.GetFloat("_Surface") == 1f), "Partial bar opacity did not use alpha-blended materials.");
                        bar.SetHealth(.25f);
                        var outline = (Material)typeof(VoxelEnemyHealthBar).GetField("outlineMaterial", Private).GetValue(bar);
                        Check(outline.color == Color.Lerp(Color.red, new Color(1f, .92f, 0f), .5f), "Damage while fading lost outline health colour.");
                        pausedAlpha = bar.VisibilityAlpha; Time.timeScale = 0;
                        break;
                    case 2:
                        Check(bar.VisibilityAlpha == pausedAlpha, "Paused bar opacity changed on real frames.");
                        float held = bar.VisibilityAlpha;
                        VoxelMissileValidation.Call(bar, "TickVisibility", 0f);
                        Check(bar.VisibilityAlpha == held, "Paused bar opacity changed.");
                        Time.timeScale = 1;
                        break;
                    case 3:
                        Check(bar.VisibilityAlpha == 1 && renderers.All(r => r.enabled), "Nearby bar did not reach full opacity.");
                        Check(renderers.All(r => r.sharedMaterial.GetFloat("_Surface") == 0f), "Full bar did not restore its original materials.");
                        enemy.transform.position = new Vector3(0, 0, 151);
                        break;
                    case 4:
                        Check(bar.VisibilityAlpha > 0 && bar.VisibilityAlpha < 1, "Bar did not fade out beyond 150m.");
                        break;
                    case 5:
                        Check(bar.VisibilityAlpha == 0 && renderers.All(r => !r.enabled), "Out-of-range bar continued drawing.");
                        enemy.transform.position = new Vector3(149, 0, 0);
                        break;
                    case 6:
                        Check(bar.VisibilityAlpha == 1, "Lateral player distance did not bring the bar back.");
                        bar.gameObject.SetActive(false);
                        Check(bar.VisibilityAlpha == 0, "Disabled bar retained its opacity.");
                        bar.gameObject.SetActive(true);
                        break;
                    case 7:
                        Check(bar.VisibilityAlpha == 1, "Re-enabled bar did not fade back in.");
                        Object.Destroy(enemy.gameObject);
                        break;
                    default:
                        Check(materials.All(m => m == null), "Health bar opacity materials leaked after destruction.");
                        Finish("PASS (Play Mode): real enemy starts hidden at 250m; parent spawn fade cannot expose it; begins smooth 0.4s fade at 150m; pause and health/outline changes during fading; full opacity restores existing materials; beyond-range fade and disabled distant renderers; lateral-distance return; disable/re-enable; native material cleanup. Generic/barrel bars retain existing visibility. Original scene, clock, background and RNG restored.");
                        return;
                }
                phase++; started = Time.realtimeSinceStartup;
            }
            catch (Exception error) { Finish("FAIL: " + error); }
        }
        private static void Finish(string report)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Temp/EnemyHealthBar"); File.WriteAllText("Temp/EnemyHealthBar/VisibilityValidation.txt", report); Debug.Log(report);
            SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
            UnityEngine.Random.state = random; Time.timeScale = savedScale; Application.runInBackground = savedBackground;
            EditorApplication.isPlaying = false;
        }
    }
}
