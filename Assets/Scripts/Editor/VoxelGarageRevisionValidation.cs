using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelGarageRevisionValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Private).Invoke(target, args);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static float Difference(Matrix4x4 a, Matrix4x4 b)
        { float result=0; for(int i=0;i<16;i++)result=Mathf.Max(result,Mathf.Abs(a[i]-b[i]));return result; }

        [MenuItem("Tools/Voxel Racer/Validate Garage Mission Pricing and Transitions")]
        public static void Run()
        {
            Check(!Application.isPlaying, "Start garage revision validation in Edit Mode.");
            using var saved = new VoxelGarageUpgradeDockValidation.SavedState();
            var oldEvents=Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var scene=EditorSceneManager.NewPreviewScene();
            var root=new GameObject("Temporary Garage Revision QA"); SceneManager.MoveGameObjectToScene(root,scene);
            var repair=ScriptableObject.CreateInstance<VoxelRepairTuning>();
            var mission=ScriptableObject.CreateInstance<VoxelMissionTuning>();
            var nextMission=ScriptableObject.CreateInstance<VoxelMissionTuning>();
            var track=ScriptableObject.CreateInstance<VoxelTrackDefinition>();
            var nextTrack=ScriptableObject.CreateInstance<VoxelTrackDefinition>();
            var sequence=ScriptableObject.CreateInstance<VoxelTrackSequence>();
            var cameraTuning=Object.Instantiate(VoxelRepairUpgradeTuning.Load());
            float oldScale=Time.timeScale;
            try
            {
                Check(mission.repairCostPerPixel==2, "New missions must retain the previous repair price by default.");
                var definition=Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar");
                var model=Object.Instantiate(definition.visualPrefab,root.transform);
                var car=model.AddComponent<VoxelCarController>();car.enabled=false;car.ResetIntegrityBaseline();
                var pieces=car.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null).Take(5).ToArray();
                foreach(var piece in pieces)piece.gameObject.SetActive(false);
                Check(car.RepairableIntegrityVoxels==5,"QA damage count incorrect");
                var shop=root.AddComponent<VoxelRepairUpgradeSceneController>();
                typeof(VoxelRepairUpgradeSceneController).GetProperty("DisplayedCar").SetValue(shop,car);
                Set(shop,"definition",definition);shop.repairTuning=repair;shop.cameraTuning=cameraTuning;
                var cameraObject=new GameObject("Garage transition QA Camera");cameraObject.transform.SetParent(root.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
                Set(shop,"workshopCamera",camera);shop.ApplyCameraTuning();
                track.missionTuning=mission;nextTrack.missionTuning=nextMission;
                mission.repairCostPerPixel=.7f;nextMission.repairCostPerPixel=5;
                sequence.tracks=new[]{track,nextTrack};
                typeof(VoxelTrackProgressState).GetField("sequence",Static).SetValue(null,sequence);
                typeof(VoxelTrackProgressState).GetField("currentTrackIndex",Static).SetValue(null,0);
                Call(shop,"BuildUi");Call(shop,"RefreshUi");Canvas.ForceUpdateCanvases();Call(shop,"LayoutGarageDock");
                Check((int)Call(shop,"GetRepairCost",100f)==4,"Garage used the next mission or failed to round fractional total cost up.");
                foreach(float percent in new[]{10f,25f,50f})
                    Check((int)Call(shop,"GetRepairCost",percent)==Mathf.CeilToInt(Mathf.CeilToInt(car.TotalIntegrityVoxels*percent/100)*.7f),
                        "Partial repair cost ignored the completed mission's price.");
                Check(repair.GetRepairCost(car,100f)==10,"Default repair prices changed.");
                typeof(VoxelTrackProgressState).GetField("currentTrackIndex",Static).SetValue(null,1);
                Check((int)Call(shop,"GetRepairCost",100f)==25,"Per-mission price did not change.");
                typeof(VoxelTrackProgressState).GetField("currentTrackIndex",Static).SetValue(null,0);
                VoxelCurrencyState.Reset();VoxelCurrencyState.Add(4);
                var repairPanel=shop.transform.Find("Repair Upgrade UI/Repair Panel").gameObject;
                Call(shop,"ShowGaragePanel",repairPanel);Call(shop,"TryRepair",100f,(int)Call(shop,"GetRepairCost",100f));
                Check(VoxelCurrencyState.Balance==0 && car.RepairableIntegrityVoxels==0 && !repairPanel.activeSelf,
                    "Repair did not charge the mission price and return home after finishing damage.");
                pieces[0].gameObject.SetActive(false);mission.repairCostPerPixel=0;
                Call(shop,"ShowGaragePanel",repairPanel);Call(shop,"TryRepair",100f,(int)Call(shop,"GetRepairCost",100f));
                Check(car.RepairableIntegrityVoxels==0 && VoxelCurrencyState.Balance==0,"Free mission repair failed.");

                var upgrades=shop.transform.Find("Repair Upgrade UI/Car Upgrade Panel").gameObject;
                Call(shop,"ApplyGaragePreviewViewport");var home=camera.projectionMatrix;
                Call(shop,"StartGarageCameraTransition",.45f);Call(shop,"ShowGaragePanel",upgrades);
                Check(Difference(camera.projectionMatrix,home)<.00001f,"Opening upgrades cut directly to the target camera view.");
                var target=(Matrix4x4)typeof(VoxelRepairUpgradeSceneController).GetField("garageCameraTargetProjection",Private).GetValue(shop);
                Check(Difference(target,home)>.01f,"QA views do not differ");
                Time.timeScale=0;
                Call(shop,"AdvanceGarageCameraTransition",.225f);
                var halfway=camera.projectionMatrix;
                Check(Difference(halfway,home)>.001f && Difference(halfway,target)>.001f,
                    "Camera did not interpolate while the gameplay clock was paused.");
                Call(shop,"StartGarageCameraTransition",.45f);Call(shop,"ShowGaragePanel",new object[]{null});
                Check(Difference(camera.projectionMatrix,halfway)<.00001f,"Interrupted transition jumped before returning home.");
                Call(shop,"AdvanceGarageCameraTransition",.45f);
                Check(Difference(camera.projectionMatrix,home)<.00001f,"Camera failed to settle exactly on the home framing.");
                foreach(var sample in new[]{(VoxelEasingType.Linear,.25f),(VoxelEasingType.EaseInQuad,.0625f),
                    (VoxelEasingType.EaseOutQuad,.4375f),(VoxelEasingType.SmoothStep,.15625f)})
                {
                    Call(shop,"ShowGaragePanel",new object[]{null});
                    cameraTuning.cameraTransitionEasing=sample.Item1;
                    Call(shop,"StartGarageCameraTransition",1f);Call(shop,"ShowGaragePanel",upgrades);
                    var easingTarget=(Matrix4x4)typeof(VoxelRepairUpgradeSceneController).GetField("garageCameraTargetProjection",Private).GetValue(shop);
                    // Changing the selector must not change a transition already in progress.
                    cameraTuning.cameraTransitionEasing=VoxelEasingType.EaseInQuint;
                    Call(shop,"AdvanceGarageCameraTransition",.25f);
                    float fraction=Difference(camera.projectionMatrix,home)/Difference(easingTarget,home);
                    Check(Mathf.Abs(fraction-sample.Item2)<.0001f,"Garage ignored the selected easing or changed it mid-transition: "+sample.Item1);
                    Call(shop,"AdvanceGarageCameraTransition",.75f);
                    Check(Difference(camera.projectionMatrix,easingTarget)<.00001f,"Easing did not settle at the target: "+sample.Item1);
                }
                Call(shop,"StartGarageCameraTransition",0f);Call(shop,"ShowGaragePanel",upgrades);
                VoxelGarageUpgradeDockValidation.CheckLayout(shop,camera,true);
                VoxelGarageUpgradeDockValidation.CheckStaticCamera(shop,camera,true);
                Check(shop.NextRaceButton.interactable && shop.NextRaceButton.GetComponent<CanvasGroup>().alpha==1,
                    "Next Mission was not ready with no pending upgrades.");
                Directory.CreateDirectory("Temp");File.WriteAllText("Temp/GarageRevisionValidation.txt",
                    "PASS: completed-mission repair prices, default 2, fractional total rounding, 10/25/50/full quotes, exact-budget and free repairs, full repair navigation, smooth projection transition, paused clock, interruption continuity, selected linear/ease-in/ease-out/Smooth Step curves, selection retained during active transitions, exact endpoints, zero duration, fixed rotation/zoom framing, matching 30%-footprint Back buttons and layout. Cash, damage, purchases, campaign and time scale restored.");
                Debug.Log("PASS garage mission pricing, camera transitions and Back buttons.");
            }
            catch(Exception e){Directory.CreateDirectory("Temp");File.WriteAllText("Temp/GarageRevisionValidation.txt","FAIL: "+e);throw;}
            finally
            {
                Time.timeScale=oldScale;Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);
                foreach(var asset in new ScriptableObject[]{repair,mission,nextMission,track,nextTrack,sequence,cameraTuning})Object.DestroyImmediate(asset);
                if(oldEvents==null){var events=Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();if(events!=null)Object.DestroyImmediate(events.gameObject);}
            }
        }
    }
}
