using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    /// <summary>Edits the real upgrade tunings through Unity serialized properties.</summary>
    public sealed class VoxelUpgradeDashboard : EditorWindow
    {
        [SerializeField] private ScriptableObject selectedAsset;
        [SerializeField] private string search = "", category = "All categories";
        [SerializeField] private bool showMounting;
        [SerializeField] private VoxelUpgradeDashboardSort sortOrder;
        private static readonly string[] SortLabels = { "Name: A to Z", "Name: Z to A", "Price: low to high", "Price: high to low", "Category" };
        private List<VoxelUpgradeDashboardEntry> entries = new();
        private List<RowLayout> visibleEntries = new();
        private VoxelUpgradeDashboardEntry detailEntry;
        private string detailName, detailDescription, detailCompatibility;
        private ScriptableObject pendingSelection, detailTuning;
        private bool selectionRequested, drawMounting, resetListScroll;
        private VoxelCarDefinition[] cars = System.Array.Empty<VoxelCarDefinition>();
        private Vector2 listScroll, detailScroll;
        private readonly Dictionary<ScriptableObject, TuningView> views = new();
        private readonly HashSet<ScriptableObject> pendingSaves = new();
        private bool refreshRequested;
        private GUIStyle wrapped, smallWrapped, titleStyle, detailTitleStyle, rowNameStyle, badgeStyle, filterStyle, pageStyle;
        private readonly List<CategoryFilter[]> filterRows = new();

        private sealed class CategoryFilter
        {
            public string Name, Label;
            public float Width;
        }

        // The same restrained accent follows a category through its filter, rows and details.
        private static Color CategoryColor(string name)
        {
            Color color = name switch
            {
                "Armour" => new Color(.54f, .72f, .84f),
                "Boost" => new Color(.40f, .76f, .88f),
                "Engines" => new Color(.83f, .64f, .43f),
                "Guns" => new Color(.79f, .56f, .53f),
                "Missiles" => new Color(.74f, .60f, .83f),
                "Ploughs" => new Color(.76f, .73f, .48f),
                "Wheels" => new Color(.48f, .76f, .63f),
                _ => new Color(.66f, .70f, .76f)
            };
            return EditorGUIUtility.isProSkin ? color : Color.Lerp(color, Color.black, .43f);
        }

        private static void DrawAccentPanel(Rect rect, Color accent, bool selected = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(rect, new Color(accent.r, accent.g, accent.b, selected ? .16f : .055f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1, selected ? 4 : 3, rect.height - 2), accent);
            if (selected)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), accent);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), accent);
            }
        }

        private void DrawCategoryBadge(string name)
        {
            Color accent = CategoryColor(name);
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(name), badgeStyle, GUILayout.ExpandWidth(false));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(accent.r, accent.g, accent.b, .13f));
            badgeStyle.normal.textColor = accent;
            GUI.Label(rect, name, badgeStyle);
        }

        // Standalone SerializedProperty controls avoid nesting Odin's Inspector layout inside
        // another editor window. Each asset retains its own independent foldout state.
        internal sealed class TuningView
        {
            public readonly SerializedObject Serialized;
            public readonly List<FieldGroup> Groups = new();
            public TuningView(ScriptableObject asset)
            {
                Serialized = new SerializedObject(asset);
                var iterator = Serialized.GetIterator();
                bool children = true;
                while (iterator.NextVisible(children))
                {
                    children = false;
                    if (iterator.propertyPath == "m_Script") continue;
                    var member = asset.GetType().GetField(iterator.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    string section = member == null ? "Settings" : VoxelTrackOdinLayout.Section(member.DeclaringType, member.Name);
                    section = VoxelOdinTuningLayout.UpgradeSection(asset.GetType(), iterator.name, section);
                    var group = Groups.FirstOrDefault(g => g.Name == section);
                    if (group == null) { group = new FieldGroup { Name = section }; Groups.Add(group); }
                    group.Fields.Add(new TuningField { Path = iterator.propertyPath,
                        Label = new GUIContent(iterator.displayName + VoxelOdinTuningLayout.Unit(iterator.name), iterator.tooltip) });
                }
            }
            public void Prepare()
            {
                Serialized.UpdateIfRequiredOrScript();
                foreach (var group in Groups)
                {
                    group.DrawExpanded = group.Expanded;
                    foreach (var field in group.Fields)
                        field.Visible = VoxelOdinTuningLayout.UpgradeFieldVisible(Serialized, field.Path);
                }
            }
        }
        internal sealed class FieldGroup
        {
            public string Name;
            public bool Expanded = true, DrawExpanded = true;
            public readonly List<TuningField> Fields = new();
        }
        internal sealed class TuningField
        {
            public string Path;
            public GUIContent Label;
            public bool Visible = true;
        }

        private sealed class RowLayout
        {
            public VoxelUpgradeDashboardEntry Entry;
            public string Name, Description, Category, PriceUnit;
            public int? Price;
        }

        [MenuItem("Tools/Voxel Racer/Upgrade Dashboard")]
        public static void Open()
        {
            var window = GetWindow<VoxelUpgradeDashboard>("Upgrade Dashboard");
            window.minSize = new Vector2(1180, 640);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(1180, 640);
            EditorApplication.projectChanged += ProjectChanged;
            Undo.undoRedoPerformed += UndoRedo;
            Discover();
        }
        private void OnDisable()
        {
            EditorApplication.projectChanged -= ProjectChanged;
            Undo.undoRedoPerformed -= UndoRedo;
            EditorApplication.delayCall -= FlushSaves;
            FlushSaves();
            foreach (var view in views.Values) view.Serialized.Dispose();
            views.Clear();
        }
        private void ProjectChanged() { refreshRequested = true; Repaint(); }
        private void UndoRedo()
        {
            foreach (var asset in AllAssets().Where(EditorUtility.IsDirty)) QueueSave(asset);
            Repaint();
        }
        private IEnumerable<ScriptableObject> AllAssets() => entries
            .SelectMany(e => new[] { e.Asset, e.PriceAsset }).Where(a => a != null).Distinct();
        private void Discover()
        {
            entries = VoxelUpgradeDashboardCatalog.Discover();
            if (category == "Wheel spikes") category = "Wheels";
            if (category != "All categories" && !entries.Any(e => e.Category == category)) category = "All categories";
            cars = VoxelUpgradeFitCatalog.Assets<VoxelCarDefinition>().Where(c => c.visualPrefab != null).ToArray();
            if (!entries.Any(e => e.Asset == selectedAsset)) Select(entries.FirstOrDefault()?.Asset);
            refreshRequested = false;
        }
        private void Select(ScriptableObject asset)
        {
            if (selectedAsset == asset) return;
            selectedAsset = asset;
            detailScroll = Vector2.zero;
            Repaint();
        }
        private void RequestSelection(ScriptableObject asset)
        {
            if (selectedAsset == asset) return;
            pendingSelection = asset;
            selectionRequested = true;
            Repaint();
        }
        private void PrepareListLayout()
        {
            if (refreshRequested) Discover();
            PrepareCategoryFilters();
            // Keep row controls stable throughout the input/Repaint pass, especially while
            // typing a price that will change the sort order when committed.
            if (visibleEntries.Count == 0 || (GUIUtility.hotControl == 0 &&
                (!EditorGUIUtility.editingTextField || GUI.GetNameOfFocusedControl() == "upgrade-search")))
                visibleEntries = VoxelUpgradeDashboardCatalog.Sort(entries, sortOrder)
                    .Where(e => (category == "All categories" || category == e.Category) && e.Matches(search))
                    .Select(e => new RowLayout { Entry = e, Name = e.Name, Description = e.Description,
                        Category = e.Category, PriceUnit = e.PriceUnit, Price = e.Price }).ToList();
            if (resetListScroll) { listScroll = Vector2.zero; resetListScroll = false; }
        }
        private void PrepareCategoryFilters()
        {
            filterRows.Clear();
            var row = new List<CategoryFilter>();
            float used = 0, available = Mathf.Max(300, position.width - 28);
            foreach (string name in new[] { "All categories" }.Concat(VoxelUpgradeDashboardCatalog.Categories(entries)))
            {
                int count = name == "All categories" ? entries.Count : entries.Count(e => e.Category == name);
                string label = (name == "All categories" ? "All parts" : name) + "  " + count;
                float width = filterStyle.CalcSize(new GUIContent(label)).x + 14;
                if (row.Count > 0 && used + width > available)
                { filterRows.Add(row.ToArray()); row.Clear(); used = 0; }
                row.Add(new CategoryFilter { Name = name, Label = label, Width = width });
                used += width + 5;
            }
            if (row.Count > 0) filterRows.Add(row.ToArray());
        }
        private void PrepareDetailLayout()
        {
            if (selectionRequested)
            {
                Select(pendingSelection);
                selectionRequested = false;
                GUI.FocusControl(null);
            }
            detailEntry = entries.FirstOrDefault(e => e.Asset == selectedAsset);
            detailTuning = detailEntry?.PriceAsset;
            detailName = detailEntry?.Name;
            detailDescription = detailEntry?.Description;
            string[] compatible = detailEntry == null ? System.Array.Empty<string>() : cars
                .Where(c => detailEntry.Fits.Any(f => f.Fits(c))).Select(c => c.displayName).Distinct().ToArray();
            detailCompatibility = compatible.Length == 0 ? "No compatible car configured" : string.Join(", ", compatible);
            drawMounting = showMounting;
            foreach (var removed in views.Keys.Where(a => a == null).ToArray())
            { views[removed].Serialized.Dispose(); views.Remove(removed); }
            if (detailTuning != null) ViewFor(detailTuning).Prepare();
            if (detailEntry != null && detailTuning != detailEntry.Asset) ViewFor(detailEntry.Asset).Prepare();
        }
        private void QueueSave(ScriptableObject asset)
        {
            if (asset == null || !AssetDatabase.Contains(asset)) return;
            pendingSaves.Add(asset);
            EditorApplication.delayCall -= FlushSaves;
            EditorApplication.delayCall += FlushSaves;
        }
        private void FlushSaves()
        {
            EditorApplication.delayCall -= FlushSaves;
            foreach (var asset in pendingSaves)
                if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
            pendingSaves.Clear();
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (Event.current.type == EventType.Layout)
            {
                PrepareListLayout();
                PrepareDetailLayout();
            }
            using (new EditorGUILayout.VerticalScope(pageStyle))
            {
                DrawHeader();
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawList(Mathf.Max(650f, (position.width - 24f) * .57f));
                    GUILayout.Space(8);
                    DrawDetails();
                }
                GUILayout.Space(4);
                GUILayout.Label("Edits save automatically to tuning assets  /  Undo and Redo supported", EditorStyles.miniLabel);
            }
        }
        private void EnsureStyles()
        {
            wrapped ??= new GUIStyle(EditorStyles.wordWrappedLabel) { padding = new RectOffset(4, 4, 3, 3) };
            smallWrapped ??= new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            titleStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 20 };
            detailTitleStyle ??= new GUIStyle(titleStyle) { fontSize = 17, wordWrap = true, padding = new RectOffset(6, 4, 3, 3) };
            rowNameStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel) { fontStyle = FontStyle.Bold, padding = new RectOffset(4, 4, 3, 3) };
            badgeStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { padding = new RectOffset(7, 7, 3, 3), margin = new RectOffset(4, 4, 2, 4) };
            filterStyle ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(12, 12, 4, 4) };
            pageStyle ??= new GUIStyle { padding = new RectOffset(12, 12, 10, 8) };
        }
        private void DrawHeader()
        {
            EnsureStyles();
            GUILayout.Label("Upgrade Dashboard", titleStyle);
            GUILayout.Label("Compare prices and effects. Select an upgrade to edit all of its tuning values.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName("upgrade-search");
                search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField, GUILayout.Width(240));
                GUILayout.Space(8);
                GUILayout.Label("Sort", EditorStyles.miniLabel, GUILayout.Width(28));
                sortOrder = (VoxelUpgradeDashboardSort)EditorGUILayout.Popup((int)sortOrder, SortLabels,
                    EditorStyles.toolbarPopup, GUILayout.Width(160));
                if (EditorGUI.EndChangeCheck()) { resetListScroll = true; Repaint(); }
                GUILayout.FlexibleSpace();
                GUILayout.Label(visibleEntries.Count + " of " + entries.Count + " upgrades", EditorStyles.miniLabel);
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65))) refreshRequested = true;
            }
            GUILayout.Space(8);
            foreach (var row in filterRows)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var filter in row)
                    {
                        bool active = category == filter.Name;
                        Color accent = CategoryColor(filter.Name);
                        Rect rect = GUILayoutUtility.GetRect(filter.Width, 30, GUILayout.Width(filter.Width));
                        if (Event.current.type == EventType.Repaint)
                        {
                            EditorGUI.DrawRect(rect, new Color(accent.r, accent.g, accent.b, active ? .21f : .07f));
                            if (active) EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3, rect.width, 3), accent);
                        }
                        filterStyle.normal.textColor = active ? accent : EditorStyles.label.normal.textColor;
                        if (GUI.Button(rect, filter.Label, filterStyle) && !active)
                        {
                            category = filter.Name;
                            resetListScroll = true;
                            GUI.FocusControl(null);
                            Repaint();
                        }
                        GUILayout.Space(5);
                    }
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Space(4);
            }
            GUILayout.Space(6);
        }

        private void DrawList(float width)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label("UPGRADE", GUILayout.Width(195));
                    GUILayout.Label("PRICE", GUILayout.Width(105));
                    GUILayout.Label("WHAT IT DOES");
                }
                listScroll = EditorGUILayout.BeginScrollView(listScroll);
                int visible = 0;
                foreach (var row in visibleEntries)
                {
                    var entry = row.Entry;
                    visible++;
                    using (var rowScope = new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.MinHeight(86)))
                    {
                        DrawAccentPanel(rowScope.rect, CategoryColor(row.Category), entry.Asset == selectedAsset);
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(195)))
                        {
                            if (GUILayout.Button(row.Name, rowNameStyle, GUILayout.MinHeight(24))) RequestSelection(entry.Asset);
                            DrawCategoryBadge(row.Category);
                            if (GUILayout.Button("Edit tuning", EditorStyles.miniButton, GUILayout.Width(90))) RequestSelection(entry.Asset);
                        }
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(105)))
                        {
                            int? price = row.Price;
                            if (price.HasValue)
                            {
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    GUILayout.Label("$", GUILayout.Width(10));
                                    EditorGUI.BeginChangeCheck();
                                    GUI.SetNextControlName("upgrade-price-" + entry.Asset.GetInstanceID());
                                    int value = EditorGUILayout.DelayedIntField(price.Value);
                                    if (EditorGUI.EndChangeCheck() && entry.SetPrice(value))
                                    { QueueSave(entry.PriceAsset); GUI.FocusControl(null); Repaint(); }
                                }
                                GUILayout.Label(row.PriceUnit, smallWrapped);
                            }
                            else GUILayout.Label("Unconfigured", smallWrapped);
                        }
                        GUILayout.Label(row.Description, wrapped, GUILayout.ExpandWidth(true));
                        // Buttons/price fields consume their own events first. The remaining
                        // row background and description are also selection targets.
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rowScope.rect.Contains(Event.current.mousePosition))
                        { RequestSelection(entry.Asset); GUI.FocusControl(null); Event.current.Use(); }
                    }
                    GUILayout.Space(3);
                }
                if (visible == 0) EditorGUILayout.HelpBox("No upgrades match the current search and category.", MessageType.Info);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDetails()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                var entry = detailEntry;
                if (entry == null) { GUILayout.Label("Select an upgrade to edit its tuning."); return; }
                using var scrolling = new EditorGUILayout.ScrollViewScope(detailScroll);
                detailScroll = scrolling.scrollPosition;
                using (var heading = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawAccentPanel(heading.rect, CategoryColor(entry.Category), true);
                    DrawCategoryBadge(entry.Category);
                    GUILayout.Label(detailName, detailTitleStyle);
                    GUILayout.Label(detailDescription, wrapped);
                    GUILayout.Space(4);
                }
                GUILayout.Space(4);
                GUILayout.Label("Fits: " + detailCompatibility, smallWrapped);
                GUILayout.Label(AssetDatabase.GetAssetPath(entry.Asset), smallWrapped);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select asset")) { Selection.activeObject = entry.Asset; EditorGUIUtility.PingObject(entry.Asset); }
                    if (GUILayout.Button("Fit preview")) VoxelUpgradeFitPreview.Open(entry.Asset);
                }
                GUILayout.Space(8);
                if (detailTuning != null) DrawTuning(detailTuning);
                if (detailTuning != entry.Asset)
                {
                    bool expanded = EditorGUILayout.Foldout(showMounting, "Launcher mounting and compatibility", true);
                    if (expanded != showMounting) { showMounting = expanded; Repaint(); }
                    if (drawMounting || detailTuning == null) DrawTuning(entry.Asset);
                }
            }
        }
        private TuningView ViewFor(ScriptableObject asset)
        {
            if (!views.TryGetValue(asset, out var view))
            {
                view = new TuningView(asset);
                views.Add(asset, view);
            }
            return view;
        }
        private void DrawTuning(ScriptableObject asset)
        {
            var view = ViewFor(asset);
            view.Serialized.UpdateIfRequiredOrScript();
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp((position.width * .43f - 50f) * .52f, 160f, 280f);
            foreach (var group in view.Groups)
            {
                if (!group.Fields.Any(f => f.Visible)) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool expanded = EditorGUILayout.Foldout(group.Expanded, group.Name, true, EditorStyles.foldoutHeader);
                    if (expanded != group.Expanded) { group.Expanded = expanded; Repaint(); }
                    if (!group.DrawExpanded) continue;
                    foreach (var field in group.Fields)
                    {
                        if (!field.Visible) continue;
                        var property = view.Serialized.FindProperty(field.Path);
                        if (property != null) EditorGUILayout.PropertyField(property, field.Label, true);
                    }
                }
            }
            EditorGUIUtility.labelWidth = previousLabelWidth;
            // Applying actual serialized edits records Undo, runs OnValidate and marks dirty.
            // Expanding/collapsing groups never queues a save/import cycle.
            if (view.Serialized.ApplyModifiedProperties()) { QueueSave(asset); Repaint(); }
        }
    }
}
