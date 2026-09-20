using UnityEngine;

namespace VoxelRacer
{
    public sealed class VoxelRoadMine : MonoBehaviour
    {
        private VoxelCarController target;
        private VoxelMineLayerTuning tuning;
        private float distance, offset, born;
        private Vector2 previousPlayer;
        private Transform visual;
        private bool detonated;

        public void Configure(VoxelCarController player, EndlessVoxelRoad road, VoxelMineLayerTuning settings,
            float trackDistance, float laneOffset)
        {
            target = player; tuning = settings; distance = trackDistance; offset = laneOffset; born = Time.time;
            previousPlayer = new Vector2(player.CurrentLaneOffset, player.TrackDistance);
            var pose = road.Evaluate(distance);
            transform.SetPositionAndRotation(pose.position + pose.right * offset, pose.rotation);
            visual = Instantiate(settings.minePrefab, transform, false).transform;
        }

        private void Update()
        {
            if (target == null || target.IsDestroyed || VoxelMissionProgress.Active?.IsComplete == true ||
                Time.time - born > tuning.lifetime || distance < target.TrackDistance - 30f)
            { Destroy(gameObject); return; }
            float age = Time.time - born;
            if (visual != null) visual.localPosition = Vector3.up * (.35f * (1f - Mathf.Clamp01(age / .3f)));
            var current = new Vector2(target.CurrentLaneOffset, target.TrackDistance);
            if (!detonated && age >= tuning.armingDelay && CrossesMine(previousPlayer, current,
                new Vector2(offset, distance), new Vector2(tuning.collisionHalfWidth, tuning.collisionHalfLength)))
            {
                detonated = true;
                VoxelDestructionExplosion.Play(transform.position + Vector3.up * .2f, tuning.explosionScale);
                int saved = target.damageVoxelsPerHit;
                try
                {
                    target.damageVoxelsPerHit = Random.Range(Mathf.Min(tuning.playerDamageVoxelsMin, tuning.playerDamageVoxelsMax),
                        Mathf.Max(tuning.playerDamageVoxelsMin, tuning.playerDamageVoxelsMax) + 1);
                    target.ApplyDamage(target.GetDamageSurfacePoint(transform.position), Vector3.up, "Enemy mines");
                }
                finally { target.damageVoxelsPerHit = saved; }
                Destroy(gameObject);
            }
            previousPlayer = current;
        }

        // Swept lane/track bounds prevent fast or boosting players skipping a mine between frames.
        public static bool CrossesMine(Vector2 start, Vector2 end, Vector2 center, Vector2 halfSize)
        {
            float enter = 0f, exit = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                float delta = end[axis] - start[axis], local = start[axis] - center[axis];
                if (Mathf.Abs(delta) < .00001f) { if (Mathf.Abs(local) > halfSize[axis]) return false; }
                else
                {
                    float a = (-halfSize[axis] - local) / delta, b = (halfSize[axis] - local) / delta;
                    enter = Mathf.Max(enter, Mathf.Min(a, b)); exit = Mathf.Min(exit, Mathf.Max(a, b));
                    if (enter > exit) return false;
                }
            }
            return true;
        }
    }
}
