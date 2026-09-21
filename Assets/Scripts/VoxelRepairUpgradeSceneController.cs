using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Builds the desert workshop, damaged car display, repair controls, and next-race flow.</summary>
    public sealed partial class VoxelRepairUpgradeSceneController : MonoBehaviour
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
        private Text rightArmorUpgradeButtonLabel;
        private Button rightArmorUpgradeButton;
        private Text leftArmorUpgradeButtonLabel;
        private Button leftArmorUpgradeButton;
        private Text wheelSpikeUpgradeButtonLabel;
        private Button wheelSpikeUpgradeButton;
        private Button performanceWheelButton;
        private Text performanceWheelLabel;
        private Button boostUpgradeButton;
        private Text boostUpgradeLabel;
        private Button engineUpgradeButton;
        private Text engineUpgradeLabel;
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
            UpdateCarRotationInput();
            if (cameraTuning != null &&
                (cameraTuning.cameraPosition != appliedCameraPosition ||
                 cameraTuning.cameraLookAt != appliedCameraLookAt ||
                 !Mathf.Approximately(cameraTuning.cameraFieldOfView, appliedCameraFieldOfView)))
                ApplyCameraTuning();
        }

        private void BuildWorkshop()
        {
            Transform workshop = new GameObject("Garage Workshop").transform;
            workshop.SetParent(transform, false);

            VoxelGarageEnvironment.Build(workshop);
            BuildCar(workshop);
            SetupCamera();
        }

        private void BuildCar(Transform workshop)
        {
            Transform car = new GameObject("Workshop Player Car").transform;
            car.SetParent(workshop, false);
            car.localPosition = new Vector3(0f, .18f, 0f);
            car.localRotation = Quaternion.Euler(0f, -15f, 0f);

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
            VoxelGunUpgradeState.ApplyTo(car, VoxelGunUpgradeState.LongGunTuning);
            VoxelArmorUpgradeState.ApplyTo(car, definition);
            VoxelWheelSpikeUpgradeState.ApplyTo(car);
            VoxelPerformanceWheelUpgradeState.ApplyTo(car, definition);
            VoxelBoostUpgradeState.ApplyTo(car, definition);
            VoxelEngineUpgradeState.ApplyTo(car, definition);
            DisplayedCar.ResetIntegrityBaseline();
            VoxelCarRunState.Apply(DisplayedCar, definition);
            DisplayedCar.enabled = false;
            VoxelCarTurntable.Create(workshop, car);

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

            workshopCamera.clearFlags = CameraClearFlags.SolidColor;
            workshopCamera.backgroundColor = new Color(.018f, .023f, .033f);
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
            garageZoomAmount = 0f;
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

            Image weaponUpgradePanel = VoxelMenuUi.CreatePanel(canvas, "Car Upgrade Panel", new Vector2(0f, 0.5f),
                new Vector2(330f, 0f), new Vector2(610f, 700f));
            VoxelMenuUi.CreateText(weaponUpgradePanel.transform, "Upgrade Title", "CAR UPGRADES", 65,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 285f), new Vector2(590f, 90f));
            gunUpgradeButton = VoxelMenuUi.CreateButton(weaponUpgradePanel.transform, "Long Gun Purchase Button", string.Empty, 36,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(560f, 100f), TryPurchaseLongGun);
            gunUpgradeButtonLabel = gunUpgradeButton.GetComponentInChildren<Text>();
            rightArmorUpgradeButton = VoxelMenuUi.CreateButton(weaponUpgradePanel.transform, "Right Door Armor Purchase Button", string.Empty, 30,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 65f), new Vector2(560f, 100f),
                () => TryPurchaseDoorArmor(VoxelArmorSide.Right));
            rightArmorUpgradeButtonLabel = rightArmorUpgradeButton.GetComponentInChildren<Text>();
            leftArmorUpgradeButton = VoxelMenuUi.CreateButton(weaponUpgradePanel.transform, "Left Door Armor Purchase Button", string.Empty, 30,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(560f, 100f),
                () => TryPurchaseDoorArmor(VoxelArmorSide.Left));
            leftArmorUpgradeButtonLabel = leftArmorUpgradeButton.GetComponentInChildren<Text>();
            wheelSpikeUpgradeButton = VoxelMenuUi.CreateButton(weaponUpgradePanel.transform, "Wheel Spike Purchase Button", string.Empty, 30,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -165f), new Vector2(560f, 100f), TryPurchaseWheelSpikes);
            wheelSpikeUpgradeButtonLabel = wheelSpikeUpgradeButton.GetComponentInChildren<Text>();
            performanceWheelButton = VoxelMenuUi.CreateButton(weaponUpgradePanel.transform, "Performance Wheel Purchase Button", string.Empty, 30,
                new Vector2(.5f, .5f), new Vector2(0, -280), new Vector2(560, 100), TryPurchasePerformanceWheels);
            performanceWheelLabel = performanceWheelButton.GetComponentInChildren<Text>();
            boostUpgradeButton = VoxelMenuUi.CreateButton(weaponUpgradePanel.transform, "Boost Bottle Purchase Button", string.Empty, 30,
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(560, 100), TryPurchaseBoostBottle);
            boostUpgradeLabel = boostUpgradeButton.GetComponentInChildren<Text>();
            engineUpgradeButton = VoxelMenuUi.CreateButton(weaponUpgradePanel.transform, "V6 Engine Purchase Button", string.Empty, 30,
                new Vector2(.5f,.5f), Vector2.zero, new Vector2(560,100), TryPurchaseEngine);
            engineUpgradeLabel = engineUpgradeButton.GetComponentInChildren<Text>();
            BuildUpgradeScroll(weaponUpgradePanel.transform);

            feedbackText = VoxelMenuUi.CreateText(canvas, "Repair Feedback", string.Empty, 68,
                TextAnchor.MiddleCenter, new Vector2(1f, 0.5f), new Vector2(-230f, -370f), new Vector2(340f, 80f));
            feedbackText.color = Color.black;

            NextRaceButton = VoxelMenuUi.CreateButton(canvas, "Next Race Button", "NEXT MISSION", 99,
                new Vector2(0.5f, 0f), new Vector2(0f, 78f), new Vector2(800f, 150f), StartNextRace);
            StyleGarageUi(canvas);
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

        private void TryPurchaseDoorArmor(VoxelArmorSide side)
        {
            VoxelArmorTuning armor = VoxelArmorTuning.Load();
            if (DisplayedCar == null || !VoxelArmorUpgradeState.TryPurchase(armor, definition, side))
            {
                feedbackText.text = (side == VoxelArmorSide.Right ? "RIGHT" : "LEFT") + " DOOR ARMOUR UNAVAILABLE";
                RefreshUi();
                return;
            }
            VoxelArmorUpgradeState.ApplyTo(DisplayedCar.transform, definition);
            DisplayedCar.ResetIntegrityBaseline();
            VoxelCarRunState.Capture(DisplayedCar, definition);
            feedbackText.text = (side == VoxelArmorSide.Right ? "RIGHT" : "LEFT") + " DOOR ARMOUR INSTALLED";
            RefreshUi();
        }

        private void TryPurchaseWheelSpikes()
        {
            VoxelWheelSpikeTuning tuning = VoxelWheelSpikeTuning.Load();
            if (DisplayedCar == null || !VoxelWheelSpikeUpgradeState.TryPurchase(tuning))
            {
                feedbackText.text = "WHEEL SPIKES UNAVAILABLE";
                RefreshUi();
                return;
            }

            VoxelWheelSpikeUpgradeState.ApplyTo(DisplayedCar.transform, tuning);
            feedbackText.text = tuning.displayName.ToUpperInvariant() + " INSTALLED";
            RefreshUi();
        }

        private int RepairFull() => DisplayedCar.RepairPercent(100f);

        private void RefreshUi()
        {
            if (currencyText != null)
                currencyText.text = "$ " + VoxelCurrencyState.Balance.ToString("N0");

            float missingPercent = DisplayedCar == null
                ? 0f
                : 100f * DisplayedCar.RepairableIntegrityVoxels / Mathf.Max(1, DisplayedCar.TotalIntegrityVoxels);
            bool canRepair10 = missingPercent >= 10f - 0.001f;
            bool canRepair25 = missingPercent >= 25f - 0.001f;
            bool canRepair50 = missingPercent >= 50f - 0.001f;
            bool canRepairFull = missingPercent > 0.001f;
            SetRepairButtonAvailability(repair10ButtonLabel, canRepair10);
            SetRepairButtonAvailability(repair25ButtonLabel, canRepair25);
            SetRepairButtonAvailability(repair50ButtonLabel, canRepair50);
            SetRepairButtonAvailability(fullRepairButtonLabel, canRepairFull);

            if (repair10ButtonLabel != null)
                repair10ButtonLabel.text = RepairLabel("REPAIR 10%", GetRepairCost(10f), canRepair10);
            if (repair25ButtonLabel != null)
                repair25ButtonLabel.text = RepairLabel("REPAIR 25%", GetRepairCost(25f), canRepair25);
            if (repair50ButtonLabel != null)
                repair50ButtonLabel.text = RepairLabel("REPAIR 50%", GetRepairCost(50f), canRepair50);
            if (fullRepairButtonLabel != null)
                fullRepairButtonLabel.text = RepairLabel("FULL REPAIR", GetRepairCost(100f), canRepairFull);

            RefreshArmorShop();
            RefreshWheelSpikeShop();
            RefreshPerformanceWheelShop();
            RefreshEngineShop();
            RefreshBoostShop();
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

        private void RefreshWheelSpikeShop()
        {
            VoxelWheelSpikeTuning tuning = VoxelWheelSpikeTuning.Load();
            if (wheelSpikeUpgradeButton == null || wheelSpikeUpgradeButtonLabel == null)
                return;

            if (tuning == null || tuning.spikePrefab == null)
            {
                wheelSpikeUpgradeButton.interactable = false;
                wheelSpikeUpgradeButtonLabel.text = "WHEEL SPIKES\nUNAVAILABLE";
                return;
            }

            if (VoxelWheelSpikeUpgradeState.IsPurchased)
            {
                wheelSpikeUpgradeButton.interactable = false;
                wheelSpikeUpgradeButtonLabel.text = tuning.displayName.ToUpperInvariant() + "\nINSTALLED (+" +
                    tuning.sideRamDamageBonus + " SIDE RAM DAMAGE)";
                return;
            }

            bool affordable = VoxelCurrencyState.Balance >= tuning.purchasePrice;
            wheelSpikeUpgradeButton.interactable = affordable;
            wheelSpikeUpgradeButtonLabel.text = tuning.displayName.ToUpperInvariant() + " (SET OF 4)\n+" +
                tuning.sideRamDamageBonus + " SIDE RAM DAMAGE\nCOST <color=#FFD12A>" + tuning.purchasePrice + "</color>";
        }

        private void BuildUpgradeScroll(Transform panel)
        {
            // Keep cards readable as the upgrade catalogue grows; supports mouse wheel and touch drag.
            var viewport = new GameObject("Upgrade Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(panel, false);
            var rect = (RectTransform)viewport.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, -52.5f); rect.sizeDelta = new Vector2(580, 565);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, .01f);
            var content = new GameObject("Upgrade Cards", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = (RectTransform)content.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(.5f, 1); cr.pivot = new Vector2(.5f, 1);
            var buttons = new[] { gunUpgradeButton, rightArmorUpgradeButton, leftArmorUpgradeButton,
                wheelSpikeUpgradeButton, performanceWheelButton, boostUpgradeButton, engineUpgradeButton };
            cr.sizeDelta = new Vector2(560, buttons.Length * 115 - 15);
            for (int i = 0; i < buttons.Length; i++)
            {
                var br = (RectTransform)buttons[i].transform; br.SetParent(content.transform, false);
                br.anchorMin = br.anchorMax = new Vector2(.5f, 1);
                br.anchoredPosition = new Vector2(0, -50 - i * 115);
            }
            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = rect; scroll.content = cr; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            VoxelMenuUi.CreateText(panel, "Upgrade Scroll Hint", "DRAG OR SCROLL FOR MORE", 22,
                TextAnchor.MiddleCenter, new Vector2(.5f, .5f), new Vector2(0, 238), new Vector2(560, 26));
        }

        private void TryPurchaseEngine()
        {
            var tuning=VoxelEngineUpgradeTuning.Load();
            if(DisplayedCar==null || !VoxelEngineUpgradeState.TryPurchase(tuning,definition)) return;
            VoxelEngineUpgradeState.ApplyTo(DisplayedCar.transform,definition);
            VoxelCarRunState.Capture(DisplayedCar,definition);
            feedbackText.text="V6 INSTALLED"; RefreshUi();
        }
        private void RefreshEngineShop()
        {
            if(engineUpgradeButton==null) return;
            var tuning=VoxelEngineUpgradeTuning.Load();
            bool fits=tuning!=null && tuning.Fits(definition);
            engineUpgradeButton.interactable=fits && !VoxelEngineUpgradeState.IsPurchased && VoxelCurrencyState.Balance>=tuning.purchasePrice;
            engineUpgradeLabel.text=!fits?"V6 ENGINE\nUNAVAILABLE FOR THIS CAR":tuning.displayName+
                (VoxelEngineUpgradeState.IsPurchased?"\nINSTALLED":"\n+"+tuning.topSpeedBonusPercent+"% SPEED  +"+tuning.accelerationBonusPercent+
                "% ACCEL\nCOST <color=#FFD12A>"+tuning.purchasePrice+"</color>");
        }

        private void TryPurchaseBoostBottle()
        {
            var tuning = VoxelBoostUpgradeTuning.LoadUpgrade();
            if (DisplayedCar == null || !VoxelBoostUpgradeState.TryPurchase(tuning, definition)) return;
            VoxelBoostUpgradeState.ApplyTo(DisplayedCar.transform, definition);
            VoxelCarRunState.Capture(DisplayedCar, definition);
            feedbackText.text = "BOOST INSTALLED";
            RefreshUi();
        }

        private void RefreshBoostShop()
        {
            if (boostUpgradeButton == null) return;
            var tuning = VoxelBoostUpgradeTuning.LoadUpgrade();
            bool fits = tuning != null && tuning.Fits(definition);
            boostUpgradeButton.interactable = fits && !VoxelBoostUpgradeState.IsPurchased && VoxelCurrencyState.Balance >= tuning.purchasePrice;
            boostUpgradeLabel.text = !fits ? "BOOST BOTTLE\nUNAVAILABLE FOR THIS CAR" : tuning.displayName +
                (VoxelBoostUpgradeState.IsPurchased ? "\nINSTALLED" : "\nSPEED " + tuning.boostSpeed + "  DURATION " + tuning.boostLength +
                " SEC\nCOST <color=#FFD12A>" + tuning.purchasePrice + "</color>");
        }

        private void TryPurchasePerformanceWheels()
        {
            var tuning = VoxelPerformanceWheelTuning.Load();
            if (DisplayedCar == null || !VoxelPerformanceWheelUpgradeState.TryPurchase(tuning, definition)) return;
            VoxelPerformanceWheelUpgradeState.ApplyTo(DisplayedCar.transform, definition);
            VoxelCarRunState.Capture(DisplayedCar, definition);
            feedbackText.text = "WHEELS INSTALLED";
            RefreshUi();
        }

        private void RefreshPerformanceWheelShop()
        {
            if (performanceWheelButton == null) return;
            var tuning = VoxelPerformanceWheelTuning.Load();
            bool fits = tuning != null && tuning.Fits(definition);
            performanceWheelButton.interactable = fits && !VoxelPerformanceWheelUpgradeState.IsPurchased && VoxelCurrencyState.Balance >= tuning.purchasePrice;
            performanceWheelLabel.text = !fits ? "PERFORMANCE WHEELS\nUNAVAILABLE FOR THIS CAR" :
                tuning.displayName + (VoxelPerformanceWheelUpgradeState.IsPurchased ? "\nINSTALLED" :
                " (SET OF 4)\nBONUS PCT " + tuning.accelerationBonusPercent + " ACC  " + tuning.laneChangeBonusPercent +
                " LANE  " + tuning.brakingBonusPercent + " BRAKE\nCOST <color=#FFD12A>" + tuning.purchasePrice + "</color>");
        }

        private void RefreshArmorShop()
        {
            VoxelArmorTuning armor = VoxelArmorTuning.Load();
            bool compatible = armor != null && armor.panelPrefab != null && armor.Fits(definition);
            RefreshArmorButton(rightArmorUpgradeButton, rightArmorUpgradeButtonLabel, armor, compatible,
                VoxelArmorSide.Right);
            RefreshArmorButton(leftArmorUpgradeButton, leftArmorUpgradeButtonLabel, armor, compatible,
                VoxelArmorSide.Left);
        }

        private static void RefreshArmorButton(Button button, Text label, VoxelArmorTuning armor,
            bool compatible, VoxelArmorSide side)
        {
            if (button == null || label == null)
                return;

            bool owned = VoxelArmorUpgradeState.IsPurchasedFor(side);
            bool affordable = armor != null && VoxelCurrencyState.Balance >= armor.panelPurchasePrice;
            button.interactable = compatible && !owned && affordable;
            string sideName = side == VoxelArmorSide.Right ? "RIGHT" : "LEFT";
            if (!compatible)
                label.text = sideName + " DOOR ARMOUR\nUNAVAILABLE FOR THIS CAR";
            else if (owned)
                label.text = sideName + " DOOR ARMOUR\nINSTALLED";
            else
            {
                int count = VoxelCarSelectionState.CountIntegrityVoxels(armor.panelPrefab);
                label.text = sideName + " DOOR ARMOUR\n+" + count + " VOXELS | " +
                    armor.voxelHitPoints + " HP EACH\nCOST <color=#FFD12A>" + armor.panelPurchasePrice + "</color>";
            }
        }

        private static string RepairLabel(string repairName, int cost, bool available)
        {
            if (!available)
                return "<color=#787878>" + repairName + "\nCOST -</color>";
            return repairName + "\nCOST <color=#FFD12A>" + Mathf.Max(0, cost) + "</color>";
        }

        private static void SetRepairButtonAvailability(Text label, bool available)
        {
            if (label == null)
                return;

            Button button = label.GetComponentInParent<Button>();
            if (button == null)
                return;

            button.interactable = available;
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = available
                    ? new Color(0.14f, 0.18f, 0.27f, 0.96f)
                    : new Color(0.08f, 0.08f, 0.09f, 0.9f);
            label.color = available ? Color.white : new Color(0.47f, 0.47f, 0.47f);
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
