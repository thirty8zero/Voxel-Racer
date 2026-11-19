using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Follows wheel speed and load without restarting loops as the car changes speed.</summary>
    [DisallowMultipleComponent]
    public sealed class VoxelPlayerEngineAudio : MonoBehaviour
    {
        private const float IgnitionDelay = .2f;
        private const float StartupToIdle = 1f;
        internal const float IgnitionCueToIdle = IgnitionDelay + StartupToIdle;
        private const float IgnitionCrossfade = .16f;
        private const float TouchdownIdleHold = .5f;
        private const float TouchdownIdleFade = .5f;
        private const float TouchdownRoadFade = .06f;
        private const float TouchdownRevDuration = 1f;
        private const float TouchdownStartSpeed = .08f;
        private const float SpeedResponse = .13f;
        private const float LoadResponse = .16f;
        private const float ShutdownDuration = .22f;
        private const float ShiftAttack = .035f;
        private const float UpshiftHold = .060f;
        private const float UpshiftDuration = .35f;
        private const float DownshiftHold = .025f;
        private const float DownshiftDuration = .205f;

        // Fractions of normal top speed. The gap between thresholds keeps a gear engaged
        // through small changes in speed near a shift point.
        private static readonly float[] UpshiftSpeeds = { .28f, .52f, .78f };
        private static readonly float[] DownshiftSpeeds = { .20f, .41f, .66f };

        // Match neighbouring driving recordings to the same four-stroke cycle rate during the crossfade.
        // The idle recording stays at its original pitch.
        private static readonly float[] RecordedRates = { 15.6f, 22.13f, 36.47f, 45.94f, 48.7f };
        private static readonly float[] SpeedPoints = { .015f, .24f, .62f, 1f, 1.22f };
        private const float PeakRate = 53f;

        private enum Stage { Off, Ignition, Road }
        private Stage stage;
        private VoxelCarController player;
        private VoxelPlayerEngineAudioSettings settings;
        private AudioSource starter;
        private (AudioSource on, float onGain, AudioSource off, float offGain)[] bands;
        private double idleAt;
        private double? touchdownAt;
        private float speed, laneLift, load = .7f, output = 1f;
        private bool shuttingDown;
        private int gear;
        private float shiftElapsed = UpshiftDuration, shiftPitch, shiftStart, shiftPeak;

        internal bool IdleCueReached => stage != Stage.Off && AudioSettings.dspTime >= idleAt;

        internal static VoxelPlayerEngineAudio Attach(VoxelCarController player)
        {
            var settings = Resources.Load<VoxelPlayerEngineAudioSettings>("PlayerEngineAudioSettings");
            if (settings == null || settings.idle == null)
                return null;
            if (player.TryGetComponent(out VoxelPlayerEngineAudio existing))
                return existing;

            var engine = player.gameObject.AddComponent<VoxelPlayerEngineAudio>();
            engine.player = player;
            engine.settings = settings;
            engine.starter = engine.CreateSource(settings.startup, loop: false);
            engine.bands = new[]
            {
                (engine.CreateSource(settings.idle), .76f, (AudioSource)null, 0f),
                (engine.CreateSource(settings.lowOn), .74f, engine.CreateSource(settings.lowOff), .60f),
                (engine.CreateSource(settings.mediumOn), .76f, engine.CreateSource(settings.mediumOff), .49f),
                (engine.CreateSource(settings.highOn), .69f, engine.CreateSource(settings.highOff), .46f),
                (engine.CreateSource(settings.maxRpm), .94f, (AudioSource)null, 0f)
            };
            return engine;
        }

        internal void StartIgnition()
        {
            if (stage != Stage.Off)
                return;
            stage = Stage.Ignition;
            double startupAt = AudioSettings.dspTime + IgnitionDelay;
            idleAt = startupAt + StartupToIdle;
            if (starter != null)
            {
                starter.volume = settings.volume;
                starter.PlayScheduled(startupAt);
            }
            AudioSource idle = bands[0].on;
            idle.volume = 0f;
            idle.pitch = 1f;
            idle.PlayScheduled(idleAt);
        }

        internal void BeginRoadSound(bool rearTyresTouchedDown = false, bool skipIgnition = false)
        {
            if (stage == Stage.Road)
                return;
            if (skipIgnition)
            {
                // A covered unload skip starts the driving blend directly instead
                // of playing a delayed ignition after the car is already on-road.
                starter?.Stop();
                bands[0].on.Stop();
                idleAt = AudioSettings.dspTime - IgnitionCrossfade;
            }
            else StartIgnition();
            stage = Stage.Road;
            if (rearTyresTouchedDown)
            {
                touchdownAt = AudioSettings.dspTime;
                // Keep the gear state synced to convoy speed while the audible revs build.
                speed = Mathf.Clamp01(player.EngineDriveSpeed / Mathf.Max(1f, player.EffectiveTopSpeed));
            }
            for (int i = 1; i < bands.Length; i++)
            {
                bands[i].on?.Play();
                bands[i].off?.Play();
            }
        }

        internal void BeginShutdown() => shuttingDown = true;

        private AudioSource CreateSource(AudioClip clip, bool loop = true)
        {
            if (clip == null)
                return null;
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = 0f;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 9f;
            source.maxDistance = 65f;
            return source;
        }

        private void LateUpdate()
        {
            if (stage == Stage.Off || AudioListener.pause)
                return;

            float dt = Time.unscaledDeltaTime;
            if (shuttingDown || player.IsDestroyed)
            {
                output = Mathf.MoveTowards(output, 0f, dt / ShutdownDuration);
                if (output <= 0f)
                {
                    StopSources();
                    return;
                }
            }

            double now = AudioSettings.dspTime;
            float ignitionMix = Mathf.SmoothStep(0f, 1f, (float)(now - idleAt) / IgnitionCrossfade);
            float volume = settings.volume * output;
            if (starter != null)
                starter.volume = volume * (1f - ignitionMix);

            if (stage != Stage.Road)
            {
                bands[0].on.volume = volume * ignitionMix * bands[0].onGain;
                return;
            }

            float driveSpeed = player.EngineDriveSpeed;
            float topSpeed = Mathf.Max(1f, player.EffectiveTopSpeed);
            float targetSpeed = Mathf.Clamp01(driveSpeed / topSpeed);
            if (driveSpeed > topSpeed)
            {
                float boostRange = Mathf.Max(1f, player.BoostController?.Tuning?.boostSpeed ?? topSpeed * .35f);
                targetSpeed = Mathf.Lerp(1f, SpeedPoints[^1], (driveSpeed - topSpeed) / boostRange);
            }
            bool coasting = player.IsBraking || player.BrakeLightsActive || targetSpeed + .015f < speed;
            speed = Mathf.Lerp(speed, targetSpeed, 1f - Mathf.Exp(-dt / SpeedResponse));
            laneLift = Mathf.Lerp(laneLift, player.IsChangingLane ? 1f : 0f, 1f - Mathf.Exp(-dt / .085f));
            UpdateGearShift(dt);

            // A small change in load keeps cruising from sitting on the same blend indefinitely.
            float cruiseAmount = Mathf.Clamp01(1f - Mathf.Abs(speed - 1f) * 5f);
            float cruiseVariation = (Mathf.PerlinNoise(Time.time * .6f, 4.7f) - .5f) * cruiseAmount;
            float cruiseLoad = Mathf.Lerp(1f, .72f, Mathf.InverseLerp(.65f, 1f, targetSpeed));
            float targetLoad = coasting ? 0f : Mathf.Clamp01(cruiseLoad - .22f * laneLift + .24f * cruiseVariation);
            load = Mathf.Lerp(load, targetLoad, 1f - Mathf.Exp(-dt / LoadResponse));

            float pitchScale = (1f - .035f * laneLift + .016f * cruiseVariation) * (1f + shiftPitch);
            float shiftLoad = Mathf.Clamp01(load + 1.8f * shiftPitch);
            float shutdownPitch = shuttingDown ? Mathf.Lerp(.8f, 1f, output) : 1f;
            MixDrivingLoops(volume, ignitionMix, pitchScale, shiftLoad, shutdownPitch);
        }

        private void UpdateGearShift(float dt)
        {
            bool canShift = player.UsesRoadGears && !shuttingDown;
            int next = canShift ? gear : 0;
            while (next < UpshiftSpeeds.Length && speed >= UpshiftSpeeds[next])
                next++;
            while (next > 0 && speed <= DownshiftSpeeds[next - 1])
                next--;

            if (!canShift)
            {
                // Match the current speed silently, for example during the rolling launch.
                gear = next;
                shiftElapsed = UpshiftDuration;
                shiftPitch = 0f;
                return;
            }

            if (next != gear)
            {
                shiftStart = shiftPitch;
                shiftPeak = next > gear ? -.24f : .30f;
                shiftElapsed = 0f;
                gear = next;
            }

            // Shorter blips leave room between consecutive downshifts under hard braking.
            bool upshift = shiftPeak < 0f;
            float duration = upshift ? UpshiftDuration : DownshiftDuration;
            if (shiftElapsed >= duration)
                return;
            shiftElapsed = Mathf.Min(duration, shiftElapsed + dt);
            float releaseAt = ShiftAttack + (upshift ? UpshiftHold : DownshiftHold);
            shiftPitch = shiftElapsed < ShiftAttack
                ? Mathf.SmoothStep(shiftStart, shiftPeak, shiftElapsed / ShiftAttack)
                : Mathf.SmoothStep(shiftPeak, 0f, Mathf.InverseLerp(releaseAt, duration, shiftElapsed));
        }

        private void MixDrivingLoops(float volume, float ignitionMix, float pitchScale, float mixedLoad, float shutdownPitch)
        {
            float touchdownIdle = 0f;
            float roadGain = 1f;
            float mixSpeed = speed;
            if (touchdownAt.HasValue)
            {
                float elapsed = (float)(AudioSettings.dspTime - touchdownAt.Value);
                roadGain = Mathf.SmoothStep(0f, 1f, elapsed / TouchdownRoadFade);
                touchdownIdle = Mathf.SmoothStep(1f, 0f, (elapsed - TouchdownIdleHold) / TouchdownIdleFade);
                mixSpeed = Mathf.SmoothStep(Mathf.Min(TouchdownStartSpeed, speed), speed, elapsed / TouchdownRevDuration);
                if (elapsed >= Mathf.Max(TouchdownIdleHold + TouchdownIdleFade, TouchdownRevDuration))
                    touchdownAt = null;
            }

            int upper = 1;
            while (upper < SpeedPoints.Length - 1 && mixSpeed > SpeedPoints[upper])
                upper++;

            int lower = upper - 1;
            float between = Mathf.InverseLerp(SpeedPoints[lower], SpeedPoints[upper], mixSpeed);
            float blend = Mathf.SmoothStep(0f, 1f, between);
            float upperRate = upper == SpeedPoints.Length - 1 ? PeakRate : RecordedRates[upper];
            float rate = Mathf.Lerp(RecordedRates[lower], upperRate, between);
            for (int i = 0; i < bands.Length; i++)
            {
                var band = bands[i];
                float weight = i == lower ? Mathf.Sqrt(1f - blend) : i == upper ? Mathf.Sqrt(blend) : 0f;
                weight = i == 0 ? Mathf.Max(weight, touchdownIdle) * ignitionMix : weight * roadGain;
                float pitch = i == 0 ? shutdownPitch :
                    Mathf.Clamp(rate / RecordedRates[i] * pitchScale * shutdownPitch, .55f, 1.75f);
                if (band.on != null)
                {
                    band.on.pitch = pitch;
                    band.on.volume = volume * weight * band.onGain * (band.off != null ? mixedLoad : 1f);
                }
                if (band.off != null)
                {
                    band.off.pitch = pitch;
                    band.off.volume = volume * weight * band.offGain * (band.on != null ? 1f - mixedLoad : 1f);
                }
            }
        }

        private void OnDisable() => StopSources();

        private void StopSources()
        {
            if (stage == Stage.Off)
                return;
            stage = Stage.Off;
            starter?.Stop();
            foreach (var band in bands)
            {
                band.on?.Stop();
                band.off?.Stop();
            }
            touchdownAt = null;
            speed = laneLift = 0f;
            load = .7f;
            output = 1f;
            shuttingDown = false;
            gear = 0;
            shiftElapsed = UpshiftDuration;
            shiftPitch = 0f;
        }
    }
}
