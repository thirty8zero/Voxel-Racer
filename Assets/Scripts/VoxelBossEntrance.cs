using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Grows only the boss model, leaving its road position and collision bounds intact.</summary>
    public sealed class VoxelBossEntrance : MonoBehaviour
    {
        private Vector3 fullScale;
        private float duration, elapsed;

        public void Configure(float seconds)
        {
            fullScale=transform.localScale;
            duration=Mathf.Max(0,seconds);
            elapsed=0;
            transform.localScale=duration>0 ? Vector3.zero : fullScale;
            enabled=duration>0;
        }

        private void Update() => Advance(Time.deltaTime);

        private void Advance(float seconds)
        {
            elapsed+=Mathf.Max(0,seconds);
            float t=duration>0 ? Mathf.Clamp01(elapsed/duration) : 1;
            transform.localScale=fullScale*Mathf.SmoothStep(0,1,t);
            if(t>=1) enabled=false;
        }
    }
}
