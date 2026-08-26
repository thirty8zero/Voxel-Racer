using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Persists the selected catalogue car between the menu and race scenes.</summary>
    public static class VoxelCarSelectionState
    {
        public const string SelectedCarKey = "VoxelRacer.SelectedCar";

        public static VoxelCarDefinition[] LoadDefinitions()
        {
            var availableDefinitions = new List<VoxelCarDefinition>();
            foreach (VoxelCarDefinition definition in Resources.LoadAll<VoxelCarDefinition>("Cars"))
                if (definition != null && definition.availableForSelection)
                    availableDefinitions.Add(definition);

            VoxelCarDefinition[] definitions = availableDefinitions.ToArray();
            Array.Sort(definitions, (first, second) => first.selectionOrder.CompareTo(second.selectionOrder));
            return definitions;
        }

        public static VoxelCarDefinition GetSelectedOrDefault()
        {
            VoxelCarDefinition[] definitions = LoadDefinitions();
            if (definitions.Length == 0)
                return null;

            string selectedName = PlayerPrefs.GetString(SelectedCarKey, string.Empty);
            foreach (VoxelCarDefinition definition in definitions)
                if (definition.name == selectedName)
                    return definition;

            return definitions[0];
        }

        public static bool IsSelected(VoxelCarDefinition definition)
        {
            return definition != null &&
                PlayerPrefs.GetString(SelectedCarKey, string.Empty) == definition.name;
        }

        public static void Select(VoxelCarDefinition definition)
        {
            if (definition == null)
                return;

            PlayerPrefs.SetString(SelectedCarKey, definition.name);
            PlayerPrefs.Save();
        }

        public static int CountIntegrityVoxels(GameObject visualPrefab)
        {
            if (visualPrefab == null)
                return 0;

            int count = 0;
            foreach (MeshRenderer renderer in visualPrefab.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.GetComponentInParent<VoxelIndestructiblePart>() == null)
                    count++;
            return count;
        }
    }
}
