using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Moves the actual race car from the carrier to the road before handing it to the countdown.</summary>
    [DefaultExecutionOrder(-150)]
    public sealed class VoxelCarrierIntro : MonoBehaviour
    {
        private const float ConvoySpeed = 26f;
        private const float OpeningRate = 1.4f;
        private const float CloseDuration = .9f;
        private const float MergeDuration = .65f;
        private const float BrakeDuration = .42f;
        private const float DepartureDuration = 2.95f;

        private VoxelCarrierTruck truck;
        private VoxelCarController player;
        private VoxelStartCountdown countdown;
        private EndlessVoxelRoad road;
        private VoxelCarrierCar fit;
        private float speed, truckDistance, truckLane, laneWidth;
        private float rearAxle, reverseFrom, reverseTo, unloadTime, unloadDuration, departureTime;
        private float carDistance, sideFraming = 1f;
        private bool startRequested, countdownStarted, grounded;

        internal static VoxelCarrierIntro TryCreate(Transform owner, VoxelCarrierTruck prefab,
            VoxelCarController player, EndlessVoxelRoad road, VoxelStartCountdown countdown)
        {
            if (prefab == null || road.laneCount < 2)
                return null;
            VoxelCarrierCar fit = VoxelCarrierCar.TryMeasure(player, prefab);
            if (fit == null)
                return null;

            var intro = owner.gameObject.AddComponent<VoxelCarrierIntro>();
            intro.truck = Instantiate(prefab, owner);
            intro.truck.UseManualRampControl();
            intro.player = player;
            intro.road = road;
            intro.countdown = countdown;
            countdown.PrepareRollingOpening();
            intro.fit = fit;
            intro.speed = Mathf.Min(ConvoySpeed, player.EffectiveTopSpeed);
            player.PrepareCarrierLane();
            intro.truckLane = player.CurrentLaneOffset;
            intro.laneWidth = road.roadWidth / road.laneCount;
            intro.reverseFrom = fit.AxleBehindNose(intro.truck.BayFront - .13f);
            intro.reverseTo = fit.AxleBehindNose(intro.truck.RampTip - .24f);
            intro.rearAxle = intro.reverseFrom;
            intro.unloadDuration = (intro.reverseFrom - intro.reverseTo) / 3.3f;
            intro.truckDistance = player.TrackDistance - fit.Evaluate(intro.truck, intro.rearAxle).position.z;
            intro.PlaceTruck(0f);
            intro.PlaceCarOnRamp(0f);
            intro.truck.SetRampOpen(true);
            return intro;
        }

        public void RequestStart() => startRequested = true;

        private void Update()
        {
            if (VoxelLoadingScreen.IsLoading || VoxelPauseMenu.IsPaused)
                return;

            float dt = Time.deltaTime;
            sideFraming = Mathf.MoveTowards(sideFraming, startRequested ? 0f : 1f, dt * 2.5f);
            if (!grounded)
            {
                if (truck.IsOpen)
                {
                    unloadTime += dt;
                    rearAxle = unloadTime >= unloadDuration ? reverseTo :
                        Mathf.Lerp(reverseFrom, reverseTo, Mathf.SmoothStep(0f, 1f, unloadTime / unloadDuration));
                }
                else
                {
                    truck.AdvanceRamp(dt * OpeningRate);
                }
                MoveTruck(0f, speed * dt);
                PlaceCarOnRamp(dt);
                if (rearAxle > reverseTo)
                    return;

                grounded = true;
                fit.RestorePlough();
                truck.SetRampOpen(false);
            }
            else
            {
                carDistance += speed * dt;
                PlaceCarOnRoad(dt);
                AdvanceDeparture(dt);
            }

            if (startRequested && !countdownStarted)
            {
                countdownStarted = true;
                countdown.BeginRollingCountdown();
                road.PlanOpeningTurns(carDistance + speed * VoxelStartCountdown.Duration,
                    carDistance + speed * (VoxelStartCountdown.Duration - 1f));
            }
            if (countdownStarted)
                road.BlendOpeningTurns(countdown.CountdownElapsed / (VoxelStartCountdown.Duration - 1f));
            if (!countdown.IsComplete)
                return;

            player.ReleaseCarrierPose();
            player.SetDrivingEnabled(true);
            if (truck != null)
                Destroy(truck.gameObject);
            Destroy(this);
        }

        private void PlaceCarOnRamp(float dt)
        {
            Pose local = fit.Evaluate(truck, rearAxle);
            carDistance = truckDistance + local.position.z;
            Transform carrier = truck.transform;
            player.SetCarrierPose(carDistance, speed, carrier.TransformPoint(local.position), carrier.rotation * local.rotation);
            if (!truck.IsOpen)
                return;

            float duration = Mathf.Clamp01(unloadTime / unloadDuration);
            float reverseSpeed = (reverseTo - reverseFrom) * 6f * duration * (1f - duration) / unloadDuration;
            float onRoad = Mathf.InverseLerp(truck.RampTip - .45f, reverseTo, rearAxle);
            fit.RotateWheels(Mathf.Lerp(reverseSpeed, speed, onRoad) * dt);
            fit.UpdatePlough(truck, local, Mathf.Max(reverseTo, rearAxle - .5f), dt);
        }

        private void PlaceCarOnRoad(float dt)
        {
            VoxelTrackPose pose = road.Evaluate(carDistance);
            Vector3 position = pose.position + pose.right * (truckLane - fit.Bounds.center.x) - Vector3.up * fit.WheelGround;
            player.SetCarrierPose(carDistance, speed, position, pose.rotation);
            fit.RotateWheels(speed * dt);
        }

        private void AdvanceDeparture(float dt)
        {
            if (truck == null)
                return;

            departureTime += dt;
            if (!truck.IsClosed)
                truck.AdvanceRamp(dt * (3f / CloseDuration));
            float merge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((departureTime - CloseDuration) / MergeDuration));
            float braking = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((departureTime - CloseDuration - MergeDuration) / BrakeDuration));
            MoveTruck(merge, Mathf.Lerp(speed, 1.5f, braking) * dt);
            if (departureTime >= DepartureDuration)
            {
                Destroy(truck.gameObject);
                truck = null;
            }
        }

        private void MoveTruck(float merge, float distance)
        {
            truckDistance += distance;
            PlaceTruck(merge);
            truck.AdvanceTravel(distance);
        }

        private void PlaceTruck(float merge)
        {
            VoxelTrackPose pose = road.Evaluate(truckDistance);
            float steer = 7f * Mathf.Sin(merge * Mathf.PI);
            truck.transform.SetPositionAndRotation(pose.position + pose.right * (truckLane + laneWidth * merge),
                pose.rotation * Quaternion.Euler(0f, steer, 0f));
        }

        internal void BlendCamera(Transform camera)
        {
            float blend = countdownStarted ? Mathf.SmoothStep(0f, 1f, countdown.CountdownElapsed / 2f) : 0f;
            if (blend >= 1f)
                return;
            VoxelTrackPose roadPose = road.Evaluate(carDistance);
            Vector3 focus = player.transform.position + Vector3.up * .85f;
            if (!grounded && truck != null)
            {
                Vector3 door = truck.transform.TransformPoint(new Vector3(0f, truck.DeckHeight + .7f, truck.BayRear));
                focus = Vector3.Lerp(door, focus, Mathf.SmoothStep(0f, 1f, unloadTime / unloadDuration));
            }
            Vector3 eye = focus + roadPose.rotation * VoxelCarrierCar.RearLeftEye;
            Vector3 aim = focus - roadPose.right * (2.15f * sideFraming);
            Quaternion view = Quaternion.LookRotation(aim - eye);
            camera.SetPositionAndRotation(Vector3.Lerp(eye, camera.position, blend),
                Quaternion.Slerp(view, camera.rotation, blend));
        }

    }
}
