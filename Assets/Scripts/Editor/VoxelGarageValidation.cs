using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer.Editor
{
    public static class VoxelGarageValidation
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Invoke(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        [MenuItem("Tools/Voxel Racer/Validate and Render Garage")]
        public static void Run()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Run garage QA outside Play Mode.");
            var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled).ToArray();
            var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;bool fog=RenderSettings.fog;
            var oldEvent=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var root=new GameObject("Temporary Garage QA");
            using var state = new VoxelGarageUpgradeDockValidation.SavedState();
            try
            {
                foreach(var light in lights)light.enabled=false;
                VoxelGarageEnvironment.Build(root.transform);
                var definition=AssetDatabase.LoadAssetAtPath<VoxelCarDefinition>("Assets/Resources/Cars/SpyCar2PlayerCar.asset");
                var cr=new GameObject("Preview Car");cr.transform.SetParent(root.transform,false);cr.transform.localRotation=Quaternion.Euler(0,-15,0);
                UnityEngine.Object.Instantiate(definition.visualPrefab,cr.transform);
                var car=cr.AddComponent<VoxelCarController>();car.enabled=false;car.ResetIntegrityBaseline();
                cr.transform.localPosition=Vector3.up*.18f;
                int originalIntegrity=car.TotalIntegrityVoxels;
                var table=VoxelCarTurntable.Create(root.transform,cr.transform);
                Check(car.TotalIntegrityVoxels==originalIntegrity,"Turntable changed car integrity");
                cr.transform.Rotate(0,45,0);Invoke(table,"LateUpdate");
                Check(Quaternion.Angle(table.transform.Find("Rotating steel deck").rotation,cr.transform.rotation)<.01f,"Deck does not follow car rotation");
                cr.transform.localRotation=Quaternion.Euler(0,-15,0);Invoke(table,"LateUpdate");
                var shop=root.AddComponent<VoxelRepairUpgradeSceneController>();
                typeof(VoxelRepairUpgradeSceneController).GetField("definition",Flags).SetValue(shop,definition);
                typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop,car);
                shop.repairTuning=VoxelRepairTuning.Load();
                Invoke(shop,"BuildUi");Invoke(shop,"RefreshUi");
                var hud=root.AddComponent<VoxelCarIntegrityDisplay>();hud.target=car;
                if(root.GetComponentsInChildren<Canvas>().Length<2)Invoke(hud,"BuildHud");
                var group=(CanvasGroup)typeof(VoxelCarIntegrityDisplay).GetField("canvasGroup",Flags).GetValue(hud);group.alpha=1;
                Invoke(hud,"RefreshIntegrity");
                var health=(Image)typeof(VoxelCarIntegrityDisplay).GetField("healthRing",Flags).GetValue(hud);
                Check(Mathf.Approximately(health.fillAmount,1),"Full-health dial incorrect");
                var body=car.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null).ToArray();
                for(int i=0;i<body.Length/2+1;i++)body[i].gameObject.SetActive(false);
                Invoke(hud,"RefreshIntegrity");Check(Mathf.Abs(health.fillAmount-car.IntegrityPercent/100)<.001f,"Damaged health dial does not track integrity");
                typeof(VoxelCarIntegrityDisplay).GetField("damagePulseStartedAt",Flags).SetValue(hud,Time.unscaledTime-.14f);
                Invoke(hud,"RefreshIntegrity");Check(health.color.r>.9f,"Damage pulse missing");
                foreach(var part in body)part.gameObject.SetActive(true);
                typeof(VoxelCarIntegrityDisplay).GetField("damagePulseStartedAt",Flags).SetValue(hud,-1f);Invoke(hud,"RefreshIntegrity");
                var home=root.GetComponentsInChildren<Button>(true).First(b=>b.name=="Repair Menu Button");home.onClick.Invoke();
                var repairs=root.GetComponentsInChildren<Button>(true).Where(b=>b.name.StartsWith("Repair ")&&b.name.EndsWith(" Button")&&b.name!="Repair Menu Button").ToArray();
                Check(repairs.Length==3,"Repair controls missing");
                Check(!root.transform.Find("Repair Upgrade UI/Garage Home").gameObject.activeSelf,"Repair menu did not open");
                var panel=root.transform.Find("Repair Upgrade UI/Repair Panel");panel.Find("Close Panel").GetComponent<Button>().onClick.Invoke();
                root.GetComponentsInChildren<Button>().First(b=>b.name=="Upgrade Menu Button").onClick.Invoke();
                Check(root.GetComponentsInChildren<ScrollRect>().Length==1,"Upgrade list did not open");
                var upgrades=root.transform.Find("Repair Upgrade UI/Car Upgrade Panel");upgrades.Find("Close Panel").GetComponent<Button>().onClick.Invoke();
                Check(car.enabled==false && car.TotalIntegrityVoxels==car.RemainingIntegrityVoxels,"UI changed car control or integrity");
                VoxelGarageUpgradeDockValidation.CheckShop(shop,car,definition);
                var purchaseFeedback=root.transform.Find("Repair Upgrade UI/Repair Feedback").GetComponent<Text>();
                string purchaseMessage=purchaseFeedback.text;
                root.transform.Find("Repair Upgrade UI/Repair Feedback").GetComponent<Text>().text=string.Empty;
                var cg=new GameObject("QA Camera");cg.transform.SetParent(root.transform,false);var cam=cg.AddComponent<Camera>();
                var tuning=VoxelRepairUpgradeTuning.Load();cam.transform.position=tuning.cameraPosition;cam.transform.LookAt(tuning.cameraLookAt);cam.fieldOfView=tuning.cameraFieldOfView;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.018f,.023f,.033f);cam.cullingMask=1<<30;
                shop.cameraTuning=tuning;
                typeof(VoxelRepairUpgradeSceneController).GetField("workshopCamera",Flags).SetValue(shop,cam);
                var uiObject=new GameObject("QA UI Camera");uiObject.transform.SetParent(root.transform,false);
                var uiCamera=uiObject.AddComponent<Camera>();uiCamera.cullingMask=1<<31;uiCamera.clearFlags=CameraClearFlags.Depth;
                foreach(var canvas in root.GetComponentsInChildren<Canvas>()){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=uiCamera;canvas.planeDistance=1;}
                // Capture the reserved 3D viewport beneath the full-screen UI, matching
                // the runtime overlay canvas without URP's second base-camera clear.
                var background=new GameObject("QA scene composite",typeof(RectTransform),typeof(RawImage));
                background.transform.SetParent(root.transform.Find("Repair Upgrade UI"),false);background.transform.SetAsFirstSibling();
                var backgroundRect=(RectTransform)background.transform;backgroundRect.anchorMin=Vector2.zero;backgroundRect.anchorMax=Vector2.one;
                backgroundRect.offsetMin=backgroundRect.offsetMax=Vector2.zero;background.GetComponent<RawImage>().raycastTarget=false;
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=t.GetComponentInParent<Canvas>()!=null?31:30;
                foreach(var l in root.GetComponentsInChildren<Light>())l.cullingMask=1<<30;
                Directory.CreateDirectory("Temp");
                foreach(int width in new[]{1440,1920,2400})
                for(int view=0;view<3;view++)
                {
                    Invoke(shop,"ShowGaragePanel",view==1?panel.gameObject:view==2?upgrades.gameObject:null);
                    purchaseFeedback.text=view==2?purchaseMessage:string.Empty;
                    var rt=new RenderTexture(width,1080,24);var texture=new Texture2D(width,1080,TextureFormat.RGB24,false);
                    var sceneTexture=new RenderTexture(width,1080,24);background.GetComponent<RawImage>().texture=sceneTexture;
                    try
                    {
                        cam.targetTexture=sceneTexture;uiCamera.targetTexture=rt;Canvas.ForceUpdateCanvases();uiCamera.Render();Canvas.ForceUpdateCanvases();
                        Invoke(shop,"LayoutGarageDock");Invoke(shop,"ApplyGaragePreviewViewport");
                        VoxelGarageUpgradeDockValidation.CheckLayout(shop,cam,view==2);
                        VoxelGarageUpgradeDockValidation.CheckStaticCamera(shop,cam,view==2);
                        purchaseFeedback.text=view==2?purchaseMessage:string.Empty;
                        Invoke(table,"LateUpdate");
                        RenderTexture.active=sceneTexture;GL.Clear(true,true,Color.black);cam.Render();uiCamera.Render();RenderTexture.active=rt;
                        texture.ReadPixels(new Rect(0,0,width,1080),0,0);texture.Apply();
                        File.WriteAllBytes("Temp/Garage_"+width+(view==0?"":view==1?"_Repair":"_Upgrades")+".png",texture.EncodeToPNG());
                        if(view==2)
                        {
                            upgrades.GetComponentInChildren<ScrollRect>().horizontalNormalizedPosition=1;Canvas.ForceUpdateCanvases();
                            cam.Render();uiCamera.Render();texture.ReadPixels(new Rect(0,0,width,1080),0,0);texture.Apply();
                            File.WriteAllBytes("Temp/Garage_"+width+"_Upgrades_End.png",texture.EncodeToPNG());
                            upgrades.GetComponentInChildren<ScrollRect>().horizontalNormalizedPosition=0;
                        }
                    }
                    finally{cam.targetTexture=uiCamera.targetTexture=null;RenderTexture.active=null;rt.Release();sceneTexture.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(sceneTexture);UnityEngine.Object.DestroyImmediate(texture);}
                }
                File.WriteAllText("Temp/GarageValidation.txt","PASS: ten bottom upgrade cards, affordable/unaffordable/equipped states, stable ascending price order with fully purchased cards last, partial gun slots, compatibility, guarded purchase callbacks, actual engine/missile installation and damage preservation, read-only next mission preview with loop/non-loop/empty sequence, navigation/close, horizontal touch/mouse scroll, feedback above cards and dock/mission/UI separation. Static camera position, rotation, FOV and projection through full car rotation in home/repair/upgrades; player zoom-in/out changes and persists; shop refresh/re-open preserves framing; rotation envelope fits above cards with the floor rendered behind translucent cards. Rendered 4:3, 16:9 and 20:9 including end of catalogue. Cash, purchases, run damage and campaign progress restored.");
            }
            catch(Exception e){Directory.CreateDirectory("Temp");File.WriteAllText("Temp/GarageValidation.txt","FAIL: "+e);throw;}
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);foreach(var light in lights)if(light!=null)light.enabled=true;
                RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;RenderSettings.fog=fog;
                if(oldEvent==null){var created=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();if(created!=null)UnityEngine.Object.DestroyImmediate(created.gameObject);}
            }
        }
    }
}
