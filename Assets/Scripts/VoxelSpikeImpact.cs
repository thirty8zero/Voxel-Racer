using UnityEngine;
namespace VoxelRacer
{
    /// <summary>A single short-lived mesh-particle burst, without per-fragment objects or colliders.</summary>
    public static class VoxelSpikeImpact
    {
        private static Mesh cubeMesh;
        private static Material material;
        public static void Play(Vector3 position, Vector3 forward, float vehicleSpeed)
        {
            if(cubeMesh == null) cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if(material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                material.name = "Spike Impact Debris";
                material.SetColor("_BaseColor", Color.white);
                material.enableInstancing = true;
            }
            var root = new GameObject("Boss Spike Impact Debris");
            root.transform.position = position + Vector3.up * .35f;
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.playOnAwake = false; main.duration = .1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f,2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.16f,.48f);
            main.gravityModifier = 1.4f; main.maxParticles = 64;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var rotation = particles.rotationOverLifetime; rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-7,7); rotation.y = new ParticleSystem.MinMaxCurve(-9,9);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = cubeMesh;
            renderer.sharedMaterial = material;
            particles.Play();
            Vector3 right = Vector3.Cross(Vector3.up,forward).normalized;
            for(int i=0;i<56;i++)
            {
                float grey = Random.Range(.45f,1f);
                particles.Emit(new ParticleSystem.EmitParams {
                    position = root.transform.position + right * Random.Range(-1.1f,1.1f),
                    velocity = forward * (Mathf.Max(0,vehicleSpeed)*.7f-Random.Range(5,14)) + right*Random.Range(-11f,11f) + Vector3.up*Random.Range(7f,16f),
                    startColor = i%5==0 ? new Color(1f,.55f,.12f) : new Color(grey,grey,grey),
                    rotation3D = Random.insideUnitSphere*180f
                },1);
            }
            if(Application.isPlaying) Object.Destroy(root,2.5f);
        }
    }
}
