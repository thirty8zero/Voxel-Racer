using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelCarController
    {
        private Material tailLightMaterial, brakeLightMaterial;
        private MeshRenderer[] brakeLightRenderers;
        public bool BrakeLightsActive { get; private set; }

        /// <summary>Bind only the red rear lenses, including damaged lenses that can be repaired.</summary>
        public void RefreshBrakeLights()
        {
            SetBrakeLights(false);
            if (tailLightMaterial == null) tailLightMaterial = Resources.Load<Material>("CarMaterials/CarTailLights");
            if (brakeLightMaterial == null) brakeLightMaterial = Resources.Load<Material>("CarMaterials/CarBrakeLights");
            var lenses = new List<MeshRenderer>();
            if (tailLightMaterial != null && brakeLightMaterial != null)
                foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.sharedMaterial == tailLightMaterial) lenses.Add(renderer);
            brakeLightRenderers = lenses.ToArray();
        }

        private void SetBrakeLights(bool braking)
        {
            if (brakeLightRenderers == null)
            {
                if (!braking) return;
                RefreshBrakeLights();
            }
            braking = braking && brakeLightMaterial != null && brakeLightRenderers.Length > 0;
            if (BrakeLightsActive == braking) return;
            BrakeLightsActive = braking;
            foreach (var renderer in brakeLightRenderers)
                if (renderer != null) renderer.sharedMaterial = braking ? brakeLightMaterial : tailLightMaterial;
        }

        private void OnDisable() => SetBrakeLights(false);
    }
}
