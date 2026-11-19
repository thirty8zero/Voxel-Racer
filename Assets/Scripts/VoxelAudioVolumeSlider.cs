using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Shared master volume control for the race and the garage.</summary>
    public sealed class VoxelAudioVolumeSlider : MonoBehaviour
    {
        private const string PreferenceKey = "VoxelRacer.MasterVolume";
        private static float SavedVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(PreferenceKey, 1f));
        private Slider slider;
        private Text readout;
        private bool changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySavedVolume() => AudioListener.volume = SavedVolume;

        internal static void Create(Transform parent, float y, float width, Color accent)
        {
            var centre = new Vector2(.5f, .5f);
            var row = VoxelMenuUi.CreatePanel(parent, "Audio Volume Row", centre, new Vector2(0, y), new Vector2(width, 88));
            row.color = new Color(.065f, .085f, .115f);
            row.raycastTarget = false;
            VoxelMenuUi.CreateText(row.transform, "Label", "AUDIO VOLUME", 26, TextAnchor.MiddleLeft,
                centre, new Vector2(-48, 20), new Vector2(width - 144, 32));
            var percent = VoxelMenuUi.CreateText(row.transform, "Volume Percentage", "", 24, TextAnchor.MiddleRight,
                centre, new Vector2(width * .5f - 66f, 20), new Vector2(84, 32));
            percent.font = Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001") ?? percent.font;
            percent.color = accent;

            var hitArea = VoxelMenuUi.CreatePanel(row.transform, "Audio Volume Slider", centre,
                new Vector2(0, -17), new Vector2(width - 24, 48));
            hitArea.color = row.color;
            var rail = VoxelMenuUi.CreatePanel(hitArea.transform, "Track", centre, Vector2.zero, new Vector2(width - 48, 8));
            rail.color = new Color(.18f, .26f, .34f);
            rail.raycastTarget = false;
            var fill = VoxelMenuUi.CreatePanel(rail.transform, "Fill", centre, Vector2.zero, Vector2.zero);
            fill.color = accent;
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var thumb = VoxelMenuUi.CreatePanel(rail.transform, "Thumb", centre, Vector2.zero, new Vector2(20, 26));
            thumb.color = Color.white;
            thumb.raycastTarget = false;

            var control = hitArea.gameObject.AddComponent<Slider>();
            control.targetGraphic = thumb;
            control.fillRect = fill.rectTransform;
            control.handleRect = thumb.rectTransform;
            control.maxValue = 100f;
            control.wholeNumbers = true;
            var binding = hitArea.gameObject.AddComponent<VoxelAudioVolumeSlider>();
            binding.slider = control;
            binding.readout = percent;
            binding.Refresh();
            control.onValueChanged.AddListener(binding.SetVolume);
        }

        private void OnEnable()
        {
            if (slider != null)
                Refresh();
        }

        private void Refresh()
        {
            slider.SetValueWithoutNotify(SavedVolume * 100f);
            UpdateReadout(slider.value);
        }

        private void SetVolume(float percent)
        {
            AudioListener.volume = percent / 100f;
            PlayerPrefs.SetFloat(PreferenceKey, AudioListener.volume);
            changed = true;
            UpdateReadout(percent);
        }

        private void UpdateReadout(float percent) => readout.text = $"{percent:0}%";

        private void OnDisable() => SaveIfChanged();

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveIfChanged();
        }

        private void SaveIfChanged()
        {
            if (!changed)
                return;
            PlayerPrefs.Save();
            changed = false;
        }
    }
}
