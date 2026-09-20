using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelMineLayerBuilder
    {
        public const string CarPath = "Assets/Prefabs/Cars/BI_MineLayerEnemyCar.prefab";
        [MenuItem("Tools/Voxel Racer/Build Mine Layer Enemy")]
        public static void Build()
        {
            var yellow = Material("MineLayerYellow", new Color(.95f,.67f,.025f));
            var black = Material("MineLayerBlack", new Color(.025f,.029f,.032f));
            var steel = Material("MineLayerSteel", new Color(.22f,.25f,.26f));
            var red = Material("MineLayerWarning", new Color(1f,.08f,.015f));
            var source = AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>("Assets/Resources/EnemyVehicles/BlackInterceptorTuning.asset");
            var root = Object.Instantiate(source.modelPrefab);
            root.name = "BI_MineLayerEnemyCar";
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var p = renderer.transform.localPosition;
                    if (renderer.name == "Voxel steel rim") renderer.sharedMaterial = yellow;
                    else if (renderer.name == "Stepped hubcap") renderer.sharedMaterial = black;
                    if (renderer.name.StartsWith("Number plate") && p.z < 0) Object.DestroyImmediate(renderer.gameObject);
                    else if ((renderer.name == "Roof voxel" || renderer.name == "Body voxel") &&
                        ((Mathf.Abs(p.x) > .15f && Mathf.Abs(p.x) < .5f && p.y > .85f) ||
                         (Mathf.Abs(p.x) > .9f && p.y > .8f && Mathf.Abs(p.z) < .95f))) renderer.sharedMaterial = yellow;
                }
                // Two rails meet the rear bumper and support a square hopper with an open discharge slot.
                foreach (float x in new[]{-.31f,.31f})
                    Block(root.transform,"Dispenser chassis bracket",new Vector3(x,.5f,-2.39f),new Vector3(.12f,.12f,.48f),steel);
                Block(root.transform,"Mine hopper",new Vector3(0,.67f,-2.55f),new Vector3(.84f,.68f,.46f),black);
                Block(root.transform,"Hopper lid",new Vector3(0,1.03f,-2.55f),new Vector3(.9f,.08f,.5f),steel);
                for(int x=0;x<6;x++) for(int y=0;y<4;y++)
                    Block(root.transform,"Hazard chevron voxel",new Vector3((x-2.5f)*.14f,.58f+y*.1f,-2.79f),
                        new Vector3(.14f,.1f,.035f), (x+y)%4<2 ? yellow : black);
                Block(root.transform,"Discharge slot",new Vector3(0,.43f,-2.80f),new Vector3(.59f,.14f,.05f),black);
                foreach(float x in new[]{-.37f,.37f})
                    Block(root.transform,"Outlet guide",new Vector3(x,.36f,-2.73f),new Vector3(.1f,.12f,.35f),steel);
                Block(root.transform,"Ejection tray",new Vector3(0,.305f,-2.75f),new Vector3(.8f,.05f,.4f),steel);
                Block(root.transform,"Dispenser warning lamp",new Vector3(0,1.09f,-2.6f),new Vector3(.14f,.07f,.12f),red);
                PrefabUtility.SaveAsPrefabAsset(root,CarPath);
            }
            finally { Object.DestroyImmediate(root); }
            var mine = new GameObject("BI_RoadMine");
            mine.transform.localScale = new Vector3(2f, 1f, 2f);
            const string minePath = "Assets/Prefabs/Cars/BI_RoadMine.prefab";
            try
            {
                Block(mine.transform,"Mine base",new Vector3(0,.09f,0),new Vector3(.65f,.16f,.48f),black);
                Block(mine.transform,"Mine stepped base",new Vector3(0,.09f,0),new Vector3(.48f,.16f,.65f),black);
                Block(mine.transform,"Pressure plate",new Vector3(0,.19f,0),new Vector3(.43f,.06f,.43f),yellow);
                Block(mine.transform,"Armed indicator",new Vector3(0,.235f,0),new Vector3(.1f,.035f,.1f),red);
                foreach(float x in new[]{-.26f,.26f})
                    Block(mine.transform,"Hazard tab",new Vector3(x,.18f,0),new Vector3(.08f,.03f,.24f),yellow);
                PrefabUtility.SaveAsPrefabAsset(mine,minePath);
            }
            finally { Object.DestroyImmediate(mine); }
            var attack = AssetDatabase.LoadAssetAtPath<VoxelMineLayerTuning>("Assets/Resources/EnemyVehicles/MineLayerAttackTuning.asset");
            if(attack == null) { attack=ScriptableObject.CreateInstance<VoxelMineLayerTuning>(); AssetDatabase.CreateAsset(attack,"Assets/Resources/EnemyVehicles/MineLayerAttackTuning.asset"); }
            attack.minePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(minePath); EditorUtility.SetDirty(attack);
            var enemy = AssetDatabase.LoadAssetAtPath<VoxelEnemyVehicleTuning>("Assets/Resources/EnemyVehicles/BI_MineLayerTuning.asset");
            if(enemy == null) { enemy=Object.Instantiate(source); AssetDatabase.CreateAsset(enemy,"Assets/Resources/EnemyVehicles/BI_MineLayerTuning.asset"); }
            enemy.displayName="BI Mine Layer"; enemy.modelPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(CarPath); enemy.mineLayer=attack;
            EditorUtility.SetDirty(enemy); AssetDatabase.SaveAssets();
            Debug.Log("Mine layer prefab, road mine and tuning assets saved.");
        }
        private static Material Material(string name, Color color)
        {
            string path="Assets/Resources/CarMaterials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.SetColor("_BaseColor",color); mat.SetFloat("_Smoothness",.2f); EditorUtility.SetDirty(mat); return mat;
        }
        private static void Block(Transform parent,string name,Vector3 position,Vector3 size,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=size; go.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
    }
}
