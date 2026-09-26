using UnityEditor;
using UnityEngine;
namespace VoxelRacer.Editor
{
    public static class VoxelBossMineBuilder
    {
        public const string PrefabPath="Assets/Resources/Bosses/VanditoBossMine.prefab";
        public const string TuningPath="Assets/Resources/Bosses/VanditoBossMineTuning.asset";
        public const string AttackTuningPath=VoxelRedVanBossBuilder.MineAttackPath;
        [MenuItem("Tools/Voxel Racer/Build Vandito Boss Mine")]
        public static void Build()
        {
            var standard=Resources.Load<VoxelMineLayerTuning>("EnemyVehicles/MineLayerAttackTuning");
            const string materialPath="Assets/Resources/Bosses/VanditoBossMine.mat";
            var red=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(red==null) {red=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(red,materialPath);}
            red.SetColor("_BaseColor",new Color(1f,.015f,.025f));EditorUtility.SetDirty(red);
                var root=new GameObject("Vandito Boss Mine");
            try
            {
                var body=Object.Instantiate(standard.minePrefab,root.transform,false);
                body.name="Double size mine body";body.transform.localScale*=2;
                foreach(var renderer in body.GetComponentsInChildren<MeshRenderer>())
                    if(renderer.sharedMaterial.name.Contains("Yellow"))renderer.sharedMaterial=red;
                // Small stepped points keep the voxel silhouette while making the perimeter threatening.
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI/4;
                    var centre=new Vector3(Mathf.Cos(angle)*.92f,.35f,Mathf.Sin(angle)*.92f);
                    for(int step=0;step<3;step++)
                    {
                        var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.name="Red spike step";
                        Object.DestroyImmediate(block.GetComponent<Collider>());block.transform.SetParent(root.transform,false);
                        block.transform.localPosition=centre+Vector3.up*(step*.105f);
                        float width=.26f-step*.08f;block.transform.localScale=new Vector3(width,.11f,width);
                        block.GetComponent<MeshRenderer>().sharedMaterial=red;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally {Object.DestroyImmediate(root);}
            var track=AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(VoxelRedVanBossBuilder.TrackPath);
            var tuning=AssetDatabase.LoadAssetAtPath<VoxelMineLayerTuning>(TuningPath);
            var attack=AssetDatabase.LoadAssetAtPath<VoxelBossMineAttackTuning>(AttackTuningPath);
            if(attack==null)
            {
                attack=ScriptableObject.CreateInstance<VoxelBossMineAttackTuning>();
                attack.name="VanditoBossMineAttack";
                attack.dropChance=.6f;attack.dropInterval=3;attack.armingDelay=.5f;attack.lifetime=25;
                AssetDatabase.CreateAsset(attack,AttackTuningPath);
            }
            if(tuning==null)
            {
                tuning=Object.Instantiate(standard);tuning.name="VanditoBossMineTuning";
                // Enlarge only the mine portion of the swept hit area, preserving the player's footprint allowance.
                tuning.collisionHalfWidth=standard.collisionHalfWidth+.65f;
                tuning.collisionHalfLength=standard.collisionHalfLength+.65f;
                AssetDatabase.CreateAsset(tuning,TuningPath);
            }
            tuning.minePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            attack.mineTuning=tuning;
            track.boss.SetAttack(attack);
            EditorUtility.SetDirty(tuning);EditorUtility.SetDirty(attack);EditorUtility.SetDirty(track.boss);EditorUtility.SetDirty(track);AssetDatabase.SaveAssets();
            var preview=new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(Object.Instantiate(tuning.minePrefab));
                preview.camera.transform.position=new Vector3(3,3,4);preview.camera.transform.LookAt(new Vector3(0,.2f,0));
                preview.camera.fieldOfView=38;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=20;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.16f,.18f,.21f);
                preview.lights[0].intensity=2;preview.lights[0].transform.rotation=Quaternion.Euler(45,30,0);
                preview.lights[1].intensity=1;preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,900,650));preview.Render(true);
                var image=preview.EndStaticPreview();System.IO.File.WriteAllBytes("Temp/VanditoBossMine.png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally {preview.Cleanup();}
        }
    }
}
