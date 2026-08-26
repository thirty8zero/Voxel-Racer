using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Generates and recycles a road measured along a shared centreline path.</summary>
    [ExecuteAlways]
    public sealed class EndlessVoxelRoad : MonoBehaviour
    {
        [Header("Persistent Tuning")]
        [Tooltip("This road reads from the shared asset. Edit the asset itself to make persistent changes.")]
        public VoxelRoadTuning tuning;
        [Tooltip("Supplies this road's cactus palette and optional decorative scenery prefabs.")]
        public VoxelTrackDefinition trackDefinition;

        [Header("Road")]
        [Min(1)] public int laneCount = 4;
        [Min(1f)] public float roadWidth = 12f;
        [Min(1f)] public float groundWidth = 80f;
        [Header("Cacti")]
        [Min(0)] public int minimumCactiPerSegment = 3;
        [Min(0)] public int maximumCactiPerSegment = 8;
        [Min(0.1f)] public float minimumCactusHeightScale = 0.75f;
        [Min(0.1f)] public float maximumCactusHeightScale = 1.5f;
        [Min(0.1f)] public float minimumCactusWidthScale = 0.8f;
        [Min(0.1f)] public float maximumCactusWidthScale = 1.25f;
        [Header("Segments")]
        [Min(5f)] public float segmentLength = 30f;
        [Min(3)] public int segmentCount = 8;
        [Min(0f)] public float recycleBehindDistance = 45f;
        [Header("Turning Road Pieces")]
        [Range(0f, 1f)] public float turnChancePerSegment = 0.35f;
        [Min(1f)] public float minimumTurnAngle = 12f;
        [Min(1f)] public float maximumTurnAngle = 32f;
        [Min(0)] public int minimumStraightSegmentsBetweenTurns = 1;
        [Min(1f)] public float maximumTrackHeading = 65f;
        [Range(1f, 15f)] public float curveDegreesPerSlice = 5f;
        public int turnSeed = 173;

        private sealed class RoadSegment
        {
            public int index;
            public float startDistance;
            public Vector3 startPosition;
            public float startHeading;
            public float turnAngle;
            public Transform visual;
            public float EndDistance(float length) => startDistance + length;
        }

        private readonly List<RoadSegment> pathSegments = new();
        private readonly List<RoadSegment> visibleSegments = new();
        private Transform target;
        private VoxelCarController targetController;
        private Transform continuousGround;
        private System.Random turnRandom;
        private int straightSegmentsSinceTurn;
        private bool isRebuilding;
        private bool isApplyingTuning;
        private bool rebuildQueued;
        private bool editorRebuildQueued;

        private float appliedGroundWidth;
        private int appliedLaneCount;
        private float appliedRoadWidth;
        private float appliedSegmentLength;
        private int appliedSegmentCount;
        private int appliedMinimumCacti;
        private int appliedMaximumCacti;
        private float appliedMinimumCactusHeightScale;
        private float appliedMaximumCactusHeightScale;
        private float appliedMinimumCactusWidthScale;
        private float appliedMaximumCactusWidthScale;
        private float appliedTurnChance;
        private float appliedMinimumTurnAngle;
        private float appliedMaximumTurnAngle;
        private int appliedStraightSegmentsBetweenTurns;
        private float appliedMaximumTrackHeading;
        private float appliedCurveDegreesPerSlice;
        private int appliedTurnSeed;

        private static readonly Color[] CactusShades =
        {
            new(0.09f, 0.28f, 0.10f), new(0.12f, 0.34f, 0.12f),
            new(0.16f, 0.40f, 0.14f), new(0.18f, 0.33f, 0.10f)
        };

        public void SetTarget(Transform player)
        {
            target = player;
            targetController = player != null ? player.GetComponent<VoxelCarController>() : null;
            EnsureContinuousGround();
            if (targetController != null)
                targetController.SetTrack(this, FindClosestDistance(player.position));
            UpdateContinuousGroundPosition();
            SyncLaneLayout();
        }

        public void SetTuning(VoxelRoadTuning value)
        {
            tuning = value;
            if (tuning == null)
                return;
            isApplyingTuning = true;
            tuning.ApplyTo(this);
            isApplyingTuning = false;
            SanitizeValues();
            SyncLaneLayout();
            if (!Application.isPlaying && transform.Find("Road Segment") != null)
                QueueEditorRebuild();
        }

        public void BuildInitialRoad()
        {
            if (pathSegments.Count > 0)
                return;
            EnsureContinuousGround();
            turnRandom = new System.Random(turnSeed);
            straightSegmentsSinceTurn = minimumStraightSegmentsBetweenTurns;
            for (int index = 0; index < Mathf.Max(3, segmentCount); index++)
                AppendSegment(true);
            CaptureAppliedValues();
        }

        /// <summary>Compatibility hook used by the generated-scene bootstrap.</summary>
        public void RebuildSegmentCache()
        {
            if (pathSegments.Count > 0)
                return;

            // Runtime path data is intentionally non-serialized. Recreate it after a
            // domain reload and replace any now-untracked generated visuals.
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Transform child = transform.GetChild(index);
                if (child.name != "Road Segment")
                    continue;
                child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            BuildInitialRoad();
        }

        public float GetLaneOffset(int lane)
        {
            float width = roadWidth / Mathf.Max(1, laneCount);
            return (Mathf.Clamp(lane, 0, Mathf.Max(0, laneCount - 1)) - (laneCount - 1) * 0.5f) * width;
        }

        public void EnsurePathCovers(float distance)
        {
            if (pathSegments.Count == 0)
                BuildInitialRoad();
            while (pathSegments.Count > 0 && pathSegments[^1].EndDistance(segmentLength) < distance)
                AppendSegment(false);
        }

        public VoxelTrackPose Evaluate(float distance)
        {
            EnsurePathCovers(distance + segmentLength);
            if (pathSegments.Count == 0)
                return new VoxelTrackPose(new Vector3(0f, 0f, distance), 0f);
            RoadSegment first = pathSegments[0];
            if (distance < first.startDistance)
                return new VoxelTrackPose(first.startPosition + HeadingForward(first.startHeading) * (distance - first.startDistance), first.startHeading);

            int index = Mathf.Clamp(Mathf.FloorToInt((distance - first.startDistance) / Mathf.Max(0.01f, segmentLength)), 0, pathSegments.Count - 1);
            RoadSegment segment = pathSegments[index];
            return EvaluateSegment(segment, Mathf.Clamp(distance - segment.startDistance, 0f, segmentLength));
        }

        public float FindClosestDistance(Vector3 worldPosition)
        {
            if (pathSegments.Count == 0)
                BuildInitialRoad();
            float bestDistance = pathSegments.Count > 0 ? pathSegments[0].startDistance : worldPosition.z;
            float bestSqrDistance = float.PositiveInfinity;
            int searchCount = Mathf.Min(pathSegments.Count, Mathf.Max(3, segmentCount));
            for (int segmentIndex = 0; segmentIndex < searchCount; segmentIndex++)
            for (int sample = 0; sample <= 8; sample++)
            {
                RoadSegment segment = pathSegments[segmentIndex];
                float localDistance = segmentLength * sample / 8f;
                float sqrDistance = (EvaluateSegment(segment, localDistance).position - worldPosition).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestDistance = segment.startDistance + localDistance;
                }
            }

            float refinementStep = segmentLength / 8f;
            for (int refinement = 0; refinement < 10; refinement++)
            {
                float lowerDistance = bestDistance - refinementStep;
                float upperDistance = bestDistance + refinementStep;
                float lowerSqr = (Evaluate(lowerDistance).position - worldPosition).sqrMagnitude;
                float upperSqr = (Evaluate(upperDistance).position - worldPosition).sqrMagnitude;
                if (lowerSqr < bestSqrDistance)
                {
                    bestSqrDistance = lowerSqr;
                    bestDistance = lowerDistance;
                }
                if (upperSqr < bestSqrDistance)
                {
                    bestSqrDistance = upperSqr;
                    bestDistance = upperDistance;
                }
                refinementStep *= 0.5f;
            }
            return bestDistance;
        }

        private void OnValidate()
        {
            if (isApplyingTuning)
                return;
            SanitizeValues();
            SyncLaneLayout();
            if (!Application.isPlaying && !isRebuilding && isActiveAndEnabled)
                QueueEditorRebuild();
        }

        private void SanitizeValues()
        {
            maximumCactiPerSegment = Mathf.Max(minimumCactiPerSegment, maximumCactiPerSegment);
            minimumCactusHeightScale = Mathf.Max(0.1f, minimumCactusHeightScale);
            maximumCactusHeightScale = Mathf.Max(minimumCactusHeightScale, maximumCactusHeightScale);
            minimumCactusWidthScale = Mathf.Max(0.1f, minimumCactusWidthScale);
            maximumCactusWidthScale = Mathf.Max(minimumCactusWidthScale, maximumCactusWidthScale);
            maximumTurnAngle = Mathf.Max(minimumTurnAngle, maximumTurnAngle);
            maximumTrackHeading = Mathf.Max(maximumTurnAngle, maximumTrackHeading);
            curveDegreesPerSlice = Mathf.Max(1f, curveDegreesPerSlice);
        }

        private void RebuildRoadVisuals()
        {
            isRebuilding = true;
            DestroyRoadSegments(true);
            pathSegments.Clear();
            visibleSegments.Clear();
            BuildInitialRoad();
            ReapplyTargetToPath();
            isRebuilding = false;
        }

        private void QueueEditorRebuild()
        {
#if UNITY_EDITOR
            if (editorRebuildQueued)
                return;
            editorRebuildQueued = true;
            UnityEditor.EditorApplication.delayCall += RebuildInEditorWhenSafe;
#endif
        }

