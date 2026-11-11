using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelGoldTrackWheelValidation
    {
        private const string Output = "Temp/GoldTrackWheels/";

        [MenuItem("Tools/Voxel Racer/Validate and Render Gold Track Wheels")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Gold Track Wheel Validation");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                var sports = VoxelPerformanceWheelTuning.Load();
                var gold = VoxelPerformanceWheelTuning.LoadGold();
                var entries = VoxelUpgradeFitCatalog.Discover();
                var goldEntries = entries.Where(entry => entry.Asset == gold).ToArray();
                Check(gold != null && gold != sports && gold.Fits(definition) && goldEntries.Length == 3,
                    "Gold tuning or its three fit choices are missing.");
                Check(VoxelUpgradeFitCatalog.Assets<VoxelCarDefinition>().All(car =>
                    gold.Fits(car) == (car.visualPrefab == sports.compatibleCarPrefab)), "Gold compatibility differs from Sports Wheels.");
                Check(VoxelUpgradeDashboardCatalog.Discover().Count(entry => entry.Asset == gold && entry.Category == "Wheels") == 1,
                    "Gold wheels must appear once in the dashboard.");

                var prefab = gold.wheelPrefab;
                var mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
                var sportsDiameter = sports.wheelPrefab.GetComponentsInChildren<MeshRenderer>().Max(r => r.bounds.max.y) -
                    sports.wheelPrefab.GetComponentsInChildren<MeshRenderer>().Min(r => r.bounds.min.y);
                Check(mesh.bounds.size.y > sportsDiameter && mesh.bounds.size.y <= sportsDiameter * 1.06f,
                    "The new tyre should be slightly larger than the Sports Wheel.");
                Check(Mathf.Abs(mesh.bounds.size.x - .302f * 1.5f) < .0001f &&
                    Mathf.Abs(mesh.bounds.min.x + prefab.transform.localPosition.x + .151f) < .0001f,
                    "The wheel must be 50% wider with the same inboard edge.");
                Check(prefab.GetComponentsInChildren<MeshRenderer>().Length == 1 && mesh.subMeshCount == 4 &&
                    mesh.triangles.Length / 3 < 3100 && prefab.GetComponentsInChildren<Collider>().Length == 0,
                    "Wheel exceeded its renderer, geometry or physics budget.");
                var sportsTabs = sports.wheelPrefab.transform.Cast<Transform>()
                    .Where(child => child.name == "White Sidewall Panel").Select(child => child.localPosition).ToArray();
                CheckTabs(sportsTabs, 1, 3);
                CheckGoldBrakeAndTabs(prefab.GetComponent<MeshRenderer>(), mesh);

                int cash = VoxelCurrencyState.Balance;
                int missing = VoxelCarRunState.MissingVoxelCount;
                var owned = VoxelPerformanceWheelUpgradeState.InstalledTuning;
                foreach (var entry in goldEntries)
                {
                    var car = Object.Instantiate(definition.visualPrefab, root.transform);
                    entry.Build(car.transform);
                    bool left = entry.Label.Contains("Left wheels"), right = entry.Label.Contains("Right wheels");
                    var mounts = car.GetComponentsInChildren<VoxelPerformanceWheelMount>();
                    Check(mounts.Length == (left || right ? 2 : 4) && mounts.All(m =>
                        (!left || m.transform.position.x < 0) && (!right || m.transform.position.x > 0)), "Incorrect preview sides.");
                    VerifyMounts(car, gold, mounts.Length);
                }

                var mounted = Object.Instantiate(definition.visualPrefab, root.transform);
                var baseRenderers = mounted.GetComponentsInChildren<MeshRenderer>(true);
                VoxelPerformanceWheelUpgradeState.CreateVisuals(mounted.transform, sports);
                var sportsMounts = VerifyMounts(mounted, sports);
                Render(mounted, "Sports", new Vector3(6, 2.3f, 3.8f));
                Render(mounted, "SportsRear", new Vector3(-6, 2.3f, -3.8f));
                RenderFrontWheel(mounted, sportsMounts, "SportsMountedDetail");
                var earlySpikes = sportsMounts.Select(m => VoxelWheelSpikeUpgradeState.CreateVisual(
                    mounted.transform, m.transform.parent, VoxelWheelSpikeTuning.LoadStar())).ToArray();
                foreach (var entry in goldEntries) entry.Build(mounted.transform);
                var goldMounts = VerifyMounts(mounted, gold);
                Check(earlySpikes.All(spike => spike.transform.localPosition ==
                    spike.transform.parent.Find(VoxelPerformanceWheelUpgradeState.InstanceName).localPosition),
                    "Replacing Sports Wheels failed to move previously fitted spikes to the new face.");
                foreach (var spike in earlySpikes) spike.SetActive(false);
                VoxelPerformanceWheelUpgradeState.CreateVisuals(mounted.transform, sports);
                VoxelPerformanceWheelUpgradeState.CreateVisuals(mounted.transform, gold);
                Check(goldMounts.SequenceEqual(VerifyMounts(mounted, gold)), "Repeated or lower-tier application replaced the wheel.");
                float radius = mesh.vertices.Max(v => new Vector2(v.y, v.z).magnitude);
                CheckArchClearance(mounted, goldMounts, mesh.bounds, radius);
                Render(mounted, "Mounted", new Vector3(6, 2.3f, 3.8f));
                Render(mounted, "MountedRear", new Vector3(-6, 2.3f, -3.8f));
                RenderFrontWheel(mounted, goldMounts, "MountedDetail");
                Render(prefab, "WheelDetail", new Vector3(3, .65f, 1.1f), Vector3.zero, .56f);
                Render(prefab, "WheelInboard", new Vector3(-3, .65f, 1.1f), Vector3.zero, .56f);

                foreach (var entry in entries.Where(entry => entry.Fits(definition))) entry.Build(mounted.transform);
                foreach (var spike in mounted.GetComponentsInChildren<VoxelWheelSpikeMount>(true))
                    spike.gameObject.SetActive(true);
                VerifyMounts(mounted, gold);
                var spikes = mounted.GetComponentsInChildren<VoxelWheelSpikeMount>();
                Check(spikes.Length == 4 && spikes.All(spike =>
                    spike.transform.localPosition == spike.transform.parent.Find(VoxelPerformanceWheelUpgradeState.InstanceName).localPosition),
                    "Combined fit shifted the wheel spikes off their hubs.");
                Render(mounted, "Combined", new Vector3(6, 2.3f, 3.8f));
                foreach (var renderer in baseRenderers)
                    if (renderer.GetComponentInParent<VoxelIndestructiblePart>() == null) renderer.enabled = false;
                Render(mounted, "BodyHidden", new Vector3(-6, 2.3f, -3.8f));
                foreach (var renderer in baseRenderers) renderer.enabled = false;
                Render(mounted, "UpgradesOnly", new Vector3(6, 2.3f, 3.8f));
                Check(cash == VoxelCurrencyState.Balance && missing == VoxelCarRunState.MissingVoxelCount &&
                    owned == VoxelPerformanceWheelUpgradeState.InstalledTuning, "Fit checks changed run state.");
                CheckDirectPurchase(definition, gold);
                File.WriteAllText(Output + "Validation.txt", "PASS: 50% wider with the same inboard edge, diameter, 13-degree tab spacing on both variants, Gold white caliper, arch clearance, both wheel orientations, " +
                    "side-specific fit, exclusive and idempotent wheel replacement, combined fit with spikes, dashboard and preview discovery, " +
                    "preview state isolation, direct purchase, affordability, and new-run reset. " +
                    $"{mesh.triangles.Length / 3} triangles, one renderer and four shared materials per wheel.");
                Debug.Log(File.ReadAllText(Output + "Validation.txt"));
            }
            catch (Exception ex) { File.WriteAllText(Output + "Validation.txt", "FAIL: " + ex); throw; }
            finally
            {
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(scene);
                UnityEngine.Random.state = random;
            }
        }

        private static void CheckDirectPurchase(VoxelCarDefinition definition, VoxelPerformanceWheelTuning gold)
        {
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            VoxelCarRunState.BeginNewRun(definition);
            if (gold.purchasePrice > 0)
                Check(!VoxelPerformanceWheelUpgradeState.TryPurchase(gold, definition), "Gold wheels accepted an unaffordable purchase.");
            VoxelCurrencyState.Add(gold.purchasePrice);
            Check(VoxelPerformanceWheelUpgradeState.TryPurchase(gold, definition) &&
                VoxelPerformanceWheelUpgradeState.InstalledTuning == gold && VoxelCurrencyState.Balance == 0,
                "Direct gold purchase failed or charged the wrong amount.");
            VoxelCarRunState.BeginNewRun(definition);
            Check(VoxelPerformanceWheelUpgradeState.InstalledTuning == null, "New run retained the installed gold wheels.");
        }

        internal static VoxelPerformanceWheelMount[] VerifyMounts(GameObject car, VoxelPerformanceWheelTuning tuning, int count = 4)
        {
            var mounts = car.GetComponentsInChildren<VoxelPerformanceWheelMount>();
            Check(mounts.Length == count && mounts.All(m => m.tuning == tuning), "Wrong or duplicate wheel mounts.");
            foreach (var mount in mounts)
            {
                var wheel = mount.transform.parent;
                float side = Mathf.Sign(car.transform.InverseTransformPoint(wheel.position).x);
                Check(wheel.name == "Voxel Wheel" && mount.GetComponentInParent<VoxelIndestructiblePart>() != null &&
                    Vector3.Dot(mount.transform.right, wheel.right * side) > .99f &&
                    Mathf.Approximately(mount.transform.localPosition.x, side * (VoxelPerformanceWheelUpgradeState.OutwardOffset +
                        tuning.wheelPrefab.transform.localPosition.x)),
                    "A wheel lost its protected, outward-facing axle mount.");
                Check(wheel.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.transform != mount.transform &&
                    renderer.transform.GetComponentInParent<VoxelWheelSpikeMount>(true) == null).All(renderer =>
                    !renderer.enabled || renderer.transform.IsChildOf(mount.transform)), "Stock tyre rendered through its replacement.");
            }
            return mounts;
        }

        private static void CheckGoldBrakeAndTabs(MeshRenderer renderer, Mesh mesh)
        {
            var points = mesh.vertices;
            var white = mesh.GetTriangles(3);
            Check(renderer.sharedMaterials[3] == Resources.Load<Material>("CarMaterials/PerformanceTyreWhite"),
                "Gold tabs and caliper must use the shared white material.");
            const int tabCount = 20, indicesPerTab = 36;
            var tabIndices = white.Where(i => Mathf.Abs(points[i].x) > .14f).ToArray();
            Check(tabIndices.Length == tabCount * indicesPerTab, "Expected twenty white tabs across both tyre faces.");
            var centres = new Vector3[tabCount];
            for (int tab = 0; tab < tabCount; tab++)
            {
                var box = new Bounds(points[tabIndices[tab * indicesPerTab]], Vector3.zero);
                foreach (int index in tabIndices.Skip(tab * indicesPerTab).Take(indicesPerTab))
                    box.Encapsulate(points[index]);
                centres[tab] = box.center;
            }
            CheckTabs(centres.Where(p => p.x > 0).ToArray(), 1, 5);
            CheckTabs(centres.Where(p => p.x < 0).ToArray(), -1, 5);
            var caliper = white.Where(i => Mathf.Abs(points[i].x) <= .14f).Select(i => points[i]).ToArray();
            Check(caliper.Length > 0 && caliper.Max(v => v.x) < .025f && caliper.Max(v => v.z) < -.17f &&
                caliper.Min(v => v.z) < -.28f && caliper.Min(v => v.x) < -.074f,
                "The white caliper must straddle the rotor behind the spokes.");
        }

        private static void CheckTabs(Vector3[] centres, int side, int countPerArc)
        {
            Check(centres.Length == countPerArc * 2, "Wrong number of sidewall tabs.");
            foreach (int arc in new[] { 0, 180 })
                for (int step = -countPerArc / 2; step <= countPerArc / 2; step++)
                {
                    float angle = (arc + step * 13f) * Mathf.Deg2Rad;
                    var expected = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .398f;
                    Check(centres.Count(p => p.x * side > .14f &&
                        Vector2.Distance(new Vector2(p.y, p.z), expected) < .0002f) == 1,
                        "Sidewall tabs must retain the original 13-degree pitch and radius.");
                }
        }

        private static void RenderFrontWheel(GameObject car, VoxelPerformanceWheelMount[] mounts, string name)
        {
            var front = mounts.First(m => m.transform.position.x > 0 && m.transform.position.z > 0).transform.position;
            Render(car, name, front + new Vector3(3.5f, .65f, 1.1f), front, .70f);
        }

        private static void CheckArchClearance(GameObject car, VoxelPerformanceWheelMount[] mounts, Bounds meshBounds, float radius)
        {
            foreach (var mount in mounts)
            {
                var centre = mount.transform.position;
                Check(centre.y >= radius, "The tyre crown clips through the road in a full revolution.");
                float a = mount.transform.TransformPoint(Vector3.right * meshBounds.min.x).x;
                float b = mount.transform.TransformPoint(Vector3.right * meshBounds.max.x).x;
                foreach (var renderer in car.GetComponentsInChildren<MeshRenderer>().Where(r =>
                    r.enabled && r.GetComponentInParent<VoxelIndestructiblePart>() == null))
                {
                    Bounds bounds = renderer.bounds;
                    if (Mathf.Max(a, b) <= bounds.min.x || Mathf.Min(a, b) >= bounds.max.x) continue;
                    var closest = bounds.ClosestPoint(centre);
                    Check(new Vector2(closest.y - centre.y, closest.z - centre.z).magnitude > radius,
                        "A wheel intersects bodywork at its resting steering angle.");
                }
            }
        }

        private static void Render(GameObject source, string name, Vector3 position, Vector3? centre = null, float size = 2.20f)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(Object.Instantiate(source));
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = size;
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = 50;
                preview.camera.transform.position = position;
                preview.camera.transform.LookAt(centre ?? new Vector3(0, .75f, 0));
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.17f, .20f, .24f);
                preview.ambientColor = new Color(.52f, .52f, .52f);
                float yaw = preview.camera.transform.eulerAngles.y;
                preview.lights[0].intensity = 1.65f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, yaw - 30, 0);
                preview.lights[1].intensity = .85f;
                preview.lights[1].transform.rotation = Quaternion.Euler(12, yaw + 62, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 1200, 800));
                preview.Render(true);
                var image = preview.EndStaticPreview();
                try { File.WriteAllBytes(Output + name + ".png", image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
            finally { preview.Cleanup(); }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
