using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelCivilianDrivelineValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }

        [MenuItem("Tools/Voxel Racer/Validate Civilian Axles and Drivelines")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Temporary civilian driveline validation");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(10000, 10000, 10000);
            var random = UnityEngine.Random.state;
            var traffic = Object.Instantiate(VoxelObstacleCarTuning.Load());
            var vehicle = Object.Instantiate(Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/TrafficCarTuning"));
            try
            {
                var player = root.AddComponent<VoxelCarController>(); player.enabled = false;
                vehicle.vehicleHealth = 1000; vehicle.voxelHealth = 1000;
                traffic.civilianVehiclePool = new[] { vehicle };
                foreach (string path in VoxelCivilianDrivelineBuilder.PrefabPaths)
                {
                    vehicle.modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Check(vehicle.modelPrefab != null, "Missing civilian prefab: " + path);
                    var copy = Object.Instantiate(vehicle.modelPrefab, root.transform);
                    int originalCount = copy.GetComponentsInChildren<MeshRenderer>().Length;
                    var structure = copy.transform.Find(VoxelCivilianDrivelineBuilder.StructureName);
                    Check(structure != null && structure.GetComponent<VoxelIndestructiblePart>() != null, "Missing protected driveline: " + path);
                    Check(structure.GetComponentsInChildren<MeshRenderer>().Length == 3, "Driveline must use three pieces");
                    var axles = structure.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.EndsWith("Axle")).ToArray();
                    var shaft = structure.Find("Driveline").GetComponent<MeshRenderer>();
                    Check(axles.Length == 2 && axles.All(r => r.bounds.Intersects(shaft.bounds)), "Driveline does not join both axles");
                    foreach (var wheel in copy.GetComponentsInChildren<Transform>().Where(t => t.name == "Obstacle Voxel Wheel"))
                    {
                        Vector3 innerPoint = wheel.position + copy.transform.right * (wheel.localPosition.x < 0 ? .12f : -.12f);
                        Check(axles.Any(r => r.bounds.Contains(innerPoint)), "Axle misses wheel centre: " + path);
                        Check(wheel.GetComponentInParent<VoxelIndestructiblePart>() == null, "Wheel protection was changed");
                    }
                    VoxelCivilianDrivelineBuilder.AddStructure(copy);
                    VoxelCivilianDrivelineBuilder.AddStructure(copy);
                    Check(copy.GetComponentsInChildren<MeshRenderer>().Length == originalCount, "Updating driveline duplicated geometry");
                    Object.DestroyImmediate(copy);

                    foreach (bool sameDirection in new[] { true, false })
                        for (int damageStyle = 0; damageStyle < 4; damageStyle++)
                        {
                            var go = new GameObject("Civilian structure damage test"); go.transform.SetParent(root.transform, false);
                            var car = go.AddComponent<VoxelObstacleCar>(); car.enabled = false;
                            try
                            {
                                car.Configure(player, traffic, sameDirection, null, 70, 3, 12);
                                go.transform.localRotation = Quaternion.Euler(0, sameDirection ? 37 : 180, 0);
                                var pieces = go.GetComponentsInChildren<MeshRenderer>();
                                var protectedPieces = pieces.Where(r => r.GetComponentInParent<VoxelIndestructiblePart>() != null).ToArray();
                                var body = pieces.Where(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null).ToArray();
                                Check(protectedPieces.Length == 3 && body.Length > 300, "Protected/body split is wrong");
                                Check(go.GetComponentsInChildren<Collider>().Length == 0, "Driveline added colliders");
                                foreach (var r in protectedPieces)
                                {
                                    car.TakeProjectileHit(r.transform, 5000, r.bounds.center, Vector3.forward);
                                    car.TakeHostileProjectileHit(r.transform, 5000, r.bounds.center, Vector3.forward);
                                }
                                Check(car.CurrentHealth == 1000, "Protected bullet hit damaged vehicle health");
                                car.TakeProjectileHit(body[0].transform, .25f, body[0].bounds.center, Vector3.forward);
                                Check(car.CurrentHealth == 999.75f, "Ordinary body bullet damage changed");
                                if (damageStyle == 3)
                                    car.TakeMissileBlast(go.transform.position, 10, 1, Vector3.forward, pieces);
                                else
                                {
                                    // Exercise the actual ram/weapon/death-explosion selectors without editor debris.
                                    typeof(VoxelObstacleCar).GetField("<EnemyTuning>k__BackingField", Private).SetValue(car, null);
                                    var damage = typeof(VoxelObstacleCar).GetMethod("ApplyVoxelDamage", Private);
                                    var style = Enum.ToObject(damage.GetParameters()[3].ParameterType, damageStyle);
                                    int removed = (int)damage.Invoke(car, new object[] { protectedPieces[0].bounds.center, Vector3.forward, int.MaxValue, style });
                                    Check(removed == body.Length, "Damage counted protected pieces");
                                    Check((int)damage.Invoke(car, new object[] { go.transform.position, Vector3.forward, int.MaxValue, style }) == 0,
                                        "Damage counted removed pieces again");
                                }
                                Check(body.All(r => !r.gameObject.activeSelf), "Destructible shell survived complete removal");
                                Check(protectedPieces.All(r => r != null && r.enabled && r.gameObject.activeInHierarchy), "Damage removed an axle or driveline");
                                car.enabled = true;
                                Check(!VoxelObstacleCar.TryFindProjectileHit(go.transform.TransformPoint(new Vector3(4, .43f, 0)),
                                    -go.transform.right, 8, out _, out _, out _, out _), "Protected driveline caught a bullet");
                            }
                            finally { Object.DestroyImmediate(go); }
                        }
                    Debug.Log("PASS civilian driveline: " + path + "; " + originalCount + " total pieces, three protected pieces; aligned wheels, connected shaft, idempotent authoring, both traffic directions, rotated models, player/hostile bullets, ram and explosion selection, missile blast, complete shell removal.");
                }
                Directory.CreateDirectory("Temp/CivilianDrivelines");
                File.WriteAllText("Temp/CivilianDrivelines/Validation.txt", "PASS all " + VoxelCivilianDrivelineBuilder.PrefabPaths.Length + " civilians: aligned protected axles and driveline, three pieces each, idempotent update, no colliders, wheels remain destructible; both directions and rotated models; player/hostile bullet immunity; ordinary body damage; ram/weapon/explosion selection and missile blasts preserve structure after complete body removal. Edit Mode.");
            }
            finally
            {
                Object.DestroyImmediate(vehicle); Object.DestroyImmediate(traffic);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Random.state = random;
            }
            Render();
        }

        [MenuItem("Tools/Voxel Racer/Render Civilian Axles and Drivelines")]
        public static void Render()
        {
            Directory.CreateDirectory("Temp/CivilianDrivelines");
            foreach (string path in VoxelCivilianDrivelineBuilder.PrefabPaths)
            {
                var preview = new PreviewRenderUtility();
                try
                {
                    var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        bool wheel = false;
                        for (var t = r.transform; t != model.transform; t = t.parent)
                            if (t.name == "Obstacle Voxel Wheel") wheel = true;
                        r.enabled = wheel || r.GetComponentInParent<VoxelIndestructiblePart>() != null;
                    }
                    preview.AddSingleGO(model);
                    preview.camera.transform.position = new Vector3(4, 2.5f, 6);
                    preview.camera.transform.LookAt(new Vector3(0, .42f, 0));
                    preview.camera.fieldOfView = 35; preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 50;
                    preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.25f, .28f, .31f);
                    preview.lights[0].intensity = 2; preview.lights[0].transform.rotation = Quaternion.Euler(40, 25, 0);
                    preview.lights[1].intensity = 1.3f; preview.lights[1].transform.rotation = Quaternion.Euler(30, 210, 0);
                    preview.ambientColor = new Color(.5f, .5f, .5f);
                    preview.BeginStaticPreview(new Rect(0, 0, 1000, 650)); preview.Render(true);
                    var image = preview.EndStaticPreview();
                    File.WriteAllBytes("Temp/CivilianDrivelines/" + Path.GetFileNameWithoutExtension(path) + ".png", image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                finally { preview.Cleanup(); }
            }
        }
    }
}
