using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    [CustomEditor(typeof(VoxelArmorTuning)), CanEditMultipleObjects]
    public sealed class VoxelArmorTuningEditor : UnityEditor.Editor
    {
        private bool shop = true, durability = true, fit = true;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            shop = EditorGUILayout.Foldout(shop, "Shop", true);
            if (shop) Fields("displayName", "panelPrefab", "panelPurchasePrice");
            durability = EditorGUILayout.Foldout(durability, "Durability", true);
            if (durability) Fields("voxelHitPoints");
            fit = EditorGUILayout.Foldout(fit, "Car Fit", true);
            if (fit) Fields("compatibleCarPrefab", "rightMountPosition");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Each door is purchased separately. Each panel voxel adds one integrity unit; health controls how much damage it absorbs.", MessageType.Info);
            if (GUILayout.Button("Preview Fit on Car")) VoxelArmorFitPreview.Open((VoxelArmorTuning)target);
        }
        private void Fields(params string[] names)
        {
            foreach (string name in names) EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
        }
    }

    /// <summary>Isolated, interactive preview: never saves armour into the base car.</summary>
    public sealed class VoxelArmorFitPreview : EditorWindow
    {
        private VoxelArmorTuning tuning;
        private PreviewRenderUtility preview;
        private GameObject car;
        private Transform panels;
        private bool armorOnly;
        private float yaw = 130f, pitch = 20f, zoom = 3.2f;

        [MenuItem("Tools/Voxel Racer/Preview Door Armour Fit")]
        public static void OpenDefault() => Open(VoxelArmorTuning.Load());

        public static void Open(VoxelArmorTuning value)
        {
            var window = GetWindow<VoxelArmorFitPreview>("Armour Fit Preview");
            window.minSize = new Vector2(540, 420);
            window.tuning = value;
            window.Rebuild();
            window.Show();
        }

        private void Rebuild()
        {
            preview?.Cleanup();
            preview = null;
            car = null;
            if (tuning == null || tuning.compatibleCarPrefab == null || tuning.panelPrefab == null) return;
            preview = new PreviewRenderUtility();
            preview.camera.orthographic = true;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.18f, .22f, .27f);
            preview.lights[0].intensity = 1.1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
            preview.lights[1].intensity = .6f;
            preview.ambientColor = new Color(.4f, .4f, .4f);
            car = Instantiate(tuning.compatibleCarPrefab);
            foreach (var gun in car.GetComponentsInChildren<VoxelGunMount>(true)) gun.enabled = false;
            panels = VoxelArmorUpgradeState.CreatePair(car.transform, tuning);
            preview.AddSingleGO(car);
            SetVisibility();
            Repaint();
        }

        private void SetVisibility()
        {
            if (car == null) return;
            foreach (Transform child in car.transform)
                if (child != panels) child.gameObject.SetActive(!armorOnly);
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            tuning = (VoxelArmorTuning)EditorGUILayout.ObjectField("Upgrade", tuning, typeof(VoxelArmorTuning), false);
            if (EditorGUI.EndChangeCheck()) Rebuild();
            EditorGUILayout.BeginHorizontal();
            bool only = GUILayout.Toggle(armorOnly, "Armour only", "Button");
            if (only != armorOnly) { armorOnly = only; SetVisibility(); }
            if (GUILayout.Button("Other side")) yaw += 180f;
            if (GUILayout.Button("Refresh fit")) Rebuild();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Drag to rotate. Scroll to zoom. Refresh after editing the prefab or fit values.", EditorStyles.wordWrappedLabel);
            if (tuning != null && tuning.panelPrefab != null)
                EditorGUILayout.LabelField($"Each side: {VoxelCarSelectionState.CountIntegrityVoxels(tuning.panelPrefab)} integrity voxels | {tuning.voxelHitPoints} HP per voxel");
            Rect rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (preview == null) return;
            Event e = Event.current;
            if (rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag && e.button == 0)
                { yaw += e.delta.x; pitch = Mathf.Clamp(pitch + e.delta.y * .5f, -35, 80); e.Use(); Repaint(); }
                else if (e.type == EventType.ScrollWheel)
                { zoom = Mathf.Clamp(zoom + e.delta.y * .12f, .6f, 6f); e.Use(); Repaint(); }
            }
            if (e.type != EventType.Repaint) return;
            preview.BeginPreview(rect, GUIStyle.none);
            preview.camera.orthographicSize = zoom;
            preview.camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            preview.camera.transform.position = new Vector3(0, .7f, 0) - preview.camera.transform.forward * 12f;
            preview.Render(true);
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
        }

        private void OnDisable() { preview?.Cleanup(); preview = null; car = null; }
    }
}
