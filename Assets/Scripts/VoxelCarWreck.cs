using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelRacer
{
    public sealed partial class VoxelCarController
    {
        // Reserved for the two death-only colliders. Layer overrides isolate them from traffic/debris.
        private const int WreckPhysicsLayer = 30;
        private Rigidbody wreckBody;
        private BoxCollider wreckCollider;
        private GameObject wreckGround;
        private PhysicsMaterial wreckMaterial;
        private Bounds wreckLocalBounds;
        private bool wreckBoundsCached;
        private float wreckSurfaceHeight, wreckStillTime;

        public Vector3 WreckFocusPosition => wreckBoundsCached
            ? transform.TransformPoint(wreckLocalBounds.center) : transform.position;

        private void BeginDestroyedWreck(Vector3 impactDirection)
        {
            // Cache a single hull around surviving chassis/body/wheels. Attachments do not enlarge it.
            bool hasBounds = false;
            foreach (var filter in GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;
                bool attachment = false;
                for (var parent = filter.transform; parent != transform && parent != null; parent = parent.parent)
                    if (parent.name.StartsWith("Purchased ")) { attachment = true; break; }
                if (attachment) continue;
                var bounds = filter.sharedMesh.bounds;
                var matrix = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (!hasBounds) { wreckLocalBounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    else wreckLocalBounds.Encapsulate(point);
                }
            }
            if (!hasBounds) wreckLocalBounds = new Bounds(Vector3.up * .7f, new Vector3(2.2f, 1.4f, 5f));
            wreckBoundsCached = true;
            wreckStillTime = 0f;
            wreckSurfaceHeight = TrackPath != null ? TrackPath.Evaluate(TrackDistance).position.y : transform.position.y;
            // Existing road geometry is flat and collider-free; a local slab supplies just the wreck's contact.
            wreckGround = new GameObject("Wreck Ground Collision") { layer = WreckPhysicsLayer };
            SceneManager.MoveGameObjectToScene(wreckGround, gameObject.scene);
            wreckGround.transform.SetParent(transform.parent, true);
            wreckGround.transform.position = new Vector3(transform.position.x, wreckSurfaceHeight - .5f, transform.position.z);
            var groundCollider = wreckGround.AddComponent<BoxCollider>();
            groundCollider.size = new Vector3(160f, 1f, 160f);
            wreckMaterial = new PhysicsMaterial("Wreck Ground Contact") {
                dynamicFriction = .75f, staticFriction = .85f, bounciness = .16f,
                frictionCombine = PhysicsMaterialCombine.Maximum, bounceCombine = PhysicsMaterialCombine.Minimum };
            ConfigureWreckCollider(groundCollider);

            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            var hull = new GameObject("Wreck Collision Hull") { layer = WreckPhysicsLayer };
            hull.transform.SetParent(transform, false);
            wreckCollider = hull.AddComponent<BoxCollider>();
            wreckCollider.center = wreckLocalBounds.center;
            wreckCollider.size = Vector3.Max(wreckLocalBounds.size, Vector3.one * .1f);
            ConfigureWreckCollider(wreckCollider);
            wreckBody = GetComponent<Rigidbody>();
            if (wreckBody == null) wreckBody = gameObject.AddComponent<Rigidbody>();
            wreckBody.isKinematic = false;
            wreckBody.useGravity = true;
            wreckBody.mass = 1000f;
            wreckBody.linearDamping = .12f;
            wreckBody.angularDamping = .8f;
            wreckBody.interpolation = RigidbodyInterpolation.Interpolate;
            // Speculative CCD covers angular motion as well as travel, at lower cost than sweep-based CCD.
            wreckBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            wreckBody.solverIterations = 6;
            wreckBody.solverVelocityIterations = 2;
            wreckBody.sleepThreshold = .02f;
            wreckBody.ResetCenterOfMass();
            wreckBody.ResetInertiaTensor();
            Vector3 direction = impactDirection.sqrMagnitude > .001f ? impactDirection.normalized : transform.forward;
            wreckBody.linearVelocity = direction * Random.Range(explosionForwardForceMin, explosionForwardForceMax)
                + Vector3.up * Mathf.Max(2f, explosionUpwardForce * 2f);
            // Contact torque now determines the bounce/roll; rotation is never written by the driving controller.
            wreckBody.angularVelocity = Vector3.Cross(Vector3.up, direction).normalized * 3.2f
                + transform.forward * 1.3f + Vector3.up * .6f;
            wreckBody.WakeUp();
        }

        private void ConfigureWreckCollider(BoxCollider collider)
        {
            collider.sharedMaterial = wreckMaterial;
            collider.includeLayers = 1 << WreckPhysicsLayer;
            collider.excludeLayers = ~(1 << WreckPhysicsLayer);
            collider.layerOverridePriority = 100;
        }

        private void UpdateDestroyedWreck() => TickDestroyedWreck(Time.deltaTime);

        private void TickDestroyedWreck(float deltaTime)
        {
            if (wreckResting || wreckBody == null) return;
            bool grounded = wreckCollider.bounds.min.y <= wreckSurfaceHeight + .06f;
            bool slow = wreckBody.linearVelocity.sqrMagnitude < .01f && wreckBody.angularVelocity.sqrMagnitude < .01f;
            wreckStillTime = grounded && slow ? wreckStillTime + Mathf.Max(0f, deltaTime) : 0f;
            if (!grounded || (!wreckBody.IsSleeping() && wreckStillTime < .5f)) return;
            wreckBody.linearVelocity = wreckBody.angularVelocity = Vector3.zero;
            wreckBody.Sleep();
            wreckResting = true;
        }

        private void OnDestroy()
        {
            if (wreckGround != null) wreckGround.SetActive(false);
            if (Application.isPlaying) { Destroy(wreckGround); Destroy(wreckMaterial); }
            else { if (wreckGround != null) DestroyImmediate(wreckGround); if (wreckMaterial != null) DestroyImmediate(wreckMaterial); }
        }
    }
}
