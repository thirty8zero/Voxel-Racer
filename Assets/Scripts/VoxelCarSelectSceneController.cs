using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Previews catalogue cars, reports their tuning, and launches the race.</summary>
    public sealed class VoxelCarSelectSceneController : MonoBehaviour
    {
        public Transform displayAnchor;
        public string mainMenuSceneName = "MainMenu";
        public string raceSceneName = "SampleScene";
        [Min(0f)] public float rotationDegreesPerSecond = 24f;
        [Min(10f)] public float previewCameraFieldOfView = 55f;
        public Vector3 previewCameraLookAt = new Vector3(0f, -1.2f, 0f);

        private VoxelCarDefinition[] definitions;
        private VoxelCarDefinition selectedDefinition;
        private GameObject displayedVisual;
        private Text carNameText;
        private Text statsText;
        private Text statValuesText;
        private Button selectButton;
        private Button raceButton;
        private int currentIndex;
        private bool isLoading;

        private VoxelCarDefinition Current => definitions != null && definitions.Length > 0
            ? definitions[currentIndex]
            : null;

        private void Awake()
        {
            definitions = VoxelCarSelectionState.LoadDefinitions();
            selectedDefinition = null;

            Camera previewCamera = Camera.main;
            if (previewCamera != null)
            {
                previewCamera.fieldOfView = previewCameraFieldOfView;
                previewCamera.transform.rotation = Quaternion.LookRotation(
                    previewCameraLookAt - previewCamera.transform.position);
            }

            if (displayAnchor == null)
            {
                var anchor = new GameObject("Rotating Car Display");
                anchor.transform.SetParent(transform, false);
                displayAnchor = anchor.transform;
            }

            ShowCurrentCar();
            BuildUi();
            RefreshUi();
        }

        private void Update()
        {
            if (displayAnchor != null)
                displayAnchor.Rotate(Vector3.up, rotationDegreesPerSecond * Time.deltaTime, Space.World);

            if (isLoading)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                Move(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                Move(1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                SelectCurrent();
            if (keyboard.rKey.wasPressedThisFrame && selectedDefinition != null)
                StartRace();
            if (keyboard.escapeKey.wasPressedThisFrame)
                ReturnToMainMenu();
        }

        private void BuildUi()
        {
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Car Select UI");
            VoxelMenuUi.CreateButton(canvas, "Back Button", "BACK", 33,
                new Vector2(0f, 1f), new Vector2(92f, -52f), new Vector2(150f, 58f), ReturnToMainMenu);

            carNameText = VoxelMenuUi.CreateText(canvas, "Car Name", string.Empty, 114,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(1500f, 140f));

            VoxelMenuUi.CreateButton(canvas, "Previous Car", "<", 63,
                new Vector2(0f, 0.5f), new Vector2(78f, 0f), new Vector2(76f, 76f), () => Move(-1));
            VoxelMenuUi.CreateButton(canvas, "Next Car", ">", 63,
                new Vector2(1f, 0.5f), new Vector2(-78f, 0f), new Vector2(76f, 76f), () => Move(1));

            Image statsPanel = VoxelMenuUi.CreatePanel(canvas, "Stats Panel", new Vector2(0.5f, 0f),
                new Vector2(0f, 410f), new Vector2(1220f, 352f));
            statsText = VoxelMenuUi.CreateText(statsPanel.transform, "Stat Labels", string.Empty, 69,
                TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(-270f, 0f), new Vector2(620f, 300f));
            statValuesText = VoxelMenuUi.CreateText(statsPanel.transform, "Stat Values", string.Empty, 69,
                TextAnchor.MiddleRight, new Vector2(0.5f, 0.5f), new Vector2(350f, 0f), new Vector2(360f, 300f));

            selectButton = VoxelMenuUi.CreateButton(canvas, "Select Button", "SELECT", 78,
                new Vector2(0.5f, 0f), new Vector2(0f, 78f), new Vector2(540f, 128f), SelectCurrent);
            raceButton = VoxelMenuUi.CreateButton(canvas, "Race Button", "RACE", 96,
                new Vector2(1f, 0f), new Vector2(-290f, 100f), new Vector2(540f, 160f), StartRace);
        }

        private void ShowCurrentCar()
        {
            if (displayedVisual != null)
                Destroy(displayedVisual);

            VoxelCarDefinition definition = Current;
            if (definition == null || definition.visualPrefab == null)
            {
                displayedVisual = null;
                RefreshUi();
                return;
            }

            displayAnchor.rotation = Quaternion.identity;
            displayedVisual = Instantiate(definition.visualPrefab, displayAnchor);
            displayedVisual.name = definition.displayName + " Preview";
            displayedVisual.transform.localPosition = Vector3.zero;
            displayedVisual.transform.localRotation = Quaternion.identity;
            displayedVisual.transform.localScale = Vector3.one;
            RefreshUi();
        }

        private void Move(int direction)
        {
            if (definitions == null || definitions.Length < 2)
                return;
            currentIndex = (currentIndex + direction + definitions.Length) % definitions.Length;
            ShowCurrentCar();
        }

        private void SelectCurrent()
        {
            if (Current == null)
                return;
            VoxelCarSelectionState.Select(Current);
            selectedDefinition = Current;
            RefreshUi();
        }

        private void StartRace()
        {
            if (isLoading || selectedDefinition == null)
                return;
            VoxelTrackProgressState.BeginSequence();
            VoxelTrackDefinition firstTrack = VoxelTrackProgressState.CurrentTrack;
            string sceneName = firstTrack != null && !string.IsNullOrWhiteSpace(firstTrack.raceSceneName)
                ? firstTrack.raceSceneName
                : raceSceneName;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + sceneName + ".unity");
            if (buildIndex < 0)
            {
                Debug.LogError("Race scene is not enabled in Build Settings: " + sceneName);
                return;
            }
            VoxelCarRunState.BeginNewRun(selectedDefinition);
            isLoading = true;
            SceneManager.LoadScene(buildIndex);
        }

        private void ReturnToMainMenu()
        {
            if (isLoading)
                return;
            int buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + mainMenuSceneName + ".unity");
            if (buildIndex < 0)
            {
                Debug.LogError("Main Menu scene is not enabled in Build Settings: " + mainMenuSceneName);
                return;
            }
            isLoading = true;
            SceneManager.LoadScene(buildIndex);
        }

        private void RefreshUi()
        {
            if (carNameText == null || statsText == null || statValuesText == null ||
                selectButton == null || raceButton == null)
                return;

            VoxelCarDefinition definition = Current;
            if (definition == null)
            {
                carNameText.text = "NO CARS AVAILABLE";
                statsText.text = string.Empty;
                statValuesText.text = string.Empty;
                selectButton.gameObject.SetActive(false);
                raceButton.gameObject.SetActive(false);
                return;
            }

            carNameText.text = definition.displayName.ToUpperInvariant();
            VoxelCarTuning tuning = definition.tuning;
            statsText.text = "SPEED\nACCELERATION\nBRAKING\nINTEGRITY";
            statValuesText.text =
                (tuning != null ? tuning.topSpeed.ToString("0.#") : "-") + "\n" +
                (tuning != null ? tuning.acceleration.ToString("0.#") : "-") + "\n" +
                (tuning != null ? tuning.brakingForce.ToString("0.#") : "-") + "\n" +
                VoxelCarSelectionState.CountIntegrityVoxels(displayedVisual);

            selectButton.gameObject.SetActive(true);
            Text selectLabel = selectButton.GetComponentInChildren<Text>();
            if (selectLabel != null)
                selectLabel.text = selectedDefinition == definition ? "SELECTED" : "SELECT";
            raceButton.gameObject.SetActive(selectedDefinition != null);
        }
    }
}
