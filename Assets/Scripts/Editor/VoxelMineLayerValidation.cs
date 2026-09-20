using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelMineLayerValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Mine Layer")]
        public static void Run()
        {
            var tuning=Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BI_MineLayerTuning");
            if(tuning==null || tuning.mineLayer==null || tuning.mineLayer.minePrefab==null) throw new Exception("Missing mine layer assets");
            if(tuning.modelPrefab.GetComponentsInChildren<Collider>().Length!=0) throw new Exception("Unexpected visual colliders");
            if(!VoxelRoadMine.CrossesMine(new Vector2(0,-20),new Vector2(0,20),Vector2.zero,Vector2.one)) throw new Exception("Fast crossing missed");
            if(VoxelRoadMine.CrossesMine(new Vector2(3,-20),new Vector2(3,20),Vector2.zero,Vector2.one)) throw new Exception("Adjacent lane false hit");
            if(VoxelRoadMine.CrossesMine(new Vector2(0,-20),new Vector2(0,-10),Vector2.zero,Vector2.one)) throw new Exception("Distant false hit");
            if(!VoxelRoadMine.CrossesMine(Vector2.zero,Vector2.zero,Vector2.zero,Vector2.one)) throw new Exception("Stationary overlap missed");
            var preview=new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(UnityEngine.Object.Instantiate(tuning.modelPrefab));
                preview.camera.transform.position=new Vector3(5.7f,3.4f,-7.5f);
                preview.camera.transform.LookAt(new Vector3(0,.65f,-.25f));
                preview.camera.fieldOfView=34; preview.camera.nearClipPlane=.1f; preview.camera.farClipPlane=50;
                preview.camera.clearFlags=CameraClearFlags.SolidColor; preview.camera.backgroundColor=new Color(.17f,.20f,.23f);
                preview.lights[0].intensity=1.8f; preview.lights[0].transform.rotation=Quaternion.Euler(40,160,0);
                preview.lights[1].intensity=1.3f; preview.lights[1].transform.rotation=Quaternion.Euler(30,20,0);
                preview.ambientColor=new Color(.45f,.45f,.45f);
                preview.BeginStaticPreview(new Rect(0,0,1280,800)); preview.Render(true);
                var image=preview.EndStaticPreview(); File.WriteAllBytes("Temp/BI_MineLayerEnemyCar.png",image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
            File.WriteAllText("Temp/MineLayerValidation.txt","PASS: asset references, no visual colliders, fast crossing, adjacent lane, distant exclusion, stationary overlap; preview rendered.");
            Debug.Log("Mine layer validation passed.");
        }
    }
}
