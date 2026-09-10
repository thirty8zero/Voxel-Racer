using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelEngineExhaustValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Engine Exhaust")]
        public static void Run()
        {
            var root = new GameObject("Temporary Engine Exhaust Validation");
            try
            {
                var car = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Cars/SpyCar2PlayerCar.prefab"), root.transform);
                var controller = root.AddComponent<VoxelCarController>();
                controller.enabled = false;
                controller.ResetIntegrityBaseline();
                int integrity = controller.TotalIntegrityVoxels;
                var boost = root.AddComponent<VoxelBoostController>();
                boost.enabled = false;
                boost.Configure(controller, Resources.Load<VoxelBoostTuning>("Boost/DefaultBoostTuning"));
                var original = car.GetComponentInChildren<VoxelEngineExhaustOutlet>();
                Check(original != null && original.GetComponentInParent<VoxelIndestructiblePart>() != null,
                    "Missing protected engine outlet");
                Check(original.transform.parent.parent.name == "Protected Engine", "Exhaust is not engine-owned");
                Verify(original);

                // Simulate a replacement engine with three differently positioned/oriented outlets.
                original.transform.parent.gameObject.SetActive(false);
                var replacement = new GameObject("Temporary Replacement Engine");
                replacement.transform.SetParent(car.transform, false);
                replacement.AddComponent<VoxelIndestructiblePart>();
                for (int i = 0; i < 3; i++)
                {
                    var tip = new GameObject("Outlet " + i).AddComponent<VoxelEngineExhaustOutlet>();
                    tip.transform.SetParent(replacement.transform, false);
                    tip.transform.localPosition = new Vector3((i - 1) * .4f, .3f, -2.6f);
                    tip.transform.localRotation = Quaternion.Euler(0, 160 + i * 20, 0);
                }
                var rebuild = typeof(VoxelBoostController).GetMethod("EnsureExhaustFireEffects",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                rebuild.Invoke(boost, null);
                rebuild.Invoke(boost, null);
                foreach (var tip in replacement.GetComponentsInChildren<VoxelEngineExhaustOutlet>()) Verify(tip);
                Check(root.GetComponentsInChildren<ParticleSystem>(true).Length == 3,
                    "Old engine effects remained or replacement outlets were capped");
                Check(controller.TotalIntegrityVoxels == integrity, "Exhaust affected integrity");
                Debug.Log("PASS: engine exhaust ownership, protection, exact emission position/direction, engine replacement, multiple outlets and no duplicate effects.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Verify(VoxelEngineExhaustOutlet outlet)
        {
            var effects = outlet.GetComponentsInChildren<ParticleSystem>();
            Check(effects.Length == 1, "Expected one effect per outlet");
            Check((effects[0].transform.position - outlet.transform.position).sqrMagnitude < .000001f,
                "Effect is not at pipe tip");
            Check(Vector3.Dot(effects[0].transform.forward, outlet.transform.forward) > .999f,
                "Effect direction does not follow outlet");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
