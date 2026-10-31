using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelBichoLocoBuilder
    {
        public const string PrefabPath = "Assets/Resources/Bosses/ElBichoLoco.prefab";
        public const string DefinitionPath = "Assets/Resources/Bosses/ElBichoLocoBoss.asset";
        public const string AttackPath = "Assets/Resources/Bosses/BichoLocoAttackTuning.asset";
        public const string MinePath = "Assets/Resources/Bosses/BichoLocoMineTuning.asset";
        public const string BodyBehaviourPath = "Assets/Resources/Bosses/BichoLocoBodyBehaviour.asset";
        public const string TrackPath = "Assets/Resources/Tracks/TrackBoss02.asset";
        private const string Art = "Assets/Resources/Bosses/BichoLocoArt";

        [MenuItem("Tools/Voxel Racer/Bosses/Build El Bicho Loco")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Build El Bicho Loco in Edit Mode.");
            Directory.CreateDirectory(Art);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(VoxelPsychoBugBuilder.PrefabPath);
            if (source == null) throw new InvalidOperationException("The existing Psycho Bug prefab is required.");
            var black = Material("Black", new Color(.022f, .025f, .03f), .25f);
            var red = Material("Red", new Color(.8f, .012f, .018f), .5f);
            var lampOff = Material("LampOff", new Color(.25f, .006f, .008f), .25f);
            var lampOn = BrakeLightGlow();
            var headlamp = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Effects/NightEnemyHeadlampGlow.mat");
            var rubber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/EnemyVehicles/PsychoBugArt/RoughRubber.mat");
            var tyreMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Resources/EnemyVehicles/PsychoBugArt/OctagonalTyre.asset");
            var hubMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Resources/EnemyVehicles/PsychoBugArt/OctagonalHub.asset");
            var root = Object.Instantiate(source); root.name = "El Bicho Loco";
            float referenceWidth, halfLength;
            try
            {
                Bounds body = root.GetComponentsInChildren<MeshRenderer>()[0].bounds;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    body.Encapsulate(renderer.bounds);
                    string material = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty;
                    if (renderer.name == "Headlight lens" && headlamp != null) renderer.sharedMaterial = headlamp;
                    else if (renderer.name == "Steel wheel spoke") renderer.sharedMaterial = black;
                    else if (material.Contains("SpikeSteel") || material.Contains("GunmetalRim") || material.Contains("HubRed")) renderer.sharedMaterial = red;
                    else if (material.Contains("BlackPaint"))
                    {
                        Vector3 p = root.transform.InverseTransformPoint(renderer.bounds.center);
                        bool stripe = p.y > .6f && Mathf.Abs(Mathf.Abs(p.x) - .36f) < .105f;
                        bool wing = renderer.name == "Rounded wing voxel" && p.y > .9f;
                        renderer.sharedMaterial = stripe || wing ? red : black;
                    }
                }
                referenceWidth = body.size.x;
                halfLength = Mathf.Max(Mathf.Abs(body.min.z), Mathf.Abs(body.max.z));
                float lane = referenceWidth / 1.8f;
                var rig = root.AddComponent<VoxelBichoLocoRig>();
                for (int slot = 0; slot < 4; slot++)
                {
                    float side = slot < 2 ? -1 : 1;
                    var go = new GameObject("Hopper " + (slot + 1));
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = new Vector3((slot - 1.5f) * lane, 0, body.min.z - lane * .39f);
                    go.AddComponent<VoxelIndestructiblePart>(); // Damage belongs to its independent health, never the car's voxel selector.
                    var hopper = go.AddComponent<VoxelBichoHopper>(); rig.hoppers[slot] = hopper;
                    hopper.slot = slot; hopper.lampOff = lampOff; hopper.lampOn = lampOn;
                    for (int row = 0; row < 4; row++)
                    {
                        float width = .36f + row * .09f, depth = .31f + row * .08f;
                        Block(go.transform, "Hopper stepped casing", new Vector3(0, .3f + row * .14f, 0) * lane,
                            new Vector3(width, .14f, depth) * lane, black);
                    }
                    Block(go.transform, "Red lid rim", new Vector3(0, .83f, 0) * lane, new Vector3(.70f, .07f, .64f) * lane, red);
                    Block(go.transform, "Black roof", new Vector3(0, .88f, 0) * lane, new Vector3(.70f, .06f, .64f) * lane, black);
                    hopper.roofLamp = Block(go.transform, "Sequential roof beacon", new Vector3(0, .935f, 0) * lane,
                        new Vector3(.15f, .05f, .13f) * lane, lampOff).GetComponent<MeshRenderer>();
                    Block(go.transform, "Rear status lens", new Vector3(0, .63f, -.29f) * lane, new Vector3(.16f, .11f, .025f) * lane, red);
                    Block(go.transform, "Mine outlet", new Vector3(0, .16f, -.25f) * lane, new Vector3(.22f, .10f, .25f) * lane, red);
                    var outlet = new GameObject("Mine release point").transform; outlet.SetParent(go.transform, false);
                    outlet.localPosition = new Vector3(0, .1f, -.43f) * lane; hopper.mineOutlet = outlet;
                    Block(go.transform, "Crossbeam segment", new Vector3(0, .48f, .27f) * lane, new Vector3(.98f, .12f, .13f) * lane, black);
                    foreach (int edge in new[] { -1, 1 })
                        Block(go.transform, "Red linking joint", new Vector3(edge * .46f, .48f, .27f) * lane, new Vector3(.08f, .18f, .17f) * lane, red);
                    if (slot == 1 || slot == 2)
                        Block(go.transform, "Short direct chassis bracket", new Vector3(0, .42f, .37f) * lane,
                            new Vector3(.14f, .16f, .35f) * lane, red);
                    Block(go.transform, "Side wheel axle", new Vector3(side * .35f, .30f, 0) * lane, new Vector3(.25f, .09f, .10f) * lane, red);
                    var wheel = new GameObject("Hopper Support Wheel").transform; wheel.SetParent(go.transform, false);
                    wheel.localPosition = new Vector3(side * .39f, .30f, 0) * lane; hopper.wheel = wheel;
                    Mesh(wheel, "Black octagonal tyre", Vector3.zero, new Vector3(.18f / .32f, .60f / .79f, .60f / .79f) * lane, tyreMesh, rubber);
                    Mesh(wheel, "Red wheel hub", new Vector3(side * .09f, 0, 0) * lane,
                        new Vector3(.04f / .05f, .38f / .54f, .38f / .54f) * lane, hubMesh, red);
                    // The four-lane beam sets width, not hopper height. Keep the Bug's
                    // cabin clearly above the low implement in the normal chase view.
                    const float heightScale = .6f;
                    foreach (Transform child in go.transform)
                    {
                        var position = child.localPosition; position.y *= heightScale; child.localPosition = position;
                        if (child == wheel) continue;
                        var size = child.localScale; size.y *= heightScale; child.localScale = size;
                    }
                    foreach (Transform surface in wheel)
                    {
                        var size = surface.localScale; size.y *= heightScale; size.z *= heightScale; surface.localScale = size;
                    }
                }
                ConfigureHopperDetails(root);
                ConfigureBeacons(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }

            var mine = AssetDatabase.LoadAssetAtPath<VoxelMineLayerTuning>(MinePath);
            if (mine == null)
            {
                var original = AssetDatabase.LoadAssetAtPath<VoxelMineLayerTuning>(VoxelBossMineBuilder.TuningPath) ?? Resources.Load<VoxelMineLayerTuning>("EnemyVehicles/MineLayerAttackTuning");
                if (original == null) throw new InvalidOperationException("An existing mine tuning and model are required.");
                mine = Object.Instantiate(original); mine.name = "El Bicho Loco Mines";
                AssetDatabase.CreateAsset(mine, MinePath);
            }
            var attack = AssetDatabase.LoadAssetAtPath<VoxelBichoLocoTuning>(AttackPath);
            if (attack == null)
            {
                attack = ScriptableObject.CreateInstance<VoxelBichoLocoTuning>(); attack.mineTuning = mine;
                attack.usePsychoBehaviourAfterHoppers = true;
                AssetDatabase.CreateAsset(attack, AttackPath);
            }
            if (attack.bodyPhaseBehaviour == null)
            { attack.bodyPhaseBehaviour = EnsureBodyBehaviour(); EditorUtility.SetDirty(attack); }
            var boss = AssetDatabase.LoadAssetAtPath<VoxelBossDefinition>(DefinitionPath);
            if (boss == null)
            {
                var original = AssetDatabase.LoadAssetAtPath<VoxelBossDefinition>(VoxelRedVanBossBuilder.DefinitionPath);
                boss = original != null ? Object.Instantiate(original) : ScriptableObject.CreateInstance<VoxelBossDefinition>();
                boss.name = "ElBichoLocoBoss"; boss.bossName = "El Bicho Loco";
                boss.referenceBodyWidth = referenceWidth; boss.bodyCollisionHalfLength = halfLength;
                boss.minimumDistanceAhead = 28; boss.maximumDistanceAhead = 120;
                boss.escapeWarningDistance = 200; boss.escapeFailureDistance = 250;
                boss.catchUpDistance = 180; boss.catchUpResumeDistance = 50;
                boss.attacks = new VoxelBossAttackDefinition[] { attack };
                boss.detachSpikesOnDestruction = false;
                boss.introductionPoster = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/El Bicho Loco Boss Introduction v3.png");
                AssetDatabase.CreateAsset(boss, DefinitionPath);
            }
            boss.bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); EditorUtility.SetDirty(boss);

            bool newTrack = AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(TrackPath) == null;
            if (newTrack && !AssetDatabase.CopyAsset(VoxelRedVanBossBuilder.TrackPath, TrackPath))
                throw new InvalidOperationException("Could not duplicate the existing boss mission.");
            var track = AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(TrackPath);
            if (newTrack)
            {
                track.displayName = "El Bicho Loco"; track.boss = boss;
                track.roadTuning.laneCount = 4; track.roadTuning.roadWidth = 20;
                if (track.missionTuning != null) track.missionTuning.displayName = "El Bicho Loco";
                EditorUtility.SetDirty(track); EditorUtility.SetDirty(track.roadTuning); EditorUtility.SetDirty(track.missionTuning);
            }
            var sequence = VoxelTrackSequence.Load();
            if (sequence != null && !(sequence.tracks ?? Array.Empty<VoxelTrackDefinition>()).Contains(track))
            { sequence.tracks = (sequence.tracks ?? Array.Empty<VoxelTrackDefinition>()).Append(track).ToArray(); EditorUtility.SetDirty(sequence); }
            AssetDatabase.SaveAssets();
            Selection.activeObject = boss; EditorGUIUtility.PingObject(boss);
            Debug.Log("El Bicho Loco ready: four independent hoppers, radar opening and TrackBoss02 appended once. Existing tunings retained on rebuild.");
        }

        private static Material Material(string name, Color color, float metallic)
        {
            string path = Art + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Bicho " + name, color = color };
            material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", .35f);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        [MenuItem("Tools/Voxel Racer/Bosses/Update El Bicho Loco Hopper Details")]
        public static void UpdateHopperDetails()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Update the boss prefab in Edit Mode.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { ConfigureHopperDetails(root); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("El Bicho Loco hopper wheel stars, trim, rivets and hinges updated; gameplay tuning preserved.");
        }

        // Combine small fittings by material, rather than adding a renderer for every rivet.
        public static void ConfigureHopperDetails(GameObject root)
        {
            var black = Material("Black", new Color(.022f, .025f, .03f), .25f);
            var red = Material("Red", new Color(.8f, .012f, .018f), .5f);
            var steel = Material("HopperSteel", new Color(.16f, .175f, .19f), .65f);
            var redParts = new List<CombineInstance>();
            var steelParts = new List<CombineInstance>();
            var starParts = new List<CombineInstance>();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(cube);
            void Box(List<CombineInstance> parts, Vector3 position, Vector3 size, float angle = 0)
                => parts.Add(new CombineInstance { mesh = cubeMesh,
                    transform = Matrix4x4.TRS(position, Quaternion.Euler(angle, 0, 0), size) });
            // Normalised to the original hopper lane spacing, including its low .6-height casing.
            for (int row = 0; row < 4; row++)
            {
                float width = .36f + row * .09f, depth = .31f + row * .08f;
                float y = (.3f + row * .14f) * .6f;
                Box(redParts, new Vector3(0, y - .032f, -depth * .5f - .004f), new Vector3(width + .012f, .012f, .014f));
                foreach (int side in new[] { -1, 1 })
                {
                    // A pair of strapped, bolted edges follows the stepped rear casing.
                    float x = side * (width * .5f - .027f);
                    Box(steelParts, new Vector3(x, y, -depth * .5f - .009f), new Vector3(.027f, .074f, .015f));
                    foreach (float dy in new[] { -.022f, .022f })
                        Box(redParts, new Vector3(x, y + dy, -depth * .5f - .019f), new Vector3(.014f, .014f, .009f));
                    Box(steelParts, new Vector3(side * (width * .5f + .004f), y, 0), new Vector3(.012f, .024f, depth * .7f));
                }
            }
            foreach (int side in new[] { -1, 1 })
            {
                // Roof perimeter rails, corner bolts and two rear hinge barrels.
                Box(redParts, new Vector3(side * .338f, .54f, 0), new Vector3(.018f, .018f, .62f));
                foreach (int end in new[] { -1, 1 })
                    Box(steelParts, new Vector3(side * .303f, .550f, end * .275f), new Vector3(.023f, .009f, .023f));
                Box(steelParts, new Vector3(side * .20f, .520f, -.326f), new Vector3(.09f, .032f, .032f));
                Box(redParts, new Vector3(side * .20f, .520f, -.345f), new Vector3(.018f, .040f, .012f));
            }
            // Reinforced outlet collar and a central service hatch latch above it.
            foreach (int side in new[] { -1, 1 })
                Box(steelParts, new Vector3(side * .118f, .096f, -.25f), new Vector3(.018f, .068f, .25f));
            Box(steelParts, new Vector3(0, .285f, -.231f), new Vector3(.07f, .044f, .02f));
            Box(redParts, new Vector3(0, .285f, -.248f), new Vector3(.021f, .025f, .018f));
            for (int spoke = 0; spoke < 6; spoke++)
                Box(starParts, Vector3.zero, new Vector3(1, .145f, 1), spoke * 30f);

            var redMesh = DetailMesh("HopperRedFittings", redParts);
            var steelMesh = DetailMesh("HopperSteelFittings", steelParts);
            var starMesh = DetailMesh("HopperWheelStar", starParts);
            foreach (var hopper in root.GetComponentsInChildren<VoxelBichoHopper>(true))
            {
                float lane = hopper.transform.Find("Black roof").localScale.x / .70f;
                Fit(hopper.transform, "Red mechanical trim", Vector3.zero, Vector3.one * lane, redMesh, red);
                Fit(hopper.transform, "Bolted mechanical fittings", Vector3.zero, Vector3.one * lane, steelMesh, steel);
                float side = hopper.slot < 2 ? -1 : 1;
                // Recess the red hub slightly so the raised star keeps the original wheel envelope.
                var hub = hopper.wheel.Find("Red wheel hub");
                var hubPosition = hub.localPosition; hubPosition.x = side * .08f * lane; hub.localPosition = hubPosition;
                Fit(hopper.wheel, "Black wheel star", new Vector3(side * .1035f * lane, 0, 0),
                    new Vector3(.013f, .195f, .195f) * lane, starMesh, black);
            }
        }

        private static void Fit(Transform parent, string name, Vector3 position, Vector3 scale, UnityEngine.Mesh mesh, Material material)
        {
            var existing = parent.Find(name);
            if (existing == null) { Mesh(parent, name, position, scale, mesh, material); return; }
            existing.localPosition = position; existing.localRotation = Quaternion.identity; existing.localScale = scale;
            existing.GetComponent<MeshFilter>().sharedMesh = mesh;
            existing.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static UnityEngine.Mesh DetailMesh(string name, List<CombineInstance> parts)
        {
            string path = Art + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new UnityEngine.Mesh { name = name }; else mesh.Clear();
            mesh.CombineMeshes(parts.ToArray(), true, true); mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
            return mesh;
        }

        private static Material BrakeLightGlow()
        {
            var material = Resources.Load<Material>("CarMaterials/CarBrakeLights");
            if (material == null) throw new InvalidOperationException("The shared red brake-light material is required for Bicho beacons.");
            return material;
        }

        [MenuItem("Tools/Voxel Racer/Bosses/Update El Bicho Loco Beacons")]
        public static void UpdateBeacons()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Update the boss prefab in Edit Mode.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { ConfigureBeacons(root); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("El Bicho Loco roof beacon and emissive LOCO hopper letters updated; gameplay tuning preserved.");
        }

        // Shared by full builds and the incremental update so authored gameplay/model edits survive.
        public static void ConfigureBeacons(GameObject root)
        {
            var rig = root.GetComponent<VoxelBichoLocoRig>();
            if (rig == null || rig.hoppers.Length != 4) throw new InvalidOperationException("Four-hopper Bicho rig required.");
            var black = Material("Black", new Color(.022f, .025f, .03f), .25f);
            var off = Material("LampOff", new Color(.25f, .006f, .008f), .25f);
            // Reuse the authored build-referenced brake-light emission variant. Clearing
            // AnyEmissive flags lets URP strip the emission keyword on material validation.
            var on = BrakeLightGlow();
            var beacon = root.transform.Find("Boss Roof Beacon");
            float top = float.NegativeInfinity;
            var roof = new Bounds();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponentInParent<VoxelBichoHopper>() != null ||
                    (beacon != null && renderer.transform.IsChildOf(beacon))) continue;
                var matrix = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                var local = renderer.localBounds; var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
                for (int corner = 0; corner < 8; corner++) bounds.Encapsulate(matrix.MultiplyPoint3x4(local.center +
                    Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
                if (bounds.max.y > top + .001f) { top = bounds.max.y; roof = bounds; }
                else if (Mathf.Abs(bounds.max.y - top) <= .001f) roof.Encapsulate(bounds);
            }
            ConfigureHopperLetters(root.transform, rig, top, off, on);
            if (beacon == null)
            {
                beacon = new GameObject("Boss Roof Beacon").transform;
                beacon.SetParent(root.transform, false);
            }
            beacon.localPosition = new Vector3(0, top + .025f, roof.center.z);
            if (beacon.GetComponent<VoxelIndestructiblePart>() == null) beacon.gameObject.AddComponent<VoxelIndestructiblePart>();
            var mount = beacon.Find("Black beacon mount") ?? Block(beacon, "Black beacon mount", Vector3.zero,
                new Vector3(.28f, .05f, .24f), black);
            mount.localPosition = Vector3.zero; mount.localScale = new Vector3(.28f, .05f, .24f);
            var lens = beacon.Find("Red beacon lens") ?? Block(beacon, "Red beacon lens", new Vector3(0, .08f, 0),
                new Vector3(.22f, .11f, .18f), off);
            lens.localPosition = new Vector3(0, .08f, 0); lens.localScale = new Vector3(.22f, .11f, .18f);
            rig.roofLamp = lens.GetComponent<MeshRenderer>(); rig.roofLamp.sharedMaterial = off;
            rig.roofLampOff = off; rig.roofLampOn = on;
        }

        private static void ConfigureHopperLetters(Transform root, VoxelBichoLocoRig rig, float roofTop,
            Material off, Material on)
        {
            var meshes = new Dictionary<char, UnityEngine.Mesh>
            { { 'L', LetterMesh('L') }, { 'O', LetterMesh('O') }, { 'C', LetterMesh('C') } };
            foreach (var hopper in rig.hoppers)
            {
                char letter = "LOCO"[hopper.slot];
                var lamp = hopper.roofLamp.transform;
                var lid = hopper.transform.Find("Black roof");
                float bottom = lid.localPosition.y + lid.localScale.y * .5f;
                float localRoofTop = hopper.transform.InverseTransformPoint(root.TransformPoint(new Vector3(0, roofTop, 0))).y;
                float height = Mathf.Max(.1f, localRoofTop - bottom);
                float width = Mathf.Min(lid.localScale.x * .8f, height * .8f);
                lamp.name = "Hopper Letter " + letter;
                lamp.localPosition = new Vector3(lid.localPosition.x, bottom + height * .5f, lid.localPosition.z);
                lamp.localRotation = Quaternion.identity;
                lamp.localScale = new Vector3(width, height, height * .12f);
                lamp.GetComponent<MeshFilter>().sharedMesh = meshes[letter];
                hopper.lampOff = off; hopper.lampOn = on; hopper.roofLamp.sharedMaterial = off;
            }
        }

        // One closed extruded mesh per letter keeps the existing renderer/light slot.
        // Glyphs read L O C O from the rear chase view; no font atlas or per-pixel objects.
        private static UnityEngine.Mesh LetterMesh(char letter)
        {
            string[] rows = letter switch
            {
                'L' => new[] { "1100000", "1100000", "1100000", "1100000", "1100000", "1100000", "1100000", "1111111", "1111111" },
                'O' => new[] { "0111110", "1100011", "1100011", "1100011", "1100011", "1100011", "1100011", "1100011", "0111110" },
                'C' => new[] { "0111111", "0111111", "1100000", "1100000", "1100000", "1100000", "1100000", "0111111", "0111111" },
                _ => throw new ArgumentOutOfRangeException(nameof(letter))
            };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            var used = new bool[rows.Length, rows[0].Length];
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int first = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            for (int row = 0; row < rows.Length; row++)
            for (int col = 0; col < rows[row].Length; col++)
            {
                if (used[row, col] || rows[row][col] != '1') continue;
                int width = 1;
                while (col + width < rows[row].Length && rows[row][col + width] == '1' && !used[row, col + width]) width++;
                int height = 1;
                while (row + height < rows.Length)
                {
                    bool solid = true;
                    for (int x = col; x < col + width; x++) solid &= rows[row + height][x] == '1' && !used[row + height, x];
                    if (!solid) break;
                    height++;
                }
                for (int y = row; y < row + height; y++) for (int x = col; x < col + width; x++) used[y, x] = true;
                float x0 = col / 7f - .5f, x1 = (col + width) / 7f - .5f;
                float y0 = .5f - (row + height) / 9f, y1 = .5f - row / 9f;
                Face(new(x0,y0,-.5f), new(x0,y1,-.5f), new(x1,y1,-.5f), new(x1,y0,-.5f));
                Face(new(x1,y0,.5f), new(x1,y1,.5f), new(x0,y1,.5f), new(x0,y0,.5f));
                Face(new(x1,y0,-.5f), new(x1,y1,-.5f), new(x1,y1,.5f), new(x1,y0,.5f));
                Face(new(x0,y0,.5f), new(x0,y1,.5f), new(x0,y1,-.5f), new(x0,y0,-.5f));
                Face(new(x0,y1,-.5f), new(x0,y1,.5f), new(x1,y1,.5f), new(x1,y1,-.5f));
                Face(new(x0,y0,.5f), new(x0,y0,-.5f), new(x1,y0,-.5f), new(x1,y0,.5f));
            }
            string path = Art + "/HopperLetter" + letter + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new UnityEngine.Mesh { name = "Hopper Letter " + letter };
            else mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
            return mesh;
        }

        public static VoxelPsychoBugTuning EnsureBodyBehaviour()
        {
            var behaviour = AssetDatabase.LoadAssetAtPath<VoxelPsychoBugTuning>(BodyBehaviourPath);
            if (behaviour != null) return behaviour;
            var source = Resources.Load<VoxelPsychoBugTuning>("EnemyVehicles/PsychoBugBehaviour");
            if (source == null) throw new InvalidOperationException("The existing Psycho Bug behaviour is required.");
            behaviour = Object.Instantiate(source); behaviour.name = "El Bicho Loco Body Behaviour";
            AssetDatabase.CreateAsset(behaviour, BodyBehaviourPath); return behaviour;
        }
        private static Transform Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material; return go.transform;
        }
        private static void Mesh(Transform parent, string name, Vector3 position, Vector3 scale, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
