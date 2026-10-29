using UnityEngine;

namespace VoxelRacer
{
    /// <summary>One separately targetable, solid hopper; its geometry disappears only at zero health.</summary>
    public sealed class VoxelBichoHopper : MonoBehaviour
    {
        [Range(0, 3)] public int slot;
        public MeshRenderer roofLamp;
        public Material lampOff, lampOn;
        public Transform wheel;
        public Transform mineOutlet;
        public float CurrentHealth { get; private set; }
        public float MaximumHealth { get; private set; }
        public bool IsAlive => configured && CurrentHealth > 0 && gameObject.activeInHierarchy;
        public bool IsLit { get; private set; }
        public VoxelBichoLocoRig Rig { get; private set; }
        public Bounds LocalBounds { get; private set; }
        public VoxelEnemyHealthBar HealthBar { get; private set; }
        private MeshRenderer[] pieces;
        private bool configured;

        internal void Configure(VoxelBichoLocoRig rig, float health)
        {
            Rig = rig; MaximumHealth = CurrentHealth = Mathf.Max(.001f, health);
            pieces = GetComponentsInChildren<MeshRenderer>(true);
            bool first = true; var bounds = new Bounds();
            foreach (var r in pieces)
            {
                var matrix = transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                var b = r.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            LocalBounds = bounds;
            // Keep health-bar geometry and dimensions independent of boss model scaling.
            HealthBar = VoxelEnemyHealthBar.Create(rig.Owner.transform, rig.Settings.healthBarWidth,
                rig.Settings.healthBarHeight, 0, new Color(.95f, .12f, .08f), new Color(.3f, .02f, .02f), outlined: true);
            configured = true;
            VoxelMissileTarget.Register(gameObject);
            SetLamp(false);
        }

        internal void UpdatePresentation(float distance, float wheelDegreesPerUnit)
        {
            if (!IsAlive) return;
            if (wheel != null) wheel.Rotate(Vector3.right, distance * wheelDegreesPerUnit, Space.Self);
            if (HealthBar != null)
            {
                HealthBar.gameObject.SetActive(!Rig.Owner.IsBossEntering && transform.lossyScale.y > .1f);
                HealthBar.transform.position = transform.TransformPoint(new Vector3(LocalBounds.center.x, LocalBounds.max.y, LocalBounds.center.z)) +
                    Vector3.up * Rig.Settings.healthBarClearance;
            }
        }

        private void LateUpdate() { if (configured) UpdatePresentation(0, 0); }

        internal void SetLamp(bool lit)
        {
            IsLit = lit && CurrentHealth > 0;
            if (roofLamp != null) roofLamp.sharedMaterial = IsLit ? lampOn : lampOff;
        }

        internal bool Trace(Vector3 start, Vector3 direction, float length, out float distance)
        {
            distance = 0;
            if (!IsAlive || transform.lossyScale.sqrMagnitude < .00001f) return false;
            var origin = transform.InverseTransformPoint(start);
            var localDirection = transform.InverseTransformVector(direction).normalized;
            float localDistance = 0;
            if (!LocalBounds.Contains(origin) && !LocalBounds.IntersectRay(new Ray(origin, localDirection), out localDistance)) return false;
            distance = Vector3.Dot(transform.TransformPoint(origin + localDirection * localDistance) - start, direction);
            return distance >= 0 && distance <= length;
        }

        internal Transform HitPart(Vector3 point)
        {
            Transform result = transform; float best = float.PositiveInfinity;
            foreach (var piece in pieces)
            {
                if (piece == null || !piece.enabled || !piece.gameObject.activeInHierarchy) continue;
                float distance = (piece.bounds.ClosestPoint(point) - point).sqrMagnitude;
                if (distance < best) { best = distance; result = piece.transform; }
            }
            return result;
        }

        public float TakeDamage(float damage, Vector3 point, Vector3 direction, bool awardPoints = true)
        {
            if (!IsAlive || damage <= 0 || Rig.Owner == null || !Rig.Owner.IsDrivingEnemy) return 0;
            float applied = Mathf.Min(CurrentHealth, damage);
            CurrentHealth = Mathf.Max(0, CurrentHealth - applied);
            HealthBar?.SetHealth(CurrentHealth / MaximumHealth);
            Rig.Owner.ApplyHopperHealthDamage(applied);
            if (awardPoints)
            {
                VoxelMissionProgress.ReportEnemyVoxelDamage();
                VoxelScorePopup.Show(HealthBar.transform.position, VoxelMissionProgress.GetEnemyVoxelDamagePoints(),
                    VoxelScorePopup.Style.WeaponDamage, healthBar: HealthBar);
            }
            if (CurrentHealth <= 0) DestroyHopper(direction, awardPoints);
            return applied;
        }

        internal void TakeMissileBlast(Vector3 point, float radius, float damage, Vector3 direction, bool direct)
        {
            if (!IsAlive || damage <= 0) return;
            float scaledRadius = radius * Mathf.Max(0, Rig.Owner.BossDefinition.missileBlastRadiusMultiplier);
            var nearest = transform.TransformPoint(LocalBounds.ClosestPoint(transform.InverseTransformPoint(point)));
            if (!direct && (nearest - point).sqrMagnitude > scaledRadius * scaledRadius) return;
            TakeDamage(damage * Mathf.Max(0, Rig.Owner.BossDefinition.missileHealthDamageMultiplier), point, direction);
        }

        private void DestroyHopper(Vector3 direction, bool awardPoints)
        {
            SetLamp(false);
            if (Application.isPlaying)
            {
                VoxelDestructionExplosion.PlayAtWorldScale(transform.TransformPoint(LocalBounds.center), Rig.Settings.explosionScale, true);
                int count = Mathf.Min(pieces.Length, Mathf.Max(0, Rig.Settings.maximumDebris));
                for (int i = 0; i < count; i++)
                {
                    var source = pieces[(int)((long)i * pieces.Length / count)];
                    if (source == null) continue;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "El Bicho Loco Hopper Debris";
                    go.transform.SetPositionAndRotation(source.bounds.center, source.transform.rotation);
                    go.transform.localScale = VoxelDebris.GetSourceSize(source) * .8f;
                    go.GetComponent<MeshRenderer>().sharedMaterial = source.sharedMaterial;
                    var collider = go.GetComponent<BoxCollider>(); collider.enabled = false; Destroy(collider);
                    go.AddComponent<VoxelDebris>().Launch(direction.normalized * Rig.Settings.debrisOutwardSpeed +
                        Random.insideUnitSphere * Rig.Settings.debrisOutwardSpeed + Vector3.up * Rig.Settings.debrisUpwardSpeed,
                        Rig.Settings.debrisLifetime);
                }
            }
            if (awardPoints) VoxelMissionProgress.ReportEnemyVoxelDestroyed(pieces.Length, transform.position);
            if (HealthBar != null) HealthBar.gameObject.SetActive(false);
            gameObject.SetActive(false);
            Rig.HopperDestroyed(this);
        }

        private void OnDestroy()
        {
            if (HealthBar != null)
            {
                if (Application.isPlaying) Destroy(HealthBar.gameObject); else DestroyImmediate(HealthBar.gameObject);
            }
        }
    }
}
