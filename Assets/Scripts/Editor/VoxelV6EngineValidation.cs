using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer.Editor
{
    public static class VoxelV6EngineValidation
    {
        private static void Check(bool value,string message) {if(!value)throw new Exception(message);}
        [MenuItem("Tools/Voxel Racer/Validate V6 Engine Upgrade")]
        public static void Run()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Run from Edit Mode.");
            using var savedState=new VoxelGarageUpgradeDockValidation.SavedState();
            var random=UnityEngine.Random.state;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            const BindingFlags statics=BindingFlags.Static|BindingFlags.NonPublic;
            int cash=VoxelCurrencyState.Balance;bool owned=VoxelEngineUpgradeState.IsPurchased;
            var missing=(HashSet<string>)typeof(VoxelCarRunState).GetField("missingVoxelPaths",statics).GetValue(null);
            var armor=(Dictionary<string,int>)typeof(VoxelCarRunState).GetField("armorHealth",statics).GetValue(null);
            var savedMissing=missing.ToArray();var savedArmor=armor.ToArray();
            var nameField=typeof(VoxelCarRunState).GetField("carDefinitionName",statics);var savedName=nameField.GetValue(null);
            var oldEvent=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var root=new GameObject("Temporary V6 validation");
            try
            {
                var tuning=VoxelEngineUpgradeTuning.Load();
                var definition=AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                Check(tuning!=null && tuning.Fits(definition) && !tuning.Fits(null),"Engine compatibility failed");
                var incompatible=ScriptableObject.CreateInstance<VoxelCarDefinition>();
                try {Check(!tuning.Fits(incompatible),"Engine/spoiler offered to an incompatible car");}
                finally {UnityEngine.Object.DestroyImmediate(incompatible);}
                Check(tuning.purchasePrice==1500,"Incorrect price");
                var holder=new GameObject("Car");holder.transform.SetParent(root.transform,false);
                UnityEngine.Object.Instantiate(definition.visualPrefab,holder.transform);
                var car=holder.AddComponent<VoxelCarController>();car.enabled=false;car.SetTuning(definition.tuning);car.ResetIntegrityBaseline();
                int integrity=car.TotalIntegrityVoxels;
                var damaged=car.GetComponentsInChildren<MeshRenderer>().First(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null);
                damaged.gameObject.SetActive(false);
                VoxelCarRunState.BeginNewRun(definition);
                Check(!VoxelEngineUpgradeState.TryPurchase(tuning,definition),"Unaffordable purchase allowed");
                var shop=root.AddComponent<VoxelRepairUpgradeSceneController>();
                typeof(VoxelRepairUpgradeSceneController).GetField("definition",flags).SetValue(shop,definition);
                typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop,car);
                shop.repairTuning=VoxelRepairTuning.Load();
                typeof(VoxelRepairUpgradeSceneController).GetMethod("BuildUi",flags).Invoke(shop,null);
                VoxelCurrencyState.Add(3000);
                typeof(VoxelRepairUpgradeSceneController).GetMethod("RefreshUi",flags).Invoke(shop,null);
                var button=root.GetComponentsInChildren<Button>(true).First(b=>b.name=="V6 Engine Purchase Button");
                Check(button.interactable,"Shop engine unavailable");button.onClick.Invoke();
                Check(!VoxelEngineUpgradeState.IsPurchased && VoxelCurrencyState.Balance==3000,"Engine preview charged before confirmation");
                var placement=(VoxelGarageUpgradePlacement)typeof(VoxelRepairUpgradeSceneController).GetField("upgradePlacement",flags).GetValue(shop);
                Verify(placement.Car);
                root.GetComponentsInChildren<Button>(true).First(b=>b.name=="Confirm Upgrade Purchase").onClick.Invoke();
                Check(VoxelEngineUpgradeState.IsPurchased && VoxelCurrencyState.Balance==1500 && !button.interactable,"Shop purchase failed");
                Check(!VoxelEngineUpgradeState.TryPurchase(tuning,definition),"Duplicate purchase allowed");
                VoxelEngineUpgradeState.ApplyTo(car.transform,definition);
                Check(car.GetComponentsInChildren<Transform>().Count(t=>t.name==VoxelEngineUpgradeState.InstanceName)==1,"Duplicate engine");
                int spoilerVoxels=car.GetComponentsInChildren<Transform>(true).First(t=>t.name==VoxelV6EngineBuilder.SpoilerName)
                    .GetComponentsInChildren<MeshRenderer>(true).Length;
                Check(car.TotalIntegrityVoxels==integrity+spoilerVoxels && car.MissingIntegrityVoxels==1,
                    "Purchase must add spoiler integrity without repairing existing body damage");
                Check(Mathf.Approximately(car.EffectiveTopSpeed,car.topSpeed*1.15f),"Top speed bonus failed");
                car.SetWheelPerformance(20,15,20);
                Check(Mathf.Approximately(car.EffectiveAcceleration,car.acceleration*1.25f*1.2f),"Engine and wheel bonuses do not compose");
                Verify(car.gameObject);
                var scroll=button.GetComponentInParent<ScrollRect>(true);Check(scroll!=null && scroll.horizontal && scroll.content.rect.width>scroll.viewport.rect.width,"Engine not in scrollable shop");
                var next=new GameObject("Next mission car");next.transform.SetParent(root.transform,false);
                UnityEngine.Object.Instantiate(definition.visualPrefab,next.transform);
                var nextCar=next.AddComponent<VoxelCarController>();nextCar.enabled=false;
                VoxelEngineUpgradeState.ApplyTo(next.transform,definition);nextCar.ResetIntegrityBaseline();VoxelCarRunState.Apply(nextCar,definition);Verify(next);
                Check(nextCar.MissingIntegrityVoxels==1,"Damage paths changed across engine installation");
                var boost=next.AddComponent<VoxelBoostController>();boost.enabled=false;boost.Configure(nextCar,VoxelBoostTuning.Load());
                foreach(var outlet in next.GetComponentsInChildren<VoxelEngineExhaustOutlet>())
                {
                    var effect=outlet.GetComponentInChildren<ParticleSystem>();
                    Check(effect!=null && Vector3.Distance(effect.transform.position,outlet.transform.position)<.001f,"Boost flame does not match V6 tip");
                }
                CheckSpoilerDamage(root,car,definition,shop);
                var entry=VoxelUpgradeFitCatalog.Discover().First(e=>e.Asset==tuning);
                int previewCash=VoxelCurrencyState.Balance, previewMissing=VoxelCarRunState.MissingVoxelCount;
                var preview=UnityEngine.Object.Instantiate(definition.visualPrefab,root.transform);
                var baseRenderers=preview.GetComponentsInChildren<Renderer>(true).ToHashSet();
                entry.Build(preview.transform);Verify(preview);
                RenderViews(preview,"Isolated");
                foreach(var other in VoxelUpgradeFitCatalog.Discover().Where(e=>e.Asset!=tuning && e.Fits(definition))) other.Build(preview.transform);
                Verify(preview);
                RenderViews(preview,"Combined");
                foreach(var r in preview.GetComponentsInChildren<Renderer>())
                    if(baseRenderers.Contains(r) && r.GetComponentInParent<VoxelIndestructiblePart>()==null) r.enabled=false;
                RenderViews(preview,"BodyHidden");
                foreach(var r in preview.GetComponentsInChildren<Renderer>())
                    r.enabled=r.GetComponentsInParent<Transform>().Any(t=>t.name==VoxelEngineUpgradeState.InstanceName || t.name==VoxelV6EngineBuilder.SpoilerName);
                RenderViews(preview,"EngineOnly");
                Check(VoxelCurrencyState.Balance==previewCash && VoxelCarRunState.MissingVoxelCount==previewMissing,
                    "Fit preview changed wallet or run damage");
                VoxelEngineUpgradeState.BeginNewRun();Check(!VoxelEngineUpgradeState.IsPurchased,"New-run reset failed");
                var reset=UnityEngine.Object.Instantiate(definition.visualPrefab,root.transform);
                VoxelEngineUpgradeState.ApplyTo(reset.transform,definition);
                Check(!reset.GetComponentsInChildren<Transform>(true).Any(t=>t.name==VoxelV6EngineBuilder.SpoilerName),"Spoiler survived ownership reset");
                System.IO.File.WriteAllText("Temp/V6Spoiler/Validation.txt","PASS (Edit Mode): bundled engine/spoiler purchase, free preview, affordability/duplicate/compatibility guards, 36 damageable spoiler voxels, hostile projectile surface/cache, actual body damage selection, no healing on repeat mounting, body/spoiler damage persistence across rebuild, completely missing spoiler persistence, partial/full connected repair and garage billing, protected engine/exhausts, run reset, performance and fit discovery. Rendered damaged/repaired spoiler and both sides, rear and top in isolated, combined, body-hidden and engine-only views; cash, ownership, damage and RNG restored.");
                Debug.Log("V6 engine/spoiler validation passed; report and fit views: Temp/V6Spoiler/.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Random.state=random;
                typeof(VoxelEngineUpgradeState).GetField("<IsPurchased>k__BackingField",statics).SetValue(null,owned);
                VoxelCurrencyState.Reset();VoxelCurrencyState.Add(cash);
                missing.Clear();foreach(var p in savedMissing)missing.Add(p);armor.Clear();foreach(var p in savedArmor)armor.Add(p.Key,p.Value);nameField.SetValue(null,savedName);
                if(oldEvent==null){var e=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();if(e!=null)UnityEngine.Object.DestroyImmediate(e.gameObject);}
            }
        }
        private static void Verify(GameObject car)
        {
            var outlets=car.GetComponentsInChildren<VoxelEngineExhaustOutlet>();Check(outlets.Length==2,"Expected exactly two active exhaust outlets");
            Check(outlets.Any(o=>o.transform.position.x<0) && outlets.Any(o=>o.transform.position.x>0),"Both exhaust sides required");
            foreach(var o in outlets)Check(o.GetComponentInParent<VoxelIndestructiblePart>()!=null && Vector3.Dot(o.transform.forward,Vector3.back)>.99f,"Outlet protection/orientation incorrect");
            var engine=car.GetComponentsInChildren<Transform>().First(t=>t.name==VoxelEngineUpgradeState.InstanceName);
            Check(engine.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Header input"))==6,"Need three exhaust inputs per bank");
            Check(engine.GetComponentsInChildren<MeshRenderer>(true).All(r=>r.GetComponentInParent<VoxelIndestructiblePart>()!=null),
                "Engine hardware must stay protected");
            var spoilers=car.GetComponentsInChildren<Transform>(true).Where(t=>t.name==VoxelV6EngineBuilder.SpoilerName).ToArray();
            Check(spoilers.Length==1,"Bundled rear spoiler missing or duplicated");
            var spoiler=spoilers[0];
            var voxels=spoiler.GetComponentsInChildren<MeshRenderer>(true);
            Check(spoiler.GetComponentInParent<VoxelIndestructiblePart>()==null && spoiler.GetComponentsInChildren<Collider>(true).Length==0,
                "Spoiler must be damageable without adding per-voxel colliders");
            Check(voxels.Length==36 && voxels.All(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null),"Expected 36 separate body-damage voxels");
            var bounds=new Bounds(spoiler.InverseTransformPoint(voxels[0].bounds.center),Vector3.zero);
            foreach(var voxel in voxels)
            {
                var b=voxel.GetComponent<MeshFilter>().sharedMesh.bounds;
                for(int i=0;i<8;i++)
                    bounds.Encapsulate(spoiler.InverseTransformPoint(voxel.transform.TransformPoint(b.center+Vector3.Scale(b.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)))));
            }
            Check(Mathf.Abs(bounds.min.x+bounds.max.x)<.001f && bounds.size.x>2f && bounds.size.x<2.4f && bounds.min.y>1.04f && bounds.max.y<1.5f && bounds.max.z<-2f,"Spoiler is not centred over the rear deck");
            var paint=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/LongtailPaint.mat");
            Check(voxels.All(r=>r.sharedMaterial==paint),"Spoiler must match the body paint");
        }
        private static void CheckSpoilerDamage(GameObject root,VoxelCarController car,VoxelCarDefinition definition,
            VoxelRepairUpgradeSceneController shop)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            MeshRenderer[] Spoiler(VoxelCarController c) => c.GetComponentsInChildren<Transform>(true)
                .Single(t=>t.name==VoxelV6EngineBuilder.SpoilerName).GetComponentsInChildren<MeshRenderer>(true);
            var voxels=Spoiler(car);
            var target=voxels.First(r=>r.name=="Wing centre 6-0");
            int missing=car.MissingIntegrityVoxels;
            // Exercise the real cached hostile-bullet path and then the normal collision damage path.
            var trace=typeof(VoxelCarController).GetMethod("TryFindHostileProjectileHit",flags);
            object[] ray={target.bounds.center+Vector3.up*.2f,Vector3.down,.3f,Vector3.zero,0f};
            Check((bool)trace.Invoke(car,ray) && Mathf.Abs(((Vector3)ray[3]).y-target.bounds.max.y)<.002f,
                "Hostile bullets do not hit the spoiler surface");
            car.damageVoxelsPerHit=1;car.debrisVoxelsPerDamagedVoxel=0;
            typeof(VoxelCarController).GetField("nextDamageTime",flags).SetValue(car,float.NegativeInfinity);
            car.ApplyDamage(target.transform.position,Vector3.forward,"V6 spoiler validation");
            Check(!target.gameObject.activeSelf && car.MissingIntegrityVoxels==missing+1 &&
                voxels.Count(r=>!r.gameObject.activeSelf)==1,"Body damage did not remove exactly one spoiler voxel");
            Check(!(bool)trace.Invoke(car,ray),"Destroyed spoiler voxel still intercepts hostile bullets");
            VoxelEngineUpgradeState.ApplyTo(car.transform,definition);
            VoxelEngineUpgradeState.CreateVisual(car.transform,VoxelEngineUpgradeTuning.Load());
            Check(!target.gameObject.activeSelf && Spoiler(car).Length==36,"Repeat mounting healed or duplicated the spoiler");
            Render(car.gameObject,"DamagedSpoilerRear",new Vector3(0,1.65f,-8));
            VoxelCarRunState.Capture(car,definition);
            var holder=new GameObject("Spoiler damage persistence");holder.transform.SetParent(root.transform,false);
            UnityEngine.Object.Instantiate(definition.visualPrefab,holder.transform);
            var rebuilt=holder.AddComponent<VoxelCarController>();rebuilt.enabled=false;
            VoxelEngineUpgradeState.ApplyTo(holder.transform,definition);rebuilt.ResetIntegrityBaseline();
            VoxelCarRunState.Apply(rebuilt,definition);
            Check(rebuilt.MissingIntegrityVoxels==missing+1 && !Spoiler(rebuilt).First(r=>r.name==target.name).gameObject.activeSelf,
                "Spoiler or existing body damage did not persist into the next car");
            int balance=VoxelCurrencyState.Balance;
            int bill=(int)typeof(VoxelRepairUpgradeSceneController).GetMethod("GetRepairCost",flags).Invoke(shop,new object[]{100f});
            Check(shop.repairTuning.GetRepairCost(car,100f,2)==(missing+1)*2,"Spoiler omitted from normal per-voxel repair pricing");
            typeof(VoxelRepairUpgradeSceneController).GetMethod("TryRepair",flags).Invoke(shop,new object[]{100f,bill});
            Check(car.MissingIntegrityVoxels==0 && target.gameObject.activeSelf && VoxelCurrencyState.Balance==balance-bill,
                "Paid garage full repair did not restore the spoiler and body");
            Check((bool)trace.Invoke(car,ray),"Repaired spoiler did not rejoin the hostile projectile cache");
            Render(car.gameObject,"RepairedSpoilerRear",new Vector3(0,1.65f,-8));
            // With all body voxels intact, erase the entire wing and verify connected partial/full repair.
            rebuilt.RepairPercent(100);
            foreach(var voxel in Spoiler(rebuilt)) voxel.gameObject.SetActive(false);
            VoxelCarRunState.Capture(rebuilt,definition);
            VoxelCarRunState.Apply(car,definition);
            Check(car.MissingIntegrityVoxels==36 && Spoiler(car).All(r=>!r.gameObject.activeSelf),"Completely lost spoiler did not persist");
            Check(car.RepairPercent(.001f)==1 && car.MissingIntegrityVoxels==35,"Partial repair did not reconnect one spoiler voxel");
            Check(car.RepairPercent(100)==35 && car.MissingIntegrityVoxels==0,"Full repair could not rebuild the completely lost spoiler");
            Verify(car.gameObject);
            VoxelCarRunState.Capture(car,definition);
        }
        private static void RenderViews(GameObject source,string mode)
        {
            System.IO.Directory.CreateDirectory("Temp/V6Spoiler");
            Render(source,mode+"Left",new Vector3(-5,2.6f,-7));
            Render(source,mode+"Right",new Vector3(5,2.6f,-7));
            Render(source,mode+"Rear",new Vector3(0,1.65f,-8));
            Render(source,mode+"Top",new Vector3(0,11,-.1f));
        }
        private static void Render(GameObject source,string name,Vector3 cameraPosition)
        {
            System.IO.Directory.CreateDirectory("Temp/V6Spoiler");
            var preview=new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(UnityEngine.Object.Instantiate(source));
                preview.camera.transform.position=cameraPosition;
                preview.camera.transform.LookAt(new Vector3(0,.45f,0));preview.camera.fieldOfView=37;
                preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.camera.backgroundColor=new Color(.17f,.2f,.23f);preview.ambientColor=new Color(.5f,.5f,.5f);
                preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,150,0);
                preview.lights[1].intensity=1;preview.lights[1].transform.rotation=Quaternion.Euler(30,210,0);
                preview.BeginStaticPreview(new Rect(0,0,1280,800));preview.Render(true);
                var image=preview.EndStaticPreview();System.IO.File.WriteAllBytes("Temp/V6Spoiler/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{preview.Cleanup();}
        }
    }
}
