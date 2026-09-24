using UnityEngine;
namespace VoxelRacer
{
    public sealed class VoxelBossSpikeRig : MonoBehaviour
    {
        public Transform leftDoor, rightDoor, spikes;
        public void SetPose(float doorsOpen, float spikesOut)
        {
            float angle = Mathf.SmoothStep(0, 180, Mathf.Clamp01(doorsOpen));
            if (leftDoor != null) leftDoor.localRotation = Quaternion.Euler(0, angle, 0);
            if (rightDoor != null) rightDoor.localRotation = Quaternion.Euler(0, -angle, 0);
            if (spikes != null) spikes.localPosition = new Vector3(0, 0, Mathf.Lerp(0, -2.05f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(spikesOut))));
        }
    }
}
