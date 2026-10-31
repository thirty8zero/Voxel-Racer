using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public sealed class VoxelBossIntroductionSequenceOdinLayout : OdinAttributeProcessor<VoxelBossIntroductionSequence>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parent, MemberInfo member, List<Attribute> attributes)
        {
            if (member is not FieldInfo) return;
            attributes.RemoveAll(a => a is HeaderAttribute);
            attributes.Add(new FoldoutGroupAttribute(VoxelTrackOdinLayout.Section(typeof(VoxelBossIntroductionSequence), member.Name)));
            if (member.Name == "panels") attributes.Add(new ListDrawerSettingsAttribute { DraggableItems = true, Expanded = true });
        }
    }
}
