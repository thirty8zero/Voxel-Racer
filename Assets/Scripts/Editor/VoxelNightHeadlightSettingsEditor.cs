using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    [CustomEditor(typeof(VoxelNightHeadlightSettings))]
    public sealed class VoxelNightHeadlightSettingsEditor : VoxelOdinTuningEditor { }

    public sealed class VoxelNightHeadlightSettingsLayout : OdinAttributeProcessor<VoxelNightHeadlightSettings>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parent, MemberInfo member, List<Attribute> attributes)
        {
            if (!(member is FieldInfo)) return;
            attributes.RemoveAll(a => a is HeaderAttribute);
            string group = VoxelTrackOdinLayout.Section(typeof(VoxelNightHeadlightSettings), member.Name);
            attributes.Add(new FoldoutGroupAttribute(group));
            VoxelOdinTuningLayout.AddRangeAndUnits(typeof(VoxelNightHeadlightSettings), member.Name, group, attributes);
        }
    }
}
