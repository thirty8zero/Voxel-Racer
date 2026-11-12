using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Measured race-car geometry shared by both rolling carrier sequences.</summary>
    internal sealed class VoxelCarrierCar
    {
        internal static readonly Vector3 RearLeftEye = new(-5.2f, 1.85f, -6.4f);
        private readonly VoxelCarController player;
        private readonly Transform[] wheels;
        private readonly Transform[] steeringPivots;
        private readonly Vector2 axles;
        private Transform plough;
        private Vector3 ploughPosition, ploughHinge;
        private Quaternion ploughRotation;
        private Vector3[] ploughCorners;
        private float ploughLift;
        private bool steeringCentered;
        internal readonly Bounds Bounds;
        internal readonly float WheelGround;

        private VoxelCarrierCar(VoxelCarController player, Transform[] wheels, Transform[] steeringPivots,
            Bounds body, Bounds tyres)
        {
            this.player = player;
            this.wheels = wheels;
            this.steeringPivots = steeringPivots;
            Bounds = body;
            WheelGround = tyres.min.y;
            Transform car = player.transform;
            axles = new Vector2(wheels.Min(wheel => car.InverseTransformPoint(wheel.position).z),
                wheels.Max(wheel => car.InverseTransformPoint(wheel.position).z));
            CachePlough();
        }

        internal static VoxelCarrierCar TryMeasure(VoxelCarController player, VoxelCarrierTruck truck)
        {
            Transform car = player.transform;
            Transform[] wheels = car.GetComponentsInChildren<Transform>()
                .Where(child => child.name == "Voxel Wheel").ToArray();
            if (wheels.Length != 4)
                return null;

            Transform[] steering = wheels.Select(wheel => wheel.parent)
                .Where(pivot => pivot != null && pivot.name == "Front Wheel Steering").Distinct().ToArray();
            Quaternion[] steeringRotations = steering.Select(pivot => pivot.localRotation).ToArray();
            try
            {
                // Measure the pose the car will actually have when it reaches the ramp.
                foreach (var pivot in steering) pivot.localRotation = Quaternion.identity;
                if (!Measure(car, car.GetComponentsInChildren<MeshRenderer>(), out Bounds body) ||
                    !Measure(car, wheels.SelectMany(wheel => wheel.GetComponentsInChildren<MeshRenderer>()), out Bounds tyres))
                    return null;
                if (body.size.x > truck.ClearWidth - .06f || body.size.y > truck.ClearHeight - .09f ||
                    body.size.z > truck.BayFront - truck.BayRear - .30f)
                    return null;
                return new VoxelCarrierCar(player, wheels, steering, body, tyres);
            }
            finally
            {
                for (int i = 0; i < steering.Length; i++) steering[i].localRotation = steeringRotations[i];
            }
        }

        internal float AxleBehindNose(float noseZ) => noseZ - (Bounds.max.z - axles.x);

        internal Pose Evaluate(VoxelCarrierTruck truck, float rearAxle) =>
            truck.EvaluateCarPose(axles, WheelGround, Bounds.center.x, rearAxle);

        internal void RotateWheels(float distance)
        {
            foreach (Transform wheel in wheels)
                wheel.Rotate(Vector3.right, distance * player.wheelSpinDegreesPerUnit, Space.Self);
        }

        internal void CenterSteering(float dt)
        {
            if (steeringCentered) return;

            steeringCentered = true;
            foreach (Transform pivot in steeringPivots)
                if (pivot != null)
                    steeringCentered &= VoxelCarController.AdvanceFrontWheelSteering(pivot, 0f, dt) == 0f;
        }

        private void CachePlough()
        {
            plough = player.GetComponentInChildren<VoxelPloughMount>()?.transform;
            if (plough == null)
                return;
            Transform car = player.transform;
            var points = new List<Vector3>();
            foreach (var renderer in plough.GetComponentsInChildren<MeshRenderer>())
                AddCorners(car, renderer, points);
            if (points.Count == 0)
            {
                plough = null;
                return;
            }
            ploughCorners = points.ToArray();
            ploughPosition = plough.localPosition;
            ploughRotation = plough.localRotation;
            ploughHinge = new Vector3(0f, ploughPosition.y, points.Min(point => point.z));
        }

        internal void UpdatePlough(VoxelCarrierTruck truck, Pose local, float leadAxle, float dt)
        {
            if (plough == null)
                return;
            float required = RequiredLift(truck, local);
            Pose next = Evaluate(truck, leadAxle);
            float lead = Mathf.Max(required, RequiredLift(truck, next));
            ploughLift = Mathf.Max(required, Mathf.MoveTowards(ploughLift, lead, dt * 120f));
            var lift = Quaternion.AngleAxis(-ploughLift, Vector3.right);
            plough.localPosition = ploughHinge + lift * (ploughPosition - ploughHinge);
            plough.localRotation = lift * ploughRotation;
        }

        private float RequiredLift(VoxelCarrierTruck truck, Pose pose)
        {
            if (PloughClears(truck, pose, 0f))
                return 0f;
            float min = 0f, max = 42f;
            for (int i = 0; i < 7; i++)
            {
                float angle = (min + max) * .5f;
                if (PloughClears(truck, pose, angle)) max = angle;
                else min = angle;
            }
            return max;
        }

        private bool PloughClears(VoxelCarrierTruck truck, Pose pose, float liftDegrees)
        {
            Quaternion lift = Quaternion.AngleAxis(-liftDegrees, Vector3.right);
            foreach (Vector3 corner in ploughCorners)
            {
                Vector3 point = pose.position + pose.rotation * (ploughHinge + lift * (corner - ploughHinge));
                if (point.y < truck.SurfaceHeight(point.z) + .025f)
                    return false;
            }
            return true;
        }

        internal void RestorePlough()
        {
            if (plough == null)
                return;
            plough.localPosition = ploughPosition;
            plough.localRotation = ploughRotation;
        }

        private static bool Measure(Transform root, IEnumerable<MeshRenderer> renderers, out Bounds bounds)
        {
            bounds = default;
            bool measured = false;
            Matrix4x4 toCar = root.worldToLocalMatrix;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled)
                    continue;
                Matrix4x4 matrix = toCar * renderer.localToWorldMatrix;
                Bounds local = renderer.localBounds;
                Vector3 extent = local.extents;
                Vector3 x = matrix.MultiplyVector(new Vector3(extent.x, 0f, 0f));
                Vector3 y = matrix.MultiplyVector(new Vector3(0f, extent.y, 0f));
                Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, extent.z));
                Vector3 radius = new(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                    Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                    Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
                var meshBounds = new Bounds(matrix.MultiplyPoint3x4(local.center), radius * 2f);
                if (measured) bounds.Encapsulate(meshBounds);
                else bounds = meshBounds;
                measured = true;
            }
            return measured;
        }

        private static void AddCorners(Transform root, MeshRenderer renderer, List<Vector3> points)
        {
            Matrix4x4 matrix = root.worldToLocalMatrix * renderer.localToWorldMatrix;
            Bounds local = renderer.localBounds;
            for (int i = 0; i < 8; i++)
                points.Add(matrix.MultiplyPoint3x4(new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z)));
        }
    }
}
