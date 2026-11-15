using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelMissionBriefingBuilder
    {
        public const string SettingsPath = "Assets/Resources/MissionBriefingSettings.asset";
        public const string ArtFolder = "Assets/Images/MissionBriefing";
        private const string RadarResource = "EnemyVehicles/RadarInterceptorTuning";
        private const string RadarPortraitAtlas = ArtFolder + "/Portraits/RadarDishSpin.png";
        private const int SpinFrames = 72, SpinColumns = 8, SpinRows = 9;
        [MenuItem("Tools/Voxel Racer/Mission Briefing/Select Settings")]
        public static void SelectSettings() => Selection.activeObject = VoxelMissionBriefingSettings.Load();

        [MenuItem("Tools/Voxel Racer/Mission Briefing/Rebuild Model Previews")]
        public static void Build()
        {
            Directory.CreateDirectory(ArtFolder + "/Models");
            AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<VoxelMissionBriefingSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<VoxelMissionBriefingSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            if (settings.dayBackground == null) settings.dayBackground = Import(ArtFolder + "/Day.png", 2048);
            if (settings.nightBackground == null) settings.nightBackground = Import(ArtFolder + "/Night.png", 2048);
            var entries = Resources.LoadAll<VoxelTrackDefinition>("Tracks").SelectMany(VoxelMissionBriefingContent.Enemies)
                .GroupBy(e => e.Source).Select(g => g.First()).ToArray();
            var radar = Resources.Load<VoxelEnemyVehicleTuning>(RadarResource);
            foreach (var entry in entries)
            {
                if (entry.Source == null || entry.Model == null) continue;
                string path = ArtFolder + "/Models/" + entry.Source.name + ".png";
                var record = settings.previews.FirstOrDefault(p => p != null && p.source == entry.Source);
                if (record == null) { record = new VoxelMissionBriefingPreview { source = entry.Source }; settings.previews.Add(record); }
                Bake(entry.Model, path, record);
                record.image = Import(path, 1024);
                if (entry.Source == radar && record.portrait != null) BakePortraitDish(entry.Model, record);
            }
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log("Mission briefing: baked " + entries.Length + " actual enemy model previews; source prefabs and mission tuning unchanged.");
        }

        [MenuItem("Tools/Voxel Racer/Mission Briefing/Rebuild Radar Animation")]
        public static void BuildRadarAnimation()
        {
            var settings = VoxelMissionBriefingSettings.Load();
            var radar = Resources.Load<VoxelEnemyVehicleTuning>(RadarResource);
            if (settings == null || radar == null || radar.modelPrefab == null)
                throw new System.InvalidOperationException("Missing briefing settings or radar model.");
            var record = settings.PreviewRecordFor(radar);
            if (record == null) { record = new VoxelMissionBriefingPreview { source = radar }; settings.previews.Add(record); }
            if (record.portrait != null) BakePortraitDish(radar.modelPrefab, record);
            else
            {
                Directory.CreateDirectory(ArtFolder + "/Models");
                string path = ArtFolder + "/Models/" + radar.name + ".png";
                Bake(radar.modelPrefab, path, record);
                record.image = Import(path, 1024);
            }
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log("Baked radar briefing animation; source model and gameplay spin unchanged.");
        }

        private static Texture2D Import(string path, int maximum)
        {
            if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = maximum; importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void BakePortraitDish(GameObject prefab, VoxelMissionBriefingPreview record)
        {
            const int frameWidth = 128, frameHeight = 96, roofContactFromBottom = 12;
            const float pixelsPerMetre = 115f;
            var preview = new PreviewRenderUtility();
            var atlas = new Texture2D(SpinColumns * frameWidth, SpinRows * frameHeight, TextureFormat.RGBA32, false);
            var tile = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false, true);
            var random = Random.state;
            try
            {
                var model = Object.Instantiate(prefab);
                preview.AddSingleGO(model);
                var dish = model.GetComponentInChildren<VoxelRadarDish>(true);
                if (dish == null || dish.transform.parent == model.transform || Mathf.Approximately(dish.degreesPerSecond, 0f))
                    throw new System.InvalidOperationException("The radar prefab needs a spinning dish with a roof mount.");
                var mount = dish.transform.parent;
                var studioSurface = new MaterialPropertyBlock();
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.transform.IsChildOf(mount)) { renderer.enabled = false; continue; }
                    // The preview has no road-sky reflection probe for the chrome.
                    renderer.GetPropertyBlock(studioSurface);
                    studioSurface.SetFloat("_Metallic", .18f);
                    studioSurface.SetFloat("_Smoothness", .35f);
                    renderer.SetPropertyBlock(studioSurface);
                }
                foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;

                var camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = frameHeight / (2f * pixelsPerMetre);
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 20f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                // Match the illustrated car's front three-quarter view and project the mount foot onto the roof centre.
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(5.3f, -2.1f, -8f));
                Vector3 focus = mount.position + camera.transform.up * ((frameHeight * .5f - roofContactFromBottom) / pixelsPerMetre);
                camera.transform.position = focus - camera.transform.forward * 5f;
                preview.ambientColor = new Color(.65f, .68f, .75f);
                preview.lights[0].intensity = 2.4f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, 20, 0);
                preview.lights[1].intensity = 2f;
                preview.lights[1].transform.rotation = Quaternion.Euler(25, 145, 0);

                Quaternion start = dish.transform.localRotation;
                var rect = new Rect(0, 0, frameWidth, frameHeight);
                for (int frame = 0; frame < SpinFrames; frame++)
                {
                    dish.transform.localRotation = start * Quaternion.Euler(0, Mathf.Sign(dish.degreesPerSecond) * frame * 360f / SpinFrames, 0);
                    preview.BeginPreview(rect, GUIStyle.none);
                    preview.Render(true);
                    var rendered = (RenderTexture)preview.EndPreview();
                    var previous = RenderTexture.active;
                    try { RenderTexture.active = rendered; tile.ReadPixels(rect, 0, 0, false); }
                    finally { RenderTexture.active = previous; }
                    Color[] pixels = tile.GetPixels();
                    if (pixels[0].a > .01f || !pixels.Any(pixel => pixel.a > .99f))
                        throw new System.InvalidOperationException("The radar dish preview did not render with alpha.");
                    // PreviewRenderUtility's HDR target stores linear RGB; the PNG importer expects sRGB.
                    if (!rendered.sRGB && QualitySettings.activeColorSpace == ColorSpace.Linear)
                        for (int i = 0; i < pixels.Length; i++) pixels[i] = pixels[i].gamma;
                    atlas.SetPixels(frame % SpinColumns * frameWidth, frame / SpinColumns * frameHeight, frameWidth, frameHeight, pixels);
                }
                File.WriteAllBytes(RadarPortraitAtlas, atlas.EncodeToPNG());
                AssetDatabase.ImportAsset(RadarPortraitAtlas, ImportAssetOptions.ForceSynchronousImport);
                record.portraitDish = AssetDatabase.LoadAssetAtPath<Texture2D>(RadarPortraitAtlas);
                SetSpinTiming(record, dish.degreesPerSecond);
            }
            finally
            {
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(atlas);
                preview.Cleanup();
                Random.state = random;
            }
        }

        private static void SetSpinTiming(VoxelMissionBriefingPreview record, float degreesPerSecond)
        {
            record.columns = SpinColumns; record.rows = SpinRows; record.frameCount = SpinFrames;
            record.framesPerSecond = SpinFrames * Mathf.Abs(degreesPerSecond) / 360f;
        }

        private static void Bake(GameObject prefab, string path, VoxelMissionBriefingPreview record)
        {
            var random = Random.state;
            var preview = new PreviewRenderUtility();
            try
            {
                var model = Object.Instantiate(prefab); preview.AddSingleGO(model);
                foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var renderers = model.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) throw new System.InvalidOperationException("No visible model in " + prefab.name);
                var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var dish = model.GetComponentInChildren<VoxelRadarDish>(true);
                Quaternion dishRotation = dish != null ? dish.transform.localRotation : Quaternion.identity;
                const int frameWidth = 384, frameHeight = 240;
                if (dish != null)
                {
                    // Reserve the complete sweep so neither the camera nor framing moves as the dish turns.
                    for (int frame = 0; frame < SpinFrames; frame++)
                    {
                        dish.transform.localRotation = dishRotation * Quaternion.Euler(0, frame * 360f / SpinFrames, 0);
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    }
                    dish.transform.localRotation = dishRotation;
                }
                var camera = preview.camera;
                camera.orthographic = true;
                camera.transform.position = bounds.center + new Vector3(-6, 4.2f, 8).normalized * Mathf.Max(12, bounds.extents.magnitude * 3);
                camera.transform.LookAt(bounds.center);
                float extentX = 0, extentY = 0;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = camera.transform.InverseTransformPoint(corner) - camera.transform.InverseTransformPoint(bounds.center);
                    extentX = Mathf.Max(extentX, Mathf.Abs(local.x)); extentY = Mathf.Max(extentY, Mathf.Abs(local.y));
                }
                camera.orthographicSize = Mathf.Max(extentY, extentX / (768f / 480f)) * 1.08f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 1000;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .04f, .07f);
                preview.ambientColor = new Color(.58f, .63f, .72f);
                preview.lights[0].intensity = 2.2f; preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
                preview.lights[1].intensity = 1.4f; preview.lights[1].transform.rotation = Quaternion.Euler(40, 145, 0);
                preview.BeginStaticPreview(new Rect(0, 0, 768, 480)); preview.Render(true);
                var image = preview.EndStaticPreview();
                File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
                if (dish != null && !Mathf.Approximately(dish.degreesPerSecond, 0))
                {
                    var atlas = new Texture2D(SpinColumns * frameWidth, SpinRows * frameHeight, TextureFormat.RGB24, false);
                    try
                    {
                        for (int frame = 0; frame < SpinFrames; frame++)
                        {
                            dish.transform.localRotation = dishRotation * Quaternion.Euler(0,
                                Mathf.Sign(dish.degreesPerSecond) * frame * 360f / SpinFrames, 0);
                            preview.BeginStaticPreview(new Rect(0, 0, frameWidth, frameHeight)); preview.Render(true);
                            var tile = preview.EndStaticPreview();
                            try { atlas.SetPixels(frame % SpinColumns * frameWidth, frame / SpinColumns * frameHeight,
                                frameWidth, frameHeight, tile.GetPixels()); }
                            finally { Object.DestroyImmediate(tile); }
                        }
                        atlas.Apply();
                        string animationPath = Path.ChangeExtension(path, null) + "_DishSpin.png";
                        File.WriteAllBytes(animationPath, atlas.EncodeToPNG());
                        record.animation = Import(animationPath, 4096);
                        SetSpinTiming(record, dish.degreesPerSecond);
                    }
                    finally { Object.DestroyImmediate(atlas); }
                }
                else
                {
                    record.animation = null; record.columns = record.rows = record.frameCount = 1; record.framesPerSecond = 0;
                }
            }
            finally { preview.Cleanup(); Random.state = random; }
        }
    }
}
