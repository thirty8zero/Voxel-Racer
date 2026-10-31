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
                EditorGUILayout.HelpBox("Enemy destruction scores are on each enemy vehicle tuning. Crate Rewards below belong to this mission: chance is per destroyed crate; prize weights set the relative odds after a successful roll. Empty sources or all-zero weights award nothing. Cash uses steps of 10, time uses steps of 5; time prizes are excluded after expiry.",MessageType.Info);
            }
            if(target is VoxelDestructionRewards)
                EditorGUILayout.HelpBox("Legacy shared reward reference. Gameplay now reads Crate Rewards on the active mission's Mission Tuning asset; changes here do not affect missions.",MessageType.Info);
            if(target is VoxelObstacleCarTuning)
                EditorGUILayout.HelpBox("Phase speeds are multiples of player top speed (1 = 100%). Civilian traffic maintains a 1.5m following margin in addition to vehicle lengths; blocked spawn positions are skipped.",MessageType.Info);
            if(target is VoxelCivilianTrafficSet)
                EditorGUILayout.HelpBox("This set is shared: edits apply to every track referencing it. Duplicate it in the Project window for a separate vehicle pool or palette.",MessageType.Info);
            base.OnInspectorGUI();
        }
    }
    [CustomEditor(typeof(VoxelGunTuning)),CanEditMultipleObjects] internal sealed class VoxelGunOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelMissileLauncherTuning)),CanEditMultipleObjects] internal sealed class VoxelMissileOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelPloughTuning)),CanEditMultipleObjects] internal sealed class VoxelPloughOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelUnderbodyArmorTuning)),CanEditMultipleObjects] internal sealed class VoxelUnderbodyArmorOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelEngineUpgradeTuning)),CanEditMultipleObjects] internal sealed class VoxelEngineOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelPerformanceWheelTuning)),CanEditMultipleObjects] internal sealed class VoxelWheelOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelWheelSpikeTuning)),CanEditMultipleObjects] internal sealed class VoxelSpikeOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelBoostTuning),true),CanEditMultipleObjects] internal sealed class VoxelBoostOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelMineLayerTuning)),CanEditMultipleObjects] internal sealed class VoxelMineOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelPsychoBugTuning)),CanEditMultipleObjects] internal sealed class VoxelPsychoBugOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelDestructionRewards)),CanEditMultipleObjects] internal sealed class VoxelRewardsOdinEditor:VoxelOdinTuningEditor {}
    [CustomEditor(typeof(VoxelCivilianTrafficSet)),CanEditMultipleObjects] internal sealed class VoxelCivilianTrafficSetOdinEditor:VoxelOdinTuningEditor {}

    public sealed class VoxelOdinTuningLayout : OdinAttributeProcessor<ScriptableObject>
    {
        public static bool IsUpgrade(Type t) => t==typeof(VoxelGunTuning)||t==typeof(VoxelArmorTuning)||t==typeof(VoxelEngineUpgradeTuning)||
            t==typeof(VoxelMissileLauncherTuning)||t==typeof(VoxelPloughTuning)||t==typeof(VoxelUnderbodyArmorTuning)||t==typeof(VoxelPerformanceWheelTuning)||t==typeof(VoxelWheelSpikeTuning)||t==typeof(VoxelBoostUpgradeTuning);
        public static bool Supports(Type t) => VoxelContentOdinLayout.Supports(t)||IsUpgrade(t)||typeof(VoxelBoostTuning).IsAssignableFrom(t)||t==typeof(VoxelCarTuning)||
            t==typeof(VoxelEnemyVehicleTuning)||t==typeof(VoxelObstacleCarTuning)||t==typeof(VoxelCivilianTrafficSet)||t==typeof(VoxelMissionTuning)||t==typeof(VoxelMineLayerTuning)||t==typeof(VoxelPsychoBugTuning)||t==typeof(VoxelDesertFoliageTuning)||t==typeof(VoxelDestructionRewards);
        public override void ProcessChildMemberAttributes(InspectorProperty parent,MemberInfo member,List<Attribute> attributes)
        {
            var type=parent.ValueEntry.TypeOfValue;
            if(!Supports(type) || !(member is FieldInfo) || attributes.OfType<HideInInspector>().Any()) return;
            string name=member.Name;
            string group=VoxelTrackOdinLayout.Section(member.DeclaringType,name);
            if(VoxelContentOdinLayout.Supports(type)) group=VoxelContentOdinLayout.Configure(type,name,group,attributes);
            if(IsUpgrade(type)) group=UpgradeSection(type,name,group);
            if (type == typeof(VoxelMissileLauncherTuning) && name == "weapon")
            { group = "Weapon Settings"; attributes.Add(new InlineEditorAttribute()); }
            if (type == typeof(VoxelGunTuning) && name.StartsWith("missile") && name != "missileCooldownSeconds")
            { group = "Missile Flight"; attributes.Add(new ShowIfAttribute("@projectileKind == VoxelRacer.VoxelProjectileKind.Missile")); }
            if (type == typeof(VoxelGunTuning) && (name == "useMissileCooldown" || name == "missileCooldownSeconds"))
            {
                group = "Missile Cooldown";
                if (name == "useMissileCooldown") attributes.Add(new ShowIfAttribute("@projectileKind == VoxelRacer.VoxelProjectileKind.Missile"));
                else attributes.Add(new ShowIfAttribute("@projectileKind == VoxelRacer.VoxelProjectileKind.Missile && useMissileCooldown"));
            }
            if (type == typeof(VoxelGunTuning) && name == "shotsPerSecond")
                attributes.Add(new HideIfAttribute("@projectileKind == VoxelRacer.VoxelProjectileKind.Missile && useMissileCooldown"));
            if(type==typeof(VoxelBoostTuning) && group=="Settings")group="Boost Performance & Effects";
            if(typeof(VoxelBoostTuning).IsAssignableFrom(type) &&
                (name=="boostRamDamageBonusPercent" || name=="boostRamDamageWithPloughBonusPercent"))
                group="Boost Ram Damage";
            if(type==typeof(VoxelObstacleCarTuning))
            {
                group="Spawning & Density";
                if(name=="enemyVehicleSpawns"||name=="enemyCarSpawnChance"||name=="enemyDroughtWaveLimit")group="Enemy Vehicles";
                else if(name.StartsWith("sameDirection"))group="Movement/Same Direction";
                else if(name.StartsWith("oncoming"))group="Movement/Oncoming";
                else if(name.Contains("SpeedDistance")||name=="wheelSpinDegreesPerUnit")group="Movement/Phase Boundaries";
                else if(name=="civilianTrafficSet")
                {
                    group="Civilian Models & Colours";
                    attributes.Add(new InlineEditorAttribute(InlineEditorModes.GUIOnly));
                }
                else if(name=="staticObstacleSpawns")group="Static Obstacles";
                else if(name.Contains("Damage")||name.Contains("collision")||name.Contains("launch")||name=="destroyedLifetime")group="Collisions & Damage";
            }
            if(type==typeof(VoxelMissionTuning))
            {
                if(name=="crateRewards") attributes.Add(new LabelTextAttribute("Reward Sources"));
                if(name.Contains("MultiplierPenalty"))group="Multiplier/Penalties";
                else if(name=="enemyVoxelMultiplier"||name=="enemyDestroyedMultiplier"||name=="barrelMultiplier"||name=="nearMissMultiplier")group="Multiplier/Gains";
                else if(name=="maximumTimeMultiplier"||name=="timeBonusCurrencyMultiplier"||name=="bonusWarningSeconds")group="Multiplier/Limits & Timer";
                else if(group.Contains("Score")||group=="Civilian Penalties")group="Scoring/"+group;
            }
            if(type==typeof(VoxelDestructionRewards))group="Crate Reward Sources";
            if(type==typeof(VoxelDesertFoliageTuning) && name.StartsWith("horizon"))
            {
                group="Road Horizon Ground Cover";
                if(name!="horizonGroundCoverEnabled") attributes.Add(new ShowIfAttribute("horizonGroundCoverEnabled"));
            }
            if(type==typeof(VoxelEnemyVehicleTuning) && name=="mineLayer")
            {group="Mine Laying";attributes.Add(new InlineEditorAttribute());}
            if(type==typeof(VoxelEnemyVehicleTuning) && name=="psychoBug")
            {group="Advanced Driving";attributes.Add(new InlineEditorAttribute());}
            if(type==typeof(VoxelEnemyVehicleTuning) && (group=="Movement" || group=="Evasive Lane Change"))
                attributes.Add(new HideIfAttribute("@psychoBug != null"));
            if(type==typeof(VoxelPsychoBugTuning))
            {
                if(group=="Side Ram" && name!="sideRamEnabled") attributes.Add(new ShowIfAttribute("sideRamEnabled"));
                if(group=="Shooting Windows" && name!="shootingWindows") attributes.Add(new ShowIfAttribute("shootingWindows"));
                if(group=="Damage Evasion" && name!="damageEvasion") attributes.Add(new ShowIfAttribute("damageEvasion"));
                if(name=="maximumReverseSpeed") attributes.Add(new ShowIfAttribute("allowReverse"));
                if(name=="ramReturnSpeedFraction") attributes.Add(new HideIfAttribute("fastRamReturn"));
                if(name=="ramReturnClosingSpeed" || name=="ramReturnSlowdownDistance" || name=="ramReturnClosingResponse" || name=="ramReturnAcceleration")
                    attributes.Add(new ShowIfAttribute("fastRamReturn"));
            }
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
        // Shared by the Odin Inspector and the dashboard's native serialized-property editor.
        internal static string UpgradeSection(Type type,string name,string section)
        {
            if(name=="displayName")return "Identity";
            if(name.Contains("Price")||name=="maximumPurchases"||name=="upgradeLevel")return "Purchase";
            if(type==typeof(VoxelBoostUpgradeTuning) && name=="bottleCount")return "Boost Charges";
            if(typeof(VoxelBoostTuning).IsAssignableFrom(type) &&
                (name=="boostRamDamageBonusPercent"||name=="boostRamDamageWithPloughBonusPercent"))return "Boost Ram Damage";
            if(type==typeof(VoxelMissileLauncherTuning) && name=="weapon")return "Weapon Settings";
            if(type==typeof(VoxelGunTuning) && (name=="useMissileCooldown"||name=="missileCooldownSeconds"))return "Missile Cooldown";
            if(type==typeof(VoxelGunTuning) && name.StartsWith("missile"))return "Missile Flight";
            if(name.Contains("Prefab"))return "Model & Compatibility";
            if(name.IndexOf("mount",StringComparison.OrdinalIgnoreCase)>=0)return "Fit & Mounting";
            if(name.Contains("Bonus")||name.Contains("Reduction")||name=="voxelHitPoints")return "Performance & Damage";
            return section;
        }
        internal static bool UpgradeFieldVisible(SerializedObject serialized,string name)
        {
            if(!(serialized.targetObject is VoxelGunTuning))return true;
            bool missile=serialized.FindProperty("projectileKind").enumValueIndex==(int)VoxelProjectileKind.Missile;
            bool cooldown=serialized.FindProperty("useMissileCooldown").boolValue;
            if(name=="shotsPerSecond")return !missile||!cooldown;
            if(name=="useMissileCooldown")return missile;
            if(name=="missileCooldownSeconds")return missile&&cooldown;
            if(name.StartsWith("missile"))return missile;
            return true;
        }
        internal static string Unit(string name)
        {
            string n=name.ToLowerInvariant();
            if(n.Contains("percent"))return " (%)";
            if(n.Contains("chance"))return " (0–1)";
            if(n.EndsWith("seconds"))return " (s)";
            if(n.Contains("multiplier")||n.Contains("fraction"))return " (×)";
            if(n.Contains("duration")||n.Contains("interval")||n.Contains("seconds")||n.Contains("delay")||n.Contains("lifetime")||n.Contains("cooldown")||n=="boostlength")return " (s)";
            if(n=="acceleration"||n=="braking"||n=="brakingforce"||n=="ramreturnacceleration"||n=="ramreturnbraking"||n=="rearramescapeacceleration")return " (m/s²)";
            if(n=="ramreturnclosingspeed")return " (m/s)";
            if(n=="topspeed"||n=="boostspeed"||n=="projectilespeed"||n=="lanechangespeed"||n=="distanceadjustmentspeed"||n=="minimumdrivingspeed"||n=="minimumramspeed")return " (m/s)";
            if(n.Contains("distance")||n.Contains("radius")||n=="collisionhalfwidth"||n=="collisionhalflength")return " (m)";
            return "";
        }
    }
    public sealed class VoxelRewardEntryOdinLayout:OdinAttributeProcessor<VoxelDestructionRewards.Entry>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parent,MemberInfo member,List<Attribute> attributes)
        {
            if(!(member is FieldInfo))return;
            string group=member.Name.EndsWith("Weight")?"Prize Odds":member.Name.Contains("Cash")?"Cash Prize":member.Name.Contains("Time")?"Timer Prize":member.Name=="multiplierBonus"?"Multiplier Prize":"Source & Chance";
            attributes.Add(new FoldoutGroupAttribute(group));
            if(member.Name.EndsWith("Weight")) attributes.Add(new SuffixLabelAttribute("weight"));
            else VoxelOdinTuningLayout.AddRangeAndUnits(typeof(VoxelDestructionRewards.Entry),member.Name,group,attributes);
        }
    }
}
