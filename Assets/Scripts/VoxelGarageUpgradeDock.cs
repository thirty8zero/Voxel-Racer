using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        private RectTransform garageCanvas;
        private GameObject garageDockShade;
        private Rect garageCameraOriginalRect = new Rect(0, 0, 1, 1);
        private Vector2 garageLayoutSize;
        private Rect garageLayoutSafeArea;
        private float garageDockTop = 382;
        private VoxelGarageUpgradeCard[] sortedUpgradeCards;
        private Vector3[] garageCarFitPoints = System.Array.Empty<Vector3>();
        private bool garageUpgradeFramingValid;
        private Matrix4x4 garageUpgradeProjection, garageUpgradeBaseProjection;
        private Vector2 garageUpgradeTargetCentre;

        private void BuildBottomUpgradeStrip(Transform panel)
        {
            panel.GetComponent<Image>().enabled = false;
            var heading = panel.Find("Upgrade Title").GetComponent<Text>();
            heading.text = "VEHICLE UPGRADES"; heading.font = GarageMono; heading.fontSize = 24;
            heading.alignment = TextAnchor.MiddleLeft; heading.color = new Color(.68f,.73f,.81f);
            Place(heading.rectTransform, new Vector2(0,1), new Vector2(274,-24), new Vector2(400,32));
            var viewport = new GameObject("Upgrade Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(panel,false);
            var rect = (RectTransform)viewport.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12,20); rect.offsetMax = new Vector2(-12,-52);
            viewport.GetComponent<Image>().color = new Color(0,0,0,.01f);
            var content = new GameObject("Upgrade Cards", typeof(RectTransform)); content.transform.SetParent(viewport.transform,false);
            var cr = (RectTransform)content.transform; cr.anchorMin = new Vector2(0,.5f); cr.anchorMax = new Vector2(0,.5f);
            cr.pivot = new Vector2(0,.5f); cr.anchoredPosition = Vector2.zero;
            var buttons = new[] { leftArmorUpgradeButton, rightArmorUpgradeButton, engineUpgradeButton, gunUpgradeButton,
                leftMissileButton, rightMissileButton, performanceWheelButton, boostUpgradeButton, wheelSpikeUpgradeButton, ploughUpgradeButton };
            var titles = new[] { "ARMOUR\nLEFT", "ARMOUR\nRIGHT", "ENGINE", "MACHINE\nGUNS", "MISSILES\nLEFT", "MISSILES\nRIGHT",
                "TIRES", "BOOST", "WHEEL\nSPIKES", "PLOUGH" };
            var kinds = new[] { VoxelGarageIconKind.Armour, VoxelGarageIconKind.Armour, VoxelGarageIconKind.Engine, VoxelGarageIconKind.Guns,
                VoxelGarageIconKind.Missile, VoxelGarageIconKind.Missile, VoxelGarageIconKind.Wheels, VoxelGarageIconKind.Boost,
                VoxelGarageIconKind.Spikes, VoxelGarageIconKind.Plough };
            cr.sizeDelta = new Vector2(buttons.Length*224-14,234);
            sortedUpgradeCards = new VoxelGarageUpgradeCard[buttons.Length];
            for(int i=0;i<buttons.Length;i++)
            {
                buttons[i].transform.SetParent(content.transform,false);
                Place((RectTransform)buttons[i].transform,new Vector2(0,.5f),new Vector2(105+i*224,0),new Vector2(210,234));
                sortedUpgradeCards[i] = buttons[i].gameObject.AddComponent<VoxelGarageUpgradeCard>();
                sortedUpgradeCards[i].Build(buttons[i],titles[i],kinds[i],GarageHeading,GarageMono,i);
            }
            var scroll=viewport.AddComponent<ScrollRect>(); scroll.viewport=rect; scroll.content=cr;
            scroll.horizontal=true; scroll.vertical=false; scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity=55;
            Caption(panel,"Upgrade Scroll Hint","SWIPE / SCROLL  >",18,new Vector2(.5f,0),new Vector2(0,8),new Vector2(380,22));
            var dockBackground=VoxelMenuUi.CreatePanel(panel.parent,"Upgrade Dock Shade",new Vector2(.5f,0),new Vector2(0,190),new Vector2(4000,380));
            dockBackground.color=new Color(.014f,.020f,.032f,.14f); dockBackground.raycastTarget=false;
            garageDockShade=dockBackground.gameObject;garageDockShade.transform.SetAsFirstSibling();
        }

        private void StyleNextMission()
        {
            Frame(NextRaceButton.gameObject,new Color(1,.83f,.38f),new Color(.63f,.36f,.075f));
            var frame=NextRaceButton.GetComponentInChildren<VoxelGaragePanel>();
            frame.useGradient=true; frame.topColor=new Color(1,.77f,.28f); frame.SetVerticesDirty();
            var label=NextRaceButton.GetComponentInChildren<Text>();
            label.font=GarageHeading; label.fontSize=52; label.color=new Color(.12f,.09f,.025f);
            label.resizeTextForBestFit=true; label.resizeTextMinSize=32; label.resizeTextMaxSize=52;
            Place(label.rectTransform,new Vector2(.5f,.5f),new Vector2(-40,0),new Vector2(250,140));
            var arrow=new GameObject("Mission Chevrons",typeof(RectTransform),typeof(VoxelGarageUpgradeIcon));
            arrow.transform.SetParent(NextRaceButton.transform,false);
            Place((RectTransform)arrow.transform,new Vector2(.5f,.5f),new Vector2(120,0),new Vector2(60,80));
            var graphic=arrow.GetComponent<VoxelGarageUpgradeIcon>(); graphic.kind=VoxelGarageIconKind.Next;
            graphic.color=new Color(.16f,.11f,.025f); graphic.raycastTarget=false;
            RefreshNextMission();
        }
        private void RefreshNextMission()
        {
            if(NextRaceButton==null) return;
            NextRaceButton.GetComponentInChildren<Text>().text="NEXT\nMISSION "+(VoxelTrackProgressState.NextTrackIndex+1);
        }
        private void LayoutGarageDock()
        {
            if(garageCanvas==null || garageUpgradePanel==null || NextRaceButton==null) return;
            Vector2 size=garageCanvas.rect.size; Rect safe=Screen.safeArea;
            if(size==garageLayoutSize && safe==garageLayoutSafeArea) return;
            garageLayoutSize=size; garageLayoutSafeArea=safe;
            garageUpgradeFramingValid=false;
            float sx=size.x/Mathf.Max(1,Screen.width), sy=size.y/Mathf.Max(1,Screen.height);
            float left=Mathf.Max(36,safe.xMin*sx+18);
            float right=Mathf.Max(36,(Screen.width-safe.xMax)*sx+18);
            float bottom=Mathf.Max(54,safe.yMin*sy+20);
            const float missionWidth=350, gap=24, height=310;
            var panel=(RectTransform)garageUpgradePanel.transform;
            panel.anchorMin=new Vector2(0,0); panel.anchorMax=new Vector2(1,0); panel.pivot=new Vector2(.5f,0);
            panel.offsetMin=new Vector2(left,bottom); panel.offsetMax=new Vector2(-right-missionWidth-gap,bottom+height);
            // The cards are centred inside the viewport's 20px bottom / 52px top insets.
            float cardCentre=bottom+(height+20-52)*.5f;
            Place((RectTransform)NextRaceButton.transform,new Vector2(1,0),new Vector2(-right-missionWidth*.5f,cardCentre),new Vector2(missionWidth,172));
            garageDockTop=bottom+height+18;
            var backdrop=(RectTransform)garageDockShade.transform;
            backdrop.anchorMin=Vector2.zero;backdrop.anchorMax=new Vector2(1,0);backdrop.pivot=new Vector2(.5f,0);
            backdrop.anchoredPosition=Vector2.zero;backdrop.sizeDelta=new Vector2(0,garageDockTop);
            LayoutGarageFeedback();
            ApplyGaragePreviewViewport();
        }
        private void LayoutGarageFeedback()
        {
            if (feedbackText == null || garageCanvas == null) return;
            if (garageUpgradePanel.activeSelf)
            {
                var panel = (RectTransform)garageUpgradePanel.transform;
                Place(feedbackText.rectTransform, Vector2.zero,
                    new Vector2(panel.offsetMin.x + panel.rect.width*.5f, panel.offsetMax.y + 32),
                    new Vector2(panel.rect.width,36));
            }
            else Place(feedbackText.rectTransform,new Vector2(.5f,1),new Vector2(0,-210),new Vector2(850,36));
        }
        private static Vector3 BoundsCorner(Bounds bounds, int index)
        {
            return bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((index&1)==0?-1:1,(index&2)==0?-1:1,(index&4)==0?-1:1));
        }
        private void CacheGarageCarBounds()
        {
            if (DisplayedCar == null) return;
            // Cache the swept silhouette for a complete turn around the fixed pivot.
            // The framing uses this envelope once, rather than following each pose.
            var radii = new System.Collections.Generic.Dictionary<float,float>();
            foreach (var filter in DisplayedCar.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
                var toCar = DisplayedCar.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i=0;i<8;i++)
                {
                    var point = toCar.MultiplyPoint3x4(BoundsCorner(filter.sharedMesh.bounds,i));
                    float radius = new Vector2(point.x,point.z).magnitude;
                    radii.TryGetValue(point.y,out float previous);
                    radii[point.y]=Mathf.Max(previous,radius);
                }
            }
            const int steps=72;
            garageCarFitPoints = new Vector3[radii.Count*steps];
            int index=0;
            // Circumscribed rings enclose the intervening angles too.
            float padding=1/Mathf.Cos(Mathf.PI/steps);
            foreach(var ring in radii)
            for(int step=0;step<steps;step++)
            {
                float angle=step*2*Mathf.PI/steps;
                garageCarFitPoints[index++]=new Vector3(Mathf.Cos(angle)*ring.Value*padding,ring.Key,
                    Mathf.Sin(angle)*ring.Value*padding);
            }
        }
        private void ApplyGaragePreviewViewport()
        {
            if(workshopCamera==null || garageCanvas==null) return;
            workshopCamera.rect=garageCameraOriginalRect;
            workshopCamera.ResetProjectionMatrix();
            if(garageUpgradePanel==null || !garageUpgradePanel.activeSelf) return;
            if (garageCarFitPoints.Length == 0 || DisplayedCar == null) return;
            float baseFov=Mathf.Clamp(cameraTuning!=null?cameraTuning.cameraFieldOfView:workshopCamera.fieldOfView+garageZoomAmount,10,90);
            var baseProjection=Matrix4x4.Perspective(baseFov,workshopCamera.aspect,workshopCamera.nearClipPlane,workshopCamera.farClipPlane);
            if(!garageUpgradeFramingValid || baseProjection!=garageUpgradeBaseProjection)
                FitGarageUpgradeView(baseProjection);
            // Zoom the fixed composition around its centre; rotating or buying an
            // upgrade must not change the camera's scale or framing.
            float zoom=Mathf.Tan(baseFov*Mathf.Deg2Rad*.5f)/Mathf.Tan(workshopCamera.fieldOfView*Mathf.Deg2Rad*.5f);
            var projection=garageUpgradeProjection;
            projection.m00*=zoom;projection.m11*=zoom;
            projection.m02=projection.m02*zoom+(zoom-1)*garageUpgradeTargetCentre.x;
            projection.m12=projection.m12*zoom+(zoom-1)*garageUpgradeTargetCentre.y;
            workshopCamera.projectionMatrix=projection;
        }
        private void FitGarageUpgradeView(Matrix4x4 projection)
        {
            garageUpgradeBaseProjection=projection;
            // Ignore the car's current rotation. The cached rings cover every yaw.
            var fixedPivot=Matrix4x4.TRS(DisplayedCar.transform.position,Quaternion.identity,DisplayedCar.transform.lossyScale);
            var toClip = projection * workshopCamera.worldToCameraMatrix * fixedPivot;
            var min = new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for (int i=0;i<garageCarFitPoints.Length;i++)
            {
                var point = toClip.MultiplyPoint(garageCarFitPoints[i]);
                min=Vector2.Min(min,new Vector2(point.x,point.y));
                max=Vector2.Max(max,new Vector2(point.x,point.y));
            }
            var size=garageCanvas.rect.size;
            float marginX=Mathf.Max(48,Screen.safeArea.xMin*size.x/Mathf.Max(1,Screen.width)+18);
            float marginRight=Mathf.Max(48,(Screen.width-Screen.safeArea.xMax)*size.x/Mathf.Max(1,Screen.width)+18);
            float top=size.y-Mathf.Max(210,(Screen.height-Screen.safeArea.yMax)*size.y/Mathf.Max(1,Screen.height)+18);
            float bottom=garageDockTop+66;
            var targetMin=new Vector2(2*marginX/size.x-1,2*bottom/size.y-1);
            var targetMax=new Vector2(1-2*marginRight/size.x,2*top/size.y-1);
            var extent=max-min;
            float scale=Mathf.Min((targetMax.x-targetMin.x)/Mathf.Max(.001f,extent.x),
                (targetMax.y-targetMin.y)/Mathf.Max(.001f,extent.y));
            scale=Mathf.Max(.01f,scale);
            var offset=(targetMin+targetMax)*.5f-(min+max)*(.5f*scale);
            projection.m00*=scale;projection.m11*=scale;
            projection.m02-=offset.x;projection.m12-=offset.y;
            garageUpgradeProjection=projection;
            garageUpgradeTargetCentre=(targetMin+targetMax)*.5f;
            garageUpgradeFramingValid=true;
        }
        private void OnDestroy()
        {
            if(workshopCamera!=null) { workshopCamera.rect=garageCameraOriginalRect;workshopCamera.ResetProjectionMatrix(); }
        }
        private static void Card(Button button,int cost,bool compatible,bool owned)
        { if(button!=null) button.GetComponent<VoxelGarageUpgradeCard>()?.Refresh(cost,compatible,owned?1:0); }
        private void RefreshUpgradeCards()
        {
            var armour=VoxelArmorTuning.Load(); bool fits=armour!=null && armour.panelPrefab!=null && armour.Fits(definition);
            Card(leftArmorUpgradeButton,armour!=null?armour.panelPurchasePrice:0,fits,VoxelArmorUpgradeState.IsPurchasedFor(VoxelArmorSide.Left));
            Card(rightArmorUpgradeButton,armour!=null?armour.panelPurchasePrice:0,fits,VoxelArmorUpgradeState.IsPurchasedFor(VoxelArmorSide.Right));
            var engine=VoxelEngineUpgradeTuning.Load(); Card(engineUpgradeButton,engine!=null?engine.purchasePrice:0,engine!=null && engine.Fits(definition),VoxelEngineUpgradeState.IsPurchased);
            var gun=VoxelGunUpgradeState.LongGunTuning;
            gunUpgradeButton?.GetComponent<VoxelGarageUpgradeCard>()?.Refresh(gun!=null?gun.purchasePrice:0,gun!=null && gun.visualPrefab!=null,
                VoxelGunUpgradeState.PurchasedLongGunCount,gun!=null?gun.maximumPurchases:1);
            var missile=VoxelMissileLauncherTuning.Load(); fits=missile!=null && missile.Fits(definition);
            Card(leftMissileButton,missile!=null && missile.weapon!=null?missile.weapon.purchasePrice:0,fits,VoxelMissileUpgradeState.IsPurchased(false));
            Card(rightMissileButton,missile!=null && missile.weapon!=null?missile.weapon.purchasePrice:0,fits,VoxelMissileUpgradeState.IsPurchased(true));
            var wheels=VoxelPerformanceWheelTuning.Load(); Card(performanceWheelButton,wheels!=null?wheels.purchasePrice:0,wheels!=null && wheels.Fits(definition),VoxelPerformanceWheelUpgradeState.IsPurchased);
            var boost=VoxelBoostUpgradeTuning.LoadUpgrade(); Card(boostUpgradeButton,boost!=null?boost.purchasePrice:0,boost!=null && boost.Fits(definition),VoxelBoostUpgradeState.IsPurchased);
            var spikes=VoxelWheelSpikeTuning.Load(); Card(wheelSpikeUpgradeButton,spikes!=null?spikes.purchasePrice:0,spikes!=null && spikes.spikePrefab!=null,VoxelWheelSpikeUpgradeState.IsPurchased);
            var plough=VoxelPloughTuning.Load(); Card(ploughUpgradeButton,plough!=null?plough.purchasePrice:0,plough!=null && plough.Fits(definition),VoxelPloughUpgradeState.IsPurchased);
            SortUpgradeCards();
            CacheGarageCarBounds();
        }
        private void SortUpgradeCards()
        {
            if (sortedUpgradeCards == null) return;
            System.Array.Sort(sortedUpgradeCards, (a,b) =>
            {
                int comparison=a.FullyPurchased.CompareTo(b.FullyPurchased);
                if(comparison==0) comparison=a.Cost.CompareTo(b.Cost);
                return comparison!=0?comparison:a.CatalogueOrder.CompareTo(b.CatalogueOrder);
            });
            for(int i=0;i<sortedUpgradeCards.Length;i++)
            {
                var card=sortedUpgradeCards[i];
                card.transform.SetSiblingIndex(i);
                ((RectTransform)card.transform).anchoredPosition=new Vector2(105+i*224,0);
            }
        }
    }
}
