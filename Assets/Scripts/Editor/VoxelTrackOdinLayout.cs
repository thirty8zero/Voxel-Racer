using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    // Editor-only presentation: existing Unity fields and subassets remain the source of truth.
    public sealed class VoxelTrackOdinLayout : OdinAttributeProcessor<VoxelTrackDefinition>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parent,MemberInfo member,List<Attribute> attributes)
        {
            if(!(member is FieldInfo) || attributes.OfType<HideInInspector>().Any()) return;
            attributes.RemoveAll(a=>a is HeaderAttribute);
            string name=member.Name;
            string group=name=="roadTuning"?"Road":name=="obstacleCarTuning"?"Traffic":
                name=="missionTuning"?"Mission":name=="boss"||name=="bossEncounter"?"Boss":
                name=="displayName"||name=="raceSceneName"?"General":"Environment";
            attributes.Add(new FoldoutGroupAttribute(group));
            if(group=="Environment")
                attributes.Add(new FoldoutGroupAttribute("Environment/"+Section(typeof(VoxelTrackDefinition),name)));
            if(name=="bossEncounter") {attributes.Add(new ShowIfAttribute("@boss != null"));attributes.Add(new HideLabelAttribute());attributes.Add(new InlinePropertyAttribute());}
            if(name=="roadTuning" || name=="obstacleCarTuning")
            {
                attributes.Add(new InlineEditorAttribute(InlineEditorModes.FullEditor,InlineEditorObjectFieldModes.Hidden));
                attributes.Add(new HideLabelAttribute());
            }
            if(name=="missionTuning" || name=="scenerySet") attributes.Add(new InlineEditorAttribute());
            if(name.StartsWith("fog") && name!="fogEnabled") attributes.Add(new ShowIfAttribute("fogEnabled"));
            if(name.StartsWith("groundNoise")) attributes.Add(new ShowIfAttribute("groundPixelNoiseEnabled"));
            if(name.StartsWith("sun")) attributes.Add(new ShowIfAttribute("horizonSunEnabled"));
            if(name.StartsWith("mountain") || name=="minimumMountainPeakHeight" || name=="maximumMountainPeakHeight")
                attributes.Add(new ShowIfAttribute("horizonMountainsEnabled"));
        }

        internal static string Section(Type type,string field)
        {
            string section="Settings";
            foreach(var member in type.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.MetadataToken))
            {
                var header=member.GetCustomAttribute<HeaderAttribute>();
                if(header!=null) section=header.header;
                if(member.Name==field) return section;
            }
            return section;
        }
    }

    public sealed class VoxelBossEncounterOdinLayout : OdinAttributeProcessor<VoxelBossEncounterSettings>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parent,MemberInfo member,List<Attribute> attributes)
        {
            if(!(member is FieldInfo)) return;
            attributes.RemoveAll(a=>a is HeaderAttribute);
            if(member.Name=="radarInterceptorSpawnChance") attributes.Add(new LabelTextAttribute("Radar Interceptor Spawn Chance (%)"));
            string group=VoxelTrackOdinLayout.Section(typeof(VoxelBossEncounterSettings),member.Name);
            attributes.Add(new FoldoutGroupAttribute(group));
            VoxelOdinTuningLayout.AddRangeAndUnits(typeof(VoxelBossEncounterSettings),member.Name,group,attributes);
        }
    }

    public sealed class VoxelBossDefinitionOdinLayout : OdinAttributeProcessor<VoxelBossDefinition>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parent,MemberInfo member,List<Attribute> attributes)
        {
            if(!(member is FieldInfo)) return;
            attributes.RemoveAll(a=>a is HeaderAttribute);
            string group=VoxelTrackOdinLayout.Section(typeof(VoxelBossDefinition),member.Name);
            attributes.Add(new FoldoutGroupAttribute(group));
            VoxelOdinTuningLayout.AddRangeAndUnits(typeof(VoxelBossDefinition),member.Name,group,attributes);
            if(member.Name=="catchUpDistance")
            {
                attributes.Add(new InfoBoxAttribute("Catch-up starts inside the preferred combat range, so it interrupts the full distance cycle.",InfoMessageType.Warning,"@catchUpDistance < maximumDistanceAhead"));
                attributes.Add(new InfoBoxAttribute("Catch-up distance must be greater than resume distance and no greater than escape warning distance.",InfoMessageType.Warning,"@catchUpDistance <= catchUpResumeDistance || catchUpDistance > escapeWarningDistance"));
            }
            if(member.Name=="escapeFailureDistance") attributes.Add(new InfoBoxAttribute("Failure distance must be greater than warning distance.",InfoMessageType.Warning,"@escapeFailureDistance <= escapeWarningDistance"));
            if(member.Name=="minimumDistanceAhead") attributes.Add(new InfoBoxAttribute("Minimum combat distance must be below the catch-up resume distance.",InfoMessageType.Warning,"@minimumDistanceAhead >= catchUpResumeDistance"));
        }
    }

    public sealed class VoxelBossSpikeAttackOdinLayout : OdinAttributeProcessor<VoxelBossSpikeAttackTuning>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parent,MemberInfo member,List<Attribute> attributes)
        {
            if(!(member is FieldInfo)) return;
            attributes.RemoveAll(a=>a is HeaderAttribute);
            string group=VoxelTrackOdinLayout.Section(typeof(VoxelBossSpikeAttackTuning),member.Name);
            attributes.Add(new FoldoutGroupAttribute(group));
            VoxelOdinTuningLayout.AddRangeAndUnits(typeof(VoxelBossSpikeAttackTuning),member.Name,group,attributes);
            if(member.Name=="retreatDistance") attributes.Add(new InfoBoxAttribute("Retreat distance is capped 5m below the boss escape warning distance.",InfoMessageType.Info));
        }
    }

    public sealed class VoxelBossMineAttackOdinLayout : OdinAttributeProcessor<VoxelBossMineAttackTuning>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parent,MemberInfo member,List<Attribute> attributes)
        {
            if(!(member is FieldInfo)) return;
            attributes.RemoveAll(a=>a is HeaderAttribute);
            string group=VoxelTrackOdinLayout.Section(typeof(VoxelBossMineAttackTuning),member.Name);
            attributes.Add(new FoldoutGroupAttribute(group));
            VoxelOdinTuningLayout.AddRangeAndUnits(typeof(VoxelBossMineAttackTuning),member.Name,group,attributes);
            if(member.Name=="mineTuning") attributes.Add(new InlineEditorAttribute());
            if(member.Name.StartsWith("fallback") && member.Name!="mineTuning")
                attributes.Add(new HideIfAttribute("@mineTuning != null"));
        }
    }
}
