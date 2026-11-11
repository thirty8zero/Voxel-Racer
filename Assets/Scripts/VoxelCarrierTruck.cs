using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Articulated rear door and truck-local driving surface, with +Z toward the cab.</summary>
    public sealed class VoxelCarrierTruck : MonoBehaviour
    {
        [SerializeField] private Transform door;
        [SerializeField] private Transform extension;
        [SerializeField] private Transform slidingTip;
        [SerializeField] private float leafLength;
        [SerializeField] private float extensionLength;
        [SerializeField] private float tipLength;
        [SerializeField] private float bayFront;
        [SerializeField] private float clearWidth;
        [SerializeField] private float clearHeight;

        private float deployment;
        private bool openRequested;

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

        public void SetRampOpen(bool open) => openRequested = open;

        private void Update() => AdvanceRamp(Time.deltaTime);

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
    }
}
