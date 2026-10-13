using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelBrakeLightValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private const string Pending = "VoxelRacer.BrakeLightPlayValidation";
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Call(VoxelCarController car, string method, params object[] args) =>
            typeof(VoxelCarController).GetMethod(method, Private).Invoke(car, args);

        [MenuItem("Tools/Voxel Racer/Validate Player Brake Lights")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run the material/render validation in Edit Mode.");
            Directory.CreateDirectory("Temp/BrakeLights");
            var tail = Resources.Load<Material>("CarMaterials/CarTailLights");
            var brake = Resources.Load<Material>("CarMaterials/CarBrakeLights");
            Check(tail != null && brake != null, "Missing tail/brake materials.");
            Check(brake.IsKeywordEnabled("_EMISSION") && brake.GetColor("_EmissionColor").r > 1,
                "Brake material needs an authored HDR emission variant for Android builds.");
            Check((brake.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive) != 0,
                "The material emission toggle must stay enabled when Unity validates it.");
            string tailBefore = EditorJsonUtility.ToJson(tail), brakeBefore = EditorJsonUtility.ToJson(brake);
            int lenses = 0;
            foreach (var definition in Resources.LoadAll<VoxelCarDefinition>("Cars"))
            {
                if (definition.visualPrefab == null) continue;
                var car = Object.Instantiate(definition.visualPrefab).AddComponent<VoxelCarController>();
                car.enabled = false;
                try
                {
                    var renderers = car.GetComponentsInChildren<MeshRenderer>(true);
                    var originals = renderers.Select(r => r.sharedMaterial).ToArray();
                    var red = renderers.Where(r => r.sharedMaterial == tail).ToArray();
                    car.ResetIntegrityBaseline();
                    int integrity = car.TotalIntegrityVoxels;
                    Call(car, "SetBrakeLights", true);
                    Check(car.BrakeLightsActive == (red.Length > 0), "Wrong activation for " + definition.name);
                    for (int i = 0; i < renderers.Length; i++)
                        Check(renderers[i].sharedMaterial == (originals[i] == tail ? brake : originals[i]),
                            "Changed a non-red lens/body material on " + definition.name);
                    Check(car.TotalIntegrityVoxels == integrity, "Brake lights changed integrity.");
                    Call(car, "OnDisable");
                    Check(!car.BrakeLightsActive && red.All(r => r.sharedMaterial == tail), "Disable did not restore lenses.");
                    if (red.Length > 0)
                    {
                        red[0].gameObject.SetActive(false);
                        car.RefreshBrakeLights(); Call(car, "SetBrakeLights", true);
                        Check(!red[0].gameObject.activeSelf, "Braking resurrected a damaged lens.");
                        red[0].gameObject.SetActive(true);
                        Check(red[0].sharedMaterial == brake, "A repaired lens did not share the current brake state.");
                        Call(car, "SetBrakeLights", false);
                    }
                    lenses += red.Length;
                }
                finally { Object.DestroyImmediate(car.gameObject); }
            }
            Check(lenses >= 6, "Primary player car brake lenses were not found.");
            Render();
            Check(tailBefore == EditorJsonUtility.ToJson(tail) && brakeBefore == EditorJsonUtility.ToJson(brake),
                "Validation mutated shared material assets.");
            File.WriteAllText("Temp/BrakeLights/Validation.txt", "PASS (Edit Mode): all car definitions, red-lens-only material swaps, amber/white/body preservation, release/disable, damaged and repaired lenses, integrity unchanged, shared assets unchanged, Android emission keyword and matching rear renders. " + lenses + " red lenses checked.");
            Debug.Log("PASS player brake lights: red lenses, damage/repair, materials, integrity and rear renders.");
        }

        private static void Render()
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var model = Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab);
                preview.AddSingleGO(model);
                var car = model.AddComponent<VoxelCarController>(); car.enabled = false; car.RefreshBrakeLights();
                preview.camera.transform.position = new Vector3(3.7f, 2.7f, -7.5f);
                preview.camera.transform.LookAt(new Vector3(0, .8f, -.6f));
                preview.camera.fieldOfView = 33; preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 50;
                preview.camera.allowHDR = true;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.09f, .11f, .14f);
                preview.lights[0].intensity = 1.4f; preview.lights[0].transform.rotation = Quaternion.Euler(35, 210, 0);
                preview.lights[1].intensity = .7f; preview.lights[1].transform.rotation = Quaternion.Euler(30, 25, 0);
                preview.ambientColor = new Color(.3f, .3f, .3f);
                VoxelMobilePostProcessing.Configure(preview.camera);
                int releasedRedPixels = 0;
                foreach (bool braking in new[] { false, true })
                {
                    Call(car, "SetBrakeLights", braking);
                    preview.BeginStaticPreview(new Rect(0, 0, 1100, 700)); preview.Render(true);
                    var image = preview.EndStaticPreview();
                    int brightRedPixels = image.GetPixels().Count(c => c.r > .75f && c.r > c.g * 2 && c.r > c.b * 2);
                    if (!braking) releasedRedPixels = brightRedPixels;
                    else Check(brightRedPixels > releasedRedPixels + 100, "Brake emission did not brighten the rendered red lenses.");
                    File.WriteAllBytes("Temp/BrakeLights/" + (braking ? "Braking" : "Released") + ".png", image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
            }
            finally { preview.Cleanup(); }
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.playModeStateChanged += Changed;

        [MenuItem("Tools/Voxel Racer/Validate Player Brake Lights in Play Mode")]
        public static void RunPlay()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Start brake-light validation from Edit Mode.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                EditorApplication.delayCall += ValidateInput;
        }
        private static void ValidateInput()
        {
            var oldKeyboard = Keyboard.current;
            var pauseField = typeof(VoxelPauseMenu).GetField("<Active>k__BackingField", Static);
            object oldPause = pauseField.GetValue(null);
            var heldField = typeof(VoxelMobileControls).GetField("brakeHeld", Static);
            object oldHeld = heldField.GetValue(null);
            Keyboard keyboard = null; GameObject root = null;
            try
            {
                pauseField.SetValue(null, null); heldField.SetValue(null, false);
                root = Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab);
                var car = root.AddComponent<VoxelCarController>(); car.enabled = false; car.ResetIntegrityBaseline();
                keyboard = InputSystem.AddDevice<Keyboard>();
                void Step(bool space, bool touch)
                {
                    InputSystem.QueueStateEvent(keyboard, space ? new KeyboardState(Key.Space) : new KeyboardState());
                    InputSystem.Update(); heldField.SetValue(null, touch); Call(car, "Update");
                }
                Step(false, false); Check(!car.BrakeLightsActive, "Coasting lit the brake lenses.");
                Step(true, false); Check(car.BrakeLightsActive, "Space braking did not light rear lenses.");
                Step(false, false); Check(!car.BrakeLightsActive, "Keyboard release left rear lenses lit.");
                Step(false, true); Check(car.BrakeLightsActive, "Touch braking did not light rear lenses.");
                Step(false, false); Check(!car.BrakeLightsActive, "Touch release left rear lenses lit.");
                Step(true, false); car.SetDrivingEnabled(false);
                Check(!car.BrakeLightsActive, "Disabling driving left rear lenses lit.");
                Step(true, true); Check(!car.BrakeLightsActive, "Start/disabled driving gates allowed brake glow.");
                car.SetDrivingEnabled(true);
                typeof(VoxelCarController).GetProperty("CurrentSpeed").SetValue(car, 15f);
                car.BeginFinishStop(); Step(false, false);
                Check(car.BrakeLightsActive, "Automatic finish braking did not light rear lenses.");
                typeof(VoxelCarController).GetProperty("CurrentSpeed").SetValue(car, 0f); Step(false, false);
                Check(!car.BrakeLightsActive, "Finish lights remained lit after stopping.");
                Directory.CreateDirectory("Temp/BrakeLights");
                File.WriteAllText("Temp/BrakeLights/PlayValidation.txt", "PASS (Play Mode, controlled Update calls): actual Input System Space press/release and shared touch brake input; coasting off, both inputs on, release off, driving gate, finish deceleration on and stopped finish off. Input device and held/pause state restored.");
                Debug.Log("PASS player brake input: Space/touch, release, driving gate and finish stop.");
            }
            catch (Exception e)
            {
                Directory.CreateDirectory("Temp/BrakeLights"); File.WriteAllText("Temp/BrakeLights/PlayValidation.txt", "FAIL: " + e);
                Debug.LogException(e);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                oldKeyboard?.MakeCurrent(); pauseField.SetValue(null, oldPause); heldField.SetValue(null, oldHeld);
                SessionState.SetBool(Pending, false); EditorApplication.isPlaying = false;
            }
        }
    }
}
