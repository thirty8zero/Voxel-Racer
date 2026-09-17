using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer.Editor
{
    public static class VoxelMissionBreakdownValidation
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Check(bool ok,string message) {if(!ok) throw new Exception(message);}
        [MenuItem("Tools/Voxel Racer/Validate Mission Breakdown")]
        public static void Run()
        {
            if(Application.isPlaying) throw new Exception("Run outside Play Mode");
            var previous=VoxelMissionProgress.Active;
            var oldEvent=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var root=new GameObject("Temporary Breakdown QA");
            var tuning=ScriptableObject.CreateInstance<VoxelMissionTuning>();
            var active=typeof(VoxelMissionProgress).GetField("<Active>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
            try
            {
                var mission=root.AddComponent<VoxelMissionProgress>();
                tuning.requiredPoints=10000;tuning.timeLimitSeconds=100;tuning.maximumTimeMultiplier=5;tuning.timeBonusCurrencyMultiplier=1;
                mission.Configure(tuning);active.SetValue(null,mission);
                VoxelMissionProgress.ReportEnemyVoxelDamage(10);
                VoxelMissionProgress.ReportCivilianVoxelDamage(100);
                Check(mission.Points==0,"Score floor changed");
                Check(mission.Breakdown.Total(VoxelMissionBreakdown.Group.ProgressGained)==10,"Gained points missing");
                Check(mission.Breakdown.Total(VoxelMissionBreakdown.Group.ProgressLost)==10,"Clamped losses overcounted");
                mission.ChangeMultiplier(20,"ENEMY VOXELS");
                Check(mission.Breakdown.Total(VoxelMissionBreakdown.Group.MultiplierGained)==4,"Multiplier cap overcounted");
                mission.ChangeMultiplier(-2,"CIVILIAN DAMAGE");mission.AdvanceBonusClock(101);
                Check(mission.Breakdown.Total(VoxelMissionBreakdown.Group.MultiplierLost)==5,"Expiry not included");
                mission.Configure(tuning);
                Check(mission.Breakdown.Total(VoxelMissionBreakdown.Group.ProgressGained)==0,"Previous mission totals retained");
                foreach(VoxelMissionBreakdown.Group group in Enum.GetValues(typeof(VoxelMissionBreakdown.Group)))
                {
                    if(group==VoxelMissionBreakdown.Group.CrateRewards) continue;
                    for(int i=0;i<5;i++) mission.Breakdown.Add(group,"Example source "+(i+1),group==VoxelMissionBreakdown.Group.MultiplierGained||group==VoxelMissionBreakdown.Group.MultiplierLost?.1f:10);
                }
                var ui=root.AddComponent<VoxelPostRaceContinue>();ui.missionProgress=mission;
                typeof(VoxelPostRaceContinue).GetMethod("BuildRewardSequence",Flags).Invoke(ui,null);
                Check(!root.GetComponentsInChildren<Button>(true).Any(b=>b.name=="Group 5"),"Empty crate group visible");
                mission.Breakdown.Add(VoxelMissionBreakdown.Group.CrateRewards,"$30 cash",1);
                mission.Breakdown.Add(VoxelMissionBreakdown.Group.CrateRewards,"+15s time (applied 15s)",1);
                mission.Breakdown.Add(VoxelMissionBreakdown.Group.CrateRewards,"+0.10x multiplier (applied 0.00x)",1);
                root.GetComponentsInChildren<Button>(true).First(b=>b.name=="Show Breakdown").onClick.Invoke();
                var page=root.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="Mission Breakdown Panel");
                var rewards=root.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="Mission Reward Panel");
                Check(page.sizeDelta==rewards.sizeDelta && page.gameObject.activeSelf && !rewards.gameObject.activeSelf,"Page size/navigation mismatch");
                var scroll=root.GetComponentInChildren<ScrollRect>();
                Check(scroll.viewport.GetComponent<RectMask2D>()!=null,"Missing clipping");
                var cameraObject=new GameObject("QA Camera");cameraObject.transform.SetParent(root.transform);var cam=cameraObject.AddComponent<Camera>();
                cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.15f,.18f);cam.cullingMask=1<<31;
                foreach(var canvas in root.GetComponentsInChildren<Canvas>(true)) {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;}
                for(int state=0;state<2;state++)
                {
                    if(state==1)
                    {
                        var expanded=(bool[])typeof(VoxelPostRaceContinue).GetField("expanded",Flags).GetValue(ui);
                        for(int i=0;i<6;i++) expanded[i]=true;
                        typeof(VoxelPostRaceContinue).GetMethod("RefreshBreakdown",Flags).Invoke(ui,null);
                        Check(scroll.content.sizeDelta.y>scroll.viewport.sizeDelta.y,"Expanded content cannot scroll");
                        scroll.verticalNormalizedPosition=0;
                    }
                    foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                    foreach(int width in new[]{1920,2400})
                    {
                        var rt=new RenderTexture(width,1080,24);var texture=new Texture2D(width,1080,TextureFormat.RGB24,false);var old=RenderTexture.active;
                        try {cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();cam.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,1080),0,0);texture.Apply();File.WriteAllBytes("Temp/MissionBreakdown_"+width+"_"+state+".png",texture.EncodeToPNG());}
                        finally {cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);}
                    }
                }
                root.GetComponentsInChildren<Button>(true).First(b=>b.name=="Return to Rewards").onClick.Invoke();
                Check(rewards.gameObject.activeSelf && !page.gameObject.activeSelf,"Return arrow failed");
                Debug.Log("PASS: actual score/multiplier caps and expiry, mission reset, optional crate group, equal page sizes, navigation, expanded scrolling and clipping; rendered 16:9 and 20:9.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(tuning);active.SetValue(null,previous);
                if(oldEvent==null) {var created=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();if(created!=null)UnityEngine.Object.DestroyImmediate(created.gameObject);}
            }
        }
    }
}
