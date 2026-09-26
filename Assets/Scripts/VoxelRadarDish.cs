using UnityEngine;

namespace VoxelRacer
{
    public sealed class VoxelRadarDish : MonoBehaviour
    {
        public float degreesPerSecond = 120f;
        private void Update() => transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
