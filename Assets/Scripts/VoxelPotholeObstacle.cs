using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Indestructible lane-sized road hazard with a pixelated black edge.</summary>
    public sealed class VoxelPotholeObstacle : MonoBehaviour
    {
        public float LaneOffset => laneOffset;

        private static Material potholeMaterial;
        private VoxelCarController target;
        private VoxelStaticObstacleDefinition definition;
        private EndlessVoxelRoad path;
        private float trackDistance;
        private float laneOffset;
        private float laneWidth;
        private bool hasDamagedPlayer;

        public void Configure(VoxelCarController player, EndlessVoxelRoad road, VoxelStaticObstacleDefinition value,
            float distance, float offset, float roadLaneWidth)
        {
            target = player;
            path = road;
            definition = value;
            trackDistance = distance;
            laneOffset = offset;
            laneWidth = roadLaneWidth;
            ApplyTrackPose();
            BuildVisuals();
        }

        private void Update()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            bool overlapsLane = Mathf.Abs(target.CurrentLaneOffset - laneOffset) < laneWidth * 0.42f;
            bool overlapsDepth = Mathf.Abs(target.TrackDistance - trackDistance) < 1.35f;
            if (!hasDamagedPlayer && overlapsLane && overlapsDepth)
                DamagePlayer();
            if (trackDistance < target.TrackDistance - 25f)
                Destroy(gameObject);
        }

        private void DamagePlayer()
        {
            hasDamagedPlayer = true;
            int originalDamage = target.damageVoxelsPerHit;
            int minimum = definition != null ? definition.playerDamageVoxelsMin : 10;
            int maximum = definition != null ? definition.playerDamageVoxelsMax : 14;
            target.damageVoxelsPerHit = Random.Range(Mathf.Min(minimum, maximum), Mathf.Max(minimum, maximum) + 1);
            target.ApplyDamage(target.GetDamageSurfacePoint(transform.position), target.transform.forward);
            target.damageVoxelsPerHit = originalDamage;
        }

        private void ApplyTrackPose()
        {
            if (path == null)
                return;
            VoxelTrackPose pose = path.Evaluate(trackDistance);
            transform.position = pose.position + pose.right * laneOffset + Vector3.up * 0.012f;
            transform.rotation = pose.rotation;
        }

        private void BuildVisuals()
        {
            Material material = GetPotholeMaterial();
            float width = laneWidth * 0.78f;
            // A set of stepped horizontal strips makes an oval silhouette while
            // retaining the deliberately chunky voxel/pixel edge treatment.
            float[] rowDepths = { -1.12f, -0.61f, -0.08f, 0.48f, 1.02f };
            float[] rowWidthFactors = { 0.42f, 0.72f, 0.90f, 0.78f, 0.46f };
            for (int index = 0; index < rowDepths.Length; index++)
            {
                float rowWidth = width * rowWidthFactors[index];
                CreateFlatBlock("Pothole Body", new Vector3(0f, 0f, rowDepths[index]),
                    new Vector3(rowWidth, 0.018f, 0.58f), material);
            }

            Vector3[] edgePixels =
            {
                new Vector3(-width * 0.23f, 0.008f, -1.42f), new Vector3(width * 0.19f, 0.008f, -1.39f),
                new Vector3(-width * 0.40f, 0.008f, -0.91f), new Vector3(width * 0.40f, 0.008f, -0.85f),
                new Vector3(-width * 0.49f, 0.008f, -0.27f), new Vector3(width * 0.49f, 0.008f, -0.13f),
                new Vector3(-width * 0.46f, 0.008f, 0.41f), new Vector3(width * 0.44f, 0.008f, 0.54f),
                new Vector3(-width * 0.31f, 0.008f, 1.18f), new Vector3(width * 0.28f, 0.008f, 1.22f)
            };
            foreach (Vector3 pixel in edgePixels)
                CreateFlatBlock("Pothole Edge Pixel", pixel, new Vector3(0.34f, 0.028f, 0.34f), material);
        }

        private void CreateFlatBlock(string name, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject block = VoxelRacerBootstrap.CreateBlock(name, transform, localPosition, size, material);
            Destroy(block.GetComponent<BoxCollider>());
        }

        private static Material GetPotholeMaterial()
        {
            if (potholeMaterial != null)
                return potholeMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            potholeMaterial = new Material(shader != null ? shader : Shader.Find("Standard"))
            {
                name = "Pothole Black",
                color = new Color(0.008f, 0.009f, 0.012f)
            };
            potholeMaterial.SetColor("_BaseColor", potholeMaterial.color);
            potholeMaterial.SetFloat("_Smoothness", 0f);
            return potholeMaterial;
        }
    }
}
