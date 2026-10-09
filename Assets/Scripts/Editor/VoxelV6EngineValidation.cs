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
                Check(car.TotalIntegrityVoxels==integrity && car.MissingIntegrityVoxels==1,"Engine altered body integrity");
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
                var entry=VoxelUpgradeFitCatalog.Discover().First(e=>e.Asset==tuning);
                var preview=UnityEngine.Object.Instantiate(definition.visualPrefab,root.transform);
                entry.Build(preview.transform);Verify(preview);
                RenderViews(preview,"Isolated");
                foreach(var other in VoxelUpgradeFitCatalog.Discover().Where(e=>e.Asset!=tuning && e.Fits(definition))) other.Build(preview.transform);
                Verify(preview);Check(VoxelCurrencyState.Balance==1500,"Fit preview changed wallet");
                RenderViews(preview,"Combined");
                foreach(var r in preview.GetComponentsInChildren<Renderer>())
                    if(r.GetComponentInParent<VoxelIndestructiblePart>()==null) r.enabled=false;
                RenderViews(preview,"BodyHidden");
                foreach(var r in preview.GetComponentsInChildren<Renderer>())
                    r.enabled=r.GetComponentInParent<Transform>().GetComponentsInParent<Transform>().Any(t=>t.name==VoxelEngineUpgradeState.InstanceName);
                RenderViews(preview,"EngineOnly");
                VoxelEngineUpgradeState.BeginNewRun();Check(!VoxelEngineUpgradeState.IsPurchased,"New-run reset failed");
                var reset=UnityEngine.Object.Instantiate(definition.visualPrefab,root.transform);
                VoxelEngineUpgradeState.ApplyTo(reset.transform,definition);
                Check(!reset.GetComponentsInChildren<Transform>(true).Any(t=>t.name==VoxelV6EngineBuilder.SpoilerName),"Spoiler survived ownership reset");
                System.IO.File.WriteAllText("Temp/V6Spoiler/Validation.txt","PASS (Edit Mode): confirmed bundled engine/spoiler purchase, free preview, affordability/duplicate/compatibility guards, idempotent mounting, next-mission persistence, run reset, unchanged body damage/integrity/performance/exhausts and fit catalogue. Rendered both sides, rear and top in isolated, combined, body-hidden and engine-only views.");
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
            var spoilers=engine.GetComponentsInChildren<Transform>().Where(t=>t.name==VoxelV6EngineBuilder.SpoilerName).ToArray();
            Check(spoilers.Length==1,"Bundled rear spoiler missing or duplicated");
            var spoiler=spoilers[0];
            Check(spoiler.GetComponentInParent<VoxelIndestructiblePart>()!=null && spoiler.GetComponentsInChildren<Collider>().Length==0,"Spoiler must preserve engine protection and collision/integrity behavior");
            var mesh=spoiler.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh!=null && spoiler.GetComponentsInChildren<MeshRenderer>().Length==1,"Spoiler must use one combined mesh");
            var bounds=mesh.bounds;
            Check(Mathf.Abs(bounds.min.x+bounds.max.x)<.001f && bounds.size.x>2f && bounds.size.x<2.4f && bounds.min.y>1.04f && bounds.max.y<1.5f && bounds.max.z<-2f,"Spoiler is not centred over the rear deck");
            var paint=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/CarMaterials/LongtailPaint.mat");
            Check(spoiler.GetComponent<MeshRenderer>().sharedMaterial==paint,"Spoiler must match the body paint");
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
