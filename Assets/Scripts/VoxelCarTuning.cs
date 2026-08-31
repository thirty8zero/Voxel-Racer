using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persistent, shared tuning for the generated player car.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Car Tuning", fileName = "VoxelCarTuning")]
    public sealed class VoxelCarTuning : ScriptableObject
    {
        [Header("Speed")]
        public float acceleration = 12f;
        public float topSpeed = 32f;
        public float brakingForce = 42f;

        [Header("Damage")]
        public int damageVoxelsPerHit = 8;
        public int debrisVoxelsPerDamagedVoxel = 2;
        public float explosionSpawnOffset = 0.45f;
        public float explosionUpwardBias = 0.75f;
        public float explosionForwardForceMin = 7f;
        public float explosionForwardForceMax = 10f;
        public float explosionUpwardForce = 2.5f;
        public float explosionSpreadForce = 1.5f;
        [Tooltip("Size multiplier applied to the shared VoxelDestructionExplosion effect.")]
        [Min(0.1f)] public float explosionEffectScale = 1.15f;

        [Header("Lanes")]
        public float laneChangeSpeed = 14f;

        [Header("Wheel Steering")]
        [Min(0f)] public float frontWheelTurnDegrees = 24f;
        public float wheelSpinDegreesPerUnit = 130f;

        [Header("Lane Change Visuals")]
        [Min(0f)] public float laneChangeBodyRollDegrees = 4f;
        [Min(0f)] public float laneChangeYawDegrees = 6f;
        [Min(0f)] public float laneChangeVisualRotationSpeed = 90f;

        public static VoxelCarTuning Load() => Resources.Load<VoxelCarTuning>("VoxelCarTuning");

        public void ApplyTo(VoxelCarController controller)
        {
            controller.acceleration = acceleration;
            controller.topSpeed = topSpeed;
            controller.brakingForce = brakingForce;
            controller.damageVoxelsPerHit = damageVoxelsPerHit;
            controller.debrisVoxelsPerDamagedVoxel = debrisVoxelsPerDamagedVoxel;
            controller.explosionSpawnOffset = explosionSpawnOffset;
            controller.explosionUpwardBias = explosionUpwardBias;
            controller.explosionForwardForceMin = explosionForwardForceMin;
            controller.explosionForwardForceMax = explosionForwardForceMax;
            controller.explosionUpwardForce = explosionUpwardForce;
            controller.explosionSpreadForce = explosionSpreadForce;
            controller.explosionEffectScale = explosionEffectScale;
            controller.laneChangeSpeed = laneChangeSpeed;
            controller.frontWheelTurnDegrees = frontWheelTurnDegrees;
            controller.wheelSpinDegreesPerUnit = wheelSpinDegreesPerUnit;
            controller.laneChangeBodyRollDegrees = laneChangeBodyRollDegrees;
            controller.laneChangeYawDegrees = laneChangeYawDegrees;
            controller.laneChangeVisualRotationSpeed = laneChangeVisualRotationSpeed;
        }

        public void CopyFrom(VoxelCarController controller)
        {
            acceleration = controller.acceleration;
            topSpeed = controller.topSpeed;
            brakingForce = controller.brakingForce;
            damageVoxelsPerHit = controller.damageVoxelsPerHit;
            debrisVoxelsPerDamagedVoxel = controller.debrisVoxelsPerDamagedVoxel;
            explosionSpawnOffset = controller.explosionSpawnOffset;
            explosionUpwardBias = controller.explosionUpwardBias;
            explosionForwardForceMin = controller.explosionForwardForceMin;
            explosionForwardForceMax = controller.explosionForwardForceMax;
            explosionUpwardForce = controller.explosionUpwardForce;
            explosionSpreadForce = controller.explosionSpreadForce;
            explosionEffectScale = controller.explosionEffectScale;
            laneChangeSpeed = controller.laneChangeSpeed;
            frontWheelTurnDegrees = controller.frontWheelTurnDegrees;
            wheelSpinDegreesPerUnit = controller.wheelSpinDegreesPerUnit;
            laneChangeBodyRollDegrees = controller.laneChangeBodyRollDegrees;
            laneChangeYawDegrees = controller.laneChangeYawDegrees;
            laneChangeVisualRotationSpeed = controller.laneChangeVisualRotationSpeed;
        }

#if UNITY_EDITOR
        private bool controllerRefreshQueued;

        private void OnValidate()
        {
            VoxelAssetSaveQueue.Request(this);
            if (controllerRefreshQueued)
                return;

            controllerRefreshQueued = true;
            UnityEditor.EditorApplication.delayCall += RefreshControllersWhenEditorIsIdle;
        }

        private void RefreshControllersWhenEditorIsIdle()
        {
            controllerRefreshQueued = false;
            foreach (var controller in FindObjectsByType<VoxelCarController>(FindObjectsSortMode.None))
                if (controller.tuning == this)
                    controller.SetTuning(this);
        }
#endif
    }
}
