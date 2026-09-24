using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public class VoxelOdinTuningEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            if(target is VoxelArmorTuning)
                EditorGUILayout.HelpBox("Each door is purchased separately. Each panel voxel adds one integrity unit; voxel health controls the damage it absorbs.",MessageType.Info);
            if(VoxelOdinTuningLayout.IsUpgrade(target.GetType()) && GUILayout.Button("Open Upgrade Fit Preview"))
                VoxelUpgradeFitPreview.Open((ScriptableObject)target);
            if(target is VoxelMissionTuning)
            {
                EditorGUILayout.HelpBox("Enemy destruction scores are on each enemy vehicle tuning. Crate rewards use the shared DestructionRewards asset.",MessageType.Info);
                if(GUILayout.Button("Open Shared Crate Rewards")) Selection.activeObject=Resources.Load<VoxelDestructionRewards>("DestructionRewards");
            }
            if(target is VoxelDestructionRewards)
                EditorGUILayout.HelpBox("Prize chance is per destroyed source. Eligible prizes are equally likely. Cash uses multiples of 10; timer rewards use 5-second steps and are unavailable after time expires.",MessageType.Info);
            if(target is VoxelObstacleCarTuning)
                EditorGUILayout.HelpBox("Phase speeds are multiples of player top speed (1 = 100%). Civilian traffic maintains a 1.5m following margin in addition to vehicle lengths; blocked spawn positions are skipped.",MessageType.Info);
            base.OnInspectorGUI();
        }
    }
    [CustomEditor(typeof(VoxelGunTuning)),CanEditMultipleObjects] internal sealed class VoxelGunOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelEngineUpgradeTuning)),CanEditMultipleObjects] internal sealed class VoxelEngineOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelPerformanceWheelTuning)),CanEditMultipleObjects] internal sealed class VoxelWheelOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelWheelSpikeTuning)),CanEditMultipleObjects] internal sealed class VoxelSpikeOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelBoostTuning),true),CanEditMultipleObjects] internal sealed class VoxelBoostOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelMineLayerTuning)),CanEditMultipleObjects] internal sealed class VoxelMineOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelDestructionRewards)),CanEditMultipleObjects] internal sealed class VoxelRewardsOdinEditor:VoxelOdinTuningEditor {}

    public sealed class VoxelOdinTuningLayout : OdinAttributeProcessor<ScriptableObject>
    {
        public static bool IsUpgrade(Type t) => t==typeof(VoxelGunTuning)||t==typeof(VoxelArmorTuning)||t==typeof(VoxelEngineUpgradeTuning)||
            t==typeof(VoxelPerformanceWheelTuning)||t==typeof(VoxelWheelSpikeTuning)||t==typeof(VoxelBoostUpgradeTuning);
        public static bool Supports(Type t) => VoxelContentOdinLayout.Supports(t)||IsUpgrade(t)||typeof(VoxelBoostTuning).IsAssignableFrom(t)||t==typeof(VoxelCarTuning)||
            t==typeof(VoxelEnemyVehicleTuning)||t==typeof(VoxelObstacleCarTuning)||t==typeof(VoxelMissionTuning)||t==typeof(VoxelMineLayerTuning)||t==typeof(VoxelDestructionRewards);
        public override void ProcessChildMemberAttributes(InspectorProperty parent,MemberInfo member,List<Attribute> attributes)
        {
            var type=parent.ValueEntry.TypeOfValue;
            if(!Supports(type) || !(member is FieldInfo) || attributes.OfType<HideInInspector>().Any()) return;
            string name=member.Name;
            string group=VoxelTrackOdinLayout.Section(member.DeclaringType,name);
            if(VoxelContentOdinLayout.Supports(type)) group=VoxelContentOdinLayout.Configure(type,name,group,attributes);
            if(IsUpgrade(type))
            {
                if(name=="displayName")group="Identity";
                else if(name.Contains("Price")||name=="maximumPurchases")group="Purchase";
                else if(name.Contains("Prefab"))group="Model & Compatibility";
                else if(name.IndexOf("mount",StringComparison.OrdinalIgnoreCase)>=0)group="Fit & Mounting";
                else if(name.Contains("Bonus")||name=="voxelHitPoints")group="Performance & Damage";
            }
            if(type==typeof(VoxelBoostTuning) && group=="Settings")group="Boost Performance & Effects";
            if(type==typeof(VoxelObstacleCarTuning))
            {
                group="Spawning & Density";
                if(name.StartsWith("sameDirection"))group="Movement/Same Direction";
                else if(name.StartsWith("oncoming"))group="Movement/Oncoming";
                else if(name.Contains("SpeedDistance")||name=="wheelSpinDegreesPerUnit")group="Movement/Phase Boundaries";
                else if(name=="paintColours"||name.Contains("EnemyTuning")||name=="semiTrailerSpawnChance")group="Civilian Models & Colours";
                else if(name=="staticObstacleSpawns")group="Static Obstacles";
                else if(name.Contains("Damage")||name.Contains("collision")||name.Contains("launch")||name=="destroyedLifetime")group="Collisions & Damage";
            }
            if(type==typeof(VoxelMissionTuning))
            {
                if(name.Contains("MultiplierPenalty"))group="Multiplier/Penalties";
                else if(name=="enemyVoxelMultiplier"||name=="enemyDestroyedMultiplier"||name=="barrelMultiplier"||name=="nearMissMultiplier")group="Multiplier/Gains";
                else if(name=="maximumTimeMultiplier"||name=="timeBonusCurrencyMultiplier"||name=="bonusWarningSeconds")group="Multiplier/Limits & Timer";
                else if(group.Contains("Score")||group=="Civilian Penalties")group="Scoring/"+group;
            }
            if(type==typeof(VoxelDestructionRewards))group="Crate Reward Sources";
            if(type==typeof(VoxelEnemyVehicleTuning) && name=="mineLayer")
            {group="Mine Laying";attributes.Add(new InlineEditorAttribute());}
            attributes.RemoveAll(a=>a is HeaderAttribute);
            AddGroups(group,attributes);
            AddRangeAndUnits(type,name,group,attributes);
            var inspectorName=member.GetCustomAttribute<InspectorNameAttribute>();
            if(inspectorName!=null) {attributes.RemoveAll(a=>a is LabelTextAttribute);attributes.Add(new LabelTextAttribute(inspectorName.displayName));}
        }
        internal static void AddGroups(string group,List<Attribute> attributes)
        {
            int split=group.IndexOf('/');
            if(split>0)attributes.Add(new FoldoutGroupAttribute(group.Substring(0,split)));
            attributes.Add(new FoldoutGroupAttribute(group));
        }
        internal static void AddRangeAndUnits(Type type,string name,string group,List<Attribute> attributes)
        {
            string minimum=null,maximum=null;
            if(name.StartsWith("minimum")){minimum=name;maximum="maximum"+name.Substring(7);}
            else if(name.StartsWith("maximum")){maximum=name;minimum="minimum"+name.Substring(7);}
            else if(name.Contains("Min")){minimum=name;maximum=name.Replace("Min","Max");}
            else if(name.Contains("Max")){maximum=name;minimum=name.Replace("Max","Min");}
            string unit=Unit(name);
            if(minimum!=null && type.GetField(minimum)!=null && type.GetField(maximum)!=null)
            {
                string title=ObjectNames.NicifyVariableName(minimum.StartsWith("minimum")?minimum.Substring(7):minimum.Replace("Min",""));
                string box=group+"/"+title+unit;
                attributes.Add(new BoxGroupAttribute(box));attributes.Add(new HorizontalGroupAttribute(box+"/Values"));
                attributes.Add(new LabelTextAttribute(name==minimum?"Min":"Max"));attributes.Add(new LabelWidthAttribute(35));
                if(name==maximum)attributes.Add(new InfoBoxAttribute("Maximum must be at least minimum.",InfoMessageType.Warning,"@"+maximum+" < "+minimum));
            }
            else if(unit!="")attributes.Add(new LabelTextAttribute(ObjectNames.NicifyVariableName(name)+unit));
        }
        private static string Unit(string name)
        {
            string n=name.ToLowerInvariant();
            if(n.Contains("percent"))return " (%)";
            if(n.Contains("chance"))return " (0–1)";
            if(n.Contains("multiplier")||n.Contains("fraction"))return " (×)";
            if(n.Contains("duration")||n.Contains("interval")||n.Contains("seconds")||n.Contains("delay")||n.Contains("lifetime")||n.Contains("cooldown")||n=="boostlength")return " (s)";
            if(n=="acceleration"||n=="braking"||n=="brakingforce")return " (m/s²)";
            if(n=="topspeed"||n=="boostspeed"||n=="projectilespeed"||n=="lanechangespeed"||n=="distanceadjustmentspeed")return " (m/s)";
            if(n.Contains("distance")||n.Contains("radius")||n=="collisionhalfwidth"||n=="collisionhalflength")return " (m)";
            return "";
        }
    }
    public sealed class VoxelRewardEntryOdinLayout:OdinAttributeProcessor<VoxelDestructionRewards.Entry>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parent,MemberInfo member,List<Attribute> attributes)
        {
            if(!(member is FieldInfo))return;
            string group=member.Name.Contains("Cash")?"Cash Prize":member.Name.Contains("Time")?"Timer Prize":member.Name=="multiplierBonus"?"Multiplier Prize":"Source & Chance";
            attributes.Add(new FoldoutGroupAttribute(group));
            VoxelOdinTuningLayout.AddRangeAndUnits(typeof(VoxelDestructionRewards.Entry),member.Name,group,attributes);
        }
    }
}
