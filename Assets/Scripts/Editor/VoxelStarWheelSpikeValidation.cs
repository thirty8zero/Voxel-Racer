using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelStarWheelSpikeValidation
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Call(object target,string method,params object[] args) => target.GetType().GetMethod(method,Private).Invoke(target,args);
        private static void Check(bool value,string message) {if(!value)throw new InvalidOperationException(message);}
        private static VoxelGarageUpgradePlacement Placement(VoxelRepairUpgradeSceneController shop) =>
            (VoxelGarageUpgradePlacement)typeof(VoxelRepairUpgradeSceneController).GetField("upgradePlacement",Private).GetValue(shop);

        [MenuItem("Tools/Voxel Racer/Validate Star Wheel Spikes")]
        public static void Run()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
            Directory.CreateDirectory("Temp/StarWheelSpikes");
            using var saved=new VoxelGarageUpgradeDockValidation.SavedState();
            var random=UnityEngine.Random.state;
            var oldEvents=Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var scene=EditorSceneManager.NewPreviewScene();
            var root=new GameObject("Star spike validation");SceneManager.MoveGameObjectToScene(root,scene);
            try
            {
                var definition=Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                var basic=VoxelWheelSpikeTuning.Load();var star=VoxelWheelSpikeTuning.LoadStar();
                Check(star!=null && star!=basic && star.Fits(definition),"Independent star asset missing/incompatible");
                var incompatible=ScriptableObject.CreateInstance<VoxelCarDefinition>();
                try {Check(!star.Fits(incompatible),"Wheel-less car accepted");} finally {Object.DestroyImmediate(incompatible);}
                var mesh=star.spikePrefab.GetComponent<MeshFilter>().sharedMesh;
                var tips=mesh.vertices.Where(v=>Mathf.Abs(v.x-.44f)<.0001f).Distinct().ToArray();
                Check(tips.Length==5 && tips.All(v=>Mathf.Abs(new Vector2(v.y,v.z).magnitude-.15f)<.0001f),"Five large star tips missing");
                Check(mesh.vertices.Any(v=>Vector3.Distance(v,new Vector3(.33f,0,0))<.0001f),"Short central tip missing");
                Check(star.spikePrefab.GetComponentsInChildren<MeshRenderer>().Length==1 && star.spikePrefab.GetComponentsInChildren<Collider>().Length==0,
                    "Star visual must use one renderer and no colliders");
                var carRoot=new GameObject("Temporary Car");carRoot.transform.SetParent(root.transform,false);
                Object.Instantiate(definition.visualPrefab,carRoot.transform);
                var car=carRoot.AddComponent<VoxelCarController>();car.enabled=false;car.ResetIntegrityBaseline();
                var damaged=car.GetComponentsInChildren<MeshRenderer>().First(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null);
                damaged.gameObject.SetActive(false);int total=car.TotalIntegrityVoxels,missing=car.MissingIntegrityVoxels;
                VoxelCarRunState.BeginNewRun(definition);
                Check(!VoxelWheelSpikeUpgradeState.TryPurchase(star),"Unaffordable purchase accepted");
                VoxelCurrencyState.Add(basic.purchasePrice+star.purchasePrice+100);
                Check(VoxelWheelSpikeUpgradeState.TryPurchase(basic),"Original spikes no longer purchasable");
                VoxelWheelSpikeUpgradeState.ApplyTo(carRoot.transform);Verify(carRoot,basic);
                var shop=root.AddComponent<VoxelRepairUpgradeSceneController>();
                typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop,car);
                typeof(VoxelRepairUpgradeSceneController).GetField("definition",Private).SetValue(shop,definition);
                shop.repairTuning=VoxelRepairTuning.Load();Call(shop,"BuildUi");Call(shop,"RefreshUi");
                var button=shop.GetComponentsInChildren<Button>(true).First(b=>b.name=="Star Wheel Spike Purchase Button");
                Check(button.interactable && button.GetComponent<VoxelGarageUpgradeCard>().State==VoxelGarageUpgradeState.Affordable,"Star shop card unavailable after basic purchase");
                int cash=VoxelCurrencyState.Balance;
                button.onClick.Invoke();
                Verify(Placement(shop).Car,star);
                Check(VoxelCurrencyState.Balance==cash && VoxelWheelSpikeUpgradeState.InstalledTuning==basic,"Preview modified cash/ownership");
                Call(shop,"BeginUpgradePlacement",VoxelGarageUpgradeKind.Spikes);
                Check(Placement(shop).PendingCount(VoxelGarageUpgradeKind.Spikes)==0,"Conflicting spike types entered purchase basket");
                Call(shop,"CancelUpgradePlacement");Verify(carRoot,basic);
                Check(VoxelCurrencyState.Balance==cash,"Cancel charged cash");
                button.onClick.Invoke();Call(shop,"ConfirmUpgradePurchase");
                Check(VoxelCurrencyState.Balance==cash-star.purchasePrice && VoxelWheelSpikeUpgradeState.InstalledTuning==star,"Confirmed star replacement failed");
                Check(!VoxelWheelSpikeUpgradeState.TryPurchase(star) && !VoxelWheelSpikeUpgradeState.TryPurchase(basic),"Duplicate/downgrade accepted");
                VoxelWheelSpikeUpgradeState.ApplyTo(carRoot.transform);Verify(carRoot,star);
                Check(!damaged.gameObject.activeSelf && car.TotalIntegrityVoxels==total && car.MissingIntegrityVoxels==missing,"Purchase changed body damage/integrity");
                Check(Mathf.Approximately(VoxelWheelSpikeUpgradeState.CalculateRamDamage(20,false),20*(1+star.sideRamDamageBonusPercent/100)) &&
                    Mathf.Approximately(VoxelWheelSpikeUpgradeState.CalculateRamDamage(20,true),20),"Star damage stacked with basic or changed rear impact");
                var next=Object.Instantiate(definition.visualPrefab,root.transform);next.name="Next Mission";
                VoxelWheelSpikeUpgradeState.ApplyTo(next.transform);Verify(next,star);
                var wheels=VoxelPerformanceWheelTuning.Load();
                VoxelPerformanceWheelUpgradeState.CreateVisuals(next.transform,wheels);Verify(next,star);
                var other=Object.Instantiate(definition.visualPrefab,root.transform);other.name="Wheels first";
                VoxelPerformanceWheelUpgradeState.CreateVisuals(other.transform,wheels);
                VoxelWheelSpikeUpgradeState.ApplyTo(other.transform);Verify(other,star);
                var entries=VoxelUpgradeFitCatalog.Discover().Where(e=>e.Asset==star).ToArray();
                Check(entries.Length==3 && entries.All(e=>e.Fits(definition)),"Fit preview needs all/left/right choices");
                foreach(var entry in entries)
                {
                    var fit=Object.Instantiate(definition.visualPrefab,root.transform);
                    entry.Build(fit.transform);entry.Build(fit.transform);
                    int expected=entry.Label.Contains("All wheels")?4:2;
                    Check(fit.GetComponentsInChildren<VoxelWheelSpikeMount>().Length==expected,"Individual side fit duplicated or wrong wheel count");
                    Object.DestroyImmediate(fit);
                }
                var combined=Object.Instantiate(definition.visualPrefab,root.transform);
                var original=combined.GetComponentsInChildren<Renderer>(true).ToHashSet();
                foreach(var entry in VoxelUpgradeFitCatalog.Discover().Where(e=>e.Fits(definition)))entry.Build(combined.transform);
                Verify(combined,star);
                Render(other,"IsolatedLeft",new Vector3(-6,2,-5));Render(other,"IsolatedRight",new Vector3(6,2,-5));
                Render(combined,"CombinedLeft",new Vector3(-6,2,-5));Render(combined,"CombinedRight",new Vector3(6,2,-5));
                foreach(var r in original)if(r!=null && r.GetComponentInParent<VoxelIndestructiblePart>()==null)r.enabled=false;
                Render(combined,"BodyHiddenLeft",new Vector3(-6,2,-5));Render(combined,"BodyHiddenRight",new Vector3(6,2,-5));
                foreach(var r in original)if(r!=null)r.enabled=false;
                Render(combined,"UpgradesOnlyLeft",new Vector3(-6,2,-5));Render(combined,"UpgradesOnlyRight",new Vector3(6,2,-5));
                Render(star.spikePrefab,"StarCloseup",new Vector3(1.5f,.25f,.4f),Vector3.right*.13f,.42f);
                Check(VoxelCurrencyState.Balance==cash-star.purchasePrice && VoxelWheelSpikeUpgradeState.InstalledTuning==star,"Fit preview changed run state");
                VoxelCarRunState.BeginNewRun(definition);
                VoxelWheelSpikeUpgradeState.ApplyTo(next.transform);
                Check(!VoxelWheelSpikeUpgradeState.IsPurchased && VoxelWheelSpikeUpgradeState.InstalledTuning==null && next.GetComponentsInChildren<VoxelWheelSpikeMount>().Length==0,
                    "Reset retained star spikes");
                VoxelCurrencyState.Add(star.purchasePrice);
                Check(VoxelWheelSpikeUpgradeState.TryPurchase(star),"Direct star purchase requires basic spikes");
                File.WriteAllText("Temp/StarWheelSpikes/Validation.txt","PASS: separate tuning and six-tip geometry; shop/card preview/cancel/confirmation; basic-to-star replacement, duplicate/downgrade/affordability; exclusive bonus and unchanged rear damage; preserved body damage/integrity; next-mission ownership; both performance-wheel install orders; automatic all/left/right fit choices; isolated/combined/body-hidden/upgrades-only renders from both sides; new-run reset. Temporary state restored.");
                Debug.Log(File.ReadAllText("Temp/StarWheelSpikes/Validation.txt"));
            }
            catch(Exception e){File.WriteAllText("Temp/StarWheelSpikes/Validation.txt","FAIL: "+e);throw;}
            finally
            {
                Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Random.state=random;
                if(oldEvents==null){var e=Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();if(e!=null)Object.DestroyImmediate(e.gameObject);}
            }
        }
        private static void Verify(GameObject car,VoxelWheelSpikeTuning tuning)
        {
            var mounts=car.GetComponentsInChildren<VoxelWheelSpikeMount>();
            Check(mounts.Length==4 && mounts.All(m=>m.tuning==tuning),"Incorrect or duplicate spike variant mounts");
            foreach(var mount in mounts)
            {
                var wheel=mount.transform.parent;
                bool right=car.transform.InverseTransformPoint(wheel.position).x>=0;
                Check(Vector3.Dot(mount.transform.right,right?car.transform.right:-car.transform.right)>.99f,"Spike faces inward");
                var replacement=wheel.Find(VoxelPerformanceWheelUpgradeState.InstanceName);
                Check(Vector3.Distance(mount.transform.localPosition,replacement!=null?replacement.localPosition:Vector3.zero)<.0001f,"Spike lost wheel centre");
                Check(mount.GetComponentInParent<VoxelIndestructiblePart>()!=null && mount.GetComponentsInChildren<Collider>().Length==0,"Spike protection/physics changed");
            }
        }
        private static void Render(GameObject source,string name,Vector3 position,Vector3? centre=null,float zoom=3.1f)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(Object.Instantiate(source));preview.camera.orthographic=true;preview.camera.orthographicSize=zoom;
                preview.camera.transform.position=position;preview.camera.transform.LookAt(centre??new Vector3(0,.6f,0));
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=50;preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.camera.backgroundColor=new Color(.17f,.20f,.23f);preview.ambientColor=new Color(.5f,.5f,.5f);
                preview.lights[0].intensity=1.8f;preview.lights[0].transform.rotation=Quaternion.Euler(40,150,0);
                preview.lights[1].intensity=1;preview.lights[1].transform.rotation=Quaternion.Euler(30,210,0);
                preview.BeginStaticPreview(new Rect(0,0,1200,800));preview.Render(true);
                var image=preview.EndStaticPreview();File.WriteAllBytes("Temp/StarWheelSpikes/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            finally {preview.Cleanup();}
        }
    }
}
