using UnityEngine;
namespace VoxelRacer
{
    /// <summary>A single short-lived mesh-particle burst, without per-fragment objects or colliders.</summary>
    public static class VoxelSpikeImpact
    {
        private static Mesh cubeMesh;
        private static Material material;
        private static ParticleSystem ramSparkPrefab;

        internal static ParticleSystem PlaySideRamSparks(Transform van, Vector3 contact, float side, float duration, EndlessVoxelRoad road) =>
            PlayRamSparks(van, contact, van.right * side, duration, road, bossSideRam: true);

        internal static void PlayVehicleRamSparks(Transform vehicle, Vector3 contact, Vector3 hitDirection, EndlessVoxelRoad road)
        {
            if (!Application.isPlaying) return;
            // The hit direction points into the other vehicle. A hit into its nose is frontal,
            // including head-on collisions with oncoming traffic.
            if (Vector3.Dot(hitDirection, vehicle.forward) < -Mathf.Abs(Vector3.Dot(hitDirection, vehicle.right))) return;

            Vector3 outward = Vector3.ProjectOnPlane(-hitDirection, Vector3.up).normalized;
            contact.y = Mathf.Max(contact.y, vehicle.position.y + .2f);
            PlayRamSparks(vehicle, contact, outward, .14f, road, bossSideRam: false);
        }

        private static ParticleSystem PlayRamSparks(Transform vehicle, Vector3 contact, Vector3 normal, float duration,
            EndlessVoxelRoad road, bool bossSideRam)
        {
            if (ramSparkPrefab == null)
                ramSparkPrefab = Resources.Load<ParticleSystem>("Effects/YellowVoxelImpactSparks");
            if (ramSparkPrefab == null) return null;

            Quaternion rotation = Quaternion.LookRotation(normal * .85f - vehicle.forward + Vector3.up * .10f);
            // Ordinary bursts stay at the contact even if the victim immediately tumbles.
            Transform parent = !bossSideRam && road != null ? road.transform : vehicle;
            var sparks = Object.Instantiate(ramSparkPrefab, contact + normal * .06f, rotation, parent);
            sparks.name = bossSideRam ? "Boss Side Ram Sparks" : "Vehicle Ram Sparks";
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.duration = duration;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = bossSideRam ? 96 : 24;
            main.startSpeed = bossSideRam ? new ParticleSystem.MinMaxCurve(8f, 19f) : new ParticleSystem.MinMaxCurve(6f, 13f);
            float size = bossSideRam ? 1f : .65f;
            main.startSize = new ParticleSystem.MinMaxCurve(.08f * size, .23f * size);
            main.startLifetime = new ParticleSystem.MinMaxCurve(.16f, .34f);
            main.gravityModifier = 4f;
            var shape = sparks.shape;
            shape.angle = bossSideRam ? 65f : 60f;
            var emission = sparks.emission;
            emission.rateOverTime = bossSideRam ? 180f : 60f;
            emission.burstCount = 1;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, bossSideRam ? (short)32 : (short)12));
            if (road != null)
            {
                // The road is flat even through turns. One plane per road stays level if the van rolls over.
                const string planeName = "Side Ram Spark Bounce Plane";
                Transform plane = road.transform.Find(planeName);
                if (plane == null)
                {
                    plane = new GameObject(planeName).transform;
                    plane.SetParent(road.transform, false);
                    plane.SetPositionAndRotation(vehicle.position, Quaternion.identity);
                }

                var collision = sparks.collision;
                collision.type = ParticleSystemCollisionType.Planes;
                collision.SetPlane(0, plane);
                collision.bounce = .42f;
                collision.dampen = .12f;
                collision.radiusScale = .5f;
                collision.sendCollisionMessages = false;
                collision.enabled = true;
            }
            sparks.Play();
            return sparks;
        }

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
