using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>Shared track atmosphere for race setup and isolated editor previews.</summary>
    public static class VoxelTrackEnvironment
    {
        public static Material ResolveSkybox(VoxelTrackDefinition track)
        {
            if (track == null) return null;
            if (!track.nightTime) return track.skyboxMaterial;
            return track.nightSkyboxMaterial != null ? track.nightSkyboxMaterial
                : Resources.Load<Material>("Scenery/DesertNightPixelSky");
        }

        public static void ApplyLighting(Light light, VoxelTrackDefinition track)
        {
            bool night = track != null && track.nightTime;
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            light.intensity = night ? track.nightLightIntensity : 1.3f;
            light.color = night ? track.nightLightColour : new Color(1f, .94f, .86f);
            // Gradient ambient uses the existing SH lighting path: no extra lights,
            // geometry, reflection captures or screen-space effects.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = night ? track.nightAmbientColour : new Color(.60f, .70f, .86f);
            RenderSettings.ambientEquatorColor = night
                ? track.nightAmbientColour * new Color(.85f, .86f, .92f) : new Color(.46f, .49f, .56f);
            RenderSettings.ambientGroundColor = night
                ? track.nightAmbientColour * new Color(.36f, .38f, .44f) : new Color(.24f, .20f, .18f);
        }

        public static void ApplyFog(VoxelTrackDefinition track)
        {
            RenderSettings.fog = track == null || track.fogEnabled;
            RenderSettings.fogColor = track != null
                ? (track.nightTime ? track.nightFogColour : track.fogColour) : new Color(.58f, .20f, .18f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = track != null ? track.fogStartDistance : 90f;
            RenderSettings.fogEndDistance = track != null ? track.fogEndDistance : 320f;
        }
    }
}
