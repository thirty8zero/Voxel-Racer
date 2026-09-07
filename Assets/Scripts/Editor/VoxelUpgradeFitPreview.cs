using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    /// <summary>Isolated upgrade assembly viewer. Selection never purchases or saves anything.</summary>
    public sealed class VoxelUpgradeFitPreview : EditorWindow
    {
        [SerializeField] private VoxelCarDefinition definition;
        [SerializeField] private bool upgradesOnly, hideBody;
        [SerializeField] private float yaw = 130, pitch = 20, zoom = 3.2f;
        private List<VoxelUpgradeFitEntry> entries = new();
        private PreviewRenderUtility preview;
        private GameObject car;
        private HashSet<Renderer> baseRenderers;
        private Dictionary<Renderer, bool> baseVisibility;
        private Vector2 scroll;
        private string error;

        [MenuItem("Tools/Voxel Racer/Upgrade Fit Preview")]
        public static void OpenDefault() => Open(null);

        public static void Open(ScriptableObject focus)
        {
            var window = GetWindow<VoxelUpgradeFitPreview>("Upgrade Fit Preview");
            window.minSize = new Vector2(660, 560);
            window.Discover();
            if (focus is VoxelArmorTuning armor)
                window.definition = VoxelUpgradeFitCatalog.Assets<VoxelCarDefinition>()
                    .FirstOrDefault(c => c.visualPrefab == armor.compatibleCarPrefab);
            foreach (var entry in window.entries) entry.Selected = focus == null || entry.Asset == focus;
            window.Rebuild();
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += ProjectChanged;
            Discover();
            Rebuild();
        }

        private void ProjectChanged() { Discover(); Rebuild(); }

        private void Discover()
        {
            var selected = entries.Where(e => e.Selected).Select(e => e.Key).ToHashSet();
            entries = VoxelUpgradeFitCatalog.Discover();
            foreach (var entry in entries) entry.Selected = selected.Contains(entry.Key);
            if (definition == null) definition = VoxelUpgradeFitCatalog.Assets<VoxelCarDefinition>()
                .FirstOrDefault(c => c.name == "SpyCar2PlayerCar") ??
                VoxelUpgradeFitCatalog.Assets<VoxelCarDefinition>().FirstOrDefault();
        }

        private void Rebuild()
        {
            preview?.Cleanup(); preview = null; car = null; error = null;
            if (definition == null || definition.visualPrefab == null) return;
            preview = new PreviewRenderUtility();
            preview.camera.orthographic = true;
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 100;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.18f, .22f, .27f);
            preview.lights[0].intensity = 1.1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
            preview.lights[1].intensity = .6f;
            preview.ambientColor = new Color(.4f, .4f, .4f);
            car = Instantiate(definition.visualPrefab);
            preview.AddSingleGO(car);
            baseRenderers = car.GetComponentsInChildren<Renderer>(true).ToHashSet();
            foreach (var entry in entries.Where(e => e.Selected && e.Fits(definition)))
            {
                try { entry.Build(car.transform); }
                catch (System.Exception exception) { error = entry.Label + ": " + exception.Message; }
            }
            foreach (var behaviour in car.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            baseVisibility = baseRenderers.ToDictionary(r => r, r => r.enabled);
            foreach (var body in car.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            SetVisibility(); Repaint();
        }

        private void SetVisibility()
        {
            if (car == null) return;
            foreach (var renderer in baseRenderers)
            {
                bool body = renderer.GetComponentInParent<VoxelIndestructiblePart>() == null;
                renderer.enabled = baseVisibility[renderer] && !upgradesOnly && !(hideBody && body);
            }
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            definition = (VoxelCarDefinition)EditorGUILayout.ObjectField("Car", definition, typeof(VoxelCarDefinition), false);
            if (EditorGUI.EndChangeCheck()) Rebuild();
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(150));
            bool changed = false;
            foreach (var entry in entries)
            {
                EditorGUILayout.BeginHorizontal();
                bool fits = entry.Fits(definition);
                using (new EditorGUI.DisabledScope(!fits))
                {
                    bool selected = EditorGUILayout.ToggleLeft(entry.Label + (fits ? "" : " (unavailable for this car)"), entry.Selected);
                    if (selected != entry.Selected) { entry.Selected = selected; changed = true; }
                }
                if (GUILayout.Button("Tuning", GUILayout.Width(60))) { Selection.activeObject = entry.Asset; EditorGUIUtility.PingObject(entry.Asset); }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            if (changed) Rebuild();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All")) { foreach (var e in entries) e.Selected = true; Rebuild(); }
            if (GUILayout.Button("None")) { foreach (var e in entries) e.Selected = false; Rebuild(); }
            if (GUILayout.Button("Refresh fit")) { Discover(); Rebuild(); }
            if (GUILayout.Button("Other side")) { yaw += 180; Repaint(); }
            if (GUILayout.Button("Reset view")) { yaw = 130; pitch = 20; zoom = 3.2f; Repaint(); }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            upgradesOnly = GUILayout.Toggle(upgradesOnly, "Upgrades only", "Button");
            hideBody = GUILayout.Toggle(hideBody, "Hide body / show chassis", "Button");
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) { SetVisibility(); Repaint(); }
            EditorGUILayout.LabelField("Drag to rotate · Scroll to zoom · Tuning assets are discovered automatically", EditorStyles.wordWrappedLabel);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
            Rect rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (preview == null) return;
            Event ev = Event.current;
            if (rect.Contains(ev.mousePosition))
            {
                if (ev.type == EventType.MouseDrag && ev.button == 0)
                { yaw += ev.delta.x; pitch = Mathf.Clamp(pitch + ev.delta.y * .5f, -80, 80); ev.Use(); Repaint(); }
                else if (ev.type == EventType.ScrollWheel)
                { zoom = Mathf.Clamp(zoom + ev.delta.y * .12f, .25f, 10); ev.Use(); Repaint(); }
            }
            if (ev.type != EventType.Repaint) return;
            preview.BeginPreview(rect, GUIStyle.none);
            preview.camera.orthographicSize = zoom;
            preview.camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            preview.camera.transform.position = new Vector3(0, .7f, 0) - preview.camera.transform.forward * 15;
            preview.Render(true);
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= ProjectChanged;
            preview?.Cleanup(); preview = null; car = null;
        }
    }
}
