using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoxelRacer.Editor
{
    [CustomEditor(typeof(VoxelPostProcessingSettings))]
    public sealed class VoxelPostProcessingSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (DrawDefaultInspector()) VoxelMobilePostProcessing.RefreshAll();
        }
    }

    public static class VoxelMobilePostProcessingBuilder
    {
        public const string SettingsPath = "Assets/Resources/MobilePostProcessingSettings.asset";
        public const string ProfilePath = "Assets/Settings/MobilePostProcessingProfile.asset";

        [MenuItem("Tools/Voxel Racer/Post Processing/Create Initial Mobile Profile")]
        public static void Build()
        {
            // Re-running only reconnects missing assets; never reset an artist's settings.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                var colour = profile.Add<ColorAdjustments>();
                colour.contrast.Override(12f); colour.saturation.Override(6f);
                var tone = profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.Neutral);
                var bloom = profile.Add<Bloom>();
                bloom.intensity.Override(.12f); bloom.threshold.Override(1.1f);
                bloom.scatter.Override(.45f); bloom.highQualityFiltering.Override(false);
                bloom.filter.Override(BloomFilterMode.Dual);
                bloom.downscale.Override(BloomDownscaleMode.Quarter); bloom.maxIterations.Override(4);
                var vignette = profile.Add<Vignette>();
                vignette.intensity.Override(.12f); vignette.smoothness.Override(.35f);
                foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
                EditorUtility.SetDirty(profile);
            }
            var settings = AssetDatabase.LoadAssetAtPath<VoxelPostProcessingSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<VoxelPostProcessingSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            if (settings.profile == null) { settings.profile = profile; EditorUtility.SetDirty(settings); }
            AssetDatabase.SaveAssets(); Selection.activeObject = settings;
            VoxelMobilePostProcessing.RefreshAll();
        }

        [MenuItem("Tools/Voxel Racer/Post Processing/Enable")]
        public static void Enable() => SetEnabled(true);
        [MenuItem("Tools/Voxel Racer/Post Processing/Disable")]
        public static void Disable() => SetEnabled(false);
        public static void SetEnabled(bool enabled)
        {
            var settings = VoxelPostProcessingSettings.Load();
            if (settings == null) throw new InvalidOperationException("Create the mobile profile first.");
            Undo.RecordObject(settings, "Toggle post processing");
            settings.effectsEnabled = enabled; EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets(); VoxelMobilePostProcessing.RefreshAll();
            Debug.Log("Mobile post processing " + (enabled ? "enabled" : "disabled"));
        }
        [MenuItem("Tools/Voxel Racer/Post Processing/Select Settings")]
        public static void SelectSettings() => Selection.activeObject = VoxelPostProcessingSettings.Load();
    }
}
