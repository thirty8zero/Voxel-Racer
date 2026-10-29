using UnityEngine;
namespace VoxelRacer
{
    public sealed class VoxelBossSpikeRig : MonoBehaviour
    {
        public Transform leftDoor, rightDoor, spikes;
        private bool spikesDetached;

        /// <summary>Group the authored shaft/tip pieces into three independent death fragments.</summary>
        public void DetachOnDestruction(VoxelBossDefinition settings, Vector3 inheritedVelocity)
        {
            if (spikesDetached || spikes == null || !Application.isPlaying) return;
            spikesDetached = true;
            var groups = new System.Collections.Generic.List<System.Collections.Generic.List<Transform>>();
            var positions = new System.Collections.Generic.List<float>();
            foreach (Transform piece in spikes)
            {
                if (!piece.gameObject.activeSelf || piece.GetComponent<MeshRenderer>() == null) continue;
                int group = -1;
                for (int i = 0; i < positions.Count; i++)
                    if (Mathf.Abs(piece.localPosition.x - positions[i]) < .01f) { group = i; break; }
                if (group < 0)
                {
                    group = groups.Count; positions.Add(piece.localPosition.x);
                    groups.Add(new System.Collections.Generic.List<Transform>());
                }
                groups[group].Add(piece);
            }
            foreach (var parts in groups)
            {
                Bounds bounds = parts[0].GetComponent<MeshRenderer>().bounds;
                foreach (var part in parts) bounds.Encapsulate(part.GetComponent<MeshRenderer>().bounds);
                var fragment = new GameObject("Boss Detached Ram Spike");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fragment, gameObject.scene);
                fragment.transform.position = bounds.center;
                fragment.AddComponent<VoxelIndestructiblePart>();
                float localX = spikes.InverseTransformPoint(bounds.center).x;
                float side = Mathf.Abs(localX) < .01f ? 0 : Mathf.Sign(localX);
                foreach (var part in parts) part.SetParent(fragment.transform, true);
                Vector3 launch = inheritedVelocity * Mathf.Clamp01(settings.spikeVelocityInheritance) - transform.forward * settings.spikeLaunchForce +
                    Vector3.up * settings.spikeUpwardForce + transform.right * side * settings.spikeSideForce +
                    Random.insideUnitSphere * settings.spikeSpreadForce;
                fragment.AddComponent<VoxelBossSpikeDebris>().Launch(launch, settings);
                VoxelDestructionExplosion.PlayAtWorldScale(bounds.center, settings.spikeExplosionScale);
            }
        }
        public void SetPose(float doorsOpen, float spikesOut)
        {
            float angle = Mathf.SmoothStep(0, 180, Mathf.Clamp01(doorsOpen));
            if (leftDoor != null) leftDoor.localRotation = Quaternion.Euler(0, angle, 0);
            if (rightDoor != null) rightDoor.localRotation = Quaternion.Euler(0, -angle, 0);
            if (spikes != null) spikes.localPosition = new Vector3(0, 0, Mathf.Lerp(0, -2.05f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(spikesOut))));
        }
    }
}
