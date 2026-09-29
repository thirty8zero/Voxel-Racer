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
                Block(missile.transform, "Rocket body", Vector3.zero, new Vector3(.14f, .14f, .65f), silver);
                Block(missile.transform, "Warhead", new Vector3(0, 0, .36f), new Vector3(.11f, .11f, .17f), red);
                Block(missile.transform, "Nose tip", new Vector3(0, 0, .47f), new Vector3(.055f, .055f, .08f), red);
                Block(missile.transform, "Horizontal fins", new Vector3(0, 0, -.22f), new Vector3(.32f, .035f, .18f), metal);
                Block(missile.transform, "Vertical fins", new Vector3(0, 0, -.22f), new Vector3(.035f, .32f, .18f), metal);
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
                Block(launcher.transform, "Roof rail", new Vector3(0, .025f, 0), new Vector3(.25f, .07f, .94f), dark);
                Block(launcher.transform, "Rear mount", new Vector3(0, .10f, -.28f), new Vector3(.19f, .15f, .14f), metal);
                Block(launcher.transform, "Front mount", new Vector3(0, .10f, .28f), new Vector3(.19f, .15f, .14f), metal);
                Block(launcher.transform, "Upper casing", new Vector3(0, .34f, 0), new Vector3(.36f, .07f, 1.12f), metal);
                Block(launcher.transform, "Lower casing", new Vector3(0, .14f, 0), new Vector3(.36f, .07f, 1.12f), metal);
                foreach (int sign in new[] { -1, 1 })
                    Block(launcher.transform, "Side casing", new Vector3(sign * .16f, .24f, 0), new Vector3(.06f, .16f, 1.12f), metal);
                Block(launcher.transform, "Tube interior", new Vector3(0, .24f, -.35f), new Vector3(.27f, .14f, .32f), dark);
                Block(launcher.transform, "Loaded missile", new Vector3(0, .24f, .30f), new Vector3(.12f, .12f, .46f), silver);
                Block(launcher.transform, "Loaded nose", new Vector3(0, .24f, .55f), new Vector3(.085f, .085f, .12f), red);
                var muzzle = new GameObject("Missile Muzzle").transform; muzzle.SetParent(launcher.transform, false); muzzle.localPosition = new Vector3(0, .24f, .72f);
                var mount = launcher.AddComponent<VoxelGunMount>(); mount.tuning = weapon; mount.muzzle = muzzle;
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
            go.transform.localPosition = new Vector3(0, 0, -.36f); go.transform.localRotation = Quaternion.LookRotation(Vector3.back);
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
            if (smoke) ConfigureSmoke(ps);
        }
        // Distance emission keeps the plume continuous even at high vehicle/projectile speeds.
        public static void ConfigureSmoke(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = 1024;
            main.startLifetime = 1.8f;
            main.startSize = new ParticleSystem.MinMaxCurve(.65f, .95f);
            main.startSpeed = .7f;
            main.startColor = new Color(.65f, .67f, .69f, .85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.rateOverTime = 30; emission.rateOverDistance = 4;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 2.8f));
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
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        private static void Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
