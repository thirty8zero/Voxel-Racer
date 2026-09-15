using System;
using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Scenery Set", fileName = "ScenerySet")]
    public sealed class VoxelScenerySet : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public GameObject prefab;
            [Min(0)] public float weight = 1;
            public Vector2 scaleRange = new(.8f, 1.2f);
            [Tooltip("Clearance beyond the road edge, in addition to the model's footprint.")]
            [Min(0)] public float roadClearance = 2;
            [Tooltip("Maximum distance beyond the road edge.")]
            [Min(0)] public float maximumRoadDistance = 35;
            [Tooltip("Unscaled horizontal footprint radius; leave room for branches.")]
            [Min(.1f)] public float radius = 1;
            [Min(0)] public float spacing = .5f;
            public bool randomRotation = true;
        }
        [Min(0)] public int minimumPerSegment = 12;
        [Min(0)] public int maximumPerSegment = 20;
        [Header("Distant Scenery Coverage")]
        public bool distantSceneryEnabled = true;
        [Tooltip("Metres around the camera. Zero follows camera far clip / linear fog visibility automatically.")]
        [Min(0)] public float distantSceneryDistance;
        [Tooltip("Average props per 32 x 32 metre tile beyond the roadside band.")]
        [Range(0, 30)] public int distantPropsPerTile = 7;
        public int distantScenerySeed = 7419;
        public Entry[] entries = Array.Empty<Entry>();

        public Entry Choose()
        {
            float total = 0;
            if (entries == null) return null;
            foreach (var entry in entries) if (entry != null && entry.prefab != null) total += Mathf.Max(0, entry.weight);
            if (total <= 0) return null;
            float roll = UnityEngine.Random.value * total;
            Entry last = null;
            foreach (var entry in entries)
            {
                if (entry == null || entry.prefab == null || entry.weight <= 0) continue;
                last = entry;
                roll -= entry.weight;
                if (roll <= 0) return entry;
            }
            return last;
        }
        public static float ChooseScale(Entry entry) => UnityEngine.Random.Range(
            Mathf.Max(.1f, Mathf.Min(entry.scaleRange.x, entry.scaleRange.y)),
            Mathf.Max(.1f, Mathf.Max(entry.scaleRange.x, entry.scaleRange.y)));

        public static GameObject Spawn(Entry entry, Transform parent, Vector3 position, float scale)
        {
            var instance = Instantiate(entry.prefab, parent);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0, entry.randomRotation ? UnityEngine.Random.Range(0, 360f) : 0, 0);
            instance.transform.localScale *= scale;
            var footprint = instance.GetComponent<VoxelSceneryInstance>();
            if (footprint == null) footprint = instance.AddComponent<VoxelSceneryInstance>();
            footprint.radius = entry.radius * scale + entry.spacing * .5f;
            return instance;
        }
    }
}
