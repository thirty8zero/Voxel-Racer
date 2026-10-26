using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Reusable civilian model selection and paint palette, shared by track traffic tunings.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Civilian Traffic Set", fileName = "CivilianTrafficSet")]
    public sealed class VoxelCivilianTrafficSet : ScriptableObject
    {
        [Header("Civilian Vehicles")]
        [Tooltip("Each valid entry has an equal chance. Null entries and missing models are ignored. An empty pool disables civilians; radar objectives use their own override.")]
        public VoxelEnemyVehicleTuning[] civilianVehiclePool;

        [Header("Paint")]
        [Tooltip("Random body paint choices for these civilians. An empty palette retains each model's authored paint. Trim, glass and lights are not recoloured.")]
        public Color[] paintColours =
        {
            new Color(.80f, .07f, .10f), new Color(.98f, .55f, .06f),
            new Color(.12f, .70f, .32f), new Color(.55f, .18f, .82f)
        };

        public bool HasCivilianVehicles
        {
            get
            {
                if (civilianVehiclePool != null)
                    foreach (var entry in civilianVehiclePool)
                        if (entry != null && entry.modelPrefab != null) return true;
                return false;
            }
        }

        public VoxelEnemyVehicleTuning ChooseCivilianVehicle()
        {
            int count = 0;
            if (civilianVehiclePool != null)
                foreach (var entry in civilianVehiclePool)
                    if (entry != null && entry.modelPrefab != null) count++;
            if (count == 0) return null;
            int pick = Random.Range(0, count);
            foreach (var entry in civilianVehiclePool)
                if (entry != null && entry.modelPrefab != null && pick-- == 0) return entry;
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate() => VoxelAssetSaveQueue.Request(this);
#endif
    }
}
