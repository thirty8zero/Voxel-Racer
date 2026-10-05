using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer
{
    public enum VoxelGarageUpgradeKind { Armour, Guns, Missiles, Wheels, Spikes, Boost, Engine, Plough }

    public readonly struct VoxelGarageUpgradeSelection
    {
        public readonly VoxelGarageUpgradeKind Kind;
        public readonly int Slot;
        public VoxelGarageUpgradeSelection(VoxelGarageUpgradeKind kind, int slot) { Kind = kind; Slot = slot; }
    }

    /// <summary>Disposable visual preview. The original car, purchases and damage remain untouched.</summary>
    public sealed class VoxelGarageUpgradePlacement : IDisposable
    {
        public GameObject Car { get; private set; }
        public VoxelGarageUpgradeKind Kind { get; private set; }
        public IReadOnlyList<VoxelGarageUpgradeSelection> Selections => selections;
        public int SelectedSlot { get; private set; } = -1;
        public bool RequiresPlacement => Kind == VoxelGarageUpgradeKind.Armour ||
            Kind == VoxelGarageUpgradeKind.Guns || Kind == VoxelGarageUpgradeKind.Missiles;
        public int AvailableSpotCount => spots.Count;
        private readonly GameObject original;
        private readonly Dictionary<Renderer, bool> originalRendering = new();
        private readonly List<int> spots = new();
        private readonly List<VoxelGarageUpgradeSelection> selections = new();
        private readonly Dictionary<MeshRenderer, int> ghostSlots = new();
        private readonly Dictionary<MeshRenderer, VoxelGarageUpgradeSelection> pendingModels = new();
        private MeshFilter[] pickMeshes = Array.Empty<MeshFilter>();
        private Material ghostMaterial;
        private bool canAdd = true;

        public VoxelGarageUpgradePlacement(GameObject original, VoxelGarageUpgradeKind kind)
        {
            this.original = original;
            foreach (var renderer in original.GetComponentsInChildren<Renderer>(true)) originalRendering.Add(renderer, renderer.enabled);
            try
            {
                ShowKind(kind);
            }
            catch { Dispose(); throw; }
        }

        public int PendingCount(VoxelGarageUpgradeKind kind)
        {
            int count = 0;
            foreach (var part in selections) if (part.Kind == kind) count++;
            return count;
        }

        public bool IsPending(VoxelGarageUpgradeKind kind, int slot)
        {
            foreach (var part in selections) if (part.Kind == kind && part.Slot == slot) return true;
            return false;
        }

        public void ShowKind(VoxelGarageUpgradeKind kind)
        {
            Kind = kind; SelectedSlot = -1; canAdd = true;
            RebuildSelection();
            if (!RequiresPlacement && PendingCount(kind) == 0) Select(0);
        }

        public void SetCanAdd(bool value)
        {
            if (value == canAdd) return;
            canAdd = value; RebuildSelection();
        }

        private void RebuildSelection()
        {
            ghostSlots.Clear(); pendingModels.Clear(); spots.Clear(); pickMeshes = Array.Empty<MeshFilter>();
            Rebuild();
            // Replacements first, so spikes fit the preview's final wheels regardless of click order.
            foreach (var part in selections) if (part.Kind == VoxelGarageUpgradeKind.Wheels || part.Kind == VoxelGarageUpgradeKind.Engine) BuildPendingPart(part);
            foreach (var part in selections) if (part.Kind != VoxelGarageUpgradeKind.Wheels && part.Kind != VoxelGarageUpgradeKind.Engine) BuildPendingPart(part);
            if (RequiresPlacement && canAdd)
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    if (IsPending(Kind, slot)) continue;
                    bool available = Kind == VoxelGarageUpgradeKind.Guns
                        ? VoxelGunUpgradeState.CanPurchase(VoxelGunUpgradeState.LongGunTuning, slot) &&
                            VoxelGunUpgradeState.PurchasedLongGunCount + PendingCount(Kind) < Mathf.Clamp(VoxelGunUpgradeState.LongGunTuning.maximumPurchases, 1, 2)
                        : Kind == VoxelGarageUpgradeKind.Missiles ? !VoxelMissileUpgradeState.IsPurchased(slot == 1)
                        : !VoxelArmorUpgradeState.IsPurchasedFor(slot == 0 ? VoxelArmorSide.Left : VoxelArmorSide.Right);
                    if (!available) continue;
                    var before = new HashSet<MeshRenderer>(Car.GetComponentsInChildren<MeshRenderer>(true));
                    BuildPart(Kind, slot);
                    foreach (var renderer in Car.GetComponentsInChildren<MeshRenderer>(true))
                        if (!before.Contains(renderer)) TintGhost(renderer, slot);
                    spots.Add(slot);
                }
                if (spots.Count > 0 && ghostSlots.Count == 0) throw new InvalidOperationException("No upgrade placement visuals available.");
            }
            // Pending parts remain tappable across tabs, including when no more parts are affordable.
            pickMeshes = Car.GetComponentsInChildren<MeshFilter>();
        }

        private void BuildPendingPart(VoxelGarageUpgradeSelection part)
        {
            var before = new HashSet<MeshRenderer>(Car.GetComponentsInChildren<MeshRenderer>(true));
            BuildPart(part.Kind, part.Slot);
            foreach (var renderer in Car.GetComponentsInChildren<MeshRenderer>(true))
                if (!before.Contains(renderer)) pendingModels.Add(renderer, part);
        }

        public bool Remove(VoxelGarageUpgradeKind kind, int slot = -1)
        {
            int removed = selections.RemoveAll(part => part.Kind == kind && (slot < 0 || part.Slot == slot));
            if (removed == 0) return false;
            SelectedSlot = -1;
            RebuildSelection();
            return true;
        }

        private void Rebuild()
        {
            Destroy(Car);
            RestoreOriginalRendering();
            // Instantiate under an inactive parent so copied gameplay behaviours never enable.
            var container = new GameObject("Garage preview staging"); container.SetActive(false);
            // New objects otherwise belong to the active scene, which may be the menu
            // while editor QA constructs an unparented car in a separate preview scene.
            SceneManager.MoveGameObjectToScene(container, original.scene);
            try
            {
                Car = Object.Instantiate(original, container.transform);
                Car.name = "Temporary Upgrade Placement";
                Car.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                DisableBehaviour();
                Car.transform.SetParent(original.transform.parent, false);
                Car.SetActive(true);
                SyncTransform();
                foreach (var renderer in originalRendering.Keys) if (renderer != null) renderer.enabled = false;
            }
            finally { Destroy(container); }
        }

        private void DisableBehaviour()
        {
            foreach (var behaviour in Car.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var collider in Car.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var particle in Car.GetComponentsInChildren<ParticleSystem>(true))
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var trail in Car.GetComponentsInChildren<TrailRenderer>(true)) trail.enabled = false;
        }

        private void BuildPart(VoxelGarageUpgradeKind kind, int slot)
        {
            var car = Car.transform;
            switch (kind)
            {
                case VoxelGarageUpgradeKind.Armour:
                    VoxelArmorUpgradeState.CreatePanel(car, VoxelArmorTuning.Load(), slot == 0 ? VoxelArmorSide.Left : VoxelArmorSide.Right); break;
                case VoxelGarageUpgradeKind.Guns:
                    VoxelGunUpgradeState.CreateVisual(car, VoxelGunUpgradeState.LongGunTuning, slot); break;
                case VoxelGarageUpgradeKind.Missiles:
                    VoxelMissileUpgradeState.CreateVisual(car, VoxelMissileLauncherTuning.Load(), slot == 1); break;
                case VoxelGarageUpgradeKind.Wheels:
                    VoxelPerformanceWheelUpgradeState.CreateVisuals(car, VoxelPerformanceWheelTuning.Load()); break;
                case VoxelGarageUpgradeKind.Spikes:
                    foreach (var wheel in car.GetComponentsInChildren<Transform>(true))
                        if (wheel.name == "Voxel Wheel") VoxelWheelSpikeUpgradeState.CreateVisual(car, wheel, VoxelWheelSpikeTuning.Load());
                    break;
                case VoxelGarageUpgradeKind.Boost:
                    VoxelBoostUpgradeState.CreateVisual(car, VoxelBoostUpgradeTuning.LoadUpgrade()); break;
                case VoxelGarageUpgradeKind.Engine:
                    VoxelEngineUpgradeState.CreateVisual(car, VoxelEngineUpgradeTuning.Load()); break;
                case VoxelGarageUpgradeKind.Plough:
                    VoxelPloughUpgradeState.CreateVisual(car, VoxelPloughTuning.Load()); break;
            }
            DisableBehaviour();
        }

        private void TintGhost(MeshRenderer renderer, int slot)
        {
            if (ghostMaterial == null)
            {
                ghostMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Garage Light Green Placement" };
                ghostMaterial.SetFloat("_Surface", 1); ghostMaterial.SetFloat("_Blend", 0);
                ghostMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                ghostMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                ghostMaterial.SetFloat("_ZWrite", 0);
                ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                ghostMaterial.EnableKeyword("_EMISSION");
                ghostMaterial.SetColor("_EmissionColor", new Color(.2f, .5f, .25f));
                ghostMaterial.SetOverrideTag("RenderType", "Transparent");
                ghostMaterial.renderQueue = (int)RenderQueue.Transparent;
            }
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = ghostMaterial;
            renderer.sharedMaterials = materials;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(.55f, 1f, .65f, .4f));
            renderer.SetPropertyBlock(block);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ghostSlots.Add(renderer, slot);
        }

        public void Select(int slot)
        {
            if (RequiresPlacement && !spots.Contains(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
            if (IsPending(Kind, slot)) return;
            selections.Add(new VoxelGarageUpgradeSelection(Kind, slot));
            RebuildSelection(); SelectedSlot = slot;
        }

        public bool TryPick(Ray ray, out int slot)
        {
            bool hit = TryPickPart(ray, out var part, out bool pending);
            slot = hit && !pending ? part.Slot : -1;
            return hit && !pending;
        }

        public bool TryPickPart(Ray ray, out VoxelGarageUpgradeSelection part, out bool pending)
        {
            part = default; pending = false;
            float nearest = float.PositiveInfinity; bool selectable = false;
            foreach (var filter in pickMeshes)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !renderer.bounds.IntersectRay(ray)) continue;
                var local = filter.transform.worldToLocalMatrix;
                var localRay = new Ray(local.MultiplyPoint3x4(ray.origin), local.MultiplyVector(ray.direction));
                if (!filter.sharedMesh.bounds.IntersectRay(localRay, out float distance)) continue;
                float worldDistance = Vector3.Distance(ray.origin, filter.transform.TransformPoint(localRay.GetPoint(distance)));
                if (worldDistance >= nearest) continue;
                nearest = worldDistance;
                pending = pendingModels.TryGetValue(renderer, out part);
                selectable = pending;
                if (!pending && ghostSlots.TryGetValue(renderer, out int slot))
                {
                    part = new VoxelGarageUpgradeSelection(Kind, slot); selectable = true;
                }
            }
            return selectable;
        }

        public void SyncTransform()
        {
            if (Car == null || original == null) return;
            Car.transform.localPosition = original.transform.localPosition;
            Car.transform.localRotation = original.transform.localRotation;
            Car.transform.localScale = original.transform.localScale;
        }

        public void Dispose()
        {
            Destroy(Car); Car = null; Destroy(ghostMaterial); ghostMaterial = null;
            RestoreOriginalRendering();
        }

        private void RestoreOriginalRendering()
        {
            foreach (var entry in originalRendering) if (entry.Key != null) entry.Key.enabled = entry.Value;
        }

        private static void Destroy(Object obj)
        {
            if (obj == null) return;
            if (obj is GameObject go) { go.SetActive(false); go.transform.SetParent(null, true); }
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
    }
}