#if UNITY_EDITOR
        private void RebuildInEditorWhenSafe()
        {
            editorRebuildQueued = false;
            if (this == null || Application.isPlaying || !isActiveAndEnabled || isRebuilding)
                return;
            RebuildRoadVisuals();
        }
#endif

        private void SyncLaneLayout()
        {
            if (targetController != null)
                targetController.SetLaneLayout(laneCount, roadWidth / Mathf.Max(1, laneCount));
            var spawner = GetComponent<VoxelObstacleSpawner>();
            if (spawner != null)
            {
                spawner.laneCount = laneCount;
                spawner.laneWidth = roadWidth / Mathf.Max(1, laneCount);
            }
        }

        private void Update()
        {
            EnsureContinuousGround();
            UpdateContinuousGroundPosition();
            if (AppliedLayoutChanged() && !rebuildQueued)
            {
                if (Application.isPlaying)
                    StartCoroutine(RebuildAfterCurrentFrame());
                else
                    QueueEditorRebuild();
            }
            if (!Application.isPlaying || targetController == null || visibleSegments.Count == 0)
                return;

            while (visibleSegments.Count > 0 && targetController.TrackDistance > visibleSegments[0].EndDistance(segmentLength) + recycleBehindDistance)
            {
                RoadSegment oldest = visibleSegments[0];
                visibleSegments.RemoveAt(0);
                if (oldest.visual != null)
                    Destroy(oldest.visual.gameObject);
                int nextIndex = visibleSegments.Count > 0 ? visibleSegments[^1].index + 1 : oldest.index + 1;
                EnsurePathCovers(pathSegments[0].startDistance + (nextIndex + 1) * segmentLength);
                RoadSegment next = pathSegments[Mathf.Clamp(nextIndex, 0, pathSegments.Count - 1)];
                next.visual = CreateSegmentVisual(next);
                visibleSegments.Add(next);
            }
        }

        private IEnumerator RebuildAfterCurrentFrame()
        {
            rebuildQueued = true;
            isRebuilding = true;
            DestroyRoadSegments(false);
            pathSegments.Clear();
            visibleSegments.Clear();
            yield return null;
            BuildInitialRoad();
            ReapplyTargetToPath();
            isRebuilding = false;
            rebuildQueued = false;
        }

        private void DestroyRoadSegments(bool immediate)
        {
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Transform child = transform.GetChild(index);
                if (child.name != "Road Segment")
                    continue;
                if (immediate) DestroyImmediate(child.gameObject); else Destroy(child.gameObject);
            }
        }

        private void ReapplyTargetToPath()
        {
            if (targetController != null)
                targetController.SetTrack(this, targetController.TrackDistance);
        }

        private void AppendSegment(bool buildVisual)
        {
            int index = pathSegments.Count;
            var segment = new RoadSegment { index = index, startDistance = -segmentLength * 1.5f + index * segmentLength };
            if (index == 0)
            {
                segment.startPosition = new Vector3(0f, 0f, segment.startDistance);
                segment.startHeading = 0f;
            }
            else
            {
                RoadSegment previous = pathSegments[^1];
                VoxelTrackPose end = EvaluateSegment(previous, segmentLength);
                segment.startPosition = end.position;
                segment.startHeading = previous.startHeading + previous.turnAngle;
            }
            segment.turnAngle = ChooseTurnAngle(segment);
            pathSegments.Add(segment);
            if (buildVisual)
            {
                segment.visual = CreateSegmentVisual(segment);
                visibleSegments.Add(segment);
            }
        }

        private float ChooseTurnAngle(RoadSegment segment)
        {
            if (segment.index < 3 || straightSegmentsSinceTurn < minimumStraightSegmentsBetweenTurns || turnRandom.NextDouble() > turnChancePerSegment)
            {
                straightSegmentsSinceTurn++;
                return 0f;
            }
            float magnitude = Mathf.Lerp(minimumTurnAngle, maximumTurnAngle, (float)turnRandom.NextDouble());
            float candidate = (turnRandom.NextDouble() < 0.5 ? -1f : 1f) * magnitude;
            if (Mathf.Abs(segment.startHeading + candidate) > maximumTrackHeading)
                candidate = -candidate;
            if (Mathf.Abs(segment.startHeading + candidate) > maximumTrackHeading)
            {
                straightSegmentsSinceTurn++;
                return 0f;
            }
            straightSegmentsSinceTurn = 0;
            return candidate;
        }

        private VoxelTrackPose EvaluateSegment(RoadSegment segment, float localDistance)
        {
            float distance = Mathf.Clamp(localDistance, 0f, segmentLength);
            if (Mathf.Abs(segment.turnAngle) < 0.001f)
                return new VoxelTrackPose(segment.startPosition + HeadingForward(segment.startHeading) * distance, segment.startHeading);
            float curvature = segment.turnAngle * Mathf.Deg2Rad / segmentLength;
            float startRadians = segment.startHeading * Mathf.Deg2Rad;
            float headingRadians = startRadians + curvature * distance;
            Vector3 position = segment.startPosition + new Vector3(
                (Mathf.Cos(startRadians) - Mathf.Cos(headingRadians)) / curvature, 0f,
                (Mathf.Sin(headingRadians) - Mathf.Sin(startRadians)) / curvature);
            return new VoxelTrackPose(position, headingRadians * Mathf.Rad2Deg);
        }

        private static Vector3 HeadingForward(float headingDegrees) => Quaternion.Euler(0f, headingDegrees, 0f) * Vector3.forward;

        private Transform CreateSegmentVisual(RoadSegment data)
        {
            var segment = new GameObject("Road Segment").transform;
            segment.SetParent(transform, false);
            int slices = Mathf.Abs(data.turnAngle) < 0.001f ? 1 : Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(data.turnAngle) / curveDegreesPerSlice));
            float sliceLength = segmentLength / slices;
            for (int slice = 0; slice < slices; slice++)
            {
                VoxelTrackPose pose = EvaluateSegment(data, (slice + 0.5f) * sliceLength);
                float depth = sliceLength + (slices > 1 ? 0.65f : 0f);
                CreatePlacedBlock("Road", segment, pose, 0f, -0.14f, new Vector3(roadWidth, 0.28f, depth), VoxelRacerBootstrap.RoadMaterial);
                CreatePlacedBlock("Left Shoulder", segment, pose, -roadWidth * 0.5f - 0.55f, -0.08f, new Vector3(1.1f, 0.18f, depth), VoxelRacerBootstrap.ShoulderMaterial);
                CreatePlacedBlock("Right Shoulder", segment, pose, roadWidth * 0.5f + 0.55f, -0.08f, new Vector3(1.1f, 0.18f, depth), VoxelRacerBootstrap.ShoulderMaterial);
            }
            float laneWidth = roadWidth / Mathf.Max(1, laneCount);
            for (int line = 1; line < laneCount; line++)
            {
                float offset = -roadWidth * 0.5f + laneWidth * line;
                for (float distance = 3f; distance < segmentLength; distance += 6f)
                    CreatePlacedBlock("Lane Dash", segment, EvaluateSegment(data, distance), offset, 0.02f, new Vector3(0.16f, 0.035f, 2.8f), VoxelRacerBootstrap.LineMaterial);
            }
            float edgeOffset = roadWidth * 0.5f - 0.12f;
            for (float distance = 2.5f; distance < segmentLength; distance += 5f)
            {
                VoxelTrackPose pose = EvaluateSegment(data, distance);
                CreatePlacedBlock("Left Road Marker", segment, pose, -edgeOffset, 0.03f, new Vector3(0.18f, 0.06f, 1.6f), VoxelRacerBootstrap.LineMaterial);
                CreatePlacedBlock("Right Road Marker", segment, pose, edgeOffset, 0.03f, new Vector3(0.18f, 0.06f, 1.6f), VoxelRacerBootstrap.LineMaterial);
            }
            int cactusCount = Random.Range(Mathf.Min(minimumCactiPerSegment, maximumCactiPerSegment), Mathf.Max(minimumCactiPerSegment, maximumCactiPerSegment) + 1);
            for (int cactus = 0; cactus < cactusCount; cactus++)
                CreateCactus(segment, data);
            CreateAdditionalScenery(segment, data);
            segment.gameObject.AddComponent<VoxelFadeIn>();
            return segment;
        }

        private void EnsureContinuousGround()
        {
            if (continuousGround == null)
                continuousGround = transform.Find("Continuous Brown Ground");
            if (continuousGround == null)
            {
                continuousGround = VoxelRacerBootstrap.CreateBlock("Continuous Brown Ground", transform,
                    new Vector3(0f, -0.27f, 0f), new Vector3(groundWidth, 0.25f, groundWidth),
                    VoxelRacerBootstrap.GroundMaterial).transform;
                continuousGround.SetAsFirstSibling();
            }

            continuousGround.localScale = new Vector3(groundWidth, 0.25f, groundWidth);
            var renderer = continuousGround.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = VoxelRacerBootstrap.GroundMaterial;
        }

        private void UpdateContinuousGroundPosition()
        {
            if (continuousGround == null || target == null)
                return;
            Vector3 position = target.position;
            position.y = -0.27f;
            continuousGround.position = position;
            continuousGround.rotation = Quaternion.identity;
        }

        private static GameObject CreatePlacedBlock(string name, Transform parent, VoxelTrackPose pose,
            float lateralOffset, float height, Vector3 scale, Material material)
        {
            GameObject block = VoxelRacerBootstrap.CreateBlock(name, parent, Vector3.zero, scale, material);
            block.transform.position = pose.position + pose.right * lateralOffset + Vector3.up * height;
            block.transform.rotation = pose.rotation;
            return block;
        }

        private void CreateCactus(Transform segment, RoadSegment data)
        {
            float minimumOffset = roadWidth * 0.5f + 2f;
            float maximumOffset = groundWidth * 0.5f - 1.5f;
            if (maximumOffset <= minimumOffset)
                return;
            float side = Random.value < 0.5f ? -1f : 1f;
            VoxelTrackPose pose = EvaluateSegment(data, Random.Range(segmentLength * 0.05f, segmentLength * 0.95f));
            var cactus = new GameObject("Voxel Cactus").transform;
            cactus.SetParent(segment);
            cactus.position = pose.position + pose.right * side * Random.Range(minimumOffset, maximumOffset) - Vector3.up * 0.1f;
            cactus.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            BuildRandomCactusVisual(cactus, minimumCactusHeightScale, maximumCactusHeightScale, minimumCactusWidthScale, maximumCactusWidthScale,
                trackDefinition != null ? trackDefinition.cactusShades : null);
        }

        internal static void BuildRandomCactusVisual(Transform cactus, float minimumHeightScale,
            float maximumHeightScale, float minimumWidthScale, float maximumWidthScale, Color[] shades = null)
        {
            if (cactus == null) return;
            float widthScale = Random.Range(Mathf.Min(minimumWidthScale, maximumWidthScale), Mathf.Max(minimumWidthScale, maximumWidthScale));
            float heightScale = Random.Range(Mathf.Min(minimumHeightScale, maximumHeightScale), Mathf.Max(minimumHeightScale, maximumHeightScale));
            cactus.localScale = new Vector3(widthScale, heightScale, widthScale);
            Color[] availableShades = shades != null && shades.Length > 0 ? shades : CactusShades;
            Color shade = availableShades[Random.Range(0, availableShades.Length)];
            int shape = Random.Range(0, 4);
            CreateCactusBlock("Cactus Trunk", cactus, new Vector3(0f, 0.75f, 0f),
                new Vector3(shape == 3 ? 0.46f : 0.36f, shape == 3 ? 1.3f : 1.6f, shape == 3 ? 0.46f : 0.36f), shade);
            switch (shape)
            {
                case 0: CreateCactusArm(cactus, -1f, 0.85f, 1.08f, 0.60f, shade); CreateCactusArm(cactus, 1f, 0.55f, 0.75f, 0.50f, shade); break;
                case 1: CreateCactusArm(cactus, Random.value < 0.5f ? -1f : 1f, 1.05f, 1.30f, 0.72f, shade); break;
                case 2: CreateCactusArm(cactus, -1f, 0.62f, 0.82f, 0.44f, shade); CreateCactusArm(cactus, 1f, 1.02f, 1.30f, 0.68f, shade); break;
                default: CreateCactusArm(cactus, Random.value < 0.5f ? -1f : 1f, 0.48f, 0.63f, 0.34f, shade, 0.38f); break;
            }
        }

        private static void CreateCactusArm(Transform cactus, float side, float horizontalY,
            float uprightY, float uprightHeight, Color shade, float reach = 0.42f)
        {
            CreateCactusBlock("Cactus Arm", cactus, new Vector3(side * 0.33f, horizontalY, 0f), new Vector3(reach, 0.28f, 0.30f), shade);
            CreateCactusBlock("Cactus Arm Up", cactus, new Vector3(side * 0.53f, uprightY, 0f), new Vector3(0.28f, uprightHeight, 0.30f), shade);
        }

        private static void CreateCactusBlock(string name, Transform cactus, Vector3 position, Vector3 scale, Color shade)
        {
            GameObject block = VoxelRacerBootstrap.CreateBlock(name, cactus, position, scale, VoxelRacerBootstrap.CactusMaterial);
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", shade);
            block.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
        }

        private void CreateAdditionalScenery(Transform segment, RoadSegment data)
        {
            if (trackDefinition == null || trackDefinition.sceneryPrefabs == null || trackDefinition.sceneryPrefabs.Length == 0) return;
            float minimumOffset = roadWidth * 0.5f + 2f;
            float maximumOffset = groundWidth * 0.5f - 1.5f;
            if (maximumOffset <= minimumOffset) return;
            int count = Random.Range(Mathf.Min(trackDefinition.minimumSceneryPerSegment, trackDefinition.maximumSceneryPerSegment),
                Mathf.Max(trackDefinition.minimumSceneryPerSegment, trackDefinition.maximumSceneryPerSegment) + 1);
            for (int index = 0; index < count; index++)
            {
                GameObject prefab = trackDefinition.sceneryPrefabs[Random.Range(0, trackDefinition.sceneryPrefabs.Length)];
                if (prefab == null) continue;
                GameObject scenery = Instantiate(prefab, segment);
                scenery.name = prefab.name + " Scenery";
                float side = Random.value < 0.5f ? -1f : 1f;
                VoxelTrackPose pose = EvaluateSegment(data, Random.Range(segmentLength * 0.05f, segmentLength * 0.95f));
                scenery.transform.position = pose.position + pose.right * side * Random.Range(minimumOffset, maximumOffset);
                scenery.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                scenery.transform.localScale *= Random.Range(trackDefinition.minimumSceneryScale, trackDefinition.maximumSceneryScale);
            }
        }

        private bool AppliedLayoutChanged()
        {
            return appliedLaneCount != laneCount || !Mathf.Approximately(appliedRoadWidth, roadWidth) || !Mathf.Approximately(appliedGroundWidth, groundWidth) ||
                !Mathf.Approximately(appliedSegmentLength, segmentLength) || appliedSegmentCount != segmentCount || appliedMinimumCacti != minimumCactiPerSegment ||
                appliedMaximumCacti != maximumCactiPerSegment || !Mathf.Approximately(appliedMinimumCactusHeightScale, minimumCactusHeightScale) ||
                !Mathf.Approximately(appliedMaximumCactusHeightScale, maximumCactusHeightScale) || !Mathf.Approximately(appliedMinimumCactusWidthScale, minimumCactusWidthScale) ||
                !Mathf.Approximately(appliedMaximumCactusWidthScale, maximumCactusWidthScale) || !Mathf.Approximately(appliedTurnChance, turnChancePerSegment) ||
                !Mathf.Approximately(appliedMinimumTurnAngle, minimumTurnAngle) || !Mathf.Approximately(appliedMaximumTurnAngle, maximumTurnAngle) ||
                appliedStraightSegmentsBetweenTurns != minimumStraightSegmentsBetweenTurns || !Mathf.Approximately(appliedMaximumTrackHeading, maximumTrackHeading) ||
                !Mathf.Approximately(appliedCurveDegreesPerSlice, curveDegreesPerSlice) || appliedTurnSeed != turnSeed;
        }

        private void CaptureAppliedValues()
        {
            appliedGroundWidth = groundWidth; appliedLaneCount = laneCount; appliedRoadWidth = roadWidth;
            appliedSegmentLength = segmentLength; appliedSegmentCount = segmentCount; appliedMinimumCacti = minimumCactiPerSegment;
            appliedMaximumCacti = maximumCactiPerSegment; appliedMinimumCactusHeightScale = minimumCactusHeightScale;
            appliedMaximumCactusHeightScale = maximumCactusHeightScale; appliedMinimumCactusWidthScale = minimumCactusWidthScale;
            appliedMaximumCactusWidthScale = maximumCactusWidthScale; appliedTurnChance = turnChancePerSegment;
            appliedMinimumTurnAngle = minimumTurnAngle; appliedMaximumTurnAngle = maximumTurnAngle;
            appliedStraightSegmentsBetweenTurns = minimumStraightSegmentsBetweenTurns; appliedMaximumTrackHeading = maximumTrackHeading;
            appliedCurveDegreesPerSlice = curveDegreesPerSlice; appliedTurnSeed = turnSeed;
        }
    }
}
