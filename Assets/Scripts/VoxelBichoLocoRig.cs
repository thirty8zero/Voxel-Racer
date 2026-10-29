using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Four linked hoppers, their lamp/drop clock and the boss's constrained approach/retreat cycle.</summary>
    public sealed class VoxelBichoLocoRig : MonoBehaviour
    {
        public VoxelBichoHopper[] hoppers = new VoxelBichoHopper[4];
        public MeshRenderer roofLamp;
        public Material roofLampOff, roofLampOn;
        public bool RoofLightIsLit { get; private set; }
        public VoxelEnemyCar Owner { get; private set; }
        public VoxelBichoLocoTuning Settings { get; private set; }
        public bool HasLiveHoppers { get { foreach (var hopper in hoppers) if (hopper != null && hopper.IsAlive) return true; return false; } }
        public int ActiveLightSlot { get; private set; }
        public int MinesDropped { get; private set; }
        public int LastDropSlot { get; private set; } = -1;
        public bool IsRetreating { get; private set; }
        private float phaseTime, dropClock, retreatClock, retreatDeadline;
        private VoxelCarController player;
        private EndlessVoxelRoad road;

        internal void Configure(VoxelEnemyCar owner, VoxelBichoLocoTuning settings, VoxelCarController target, EndlessVoxelRoad path)
        {
            Owner = owner; Settings = settings; player = target; road = path;
            phaseTime = dropClock = retreatClock = 0; ActiveLightSlot = 0;
            float health = owner.BossDefinition.health * Mathf.Clamp(settings.combinedHopperHealthPercent, 1, 95) * .01f / 4;
            foreach (var hopper in hoppers) if (hopper != null) hopper.Configure(this, health);
            ScheduleRetreat(); UpdateLights();
        }

        internal float DesiredSpeed(float gap, float speed, float seconds)
        {
            retreatClock += Mathf.Max(0, seconds);
            if (!IsRetreating && retreatClock >= retreatDeadline) IsRetreating = true;
            float retreatGap = Mathf.Clamp(Settings.retreatDistance, 1, Owner.BossDefinition.FailureDistance - 10);
            if (IsRetreating && gap >= retreatGap)
            { IsRetreating = false; retreatClock = 0; ScheduleRetreat(); }
            float minimum = Mathf.Max(Settings.minimumApproachDistance, Owner.Tuning.collisionHalfLength + 8);
            float playerSpeed = Mathf.Max(0, player.CurrentSpeed);
            float floor = player.EffectiveTopSpeed * Mathf.Clamp(Owner.BossDefinition.minimumCruiseSpeedFraction, .1f, 1);
            if (IsRetreating)
            {
                // Brake before the desired gap so acceleration cannot carry the boss through its own escape limit.
                float advantage = Mathf.Min(Settings.retreatSpeedAdvantage,
                    Mathf.Sqrt(2 * Mathf.Max(.1f, Owner.BossDefinition.braking) * Mathf.Max(0, retreatGap - gap)));
                return Mathf.Max(floor, playerSpeed + advantage);
            }
            return Mathf.Max(floor, playerSpeed + Mathf.Clamp((minimum - gap) * .5f, -Settings.approachClosingSpeed, Settings.retreatSpeedAdvantage));
        }

        private void ScheduleRetreat()
        {
            float min = Mathf.Max(.1f, Settings.minimumRetreatInterval);
            retreatDeadline = Random.Range(min, Mathf.Max(min, Settings.maximumRetreatInterval));
        }

        internal void ResumeApproachRetreat()
        { IsRetreating = false; retreatClock = 0; ScheduleRetreat(); }

        internal void Advance(float seconds, float speed, float wheelDegreesPerUnit)
        {
            if (seconds <= 0 || player == null || player.IsDestroyed || VoxelPauseMenu.IsPaused ||
                VoxelMissionProgress.Active?.IsComplete == true || VoxelMissionProgress.Active?.IsFailed == true) return;
            foreach (var hopper in hoppers) if (hopper != null) hopper.UpdatePresentation(speed * seconds, wheelDegreesPerUnit);
            int count = 0; foreach (var hopper in hoppers) if (hopper != null && hopper.IsAlive) count++;
            if (count == 0) return;
            float step = Mathf.Max(.05f, Settings.lightStepDuration);
            phaseTime += seconds;
            int steps = Mathf.FloorToInt(phaseTime / step);
            phaseTime %= step;
            for (int i = 0; i < steps % count; i++) ActiveLightSlot = NextAliveSlot(ActiveLightSlot);
            if (!hoppers[ActiveLightSlot].IsAlive) ActiveLightSlot = NextAliveSlot(ActiveLightSlot);
            UpdateLights();
            dropClock += seconds;
            if (dropClock < Mathf.Max(.1f, Settings.mineDropInterval) || !hoppers[ActiveLightSlot].IsLit) return;
            var tuning = Settings.mineTuning;
            if (road == null || tuning == null || tuning.minePrefab == null) return;
            var hopperToDrop = hoppers[ActiveLightSlot];
            var local = Owner.transform.InverseTransformPoint(hopperToDrop.mineOutlet.position);
            var mine = new GameObject("El Bicho Loco Road Mine").AddComponent<VoxelRoadMine>();
            mine.transform.SetParent(Owner.transform.parent, false);
            mine.Configure(player, road, tuning, Owner.TrackDistance + local.z - Settings.mineRearClearance, Owner.LaneOffset + local.x);
            dropClock = 0; MinesDropped++; LastDropSlot = ActiveLightSlot;
        }

        private void UpdateLights()
        {
            bool on = phaseTime < Mathf.Max(.05f, Settings.lightStepDuration) * Mathf.Clamp(Settings.lightOnFraction, .05f, 1);
            bool anyLit = false;
            foreach (var hopper in hoppers)
                if (hopper != null)
                {
                    hopper.SetLamp(on && hopper.slot == ActiveLightSlot && hopper.IsAlive);
                    anyLit |= hopper.IsLit;
                }
            SetRoofLamp(anyLit);
        }

        private void SetRoofLamp(bool lit)
        {
            RoofLightIsLit = lit && roofLamp != null;
            if (roofLamp == null) return;
            var material = RoofLightIsLit ? roofLampOn : roofLampOff;
            if (roofLamp.sharedMaterial != material) roofLamp.sharedMaterial = material;
        }

        private void OnEnable() { if (Settings != null) UpdateLights(); }
        private void OnDisable()
        {
            SetRoofLamp(false);
            foreach (var hopper in hoppers) if (hopper != null) hopper.SetLamp(false);
        }

        private int NextAliveSlot(int previous)
        {
            for (int i = 1; i <= 4; i++) { int slot = (previous + i) % 4; if (hoppers[slot] != null && hoppers[slot].IsAlive) return slot; }
            return previous;
        }

        internal void HopperDestroyed(VoxelBichoHopper hopper)
        {
            if (hopper.slot == ActiveLightSlot) { ActiveLightSlot = NextAliveSlot(ActiveLightSlot); phaseTime = 0; }
            UpdateLights();
            Owner.RefreshBichoBodyPhase();
        }

        internal bool Trace(Vector3 start, Vector3 direction, float length, out Transform hit)
        {
            hit = null; float best = length;
            foreach (var hopper in hoppers)
                if (hopper != null && hopper.Trace(start, direction, best, out float distance))
                { best = distance; hit = hopper.HitPart(start + direction * distance); }
            return hit != null;
        }

        internal void LanePairRange(int laneCount, out int minimum, out int maximum)
        {
            int centre = (laneCount - 2) / 2;
            minimum = hoppers[0] != null && hoppers[0].IsAlive ? centre : 0;
            maximum = hoppers[3] != null && hoppers[3].IsAlive ? centre : laneCount - 2;
        }

        internal VoxelBichoHopper FindRamContact(Vector2 previous, Vector2 current, out Vector3 direction)
        {
            direction = Vector3.forward;
            VoxelBichoHopper result = null; float earliest = float.PositiveInfinity;
            foreach (var hopper in hoppers)
            {
                if (hopper == null || !hopper.IsAlive) continue;
                Vector3 centre = Owner.transform.InverseTransformPoint(hopper.transform.TransformPoint(hopper.LocalBounds.center));
                Vector3 size = Vector3.Scale(hopper.LocalBounds.size, hopper.transform.lossyScale);
                var offset = new Vector2(centre.x, centre.z);
                if (!VoxelVehicleCollision.Sweep(previous - offset, current - offset,
                    new Vector2(size.x * .5f + 1.1f, size.z * .5f + 2.4f), out var contact)) continue;
                float distance = (contact - (previous - offset)).sqrMagnitude;
                if (distance >= earliest) continue;
                earliest = distance; result = hopper; direction = VoxelVehicleCollision.ImpactDirection(contact, player.transform);
            }
            return result;
        }
    }
}
