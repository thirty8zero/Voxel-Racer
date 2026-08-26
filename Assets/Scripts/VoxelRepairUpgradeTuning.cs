using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent, live-editable presentation settings for the repair workshop.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Repair Upgrade Tuning", fileName = "RepairUpgradeTuning")]
    public sealed class VoxelRepairUpgradeTuning : ScriptableObject
    {
        [Header("Camera")]
        public Vector3 cameraPosition = new Vector3(5.2f, 2.4f, 3.4f);
        public Vector3 cameraLookAt = new Vector3(0f, 1.05f, 0f);
        [Range(10f, 90f)] public float cameraFieldOfView = 38f;

        public static VoxelRepairUpgradeTuning Load() =>
            Resources.Load<VoxelRepairUpgradeTuning>("RepairUpgradeTuning");

#if UNITY_EDITOR
        private bool refreshQueued;

        private void OnValidate()
        {
            cameraFieldOfView = Mathf.Clamp(cameraFieldOfView, 10f, 90f);
            VoxelAssetSaveQueue.Request(this);
            if (refreshQueued)
                return;

            refreshQueued = true;
            UnityEditor.EditorApplication.delayCall += RefreshWorkshopWhenEditorIsIdle;
        }

        private void RefreshWorkshopWhenEditorIsIdle()
        {
            refreshQueued = false;
            foreach (VoxelRepairUpgradeSceneController controller in
                FindObjectsByType<VoxelRepairUpgradeSceneController>(FindObjectsSortMode.None))
                if (controller.cameraTuning == this)
                    controller.ApplyCameraTuning();
        }
#endif
    }
}
