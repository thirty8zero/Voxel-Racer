using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelCivilianTrafficSetValidation
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Voxel Racer/Validate Shared Civilian Traffic Sets")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Run from Edit Mode.");
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary shared traffic set QA");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var source = Resources.Load<VoxelCivilianTrafficSet>("Traffic/CivilianTraffic_Default");
            Check(source != null, "Missing shared default set.");
            string before = EditorJsonUtility.ToJson(source);
            var shared = Object.Instantiate(source);
            var separate = Object.Instantiate(source);
            var first = Object.Instantiate(VoxelObstacleCarTuning.Load());
            var second = Object.Instantiate(first);
            try
            {
                var tunings = Resources.LoadAll<VoxelTrackDefinition>("Tracks").Select(t => t.obstacleCarTuning).Append(VoxelObstacleCarTuning.Load()).Distinct().ToArray();
                Check(tunings.Length == 12 && tunings.All(t => t.civilianTrafficSet == source), "All track/fallback references must share the default.");
                Check(source.civilianVehiclePool.Length == 6 && source.paintColours.Length == 6, "Default model/palette migration incomplete.");
                foreach (var tuning in tunings)
                {
                    var serialized = new SerializedObject(tuning);
                    Check(serialized.FindProperty("civilianTrafficSet")?.objectReferenceValue == source, "Track reference is not serialized.");
                    Check(serialized.FindProperty("civilianVehiclePool") == null && serialized.FindProperty("paintColours") == null,
                        "Track still owns an embedded pool or palette.");
                }
                var create = typeof(VoxelCivilianTrafficSet).GetCustomAttribute<CreateAssetMenuAttribute>();
                Check(create != null && create.menuName == "Voxel Racer/Civilian Traffic Set", "Create menu unavailable.");
                using (var tree = PropertyTree.Create(first))
                {
                    tree.UpdateTree();
                    var field = tree.EnumerateTree(true).First(p => p.Name == "civilianTrafficSet");
                    Check(field.Attributes.OfType<InlineEditorAttribute>().Any(), "Set reference cannot be edited inline.");
                }
                using (var tree = PropertyTree.Create(shared))
                {
                    tree.UpdateTree();
                    foreach (string name in new[] { "civilianVehiclePool", "paintColours" })
                        Check(tree.EnumerateTree(true).First(p => p.Name == name).Attributes.OfType<FoldoutGroupAttribute>().Any(), "Missing independent foldout: " + name);
                }
                var hatch = source.civilianVehiclePool[0]; var van = source.civilianVehiclePool[1];
                first.civilianTrafficSet = second.civilianTrafficSet = shared;
                shared.civilianVehiclePool = new[] { hatch }; shared.paintColours = new[] { Color.magenta };
                Check(first.ChooseCivilianVehicle() == hatch && second.ChooseCivilianVehicle() == hatch && second.PaintColours[0] == Color.magenta,
                    "Shared changes did not reach both track references.");
                shared.civilianVehiclePool = new[] { van }; shared.paintColours[0] = Color.yellow;
                Check(first.ChooseCivilianVehicle() == van && second.ChooseCivilianVehicle() == van && first.PaintColours[0] == Color.yellow,
                    "Selection/palette cached old shared values.");
                second.civilianTrafficSet = separate;
                separate.civilianVehiclePool = new[] { hatch }; separate.paintColours = new[] { Color.cyan };
                Check(first.ChooseCivilianVehicle() == van && second.ChooseCivilianVehicle() == hatch && first.PaintColours[0] == Color.yellow,
                    "Assigning a separate set modified the shared set.");
                var player = root.AddComponent<VoxelCarController>(); player.enabled = false;
                foreach (var tuning in new[] { first, second })
                {
                    var go = new GameObject("Shared palette test"); go.transform.SetParent(root.transform);
                    var car = go.AddComponent<VoxelObstacleCar>(); car.enabled = false;
                    Check(car.Configure(player, tuning, true, null, 220f, 0f, 12f), "Shared-set configuration failed.");
                    Check(car.EnemyTuning == tuning.CivilianVehiclePool[0], "Actual spawn used a different set/model.");
                    var paint = go.GetComponentInChildren<VoxelTrafficPaint>();
                    var pieces = go.GetComponentsInChildren<MeshRenderer>().Where(r => r.sharedMaterial == paint.bodyMaterial).ToArray();
                    Check(pieces.Length > 0, "No paintable body pieces found.");
                    var block = new MaterialPropertyBlock();
                    foreach (var piece in pieces)
                    {
                        piece.GetPropertyBlock(block);
                        Check(block.GetColor("_BaseColor") == tuning.PaintColours[0], "Actual paint did not use assigned set.");
                    }
                    Object.DestroyImmediate(go);
                }
                first.civilianTrafficSet = null;
                Check(!first.HasCivilianVehicles && first.CivilianVehiclePool == null && first.PaintColours == null && first.ChooseCivilianVehicle() == null,
                    "Missing set must safely disable civilians.");
                first.civilianTrafficSet = shared;
                using (var scope = new VoxelCivilianTrafficSetScope(first))
                {
                    first.civilianTrafficSet.civilianVehiclePool = Array.Empty<VoxelEnemyVehicleTuning>();
                    first.civilianTrafficSet.paintColours = Array.Empty<Color>();
                    Check(!first.HasCivilianVehicles && shared.civilianVehiclePool.Length == 1 && shared.paintColours[0] == Color.yellow,
                        "Validation clone changed its source set.");
                }
                Check(first.civilianTrafficSet == shared && first.HasCivilianVehicles, "Validation scope did not restore reference.");
                Check(before == EditorJsonUtility.ToJson(source), "Validation edited the source asset.");
                Directory.CreateDirectory("Temp/CivilianTrafficSets");
                File.WriteAllText("Temp/CivilianTrafficSets/Validation.txt", "PASS: all twelve traffic tunings reference the shared six-model/six-colour default; removed embedded arrays; Create menu and inline Inspector/foldouts; shared model/palette edits reach both references; independent set reassignment stays isolated; actual spawned models and painted body renderers use assigned sets; missing reference disables civilians; validation clone isolation/reference restoration; source asset unchanged and RNG restored.");
                Debug.Log("PASS Shared Civilian Traffic Sets: shared editing, independent presets, real model/paint integration, Inspector and preview isolation.");
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(first); Object.DestroyImmediate(second);
                Object.DestroyImmediate(shared); Object.DestroyImmediate(separate);
                EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Random.state = random;
            }
        }
    }
}
