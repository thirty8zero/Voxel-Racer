using UnityEngine;
namespace VoxelRacer
{
    public static class VoxelEngineUpgradeState
    {
        public const string InstanceName="Purchased V6 Engine";
        public const string SpoilerName="V6 Rear Spoiler";
        public static bool IsPurchased {get;private set;}
        public static void BeginNewRun() => IsPurchased=false;
        public static bool TryPurchase(VoxelEngineUpgradeTuning tuning,VoxelCarDefinition car)
        {
            if(IsPurchased || tuning==null || !tuning.Fits(car) || !VoxelCurrencyState.TrySpend(tuning.purchasePrice)) return false;
            IsPurchased=true; return true;
        }
        public static void ApplyTo(Transform car,VoxelCarDefinition definition)
        {
            var tuning=VoxelEngineUpgradeTuning.Load();
            if(!IsPurchased || tuning==null || !tuning.Fits(definition) || car==null) return;
            if(CreateVisual(car,tuning)!=null)
                car.GetComponent<VoxelCarController>()?.SetEnginePerformance(tuning.topSpeedBonusPercent,tuning.accelerationBonusPercent);
        }
        internal static void GrantForDevelopment(VoxelEngineUpgradeTuning tuning, VoxelCarDefinition car)
        {
            if (tuning != null && tuning.Fits(car)) IsPurchased = true;
        }
        // Preserve the old engine and sibling indices so existing damage paths remain valid.
        // Called by both runtime and isolated preview; never modifies purchase state or source assets.
        public static GameObject CreateVisual(Transform car,VoxelEngineUpgradeTuning tuning)
        {
            if(car==null || tuning==null || tuning.enginePrefab==null) return null;
            Transform original=null;
            foreach(var t in car.GetComponentsInChildren<Transform>(true))
            {
                if(t.name==InstanceName) return t.gameObject;
                if(t.name=="Protected Engine") original=t;
            }
            if(original==null) return null;
            var engine=Object.Instantiate(tuning.enginePrefab,original.parent,false);
            engine.name=InstanceName;
            engine.transform.localPosition=original.localPosition;
            engine.transform.localRotation=original.localRotation;
            engine.transform.localScale=original.localScale;
            // The engine lives beneath the protected chassis. Append the bundled body
            // accessory outside that entire protected branch without moving existing
            // car children: their sibling-index damage paths must stay unchanged.
            var spoiler=engine.transform.Find(SpoilerName);
            var body=original.parent;
            while(body!=null && body.GetComponentInParent<VoxelIndestructiblePart>()!=null) body=body.parent;
            if(spoiler!=null && body!=null) spoiler.SetParent(body,true);
            original.gameObject.SetActive(false);
            return engine;
        }
    }
}
