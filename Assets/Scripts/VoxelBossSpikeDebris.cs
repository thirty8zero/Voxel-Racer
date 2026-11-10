using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Three collider-free boss death fragments, each carrying its own fire and smoke trail.</summary>
    public sealed class VoxelBossSpikeDebris : MonoBehaviour
    {
        private Vector3 velocity, spin;
        private MeshRenderer[] pieces;
        private float expiresAt;
        private bool settled;

        public void Launch(Vector3 initialVelocity, VoxelBossDefinition settings)
        {
            velocity = initialVelocity;
            spin = Random.onUnitSphere * Mathf.Max(0, settings.spikeSpinSpeed);
            pieces = GetComponentsInChildren<MeshRenderer>();
            expiresAt = Time.time + Mathf.Max(.1f, settings.spikeDebrisLifetime);
            if (!settings.EmitSpikeTrails) return;

            var effects = gameObject.AddComponent<VoxelVehicleDamageEffects>();
            effects.ConfigureExternalHealth(Vector3.zero, 0, 0);
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.startSizeMultiplier *= Mathf.Max(.01f, settings.spikeFireSmokeScale);
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            }
            effects.SetDamageFraction(1f);
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
            if (Time.time >= expiresAt) Destroy(gameObject);
        }
    }
}
