using UnityEditor;
using UnityEngine;
namespace VoxelRacer.Editor
{
    public static class VoxelMissileBuilder
    {
        [MenuItem("Tools/Voxel Racer/Build Missile Launcher Upgrade")]
        public static void Build()
        {
            var metal = Mat("MissileLauncherSteel", new Color(.24f, .28f, .30f));
            var dark = Mat("MissileLauncherDark", new Color(.055f, .065f, .075f));
            var silver = Mat("MissileBody", new Color(.72f, .75f, .77f));
            var red = Mat("MissileNose", new Color(.6f, .12f, .035f));
            var smoke = ParticleMat("MissileSmoke", false);
            var fire = ParticleMat("MissileFlame", true);
            var missile = new GameObject("Missile");
            try
            {
                VoxelMissileModelBuilder.BuildMissile(missile, metal, dark, silver, red);
                Particles(missile.transform, "Rocket exhaust", fire, false);
                Particles(missile.transform, "Smoke trail", smoke, true);
                PrefabUtility.SaveAsPrefabAsset(missile, "Assets/Prefabs/Weapons/Missile.prefab");
            }
            finally { Object.DestroyImmediate(missile); }
            const string weaponPath = "Assets/Resources/Weapons/MissileWeaponTuning.asset";
            var weapon = AssetDatabase.LoadAssetAtPath<VoxelGunTuning>(weaponPath);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<VoxelGunTuning>();
                weapon.displayName = "MISSILE LAUNCHER"; weapon.purchasePrice = 650; weapon.shotsPerSecond = .65f;
                weapon.projectileSpeed = 75; weapon.maximumRange = 160; weapon.damagePerBullet = 35; weapon.areaOfEffectRadius = 1.8f;
                weapon.projectileKind = VoxelProjectileKind.Missile;
                AssetDatabase.CreateAsset(weapon, weaponPath);
            }
            weapon.missilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/Missile.prefab");
            var launcher = new GameObject("Roof Missile Launcher"); launcher.AddComponent<VoxelIndestructiblePart>();
            try
            {
                VoxelMissileModelBuilder.BuildLauncher(launcher, metal, dark, silver, red);
                var muzzle = new GameObject("Missile Muzzle").transform; muzzle.SetParent(launcher.transform, false); muzzle.localPosition = new Vector3(0, .17f, 1f);
                var mount = launcher.AddComponent<VoxelGunMount>(); mount.tuning = weapon; mount.muzzle = muzzle;
                EnsureLauncherLaunchBurst(launcher);
                weapon.visualPrefab = PrefabUtility.SaveAsPrefabAsset(launcher, "Assets/Prefabs/Weapons/MissileLauncher.prefab");
            }
            finally { Object.DestroyImmediate(launcher); }
            EditorUtility.SetDirty(weapon); AssetDatabase.SaveAssetIfDirty(weapon);
            const string path = "Assets/Resources/Weapons/MissileLauncherTuning.asset";
            var fit = AssetDatabase.LoadAssetAtPath<VoxelMissileLauncherTuning>(path);
            if (fit == null)
            {
                fit = ScriptableObject.CreateInstance<VoxelMissileLauncherTuning>(); fit.weapon = weapon;
                fit.compatibleCarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/SpyCar2PlayerCar.prefab");
                AssetDatabase.CreateAsset(fit, path);
            }
            AssetDatabase.SaveAssetIfDirty(fit);
        }
        private static Material Mat(string name, Color colour)
        {
            string path = "Assets/Resources/CarMaterials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", colour);
            m.SetFloat("_Metallic", .35f); m.SetFloat("_Smoothness", .3f); AssetDatabase.CreateAsset(m, path); return m;
        }

        [MenuItem("Tools/Voxel Racer/Update Missile Launcher Flame Burst")]
        public static void UpdateLauncherFlameBurst()
        {
            const string path = "Assets/Prefabs/Weapons/MissileLauncher.prefab";
            var launcher = PrefabUtility.LoadPrefabContents(path);
            try
            {
                EnsureLauncherLaunchBurst(launcher);
                PrefabUtility.SaveAsPrefabAsset(launcher, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(launcher); }
        }

        public static void EnsureLauncherLaunchBurst(GameObject launcher)
        {
            const string effectName = "Launcher rear flame burst";
            var outlet = launcher.transform.Find(effectName);
            ParticleSystem ps;
            if (outlet != null)
                ps = outlet.GetComponent<ParticleSystem>();
            else
            {
                var go = new GameObject(effectName); go.transform.SetParent(launcher.transform, false);
                go.transform.localPosition = new Vector3(0, .17f, -.595f);
                go.transform.localRotation = Quaternion.LookRotation(Vector3.back);
                ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = .12f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.14f, .24f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f);
                main.startSize3D = true;
                main.startSizeX = main.startSizeY = new ParticleSystem.MinMaxCurve(.14f, .22f);
                main.startSizeZ = new ParticleSystem.MinMaxCurve(.26f, .40f);
                main.startColor = Color.white; main.maxParticles = 32;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var emission = ps.emission; emission.rateOverTime = 100; emission.rateOverDistance = 0;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0, 12) });
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 6; shape.radius = .045f;
                var size = ps.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .45f), new Keyframe(.15f, 1), new Keyframe(1, 0)));
                var colour = ps.colorOverLifetime; colour.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] {
                    new GradientColorKey(new Color(1, .96f, .65f), 0),
                    new GradientColorKey(new Color(1, .45f, .045f), .45f),
                    new GradientColorKey(new Color(.95f, .09f, .01f), 1)
                }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.9f, .5f), new GradientAlphaKey(0, 1) });
                colour.color = gradient;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = ParticleMat("MissileFlame", true); renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.alignment = ParticleSystemRenderSpace.Local;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(cube);
            }
            // Existing prefab edits survive incremental model/bracket rebuilds.
            launcher.GetComponent<VoxelGunMount>().missileLaunchBurst = ps;
        }
        private static Material ParticleMat(string name, bool glow)
        {
            string path = "Assets/Resources/CarMaterials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetColor("_BaseColor", Color.white); m.SetFloat("_Surface", 1); m.SetFloat("_ZWrite", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(glow ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
            AssetDatabase.CreateAsset(m, path); return m;
        }
        private static void Particles(Transform parent, string name, Material material, bool smoke)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0, -.425f); go.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = true; main.maxParticles = smoke ? 64 : 24;
            main.startLifetime = smoke ? 1.2f : .16f; main.startSpeed = smoke ? .4f : 3f;
            main.startSize = smoke ? .17f : .12f;
            main.simulationSpace = smoke ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startColor = smoke ? new Color(.25f, .27f, .29f, .7f) : new Color(1f, .5f, .06f);
            var emission = ps.emission; emission.rateOverTime = smoke ? 35 : 55;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8; shape.radius = .035f;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, smoke ? 3 : .1f));
            var colour = ps.colorOverLifetime; colour.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); colour.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(cube);
            if (smoke) ConfigureSmoke(ps); else ConfigureFlame(ps);
        }
        public static void ConfigureFlame(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startSize = .45f; main.startLifetime = .30f; main.startSpeed = 6f;
            var shape = ps.shape; shape.radius = .05f;
        }
        // Distance emission keeps the plume continuous even at high vehicle/projectile speeds.
        public static void ConfigureSmoke(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = 1024;
            main.startLifetime = 1.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(.60f, .90f);
            main.startSpeed = .7f;
            // Fewer, slightly narrower puffs with more opaque white cores.
            main.startColor = new Color(1f, 1f, 1f, .80f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.rateOverTime = 16; emission.rateOverDistance = 2f;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 2.6f));
            var velocity = ps.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .65f;
            var colour = ps.colorOverLifetime; colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .45f), new GradientAlphaKey(0, 1) });
            colour.color = gradient;
        }
        [MenuItem("Tools/Voxel Racer/Update Missile Smoke Trail")]
        public static void UpdateSmokeTrail()
        {
            const string path = "Assets/Prefabs/Weapons/Missile.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureSmoke(prefab.transform.Find("Smoke trail").GetComponent<ParticleSystem>());
                ConfigureFlame(prefab.transform.Find("Rocket exhaust").GetComponent<ParticleSystem>());
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
    }
}
