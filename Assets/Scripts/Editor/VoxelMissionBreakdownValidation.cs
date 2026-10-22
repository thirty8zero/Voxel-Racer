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
        internal static string MultiplierSummary(VoxelPostRaceContinue ui)
        {
            var panel=(GameObject)typeof(VoxelPostRaceContinue).GetField("timeBonusPanel",Flags).GetValue(ui);
            var columns=panel.GetComponentsInChildren<Text>(true);
            Check(columns.Length%2==0 && columns.Where((t,i)=>i%2==0).All(t=>t.alignment==TextAnchor.MiddleLeft) &&
                columns.Where((t,i)=>i%2==1).All(t=>t.alignment==TextAnchor.MiddleRight),"Multiplier labels/totals lost column alignment");
            return string.Join("\n",Enumerable.Range(0,columns.Length/2).Select(i=>columns[i*2].text+" "+columns[i*2+1].text));
        }

        private static void CheckMultiplierCenter(VoxelPostRaceContinue ui)
        {
            var panel=(GameObject)typeof(VoxelPostRaceContinue).GetField("timeBonusPanel",Flags).GetValue(ui);
            var rect=(RectTransform)panel.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            float bottom=float.PositiveInfinity,top=float.NegativeInfinity;
            var corners=new Vector3[4];
            foreach(RectTransform row in rect)
            {
                if(!row.gameObject.activeSelf)continue;
                row.GetWorldCorners(corners);
                foreach(var corner in corners)
                {float y=rect.InverseTransformPoint(corner).y;bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
            }
            Check(Mathf.Abs((top+bottom)*.5f)<.1f,"Middle reward rows are not vertically centred as a group");
            Check(bottom>=rect.rect.yMin-.1f && top<=rect.rect.yMax+.1f,"Middle reward rows overlap surrounding rewards");
            var gap=rect.Find("Final Multiplier Gap") as RectTransform;
            if(gap!=null)Check(gap.rect.height>=19.9f,"Final Multiplier lost its vertical gap");
        }

        [MenuItem("Tools/Voxel Racer/Render Mission Reward Alignment Example")]
        public static void RenderAlignmentExample() => RenderRewardExample(false, 1920);

        [MenuItem("Tools/Voxel Racer/Render Mission Reward and Continue Style")]
        public static void RenderStyledExample()
        {
            RenderRewardExample(true, 1920);
            RenderRewardExample(true, 2400);
        }

        private static void RenderRewardExample(bool fullScreen, int renderWidth)
        {
            if(Application.isPlaying) throw new Exception("Render outside Play Mode");
            const string backgroundPath = "Temp/LightingComparison/DayAfter.png";
            if (fullScreen && !File.Exists(backgroundPath)) VoxelLightingPreview.CaptureAfter();
            int cash=VoxelCurrencyState.Balance;
            var previous=VoxelMissionProgress.Active;
            var oldEvent=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root=new GameObject("Temporary Reward Alignment Preview");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
            var tuning=ScriptableObject.CreateInstance<VoxelMissionTuning>();
            RenderTexture target=null;Texture2D image=null,background=null;var previousTarget=RenderTexture.active;
            try
            {
                tuning.completionCurrencyAward=200;tuning.timeLimitSeconds=100;tuning.timeBonusCurrencyMultiplier=1;
                tuning.maximumTimeMultiplier=5;
                var mission=root.AddComponent<VoxelMissionProgress>();mission.Configure(tuning);
                mission.AdvanceBonusClock(65);mission.AddMultiplierBonus(3.2f);
                typeof(VoxelMissionProgress).GetMethod("CompleteMission",Flags).Invoke(mission,null);
                var ui=root.AddComponent<VoxelPostRaceContinue>();ui.missionProgress=mission;
                typeof(VoxelPostRaceContinue).GetMethod("BuildRewardSequence",Flags).Invoke(ui,null);
                typeof(VoxelPostRaceContinue).GetField("rewardSequenceStartedAt",Flags).SetValue(ui,Time.unscaledTime-2);
                typeof(VoxelPostRaceContinue).GetMethod("UpdateRewardSequence",Flags).Invoke(ui,null);
                if (fullScreen)
                {
                    typeof(VoxelPostRaceContinue).GetMethod("BuildContinueButton",Flags).Invoke(ui,null);
                    var backdrop = VoxelMenuUi.CreateCanvas(root.transform, "Frozen race style preview");
                    backdrop.GetComponent<Canvas>().sortingOrder = 0;
                    var art = new GameObject("Race view", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                    art.transform.SetParent(backdrop, false);
                    background = new Texture2D(2, 2); background.LoadImage(File.ReadAllBytes(backgroundPath));
                    art.GetComponent<RawImage>().texture = background; art.GetComponent<RawImage>().raycastTarget = false;
                    var fit = art.GetComponent<AspectRatioFitter>();
                    fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = background.width / (float)background.height;
                }
                Check(MultiplierSummary(ui).Contains("MISSION MULTIPLIER 4.20x") && mission.TotalCurrencyEarned==910,"Example reward values differ from reference");
                var cameraObject=new GameObject("Reward Preview Camera");cameraObject.transform.SetParent(root.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
                camera.overrideSceneCullingMask=UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.18f);camera.cullingMask=1<<31;
                foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))
                {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
                target=new RenderTexture(renderWidth,1080,24);camera.targetTexture=target;
                Canvas.ForceUpdateCanvases();camera.Render();camera.Render();Canvas.ForceUpdateCanvases();
                CheckMultiplierCenter(ui);
                // Native layout must recenter when rows are hidden or additional rows are authored.
                var middle=(GameObject)typeof(VoxelPostRaceContinue).GetField("timeBonusPanel",Flags).GetValue(ui);
                var timeRow=middle.transform.Find("TIME BONUS Row");
                timeRow.gameObject.SetActive(false);CheckMultiplierCenter(ui);
                timeRow.gameObject.SetActive(true);
                typeof(VoxelPostRaceContinue).GetMethod("BuildMultiplierRow",Flags).Invoke(ui,new object[]{"EXAMPLE EXTRA","1.00x",false});
                CheckMultiplierCenter(ui);
                UnityEngine.Object.DestroyImmediate(middle.transform.Find("EXAMPLE EXTRA Row").gameObject);
                CheckMultiplierCenter(ui);camera.Render();
                var panel=root.GetComponentsInChildren<RectTransform>(true).First(r=>r.name=="Mission Reward Panel");
                var corners=new Vector3[4];panel.GetWorldCorners(corners);
                var bottomLeft=camera.WorldToScreenPoint(corners[0]);var topRight=camera.WorldToScreenPoint(corners[2]);
                int x=Mathf.RoundToInt(bottomLeft.x),y=Mathf.RoundToInt(bottomLeft.y);
                int width=Mathf.RoundToInt(topRight.x-bottomLeft.x),height=Mathf.RoundToInt(topRight.y-bottomLeft.y);
                if (fullScreen)
                {
                    var buttonCorners = new Vector3[4]; ui.ContinueButton.GetComponent<RectTransform>().GetWorldCorners(buttonCorners);
                    Check(camera.WorldToScreenPoint(buttonCorners[2]).y < bottomLeft.y - 20, "Continue overlaps reward panel.");
                    Check(ui.ContinueButton.targetGraphic is VoxelGaragePanel, "Continue bevelled surface missing.");
                    Check(ui.ContinueButton.GetComponentsInChildren<VoxelGarageUpgradeIcon>().Length == 1, "Continue chevrons missing.");
                    x = y = 0; width = renderWidth; height = 1080;
                }
                image=new Texture2D(width,height,TextureFormat.RGB24,false);RenderTexture.active=target;
                image.ReadPixels(new Rect(x,y,width,height),0,0);image.Apply();
                System.IO.Directory.CreateDirectory("Temp/MissionRewards");
                string path = fullScreen ? "Temp/MissionRewards/BlueSilver_" + renderWidth + ".png" : "Temp/MissionRewards/AlignedExample.png";
                System.IO.File.WriteAllBytes(path,image.EncodeToPNG());
                Debug.Log("Rendered mission reward example: " + path);
            }
            finally
            {
                RenderTexture.active=previousTarget;
                if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
                if(image!=null)UnityEngine.Object.DestroyImmediate(image);
                if(background!=null)UnityEngine.Object.DestroyImmediate(background);
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(tuning);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                typeof(VoxelMissionProgress).GetField("<Active>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,previous);
                VoxelCurrencyState.Reset();VoxelCurrencyState.Add(cash);
                if(oldEvent==null){var created=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();if(created!=null)UnityEngine.Object.DestroyImmediate(created.gameObject);}
            }
        }
        [MenuItem("Tools/Voxel Racer/Validate Mission Breakdown")]
        public static void Run()
        {
            if(Application.isPlaying) throw new Exception("Run outside Play Mode");
            var previous=VoxelMissionProgress.Active;
            int previousCash=VoxelCurrencyState.Balance;
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
                Check(Mathf.Approximately(mission.Breakdown.Total(VoxelMissionBreakdown.Group.MultiplierLost),2.1f),"Actual timer decay not included");
                mission.Configure(tuning);
                Check(mission.Breakdown.Total(VoxelMissionBreakdown.Group.ProgressGained)==0,"Previous mission totals retained");
                foreach(VoxelMissionBreakdown.Group group in Enum.GetValues(typeof(VoxelMissionBreakdown.Group)))
                {
                    if(group==VoxelMissionBreakdown.Group.CrateRewards) continue;
                    for(int i=0;i<5;i++) mission.Breakdown.Add(group,"Example source "+(i+1),group==VoxelMissionBreakdown.Group.MultiplierGained||group==VoxelMissionBreakdown.Group.MultiplierLost?.1f:10);
                }
                mission.AdvanceBonusClock(70);
                mission.AddMultiplierBonus(1);
                mission.AddBonusCash(40);
                VoxelMissionProgress.ReportEnemyVoxelDamage(10000);
                Check(mission.TotalCurrencyEarned==270,"Remaining-time payout missing from rewards");
                var ui=root.AddComponent<VoxelPostRaceContinue>();ui.missionProgress=mission;
                typeof(VoxelPostRaceContinue).GetMethod("BuildRewardSequence",Flags).Invoke(ui,null);
                var bonus=MultiplierSummary(ui);
                Check(bonus.Contains("MISSION MULTIPLIER 2.00x") && bonus.Contains("FINAL MULTIPLIER 2.30x") && bonus.Contains("+$130") && bonus.Contains("+0.30x") && !bonus.Contains("SEC LEFT"),"Results must separate original multiplier, time bonus and final multiplier");
                Check(Mathf.Approximately(mission.EffectiveTimeBonusMultiplier,2),"Results time bonus changed original multiplier");
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
                typeof(VoxelPostRaceContinue).GetField("rewardSequenceStartedAt",Flags).SetValue(ui,Time.unscaledTime-2);
                typeof(VoxelPostRaceContinue).GetMethod("UpdateRewardSequence",Flags).Invoke(ui,null);
                CheckMultiplierCenter(ui);
                for(int cashState=0;cashState<2;cashState++)
                {
                    if(cashState==1)
                    {
                        rewards.parent.gameObject.SetActive(false);
                        mission.Configure(tuning);mission.AdvanceBonusClock(70);mission.AddMultiplierBonus(1);
                        VoxelMissionProgress.ReportEnemyVoxelDamage(10000);
                        typeof(VoxelPostRaceContinue).GetMethod("BuildRewardSequence",Flags).Invoke(ui,null);
                        typeof(VoxelPostRaceContinue).GetField("rewardSequenceStartedAt",Flags).SetValue(ui,Time.unscaledTime-2);
                        typeof(VoxelPostRaceContinue).GetMethod("UpdateRewardSequence",Flags).Invoke(ui,null);
                        CheckMultiplierCenter(ui);
                        foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                        foreach(var c in root.GetComponentsInChildren<Canvas>(true)){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=1;}
                        Check(!((GameObject)typeof(VoxelPostRaceContinue).GetField("bonusCashPanel",Flags).GetValue(ui)).activeSelf,"Zero bonus cash panel is visible");
                    }
                    foreach(int width in new[]{1920,2400})
                    {
                        var rt=new RenderTexture(width,1080,24);var texture=new Texture2D(width,1080,TextureFormat.RGB24,false);var old=RenderTexture.active;
                        try {cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();cam.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,1080),0,0);texture.Apply();File.WriteAllBytes("Temp/MissionRewards_"+width+(cashState==1?"_NoCash":"")+".png",texture.EncodeToPNG());}
                        finally {cam.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);}
                    }
                }
                rewards.parent.gameObject.SetActive(false);
                mission.Configure(tuning);mission.AdvanceBonusClock(100);mission.AddMultiplierBonus(1);
                VoxelMissionProgress.ReportEnemyVoxelDamage(10000);
                typeof(VoxelPostRaceContinue).GetMethod("BuildRewardSequence",Flags).Invoke(ui,null);
                var noTimeBonus=MultiplierSummary(ui);
                Check(noTimeBonus.Contains("MULTIPLIER 2.00x") && !noTimeBonus.Contains("TIME BONUS") && !noTimeBonus.Contains("FINAL MULTIPLIER"),"Late finish shows a nonexistent time bonus");
                typeof(VoxelPostRaceContinue).GetField("rewardSequenceStartedAt",Flags).SetValue(ui,Time.unscaledTime-2);
                typeof(VoxelPostRaceContinue).GetMethod("UpdateRewardSequence",Flags).Invoke(ui,null);
                CheckMultiplierCenter(ui);
                Debug.Log("PASS: actual score/multiplier caps and decay, mission reset, separate original/time/final multiplier and payout in rewards panel, late finish omits time bonus, optional crate group, equal page sizes, navigation, expanded scrolling and clipping; rendered rewards/breakdown at 16:9 and 20:9.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(tuning);active.SetValue(null,previous);
                VoxelCurrencyState.Reset();VoxelCurrencyState.Add(previousCash);
                if(oldEvent==null) {var created=UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();if(created!=null)UnityEngine.Object.DestroyImmediate(created.gameObject);}
            }
        }
    }
}
