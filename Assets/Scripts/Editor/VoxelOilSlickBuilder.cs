using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelOilSlickBuilder
    {
        [MenuItem("Tools/Voxel Racer/Create Oil Slick and Road Texture")]
        public static void Build()
        {
            const string oilPath = "Assets/Resources/StaticObstacles/OilSlick.asset";
            var oil = AssetDatabase.LoadAssetAtPath<VoxelStaticObstacleDefinition>(oilPath);
            if (oil == null)
            {
                oil = ScriptableObject.CreateInstance<VoxelStaticObstacleDefinition>();
                oil.displayName = "Oil Slick";
                oil.obstacleType = VoxelStaticObstacleType.OilSlick;
                oil.oilSpinDuration = 1.1f;
                oil.oilTireMarkLifetime = 8f;
                AssetDatabase.CreateAsset(oil, oilPath);
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Road")) AssetDatabase.CreateFolder("Assets/Resources", "Road");
            const string materialPath = "Assets/Resources/Road/AsphaltTemplate.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
            {
                var shader = Shader.Find("Voxel Racer/Road Asphalt");
                if (shader == null) throw new System.Exception("Asphalt shader did not import.");
                AssetDatabase.CreateAsset(new Material(shader) { name = "AsphaltTemplate" }, materialPath);
            }
            foreach (var track in Resources.LoadAll<VoxelTrackDefinition>("Tracks"))
            {
                // Only migrate the old default; retain deliberately authored track colours on later runs.
                if (track.roadColour == new Color(.1f, .12f, .16f))
                {
                    track.roadColour = new Color(.29f, .29f, .32f);
                    EditorUtility.SetDirty(track);
                }
                var traffic = track.obstacleCarTuning;
                if (traffic == null) continue;
                var entries = new List<VoxelStaticObstacleSpawnEntry>(traffic.staticObstacleSpawns ??
                    track.staticObstacleSpawns ?? new VoxelStaticObstacleSpawnEntry[0]);
                if (entries.Any(e => e != null && e.obstacle == oil)) continue;
                entries.Add(new VoxelStaticObstacleSpawnEntry { obstacle = oil, spawnWeight = .7f });
                traffic.staticObstacleSpawns = entries.ToArray();
                EditorUtility.SetDirty(traffic);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Oil Slick asset created/registered in track traffic; lighter asphalt defaults and shader template ready.");
        }
    }
}
