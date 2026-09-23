using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace VoxelRacer.Editor
{
    public static class VoxelPlayerDamageEffectsValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Player Damage Effects")]
        public static void Run()
        {
            var root=new GameObject("Temporary player effects validation");
            try
            {
                var car=root.AddComponent<VoxelCarController>();car.enabled=false;
                var pieces=new GameObject[4];
                for(int i=0;i<4;i++)
                {
                    pieces[i]=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pieces[i].transform.SetParent(root.transform,false);
                }
                car.ResetIntegrityBaseline();
                var effects=root.AddComponent<VoxelVehicleDamageEffects>();effects.Configure();
                var update=typeof(VoxelVehicleDamageEffects).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
                void Verify(bool smoke,bool fire)
                {
                    update.Invoke(effects,null);
                    if(effects.SmokeActive!=smoke || effects.FireActive!=fire)throw new Exception("Incorrect effects at "+car.IntegrityPercent+"% health");
                }
                Verify(false,false);
                pieces[0].SetActive(false);Verify(false,false);
                pieces[1].SetActive(false);Verify(true,false);
                pieces[2].SetActive(false);Verify(true,true);
                foreach(var ps in root.GetComponentsInChildren<ParticleSystem>())
                {ps.Simulate(.5f,true,true,false);if(ps.particleCount==0)throw new Exception("Effect did not emit");}
                foreach(var piece in pieces)piece.SetActive(true);Verify(false,false);
                if(car.TotalIntegrityVoxels!=4)throw new Exception("Particles affected player integrity");
                Debug.Log("PASS player smoke/fire: actual integrity 100/75/50/25%, emission, repaired health and unchanged integrity baseline.");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
