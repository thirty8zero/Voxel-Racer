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
                Check(tuning.purchasePrice==1500,"Incorrect price");
                var holder=new GameObject("Car");holder.transform.SetParent(root.transform,false);
                UnityEngine.Object.Instantiate(definition.visualPrefab,holder.transform);
                var car=holder.AddComponent<VoxelCarController>();car.enabled=false;car.SetTuning(definition.tuning);car.ResetIntegrityBaseline();
                int integrity=car.TotalIntegrityVoxels;
                var damaged=car.GetComponentsInChildren<MeshRenderer>().First(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null);
                damaged.gameObject.SetActive(false);
                VoxelEngineUpgradeState.BeginNewRun();VoxelCurrencyState.Reset();
                Check(!VoxelEngineUpgradeState.TryPurchase(tuning,definition),"Unaffordable purchase allowed");
                var shop=root.AddComponent<VoxelRepairUpgradeSceneController>();
                typeof(VoxelRepairUpgradeSceneController).GetField("definition",flags).SetValue(shop,definition);
                typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop,car);
                typeof(VoxelRepairUpgradeSceneController).GetMethod("BuildUi",flags).Invoke(shop,null);
                VoxelCurrencyState.Add(3000);
                typeof(VoxelRepairUpgradeSceneController).GetMethod("RefreshUi",flags).Invoke(shop,null);
                var button=root.GetComponentsInChildren<Button>(true).First(b=>b.name=="V6 Engine Purchase Button");
                Check(button.interactable,"Shop engine unavailable");button.onClick.Invoke();
                Check(VoxelEngineUpgradeState.IsPurchased && VoxelCurrencyState.Balance==1500 && !button.interactable,"Shop purchase failed");
                Check(!VoxelEngineUpgradeState.TryPurchase(tuning,definition),"Duplicate purchase allowed");
                VoxelEngineUpgradeState.ApplyTo(car.transform,definition);
                Check(car.GetComponentsInChildren<Transform>().Count(t=>t.name==VoxelEngineUpgradeState.InstanceName)==1,"Duplicate engine");
                Check(car.TotalIntegrityVoxels==integrity && car.MissingIntegrityVoxels==1,"Engine altered body integrity");
                Check(Mathf.Approximately(car.EffectiveTopSpeed,car.topSpeed*1.15f),"Top speed bonus failed");
                car.SetWheelPerformance(20,15,20);
                Check(Mathf.Approximately(car.EffectiveAcceleration,car.acceleration*1.25f*1.2f),"Engine and wheel bonuses do not compose");
                Verify(car.gameObject);
                var scroll=button.GetComponentInParent<ScrollRect>(true);Check(scroll!=null && scroll.content.rect.height>scroll.viewport.rect.height,"Engine not in scrollable shop");
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
                foreach(var other in VoxelUpgradeFitCatalog.Discover().Where(e=>e.Asset!=tuning && e.Fits(definition))) other.Build(preview.transform);
                Verify(preview);Check(VoxelCurrencyState.Balance==1500,"Fit preview changed wallet");
                Render(preview,0);
                foreach(var r in preview.GetComponentsInChildren<Renderer>())
                    if(r.GetComponentInParent<VoxelIndestructiblePart>()==null) r.enabled=false;
                Render(preview,1);
                foreach(var r in preview.GetComponentsInChildren<Renderer>())
                    r.enabled=r.GetComponentInParent<Transform>().GetComponentsInParent<Transform>().Any(t=>t.name==VoxelEngineUpgradeState.InstanceName);
                Render(preview,2);
                VoxelEngineUpgradeState.BeginNewRun();Check(!VoxelEngineUpgradeState.IsPurchased,"New-run reset failed");
                Debug.Log("V6 validation passed: purchase/shop, persistence, body damage, combined performance, six inputs, mirrored outlets, protection, fit discovery and combined views.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
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
        }
        private static void Render(GameObject source,int view)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(UnityEngine.Object.Instantiate(source));
                preview.camera.transform.position=view==0?new Vector3(5,3,-7):new Vector3(4,5,6);
                preview.camera.transform.LookAt(new Vector3(0,.45f,0));preview.camera.fieldOfView=37;
                preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.camera.backgroundColor=new Color(.17f,.2f,.23f);preview.ambientColor=new Color(.5f,.5f,.5f);
                preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,view==0?150:30,0);
                preview.lights[1].intensity=1;preview.lights[1].transform.rotation=Quaternion.Euler(30,210,0);
                preview.BeginStaticPreview(new Rect(0,0,1280,800));preview.Render(true);
                var image=preview.EndStaticPreview();System.IO.File.WriteAllBytes("Temp/V6EngineFit"+view+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{preview.Cleanup();}
        }
    }
}
