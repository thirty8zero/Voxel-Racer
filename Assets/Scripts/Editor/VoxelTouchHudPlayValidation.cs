using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelTouchHudPlayValidation
    {
        private const string Pending = "VoxelRacer.TouchHudCheck";
        private static GameObject root;
        private static VoxelBoostController boost;
        private static VoxelPauseMenu pause;
        private static VoxelMobileControls controls;
        private static VoxelBoostTuning tuning;
        private static VoxelBoostUpgradeTuning dualTuning;
        private static VoxelBoostDisplay display;
        private static Image[] readyGlows;
        private static Image[] readyRipples;
        private static bool sawFirstReadyPulse, sawSecondReadyPulse;
        private static float pausedGlowAlpha, pausedRippleAlpha;
        private static VoxelMissileButtonDisplay missileDisplay;
        private static VoxelGunMount missileLeft, missileRight;
        private static VoxelGunTuning missileTuning;
        private static VoxelMissileReadyEmbers missileEmbers;
        private static bool previousMissileLeft, previousMissileRight;
        private static RectTransform button, cap;
        private static float started, phaseAt, pausedCharge, lastCap, previousScale;
        private static bool previousBackground;
        private static UnityEngine.Random.State previousRandom;
        private static int phase, readyFrame;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }
        [MenuItem("Tools/Voxel Racer/Validate Touch HUD in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
            foreach (string key in new[] { VoxelFpsCounter.PreferenceKey, VoxelFpsCounter.WavePreferenceKey, VoxelMobileControls.ControlSchemePreferenceKey })
            {
                SessionState.SetBool(Pending + key + ".Exists", PlayerPrefs.HasKey(key));
                SessionState.SetInt(Pending + key, PlayerPrefs.GetInt(key, 1));
            }
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick; Restore(); SessionState.SetBool(Pending, false);
            }
        }
        private static void Restore()
        {
            foreach (string key in new[] { VoxelFpsCounter.PreferenceKey, VoxelFpsCounter.WavePreferenceKey, VoxelMobileControls.ControlSchemePreferenceKey })
            {
                if (SessionState.GetBool(Pending + key + ".Exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Pending + key, 1));
                else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Setup()
        {
            try
            {
                previousBackground = Application.runInBackground; Application.runInBackground = true;
                previousRandom = UnityEngine.Random.state;
                previousMissileLeft = VoxelMissileUpgradeState.IsPurchased(false);
                previousMissileRight = VoxelMissileUpgradeState.IsPurchased(true);
                previousScale = Time.timeScale; Time.timeScale = 1;
                started = phaseAt = Time.realtimeSinceStartup; phase = 0; readyFrame = -1;
                root = new GameObject("Temporary Touch HUD Play QA");
                var car = root.AddComponent<VoxelCarController>(); car.SetDrivingEnabled(false);
                controls = root.AddComponent<VoxelMobileControls>(); controls.Configure(car);
                boost = root.AddComponent<VoxelBoostController>();
                tuning = ScriptableObject.CreateInstance<VoxelBoostTuning>(); tuning.boostLength = .35f; tuning.rechargeCooldownLength = .65f;
                boost.Configure(car, tuning);
                display = root.AddComponent<VoxelBoostDisplay>(); display.Configure(boost);
                readyGlows = root.GetComponentsInChildren<Image>().Where(image => image.name.StartsWith("Boost Ready Glow ")).OrderBy(image => image.name).ToArray();
                readyRipples = root.GetComponentsInChildren<Image>().Where(image => image.name.StartsWith("Boost Ready Ripple ")).OrderBy(image => image.name).ToArray();
                sawFirstReadyPulse = sawSecondReadyPulse = false;
                button = root.GetComponentsInChildren<RectTransform>().First(r => r.name == "Boost Button");
                cap = root.GetComponentsInChildren<RectTransform>().First(r => r.name == "Boost Cap");
                pause = root.AddComponent<VoxelPauseMenu>(); pause.Configure(car);
                root.AddComponent<VoxelObstacleSpawner>(); // A race-owned row without creating traffic content.
                VoxelFpsCounter.EnsureExists(); VoxelFpsCounter.SetVisible(true); VoxelFpsCounter.SetWaveDebugVisible(true);
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void Press()
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            foreach (var handler in VoxelHudStyles.GetTouchArea(button).GetComponents<MonoBehaviour>().OfType<IPointerDownHandler>()) handler.OnPointerDown(data);
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.isPaused) return;
                Check(Time.realtimeSinceStartup - started < 12, "Touch HUD check timed out");
                if (phase == 0 && Time.realtimeSinceStartup - phaseAt > .15f)
                {
                    if (!boost.CanActivate || !button.GetComponentInParent<CanvasGroup>().blocksRaycasts)
                    { readyFrame = -1; return; }
                    if (readyFrame < 0) { readyFrame = Time.frameCount; return; }
                    if (Time.frameCount <= readyFrame + 1) return;
                    Check(readyGlows.Length == 2 && readyGlows.All(glow => !glow.enabled && !glow.raycastTarget),
                        "Ready glow flashed at startup or intercepted input.");
                    Check(readyRipples.Length == 2 && readyRipples.All(ripple => !ripple.enabled && !ripple.raycastTarget),
                        "Trailing ripple flashed at startup or intercepted input.");
                    Check(root.GetComponentsInChildren<Text>().Single(text => text.name == "Boost Label").color == Color.white,
                        "Boost label is not full-bright white.");
                    Press(); Check(boost.IsBoosting && cap.anchoredPosition.y == -8, "Pointer-down did not immediately depress/activate boost");
                    Check(root.GetComponentsInChildren<Text>().Single(text => text.name == "Boost Label").color == new Color(.38f, .38f, .38f),
                        "Active boost did not grey out its unavailable label.");
                    phaseAt = Time.realtimeSinceStartup; phase = 1;
                }
                else if (phase == 1 && Time.realtimeSinceStartup - phaseAt > .08f)
                {
                    Check(boost.IsBoosting && cap.anchoredPosition.y == -8, "Active boost incorrectly raised the cap");
                    pause.SetPaused(true); Check(VoxelPauseMenu.IsPaused, "Pause failed"); pausedCharge = boost.ChargePercent;
                    var waveToggle = root.GetComponentsInChildren<Toggle>().First(t => t.name == "Wave Debug Toggle");
                    waveToggle.isOn = false; Check(!VoxelFpsCounter.ShowWaveDebug, "Pause-menu wave preference did not turn off");
                    waveToggle.isOn = true; Check(VoxelFpsCounter.ShowWaveDebug, "Pause-menu wave preference did not turn on");
                    VoxelFpsCounter.SetVisible(false);
                    var texts = VoxelFpsCounter.Active.GetComponentsInChildren<Text>();
                    Check(!texts.First(t => t.name == "FPS").enabled && texts.First(t => t.name == "Wave Debug").enabled,
                        "Wave row must stay visible independently when FPS is off");
                    VoxelFpsCounter.SetVisible(true); waveToggle.isOn = false;
                    Check(texts.First(t => t.name == "FPS").enabled && !texts.First(t => t.name == "Wave Debug").enabled,
                        "Wave toggle must not hide FPS");
                    ValidateControlSchemes();
                    phaseAt = Time.realtimeSinceStartup; phase = 2;
                }
                else if (phase == 2 && Time.realtimeSinceStartup - phaseAt > .2f)
                {
                    Press(); Check(boost.ChargePercent == pausedCharge && cap.anchoredPosition.y == -8, "Pause did not freeze boost/cap");
                    Check(!button.GetComponentInParent<CanvasGroup>().blocksRaycasts, "Paused boost intercepted touch");
                    pause.SetPaused(false); phase = 3;
                }
                else if (phase == 3 && !boost.IsBoosting)
                { lastCap = cap.anchoredPosition.y; phase = 4; }
                else if (phase == 4)
                {
                    Check(cap.anchoredPosition.y >= lastCap - .001f, "Cap moved down during recharge"); lastCap = cap.anchoredPosition.y;
                    Check(root.GetComponentsInChildren<Text>().Single(text => text.name == "Boost Label").color ==
                        (boost.CanActivate ? Color.white : new Color(.38f, .38f, .38f)),
                        "Boost label did not follow actual activation availability.");
                    Check(button.anchoredPosition == new Vector2(420, 630) && button.sizeDelta == new Vector2(220, 220), "Animation changed touch area");
                    if (boost.IsReady && Mathf.Abs(cap.anchoredPosition.y - 24) < .001f)
                    { phaseAt = Time.realtimeSinceStartup; phase = 5; }
                }
                else if (phase == 5 && Time.realtimeSinceStartup - phaseAt > .12f)
                {
                    Check(readyGlows[0].enabled && readyGlows[0].color.a > .1f && !readyGlows[1].enabled,
                        "Completing single-bottle recharge did not pulse its blue ring.");
                    Check(readyGlows[0].rectTransform.localScale.x > 1.4f, "Ready pulse did not expand substantially beyond the blue bar.");
                    Check(readyRipples[0].enabled && readyRipples[0].color.a > .05f && !readyRipples[1].enabled &&
                        readyRipples[0].rectTransform.localScale.x < readyGlows[0].rectTransform.localScale.x,
                        "Ready pulse did not leave a smaller, delayed trailing ripple.");
                    pausedGlowAlpha = readyGlows[0].color.a;
                    pausedRippleAlpha = readyRipples[0].color.a;
                    pause.SetPaused(true); phaseAt = Time.realtimeSinceStartup; phase = 6;
                }
                else if (phase == 6 && Time.realtimeSinceStartup - phaseAt > .15f)
                {
                    Check(readyGlows[0].enabled && Mathf.Approximately(readyGlows[0].color.a, pausedGlowAlpha),
                        "Pause did not freeze the ready pulse.");
                    Check(readyRipples[0].enabled && Mathf.Approximately(readyRipples[0].color.a, pausedRippleAlpha),
                        "Pause did not freeze the trailing ripple.");
                    pause.SetPaused(false); phaseAt = Time.realtimeSinceStartup; phase = 7;
                }
                else if (phase == 7 && Time.realtimeSinceStartup - phaseAt > .8f)
                {
                    Check(readyGlows.All(glow => !glow.enabled) && readyRipples.All(ripple => !ripple.enabled), "Ready pulse repeated or did not fade away.");
                    dualTuning = ScriptableObject.CreateInstance<VoxelBoostUpgradeTuning>();
                    dualTuning.bottleCount = 2; dualTuning.boostLength = .35f; dualTuning.rechargeCooldownLength = .25f;
                    boost.Configure(root.GetComponent<VoxelCarController>(), dualTuning); display.Configure(boost);
                    Press(); Check(boost.IsBoosting && boost.ActiveChargeIndex == 0, "Dual first bottle did not activate.");
                    phase = 8;
                }
                else if (phase == 8 && !boost.IsBoosting)
                {
                    Press(); Check(boost.IsBoosting && boost.ActiveChargeIndex == 1, "Dual spare bottle did not activate.");
                    phase = 9;
                }
                else if (phase == 9)
                {
                    sawFirstReadyPulse |= readyGlows[0].enabled && readyGlows[0].color.a > .1f;
                    sawSecondReadyPulse |= readyGlows[1].enabled && readyGlows[1].color.a > .1f;
                    if (sawFirstReadyPulse && sawSecondReadyPulse)
                    {
                        Check(readyGlows.All(glow => Mathf.Abs(glow.fillAmount - 145f / 360f) < .001f),
                            "Dual ready glow did not follow the split charge rings.");
                        display.enabled = false;
                        Check(readyGlows.All(glow => !glow.enabled) && readyRipples.All(ripple => !ripple.enabled), "Disabled HUD retained a ready glow.");
                        display.enabled = true; phaseAt = Time.realtimeSinceStartup; phase = 10;
                    }
                }
                else if (phase == 10 && Time.realtimeSinceStartup - phaseAt > .15f)
                {
                    Check(readyGlows.All(glow => !glow.enabled) && readyRipples.All(ripple => !ripple.enabled), "Re-enabling HUD replayed a stale pulse.");
                    SetupMissilePulse(); phaseAt = Time.realtimeSinceStartup; phase = 11;
                }
                else if (phase == 11 && Time.realtimeSinceStartup - phaseAt > .1f)
                {
                    Check(missileDisplay.IsReady && !missileEmbers.enabled &&
                        !missileEmbers.raycastTarget,
                        "Missile ready pulse flashed at startup or changed pointer targets.");
                    Check(missileLeft.TryBeginShot(out _) && missileRight.TryBeginShot(out _), "Missile cooldown test failed to reserve both shots.");
                    VoxelMissileValidation.Call(missileDisplay, "Refresh"); phase = 12;
                }
                else if (phase == 12 && missileEmbers.enabled && missileEmbers.Age > .075f)
                {
                    Check(missileDisplay.IsReady && missileDisplay.ChargePercent == 1f &&
                        missileEmbers.Age < .30f,
                        "Missile refill did not produce a quick ember burst.");
                    pausedGlowAlpha = missileEmbers.Age;
                    pause.SetPaused(true); phaseAt = Time.realtimeSinceStartup; phase = 13;
                }
                else if (phase == 13 && Time.realtimeSinceStartup - phaseAt > .15f)
                {
                    Check(Mathf.Approximately(missileEmbers.Age, pausedGlowAlpha), "Pause advanced the red ready effect.");
                    pause.SetPaused(false); phaseAt = Time.realtimeSinceStartup; phase = 14;
                }
                else if (phase == 14 && Time.realtimeSinceStartup - phaseAt > .4f)
                {
                    Check(!missileEmbers.enabled, "Red ready effect repeated while loaded.");
                    Check(missileLeft.TryBeginShot(out _) && missileRight.IsReady, "Spare missile launcher was not ready.");
                    VoxelMissileValidation.Call(missileDisplay, "Refresh"); phase = 15;
                }
                else if (phase == 15 && missileEmbers.enabled && missileEmbers.Age > .02f)
                {
                    Check(missileDisplay.IsReady && missileRight.IsReady, "A ready spare prevented the reloaded launcher pulse.");
                    missileDisplay.enabled = false;
                    Check(!missileEmbers.enabled, "Disabled missile HUD retained its pulse.");
                    missileDisplay.enabled = true; phaseAt = Time.realtimeSinceStartup; phase = 16;
                }
                else if (phase == 16 && Time.realtimeSinceStartup - phaseAt > .12f)
                {
                    Check(!missileEmbers.enabled, "Re-enabled missile HUD replayed a stale pulse.");
                    missileTuning.ammunitionPerStage = 1;
                    VoxelMissileValidation.Call(missileLeft, "OnEnable"); VoxelMissileValidation.Call(missileRight, "OnEnable");
                    missileDisplay.Configure(root.GetComponent<VoxelCarController>());
                    Check(missileLeft.TryBeginShot(out _) && missileRight.TryBeginShot(out _), "Finite-ammo launch setup failed.");
                    phaseAt = Time.realtimeSinceStartup; phase = 17;
                }
                else if (phase == 17 && Time.realtimeSinceStartup - phaseAt > .4f)
                {
                    Check(!missileDisplay.IsReady && !missileEmbers.enabled,
                        "Empty missile launchers emitted a ready pulse.");
                    Finish("PASS (Play Mode): BOOST grey/white availability, fitted text, real pointer/cap/recharge and independent dual-bottle pulses; blue burst/ripple pause and cleanup. Missile reload produces a large translucent spinning-square wave with inner-first fade; spare-ready launcher does not mask completion; initial readiness/resume/re-enable/empty ammo do not cause false pulses; pause freezes embers and disable clears them. Existing input bounds/control-scheme/FPS/wave checks passed. Temporary objects/tunings removed; purchases/preferences restored.");
                }
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void SetupMissilePulse()
        {
            missileTuning = Object.Instantiate(VoxelMissileLauncherTuning.Load().weapon);
            missileTuning.useMissileCooldown = true; missileTuning.missileCooldownSeconds = .3f;
            missileTuning.ammunitionPerStage = 0; missileTuning.bulletsPerShot = 1;
            VoxelGunMount MakeMount(string name)
            {
                var obj = new GameObject(name); obj.transform.SetParent(root.transform, false);
                var mount = obj.AddComponent<VoxelGunMount>(); mount.tuning = missileTuning;
                VoxelMissileValidation.Call(mount, "OnEnable"); return mount;
            }
            missileLeft = MakeMount("HUD test left missile"); missileRight = MakeMount("HUD test right missile");
            SetMissileOwnership(true, true);
            controls.Configure(root.GetComponent<VoxelCarController>());
            VoxelMissileValidation.Call(controls, "UpdateMissileVisibility");
            missileDisplay = controls.GetComponentInChildren<VoxelMissileButtonDisplay>(true);
            missileEmbers = missileDisplay.GetComponentInChildren<VoxelMissileReadyEmbers>(true);
        }

        private static void SetMissileOwnership(bool left, bool right)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            typeof(VoxelMissileUpgradeState).GetField("leftPurchased", flags).SetValue(null, left);
            typeof(VoxelMissileUpgradeState).GetField("rightPurchased", flags).SetValue(null, right);
        }
        private static void ValidateControlSchemes()
        {
            var toggle = root.GetComponentsInChildren<Toggle>().First(t => t.name == "Control Scheme B Toggle");
            var rects = controls.GetComponentsInChildren<RectTransform>();
            var left = rects.First(r => r.name == "Move Left Button");
            var right = rects.First(r => r.name == "Move Right Button");
            var brake = rects.First(r => r.name == "Brake Button");
            toggle.isOn = false;
            Check(left.anchoredPosition.y == 150 && right.anchoredPosition.y == 150 && brake.anchoredPosition.y == 390,
                "Scheme A positions did not restore");
            // Seed held input directly: the public accessors deliberately mask it while paused.
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            foreach (string field in new[] { "brakeHeld", "fireHeld", "missileHeld" })
                typeof(VoxelMobileControls).GetField(field, flags).SetValue(null, true);
            toggle.isOn = true;
            Check(VoxelMobileControls.UseControlSchemeB && PlayerPrefs.GetInt(VoxelMobileControls.ControlSchemePreferenceKey) == 1,
                "Scheme B preference did not save");
            Check(left.anchoredPosition.y == 390 && right.anchoredPosition.y == 390 && brake.anchoredPosition.y == 150,
                "Scheme B did not swap brake and steering rows");
            Check(left.Find("Chevron").position.y > brake.position.y && VoxelHudStyles.GetTouchArea(brake).position.y == brake.position.y,
                "Artwork or expanded brake input did not move with its control");
            foreach (string field in new[] { "brakeHeld", "fireHeld", "missileHeld" })
                Check(!(bool)typeof(VoxelMobileControls).GetField(field, flags).GetValue(null), "Scheme switch left held input active");
            var rebuilt = new GameObject("Temporary Rebuilt Scheme B HUD"); rebuilt.transform.SetParent(root.transform);
            try
            {
                var copy = rebuilt.AddComponent<VoxelMobileControls>();
                Check(copy.GetComponentsInChildren<RectTransform>().First(r => r.name == "Brake Button").anchoredPosition.y == 150,
                    "New HUD did not use the saved Scheme B preference");
            }
            finally { Object.Destroy(rebuilt); }
            toggle.isOn = false;
            Check(!VoxelMobileControls.UseControlSchemeB && left.anchoredPosition.y == 150 && right.anchoredPosition.y == 150 && brake.anchoredPosition.y == 390,
                "Turning Scheme B off did not restore the original layout");
        }

        private static void Finish(string message)
        {
            EditorApplication.update -= Tick;
            if (pause != null) pause.SetPaused(false);
            if (root != null) Object.Destroy(root);
            if (tuning != null) Object.Destroy(tuning);
            if (dualTuning != null) Object.Destroy(dualTuning);
            if (missileTuning != null) Object.Destroy(missileTuning);
            SetMissileOwnership(previousMissileLeft, previousMissileRight);
            Restore(); Time.timeScale = previousScale; Application.runInBackground = previousBackground;
            UnityEngine.Random.state = previousRandom;
            Directory.CreateDirectory("Temp/TouchHud"); File.WriteAllText("Temp/TouchHud/PlayValidation.txt", message);
            if (message.StartsWith("PASS")) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.isPlaying = false;
        }
    }
}
