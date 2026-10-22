using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace VoxelRacer
{
    /// <summary>Two camera-facing beam cards, batched across traffic. No lights, colliders or damage meshes.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class VoxelNightHeadlights : MonoBehaviour
    {
        private sealed class Lens
        {
            public MeshRenderer renderer;
            public Material original;
            public Vector3 face;
            public int side;
        }
        private readonly List<Lens> lenses = new();
        private static readonly List<VoxelNightHeadlights> active = new();
        private static readonly Matrix4x4[] matrices = new Matrix4x4[1023];
        private static readonly Vector4[] colours = new Vector4[1023];
        private static readonly MaterialPropertyBlock properties = new();
        private static Mesh card;
        private static readonly int BeamColour = Shader.PropertyToID("_BeamColour");
        private static readonly ProfilerMarker submitMarker = new("VoxelNightHeadlights.SubmitDraws");
        private VoxelNightHeadlightSettings settings;
        private Material lampMaterial;
        private VoxelEnemyCar enemy;
        private VoxelObstacleCar traffic;
        private VoxelFadeIn fade;
        private VoxelSpawnScaleIn growth;
        private VoxelBossEntrance entrance;
        private bool powered;
        public int BoundLensCount => lenses.Count;
        public bool IsEnemyVehicle { get; private set; }
        public static int LastBeamCount { get; private set; }
        public static int LastBatchCount { get; private set; }

        public static VoxelNightHeadlights Attach(GameObject vehicle, VoxelTrackDefinition track)
        {
            if (track == null || !track.nightTime || vehicle.GetComponent<VoxelCarController>() != null) return null;
            var tuning = VoxelNightHeadlightSettings.Load();
            if (tuning == null || !tuning.headlightsEnabled || tuning.lampMaterial == null || tuning.beamMaterial == null) return null;
            var effect = vehicle.GetComponent<VoxelNightHeadlights>();
            if (effect == null) effect = vehicle.AddComponent<VoxelNightHeadlights>();
            effect.Bind(tuning);
            return effect;
        }

        private void Bind(VoxelNightHeadlightSettings tuning)
        {
            RestoreLenses(); lenses.Clear(); settings = tuning;
            enemy = GetComponent<VoxelEnemyCar>(); traffic = GetComponent<VoxelObstacleCar>();
            IsEnemyVehicle = enemy != null || traffic != null && traffic.IsEnemyTraffic;
            lampMaterial = IsEnemyVehicle && settings.enemyLampMaterial != null ? settings.enemyLampMaterial : settings.lampMaterial;
            entrance = GetComponentInChildren<VoxelBossEntrance>(true);
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                string name = renderer.name.ToLowerInvariant();
                bool namedLens = name.Contains("headlamp") || name.Contains("headlight") || name == "fog lamp";
                if (name.Contains("cover") || name.Contains("seam") || name.Contains("surround") ||
                    name.Contains("chrome") || name.Contains("housing") || name.Contains("recess")) continue;
                bool fallbackLens = renderer.sharedMaterial != null && renderer.sharedMaterial.name == "CarHeadlights";
                if (!namedLens && !fallbackLens) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds;
                Vector3 face = bounds.center + Vector3.forward * bounds.extents.z;
                // Boss entrance starts its model at zero scale. Read its authored pose for binding.
                Vector3 local = face;
                for (var part = renderer.transform; part != null && part != transform; part = part.parent)
                    local = Matrix4x4.TRS(part.localPosition, part.localRotation,
                        entrance != null && part == entrance.transform ? entrance.FullScale : part.localScale).MultiplyPoint3x4(local);
                if (local.z <= 0f || Mathf.Abs(local.x) < .1f) continue;
                lenses.Add(new Lens { renderer = renderer, original = renderer.sharedMaterial,
                    face = face, side = local.x < 0f ? 0 : 1 });
                renderer.sharedMaterial = lampMaterial;
            }
            powered = true;
            if (isActiveAndEnabled && lenses.Count > 0 && !active.Contains(this)) active.Add(this);
        }

        private void OnEnable()
        {
            if (settings != null && lenses.Count > 0 && !active.Contains(this)) active.Add(this);
        }
        private void OnDisable() => active.Remove(this);
        private void OnDestroy()
        {
            active.Remove(this); RestoreLenses();
            if (active.Count == 0) ReleaseMesh();
        }
        private void RestoreLenses()
        {
            foreach (var lens in lenses)
                if (lens.renderer != null && lens.renderer.sharedMaterial == lampMaterial)
                    lens.renderer.sharedMaterial = lens.original;
        }
        private bool HasPower => settings != null && settings.headlightsEnabled &&
            (enemy != null ? enemy.IsDrivingTraffic && enemy.CurrentHealth > 0f :
                traffic != null && traffic.IsDrivingTraffic && traffic.CurrentHealth > 0f);

        private void LateUpdate()
        {
            bool hasPower = HasPower;
            if (powered != hasPower)
            {
                if (hasPower)
                {
                    foreach (var lens in lenses)
                        if (lens.renderer != null && lens.renderer.transform.IsChildOf(transform) &&
                            lens.renderer.sharedMaterial == lens.original)
                            lens.renderer.sharedMaterial = lampMaterial;
                }
                else
                {
                    // Restore the fade's cache first so it cannot relight a wreck later.
                    fade ??= GetComponent<VoxelFadeIn>();
                    if (fade != null) fade.enabled = false;
                    RestoreLenses();
                }
                powered = hasPower;
            }
            if (active.Count > 0 && active[0] == this) SubmitDraws(Camera.main);
        }

        public static void SubmitDraws(Camera camera, bool submit = true)
        {
            using var profile = submitMarker.Auto();
            LastBeamCount = LastBatchCount = 0;
            if (camera == null) return;
            Material material = null;
            int layer = 0;
            int count = 0;
            foreach (var effect in active)
            {
                if (effect == null || !effect.isActiveAndEnabled || !effect.HasPower ||
                    (camera.cullingMask & (1 << effect.gameObject.layer)) == 0) continue;
                var tuning = effect.settings;
                float distance = Vector3.Distance(camera.transform.position, effect.transform.position);
                if (distance >= tuning.maximumDrawDistance + tuning.beamLength) continue;
                effect.fade ??= effect.GetComponent<VoxelFadeIn>();
                effect.growth ??= effect.GetComponent<VoxelSpawnScaleIn>();
                float grow = effect.growth != null ? effect.growth.Growth : 1f;
                if (effect.entrance != null && effect.entrance.enabled)
                    grow *= effect.entrance.transform.localScale.x / Mathf.Max(.001f, effect.entrance.FullScale.x);
                float opacity = tuning.beamOpacity * (effect.fade != null ? effect.fade.Opacity : 1f) * grow;
                opacity *= 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(tuning.maximumDrawDistance * .8f, tuning.maximumDrawDistance, distance));
                if (opacity <= .001f || grow <= .001f) continue;
                for (int side = 0; side < 2; side++)
                {
                    Vector3 origin = Vector3.zero;
                    int intact = 0;
                    foreach (var lens in effect.lenses)
                    {
                        var renderer = lens.renderer;
                        if (lens.side != side || renderer == null || !renderer.enabled ||
                            !renderer.gameObject.activeInHierarchy || !renderer.transform.IsChildOf(effect.transform)) continue;
                        origin += renderer.transform.TransformPoint(lens.face); intact++;
                    }
                    if (intact == 0) continue;
                    origin /= intact;
                    var up = effect.transform.up;
                    origin += effect.transform.forward * .03f;
                    var matrix = BuildBeamMatrix(origin, effect.transform.forward, up, effect.transform.position,
                        camera.transform.position, tuning.beamLength * grow, tuning.beamEndWidth * .5f * grow);
                    if (count > 0 && (material != tuning.beamMaterial || layer != effect.gameObject.layer))
                        Flush(camera, material, layer, ref count, submit);
                    material = tuning.beamMaterial;
                    layer = effect.gameObject.layer;
                    matrices[count] = matrix;
                    Color colour = effect.IsEnemyVehicle ? tuning.enemyBeamColour : tuning.beamColour;
                    colour.a = opacity;
                    colours[count++] = colour;
                    LastBeamCount++;
                    if (count == matrices.Length) Flush(camera, material, layer, ref count, submit);
                }
            }
            if (count > 0) Flush(camera, material, layer, ref count, submit);
        }

        private static Matrix4x4 BuildBeamMatrix(Vector3 origin, Vector3 forward, Vector3 up,
            Vector3 roadPosition, Vector3 cameraPosition, float length, float halfWidth)
        {
            const float roadClearance = .1f;
            float height = Mathf.Max(0f, Vector3.Dot(origin - roadPosition, up) - roadClearance);
            Vector3 axis = forward * length - up * height;
            Vector3 right = Vector3.Cross(axis.normalized, cameraPosition - (origin + axis * .5f)).normalized;
            if (right.sqrMagnitude < .01f) right = Vector3.Cross(up, forward).normalized;
            Vector3 width = right * halfWidth;
            // Rolling the card towards an oblique camera lowers one outer edge.
            // Lift only the far end to clear that edge; retain the lamp anchor,
            // horizontal beam reach and camera-facing width without another card.
            float lowestEnd = Vector3.Dot(origin + axis - roadPosition, up) - Mathf.Abs(Vector3.Dot(width, up));
            axis += up * Mathf.Max(0f, roadClearance - lowestEnd);
            var matrix = Matrix4x4.identity;
            matrix.SetColumn(0, width);
            matrix.SetColumn(1, Vector3.Cross(axis.normalized, right).normalized);
            matrix.SetColumn(2, axis);
            matrix.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
            return matrix;
        }

        private static void Flush(Camera camera, Material material, int layer, ref int count, bool submit)
        {
            if (!submit)
            {
                LastBatchCount += SystemInfo.supportsInstancing && material.enableInstancing ? 1 : count;
                count = 0; return;
            }
            if (card == null)
            {
                card = new Mesh { name = "Shared Night Headlight Card", hideFlags = HideFlags.DontSave };
                card.vertices = new[] { new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) };
                card.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                card.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                card.RecalculateBounds();
            }
            properties.SetVectorArray(BeamColour, colours);
            if (SystemInfo.supportsInstancing && material.enableInstancing)
            {
                Graphics.DrawMeshInstanced(card, 0, material, matrices, count, properties,
                    ShadowCastingMode.Off, false, layer, camera, LightProbeUsage.Off);
                LastBatchCount++;
            }
            else
                for (int i = 0; i < count; i++)
                {
                    properties.SetColor(BeamColour, colours[i]);
                    Graphics.DrawMesh(card, matrices[i], material, layer, camera, 0, properties,
                        ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
                    LastBatchCount++;
                }
            count = 0;
        }

        private static void ReleaseMesh()
        {
            if (card == null) return;
            if (Application.isPlaying) Destroy(card); else DestroyImmediate(card);
            card = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { active.Clear(); ReleaseMesh(); LastBeamCount = LastBatchCount = 0; }
    }
}
