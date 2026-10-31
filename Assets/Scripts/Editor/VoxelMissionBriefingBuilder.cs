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
            foreach (var entry in entries)
            {
                if (entry.Source == null || entry.Model == null) continue;
                string path = ArtFolder + "/Models/" + entry.Source.name + ".png";
                Bake(entry.Model, path);
                var record = settings.previews.FirstOrDefault(p => p != null && p.source == entry.Source);
                if (record == null) { record = new VoxelMissionBriefingPreview { source = entry.Source }; settings.previews.Add(record); }
                record.image = Import(path, 1024);
            }
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log("Mission briefing: baked " + entries.Length + " actual enemy model previews; source prefabs and mission tuning unchanged.");
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
        private static void Bake(GameObject prefab, string path)
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
            }
            finally { preview.Cleanup(); Random.state = random; }
        }
    }
}
