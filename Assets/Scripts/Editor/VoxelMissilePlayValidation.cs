using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace VoxelRacer.Editor
{
    public static class VoxelMissilePlayValidation
    {
        private const string Pending = "VoxelRacer.MissilePlayCheck";
        private static GameObject root;
        private static VoxelEnemyCar enemy;
        private static VoxelGunTuning weapon;
        private static VoxelEnemyVehicleTuning enemyTuning;
        private static VoxelMissileProjectile missile;
        private static float started;
        private static bool sawEffects;
        private static VoxelGunMount cooldownMount;
        private static VoxelMissileButtonDisplay cooldownDisplay;
        private static VoxelGunTuning cooldownWeapon;
        private static float cooldownStarted;
        private static bool sawCooldownRefill, checkedCooldown;
        private static VoxelGunMount launchMount;
        private static bool sawRightBurst, sawLeftBurst, sawRepeatBurst, checkedBurstCleanup;
        private static float repeatedBurstAt;
        private static bool capturedBurst;
        private static bool savedRunInBackground;
        private static float savedTimeScale;
        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        [MenuItem("Tools/Voxel Racer/Validate Missile in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Start from Edit Mode.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            { EditorApplication.update -= Tick; SessionState.SetBool(Pending, false); }
        }
        private static void Setup()
        {
            try
            {
                savedRunInBackground = Application.runInBackground; savedTimeScale = Time.timeScale;
                Application.runInBackground = true; Time.timeScale = 1f;
                root = new GameObject("Temporary live missile test");
                var definition = Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                var carObject = new GameObject("Test player"); carObject.transform.SetParent(root.transform);
                carObject.transform.position = new Vector3(1000, 20, 0);
                var car = carObject.AddComponent<VoxelCarController>(); car.SetDrivingEnabled(false);
                Object.Instantiate(definition.visualPrefab, car.transform); car.ResetIntegrityBaseline();
                var fit = VoxelMissileLauncherTuning.Load();
                var launcher = VoxelMissileUpgradeState.CreateVisual(car.transform, fit, true);
                var mount = launcher.GetComponent<VoxelGunMount>(); weapon = Object.Instantiate(fit.weapon);
                launchMount = mount;
                if (mount.missileLaunchBurst == null || mount.missileLaunchBurst.IsAlive(true))
                    throw new Exception("Launcher rear burst missing or playing before firing");
                weapon.ammunitionPerStage = 1; weapon.bulletsPerShot = 1; mount.tuning = weapon;
                VoxelMissileValidation.Call(mount, "OnEnable");
                if (!mount.TryBeginShot(out int count) || count != 1 || mount.TryBeginShot(out _)) throw new Exception("Ammo/cooldown gating failed");
                VoxelMissileValidation.Call(mount, "FireProjectile"); car.enabled = false; mount.enabled = false;
                missile = Object.FindObjectsByType<VoxelMissileProjectile>(FindObjectsSortMode.None).Single();
                SetupCooldown();
                ValidateExpiry();
                enemyTuning = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>(); enemyTuning.vehicleHealth = 1000;
                enemy = VoxelMissileValidation.MakeTarget(root.transform, enemyTuning,
                    new Vector3(1000, 20, 25));
                foreach(var r in enemy.GetComponentsInChildren<MeshRenderer>())
                    if(Mathf.Abs(r.transform.localPosition.x) < 1.9f) r.gameObject.SetActive(false);
                started = Time.realtimeSinceStartup; sawEffects = false; EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                if (launchMount.missileLaunchBurst.particleCount > 0)
                {
                    sawRightBurst = true; ValidateBurst(launchMount);
                    if (!capturedBurst) CaptureBurst();
                }
                if (cooldownMount.missileLaunchBurst.particleCount > 0)
                {
                    if (checkedCooldown) sawRepeatBurst = true; else sawLeftBurst = true;
                    ValidateBurst(cooldownMount);
                }
                if (!checkedCooldown)
                {
                    float elapsed = Time.time - cooldownStarted;
                    if (elapsed > .05f && elapsed < .55f)
                    {
                        float expected = Mathf.Clamp01(elapsed / .6f);
                        VoxelMissileValidation.Call(cooldownDisplay, "Refresh");
                        if (Mathf.Abs(cooldownDisplay.ChargePercent - expected) > .01f || cooldownDisplay.IsReady ||
                            cooldownMount.TryBeginShot(out _)) throw new Exception("Live cooldown refill/early-fire gating failed");
                        sawCooldownRefill = true;
                    }
                    if (elapsed >= .6f)
                    {
                        VoxelMissileValidation.Call(cooldownDisplay, "Refresh");
                        if (!sawCooldownRefill || !cooldownDisplay.IsReady || cooldownDisplay.ChargePercent != 1f ||
                            !cooldownMount.TryBeginShot(out _)) throw new Exception("Live cooldown did not finish/re-arm");
                        VoxelMissileValidation.Call(cooldownDisplay, "Refresh");
                        if (cooldownDisplay.ChargePercent > .01f || cooldownDisplay.IsReady)
                            throw new Exception("Repeat shot did not empty cooldown face");
                        if (!sawRightBurst || !sawLeftBurst || launchMount.missileLaunchBurst.IsAlive(true) ||
                            cooldownMount.missileLaunchBurst.IsAlive(true))
                            throw new Exception("Rear bursts failed to emit once and finish on both sides");
                        FireCooldownProjectile(); repeatedBurstAt = Time.time;
                        checkedCooldown = true;
                    }
                }
                if (checkedCooldown && Time.time - repeatedBurstAt >= .4f && !checkedBurstCleanup)
                {
                    if (!sawRepeatBurst || cooldownMount.missileLaunchBurst.IsAlive(true))
                        throw new Exception("Repeated firing burst did not restart and clean up");
                    checkedBurstCleanup = true;
                }
                if (missile != null)
                {
                    var ps = missile.GetComponentsInChildren<ParticleSystem>();
                    sawEffects |= ps.Length == 2 && ps.All(p => p.particleCount > 0);
                }
                if (enemy.CurrentHealth < 1000 && missile == null && checkedCooldown && checkedBurstCleanup)
                {
                    if (!sawEffects) throw new Exception("No live rocket/smoke particles before impact");
                    if (Mathf.Abs(enemy.CurrentHealth - (1000 - weapon.damagePerBullet)) > .01f) throw new Exception("Missile health damage repeated");
                    if (!enemy.GetComponentsInChildren<MeshRenderer>(true).Any(r => !r.gameObject.activeSelf)) throw new Exception("No blast voxels removed");
                    Finish("PASS: rear launcher flame bursts on both sides, backward particle velocity, local attachment, no idle emission, automatic finish and repeat-shot reuse; actual firing/ammo/cooldown, live clockwise HUD refill and full/ready/re-fire reset, rocket exhaust and smoke particles, Update-driven descending flight, swept impact, one explosion, health damage once, spherical voxel removal, both sides explode once at maximum range without starting/restarting camera shake and stop emitting trails; physics impacts retain shake. Play Mode.");
                }
                else if (Time.realtimeSinceStartup - started > 6) throw new Exception("Timed out waiting for live missile impact; health=" + enemy.CurrentHealth + ", missile=" + (missile != null ? missile.transform.position.ToString() : "expired") + ", effects=" + sawEffects);
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }
        private static void SetupCooldown()
        {
            var obj = new GameObject("Live cooldown player"); obj.transform.SetParent(root.transform);
            obj.transform.position = new Vector3(3000, 20, 0);
            var car = obj.AddComponent<VoxelCarController>(); car.SetDrivingEnabled(false);
            cooldownWeapon = Object.Instantiate(weapon); cooldownWeapon.ammunitionPerStage = 0;
            cooldownWeapon.useMissileCooldown = true; cooldownWeapon.missileCooldownSeconds = .6f;
            var launcher = VoxelMissileUpgradeState.CreateVisual(car.transform, VoxelMissileLauncherTuning.Load(), false);
            cooldownMount = launcher.GetComponent<VoxelGunMount>(); cooldownMount.tuning = cooldownWeapon;
            if (cooldownMount.missileLaunchBurst == null || cooldownMount.missileLaunchBurst.IsAlive(true))
                throw new Exception("Idle left launcher is emitting a rear burst");
            VoxelMissileValidation.Call(cooldownMount, "OnEnable");
            var controls = obj.AddComponent<VoxelMobileControls>(); controls.Configure(car); controls.enabled = false;
            cooldownDisplay = controls.GetComponentInChildren<VoxelMissileButtonDisplay>(true);
            controls.GetComponentInChildren<CanvasGroup>().alpha = 0;
            VoxelMissileValidation.Call(cooldownDisplay, "Refresh");
            if (!cooldownDisplay.IsReady || cooldownDisplay.ChargePercent != 1f || !cooldownMount.TryBeginShot(out _))
                throw new Exception("Initial live cooldown ready/launch failed");
            VoxelMissileValidation.Call(cooldownDisplay, "Refresh");
            if (cooldownDisplay.ChargePercent > .01f || cooldownMount.TryBeginShot(out _))
                throw new Exception("Live shot did not empty face and block repeat");
            FireCooldownProjectile();
            cooldownStarted = Time.time; sawCooldownRefill = checkedCooldown = false;
            sawRightBurst = sawLeftBurst = sawRepeatBurst = checkedBurstCleanup = false;
            capturedBurst = false;
        }
        private static void FireCooldownProjectile()
        {
            VoxelMissileValidation.Call(cooldownMount, "FireProjectile");
            foreach (var p in Object.FindObjectsByType<VoxelMissileProjectile>(FindObjectsSortMode.None))
                if (p != missile && Vector3.Distance(p.transform.position, cooldownMount.MuzzlePosition) < .01f)
                    p.transform.SetParent(root.transform, true);
        }
        private static void ValidateBurst(VoxelGunMount mount)
        {
            var ps = mount.missileLaunchBurst;
            if (ps.main.loop || ps.main.playOnAwake || ps.main.simulationSpace != ParticleSystemSimulationSpace.Local ||
                !ps.transform.IsChildOf(mount.transform) || Vector3.Dot(ps.transform.forward, -mount.FireDirection) < .999f)
                throw new Exception("Rear burst must be non-looping, attached and rear-facing");
            var particles = new ParticleSystem.Particle[ps.main.maxParticles];
            int count = ps.GetParticles(particles);
            for (int i = 0; i < count; i++)
                if (Vector3.Dot(ps.transform.TransformDirection(particles[i].velocity).normalized, -mount.FireDirection) < .95f)
                    throw new Exception("Launcher flame particle moved forwards");
        }
        private static void CaptureBurst()
        {
            var car = launchMount.GetComponentInParent<VoxelCarController>();
            foreach (var t in car.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
            var cameraObj = new GameObject("Rear launch burst capture"); cameraObj.transform.SetParent(root.transform);
            var camera = cameraObj.AddComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << 31;
            camera.transform.position = car.transform.position + new Vector3(2.3f, 2.25f, -3.6f);
            camera.transform.LookAt(car.transform.position + new Vector3(.65f, 1.55f, -1.25f));
            camera.orthographic = true; camera.orthographicSize = .62f; camera.nearClipPlane = .05f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.17f, .2f, .24f);
            var rt = new RenderTexture(1000, 650, 24); var texture = new Texture2D(1000, 650, TextureFormat.RGB24, false);
            var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1000, 650), 0, 0); texture.Apply();
                Directory.CreateDirectory("Temp/Missile"); File.WriteAllBytes("Temp/Missile/LauncherBurst.png", texture.EncodeToPNG());
                capturedBurst = true;
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = oldActive;
                rt.Release(); Object.Destroy(rt); Object.Destroy(texture); Object.Destroy(cameraObj);
            }
        }
        private static void ValidateExpiry()
        {
            var expiryWeapon = Object.Instantiate(weapon);
            var existingCamera = Camera.main;
            bool cameraWasEnabled = existingCamera != null && existingCamera.enabled;
            if (existingCamera != null) existingCamera.enabled = false;
            var cameraObject = new GameObject("Missile shake test camera");
            cameraObject.transform.SetParent(root.transform); cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            var follow = cameraObject.AddComponent<VoxelCameraFollow>(); follow.enabled = false;
            var cameraTuning = Object.Instantiate(VoxelCameraTuning.Load());
            cameraTuning.objectExplosionShakeDuration = 1f; follow.tuning = cameraTuning;
            var shakeField = typeof(VoxelCameraFollow).GetField("explosionShake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var carObject = new GameObject("Range expiry test player");
            carObject.transform.SetParent(root.transform);
            var car = carObject.AddComponent<VoxelCarController>();
            car.SetDrivingEnabled(false); car.enabled = false;
            try
            {
                expiryWeapon.maximumRange = 20f;
                foreach (float side in new[] { -1f, 1f })
                {
                    shakeField.SetValue(follow, Activator.CreateInstance(shakeField.FieldType));
                    // An empty-air explosion must leave an existing shake alone, too.
                    if (side > 0) follow.ShakeFromObjectExplosion();
                    object shakeBeforeExpiry = shakeField.GetValue(follow);
                    carObject.transform.position = new Vector3(2000 + side * 100, 20, 0);
                    Vector3 muzzle = carObject.transform.position + new Vector3(side * .8f, 1.7f, 0);
                    var projectile = VoxelMissileProjectile.Create(muzzle, car, side, expiryWeapon);
                    projectile.enabled = false;
                    var trails = projectile.GetComponentsInChildren<ParticleSystem>();
                    Vector3 endpoint = (Vector3)VoxelMissileValidation.Call(projectile, "PositionAt", expiryWeapon.maximumRange);
                    VoxelMissileValidation.Call(projectile, "Advance", 10f);
                    VoxelMissileValidation.Call(projectile, "Advance", 10f);
                    if (!shakeBeforeExpiry.Equals(shakeField.GetValue(follow)))
                        throw new Exception("Range expiry started or restarted camera shake, side=" + side);
                    var explosions = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                        .Where(t => t.name == "Voxel Destruction Explosion" && Vector3.Distance(t.position, endpoint) < .001f).ToArray();
                    if (explosions.Length != 1) throw new Exception("Range expiry must create exactly one explosion at the endpoint, side=" + side);
                    if (Vector3.Distance(projectile.transform.position, endpoint) > .001f) throw new Exception("Missile overshot maximum range");
                    if (trails.Length != 2 || trails.Any(p => p.isEmitting || p.transform.IsChildOf(projectile.transform)))
                        throw new Exception("Expired missile trails did not stop and detach");

                    shakeField.SetValue(follow, Activator.CreateInstance(shakeField.FieldType));
                    var obstruction = new GameObject("Physics missile impact test");
                    obstruction.transform.SetParent(root.transform);
                    obstruction.transform.position = carObject.transform.position + new Vector3(0, 1, 10);
                    obstruction.AddComponent<BoxCollider>().size = new Vector3(10, 10, 1);
                    Physics.SyncTransforms();
                    var impactMissile = VoxelMissileProjectile.Create(muzzle, car, side, expiryWeapon);
                    impactMissile.enabled = false;
                    VoxelMissileValidation.Call(impactMissile, "Advance", 10f);
                    object impactShake = shakeField.GetValue(follow);
                    if (!(bool)shakeField.FieldType.GetField("active").GetValue(impactShake))
                        throw new Exception("Physics impact lost explosion shake, side=" + side);
                    Object.Destroy(obstruction);
                }
            }
            finally
            {
                cameraObject.SetActive(false);
                if (existingCamera != null) existingCamera.enabled = cameraWasEnabled;
                Object.Destroy(cameraObject); Object.Destroy(cameraTuning); Object.Destroy(expiryWeapon);
            }
        }
        private static void Finish(string message)
        {
            Directory.CreateDirectory("Temp/Missile"); File.WriteAllText("Temp/Missile/PlayValidation.txt", message);
            EditorApplication.update -= Tick;
            if (root != null) Object.Destroy(root);
            if (weapon != null) Object.Destroy(weapon);
            if (cooldownWeapon != null) Object.Destroy(cooldownWeapon);
            if (enemyTuning != null) Object.Destroy(enemyTuning);
            Application.runInBackground = savedRunInBackground; Time.timeScale = savedTimeScale;
            Debug.Log(message); EditorApplication.isPlaying = false;
        }
    }
}
