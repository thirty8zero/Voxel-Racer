using System.Linq;
using UnityEditor;
using UnityEngine;
namespace VoxelRacer.Editor
{
    public static class VoxelRedVanBossBuilder
    {
        public const string PrefabPath="Assets/Resources/Bosses/RedTransitBoss.prefab";
        public const string DefinitionPath="Assets/Resources/Bosses/RedVanBoss.asset";
        public const string SpikeAttackPath="Assets/Resources/Bosses/RedVanSpikeAttack.asset";
        public const string MineAttackPath="Assets/Resources/Bosses/RedVanMineAttack.asset";
        public const string TrackPath="Assets/Resources/Tracks/TrackBoss01.asset";
        [MenuItem("Tools/Voxel Racer/Build Red Van Boss")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/Bosses");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/CivilianTransitVan.prefab");
            var root=Object.Instantiate(source);root.name="Vandito Boss Van";
            try
            {
                var marker=root.GetComponent<VoxelTrafficPaint>();
                var bodyMaterial=marker.bodyMaterial;
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
                VoxelBossStructureBuilder.AddStructure(root);
                VoxelBossSpikeBuilder.AddRig(root);
                VoxelBossVoxelDetailBuilder.Apply(root);
                VoxelBossEyesBuilder.AddEyes(root);
                VoxelVanditoBossAppearanceBuilder.Apply(root,bodyMaterial);
                VoxelBossBodyFinishingBuilder.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally {Object.DestroyImmediate(root);}
            bool newTrack=AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(TrackPath)==null;
            if(newTrack) AssetDatabase.CopyAsset("Assets/Resources/Tracks/Track01.asset",TrackPath);
            var track=AssetDatabase.LoadAssetAtPath<VoxelTrackDefinition>(TrackPath);
            var boss=AssetDatabase.LoadAssetAtPath<VoxelBossDefinition>(DefinitionPath);
            if(boss==null)
            {
                boss=ScriptableObject.CreateInstance<VoxelBossDefinition>();
                boss.name="VanditoBoss";
                AssetDatabase.CreateAsset(boss,DefinitionPath);
            }
            boss.bossPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var spikeAttack=AssetDatabase.LoadAssetAtPath<VoxelBossSpikeAttackTuning>(SpikeAttackPath);
            if(spikeAttack==null)
            {
                spikeAttack=ScriptableObject.CreateInstance<VoxelBossSpikeAttackTuning>();
                spikeAttack.name="VanditoSpikeAttack";
                AssetDatabase.CreateAsset(spikeAttack,SpikeAttackPath);
            }
            var mineAttack=AssetDatabase.LoadAssetAtPath<VoxelBossMineAttackTuning>(MineAttackPath);
            if(mineAttack==null)
            {
                mineAttack=ScriptableObject.CreateInstance<VoxelBossMineAttackTuning>();
                mineAttack.name="VanditoMineAttack";
                AssetDatabase.CreateAsset(mineAttack,MineAttackPath);
            }
            mineAttack.mineTuning=AssetDatabase.LoadAssetAtPath<VoxelMineLayerTuning>(VoxelBossMineBuilder.TuningPath);
            boss.SetAttack(spikeAttack);boss.SetAttack(mineAttack);
            track.displayName="Red Menace Boss";track.boss=boss;
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
            EditorUtility.SetDirty(boss);EditorUtility.SetDirty(spikeAttack);EditorUtility.SetDirty(mineAttack);EditorUtility.SetDirty(track);AssetDatabase.SaveAssets();
            Debug.Log("Vandito boss prefab and TrackBoss01 ready at the end of the campaign sequence.");
        }
    }
}
