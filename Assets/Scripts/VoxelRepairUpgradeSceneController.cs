using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Builds the desert workshop, damaged car display, repair controls, and next-race flow.</summary>
    public sealed class VoxelRepairUpgradeSceneController : MonoBehaviour
    {
        public string raceSceneName = "SampleScene";
        public VoxelRepairTuning repairTuning;
        public VoxelRepairUpgradeTuning cameraTuning;

        public VoxelCarController DisplayedCar { get; private set; }
        public Button NextRaceButton { get; private set; }

        private VoxelCarDefinition definition;
        private VoxelCarIntegrityDisplay integrityDisplay;
        private Text currencyText;
        private Text feedbackText;
        private Text gunUpgradeButtonLabel;
        private Button gunUpgradeButton;
        private Text repair10ButtonLabel;
        private Text repair25ButtonLabel;
        private Text repair50ButtonLabel;
        private Text fullRepairButtonLabel;
        private Camera workshopCamera;
        private Vector3 appliedCameraPosition;
        private Vector3 appliedCameraLookAt;
        private float appliedCameraFieldOfView;
        private bool isLoading;

        private void Awake()
        {
            definition = VoxelCarSelectionState.GetSelectedOrDefault();
            repairTuning = repairTuning != null ? repairTuning : VoxelRepairTuning.Load();
            if (repairTuning == null)
                repairTuning = ScriptableObject.CreateInstance<VoxelRepairTuning>();
            cameraTuning = cameraTuning != null ? cameraTuning : VoxelRepairUpgradeTuning.Load();
            if (cameraTuning == null)
                cameraTuning = ScriptableObject.CreateInstance<VoxelRepairUpgradeTuning>();

            VoxelRacerBootstrap.ReloadGeneratedMaterials();
            BuildWorkshop();
            BuildUi();
            RefreshUi();
        }

        private void Update()
        {
            if (cameraTuning != null &&
                (cameraTuning.cameraPosition != appliedCameraPosition ||
                 cameraTuning.cameraLookAt != appliedCameraLookAt ||
                 !Mathf.Approximately(cameraTuning.cameraFieldOfView, appliedCameraFieldOfView)))
                ApplyCameraTuning();
        }

        private void BuildWorkshop()
        {
            Transform workshop = new GameObject("Desert Repair Workshop").transform;
            workshop.SetParent(transform, false);

            VoxelRacerBootstrap.CreateBlock("Brown Ground", workshop,
                new Vector3(0f, -0.28f, 0f), new Vector3(80f, 0.5f, 80f),
                VoxelRacerBootstrap.GroundMaterial);

            Material poleMaterial = CreateMaterial("Tent Poles", new Color(0.24f, 0.11f, 0.045f));
            Material roofMaterial = CreateMaterial("Tent Canvas", new Color(0.88f, 0.52f, 0.20f));
            BuildTent(workshop, poleMaterial, roofMaterial);
            BuildCacti(workshop);
            BuildCar(workshop);
            SetupCamera();
            VoxelRacerBootstrap.SetupLighting();
            VoxelRacerBootstrap.SetupSky(DisplayedCar.transform);
        }

        private void BuildCar(Transform workshop)
        {
            Transform car = new GameObject("Workshop Player Car").transform;
            car.SetParent(workshop, false);
            car.localPosition = Vector3.zero;
            car.localRotation = Quaternion.Euler(0f, 24f, 0f);

            if (definition != null && definition.visualPrefab != null)
            {
                GameObject visual = Instantiate(definition.visualPrefab, car);
                visual.name = definition.displayName + " Workshop Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
            }
            else
            {
                VoxelRacerBootstrap.CreateDetailedPlayerCarVisuals(car);
            }

            DisplayedCar = car.gameObject.AddComponent<VoxelCarController>();
            if (definition != null && definition.tuning != null)
                DisplayedCar.SetTuning(definition.tuning);
            DisplayedCar.ResetIntegrityBaseline();
            VoxelCarRunState.Apply(DisplayedCar, definition);
            VoxelGunUpgradeState.ApplyTo(car, VoxelGunUpgradeState.LongGunTuning);
            DisplayedCar.enabled = false;

            // Reuse the same radial integrity widget used during missions. Its
            // built-in top-left anchors keep the workshop view consistent with the HUD.
            integrityDisplay = gameObject.AddComponent<VoxelCarIntegrityDisplay>();
            integrityDisplay.target = DisplayedCar;
        }

        private static void BuildTent(Transform workshop, Material poleMaterial, Material roofMaterial)
        {
            Vector3[] polePositions =
            {
                new Vector3(-3.5f, 1.8f, -3.3f),
                new Vector3(3.5f, 1.8f, -3.3f),
                new Vector3(-3.5f, 1.8f, 3.3f),
                new Vector3(3.5f, 1.8f, 3.3f)
            };

            for (int index = 0; index < polePositions.Length; index++)
                VoxelRacerBootstrap.CreateBlock("Tent Pole " + (index + 1), workshop,
                    polePositions[index], new Vector3(0.24f, 3.6f, 0.24f), poleMaterial);

            GameObject leftRoof = VoxelRacerBootstrap.CreateBlock("Tent Roof Left", workshop,
                new Vector3(-1.8f, 3.85f, 0f), new Vector3(3.85f, 0.22f, 7.2f), roofMaterial);
            leftRoof.transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            GameObject rightRoof = VoxelRacerBootstrap.CreateBlock("Tent Roof Right", workshop,
                new Vector3(1.8f, 3.85f, 0f), new Vector3(3.85f, 0.22f, 7.2f), roofMaterial);
            rightRoof.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
        }

        private static void BuildCacti(Transform workshop)
        {
            Vector3[] positions =
            {
                new Vector3(-12f, 0f, 5f), new Vector3(11f, 0f, 7f),
                new Vector3(-10f, 0f, -9f), new Vector3(13f, 0f, -7f),
                new Vector3(-18f, 0f, 1f), new Vector3(18f, 0f, 4f)
            };

            foreach (Vector3 position in positions)
            {
                Transform cactus = new GameObject("Workshop Voxel Cactus").transform;
                cactus.SetParent(workshop, false);
                cactus.localPosition = position;
                cactus.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                EndlessVoxelRoad.BuildRandomCactusVisual(cactus, 0.75f, 1.45f, 0.8f, 1.25f);
            }
        }

        private void SetupCamera()
        {
            workshopCamera = Camera.main;
            if (workshopCamera == null)
            {
                workshopCamera = new GameObject("Main Camera").AddComponent<Camera>();
                workshopCamera.tag = "MainCamera";
            }

            workshopCamera.clearFlags = CameraClearFlags.Skybox;
            ApplyCameraTuning();
        }

        public void ApplyCameraTuning()
        {
            if (cameraTuning == null)
                return;
            if (workshopCamera == null)
                workshopCamera = Camera.main;
            if (workshopCamera == null)
                return;

            workshopCamera.transform.position = cameraTuning.cameraPosition;
            Vector3 lookDirection = cameraTuning.cameraLookAt - cameraTuning.cameraPosition;
            if (lookDirection.sqrMagnitude > 0.0001f)
                workshopCamera.transform.rotation = Quaternion.LookRotation(lookDirection);
            workshopCamera.fieldOfView = Mathf.Clamp(cameraTuning.cameraFieldOfView, 10f, 90f);
            appliedCameraPosition = cameraTuning.cameraPosition;
            appliedCameraLookAt = cameraTuning.cameraLookAt;
            appliedCameraFieldOfView = cameraTuning.cameraFieldOfView;
        }

        private void BuildUi()
        {
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Repair Upgrade UI");

            Text title = VoxelMenuUi.CreateText(canvas, "Workshop Title", "REPAIR & UPGRADE", 194,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(1800f, 230f));
            title.color = Color.black;

            currencyText = VoxelMenuUi.CreateText(canvas, "Currency", string.Empty, 81,
                TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-470f, -82f), new Vector2(850f, 100f));
            currencyText.color = Color.black;

            VoxelMenuUi.CreatePanel(canvas, "Repair Panel", new Vector2(1f, 0.5f),
                new Vector2(-230f, 0f), new Vector2(360f, 720f));
            repair10ButtonLabel = CreateRepairButton(canvas, "Repair 10 Button", 245f,
                () => TryRepair(10f, GetRepairCost(10f)));
            repair25ButtonLabel = CreateRepairButton(canvas, "Repair 25 Button", 80f,
                () => TryRepair(25f, GetRepairCost(25f)));
            repair50ButtonLabel = CreateRepairButton(canvas, "Repair 50 Button", -85f,
                () => TryRepair(50f, GetRepairCost(50f)));
            fullRepairButtonLabel = CreateRepairButton(canvas, "Full Repair Button", -250f,
                () => TryRepair(100f, GetRepairCost(100f)));

            Image weaponUpgradePanel = VoxelMenuUi.CreatePanel(canvas, "Weapon Upgrade Panel", new Vector2(0f, 0.5f),
                new Vector2(330f, 0f), new Vector2(610f, 340f));
            VoxelMenuUi.CreateText(weaponUpgradePanel.transform, "Weapon Upgrade Title", "WEAPON UPGRADE", 75,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 112f), new Vector2(590f, 95f));
            gunUpgradeButton = VoxelMenuUi.CreateButton(weaponUpgradePanel.transform, "Long Gun Purchase Button", string.Empty, 68,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(560f, 175f), TryPurchaseLongGun);
            gunUpgradeButtonLabel = gunUpgradeButton.GetComponentInChildren<Text>();

            feedbackText = VoxelMenuUi.CreateText(canvas, "Repair Feedback", string.Empty, 68,
                TextAnchor.MiddleCenter, new Vector2(1f, 0.5f), new Vector2(-230f, -370f), new Vector2(340f, 80f));
            feedbackText.color = Color.black;

            NextRaceButton = VoxelMenuUi.CreateButton(canvas, "Next Race Button", "NEXT MISSION", 99,
                new Vector2(0.5f, 0f), new Vector2(0f, 78f), new Vector2(800f, 150f), StartNextRace);
        }

        private int GetRepairCost(float repairPercent) => repairTuning != null
            ? repairTuning.GetRepairCost(DisplayedCar, repairPercent)
            : 0;

        private static string RepairLabel(string repairName, int cost) => repairName + "\nCOST <color=#FFD12A>" + Mathf.Max(0, cost) + "</color>";

        private static Text CreateRepairButton(Transform canvas, string name,
            float verticalPosition, UnityEngine.Events.UnityAction action)
        {
            Button button = VoxelMenuUi.CreateButton(canvas, name, string.Empty, 60,
                new Vector2(1f, 0.5f), new Vector2(-230f, verticalPosition),
                new Vector2(320f, 145f), action);
            return button.GetComponentInChildren<Text>();
        }

        private void TryRepair(float percent, int cost)
        {
            if (DisplayedCar == null)
                return;

            if (!VoxelCurrencyState.TrySpend(cost))
            {
                feedbackText.text = "NOT ENOUGH CURRENCY";
                return;
            }

            int restored = percent >= 100f ? RepairFull() : DisplayedCar.RepairPercent(percent);
            VoxelCarRunState.Capture(DisplayedCar, definition);
            feedbackText.text = restored > 0 ? "REPAIRED " + restored + " VOXELS" : "NO REPAIRS NEEDED";
            RefreshUi();
        }

        private void TryPurchaseLongGun()
        {
            VoxelGunTuning tuning = VoxelGunUpgradeState.LongGunTuning;
            if (tuning == null)
            {
                feedbackText.text = "GUN UPGRADE NOT FOUND";
                return;
            }

            if (!VoxelGunUpgradeState.CanPurchase(tuning))
            {
                feedbackText.text = "GUN SLOTS FULL";
                return;
            }

            if (!VoxelGunUpgradeState.TryPurchase(tuning))
            {
                feedbackText.text = "NOT ENOUGH CURRENCY";
                return;
            }

            VoxelGunUpgradeState.ApplyTo(DisplayedCar.transform, tuning);
            feedbackText.text = tuning.displayName.ToUpperInvariant() + " INSTALLED";
            RefreshUi();
        }

        private int RepairFull()
        {
            int before = DisplayedCar.RemainingIntegrityVoxels;
            DisplayedCar.RepairToFull();
            return DisplayedCar.RemainingIntegrityVoxels - before;
        }

        private void RefreshUi()
        {
            if (currencyText != null)
                currencyText.text = "CASH  <color=#FFD12A>" + VoxelCurrencyState.Balance + "</color>";

            float missingPercent = DisplayedCar == null
                ? 0f
                : Mathf.Max(0f, 100f - DisplayedCar.IntegrityPercent);
            SetRepairButtonAvailability(repair10ButtonLabel, missingPercent >= 10f - 0.001f);
            SetRepairButtonAvailability(repair25ButtonLabel, missingPercent >= 25f - 0.001f);
            SetRepairButtonAvailability(repair50ButtonLabel, missingPercent >= 50f - 0.001f);
            SetRepairButtonAvailability(fullRepairButtonLabel, missingPercent > 0.001f);

            if (repair10ButtonLabel != null)
                repair10ButtonLabel.text = RepairLabel("REPAIR 10%", GetRepairCost(10f));
            if (repair25ButtonLabel != null)
                repair25ButtonLabel.text = RepairLabel("REPAIR 25%", GetRepairCost(25f));
            if (repair50ButtonLabel != null)
                repair50ButtonLabel.text = RepairLabel("REPAIR 50%", GetRepairCost(50f));
            if (fullRepairButtonLabel != null)
                fullRepairButtonLabel.text = RepairLabel("FULL REPAIR", GetRepairCost(100f));

            VoxelGunTuning gunTuning = VoxelGunUpgradeState.LongGunTuning;
            if (gunUpgradeButton == null || gunUpgradeButtonLabel == null || gunTuning == null)
                return;

            int owned = VoxelGunUpgradeState.PurchasedLongGunCount;
            int maximum = Mathf.Max(1, gunTuning.maximumPurchases);
            bool canPurchase = VoxelGunUpgradeState.CanPurchase(gunTuning);
            gunUpgradeButton.interactable = canPurchase;
            gunUpgradeButtonLabel.text = canPurchase
                ? gunTuning.displayName.ToUpperInvariant() + "\nCOST <color=#FFD12A>" + gunTuning.purchasePrice + "</color>   " + owned + "/" + maximum
                : "GUN SLOTS FULL\n" + owned + "/" + maximum;
        }

        private static void SetRepairButtonAvailability(Text label, bool available)
        {
            if (label == null)
                return;

            Button button = label.GetComponentInParent<Button>();
            if (button != null)
                button.interactable = available;
        }

        public void StartNextRace()
        {
            if (isLoading || DisplayedCar == null)
                return;

            VoxelTrackDefinition nextTrack = VoxelTrackProgressState.AdvanceToNextTrack();
            string sceneName = nextTrack != null && !string.IsNullOrWhiteSpace(nextTrack.raceSceneName)
                ? nextTrack.raceSceneName
                : raceSceneName;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + sceneName + ".unity");
            if (buildIndex < 0)
            {
                Debug.LogError("Race scene is not enabled in Build Settings: " + sceneName);
                return;
            }

            VoxelCarRunState.Capture(DisplayedCar, definition);
            isLoading = true;
            SceneManager.LoadScene(buildIndex);
        }

        private static Material CreateMaterial(string materialName, Color colour)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = materialName,
                color = colour
            };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.08f);
            return material;
        }
    }
}
