using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoxelRacer
{
    /// <summary>One shared authored profile; one scene-owned volume on each game camera.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class VoxelMobilePostProcessing : MonoBehaviour
    {
        private Volume volume;
        private Volume debriefVolume;
        private VolumeProfile debriefProfile;
        private DepthOfField debriefBlur;
        private bool debriefRequested;
        private float debriefSubjectDepth;
        private VoxelPostProcessingSettings settings;

        public static void Configure(Camera camera)
        {
            if (camera == null) return;
            var controller = camera.GetComponent<VoxelMobilePostProcessing>();
            if (controller == null) controller = camera.gameObject.AddComponent<VoxelMobilePostProcessing>();
            controller.Apply();
        }

        public void Apply()
        {
            settings = VoxelPostProcessingSettings.Load();
            var data = GetComponent<Camera>().GetUniversalAdditionalCameraData();
            bool active = settings != null && settings.effectsEnabled && settings.profile != null;
            data.renderPostProcessing = active;
            if (volume == null)
            {
                var root = new GameObject("Mobile Post Processing");
                root.transform.SetParent(transform, false);
                root.layer = gameObject.layer;
                volume = root.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
            }
            volume.sharedProfile = settings != null ? settings.profile : null;
            volume.enabled = active;
            data.volumeLayerMask |= 1 << volume.gameObject.layer;
            SetDebriefFocus(debriefRequested, debriefSubjectDepth);
        }

        /// <summary>Finish-camera-only far blur; the car remains in the sharp foreground.</summary>
        public void SetDebriefFocus(bool active, float subjectDepth)
        {
            debriefRequested = active;
            debriefSubjectDepth = subjectDepth;
            if (settings == null) settings = VoxelPostProcessingSettings.Load();
            bool enabled = isActiveAndEnabled && active && subjectDepth > 0 && settings != null &&
                settings.effectsEnabled && settings.profile != null && settings.debriefDepthOfField && settings.debriefBlurRadius > 0;
            if (!enabled)
            {
                if (debriefVolume != null) debriefVolume.enabled = false;
                return;
            }
            if (debriefVolume == null)
            {
                var root = new GameObject("Debrief Background Focus");
                root.transform.SetParent(transform, false);
                root.layer = gameObject.layer;
                debriefVolume = root.AddComponent<Volume>();
                debriefVolume.isGlobal = true;
                debriefVolume.priority = 11f;
            }
            if (debriefProfile == null)
            {
                debriefProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                debriefProfile.hideFlags = HideFlags.DontSave;
                debriefBlur = debriefProfile.Add<DepthOfField>();
                debriefBlur.mode.Override(DepthOfFieldMode.Gaussian);
                debriefBlur.highQualitySampling.Override(false);
                debriefVolume.sharedProfile = debriefProfile;
            }
            float start = Mathf.Max(0, subjectDepth) + Mathf.Max(0, settings.debriefSharpMargin);
            debriefBlur.gaussianStart.Override(start);
            debriefBlur.gaussianEnd.Override(start + Mathf.Max(1, settings.debriefBlurRange));
            debriefBlur.gaussianMaxRadius.Override(Mathf.Clamp01(settings.debriefBlurRadius));
            debriefVolume.enabled = true;
        }

        private void OnDisable()
        {
            if (volume != null) volume.enabled = false;
            if (debriefVolume != null) debriefVolume.enabled = false;
        }

        private void OnEnable()
        {
            if (volume != null) Apply();
        }

        private void OnDestroy() => ReleaseDebriefResources();

        // Edit Mode preview scenes do not run ordinary MonoBehaviour lifecycle
        // callbacks, so their owner explicitly releases these temporary objects.
        public void ReleaseDebriefResources()
        {
            if (debriefVolume != null)
            {
                debriefVolume.enabled = false;
                debriefVolume.sharedProfile = null;
            }
            if (Application.isPlaying)
            {
                if (debriefBlur != null) Destroy(debriefBlur);
                if (debriefProfile != null) Destroy(debriefProfile);
            }
            else
            {
                if (debriefBlur != null) DestroyImmediate(debriefBlur);
                if (debriefProfile != null) DestroyImmediate(debriefProfile);
            }
            debriefBlur = null;
            debriefProfile = null;
        }

        public static void RefreshAll()
        {
            foreach (var controller in FindObjectsByType<VoxelMobilePostProcessing>(FindObjectsSortMode.None))
                controller.Apply();
        }
    }
}
