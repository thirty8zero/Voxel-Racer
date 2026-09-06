using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Cheap short-lived replacement for physics rigidbodies on damage particles.</summary>
    public sealed class VoxelDebris : MonoBehaviour
    {
        private const float GroundHeight = 0f;
        private const float GroundBounceDamping = 0.24f;
        private const float GroundSlideDamping = 0.58f;

        private Vector3 velocity;
        private float expireTime;
        private float groundRestHeight;
        private bool hasBounced;
        private bool hasSettled;

        private void Awake()
        {
            ApplyDamageSoot();
        }

        /// <summary>
        /// Gives every detached damage voxel a cheap, per-renderer soot variation.
        /// A MaterialPropertyBlock avoids duplicating materials for the many short-lived
        /// debris pieces that can be active on mobile.
        /// </summary>
        private void ApplyDamageSoot()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer.sharedMaterial == null)
                    continue;

                // Mostly near-black, with occasional charcoal-grey fragments so an
                // impact reads as scorched material rather than a flat black cloud.
                float brightness = Random.value < 0.68f
                    ? Random.Range(0.025f, 0.13f)
                    : Random.Range(0.16f, 0.38f);
                Color soot = new Color(brightness, brightness, brightness * Random.Range(0.9f, 1f), 1f);
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                if (renderer.sharedMaterial.HasProperty("_BaseColor"))
                    properties.SetColor("_BaseColor", soot);
                if (renderer.sharedMaterial.HasProperty("_Color"))
                    properties.SetColor("_Color", soot);
                renderer.SetPropertyBlock(properties);
            }
        }

        public void Launch(Vector3 initialVelocity)
        {
            Launch(initialVelocity, 1.5f);
        }

        public void Launch(Vector3 initialVelocity, float lifetime)
        {
            velocity = initialVelocity;
            expireTime = Time.time + Mathf.Max(0.1f, lifetime);
            groundRestHeight = GroundHeight + Mathf.Max(0.02f, transform.lossyScale.y * 0.5f);
            hasBounced = false;
            hasSettled = false;
        }

        private void Update()
        {
            if (!hasSettled)
            {
                velocity += Physics.gravity * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                transform.Rotate(velocity * 120f * Time.deltaTime, Space.World);

                if (transform.position.y <= groundRestHeight && velocity.y <= 0f)
                    ResolveGroundContact();
            }

            if (Time.time >= expireTime)
                Destroy(gameObject);
        }

        private void ResolveGroundContact()
        {
            Vector3 position = transform.position;
            position.y = groundRestHeight;
            transform.position = position;

            // One small bounce reads as a physical impact. Settling immediately
            // afterwards avoids rigidbodies, colliders, and ongoing simulation cost.
            if (!hasBounced && velocity.y < -1.2f)
            {
                hasBounced = true;
                velocity.y = -velocity.y * GroundBounceDamping;
                velocity.x *= GroundSlideDamping;
                velocity.z *= GroundSlideDamping;
                return;
            }

            velocity = Vector3.zero;
            hasSettled = true;
        }
    }
}
