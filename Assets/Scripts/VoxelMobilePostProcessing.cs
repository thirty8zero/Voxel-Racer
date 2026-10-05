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

        public static void Configure(Camera camera)
        {
            if (camera == null) return;
            var controller = camera.GetComponent<VoxelMobilePostProcessing>();
            if (controller == null) controller = camera.gameObject.AddComponent<VoxelMobilePostProcessing>();
            controller.Apply();
        }

        public void Apply()
        {
            var settings = VoxelPostProcessingSettings.Load();
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
        }

        public static void RefreshAll()
        {
            foreach (var controller in FindObjectsByType<VoxelMobilePostProcessing>(FindObjectsSortMode.None))
                controller.Apply();
        }
    }
}
