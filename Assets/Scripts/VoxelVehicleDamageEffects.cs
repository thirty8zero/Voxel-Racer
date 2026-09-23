using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Health-driven, world-space smoke and fire for player and traffic vehicles.</summary>
    public sealed class VoxelVehicleDamageEffects : MonoBehaviour
    {
        [Range(0,1)] public float smokeDamageThreshold=.25f;
        [Range(0,1)] public float fireDamageThreshold=.5f;
        public Vector3 windVelocity=new Vector3(.7f,0,.25f);
        private VoxelEnemyCar enemy;
        private VoxelObstacleCar civilian;
        private VoxelCarController player;
        private ParticleSystem smoke,fire;
        private Vector3 previousPosition;
        private static Material particleMaterial;
        public bool SmokeActive {get;private set;}
        public bool FireActive {get;private set;}

        public void Configure(bool truck=false)
        {
            enemy=GetComponent<VoxelEnemyCar>();civilian=GetComponent<VoxelObstacleCar>();
            player=GetComponent<VoxelCarController>();
            if(player!=null)
            {
                smokeDamageThreshold=.5f;
                fireDamageThreshold=.75f;
            }
            previousPosition=transform.position;
            if(smoke!=null) return;
            var origin=new Vector3(0,truck?1.05f:.95f,truck?3.35f:1.15f);
            smoke=Create("Damage Smoke",origin,false);
            fire=Create("Damage Fire",origin+Vector3.up*.15f,true);
        }
        private ParticleSystem Create(string name,Vector3 origin,bool flame)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=origin;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=true;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=flame?48:72;
            main.startLifetime=flame?new ParticleSystem.MinMaxCurve(.55f,.85f):new ParticleSystem.MinMaxCurve(1.4f,2.1f);
            main.startSpeed=0;main.startSize=flame?new ParticleSystem.MinMaxCurve(.65f,1.05f):new ParticleSystem.MinMaxCurve(.55f,.85f);
            main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            var emission=ps.emission;emission.rateOverTime=flame?48:30;emission.enabled=false;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=flame?.3f:.32f;
            var noise=ps.noise;noise.enabled=true;noise.strength=flame?.25f:.4f;noise.frequency=.65f;noise.scrollSpeed=.45f;noise.quality=ParticleSystemNoiseQuality.Low;
            var size=ps.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,flame?new AnimationCurve(new Keyframe(0,.8f),new Keyframe(.25f,1.15f),new Keyframe(1,0)):AnimationCurve.Linear(0,.75f,1,2.4f));
            var colour=ps.colorOverLifetime;colour.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(flame?new[]{new GradientColorKey(new Color(1,.95f,.5f),0),new GradientColorKey(new Color(1,.55f,.04f),.45f),new GradientColorKey(new Color(.9f,.16f,.02f),1)}:
                new[]{new GradientColorKey(new Color(.58f,.59f,.60f),0),new GradientColorKey(new Color(.78f,.79f,.8f),1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(flame?1:.85f,.08f),new GradientAlphaKey(flame?.9f:.65f,.6f),new GradientAlphaKey(0,1)});
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
            float health=player!=null?Mathf.Clamp01(player.IntegrityPercent*.01f):enemy!=null?enemy.HealthPercent:civilian!=null && civilian.EnemyTuning!=null?
                Mathf.Clamp01(civilian.CurrentHealth/Mathf.Max(.001f,civilian.EnemyTuning.vehicleHealth)):1;
            SetDamageFraction(1-health);
            var velocity=Time.deltaTime>0?(transform.position-previousPosition)/Time.deltaTime:Vector3.zero;
            previousPosition=transform.position;
            velocity=Vector3.ClampMagnitude(velocity,70);
            Drift(smoke,windVelocity-velocity*.12f+Vector3.up*1.2f);
            Drift(fire,windVelocity*.5f-velocity*.08f+Vector3.up*2.2f);
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
        private void OnEnable()
        {
            previousPosition=transform.position;
            if(smoke!=null)smoke.Play();
            if(fire!=null)fire.Play();
        }
    }
}
