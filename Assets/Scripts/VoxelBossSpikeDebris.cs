using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Three collider-free boss death fragments, each carrying its own fire and smoke trail.</summary>
    public sealed class VoxelBossSpikeDebris : MonoBehaviour
    {
        private Vector3 velocity, spin;
        private MeshRenderer[] pieces;
        private VoxelVehicleDamageEffects effects;
        private float emissionEndsAt, expiresAt;
        private bool emitting, settled;

        public void Launch(Vector3 initialVelocity, VoxelBossDefinition settings)
        {
            velocity = initialVelocity;
            spin = Random.onUnitSphere * Mathf.Max(0, settings.spikeSpinSpeed);
            pieces = GetComponentsInChildren<MeshRenderer>();
            emissionEndsAt = Time.time + Mathf.Max(0, settings.spikeFireSmokeDuration);
            // Allow the existing smoke's longest particles to finish after emission ends.
            expiresAt = Time.time + Mathf.Max(.1f, Mathf.Max(settings.spikeDebrisLifetime, settings.spikeFireSmokeDuration + 2.2f));
            effects = gameObject.AddComponent<VoxelVehicleDamageEffects>();
            effects.ConfigureExternalHealth(Vector3.zero, 0, 0);
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.startSizeMultiplier *= Mathf.Max(.01f, settings.spikeFireSmokeScale);
            }
            emitting = settings.spikeFireSmokeDuration > 0;
            effects.SetDamageFraction(emitting ? 1 : 0);
        }

        private void Update()
        {
            if (VoxelPauseMenu.IsPaused) return;
            Advance(Time.deltaTime);
        }

        private void Advance(float seconds)
        {
            if (!settled)
            {
                velocity += Physics.gravity * seconds;
                transform.position += velocity * seconds;
                transform.Rotate(spin * seconds, Space.World);
                float bottom = float.PositiveInfinity;
                foreach (var piece in pieces) if (piece != null) bottom = Mathf.Min(bottom, piece.bounds.min.y);
                if (bottom < .02f)
                {
                    transform.position += Vector3.up * (.02f - bottom);
                    velocity.y = Mathf.Abs(velocity.y) > 1.2f ? Mathf.Abs(velocity.y) * .2f : 0;
                    velocity.x *= .55f; velocity.z *= .55f; spin *= .5f;
                    if (velocity.sqrMagnitude < .2f) { velocity = spin = Vector3.zero; settled = true; }
                }
            }
            if (emitting && Time.time >= emissionEndsAt)
            {
                emitting = false;
                effects.SetDamageFraction(0);
            }
            if (Time.time >= expiresAt) Destroy(gameObject);
        }
    }
}
