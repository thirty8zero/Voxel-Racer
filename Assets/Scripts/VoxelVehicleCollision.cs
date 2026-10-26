using UnityEngine;
namespace VoxelRacer
{
    /// <summary>Sweeps relative lane/track motion so boost lunges and fast oncoming traffic cannot skip contact.</summary>
    public static class VoxelVehicleCollision
    {
        public static bool Sweep(Vector2 previous, Vector2 current, Vector2 halfSize, out Vector2 contact)
        {
            float enter=0, exit=1;
            for(int axis=0;axis<2;axis++)
            {
                float delta=current[axis]-previous[axis];
                if(Mathf.Abs(delta)<.00001f)
                { if(Mathf.Abs(previous[axis])>halfSize[axis]) {contact=current;return false;} }
                else
                {
                    float a=(-halfSize[axis]-previous[axis])/delta, b=(halfSize[axis]-previous[axis])/delta;
                    enter=Mathf.Max(enter,Mathf.Min(a,b));exit=Mathf.Min(exit,Mathf.Max(a,b));
                    if(enter>exit) {contact=current;return false;}
                }
            }
            contact=Vector2.Lerp(previous,current,enter);return true;
        }
        public static Vector3 ImpactDirection(Vector2 contact,Transform player)
        {
            var forward=Vector3.ProjectOnPlane(player.forward,Vector3.up).normalized;
            var right=Vector3.Cross(Vector3.up,forward);
            var direction=-right*contact.x-forward*contact.y;
            return direction.sqrMagnitude>.0001f?direction.normalized:forward;
        }

        /// <summary>Rear contact persists through boost/recoil motion without repeating damage.</summary>
        public struct RearContact
        {
            private VoxelCarController player;
            private float clearance, width;
            public bool Active { get; private set; }
            public float BounceSpeed { get; private set; }

            public void Begin(VoxelCarController source, Transform vehicle, Vector2 collisionSize, float impactSpeed)
            {
                player = source;
                Bounds playerBounds = LocalBodyBounds(source.transform);
                Bounds vehicleBounds = LocalBodyBounds(vehicle);
                clearance = Mathf.Max(collisionSize.y, playerBounds.max.z +
                    Mathf.Max(Mathf.Abs(vehicleBounds.min.z), Mathf.Abs(vehicleBounds.max.z)) + .35f);
                width = Mathf.Max(collisionSize.x, playerBounds.extents.x + vehicleBounds.extents.x);
                BounceSpeed = Mathf.Max(source.CurrentSpeed, impactSpeed) + 8f;
                Active = true;
            }

            public bool Resolve(float lane, ref float distance)
            {
                if (!Active || player == null || player.IsDestroyed) { Active = false; return false; }
                Vector2 position = player.CollisionTrackPosition;
                float gap = distance - position.y;
                if (Mathf.Abs(lane - position.x) >= width || gap > clearance + 3f)
                { Active = false; return false; }
                float correction = Mathf.Max(0f, clearance - gap);
                distance += correction;
                return correction > 0f;
            }

            public void ResolveWreck(Transform wreck)
            {
                if (!Active || player == null || player.IsDestroyed) { Active = false; return; }
                Vector3 forward = player.TrackPath != null ? player.TrackPath.Evaluate(player.CollisionTrackPosition.y).forward : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector3 relative = wreck.position - player.transform.position;
                float gap = Vector3.Dot(relative, forward);
                if (Mathf.Abs(Vector3.Dot(relative, right)) >= width || gap > clearance + 3f)
                { Active = false; return; }
                wreck.position += forward * Mathf.Max(0f, clearance - gap);
            }

            public void HoldPlayerBehind(float distance)
            {
                if (Active && player != null) player.LimitRearRamPosition(distance - clearance);
            }

            private static Bounds LocalBodyBounds(Transform root)
            {
                Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
                bool found = false;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!renderer.enabled || renderer.GetComponentInParent<VoxelEnemyHealthBar>() != null) continue;
                    Matrix4x4 matrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    Bounds mesh = renderer.localBounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 point = matrix.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                return found ? bounds : new Bounds(Vector3.zero, new Vector3(2.6f, 2f, 5f));
            }
        }
    }
}
