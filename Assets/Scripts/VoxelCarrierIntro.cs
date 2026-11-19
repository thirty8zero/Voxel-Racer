using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Moves the actual race car from the carrier to the road before handing it to the countdown.</summary>
    [DefaultExecutionOrder(-150)]
    public sealed class VoxelCarrierIntro : MonoBehaviour
    {
        private const float OpeningRate = 1.4f;
        private const float BrakeLightFlashLead = .1f;
        private const float BrakeLightFlashDuration = .75f;
        private const float CloseDuration = .9f;
        private const float MergeDuration = .65f;
        private const float BrakeDuration = .42f;
        private const float DepartureDuration = 2.95f;
        private const float RoadFramingDuration = .65f;
        private const float RoadFramingMargin = .10f;
        private const float MaxCameraPushIn = 1.8f;
        private const float SkipFadeOutDuration = .15f;
        private const float SkipFadeInDuration = .25f;
        private enum SkipPhase { None, FadingOut, Covered, PreparingRoad, FadingIn, Complete }
        private SkipPhase skipPhase;
        private float skipFadeElapsed;
        private bool skipRoadReady;
        internal bool IsUnloading => !grounded;

        private VoxelCarrierTruck truck;
        private VoxelCarController player;
        private VoxelStartCountdown countdown;
        private EndlessVoxelRoad road;
        private VoxelDistantScenery openingScenery;
        private VoxelCarrierCar fit;
        private Camera viewCamera;
        private float speed, truckDistance, truckLane, laneWidth;
        private float rearAxle, reverseFrom, reverseTo, unloadTime, unloadDuration, departureTime;
        private float carDistance, sideFraming = 1f;
        private bool startRequested, countdownStarted, grounded;
        private bool exhaustPuffed, brakeLightFlashed, rearTyresOnRoad;
        private float brakeLightFlashAt;
        private float brakeLightFlashRemaining;
        private bool[] tyrePuffed;
        private ParticleSystem smokePrefab;
        private Material smokeMaterial;

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
            intro.openingScenery = road.GetComponent<VoxelDistantScenery>();
            intro.countdown = countdown;
            countdown.PrepareRollingOpening();
            intro.fit = fit;
            intro.tyrePuffed = new bool[fit.TyreCount];
            intro.smokePrefab = Resources.Load<ParticleSystem>("Effects/WhiteVoxelMissCloud");
            intro.smokeMaterial = Resources.Load<Material>("Effects/VoxelExplosionSmokeParticle");
            intro.speed = VoxelCarrierCar.ConvoySpeedFor(player);
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

        public void RequestStart()
        {
            if (startRequested) return;
            startRequested = true;
            if (grounded) return;
            skipPhase = SkipPhase.FadingOut;
            countdown.SetCarrierSkipFade(0f);
            BeginRollingCountdown();
        }

        private void BeginRollingCountdown()
        {
            countdownStarted = true;
            countdown.BeginRollingCountdown();
            road.PlanOpeningTurns(carDistance + speed * VoxelStartCountdown.Duration,
                carDistance + speed * (VoxelStartCountdown.Duration - 1f));
        }

        private void Update()
        {
            if (VoxelLoadingScreen.IsLoading || VoxelPauseMenu.IsPaused)
                return;
            if (player == null || player.IsDestroyed)
            {
                countdown.HideForPlayerDeath();
                if (truck != null) Destroy(truck.gameObject);
                Destroy(this);
                return;
            }

            float dt = Time.deltaTime;
            if (brakeLightFlashRemaining > 0f)
            {
                brakeLightFlashRemaining = Mathf.Max(0f, brakeLightFlashRemaining - dt);
                player.SetCarrierBrakeLights(brakeLightFlashRemaining > 0f);
            }
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
                if (!brakeLightFlashed && truck.IsBayRevealWithin(BrakeLightFlashLead * OpeningRate))
                {
                    brakeLightFlashed = true;
                    brakeLightFlashAt = Time.time;
                    brakeLightFlashRemaining = BrakeLightFlashDuration;
                    player.SetCarrierBrakeLights(true);
                    countdown.EngineAudio?.StartIgnition();
                }
                if (!exhaustPuffed && brakeLightFlashed && truck.HasRevealedBay &&
                    (countdown.EngineAudio?.IdleCueReached ??
                     Time.time - brakeLightFlashAt >= VoxelPlayerEngineAudio.IgnitionCueToIdle))
                {
                    exhaustPuffed = true;
                    PuffExhaust();
                }
                if (rearAxle <= reverseTo)
                {
                    grounded = true;
                    fit.RestorePlough();
                    truck.SetRampOpen(false);
                }
            }
            else
            {
                carDistance += speed * dt;
                PlaceCarOnRoad(dt);
                AdvanceDeparture(dt);
            }

            AdvanceSkipFade(dt);
            if (startRequested && !countdownStarted && grounded)
                BeginRollingCountdown();
            if (countdownStarted && skipPhase == SkipPhase.None)
                road.BlendOpeningTurns(countdown.CountdownElapsed / (VoxelStartCountdown.Duration - 1f));
            if (!countdown.IsComplete)
                return;

            player.ReleaseCarrierPose();
            player.SetDrivingEnabled(true);
            if (truck != null)
                Destroy(truck.gameObject);
            Destroy(this);
        }

        private void AdvanceSkipFade(float dt)
        {
            if (dt <= 0f) return;
            if (skipPhase == SkipPhase.FadingOut)
            {
                skipFadeElapsed += dt;
                countdown.SetCarrierSkipFade(Mathf.SmoothStep(0f, 1f, skipFadeElapsed / SkipFadeOutDuration));
                // Render one fully covered frame before changing the car or camera.
                if (skipFadeElapsed >= SkipFadeOutDuration) skipPhase = SkipPhase.Covered;
            }
            else if (skipPhase == SkipPhase.Covered)
            {
                grounded = skipRoadReady = rearTyresOnRoad = true;
                fit.RestorePlough();
                brakeLightFlashRemaining = 0f;
                player.SetCarrierBrakeLights(false);
                // The ordinary countdown morph is visible from the chase view.
                // Finish road geometry/colliders only after a fully black frame.
                road.BlendOpeningTurns(1f);
                // Keep the current road distance: skipping presentation must not
                // advance mission progress or jump the road underneath the car.
                PlaceCarOnRoad(0f);
                if (truck != null)
                {
                    truck.gameObject.SetActive(false);
                    Destroy(truck.gameObject);
                    truck = null;
                }
                countdown.EngineAudio?.BeginRoadSound(rearTyresTouchedDown: true, skipIgnition: true);
                skipPhase = SkipPhase.PreparingRoad;
            }
            else if (skipPhase == SkipPhase.PreparingRoad)
            {
                // Scenery prepares the final route in bounded LateUpdate slices.
                // Keep its cache swap and the horizon refresh behind the overlay,
                // without draining expensive tile work synchronously here.
                if (openingScenery != null && openingScenery.isActiveAndEnabled &&
                    !openingScenery.IsOpeningSceneryReady) return;
                skipFadeElapsed = 0f;
                skipPhase = SkipPhase.FadingIn;
            }
            else if (skipPhase == SkipPhase.FadingIn)
            {
                skipFadeElapsed += dt;
                countdown.SetCarrierSkipFade(1f - Mathf.SmoothStep(0f, 1f, skipFadeElapsed / SkipFadeInDuration));
                if (skipFadeElapsed < SkipFadeInDuration) return;
                countdown.EndCarrierSkipFade();
                skipPhase = SkipPhase.Complete;
            }
        }

        private void PlaceCarOnRamp(float dt)
        {
            Pose local = fit.Evaluate(truck, rearAxle);
            carDistance = truckDistance + local.position.z;
            Transform carrier = truck.transform;
            player.SetCarrierPose(carDistance, speed, carrier.TransformPoint(local.position), carrier.rotation * local.rotation);
            if (!truck.IsOpen)
                return;

            PuffTouchdowns(carrier, local);
            if (!rearTyresOnRoad && rearAxle <= truck.RampTip)
            {
                rearTyresOnRoad = true;
                countdown.EngineAudio?.BeginRoadSound(rearTyresTouchedDown: true);
            }
            float duration = Mathf.Clamp01(unloadTime / unloadDuration);
            float reverseSpeed = (reverseTo - reverseFrom) * 6f * duration * (1f - duration) / unloadDuration;
            float onRoad = Mathf.InverseLerp(truck.RampTip - .45f, reverseTo, rearAxle);
            fit.RotateWheels(Mathf.Lerp(reverseSpeed, speed, onRoad) * dt);
            fit.UpdatePlough(truck, local, Mathf.Max(reverseTo, rearAxle - .5f), dt);
        }

        private void PuffExhaust()
        {
            if (smokePrefab == null)
                return;

            Transform car = player.transform;
            var outlets = player.GetComponentsInChildren<VoxelEngineExhaustOutlet>();
            if (outlets.Length > 0)
            {
                foreach (var outlet in outlets)
                    Puff(outlet.transform.position, outlet.transform.forward + car.up * .22f, exhaust: true);
                return;
            }

            bool foundTailpipe = false;
            Vector3 rear = -car.forward;
            Vector3 axis = new(Mathf.Abs(rear.x), Mathf.Abs(rear.y), Mathf.Abs(rear.z));
            foreach (var part in player.GetComponentsInChildren<MeshRenderer>())
            {
                if (!part.enabled || !part.name.EndsWith("Exhaust", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                Bounds bounds = part.bounds;
                Puff(bounds.center + rear * (Vector3.Dot(bounds.extents, axis) + .03f),
                    rear + car.up * .22f, exhaust: true);
                foundTailpipe = true;
            }
            if (!foundTailpipe)
                Puff(car.TransformPoint(new Vector3(fit.Bounds.center.x + fit.Bounds.extents.x * .6f,
                    fit.WheelGround + .30f, fit.Bounds.min.z - .05f)), rear + car.up * .22f, exhaust: true);
        }

        private void OnDisable()
        {
            if (countdown != null && skipPhase != SkipPhase.None)
                countdown.EndCarrierSkipFade();
            if (brakeLightFlashRemaining > 0f && player != null)
                player.SetCarrierBrakeLights(false);
            brakeLightFlashRemaining = 0f;
        }

        private void PuffTouchdowns(Transform carrier, Pose pose)
        {
            if (smokePrefab == null)
                return;

            for (int i = 0; i < tyrePuffed.Length; i++)
            {
                if (tyrePuffed[i])
                    continue;
                Vector3 contact = pose.position + pose.rotation * fit.TyreContact(i);
                if (contact.z > truck.RampTip + .005f)
                    continue;

                tyrePuffed[i] = true;
                contact.y = .045f;
                float outward = Mathf.Sign(fit.TyreContact(i).x - fit.Bounds.center.x);
                Puff(carrier.TransformPoint(contact), carrier.up + carrier.right * (.7f * outward) +
                    carrier.forward * .65f, exhaust: false);
            }
        }

        private void Puff(Vector3 position, Vector3 direction, bool exhaust)
        {
            // Exhaust follows the moving car. Tyre smoke is left at the road contact.
            Transform parent = exhaust ? player.transform : transform;
            var smoke = Instantiate(smokePrefab, position, Quaternion.LookRotation(direction), parent);
            smoke.name = exhaust ? "Carrier Exhaust Puff" : "Carrier Tyre Touchdown Puff";
            if (smokeMaterial != null)
                smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMaterial;
            var main = smoke.main;
            main.simulationSpace = exhaust ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.28f, .52f);
            main.startSpeed = exhaust ? new ParticleSystem.MinMaxCurve(.9f, 2.2f) : new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.065f, exhaust ? .16f : .19f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.52f, .54f, .56f), new Color(.78f, .79f, .80f));
            main.gravityModifier = -.12f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = smoke.emission;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)(exhaust ? 9 : 12)));
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = exhaust ? 20f : 40f;
            shape.radius = .045f;
            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .6f),
                new Keyframe(.40f, 1f), new Keyframe(1f, 0f)));
            smoke.Play(false);
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
            if (skipRoadReady) return;
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
            if (grounded && sideFraming > 0f)
            {
                if (viewCamera == null)
                    viewCamera = camera.GetComponent<Camera>();
                float framing = Mathf.SmoothStep(0f, 1f, departureTime / RoadFramingDuration) * sideFraming;
                if (viewCamera != null && framing > 0f)
                    eye = Vector3.Lerp(eye, FrameCarBesidePanel(eye, view), framing);
            }
            camera.SetPositionAndRotation(Vector3.Lerp(eye, camera.position, blend),
                Quaternion.Slerp(view, camera.rotation, blend));
        }

        private Vector3 FrameCarBesidePanel(Vector3 eye, Quaternion view)
        {
            Rect safeArea = Screen.safeArea;
            float panelRight = VoxelMissionBriefing.RacePanelRightViewport(safeArea, Screen.width);
            float screenRight = safeArea.xMax / Screen.width;
            float margin = (screenRight - panelRight) * RoadFramingMargin;
            float halfWidth = Mathf.Tan(viewCamera.fieldOfView * .5f * Mathf.Deg2Rad) * viewCamera.aspect;
            float leftSlope = (2f * (panelRight + margin) - 1f) * halfWidth;
            float rightSlope = (2f * (screenRight - margin) - 1f) * halfWidth;

            Transform car = player.transform;
            Bounds bounds = fit.Bounds;
            Quaternion inverseView = Quaternion.Inverse(view);
            Vector3 origin = inverseView * (car.TransformPoint(bounds.center) - eye);
            Vector3 x = inverseView * car.TransformVector(Vector3.right * bounds.extents.x);
            Vector3 y = inverseView * car.TransformVector(Vector3.up * bounds.extents.y);
            Vector3 z = inverseView * car.TransformVector(Vector3.forward * bounds.extents.z);

            // Each frustum edge limits the camera's lateral position at a given depth.
            // Project the oriented car bounds onto those two edge planes.
            float leftLimit = origin.x - leftSlope * origin.z -
                (Mathf.Abs(x.x - leftSlope * x.z) + Mathf.Abs(y.x - leftSlope * y.z) + Mathf.Abs(z.x - leftSlope * z.z));
            float rightLimit = origin.x - rightSlope * origin.z +
                Mathf.Abs(x.x - rightSlope * x.z) + Mathf.Abs(y.x - rightSlope * y.z) + Mathf.Abs(z.x - rightSlope * z.z);
            float retreat = Mathf.Max(-MaxCameraPushIn, (rightLimit - leftLimit) / (rightSlope - leftSlope));
            float lateral = (leftLimit + rightLimit - (leftSlope + rightSlope) * retreat) * .5f;
            return eye + view * new Vector3(lateral, 0f, -retreat);
        }

    }
}
