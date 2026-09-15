using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelPixelSkyBuilder
    {
        private const string Folder="Assets/Resources/Scenery/";
        [MenuItem("Tools/Voxel Racer/Bake Pixel Cloud Sky")]
        public static void Bake()
        {
            var source=AssetDatabase.LoadAssetAtPath<Material>(Folder+"DesertPixelSkySource.mat");
            if(source==null)
            {
                source=new Material(Shader.Find("Voxel Racer/Pixel Cloud Sky"));
                source.SetFloat("_CloudCoverage",.8f);
                source.SetFloat("_CloudDetail",.9f);
                AssetDatabase.CreateAsset(source,Folder+"DesertPixelSkySource.mat");
            }
            if(ShaderUtil.ShaderHasError(source.shader)) throw new System.Exception("Pixel sky shader has compilation errors");
            var root=new GameObject("Temporary Pixel Sky Baker");
            var camera=root.AddComponent<Camera>(); camera.enabled=false; camera.cullingMask=0;
            camera.clearFlags=CameraClearFlags.Skybox; root.AddComponent<Skybox>().material=source;
            var cube=new Cubemap(512,TextureFormat.RGB24,false) { name="Desert Pixel Clouds",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp };
            try
            {
                if(!camera.RenderToCubemap(cube)) throw new System.Exception("Sky cubemap rendering failed");
                var savedCube=AssetDatabase.LoadAssetAtPath<Cubemap>(Folder+"DesertPixelClouds.asset");
                if(savedCube==null) { AssetDatabase.CreateAsset(cube,Folder+"DesertPixelClouds.asset"); savedCube=cube; }
                else { EditorUtility.CopySerialized(cube,savedCube); Object.DestroyImmediate(cube); }
                var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"DesertPixelSky.mat");
                if(material==null) { material=new Material(Shader.Find("Skybox/Cubemap")); AssetDatabase.CreateAsset(material,Folder+"DesertPixelSky.mat"); }
                material.shader=Shader.Find("Skybox/Cubemap"); material.SetTexture("_Tex",savedCube);
                material.SetColor("_Tint",new Color(.5f,.5f,.5f)); material.SetFloat("_Exposure",1);
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
                root.GetComponent<Skybox>().material=material;
                camera.fieldOfView=65; camera.transform.rotation=Quaternion.Euler(-24,0,0);
                var rt=new RenderTexture(1200,700,24); var previous=RenderTexture.active;
                var image=new Texture2D(1200,700,TextureFormat.RGB24,false);
                try
                {
                    camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                    image.ReadPixels(new Rect(0,0,1200,700),0,0); image.Apply();
                    File.WriteAllBytes("Temp/DesertPixelSky.png",image.EncodeToPNG());
                }
                finally { camera.targetTexture=null; RenderTexture.active=previous; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image); }
                Debug.Log("Pixel sky baked to 512px cubemap faces; runtime uses a single cubemap lookup.");
            }
            finally { Object.DestroyImmediate(root); if(cube!=null && !AssetDatabase.Contains(cube)) Object.DestroyImmediate(cube); }
        }
    }
}
