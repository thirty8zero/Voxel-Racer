using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Lets the player choose a prefab-backed car before starting the countdown.</summary>
    public sealed class VoxelCarSelectionScreen : MonoBehaviour
    {
        private const string LastCarKey = "VoxelRacer.SelectedCar";

        private VoxelCarController target;
        private VoxelStartCountdown countdown;
        private VoxelCarDefinition[] definitions = System.Array.Empty<VoxelCarDefinition>();
        private bool selectionComplete;

        public void Configure(VoxelCarController player, VoxelStartCountdown startCountdown)
        {
            target = player;
            countdown = startCountdown;
            definitions = VoxelCarSelectionState.LoadDefinitions();
            target.SetDrivingEnabled(false);

            if (definitions.Length == 0)
            {
                selectionComplete = true;
                countdown.BeginCountdown();
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || selectionComplete || target == null)
                return;

            const float width = 360f;
            float height = 105f + definitions.Length * 68f;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(panel, string.Empty, VoxelHudStyles.Box(30));

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = VoxelHudStyles.HudFont,
                fontSize = 42,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(panel.x + 10f, panel.y + 12f, panel.width - 20f, 42f), "SELECT CAR", titleStyle);

            string previousSelection = PlayerPrefs.GetString(LastCarKey, string.Empty);
            var buttonStyle = VoxelHudStyles.Button(27);
            for (int index = 0; index < definitions.Length; index++)
            {
                VoxelCarDefinition definition = definitions[index];
                string label = definition.displayName;
                if (definition.name == previousSelection)
                    label += "  •";

                var button = new Rect(panel.x + 35f, panel.y + 67f + index * 68f, panel.width - 70f, 54f);
                if (GUI.Button(button, label, buttonStyle))
                    Select(definition);
            }
        }

        private void Select(VoxelCarDefinition definition)
        {
            if (definition == null || definition.visualPrefab == null)
                return;

            for (int index = target.transform.childCount - 1; index >= 0; index--)
            {
                GameObject oldVisual = target.transform.GetChild(index).gameObject;
                oldVisual.SetActive(false);
                Destroy(oldVisual);
            }

            GameObject visual = Instantiate(definition.visualPrefab, target.transform);
            visual.name = definition.displayName + " Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            target.SetTuning(definition.tuning != null ? definition.tuning : VoxelCarTuning.Load());
            target.ResetIntegrityBaseline();
            PlayerPrefs.SetString(LastCarKey, definition.name);
            PlayerPrefs.Save();
            selectionComplete = true;
            countdown.BeginCountdown();
        }
    }
}
