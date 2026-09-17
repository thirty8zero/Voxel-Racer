using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Health-driven, world-space smoke and fire for traffic vehicles.</summary>
    public sealed class VoxelVehicleDamageEffects : MonoBehaviour
    {
        [Range(0,1)] public float smokeDamageThreshold=.25f;
        [Range(0,1)] public float fireDamageThreshold=.5f;
        public Vector3 windVelocity=new Vector3(.7f,0,.25f);
        private VoxelEnemyCar enemy;
        private VoxelObstacleCar civilian;
        private ParticleSystem smoke,fire;
        private Vector3 previousPosition;
        private static Material particleMaterial;
        public bool SmokeActive {get;private set;}
        public bool FireActive {get;private set;}

        public void Configure(bool truck=false)
        {
            enemy=GetComponent<VoxelEnemyCar>();civilian=GetComponent<VoxelObstacleCar>();
            previousPosition=transform.position;
            if(smoke!=null) return;
            var origin=new Vector3(0,truck?1.05f:.95f,truck?3.35f:1.15f);
            smoke=Create("Damage Smoke",origin,false);
            fire=Create("Damage Fire",origin,true);
        }
        private ParticleSystem Create(string name,Vector3 origin,bool flame)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=origin;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=true;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=flame?28:40;
            main.startLifetime=flame?new ParticleSystem.MinMaxCurve(.3f,.55f):new ParticleSystem.MinMaxCurve(1.1f,1.8f);
            main.startSpeed=0;main.startSize=flame?new ParticleSystem.MinMaxCurve(.18f,.35f):new ParticleSystem.MinMaxCurve(.3f,.55f);
            main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            var emission=ps.emission;emission.rateOverTime=flame?30:16;emission.enabled=false;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=flame?.18f:.25f;
            var noise=ps.noise;noise.enabled=true;noise.strength=flame?.25f:.4f;noise.frequency=.65f;noise.scrollSpeed=.45f;noise.quality=ParticleSystemNoiseQuality.Low;
            var size=ps.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,flame?AnimationCurve.Linear(0,1,1,0):AnimationCurve.Linear(0,.5f,1,2.4f));
            var colour=ps.colorOverLifetime;colour.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(flame?new[]{new GradientColorKey(new Color(1,.8f,.18f),0),new GradientColorKey(new Color(1,.23f,.025f),.5f),new GradientColorKey(new Color(.22f,.07f,.025f),1)}:
                new[]{new GradientColorKey(new Color(.18f,.17f,.16f),0),new GradientColorKey(new Color(.36f,.34f,.31f),1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(flame?1:.65f,.12f),new GradientAlphaKey(0,1)});
            colour.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();
            if(particleMaterial==null)
                particleMaterial=new Material(Resources.Load<Shader>("Effects/VehicleDamageParticle")) {name="Vehicle Damage Particles"};
            renderer.sharedMaterial=particleMaterial;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            ps.Play();return ps;
        }
        private void LateUpdate()
        {
            if(smoke==null) return;
            float health=enemy!=null?enemy.HealthPercent:civilian!=null && civilian.EnemyTuning!=null?
                Mathf.Clamp01(civilian.CurrentHealth/Mathf.Max(.001f,civilian.EnemyTuning.vehicleHealth)):1;
            SetDamageFraction(1-health);
            var velocity=Time.deltaTime>0?(transform.position-previousPosition)/Time.deltaTime:Vector3.zero;
            previousPosition=transform.position;
            velocity=Vector3.ClampMagnitude(velocity,70);
            Drift(smoke,windVelocity-velocity*.12f+Vector3.up*1.2f);
            Drift(fire,windVelocity*.5f-velocity*.08f+Vector3.up*1.5f);
        }
        public void SetDamageFraction(float damage)
        {
            SmokeActive=damage>=smokeDamageThreshold;FireActive=damage>=fireDamageThreshold;
            if(smoke!=null) {var e=smoke.emission;e.enabled=SmokeActive;}
            if(fire!=null) {var e=fire.emission;e.enabled=FireActive;}
        }
        private static void Drift(ParticleSystem ps,Vector3 velocity)
        {
            var module=ps.velocityOverLifetime;module.enabled=true;module.space=ParticleSystemSimulationSpace.World;
            module.x=velocity.x;module.y=velocity.y;module.z=velocity.z;
        }
        private void OnDisable()
        {
            if(smoke!=null)smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(fire!=null)fire.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
