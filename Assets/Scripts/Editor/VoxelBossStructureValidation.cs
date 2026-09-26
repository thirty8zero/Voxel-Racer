using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace VoxelRacer.Editor
{
    public static class VoxelBossStructureValidation
    {
        public static void Run()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VoxelRedVanBossBuilder.PrefabPath));
            var definition = ScriptableObject.CreateInstance<VoxelBossDefinition>();
            try
            {
                var structure = root.transform.Find(VoxelBossStructureBuilder.StructureName);
                if (structure == null || structure.GetComponent<VoxelIndestructiblePart>() == null) throw new Exception("Missing protected structure");
                if (structure.GetComponentsInChildren<MeshRenderer>().Length != 1 || structure.GetComponentsInChildren<Collider>().Length != 0) throw new Exception("Structure should use one renderer without colliders");
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.GetComponentInParent<VoxelIndestructiblePart>() == null) renderer.gameObject.SetActive(false);
                var enemy = root.AddComponent<VoxelEnemyCar>(); enemy.enabled = false;
                typeof(VoxelEnemyCar).GetField("bossSettings", flags).SetValue(enemy, definition);
                typeof(VoxelEnemyCar).GetField("<CurrentHealth>k__BackingField", flags).SetValue(enemy, 100f);
                enemy.TakeProjectileHit(structure, 50f, Vector3.zero, Vector3.forward);
                if (enemy.CurrentHealth != 100f || !structure.gameObject.activeSelf) throw new Exception("Direct hit damaged structure");
                if (enemy.TryGetNextProjectileVoxel(new Vector3(0, 1, -10), Vector3.forward, 20, out _)) throw new Exception("Structure is a projectile target");
                int removed = (int)typeof(VoxelEnemyCar).GetMethod("ApplyVoxelDamage", flags).Invoke(enemy, new object[] { Vector3.zero, Vector3.forward, 9999 });
                if (removed != 0 || !structure.gameObject.activeSelf) throw new Exception("Ram damage removed structure");
                typeof(VoxelEnemyCar).GetMethod("CheckBossBodyDestroyed", flags).Invoke(enemy, null);
                if (enemy.CurrentHealth != 0) throw new Exception("Structure prevents boss body defeat");
                Debug.Log("PASS boss structure: protected direct hits, projectile targeting, ram removal, body defeat; one renderer and no colliders.");
            }
            finally { Object.DestroyImmediate(root);Object.DestroyImmediate(definition); }
        }
        public static void Render()
        {
            for (int view = 0; view < 2; view++)
            {
                var preview = new PreviewRenderUtility();
                try
                {
                    var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VoxelRedVanBossBuilder.PrefabPath));
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                        if (renderer.GetComponentInParent<VoxelIndestructiblePart>() == null && !renderer.transform.parent.name.Contains("Wheel")) renderer.enabled = false;
                    preview.AddSingleGO(root);
                    preview.camera.transform.position = view == 0 ? new Vector3(6, 4.5f, -7) : new Vector3(5, -3, 7);
                    preview.camera.transform.LookAt(new Vector3(0, 1, 0));
                    preview.camera.fieldOfView = 34; preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 50;
                    preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.14f,.17f,.20f);
                    preview.lights[0].intensity = 1.8f; preview.lights[0].transform.rotation = Quaternion.Euler(view == 0 ? 40 : -40, 25, 0);
                    preview.lights[1].intensity = 1.3f; preview.lights[1].transform.rotation = Quaternion.Euler(30, 210, 0);
                    preview.ambientColor = new Color(.5f,.5f,.5f);
                    preview.BeginStaticPreview(new Rect(0,0,1100,750)); preview.Render(true);
                    var image = preview.EndStaticPreview(); File.WriteAllBytes("Temp/VanditoBossStructure" + view + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
                }
                finally { preview.Cleanup(); }
            }
        }
    }
}
