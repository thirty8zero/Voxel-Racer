using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelRacer
{
    /// <summary>A small, one-shot burst of timber slivers with no per-splinter objects or colliders.</summary>
    public static class VoxelWoodSplinters
    {
        private const float BurstScale = 4f;
        private static Material material;
        private static Mesh mesh;

        public static ParticleSystem Play(Vector3 position, Vector3 impactDirection)
        {
            if (mesh == null) mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"))
                { name = "Wood Splinter Particles", enableInstancing = true };
                material.SetColor("_BaseColor", Color.white);
            }

            var root = new GameObject("Crate Wood Splinters");
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(-90f, 0f, 0f));
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = .1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = 28;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.55f, .95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * BurstScale, 7f * BurstScale);
            main.gravityModifier = 1.2f * BurstScale;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(.025f * BurstScale, .05f * BurstScale);
            main.startSizeY = new ParticleSystem.MinMaxCurve(.025f * BurstScale, .05f * BurstScale);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(.18f * BurstScale, .38f * BurstScale);
            main.startRotation3D = true;
            main.startRotationX = main.startRotationY = main.startRotationZ =
                new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.42f, .22f, .08f), new Color(.9f, .68f, .36f));

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = .55f * BurstScale;
            var drift = particles.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            Vector3 push = impactDirection.normalized * (2.5f * BurstScale);
            drift.x = push.x; drift.y = push.y; drift.z = push.z;
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            rotation.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.7f, 1f), new Keyframe(1f, 0f)));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
            return particles;
        }
    }
}
