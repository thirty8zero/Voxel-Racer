using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Exercises the authored mission through radar traffic, hopper combat, completion and garage.</summary>
    public static class VoxelBichoLocoPlayValidation
    {
        private const string Pending = "VoxelRacer.BichoLocoPlayValidation";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static VoxelGarageUpgradeDockValidation.SavedState saved;
        private static VoxelTrackDefinition track;
        private static VoxelBossEncounter encounter;
        private static VoxelEnemyCar boss;
        private static VoxelBichoLocoRig rig;
        private static VoxelCarController player;
        private static VoxelPauseMenu pause;
        private static float scale, fixedStep, pausedDistance, lampClock, mineClock;
        private static bool audio, background, captured;
        private static UnityEngine.Random.State random;
        private static double phaseStarted;
        private static int phase, trackIndex, walletBeforeCompletion, finalWallet;
        private static string trackBefore, bossBefore, attackBefore;
        private static bool sawBodyWarning;
        private static VoxelBossIntroduction introduction;
        private static float introductionTime, introductionClock;
        private static bool sawIntroduction;

        private static void Check(bool condition, string message) => VoxelBichoLocoValidation.Check(condition, message);
        private static void Equal(float actual, float expected, string message) => VoxelBichoLocoValidation.Equal(actual, expected, message);
        private static void Set(object target, string field, object value) => VoxelBichoLocoValidation.Set(target, field, value);
        private static float Field(object target, string name) => (float)target.GetType().GetField(name, Private).GetValue(target);

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Bosses/Validate El Bicho Loco in Play Mode")]
        public static void Run()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().name == "MainMenu",
                "Start from MainMenu in Edit Mode.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            { phase = 0; captured = false; sawIntroduction = false; phaseStarted = Time.realtimeSinceStartupAsDouble; EditorApplication.update += Tick; }
            if (state == PlayModeStateChange.ExitingPlayMode) Finish(false, "Interrupted validation.");
        }

        private static void Next(int value) { phase = value; phaseStarted = Time.realtimeSinceStartupAsDouble; }
        private static double Elapsed => Time.realtimeSinceStartupAsDouble - phaseStarted;

        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                Check(Elapsed < 90, "El Bicho Loco Play Mode phase timed out: " + phase);
                if (VoxelLoadingScreen.IsLoading) return;
                if (phase == 0)
                {
                    scale = Time.timeScale; fixedStep = Time.fixedDeltaTime; audio = AudioListener.pause;
                    background = Application.runInBackground; random = UnityEngine.Random.state;
                    saved = new VoxelGarageUpgradeDockValidation.SavedState(); captured = true;
                    Application.runInBackground = true; Time.timeScale = 1; AudioListener.pause = false;
                    track = Resources.Load<VoxelTrackDefinition>("Tracks/TrackBoss02");
                    Check(track != null, "New boss mission asset missing.");
                    trackBefore = EditorJsonUtility.ToJson(track); bossBefore = EditorJsonUtility.ToJson(track.boss);
                    attackBefore = EditorJsonUtility.ToJson(track.boss.GetAttack<VoxelBichoLocoTuning>());
                    _ = VoxelTrackProgressState.CurrentTrack;
                    trackIndex = Array.IndexOf(VoxelTrackSequence.Load().tracks, track);
                    Check(trackIndex >= 0, "New mission is absent from campaign.");
                    typeof(VoxelTrackProgressState).GetField("currentTrackIndex", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, trackIndex);
                    VoxelCarRunState.BeginNewRun(VoxelCarSelectionState.GetSelectedOrDefault());
                    Check(VoxelLoadingScreen.LoadScene(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + track.raceSceneName + ".unity")), "Race load failed.");
                    Next(1);
                }
                else if (phase == 1)
                {
                    Check(VoxelRacerBootstrap.ActiveTrack == track, "Wrong authored track loaded.");
                    encounter = Object.FindFirstObjectByType<VoxelBossEncounter>();
                    var spawner = Object.FindFirstObjectByType<VoxelObstacleSpawner>();
                    Check(encounter != null && encounter.CurrentStage == VoxelBossEncounter.Stage.Traffic && encounter.Boss == null,
                        "New level did not start in radar-car traffic phase.");
                    Check(spawner.laneCount == 4 && spawner.laneWidth == 5, "New level is not four lanes.");
                    spawner.ConfigureRadarObjective(encounter, 100); // Runtime probability only; never edits track settings.
                    pause = VoxelPauseMenu.Active;
                    player = (VoxelCarController)typeof(VoxelPauseMenu).GetField("target", Private).GetValue(pause);
                    Set(player, "nextDamageTime", float.PositiveInfinity);
                    Next(2);
                }
                else if (phase == 2)
                {
                    var radar = Object.FindObjectsByType<VoxelObstacleCar>(FindObjectsSortMode.None).FirstOrDefault(c => c.IsEnemyTraffic);
                    if (radar == null) return;
                    Check(encounter.CurrentStage == VoxelBossEncounter.Stage.Traffic && VoxelMissionProgress.Active.IsBossEncounter, "Radar did not own the objective.");
                    radar.TakeProjectileHit(null, radar.CurrentHealth + 1, radar.transform.position, Vector3.forward);
                    Check(encounter.CurrentStage == VoxelBossEncounter.Stage.Clearing && !VoxelMissionProgress.Active.IsComplete,
                        "Radar defeat did not start road clearing.");
                    Time.timeScale = 3; Next(3);
                }
                else if (phase == 3)
                {
                    if (!sawIntroduction && encounter.CurrentStage == VoxelBossEncounter.Stage.Introduction)
                    {
                        introduction = VoxelBossIntroduction.Active;
                        Check(introduction != null && encounter.Boss == null &&
                            Object.FindObjectsByType<VoxelObstacleCar>(FindObjectsSortMode.None).Length == 0,
                            "Poster began before traffic cleared or after boss spawn.");
                        sawIntroduction = true; introductionTime = VoxelMissionProgress.Active.RemainingTime;
                        Time.timeScale = 1; Next(20); return;
                    }
                    if (encounter.CurrentStage != VoxelBossEncounter.Stage.Boss || encounter.Boss == null || encounter.Boss.IsBossEntering) return;
                    Check(sawIntroduction && !VoxelBossIntroduction.IsShowing && !VoxelBossIntroduction.BlocksControls(player),
                        "Boss entered without poster completion/control release.");
                    Check(VoxelMissionProgress.Active.RemainingTime < introductionTime, "Mission countdown failed to resume.");
                    boss = encounter.Boss; rig = boss.HopperRig;
                    Check(rig != null && rig.hoppers.All(h => h.IsAlive && h.HealthBar != null) && boss.LaneOffset == 0,
                        "Boss did not spawn centred with four independent health bars.");
                    Check(Object.FindObjectsByType<VoxelObstacleCar>(FindObjectsSortMode.None).Length == 0, "Boss appeared before radar/civilian traffic left naturally.");
                    Equal(boss.CurrentHealth, track.boss.health, "Initial aggregate health");
                    Time.timeScale = 1;
                    // Bring the combat example into view; movement and attack clocks then run normally.
                    Set(boss, "trackDistance", player.CollisionTrackPosition.y + 45);
                    VoxelBichoLocoValidation.Call(boss, "ApplyTrackPose"); Next(4);
                }
                else if (phase == 20)
                {
                    Equal(VoxelMissionProgress.Active.RemainingTime, introductionTime, "Poster countdown hold");
                    Check(encounter.Boss == null && VoxelBossIntroduction.BlocksControls(player), "Poster did not hold boss/input.");
                    float lane = player.TargetLaneOffset;
                    player.RequestLaneChangeDirection(1); Equal(player.TargetLaneOffset, lane, "Mobile steering during poster");
                    var boost = Object.FindFirstObjectByType<VoxelBoostController>();
                    Check(boost == null || !boost.TryActivateBoost(), "Boost activated during poster.");
                    if (introduction.Elapsed < introduction.AssembledAt + .3f) return;
                    CheckIntroductionInputs();
                    Directory.CreateDirectory("Temp/BossIntroduction"); ScreenCapture.CaptureScreenshot("Temp/BossIntroduction/LivePoster.png");
                    Next(23);
                }
                else if (phase == 23)
                {
                    if (Elapsed < .1) return;
                    pause.SetPaused(true); Check(VoxelPauseMenu.IsPaused, "Poster blocked pause menu.");
                    introductionClock = introduction.Elapsed; Next(21);
                }
                else if (phase == 21)
                {
                    if (Elapsed < .4) return;
                    Equal(introduction.Elapsed, introductionClock, "Poster animated behind pause menu");
                    Equal(VoxelMissionProgress.Active.RemainingTime, introductionTime, "Paused poster consumed countdown");
                    Check(encounter.Boss == null, "Pause spawned boss behind poster.");
                    pause.SetPaused(false); Next(22);
                }
                else if (phase == 22)
                {
                    if (VoxelBossIntroduction.IsShowing)
                    {
                        Equal(VoxelMissionProgress.Active.RemainingTime, introductionTime, "Poster hold/out consumed countdown");
                        Check(encounter.Boss == null, "Boss spawned before slide-out finished.");
                        return;
                    }
                    Check(encounter.CurrentStage == VoxelBossEncounter.Stage.Boss && encounter.Boss != null,
                        "Slide-out did not trigger normal boss spawn.");
                    Equal(introduction.Elapsed, introduction.Duration, "Poster total duration");
                    Check(!VoxelBossIntroduction.BlocksControls(player) && !VoxelBossIntroduction.PausesMission(VoxelMissionProgress.Active),
                        "Poster retained gates after slide-out.");
                    Next(3);
                }
                else if (phase == 4)
                {
                    Check(rig.RoofLightIsLit == rig.hoppers.Any(h => h.IsLit), "Live roof beacon is out of sync.");
                    var glow = Resources.Load<Material>("CarMaterials/CarBrakeLights");
                    Check(glow.IsKeywordEnabled("_EMISSION") &&
                        (glow.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive) != 0,
                        "Live beacon glow lost its authored emission variant.");
                    Check(rig.roofLamp.sharedMaterial == (rig.RoofLightIsLit ? glow : rig.roofLampOff) &&
                        rig.hoppers.All(h => h.roofLamp.sharedMaterial == (h.IsLit ? glow : h.lampOff)),
                        "Live beacon renderers do not use the brake-light emission when lit.");
                    if (rig.MinesDropped == 0 || Elapsed < 1) return;
                    Check(rig.LastDropSlot >= 0 && rig.hoppers[rig.LastDropSlot].IsAlive && Object.FindFirstObjectByType<VoxelRoadMine>() != null,
                        "No actual hopper mine spawned.");
                    Directory.CreateDirectory("Temp/BichoLoco"); ScreenCapture.CaptureScreenshot("Temp/BichoLoco/LiveEncounter.png");
                    Next(10);
                }
                else if (phase == 10)
                {
                    if (Elapsed < .25) return; // Capture the encounter before the pause overlay is shown.
                    pause.SetPaused(true); Check(VoxelPauseMenu.IsPaused, "Could not pause hopper combat.");
                    pausedDistance = boss.TrackDistance; lampClock = Field(rig, "phaseTime"); mineClock = Field(rig, "dropClock"); Next(5);
                }
                else if (phase == 5)
                {
                    if (Elapsed < .4) return;
                    Equal(boss.TrackDistance, pausedDistance, "Boss moved during pause");
                    Equal(Field(rig, "phaseTime"), lampClock, "Lamp sequence advanced during pause");
                    Equal(Field(rig, "dropClock"), mineClock, "Drop clock advanced during pause");
                    Check(rig.RoofLightIsLit == rig.hoppers.Any(h => h.IsLit), "Pause desynchronised roof beacon.");
                    pause.SetPaused(false);
                    var hopper = rig.hoppers[0]; boss.TakeProjectileHit(hopper.transform, hopper.CurrentHealth + 100, hopper.transform.position, Vector3.forward);
                    Check(!hopper.IsAlive && !hopper.HealthBar.gameObject.activeSelf && boss.IsBodyShielded, "Independent hopper death failed.");
                    Check(Object.FindObjectsByType<VoxelDebris>(FindObjectsSortMode.None).Any(d => d.name == "El Bicho Loco Hopper Debris"), "Hopper explosion did not release debris.");
                    Set(boss, "nextBossLaneChange", 0f); Next(6);
                }
                else if (phase == 6)
                {
                    if (Mathf.Abs(boss.LaneOffset + 5) > .05f) return;
                    foreach (var hopper in rig.hoppers.Where(h => h.IsAlive))
                    {
                        var local = boss.transform.InverseTransformPoint(hopper.transform.position);
                        Check(boss.LaneOffset + local.x >= -10 && boss.LaneOffset + local.x <= 10, "Surviving hopper moved outside road.");
                    }
                    Check(boss.IsBodyShielded && rig.hoppers[1].IsAlive && rig.hoppers[2].IsAlive && rig.hoppers[3].IsAlive, "Left-lane unlock changed other hoppers.");
                    float reserve = track.boss.health * (1 - rig.Settings.combinedHopperHealthPercent * .01f);
                    VoxelMissileTarget.Blast(boss.transform.position, 100, 100000, Vector3.forward);
                    Equal(boss.CurrentHealth, reserve, "Final hopper splash leaked into body");
                    Check(!boss.IsBodyShielded && !VoxelMissionProgress.Active.IsComplete && encounter.CurrentStage == VoxelBossEncounter.Stage.Boss,
                        "Hopper phase completed the mission before body defeat.");
                    Check(!rig.RoofLightIsLit, "Roof beacon stayed lit after final hopper death.");
                    if (rig.Settings.usePsychoBehaviourAfterHoppers)
                    {
                        Check(boss.IsBichoPsychoPhase && boss.IsBoss, "Last hopper did not start optional Psycho body phase.");
                        sawBodyWarning = false; Next(11);
                    }
                    else DefeatBody();
                }
                else if (phase == 11)
                {
                    Check(boss.IsBichoPsychoPhase && boss.IsBoss && Mathf.Abs(boss.LaneOffset) <= 5.01f,
                        "Live Psycho body phase lost identity or left two-lane bounds.");
                    sawBodyWarning |= boss.PsychoPhase == PsychoBugPhase.Warning;
                    if (!sawBodyWarning || !(bool)boss.GetType().GetField("psychoRamApplied", Private).GetValue(boss)) return;
                    DefeatBody();
                }
                else if (phase == 7)
                {
                    var results = Object.FindFirstObjectByType<VoxelPostRaceContinue>();
                    if (results == null || results.ContinueButton == null) return;
                    results.OpenWorkshop(); Check(VoxelLoadingScreen.IsLoading, "Normal Continue did not open workshop."); Next(8);
                }
                else if (phase == 8)
                {
                    var garage = Object.FindFirstObjectByType<VoxelRepairUpgradeSceneController>();
                    Check(garage != null && garage.DisplayedCar != null && VoxelTrackProgressState.CurrentTrackIndex == trackIndex &&
                        VoxelCurrencyState.Balance == finalWallet, "Garage transition lost mission state/rewards.");
                    Check(VoxelLoadingScreen.LoadScene(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainMenu.unity")), "Return to menu failed."); Next(9);
                }
                else if (phase == 9)
                {
                    Check(EditorJsonUtility.ToJson(track) == trackBefore && EditorJsonUtility.ToJson(track.boss) == bossBefore &&
                        EditorJsonUtility.ToJson(track.boss.GetAttack<VoxelBichoLocoTuning>()) == attackBefore, "Play validation edited source tuning.");
                    Finish(true, "PASS (Play Mode): real TrackBoss02 loads four lanes; radar defeat and natural traffic clearance; layered introduction assembles/shakes/holds for its configured duration/slides out before boss exists; steering/boost blocked and countdown held throughout; pause suspends intro and resume continues it; slide-out releases input/timer before centred boss entrance; four health bars and sequential mine drops; pause freezes movement/lamps/drop clock; independent hopper explosion and legal freed-lane movement; final hopper splash preserves body health; optional Psycho body phase completes an actual warning/side-ram while keeping boss identity and two-lane bounds; only body defeat completes the mission; duplicate death does not pay twice; normal results/Continue loads garage with position/rewards retained; source tuning, run state, clocks/audio/RNG restored; MainMenu restored. Captures: Temp/BossIntroduction/LivePoster.png and Temp/BichoLoco/LiveEncounter.png.");
                }
            }
            catch (Exception error) { Finish(false, "FAIL: " + error); }
        }

        private static void DefeatBody()
        {
            var body = boss.GetComponentsInChildren<MeshRenderer>().First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null &&
                r.GetComponentInParent<VoxelEnemyHealthBar>() == null);
            float health = boss.CurrentHealth;
            walletBeforeCompletion = VoxelCurrencyState.Balance;
            boss.TakeProjectileHit(body.transform, health + 1, body.transform.position, Vector3.forward);
            Check(VoxelMissionProgress.Active.IsComplete && encounter.CurrentStage == VoxelBossEncounter.Stage.Complete, "Body defeat failed to complete mission.");
            finalWallet = VoxelCurrencyState.Balance;
            Check(finalWallet == walletBeforeCompletion + VoxelMissionProgress.Active.TotalCurrencyEarned, "Completion rewards were not paid exactly once.");
            boss.TakeProjectileHit(body.transform, health + 1, body.transform.position, Vector3.forward);
            Check(VoxelCurrencyState.Balance == finalWallet, "Repeated death awarded currency again."); Next(7);
        }

        private static void CheckIntroductionInputs()
        {
            var previousKeyboard = Keyboard.current;
            Keyboard keyboard = null;
            var gunTuning = ScriptableObject.CreateInstance<VoxelGunTuning>();
            var gunObject = new GameObject("Temporary Reveal Input QA"); gunObject.transform.SetParent(player.transform, false);
            var gun = gunObject.AddComponent<VoxelGunMount>(); gun.tuning = gunTuning;
            float speed = player.CurrentSpeed, lane = player.TargetLaneOffset;
            try
            {
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.D, Key.LeftCtrl, Key.X, Key.LeftAlt));
                InputSystem.Update();
                VoxelBichoLocoValidation.Property(player, "CurrentSpeed", player.EffectiveTopSpeed);
                VoxelBichoLocoValidation.Call(player, "Update");
                Equal(player.CurrentSpeed, player.EffectiveTopSpeed, "Keyboard brake bypassed poster lock");
                Equal(player.TargetLaneOffset, lane, "Keyboard steering bypassed poster lock");
                Check(!gun.TryBeginShot(out _), "Gun bypassed poster lock.");
                gunTuning.projectileKind = VoxelProjectileKind.Missile;
                Check(!gun.TryBeginShot(out _), "Missile bypassed poster lock.");
                var touch = Object.FindFirstObjectByType<VoxelMobileControls>();
                if (touch != null)
                {
                    foreach (var method in new[] { "SetFireHeld", "SetMissileHeld", "SetBrakeHeld" })
                        VoxelBichoLocoValidation.Call(touch, method, true);
                    Check(!VoxelMobileControls.IsFireHeld && !VoxelMobileControls.IsMissileHeld && !VoxelMobileControls.IsBrakeHeld,
                        "Held touch controls bypassed poster lock.");
                }
            }
            finally
            {
                VoxelBichoLocoValidation.Property(player, "CurrentSpeed", speed);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                previousKeyboard?.MakeCurrent(); VoxelMobileControls.ClearHeldInput();
                Object.DestroyImmediate(gunObject); Object.DestroyImmediate(gunTuning);
            }
        }

        private static void Finish(bool success, string report)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Pending, false);
            VoxelPauseMenu.ResumeActivePause();
            if (captured)
            {
                saved?.Dispose(); saved = null;
                Time.timeScale = scale; Time.fixedDeltaTime = fixedStep; AudioListener.pause = audio;
                Application.runInBackground = background; UnityEngine.Random.state = random; captured = false;
            }
            Directory.CreateDirectory("Temp/BichoLoco"); File.WriteAllText("Temp/BichoLoco/PlayValidation.txt", report);
            if (success) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.isPlaying = false;
        }
    }
}
