using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Cheap short-lived replacement for physics rigidbodies on damage particles.</summary>
    public sealed class VoxelDebris : MonoBehaviour
    {
        private Vector3 velocity;
        private float expireTime;

        public void Launch(Vector3 initialVelocity)
        {
            velocity = initialVelocity;
            expireTime = Time.time + 1.5f;
        }

        private void Update()
        {
            velocity += Physics.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(velocity * 120f * Time.deltaTime, Space.World);
            if (Time.time >= expireTime)
                Destroy(gameObject);
        }
    }
}
