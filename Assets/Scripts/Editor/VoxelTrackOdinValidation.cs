using System;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
namespace VoxelRacer.Editor
{
    public static class VoxelTrackOdinValidation
    {
        public static void Run()
        {
            var tracks=AssetDatabase.FindAssets("t:VoxelTrackDefinition")
                .Select(g=>AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            foreach(var track in tracks)
            {
                string before=EditorJsonUtility.ToJson(track);
                using(var tree=PropertyTree.Create(track))
                {
                    tree.UpdateTree();
                    var properties=tree.EnumerateTree(true).ToArray();
                    foreach(var field in typeof(VoxelTrackDefinition).GetFields())
                    {
                        if(Attribute.IsDefined(field,typeof(HideInInspector))) continue;
                        if(!properties.Any(p=>p.Name==field.Name)) throw new Exception("Missing Track field: "+field.Name);
                    }
                    if(!properties.Any(p=>p.Attributes.OfType<FoldoutGroupAttribute>().Any())) throw new Exception("Odin foldout attributes not applied");
                }
                if(EditorJsonUtility.ToJson(track)!=before) throw new Exception("Inspector modified Track data");
            }
            var editor=UnityEditor.Editor.CreateEditor(tracks.Cast<UnityEngine.Object>().ToArray());
            try { if(!(editor is OdinEditor))throw new Exception("Track editor is not using Odin"); }
            finally {UnityEngine.Object.DestroyImmediate(editor);}
            Debug.Log("PASS Odin Track layouts: all visible fields retained, foldouts discovered, multiple-selection editor created, asset values unchanged across "+tracks.Length+" tracks.");
        }
    }
}
