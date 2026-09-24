using System;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
namespace VoxelRacer.Editor
{
    public static class VoxelOdinTuningValidation
    {
        public static void Run()
        {
            int count=0;
            foreach(string guid in AssetDatabase.FindAssets("t:ScriptableObject",new[]{"Assets/Resources"}))
            foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<ScriptableObject>())
            {
                if(!VoxelOdinTuningLayout.Supports(asset.GetType()))continue;
                string before=EditorJsonUtility.ToJson(asset);
                using(var tree=PropertyTree.Create(asset))
                {
                    tree.UpdateTree();var properties=tree.EnumerateTree(true).ToArray();
                    foreach(var field in asset.GetType().GetFields())
                    {
                        if(field.IsStatic || Attribute.IsDefined(field,typeof(HideInInspector)))continue;
                        var property=properties.FirstOrDefault(p=>p.Name==field.Name);
                        if(property==null)throw new Exception(asset.name+": missing "+field.Name);
                        if(!property.Attributes.OfType<FoldoutGroupAttribute>().Any())throw new Exception(asset.name+": ungrouped "+field.Name);
                    }
                }
                var editor=UnityEditor.Editor.CreateEditor(asset);
                try {if(!(editor is VoxelOdinTuningEditor))throw new Exception(asset.name+": wrong editor "+editor.GetType().Name);}
                finally{UnityEngine.Object.DestroyImmediate(editor);}
                if(before!=EditorJsonUtility.ToJson(asset))throw new Exception(asset.name+": values changed");
                count++;
            }
            VoxelTrackOdinValidation.Run();
            Debug.Log("PASS Odin layouts across "+count+" tuning assets: fields, foldouts, custom editors, unchanged serialization; Track layouts also passed.");
        }
    }
}
