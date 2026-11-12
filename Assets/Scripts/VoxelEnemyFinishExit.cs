using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Moves intact enemies out of the finish shot without gameplay contact.</summary>
    internal struct VoxelEnemyFinishExit
    {
        private float speed, destinationSpeed, elapsed;
        private bool fading;
        internal bool Active { get; private set; }

        internal void Begin(float currentSpeed, float playerTopSpeed, bool pullAway)
        {
            Active = true;
            speed = Mathf.Max(0f, currentSpeed);
            if (pullAway)
            {
                speed = Mathf.Max(speed, playerTopSpeed);
                destinationSpeed = speed + 22f;
            }
            else
            {
                speed = Mathf.Min(speed, playerTopSpeed);
                destinationSpeed = Mathf.Min(speed * .35f, playerTopSpeed * .24f);
            }
        }

        internal float Advance(float dt, float gap, GameObject owner)
        {
            speed = Mathf.MoveTowards(speed, destinationSpeed, 28f * dt);
            elapsed += dt;
            if (!fading && (elapsed >= 1.65f || (elapsed >= .65f && Mathf.Abs(gap) > 55f)))
            {
                fading = true;
                VoxelFadeIn.FadeOutAndDestroy(owner, .55f);
            }
            return speed;
        }
    }
}
