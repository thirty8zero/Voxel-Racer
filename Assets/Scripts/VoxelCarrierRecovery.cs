using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Runs the moving pickup once the mission has awarded its results.</summary>
    internal sealed class VoxelCarrierRecovery
    {
        private enum Phase { Waiting, Passing, Merging, Opening, Boarding, Closing, Leaving }

        private const float ApproachGap = 48f;
        private const float PassSpeed = 19f;
        private const float LaneChangeTime = 1f;
        private const float RampOpenTime = 1.25f;
        private const float RampCloseTime = .95f;
        private const float CameraPanTime = 1.65f;
        private const float ClearLaneTimeout = 15f;

        private readonly Transform owner;
        private readonly VoxelCarController player;
        private readonly VoxelCarrierTruck prefab;
        private readonly EndlessVoxelRoad road;
        private readonly VoxelCarrierCar fit;
        private readonly VoxelObstacleCar[] traffic;
        private readonly VoxelObstacle[] obstacles;
        private readonly VoxelPotholeObstacle[] potholes;
        private readonly VoxelFuelDrumObstacle[] drums;
        private readonly VoxelRoadMine[] mines;
        private readonly VoxelOilSlickObstacle[] oil;
        private readonly float cruiseSpeed, laneWidth, loadingLane, initialLane;
        private readonly float firstAxle, loadedAxle, passGap, boardingDuration;
        private VoxelCarrierTruck truck;
        private Phase phase;
        private float lane, laneChange, waiting, phaseTime;
        private float carDistance, truckDistance, lastTruckDistance, gap, cameraDistance, panTime;
        private float departureSpeed;
        private bool startedPan, capturedCamera, releasedSky;
        private Vector3 panOffset, panAimOffset;
        private Camera view;
        private float panFieldOfView;

        internal bool HasCamera => startedPan;
        internal bool IsLoaded => phase == Phase.Leaving;
        internal bool TimedOut => phase == Phase.Waiting && waiting >= ClearLaneTimeout;

        private VoxelCarrierRecovery(Transform owner, VoxelCarController player, VoxelCarrierTruck prefab, VoxelCarrierCar fit)
        {
            this.owner = owner;
            this.player = player;
            this.prefab = prefab;
            this.fit = fit;
            road = player.TrackPath;
            cruiseSpeed = player.EffectiveTopSpeed;
            laneWidth = road.roadWidth / road.laneCount;
            initialLane = lane = player.CollisionTrackPosition.x;
            int index = Mathf.Clamp(Mathf.RoundToInt(lane / laneWidth + (road.laneCount - 1) * .5f), 0, road.laneCount - 2);
            loadingLane = road.GetLaneOffset(index);
            carDistance = player.CollisionTrackPosition.y;
            firstAxle = fit.AxleBehindNose(prefab.RampTip - .38f);
            loadedAxle = fit.AxleBehindNose(prefab.BayFront - .13f);
            passGap = -fit.Evaluate(prefab, firstAxle).position.z;
            boardingDuration = (loadedAxle - firstAxle) / Mathf.Min(4.25f, cruiseSpeed * .5f);
            traffic = Object.FindObjectsByType<VoxelObstacleCar>(FindObjectsSortMode.None);
            obstacles = Object.FindObjectsByType<VoxelObstacle>(FindObjectsSortMode.None);
            potholes = Object.FindObjectsByType<VoxelPotholeObstacle>(FindObjectsSortMode.None);
            drums = Object.FindObjectsByType<VoxelFuelDrumObstacle>(FindObjectsSortMode.None);
            mines = Object.FindObjectsByType<VoxelRoadMine>(FindObjectsSortMode.None);
            oil = Object.FindObjectsByType<VoxelOilSlickObstacle>(FindObjectsSortMode.None);
            player.PrepareCarrierRecovery();
            PlacePlayerOnRoad(0f);
        }

        internal static VoxelCarrierRecovery TryCreate(Transform owner, VoxelCarController player)
        {
            if (owner.gameObject.scene.buildIndex < 0 || player.TrackPath == null ||
                player.TrackPath.laneCount < 2 || player.EffectiveTopSpeed < 1f)
                return null;
            var prefab = VoxelMissionBriefingSettings.Load()?.carrierTruck;
            if (prefab == null)
                return null;
            var fit = VoxelCarrierCar.TryMeasure(player, prefab);
            return fit == null ? null : new VoxelCarrierRecovery(owner, player, prefab, fit);
        }

        internal void Advance(float dt, bool orbitComplete)
        {
            fit.CenterSteering(dt);
            if (startedPan)
                panTime += dt;
            phaseTime += dt;
            switch (phase)
            {
                case Phase.Waiting:
                    AdvanceWaiting(dt, orbitComplete);
                    break;
                case Phase.Passing:
                    carDistance += cruiseSpeed * dt;
                    gap = Mathf.MoveTowards(gap, passGap, dt * Mathf.Clamp((passGap - gap) * 3f, 5f, PassSpeed));
                    truckDistance = carDistance + gap;
                    PlaceTruck(loadingLane + laneWidth, dt);
                    PlacePlayerOnRoad(dt);
                    startedPan |= gap >= -.75f;
                    if (gap >= passGap) Enter(Phase.Merging);
                    break;
                case Phase.Merging:
                    float merge = Mathf.Clamp01(phaseTime / LaneChangeTime);
                    AdvanceConvoy(dt, loadingLane + laneWidth * (1f - Mathf.SmoothStep(0f, 1f, merge)),
                        -7f * Mathf.Sin(merge * Mathf.PI));
                    if (phaseTime >= LaneChangeTime)
                    {
                        truck.SetRampOpen(true);
                        Enter(Phase.Opening);
                    }
                    break;
                case Phase.Opening:
                    truck.AdvanceRamp(dt * (3f / RampOpenTime));
                    AdvanceConvoy(dt, loadingLane);
                    if (truck.IsOpen) Enter(Phase.Boarding);
                    break;
                case Phase.Boarding:
                    AdvanceBoarding(dt);
                    break;
                case Phase.Closing:
                    truckDistance += cruiseSpeed * dt;
                    PlaceTruck(loadingLane, dt);
                    PlacePlayerInBay(loadedAxle, cruiseSpeed);
                    truck.AdvanceRamp(dt * (3f / RampCloseTime));
                    if (truck.IsClosed) ReleaseCamera();
                    break;
                case Phase.Leaving:
                    AdvanceDeparture(dt);
                    break;
            }
        }

        private void Enter(Phase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        private void AdvanceWaiting(float dt, bool orbitComplete)
        {
            carDistance += cruiseSpeed * dt;
            if (lane != loadingLane && (laneChange > 0f || LaneIsClear(loadingLane, -8f, 10f, LaneChangeTime + .4f)))
            {
                laneChange += dt;
                lane = laneChange >= LaneChangeTime ? loadingLane :
                    Mathf.Lerp(initialLane, loadingLane, Mathf.SmoothStep(0f, 1f, laneChange / LaneChangeTime));
            }
            PlacePlayerOnRoad(dt);
            if (!orbitComplete) return;
            waiting += dt;
            float passTime = (ApproachGap + passGap) / PassSpeed + 1.4f;
            float pickupTime = passTime + RampOpenTime + boardingDuration + RampCloseTime;
            if (lane != loadingLane || !LaneIsClear(loadingLane + laneWidth, -ApproachGap - 8f, passGap + 10f, passTime) ||
                !LaneIsClear(loadingLane, -8f, passGap + 36f, pickupTime + 2f))
                return;

            truck = Object.Instantiate(prefab, owner);
            truck.UseManualRampControl();
            gap = -ApproachGap;
            truckDistance = carDistance + gap;
            PlaceTruck(loadingLane + laneWidth, 0f);
            Enter(Phase.Passing);
        }

        private bool LaneIsClear(float offset, float rear, float front, float seconds)
        {
            bool InLane(float otherOffset, float halfWidth) => Mathf.Abs(otherOffset - offset) < laneWidth * .5f + halfWidth;
            bool InRange(float distance, float travelSpeed, float halfLength)
            {
                float relative = distance - carDistance;
                float later = relative + (travelSpeed - cruiseSpeed) * seconds;
                return Mathf.Min(relative, later) - halfLength < front && Mathf.Max(relative, later) + halfLength > rear;
            }

            foreach (var vehicle in traffic)
            {
                if (vehicle == null || !vehicle.isActiveAndEnabled || !vehicle.IsDrivingTraffic ||
                    !InLane(vehicle.LaneOffset, vehicle.TrafficHalfWidth)) continue;
                float speed = vehicle.TravelsWithPlayer ? vehicle.TravelSpeed : -vehicle.TravelSpeed;
                float fastest = vehicle.MaximumPhaseTravelSpeed * (vehicle.TravelsWithPlayer ? 1f : -1f);
                if (InRange(vehicle.TrackDistance, speed, vehicle.TrafficHalfLength) ||
                    InRange(vehicle.TrackDistance, fastest, vehicle.TrafficHalfLength)) return false;
            }
            bool Blocks(Behaviour item, float otherLane, float distance) =>
                item.isActiveAndEnabled && Mathf.Abs(otherLane - offset) < laneWidth * .5f && InRange(distance, 0f, 3f);
            foreach (var item in obstacles) if (item != null && item.IsRoadHazard && Blocks(item, item.LaneOffset, item.TrackDistance)) return false;
            foreach (var item in potholes) if (item != null && Blocks(item, item.LaneOffset, item.TrackDistance)) return false;
            foreach (var item in drums) if (item != null && Blocks(item, item.LaneOffset, item.TrackDistance)) return false;
            foreach (var item in mines) if (item != null && Blocks(item, item.LaneOffset, item.TrackDistance)) return false;
            foreach (var item in oil) if (item != null && Blocks(item, item.LaneOffset, item.TrackDistance)) return false;
            return true;
        }

        private void AdvanceConvoy(float dt, float truckLane, float steering = 0f)
        {
            carDistance += cruiseSpeed * dt;
            truckDistance = carDistance + passGap;
            PlaceTruck(truckLane, dt, steering);
            PlacePlayerOnRoad(dt);
        }

        private void AdvanceBoarding(float dt)
        {
            float progress = Mathf.Clamp01(phaseTime / boardingDuration);
            float previous = Mathf.Clamp01((phaseTime - dt) / boardingDuration);
            float axle = Mathf.Lerp(firstAxle, loadedAxle, Mathf.SmoothStep(0f, 1f, progress));
            float priorAxle = Mathf.Lerp(firstAxle, loadedAxle, Mathf.SmoothStep(0f, 1f, previous));
            float advance = axle - priorAxle;
            truckDistance += cruiseSpeed * dt - advance;
            PlaceTruck(loadingLane, dt);
            Pose pose = fit.Evaluate(truck, axle);
            carDistance = truckDistance + pose.position.z;
            VoxelTrackPose track = road.Evaluate(carDistance);
            float settle = Mathf.SmoothStep(0f, 1f, phaseTime / .35f);
            Vector3 position = Vector3.Lerp(RoadPosition(track), truck.transform.TransformPoint(pose.position), settle);
            Quaternion rotation = Quaternion.Slerp(track.rotation, truck.transform.rotation * pose.rotation, settle);
            player.SetCarrierPose(carDistance, cruiseSpeed, position, rotation, loadingLane);
            float onRamp = Mathf.InverseLerp(truck.RampTip - .8f, truck.RampTip, axle);
            fit.RotateWheels(Mathf.Lerp(cruiseSpeed * dt, advance, onRamp));
            fit.UpdatePlough(truck, pose, Mathf.Min(loadedAxle, axle + .5f), dt);
            if (progress < 1f) return;
            fit.RestorePlough();
            truck.SetRampOpen(false);
            Enter(Phase.Closing);
        }

        private void PlacePlayerOnRoad(float dt)
        {
            VoxelTrackPose track = road.Evaluate(carDistance);
            player.SetCarrierPose(carDistance, cruiseSpeed, RoadPosition(track), track.rotation, lane);
            fit.RotateWheels(cruiseSpeed * dt);
        }

        private Vector3 RoadPosition(VoxelTrackPose track) =>
            track.position + track.right * (lane - fit.Bounds.center.x) - Vector3.up * fit.WheelGround;

        private void PlacePlayerInBay(float axle, float speed)
        {
            Pose pose = fit.Evaluate(truck, axle);
            carDistance = truckDistance + pose.position.z;
            player.SetCarrierPose(carDistance, speed, truck.transform.TransformPoint(pose.position),
                truck.transform.rotation * pose.rotation, loadingLane);
        }

        private void PlaceTruck(float offset, float dt, float steering = 0f)
        {
            VoxelTrackPose pose = road.Evaluate(truckDistance);
            truck.transform.SetPositionAndRotation(pose.position + pose.right * offset,
                pose.rotation * Quaternion.Euler(0f, steering, 0f));
            truck.AdvanceTravel(dt > 0f ? truckDistance - lastTruckDistance : 0f);
            lastTruckDistance = truckDistance;
        }

        private void ReleaseCamera()
        {
            cameraDistance = truckDistance;
            road.SetFinishSceneryDistance(cameraDistance);
            departureSpeed = cruiseSpeed;
            Enter(Phase.Leaving);
        }

        private void AdvanceDeparture(float dt)
        {
            float cameraSpeed = cruiseSpeed;
            if (truck != null)
            {
                // Leave at the available traffic speed if we catch a civilian
                // farther down the road. The moving camera never catches the truck.
                departureSpeed = Mathf.Min(departureSpeed + dt * 8f, DepartureSpeedLimit());
                float separationSpeed = Mathf.SmoothStep(0f, 4f, phaseTime / 1.2f);
                cameraSpeed = Mathf.Clamp(departureSpeed - separationSpeed, 0f, cruiseSpeed);
            }
            cameraDistance += cameraSpeed * dt;
            road.SetFinishSceneryDistance(cameraDistance);
            if (truck == null) return;
            truckDistance += departureSpeed * dt;
            PlaceTruck(loadingLane, dt);
            PlacePlayerInBay(loadedAxle, departureSpeed);
            // Keep the actual car available to the garage's damage capture.
            if (truckDistance - cameraDistance > 1100f)
            {
                player.gameObject.SetActive(false);
                Object.Destroy(truck.gameObject);
                truck = null;
            }
        }

        private float DepartureSpeedLimit()
        {
            float speed = cruiseSpeed + 18f;
            foreach (var vehicle in traffic)
            {
                if (vehicle == null || !vehicle.isActiveAndEnabled || !vehicle.IsDrivingTraffic ||
                    Mathf.Abs(vehicle.LaneOffset - loadingLane) >= laneWidth * .5f + vehicle.TrafficHalfWidth)
                    continue;
                float distance = vehicle.TrackDistance - truckDistance;
                if (distance <= 0f) continue;
                float gap = Mathf.Max(0f, distance - vehicle.TrafficHalfLength - 9f);
                float leadSpeed = vehicle.TravelsWithPlayer ? vehicle.TravelSpeed : -vehicle.TravelSpeed;
                speed = Mathf.Min(speed, Mathf.Max(0f, leadSpeed + gap / 1.25f));
            }
            return speed;
        }

        internal void ApplyCamera(Transform camera)
        {
            float pan = Mathf.SmoothStep(0f, 1f, panTime / CameraPanTime);
            float rampBlend = IsLoaded ? 1f : phase == Phase.Closing
                ? Mathf.SmoothStep(0f, 1f, phaseTime / RampCloseTime) : 0f;
            VoxelTrackPose cameraTrack = road.Evaluate(IsLoaded ? cameraDistance : carDistance);
            Quaternion heading = cameraTrack.rotation;
            Vector3 focus = player.transform.TransformPoint(fit.Bounds.center);
            if (!capturedCamera)
            {
                Quaternion local = Quaternion.Inverse(heading);
                panOffset = local * (camera.position - focus);
                float depth = Vector3.Dot(focus - camera.position, camera.forward);
                panAimOffset = local * (camera.position + camera.forward * depth - focus);
                view = camera.GetComponent<Camera>();
                panFieldOfView = view != null ? view.fieldOfView : VoxelCameraFollow.RaceFieldOfView;
                if (camera.TryGetComponent<VoxelMobilePostProcessing>(out var postProcessing))
                    postProcessing.SetDebriefFocus(false, 0f);
                capturedCamera = true;
            }

            // Keep the car as the pivot through the pass and boarding. Hand the shot
            // to the rear of the truck only after the car is inside the bay.
            if (rampBlend > 0f)
            {
                VoxelTrackPose track = IsLoaded ? cameraTrack : road.Evaluate(truckDistance);
                var rampOffset = new Vector3(0f, prefab.DeckHeight + .7f, prefab.BayRear);
                Vector3 ramp = IsLoaded
                    ? track.position + track.right * loadingLane + track.rotation * rampOffset
                    : truck.transform.TransformPoint(rampOffset);
                focus = IsLoaded ? ramp : Vector3.Lerp(focus, ramp, rampBlend);
                heading = Quaternion.Slerp(heading, track.rotation, rampBlend);
            }
            Vector3 eye = focus + heading * VoxelCameraFollow.InterpolateOrbitOffset(panOffset, VoxelCarrierCar.RearLeftEye, pan);
            float framing = Mathf.SmoothStep(0f, 1f, panTime / .42f);
            float fieldOfView = Mathf.Lerp(panFieldOfView, VoxelCameraFollow.RaceFieldOfView, pan);
            float aspect = view != null ? view.aspect : 16f / 9f;
            if (view != null && view.fieldOfView != fieldOfView) view.fieldOfView = fieldOfView;
            Vector3 toSubject = focus - eye;
            float halfWidth = toSubject.magnitude * Mathf.Tan(fieldOfView * Mathf.Deg2Rad * .5f) * aspect;
            float screenOffset = VoxelPostRaceContinue.SubjectViewportX(aspect) * 2f - 1f;
            Vector3 cameraRight = Vector3.Cross(Vector3.up, toSubject).normalized;
            Vector3 aim = focus + Vector3.Lerp(heading * panAimOffset, -cameraRight * (halfWidth * screenOffset), framing);
            camera.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
            if (IsLoaded && !releasedSky)
            {
                releasedSky = true;
                var mountains = Object.FindFirstObjectByType<VoxelHorizonMountains>();
                if (mountains != null) mountains.target = camera;
                foreach (var sun in Object.FindObjectsByType<VoxelHorizonSun>(FindObjectsSortMode.None))
                    sun.target = camera;
            }
        }
    }
}
