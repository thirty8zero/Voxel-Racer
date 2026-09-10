using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer.Editor
{
    /// <summary>Repeatable editor regression checks for independent door armour purchases.</summary>
    public static class VoxelArmorValidation
    {
        private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        private static void Check(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException("Armour validation: " + message);
        }

        [MenuItem("Tools/Voxel Racer/Validate Door Armour")]
        public static void Run()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Run armour validation outside Play Mode.");

            var missingField = typeof(VoxelCarRunState).GetField("missingVoxelPaths", PrivateStatic);
            var healthField = typeof(VoxelCarRunState).GetField("armorHealth", PrivateStatic);
            var nameField = typeof(VoxelCarRunState).GetField("carDefinitionName", PrivateStatic);
            var gunField = typeof(VoxelGunUpgradeState).GetField("purchasedLongGunCount", PrivateStatic);
            var rightField = typeof(VoxelArmorUpgradeState).GetField("rightPurchased", PrivateStatic);
            var leftField = typeof(VoxelArmorUpgradeState).GetField("leftPurchased", PrivateStatic);
            Check(missingField != null && healthField != null && nameField != null && gunField != null &&
                rightField != null && leftField != null, "state fields missing");

            var missing = (HashSet<string>)missingField.GetValue(null);
            var health = (Dictionary<string, int>)healthField.GetValue(null);
            var savedMissing = new HashSet<string>(missing);
            var savedHealth = new Dictionary<string, int>(health);
            object savedName = nameField.GetValue(null);
            object savedGuns = gunField.GetValue(null);
            object savedRight = rightField.GetValue(null);
            object savedLeft = leftField.GetValue(null);
            int savedBalance = VoxelCurrencyState.Balance;
            var priorEventSystem = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var testedTuning = VoxelArmorTuning.Load();
            Check(testedTuning != null, "tuning missing");
            int savedHitPoints = testedTuning.voxelHitPoints;
            int savedPrice = testedTuning.panelPurchasePrice;
            Scene scene = EditorSceneManager.NewPreviewScene();

            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>(
                    "Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                Check(definition != null && testedTuning.panelPrefab != null && testedTuning.Fits(definition),
                    "assets missing or incompatible");
                testedTuning.voxelHitPoints = Mathf.Max(2, testedTuning.voxelHitPoints);
                testedTuning.panelPurchasePrice = Mathf.Max(1, testedTuning.panelPurchasePrice);

                int baseCount = CountBaseIntegrity(definition);
                ValidatePurchaseAndPersistence(scene, definition, testedTuning, baseCount,
                    VoxelArmorSide.Right, VoxelArmorSide.Left);
                ValidatePurchaseAndPersistence(scene, definition, testedTuning, baseCount,
                    VoxelArmorSide.Left, VoxelArmorSide.Right);
                ValidateGunOrdering(scene, definition, testedTuning, baseCount);
                ValidateShopButtons(scene, definition, testedTuning, baseCount);

                VoxelCarRunState.BeginNewRun(definition);
                Check(!VoxelArmorUpgradeState.IsRightPurchased && !VoxelArmorUpgradeState.IsLeftPurchased &&
                    VoxelCarRunState.MissingVoxelCount == 0, "new run retained armour or damage");
                var normalCar = CreateCar(scene, definition);
                normalCar.damageVoxelsPerHit = 8;
                normalCar.ApplyDamage(new Vector3(0, .9f, 2f), Vector3.back);
                Check(normalCar.MissingIntegrityVoxels == 8, "normal body damage changed");

                Debug.Log("Armour validation passed: independent right/left purchases, per-panel cash limits, " +
                    "reverse-order damage persistence, 30 integrity voxels per panel, shielding, repairs, " +
                    "gun ordering, shop status, duplicate prevention and new-run reset.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                testedTuning.voxelHitPoints = savedHitPoints;
                testedTuning.panelPurchasePrice = savedPrice;
                missing.Clear();
                missing.UnionWith(savedMissing);
                health.Clear();
                foreach (var entry in savedHealth)
                    health.Add(entry.Key, entry.Value);
                nameField.SetValue(null, savedName);
                gunField.SetValue(null, savedGuns);
                rightField.SetValue(null, savedRight);
                leftField.SetValue(null, savedLeft);
                VoxelCurrencyState.Reset();
                VoxelCurrencyState.Add(savedBalance);
                if (priorEventSystem == null)
                {
                    var created = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
                    if (created != null)
                        UnityEngine.Object.DestroyImmediate(created.gameObject);
                }
            }
        }

        private static int CountBaseIntegrity(VoxelCarDefinition definition)
        {
            var contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(definition.visualPrefab));
            try { return VoxelCarSelectionState.CountIntegrityVoxels(contents); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void ValidatePurchaseAndPersistence(Scene scene, VoxelCarDefinition definition,
            VoxelArmorTuning tuning, int baseCount, VoxelArmorSide first, VoxelArmorSide second)
        {
            VoxelCarRunState.BeginNewRun(definition);
            var other = ScriptableObject.CreateInstance<VoxelCarDefinition>();
            try
            {
                Check(!VoxelArmorUpgradeState.CanPurchase(tuning, other, first), "wrong car accepted");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
            }

            VoxelCurrencyState.Add(tuning.panelPurchasePrice - 1);
            Check(!VoxelArmorUpgradeState.TryPurchase(tuning, definition, first), "unaffordable purchase succeeded");
            Check(VoxelCurrencyState.Balance == tuning.panelPurchasePrice - 1,
                "failed panel purchase charged cash");
            VoxelCurrencyState.Add(1);
            Check(VoxelArmorUpgradeState.TryPurchase(tuning, definition, first), "first panel purchase failed");
            Check(VoxelArmorUpgradeState.IsPurchasedFor(first) &&
                !VoxelArmorUpgradeState.IsPurchasedFor(second), "first panel ownership incorrect");

            var car = CreateCar(scene, definition);
            int panelCount = car.GetComponentsInChildren<VoxelArmorVoxel>(true).Length;
            Check(panelCount == 30 && car.TotalIntegrityVoxels == baseCount + 30,
                "first purchase did not add one panel to integrity");
            ValidateFit(car.transform);
            Transform firstVoxel = FindPanelVoxel(car.transform, first);
            firstVoxel.GetComponent<VoxelArmorVoxel>().AbsorbDamage(1);
            string pathBefore = SiblingPath(car.transform, firstVoxel);
            VoxelCarRunState.Capture(car, definition);

            VoxelCurrencyState.Add(tuning.panelPurchasePrice);
            Check(VoxelArmorUpgradeState.TryPurchase(tuning, definition, second),
                "second panel purchase failed");
            Check(!VoxelArmorUpgradeState.TryPurchase(tuning, definition, first),
                "duplicate first panel purchase accepted");
            Check(VoxelArmorUpgradeState.IsPurchasedFor(first) && VoxelArmorUpgradeState.IsPurchasedFor(second),
                "both panel purchases were not tracked");
            VoxelArmorUpgradeState.ApplyTo(car.transform, definition);
            car.ResetIntegrityBaseline();
            Check(car.GetComponentsInChildren<VoxelArmorVoxel>(true).Length == 60 &&
                car.TotalIntegrityVoxels == baseCount + 60 &&
                SiblingPath(car.transform, firstVoxel) == pathBefore,
                "opposite purchase changed existing panel path or integrity");
            VoxelCarRunState.Capture(car, definition);

            var restored = CreateCar(scene, definition);
            Transform restoredFirst = FindPanelVoxel(restored.transform, first);
            Check(restored.MissingIntegrityVoxels == 0 &&
                restoredFirst.GetComponent<VoxelArmorVoxel>().RemainingHealth == tuning.voxelHitPoints - 1 &&
                restoredFirst.gameObject.activeSelf &&
                restored.GetComponentsInChildren<VoxelArmorVoxel>(true).Length == 60,
                "damaged first panel failed round-trip after second purchase");
            Check(restored.RepairableIntegrityVoxels == 1 && restored.RepairPercent(100) == 1,
                "partial first-panel damage was not repairable");
            Check(restored.RepairableIntegrityVoxels == 0, "panel repair left damage behind");
        }

        private static void ValidateGunOrdering(Scene scene, VoxelCarDefinition definition,
            VoxelArmorTuning tuning, int baseCount)
        {
            var gun = VoxelGunUpgradeState.LongGunTuning;
            Check(gun != null && gun.visualPrefab != null, "gun tuning missing");

            // Armor first, then gun: the existing damaged panel must survive gun insertion.
            VoxelCarRunState.BeginNewRun(definition);
            VoxelCurrencyState.Add(tuning.panelPurchasePrice * 2);
            Check(VoxelArmorUpgradeState.TryPurchase(tuning, definition, VoxelArmorSide.Right),
                "right panel fixture failed");
            Check(VoxelArmorUpgradeState.TryPurchase(tuning, definition, VoxelArmorSide.Left),
                "left panel fixture failed");
            var armorFirst = CreateCar(scene, definition);
            Transform damaged = FindPanelVoxel(armorFirst.transform, VoxelArmorSide.Right);
            damaged.GetComponent<VoxelArmorVoxel>().AbsorbDamage(1);
            VoxelCarRunState.Capture(armorFirst, definition);
            VoxelCurrencyState.Add(gun.purchasePrice);
            Check(VoxelGunUpgradeState.TryPurchase(gun), "gun-after-armour fixture failed");
            VoxelGunUpgradeState.ApplyTo(armorFirst.transform, gun);
            VoxelCarRunState.Capture(armorFirst, definition);
            var restoredArmorFirst = CreateCar(scene, definition);
            Check(FindPanelVoxel(restoredArmorFirst.transform, VoxelArmorSide.Right)
                .GetComponent<VoxelArmorVoxel>().RemainingHealth == tuning.voxelHitPoints - 1 &&
                restoredArmorFirst.TotalIntegrityVoxels == baseCount + 60,
                "gun insertion lost armour damage");

            // Gun first, then both panels: adding armour after a gun must also round-trip.
            VoxelCarRunState.BeginNewRun(definition);
            VoxelCurrencyState.Add(gun.purchasePrice + tuning.panelPurchasePrice * 2);
            Check(VoxelGunUpgradeState.TryPurchase(gun), "gun-first fixture failed");
            var gunFirst = CreateCar(scene, definition);
            Check(VoxelArmorUpgradeState.TryPurchase(tuning, definition, VoxelArmorSide.Left),
                "left panel after gun fixture failed");
            VoxelArmorUpgradeState.ApplyTo(gunFirst.transform, definition);
            gunFirst.ResetIntegrityBaseline();
            Transform left = FindPanelVoxel(gunFirst.transform, VoxelArmorSide.Left);
            left.GetComponent<VoxelArmorVoxel>().AbsorbDamage(1);
            Check(VoxelArmorUpgradeState.TryPurchase(tuning, definition, VoxelArmorSide.Right),
                "right panel after gun fixture failed");
            VoxelArmorUpgradeState.ApplyTo(gunFirst.transform, definition);
            gunFirst.ResetIntegrityBaseline();
            VoxelCarRunState.Capture(gunFirst, definition);
            var restoredGunFirst = CreateCar(scene, definition);
            Check(FindPanelVoxel(restoredGunFirst.transform, VoxelArmorSide.Left)
                .GetComponent<VoxelArmorVoxel>().RemainingHealth == tuning.voxelHitPoints - 1 &&
                restoredGunFirst.TotalIntegrityVoxels == baseCount + 60,
                "armour added after gun lost damage or integrity");
        }

        private static void ValidateShopButtons(Scene scene, VoxelCarDefinition definition,
            VoxelArmorTuning tuning, int baseCount)
        {
            VoxelCarRunState.BeginNewRun(definition);
            var car = CreateCar(scene, definition);
            car.damageVoxelsPerHit = 8;
            car.ApplyDamage(new Vector3(0, .9f, 2f), Vector3.back);
            var shopObject = new GameObject("Armour Shop Button Test");
            SceneManager.MoveGameObjectToScene(shopObject, scene);
            var shop = shopObject.AddComponent<VoxelRepairUpgradeSceneController>();
            var shopType = typeof(VoxelRepairUpgradeSceneController);
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            shopType.GetField("definition", instance).SetValue(shop, definition);
            shopType.GetProperty("DisplayedCar").SetValue(shop, car);
            shop.repairTuning = VoxelRepairTuning.Load();
            shopType.GetMethod("BuildUi", instance).Invoke(shop, null);
            VoxelCurrencyState.Add(tuning.panelPurchasePrice * 2);
            shopType.GetMethod("RefreshUi", instance).Invoke(shop, null);
            var rightButton = shopObject.GetComponentsInChildren<Button>(true)
                .First(button => button.name == "Right Door Armor Purchase Button");
            var leftButton = shopObject.GetComponentsInChildren<Button>(true)
                .First(button => button.name == "Left Door Armor Purchase Button");
            Check(rightButton.interactable && leftButton.interactable, "both affordable shop buttons disabled");
            rightButton.onClick.Invoke();
            Check(VoxelArmorUpgradeState.IsRightPurchased && !VoxelArmorUpgradeState.IsLeftPurchased &&
                !rightButton.interactable && leftButton.interactable &&
                car.TotalIntegrityVoxels == baseCount + 30 && car.MissingIntegrityVoxels == 8 &&
                rightButton.GetComponentInChildren<Text>().text.Contains("INSTALLED"),
                "right shop purchase did not install independently");
            leftButton.onClick.Invoke();
            Check(VoxelArmorUpgradeState.IsLeftPurchased && !leftButton.interactable &&
                car.TotalIntegrityVoxels == baseCount + 60 && car.MissingIntegrityVoxels == 8 &&
                leftButton.GetComponentInChildren<Text>().text.Contains("INSTALLED"),
                "left shop purchase did not preserve damage or integrity");
        }

        private static VoxelCarController CreateCar(Scene scene, VoxelCarDefinition definition)
        {
            var root = new GameObject("Armour Test Car");
            SceneManager.MoveGameObjectToScene(root, scene);
            UnityEngine.Object.Instantiate(definition.visualPrefab, root.transform);
            var car = root.AddComponent<VoxelCarController>();
            car.enabled = false;
            car.debrisVoxelsPerDamagedVoxel = 0;
            VoxelGunUpgradeState.ApplyTo(root.transform, VoxelGunUpgradeState.LongGunTuning);
            VoxelArmorUpgradeState.ApplyTo(root.transform, definition);
            car.ResetIntegrityBaseline();
            VoxelCarRunState.Apply(car, definition);
            return car;
        }

        private static Transform FindPanelVoxel(Transform car, VoxelArmorSide side)
        {
            string mount = side == VoxelArmorSide.Right
                ? VoxelArmorUpgradeState.RightMountName
                : VoxelArmorUpgradeState.LeftMountName;
            Transform panel = car.Find(VoxelArmorUpgradeState.UpgradeRootName + "/" + mount + "/Door Armor Panel");
            Check(panel != null, side + " panel missing");
            Transform voxel = panel.GetComponentInChildren<VoxelArmorVoxel>(true)?.transform;
            Check(voxel != null, side + " panel has no voxels");
            return voxel;
        }

        private static string SiblingPath(Transform root, Transform child)
        {
            var indices = new List<int>();
            Transform current = child;
            while (current != null && current != root)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }
            indices.Reverse();
            return string.Join("/", indices);
        }

        public static void ValidateFit(Transform car)
        {
            var armor = car.GetComponentsInChildren<VoxelArmorVoxel>(true);
            var renderers = car.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var voxel in armor)
            {
                Bounds bounds = voxel.GetComponent<Renderer>().bounds;
                foreach (var renderer in renderers)
                {
                    if (renderer.transform == voxel.transform || renderer.GetComponent<VoxelArmorVoxel>() != null)
                        continue;
                    Vector3 overlap = Vector3.Min(bounds.max, renderer.bounds.max) -
                        Vector3.Max(bounds.min, renderer.bounds.min);
                    Check(overlap.x < .0001f || overlap.y < .0001f || overlap.z < .0001f,
                        "panel intersects " + renderer.name);
                }
            }
        }

        [MenuItem("Tools/Voxel Racer/Render Armour Shop QA")]
        public static void RenderShopQa()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Render QA outside Play Mode.");
            var root = new GameObject("Temporary Armour Shop QA");
            var oldEventSystem = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var oldMode = RenderSettings.ambientMode;
            var oldAmbient = RenderSettings.ambientLight;
            try
            {
                var definition = AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>(
                    "Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                var carRoot = new GameObject("QA Car");
                carRoot.transform.SetParent(root.transform, false);
                carRoot.transform.rotation = Quaternion.Euler(0, 24, 0);
                UnityEngine.Object.Instantiate(definition.visualPrefab, carRoot.transform);
                var car = carRoot.AddComponent<VoxelCarController>();
                car.enabled = false;
                VoxelArmorUpgradeState.CreatePair(carRoot.transform, VoxelArmorTuning.Load());
                car.ResetIntegrityBaseline();
                var shop = root.AddComponent<VoxelRepairUpgradeSceneController>();
                shop.repairTuning = AssetDatabase.LoadAssetAtPath<VoxelRepairTuning>(
                    "Assets/Resources/VoxelRepairTuning.asset");
                var type = typeof(VoxelRepairUpgradeSceneController);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetField("definition", flags).SetValue(shop, definition);
                type.GetProperty("DisplayedCar").SetValue(shop, car);
                type.GetMethod("BuildUi", flags).Invoke(shop, null);
                type.GetMethod("RefreshUi", flags).Invoke(shop, null);
                var cameraObject = new GameObject("QA Camera");
                cameraObject.transform.SetParent(root.transform);
                var camera = cameraObject.AddComponent<Camera>();
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.65f, .58f, .46f);
                var cameraTuning = VoxelRepairUpgradeTuning.Load();
                camera.transform.position = cameraTuning.cameraPosition;
                camera.transform.LookAt(cameraTuning.cameraLookAt);
                camera.fieldOfView = cameraTuning.cameraFieldOfView;
                var canvas = root.GetComponentInChildren<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                for (int i = 0; i < 2; i++)
                {
                    var lightObject = new GameObject("QA Light");
                    lightObject.transform.SetParent(root.transform);
                    var light = lightObject.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = i == 0 ? 1 : .5f;
                    light.cullingMask = 1 << 31;
                    lightObject.transform.rotation = Quaternion.Euler(40, i == 0 ? -35 : 145, 0);
                }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.4f, .4f, .4f);
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 31;
                foreach (int width in new[] { 1920, 2400 })
                {
                    var renderTexture = new RenderTexture(width, 1080, 24);
                    var texture = new Texture2D(width, 1080, TextureFormat.RGB24, false);
                    try
                    {
                        camera.targetTexture = renderTexture;
                        Canvas.ForceUpdateCanvases();
                        camera.Render();
                        camera.Render();
                        RenderTexture.active = renderTexture;
                        texture.ReadPixels(new Rect(0, 0, width, 1080), 0, 0);
                        texture.Apply();
                        Directory.CreateDirectory("Temp");
                        File.WriteAllBytes("Temp/ArmorShop_" + width + ".png", texture.EncodeToPNG());
                    }
                    finally
                    {
                        camera.targetTexture = null;
                        RenderTexture.active = null;
                        renderTexture.Release();
                        UnityEngine.Object.DestroyImmediate(renderTexture);
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
                Debug.Log("Rendered ArmourShop at 16:9 and 20:9.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                RenderSettings.ambientMode = oldMode;
                RenderSettings.ambientLight = oldAmbient;
                if (oldEventSystem == null)
                {
                    var created = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
                    if (created != null)
                        UnityEngine.Object.DestroyImmediate(created.gameObject);
                }
            }
        }
    }
}
