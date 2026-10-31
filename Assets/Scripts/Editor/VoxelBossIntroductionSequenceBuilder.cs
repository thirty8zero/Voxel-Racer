using System;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelBossIntroductionSequenceBuilder
    {
        [MenuItem("Tools/Voxel Racer/Bosses/Create Missing Introduction Sequences")]
        public static void CreateMissing()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
            Create("ElBichoLocoBoss", "ElBichoLocoIntroduction", "LocoIntroSequence", "02 Blue Strip.png", "03 Car Model.png", 1, new Vector2(-20, -50), 1, Vector2.zero);
            Create("RedVanBoss", "ElVanditoIntroduction", "VanditoIntroSequence", "02 Yellow Strip.png", "03 Van Model.png", .9f, new Vector2(70, -10), .72f, new Vector2(-222, 114));
            AssetDatabase.SaveAssets();
        }

        private static void Create(string bossName, string assetName, string folder, string strip, string model,
            float modelScale, Vector2 modelPosition, float titleScale, Vector2 titlePosition)
        {
            var boss = Resources.Load<VoxelBossDefinition>("Bosses/" + bossName);
            if (boss == null) throw new InvalidOperationException("Missing boss " + bossName);
            string path = "Assets/Resources/Bosses/" + assetName + ".asset";
            var sequence = AssetDatabase.LoadAssetAtPath<VoxelBossIntroductionSequence>(path);
            if (sequence == null)
            {
                var names = new[] { "01 Dark Background.png", strip, model, "04 Title.png" };
                var textures = new Texture2D[4];
                for (int i = 0; i < 4; i++)
                {
                    textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/" + folder + "/" + names[i]);
                    if (textures[i] == null) throw new InvalidOperationException("Missing layer " + names[i]);
                }
                sequence = ScriptableObject.CreateInstance<VoxelBossIntroductionSequence>();
                sequence.slideInDuration = boss.introductionSlideInDuration;
                sequence.holdDuration = boss.introductionHoldDuration;
                sequence.slideOutDuration = boss.introductionSlideOutDuration;
                sequence.shakeDuration = boss.introductionShakeDuration;
                sequence.shakePixels = boss.introductionShakePixels;
                sequence.panels = new[]
                {
                    new VoxelBossIntroductionPanel { label = "Dark Background", texture = textures[0], fillScreen = true, drawOrder = 0 },
                    new VoxelBossIntroductionPanel { label = "Scenery Strip", texture = textures[1], drawOrder = 1 },
                    new VoxelBossIntroductionPanel { label = "Boss Model", texture = textures[2], drawOrder = 2, scale = modelScale, position = modelPosition },
                    new VoxelBossIntroductionPanel { label = "Title", texture = textures[3], drawOrder = 3, scale = titleScale, position = titlePosition }
                };
                AssetDatabase.CreateAsset(sequence, path);
            }
            if (boss.introductionSequence == null)
            {
                boss.introductionSequence = sequence;
                EditorUtility.SetDirty(boss);
            }
        }
    }
}
