using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        private GameObject garageRepairPanel, garageUpgradePanel, garageSettingsPanel, garageHome;
        private static readonly Color RepairAccent = new(.95f,.15f,.19f);
        private static readonly Color UpgradeAccent = new(.10f,.60f,1f);
        private static readonly Color GoldAccent = new(1f,.73f,.20f);
        private static Font GarageMono => Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001") ?? VoxelHudStyles.HudFont;
        private static Font GarageHeading => Resources.Load<Font>("Fonts/Square") ?? VoxelHudStyles.HudFont;

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
        { rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=pos;rect.sizeDelta=size; }
        private static void Frame(GameObject go, Color accent, Color fill)
        {
            var image=go.GetComponent<Image>(); if(image!=null) image.enabled=false;
            var surface=new GameObject("Bevel Frame",typeof(RectTransform),typeof(VoxelGaragePanel));surface.transform.SetParent(go.transform,false);surface.transform.SetAsFirstSibling();
            var sr=(RectTransform)surface.transform;sr.anchorMin=Vector2.zero;sr.anchorMax=Vector2.one;sr.offsetMin=sr.offsetMax=Vector2.zero;
            var frame=surface.GetComponent<VoxelGaragePanel>();frame.color=fill;frame.edgeColor=accent;
            var button=go.GetComponent<Button>();
            if(button!=null){button.targetGraphic=frame;var colors=button.colors;colors.highlightedColor=new Color(1.3f,1.3f,1.3f);colors.selectedColor=Color.white;colors.pressedColor=new Color(.7f,.7f,.7f);button.colors=colors;}
            var glow=surface.AddComponent<Outline>();glow.effectColor=new Color(accent.r,accent.g,accent.b,.22f);glow.effectDistance=new Vector2(3,3);
        }
        private static Text Caption(Transform parent,string name,string value,int size,Vector2 anchor,Vector2 pos,Vector2 bounds)
        {
            var t=VoxelMenuUi.CreateText(parent,name,value,size,TextAnchor.MiddleCenter,anchor,pos,bounds);
            t.font=GarageMono;t.color=new Color(.55f,.61f,.67f);return t;
        }
        private void ShowGaragePanel(GameObject selected)
        {
            garageRepairPanel.SetActive(selected==garageRepairPanel);
            garageUpgradePanel.SetActive(selected==garageUpgradePanel);
            garageSettingsPanel.SetActive(selected==garageSettingsPanel);
            garageHome.SetActive(selected==null);
        }
        private void StyleGarageUi(RectTransform canvas)
        {
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.matchWidthOrHeight=1;scaler.enabled=false;scaler.enabled=true;
            var shade=new GameObject("Header Shade",typeof(RectTransform),typeof(CanvasRenderer),typeof(VoxelGarageHeaderShade));shade.transform.SetParent(canvas,false);shade.transform.SetAsFirstSibling();Place((RectTransform)shade.transform,new(.5f,1),new(0,-200),new(3000,400));shade.GetComponent<VoxelGarageHeaderShade>().raycastTarget=false;
            var title=canvas.Find("Workshop Title").GetComponent<Text>();title.text="GARAGE";title.font=GarageHeading;title.fontSize=82;title.color=Color.white;
            Place(title.rectTransform,new Vector2(.5f,1),new Vector2(0,-85),new Vector2(620,100));
            Caption(canvas,"Garage Subtitle","REPAIR  ·  UPGRADE  ·  GET BACK IN THE FIGHT",22,new(.5f,1),new(0,-146),new(720,35));
            for(int side=-1;side<=1;side+=2) for(int i=0;i<2;i++)
            { var line=VoxelMenuUi.CreatePanel(canvas,"Header Wing",new(.5f,1),new(side*285,-74-i*20),new(135-i*25,11));line.color=new Color(.34f,.39f,.45f);line.raycastTarget=false; }
            var cash=VoxelMenuUi.CreatePanel(canvas,"Cash Frame",new(1,1),new(-290,-80),new(325,80));
            Frame(cash.gameObject,new Color(.32f,.38f,.45f),new Color(.018f,.029f,.044f,.94f));
            currencyText.transform.SetParent(cash.transform,false);Place(currencyText.rectTransform,new(.5f,.5f),new(22,0),new(260,65));
            currencyText.font=GarageMono;currencyText.fontSize=40;currencyText.alignment=TextAnchor.MiddleCenter;currencyText.color=Color.white;
            for(int i=0;i<3;i++)
            {var note=VoxelMenuUi.CreatePanel(cash.transform,"Cash Stack",new(.5f,.5f),new(-127,-12+i*10),new(38,16));note.color=new Color(.16f,.62f+i*.14f,.16f);note.transform.localRotation=Quaternion.Euler(0,0,-15);note.raycastTarget=false;
             var band=VoxelMenuUi.CreatePanel(note.transform,"Cash Band",new(.5f,.5f),Vector2.zero,new(7,16));band.color=new Color(.05f,.30f,.08f);band.raycastTarget=false;}
            Caption(canvas,"Integrity Motto","SAME CAR.\nBIGGER\nMISSIONS.",25,new(0,1),new(395,-195),new(150,140));
            Caption(canvas,"Footer Left","AGENT DRIVE SURVIVE",19,new(0,0),new(220,38),new(380,30));
            Caption(canvas,"Footer Right","WORLDWIDE OPERATIONS",19,new(1,0),new(-230,38),new(400,30));
            garageHome=new GameObject("Garage Home",typeof(RectTransform));garageHome.transform.SetParent(canvas,false);
            var hr=(RectTransform)garageHome.transform;hr.anchorMin=Vector2.zero;hr.anchorMax=Vector2.one;hr.offsetMin=hr.offsetMax=Vector2.zero;
            garageRepairPanel=canvas.Find("Repair Panel").gameObject;
            garageUpgradePanel=canvas.Find("Car Upgrade Panel").gameObject;
            Place((RectTransform)garageRepairPanel.transform,new(0,.5f),new(395,-95),new(680,640));
            Frame(garageRepairPanel,RepairAccent,new Color(.07f,.015f,.023f,.97f));
            var repairLabels=new[]{repair10ButtonLabel,repair25ButtonLabel,repair50ButtonLabel,fullRepairButtonLabel};
            for(int i=0;i<repairLabels.Length;i++)
            {var button=repairLabels[i].GetComponentInParent<Button>();button.transform.SetParent(garageRepairPanel.transform,false);
             Place((RectTransform)button.transform,new(.5f,.5f),new(0,155-i*125),new(610,110));
             Place(repairLabels[i].rectTransform,new(.5f,.5f),Vector2.zero,new(580,105));repairLabels[i].font=GarageMono;repairLabels[i].fontSize=36;
             Frame(button.gameObject,RepairAccent,new Color(.17f,.025f,.035f,.95f));}
            VoxelMenuUi.CreateText(garageRepairPanel.transform,"Repair Heading","REPAIR",54,TextAnchor.MiddleCenter,new(.5f,.5f),new(0,267),new(380,70));
            Place((RectTransform)garageUpgradePanel.transform,new(0,.5f),new(395,-125),new(680,700));
            Frame(garageUpgradePanel,UpgradeAccent,new Color(.013f,.035f,.060f,.97f));
            foreach(var button in garageUpgradePanel.GetComponentsInChildren<Button>(true))
            { Frame(button.gameObject,UpgradeAccent,new Color(.02f,.07f,.115f,.98f));var text=button.GetComponentInChildren<Text>();text.font=GarageMono;text.resizeTextForBestFit=true;text.resizeTextMinSize=18;text.resizeTextMaxSize=28; }
            garageSettingsPanel=VoxelMenuUi.CreatePanel(canvas,"Garage Settings",new(.5f,.5f),Vector2.zero,new(580,300)).gameObject;
            Frame(garageSettingsPanel,new Color(.4f,.48f,.56f),new Color(.02f,.03f,.045f,.98f));
            VoxelMenuUi.CreateText(garageSettingsPanel.transform,"Settings Title","SETTINGS",55,TextAnchor.MiddleCenter,new(.5f,.5f),new(0,85),new(420,80));
            var sound=VoxelMenuUi.CreateButton(garageSettingsPanel.transform,"Sound Toggle","",35,new(.5f,.5f),new(0,-10),new(460,70),()=>{});
            var soundLabel=sound.GetComponentInChildren<Text>();soundLabel.text=AudioListener.volume>0?"SOUND ON":"SOUND OFF";
            sound.onClick.AddListener(()=>{AudioListener.volume=AudioListener.volume>0?0:1;soundLabel.text=AudioListener.volume>0?"SOUND ON":"SOUND OFF";});
            foreach(var panel in new[]{garageRepairPanel,garageUpgradePanel,garageSettingsPanel})
            {var close=VoxelMenuUi.CreateButton(panel.transform,"Close Panel","X",30,new(1,1),new(-34,-33),new(45,42),()=>ShowGaragePanel(null));Frame(close.gameObject,new Color(.4f,.45f,.5f),new Color(.02f,.03f,.04f));}
            HomeButton("Repair Menu Button","REPAIR","RESTORE YOUR VEHICLE",-530,RepairAccent,()=>ShowGaragePanel(garageRepairPanel),false);
            HomeButton("Upgrade Menu Button","UPGRADES","MAKE IT STRONGER",-735,UpgradeAccent,()=>ShowGaragePanel(garageUpgradePanel),true);
            var settings=VoxelMenuUi.CreateButton(canvas,"Settings Button","",40,new(1,1),new(-72,-80),new(80,80),()=>ShowGaragePanel(garageSettingsPanel));
            Frame(settings.gameObject,new Color(.36f,.43f,.5f),new Color(.025f,.035f,.05f,.95f));
            for(int i=0;i<4;i++){var cog=VoxelMenuUi.CreatePanel(settings.transform,"Cog",new(.5f,.5f),Vector2.zero,new(40,15));cog.color=Color.white;cog.transform.localRotation=Quaternion.Euler(0,0,i*45);cog.raycastTarget=false;}
            var hole=VoxelMenuUi.CreatePanel(settings.transform,"Cog Centre",new(.5f,.5f),Vector2.zero,new(16,16));hole.color=new Color(.02f,.03f,.05f);hole.raycastTarget=false;
            Place((RectTransform)NextRaceButton.transform,new(1,0),new(-265,120),new(450,110));
            var nextLabel=NextRaceButton.GetComponentInChildren<Text>();nextLabel.text=">> NEXT MISSION";nextLabel.font=GarageHeading;nextLabel.fontSize=36;nextLabel.color=new Color(1,.91f,.61f);Place(nextLabel.rectTransform,new(.5f,.5f),Vector2.zero,new(425,90));
            Frame(NextRaceButton.gameObject,GoldAccent,new Color(.19f,.105f,.018f,.96f));
            Place(feedbackText.rectTransform,new(.5f,0),new(0,90),new(550,70));feedbackText.fontSize=33;feedbackText.color=Color.white;
            ShowGaragePanel(null);
        }
        private void HomeButton(string name,string title,string subtitle,float y,Color accent,UnityEngine.Events.UnityAction action,bool bars)
        {
            var button=VoxelMenuUi.CreateButton(garageHome.transform,name,title,62,new(0,1),new(390,y),new(650,165),action);
            Frame(button.gameObject,accent,bars?new Color(.015f,.06f,.105f,.96f):new Color(.18f,.016f,.025f,.96f));
            var label=button.GetComponentInChildren<Text>();label.font=GarageHeading;label.fontSize=52;label.alignment=TextAnchor.MiddleLeft;Place(label.rectTransform,new(.5f,.5f),new(45,23),new(380,78));
            Caption(button.transform,"Action Subtitle",subtitle,24,new(.5f,.5f),new(45,-35),new(400,40));
            Caption(button.transform,"Chevrons",">>",53,new(.5f,.5f),new(265,0),new(90,75)).color=Color.white;
            if(bars)for(int i=0;i<3;i++)
            {var icon=VoxelMenuUi.CreatePanel(button.transform,"Action Icon",new(.5f,.5f),new Vector2(-252+i*28,-20+i*15),new Vector2(21,35+i*30));icon.color=Color.white;icon.raycastTarget=false;}
            else
            {
                var tool=new GameObject("Wrench Icon",typeof(RectTransform));tool.transform.SetParent(button.transform,false);
                Place((RectTransform)tool.transform,new(.5f,.5f),new(-225,0),new(100,100));tool.transform.localRotation=Quaternion.Euler(0,0,-42);
                Vector2[] positions={new(0,-25),new(-20,25),new(20,25),new(0,9)};
                Vector2[] sizes={new(18,70),new(16,35),new(16,35),new(50,18)};
                for(int i=0;i<4;i++){var bit=VoxelMenuUi.CreatePanel(tool.transform,"Wrench",new(.5f,.5f),positions[i],sizes[i]);bit.color=Color.white;bit.raycastTarget=false;}
            }
        }
    }
}
