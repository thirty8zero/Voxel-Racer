using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Articulated rear door and truck-local driving surface, with +Z toward the cab.</summary>
    public sealed class VoxelCarrierTruck : MonoBehaviour
    {
        private static readonly int SparkBaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform door;
        [SerializeField] private Transform extension;
        [SerializeField] private Transform slidingTip;
        [SerializeField] private float leafLength;
        [SerializeField] private float extensionLength;
        [SerializeField] private float tipLength;
        [SerializeField] private float bayFront;
        [SerializeField] private float clearWidth;
        [SerializeField] private float clearHeight;
        [SerializeField] private Transform[] wheels = System.Array.Empty<Transform>();
        [SerializeField] private float wheelRadius;
        [SerializeField] private Transform[] rampSparkContacts = System.Array.Empty<Transform>();

        private float deployment;
        private bool openRequested;
        private bool manualRampControl;
        private ParticleSystem[] rampSparks;
        private bool emittingRampSparks;

        public float DeckHeight => door.localPosition.y;
        public float BayRear => door.localPosition.z;
        public float BayFront => bayFront;
        public float ClearWidth => clearWidth;
        public float ClearHeight => clearHeight;
        public float RampTip => BayRear - Mathf.Sqrt(RampLength * RampLength - DeckHeight * DeckHeight);
        public bool IsOpen => deployment >= 1f;
        public bool IsClosed => deployment <= 0f;
        public bool Opening => openRequested;

        private float RampLength => leafLength + extensionLength + tipLength;
        private float OpenAngle => -90f - Mathf.Asin(DeckHeight / RampLength) * Mathf.Rad2Deg;

        public void Configure(Transform rampDoor, Transform rampExtension, Transform rampTip, float mainLength,
            float middleLength, float slidingLength, float front, float width, float height)
        {
            door = rampDoor;
            extension = rampExtension;
            slidingTip = rampTip;
            leafLength = mainLength;
            extensionLength = middleLength;
            tipLength = slidingLength;
            bayFront = front;
            clearWidth = width;
            clearHeight = height;
            ApplyRampPose();
        }

        public void ConfigureRollingDetails(Transform[] wheelPivots, float radius, Transform[] sparkContacts)
        {
            wheels = wheelPivots;
            wheelRadius = radius;
            rampSparkContacts = sparkContacts;
        }

        public void SetRampOpen(bool open)
        {
            openRequested = open;
            if (!open)
                SetRampSparks(false);
        }

        internal void UseManualRampControl() => manualRampControl = true;

        internal void AdvanceTravel(float distance)
        {
            if (wheelRadius > 0f && distance != 0f)
            {
                float angle = distance / wheelRadius * Mathf.Rad2Deg;
                foreach (Transform wheel in wheels)
                    wheel.Rotate(Vector3.right, angle, Space.Self);
            }
            SetRampSparks(IsOpen && openRequested && Mathf.Abs(distance) > .0001f);
        }

        private void SetRampSparks(bool emit)
        {
            if (emit == emittingRampSparks)
                return;

            if (emit && rampSparks == null)
                CreateRampSparks();
            if (rampSparks == null)
                return;

            emittingRampSparks = emit;
            foreach (ParticleSystem sparks in rampSparks)
            {
                if (emit) sparks.Play(false);
                else sparks.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void CreateRampSparks()
        {
            if (rampSparkContacts.Length == 0)
                return;
            var prefab = Resources.Load<ParticleSystem>("Effects/YellowVoxelImpactSparks");
            rampSparks = new ParticleSystem[prefab == null ? 0 : rampSparkContacts.Length];
            var sparkColor = new MaterialPropertyBlock();
            sparkColor.SetColor(SparkBaseColor, new Color(1.28f, 1.16f, .035f));
            for (int i = 0; i < rampSparks.Length; i++)
            {
                Transform contact = rampSparkContacts[i];
                var sparks = Instantiate(prefab, contact, false);
                rampSparks[i] = sparks;
                sparks.name = "Ramp Corner Sparks";
                Vector3 alongRoad = Vector3.ProjectOnPlane(contact.forward, transform.up).normalized;
                sparks.transform.SetPositionAndRotation(contact.position,
                    Quaternion.LookRotation(alongRoad + transform.up * .62f, transform.up));
                sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = sparks.main;
                main.playOnAwake = false;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.30f, .46f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 8.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(.028f, .054f);
                main.startColor = Color.white;
                main.gravityModifier = 3.5f;
                main.maxParticles = 96;
                sparks.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(sparkColor);
                var shape = sparks.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 22f;
                shape.radius = .025f;
                var emission = sparks.emission;
                emission.burstCount = 0;
                emission.rateOverTime = 200f;

                // The carrier root stays level at road height, so both emitters can share its plane.
                var collision = sparks.collision;
                collision.type = ParticleSystemCollisionType.Planes;
                collision.SetPlane(0, transform);
                collision.bounce = .38f;
                collision.dampen = .17f;
                collision.lifetimeLoss = .08f;
                collision.radiusScale = .35f;
                collision.sendCollisionMessages = false;
                collision.enabled = true;
            }
        }

        private void OnDisable()
        {
            if (rampSparks == null)
                return;
            emittingRampSparks = false;
            foreach (ParticleSystem sparks in rampSparks)
                if (sparks != null)
                    sparks.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Update()
        {
            if (!manualRampControl)
                AdvanceRamp(Time.deltaTime);
        }

        public void AdvanceRamp(float seconds)
        {
            float next = Mathf.MoveTowards(deployment, openRequested ? 1f : 0f, seconds / 3f);
            if (next == deployment)
                return;

            deployment = next;
            ApplyRampPose();
        }

        private void ApplyRampPose()
        {
            // Swing clear of the bay, unfold in the air, then extend the nested tip to the road.
            float lower = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .43f, deployment));
            float unfold = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.40f, .73f, deployment));
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.73f, 1f, deployment));
            door.localRotation = Quaternion.Euler(-90f * lower + (OpenAngle + 90f) * slide, 0f, 0f);
            extension.localRotation = Quaternion.Euler(Mathf.Lerp(180f, 0f, unfold), 0f, 0f);
            slidingTip.localPosition = new Vector3(0f, extensionLength - tipLength * (1f - slide),
                -.052f * (1f - slide));
        }

        /// <summary>Drive surface in truck space. Sample across the ramp only while fully deployed.</summary>
        public float SurfaceHeight(float z) => DeckHeight * Mathf.InverseLerp(RampTip, BayRear, z);

        internal Pose EvaluateCarPose(Vector2 axles, float wheelGround, float centerX, float rearZ)
        {
            float rearHeight = SurfaceHeight(rearZ);
            float wheelbase = axles.y - axles.x;
            // Keep the axle spacing constant across the two grade changes.
            float near = rearZ;
            float far = rearZ + wheelbase;
            for (int i = 0; i < 14; i++)
            {
                float mid = (near + far) * .5f;
                float rise = SurfaceHeight(mid) - rearHeight;
                float run = mid - rearZ;
                if (run * run + rise * rise > wheelbase * wheelbase)
                    far = mid;
                else
                    near = mid;
            }
            float frontZ = (near + far) * .5f;
            float pitch = -Mathf.Atan2(SurfaceHeight(frontZ) - rearHeight, frontZ - rearZ);
            var rotation = Quaternion.AngleAxis(pitch * Mathf.Rad2Deg, Vector3.right);
            var rearContact = new Vector3(-centerX, rearHeight, rearZ);
            return new Pose(rearContact - rotation * new Vector3(0f, wheelGround, axles.x), rotation);
        }
    }
}
