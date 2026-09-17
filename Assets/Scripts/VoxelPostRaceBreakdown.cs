using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    public sealed partial class VoxelPostRaceContinue
    {
        private GameObject rewardPage, breakdownPage;
        private RectTransform breakdownContent;
        private ScrollRect breakdownScroll;
        private readonly bool[] expanded = new bool[6];
        private static readonly string[] GroupTitles = {
            "MISSION PROGRESS GAINED", "MISSION PROGRESS LOST", "PLAYER INTEGRITY LOST",
            "MULTIPLIER GAINED", "MULTIPLIER LOST", "CRATE REWARDS" };

        private void BuildBreakdownPage(RectTransform canvas, Image rewards)
        {
            rewardPage=rewards.gameObject;
            // Reserve a right-hand navigation gutter within the existing 900 x 520 panel.
            foreach(Transform child in rewards.transform)
            {
                var rect=child as RectTransform;
                if(rect==null) continue;
                rect.sizeDelta=new Vector2(740,rect.sizeDelta.y);
                rect.anchoredPosition+=Vector2.left*35;
                var text=child.GetComponent<Text>();
                if(text!=null) {text.resizeTextForBestFit=true;text.resizeTextMinSize=30;text.resizeTextMaxSize=text.fontSize;}
            }
            var cashLabel=bonusCashPanel.GetComponentInChildren<Text>();
            cashLabel.rectTransform.sizeDelta=new Vector2(720,85);
            cashLabel.resizeTextForBestFit=true;cashLabel.resizeTextMinSize=30;cashLabel.resizeTextMaxSize=72;
            var next=VoxelMenuUi.CreateButton(rewards.transform,"Show Breakdown",">",44,new Vector2(.5f,.5f),new Vector2(400,210),new Vector2(64,64),()=>ShowBreakdown(true));
            next.GetComponentInChildren<Text>().font=Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
            var panel=VoxelMenuUi.CreatePanel(canvas,"Mission Breakdown Panel",new Vector2(0,.5f),PostRacePanelPosition,new Vector2(900,520));
            panel.color=rewards.color;breakdownPage=panel.gameObject;
            VoxelMenuUi.CreateText(panel.transform,"Breakdown Title","MISSION BREAKDOWN",46,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),new Vector2(30,210),new Vector2(730,64));
            var back=VoxelMenuUi.CreateButton(panel.transform,"Return to Rewards","<",44,new Vector2(.5f,.5f),new Vector2(-400,210),new Vector2(64,64),()=>ShowBreakdown(false));
            back.GetComponentInChildren<Text>().font=Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
            var viewport=VoxelMenuUi.CreatePanel(panel.transform,"Breakdown Viewport",new Vector2(.5f,.5f),new Vector2(0,-32),new Vector2(840,390));
            viewport.color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            breakdownScroll=viewport.gameObject.AddComponent<ScrollRect>();
            breakdownScroll.viewport=viewport.rectTransform;breakdownScroll.horizontal=false;
            breakdownScroll.movementType=ScrollRect.MovementType.Clamped;breakdownScroll.scrollSensitivity=35;
            var content=new GameObject("Breakdown Content",typeof(RectTransform));
            content.transform.SetParent(viewport.transform,false);breakdownContent=content.GetComponent<RectTransform>();
            breakdownContent.anchorMin=new Vector2(0,1);breakdownContent.anchorMax=Vector2.one;
            breakdownContent.pivot=new Vector2(.5f,1);breakdownContent.sizeDelta=Vector2.zero;
            breakdownScroll.content=breakdownContent;
            VoxelMenuUi.CreateText(panel.transform,"Scroll Hint","TAP A ROW TO EXPAND  /  SCROLL FOR DETAILS",20,TextAnchor.MiddleCenter,new Vector2(.5f,.5f),new Vector2(0,-244),new Vector2(820,24));
            RefreshBreakdown();breakdownPage.SetActive(false);
        }
        private void ShowBreakdown(bool show)
        {
            if(show) RefreshBreakdown();
            rewardPage.SetActive(!show);breakdownPage.SetActive(show);
        }
        private void RefreshBreakdown()
        {
            for(int childIndex=breakdownContent.childCount-1;childIndex>=0;childIndex--)
            {
                var child=breakdownContent.GetChild(childIndex);
                child.gameObject.SetActive(false);
                if(Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
            var data=missionProgress?.Breakdown;
            float y=0;
            for(int i=0;i<6;i++)
            {
                var group=(VoxelMissionBreakdown.Group)i;
                float total=data!=null?data.Total(group):0;
                if(i==5 && total==0) continue;
                int index=i;
                var row=VoxelMenuUi.CreateButton(breakdownContent,"Group "+i,"",28,new Vector2(.5f,1),new Vector2(0,-y-27),new Vector2(832,54),()=>{expanded[index]=!expanded[index];RefreshBreakdown();});
                var label=row.GetComponentInChildren<Text>();
                label.text=(expanded[i]?"-  ":"+  ")+GroupTitles[i]+"   "+FormatTotal(i,total);
                label.font=Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
                label.resizeTextForBestFit=true;label.resizeTextMinSize=20;label.resizeTextMaxSize=28;
                y+=60;
                if(!expanded[i]) continue;
                var entries=data?.Entries(group);
                if(entries==null || entries.Count==0)
                { Detail("No contributions this mission",ref y);continue; }
                foreach(var entry in entries)
                    Detail(i==5?entry.count+" x "+entry.source:Friendly(entry.source)+"   "+FormatTotal(i,entry.amount),ref y);
            }
            breakdownContent.sizeDelta=new Vector2(0,y);
            Canvas.ForceUpdateCanvases();
        }
        private void Detail(string value,ref float y)
        {
            var text=VoxelMenuUi.CreateText(breakdownContent,"Detail",value,24,TextAnchor.MiddleLeft,new Vector2(.5f,1),new Vector2(8,-y-23),new Vector2(790,46));
            text.font=Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
            text.resizeTextForBestFit=true;text.resizeTextMinSize=18;text.resizeTextMaxSize=24;
            text.color=new Color(.8f,.85f,.9f);y+=48;
        }
        private static string FormatTotal(int group,float value) => group==3||group==4
            ? (group==3?"+":"-")+value.ToString("0.00")+"x"
            : value.ToString("0")+(group==2?" voxels":group==5?" rewards":" pts");
        private static string Friendly(string value) => value switch {
            "ENEMY VOXELS"=>"Enemy voxels destroyed", "ENEMY DESTROYED"=>"Enemy vehicles destroyed",
            "BARRELS DESTROYED"=>"Fuel-drum groups destroyed", "CLOSE CALL"=>"Civilian near misses",
            "BOX PRIZE"=>"Crate multiplier rewards", "CIVILIAN DAMAGE"=>"Civilian voxels destroyed",
            "CIVILIAN DESTROYED"=>"Civilian vehicles destroyed", _=>value };
    }
}
