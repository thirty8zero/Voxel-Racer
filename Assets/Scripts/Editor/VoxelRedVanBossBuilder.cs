using System.Linq;
using UnityEditor;
using UnityEngine;
namespace VoxelRacer.Editor
{
    public static class VoxelRedVanBossBuilder
    {
        public const string PrefabPath="Assets/Resources/Bosses/RedTransitBoss.prefab";
        public const string TrackPath="Assets/Resources/Tracks/TrackBoss01.asset";
        [MenuItem("Tools/Voxel Racer/Build Red Van Boss")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/Bosses");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/CivilianTransitVan.prefab");
            var root=Object.Instantiate(source);root.name="Red Transit Boss";
            try
            {
                const string matPath="Assets/Resources/Bosses/RedBossPaint.mat";
                var paint=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                var marker=root.GetComponent<VoxelTrafficPaint>();
                if(paint==null){paint=new Material(marker.bodyMaterial);AssetDatabase.CreateAsset(paint,matPath);}
                paint.SetColor("_BaseColor",new Color(.68f,.025f,.018f));EditorUtility.SetDirty(paint);
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                    if(renderer.sharedMaterial==marker.bodyMaterial) renderer.sharedMaterial=paint;
                Object.DestroyImmediate(marker);
                var layer=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/BI_MineLayerEnemyCar.prefab");
                string[] names={"Mine hopper","Hopper lid","Hazard chevron voxel","Discharge slot","Outlet guide","Ejection tray","Dispenser warning lamp","Dispenser chassis bracket"};
                foreach(int side in new[]{-1,1})
                {
                    var mount=new GameObject(side<0?"Left Mine Dispenser":"Right Mine Dispenser").transform;
                    mount.SetParent(root.transform,false);mount.localPosition=new Vector3(side*(2.23f/3.6f),.27f,-2.5f);
                    mount.localScale=Vector3.one*.75f;
                    foreach(var piece in layer.transform.Cast<Transform>().Where(t=>names.Contains(t.name)))
                    {
                        var copy=Object.Instantiate(piece.gameObject,mount,false);
                        copy.transform.localPosition=piece.localPosition+Vector3.forward*2.55f;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally {Object.DestroyImmediate(root);}
            bool newTrack=AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(TrackPath)==null;
            if(newTrack) AssetDatabase.CopyAsset("Assets/Resources/Tracks/Track01.asset",TrackPath);
            var track=AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(TrackPath);
            track.displayName="Red Menace Boss";track.isBossLevel=true;track.boss ??= new VoxelBossSettings();
            track.boss.bossPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(newTrack && track.missionTuning!=null)
            {
                var mission=Object.Instantiate(track.missionTuning);mission.name="Boss Mission Rewards";
                mission.displayName="Red Menace";mission.timeLimitSeconds=150;
                AssetDatabase.AddObjectToAsset(mission,track);track.missionTuning=mission;
            }
            var sequence=VoxelTrackSequence.Load();
            if(sequence!=null && !(sequence.tracks ?? new VoxelTrackDefinition[0]).Contains(track))
            {
                sequence.tracks=(sequence.tracks ?? new VoxelTrackDefinition[0]).Append(track).ToArray();
                EditorUtility.SetDirty(sequence);
            }
            EditorUtility.SetDirty(track);AssetDatabase.SaveAssets();
            Debug.Log("Red van boss prefab and TrackBoss01 ready at the end of the campaign sequence.");
        }
    }
}
