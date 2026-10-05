using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelGarageUpgradeDockValidation
    {
        private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic;
        private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
        private static readonly Type[] States={typeof(VoxelGunUpgradeState),typeof(VoxelArmorUpgradeState),typeof(VoxelWheelSpikeUpgradeState),
            typeof(VoxelPerformanceWheelUpgradeState),typeof(VoxelBoostUpgradeState),typeof(VoxelEngineUpgradeState),typeof(VoxelPloughUpgradeState),typeof(VoxelMissileUpgradeState)};
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Refresh(VoxelRepairUpgradeSceneController shop)
        {
            typeof(VoxelRepairUpgradeSceneController).GetMethod("RefreshUi",Instance).Invoke(shop,null);
            CheckCardOrder(shop.GetComponentsInChildren<VoxelGarageUpgradeCard>(true));
        }
        private static void CheckCardOrder(VoxelGarageUpgradeCard[] cards)
        {
            var ordered=cards.OrderBy(c=>((RectTransform)c.transform).anchoredPosition.x).ToArray();
            for(int i=1;i<ordered.Length;i++)
            {
                var a=ordered[i-1];var b=ordered[i];
                Check(!a.FullyPurchased || b.FullyPurchased,"A fully purchased upgrade precedes an available upgrade");
                if(a.FullyPurchased==b.FullyPurchased)
                    Check(a.Cost<b.Cost || a.Cost==b.Cost && a.CatalogueOrder<b.CatalogueOrder,"Cards are not in stable ascending price order");
                Check(b.transform.GetSiblingIndex()==i,"Card hierarchy order and visual order disagree");
            }
        }
        private static void ResetPurchases(){foreach(var type in States)type.GetMethod("BeginNewRun").Invoke(null,null);}
        public sealed class SavedState : IDisposable
        {
            private readonly Dictionary<FieldInfo,object> fields=new();
            private readonly HashSet<string> missing=(HashSet<string>)typeof(VoxelCarRunState).GetField("missingVoxelPaths",Static).GetValue(null);
            private readonly Dictionary<string,int> armour=(Dictionary<string,int>)typeof(VoxelCarRunState).GetField("armorHealth",Static).GetValue(null);
            private readonly HashSet<string> savedMissing;
            private readonly Dictionary<string,int> savedArmour;
            public SavedState()
            {
                foreach(var type in States.Concat(new[]{typeof(VoxelTrackProgressState),typeof(VoxelCurrencyState),typeof(VoxelCarRunState)}))
                    foreach(var field in type.GetFields(Static)) if(!field.IsLiteral && !field.IsInitOnly)fields[field]=field.GetValue(null);
                savedMissing=new HashSet<string>(missing); savedArmour=new Dictionary<string,int>(armour);
            }
            public void Dispose()
            {
                foreach(var pair in fields)pair.Key.SetValue(null,pair.Value);
                missing.Clear();foreach(var value in savedMissing)missing.Add(value);
                armour.Clear();foreach(var pair in savedArmour)armour.Add(pair.Key,pair.Value);
            }
        }
        public static void CheckShop(VoxelRepairUpgradeSceneController shop,VoxelCarController car,VoxelCarDefinition definition)
        {
            var cards=shop.GetComponentsInChildren<VoxelGarageUpgradeCard>(true);
            Check(cards.Length==8,"Some upgrade types are missing or side cards were not combined");
            ResetPurchases();VoxelCurrencyState.Reset();Refresh(shop);
            Check(cards.All(c=>c.State==VoxelGarageUpgradeState.Unaffordable && !c.GetComponent<Button>().interactable),"Unaffordable cards must be disabled");
            foreach(var card in cards)card.GetComponent<Button>().onClick.Invoke();
            Check(VoxelCurrencyState.Balance==0 && VoxelGunUpgradeState.PurchasedLongGunCount==0 && !VoxelEngineUpgradeState.IsPurchased &&
                !VoxelMissileUpgradeState.IsPurchased(false) && !VoxelMissileUpgradeState.IsPurchased(true),"Unaffordable callbacks spent or equipped upgrades");
            VoxelCurrencyState.Add(100000);Refresh(shop);
            Check(cards.All(c=>c.State==VoxelGarageUpgradeState.Affordable && c.GetComponent<Button>().interactable),"Affordable cards must allow purchase");
            foreach(var type in States)foreach(var f in type.GetFields(Static))
                if(!f.IsLiteral && !f.IsInitOnly && f.FieldType==typeof(bool))f.SetValue(null,true);
            typeof(VoxelGunUpgradeState).GetField("purchasedLongGunCount",Static).SetValue(null,VoxelGunUpgradeState.LongGunTuning.maximumPurchases);
            VoxelCurrencyState.Reset();Refresh(shop);
            Check(cards.All(c=>c.State==VoxelGarageUpgradeState.Equipped && !c.GetComponent<Button>().interactable),"Equipped state must persist with an empty wallet");
            ResetPurchases();typeof(VoxelGunUpgradeState).GetField("purchasedLongGunCount",Static).SetValue(null,1);
            VoxelCurrencyState.Add(100000);Refresh(shop);
            Check(shop.GetComponentsInChildren<VoxelGarageUpgradeCard>(true).First(c=>c.name=="Long Gun Purchase Button").State==VoxelGarageUpgradeState.Affordable,
                "One equipped gun must leave the next gun slot purchasable");
            var incompatible=ScriptableObject.CreateInstance<VoxelCarDefinition>();
            try
            {
                typeof(VoxelRepairUpgradeSceneController).GetField("definition",Instance).SetValue(shop,incompatible);Refresh(shop);
                Check(cards.Where(c=>c.name.Contains("Armor") || c.name.Contains("Missile") || c.name.Contains("Engine") || c.name.Contains("Performance"))
                    .All(c=>c.State==VoxelGarageUpgradeState.Incompatible && !c.GetComponent<Button>().interactable),"Compatibility gates were lost");
            }
            finally{typeof(VoxelRepairUpgradeSceneController).GetField("definition",Instance).SetValue(shop,definition);Object.DestroyImmediate(incompatible);}
            CheckMissionPreview(shop);
            ResetPurchases();VoxelCurrencyState.Reset();VoxelCurrencyState.Add(100000);Refresh(shop);
            var damaged=car.GetComponentsInChildren<MeshRenderer>().First(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null);
            damaged.gameObject.SetActive(false);int missing=car.RepairableIntegrityVoxels;
            int balance=VoxelCurrencyState.Balance;
            cards.First(c=>c.name=="V6 Engine Purchase Button").GetComponent<Button>().onClick.Invoke();
            Check(VoxelCurrencyState.Balance==balance && !VoxelEngineUpgradeState.IsPurchased,"Preview purchased the engine before confirmation");
            typeof(VoxelRepairUpgradeSceneController).GetMethod("ConfirmUpgradePurchase",Instance).Invoke(shop,null);
            cards.First(c=>c.name=="Left Missile Purchase Button").GetComponent<Button>().onClick.Invoke();
            typeof(VoxelRepairUpgradeSceneController).GetMethod("SelectUpgradePlacement",Instance).Invoke(shop,new object[]{0});
            typeof(VoxelRepairUpgradeSceneController).GetMethod("ConfirmUpgradePurchase",Instance).Invoke(shop,null);
            Check(VoxelEngineUpgradeState.IsPurchased && VoxelMissileUpgradeState.IsPurchased(false) && !VoxelMissileUpgradeState.IsPurchased(true),"Actual callbacks failed to install independent upgrades");
            CheckCardOrder(cards);
            Check(VoxelCurrencyState.Balance==balance-VoxelEngineUpgradeTuning.Load().purchasePrice-VoxelMissileLauncherTuning.Load().weapon.purchasePrice,"Purchase prices charged incorrectly");
            Check(!damaged.gameObject.activeSelf && car.RepairableIntegrityVoxels==missing,"Purchase repaired existing damage");
            damaged.gameObject.SetActive(true);car.ResetIntegrityBaseline();
            VoxelCurrencyState.Reset();VoxelCurrencyState.Add(200);Refresh(shop);
        }
        private static void CheckMissionPreview(VoxelRepairUpgradeSceneController shop)
        {
            var sequenceField=typeof(VoxelTrackProgressState).GetField("sequence",Static);
            var indexField=typeof(VoxelTrackProgressState).GetField("currentTrackIndex",Static);
            object oldSequence=sequenceField.GetValue(null),oldIndex=indexField.GetValue(null);
            var sequence=ScriptableObject.CreateInstance<VoxelTrackSequence>();
            try
            {
                sequence.tracks=VoxelTrackSequence.Load().tracks.Take(3).ToArray();sequenceField.SetValue(null,sequence);
                foreach(bool loop in new[]{false,true})
                foreach(int current in new[]{0,1,2})
                {
                    sequence.loopSequence=loop;indexField.SetValue(null,current);
                    int expected=current<2?current+1:loop?0:2;
                    Check(VoxelTrackProgressState.NextTrackIndex==expected && VoxelTrackProgressState.NextTrack==sequence.tracks[expected] &&
                        VoxelTrackProgressState.CurrentTrackIndex==current,"Mission preview must not advance campaign");
                    Refresh(shop);
                    Check(shop.NextRaceButton.GetComponentInChildren<Text>().text==sequence.tracks[expected].displayName.Trim(),"Continue button must show only the next track display name");
                    Check(VoxelTrackProgressState.AdvanceToNextTrack()==sequence.tracks[expected],"Mission preview and next race disagree");
                }
                sequence.tracks=Array.Empty<VoxelTrackDefinition>();indexField.SetValue(null,0);
                Check(VoxelTrackProgressState.NextTrack==null && VoxelTrackProgressState.NextTrackIndex==0,"Empty sequence preview failed");
                Refresh(shop);
                Check(shop.NextRaceButton.GetComponentInChildren<Text>().text=="Mission","Empty sequence name fallback failed");
            }
            finally{sequenceField.SetValue(null,oldSequence);indexField.SetValue(null,oldIndex);Object.DestroyImmediate(sequence);Refresh(shop);}
        }
        public static void CheckLayout(VoxelRepairUpgradeSceneController shop,Camera camera,bool upgrades)
        {
            var canvas=shop.transform.Find("Repair Upgrade UI").GetComponent<RectTransform>();
            var panel=canvas.Find("Car Upgrade Panel").GetComponent<RectTransform>();
            var next=(RectTransform)shop.NextRaceButton.transform;
            Bounds cash=RectBounds(canvas,(RectTransform)canvas.Find("Cash Frame"));
            Bounds settings=RectBounds(canvas,(RectTransform)canvas.Find("Settings Button"));
            Check(settings.max.x+16<=cash.min.x && Mathf.Abs(settings.center.y-cash.center.y)<.1f,
                "Settings cog must sit immediately left of cash");
            Check(cash.max.x<=canvas.rect.xMax && cash.max.y<=canvas.rect.yMax,"Cash tab escapes the screen");
            Bounds a=RectBounds(canvas,panel),b=RectBounds(canvas,next);
            Check(a.max.x+10<b.min.x,"Upgrade strip overlaps next mission button");
            Check(a.min.y>=canvas.rect.yMin && b.min.y>=canvas.rect.yMin && b.max.x<=canvas.rect.xMax+.1f,"Dock escapes screen bounds");
            var scroll=panel.GetComponentInChildren<ScrollRect>(true);
            Check(scroll.horizontal && !scroll.vertical && scroll.content.GetComponentsInChildren<VoxelGarageUpgradeCard>().Length==8,
                "Horizontal catalogue missing");
            Check(camera.rect==new Rect(0,0,1,1),"Garage must render the floor behind the translucent cards");
            if(upgrades)
            {
                var feedback=canvas.Find("Repair Feedback").GetComponent<RectTransform>();
                Bounds f=RectBounds(canvas,feedback);
                Check(f.min.y>a.max.y+10 && f.min.x>=a.min.x-.1f && f.max.x<=a.max.x+.1f,"Purchase feedback must sit just above the cards");
                var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                foreach(var filter in shop.DisplayedCar.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=filter.GetComponent<MeshRenderer>();
                    if(filter.sharedMesh==null || renderer==null || !renderer.enabled)continue;
                    var bounds=filter.sharedMesh.bounds;
                    for(int i=0;i<8;i++)
                    {
                        var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        var point=camera.WorldToViewportPoint(filter.transform.TransformPoint(corner));
                        min=Vector2.Min(min,new Vector2(point.x,point.y));max=Vector2.Max(max,new Vector2(point.x,point.y));
                    }
                }
                float bottom=((float)typeof(VoxelRepairUpgradeSceneController).GetField("garageDockTop",Instance).GetValue(shop)+66)/canvas.rect.height;
                float top=1-Mathf.Max(210,(Screen.height-Screen.safeArea.yMax)*canvas.rect.height/Mathf.Max(1,Screen.height)+18)/canvas.rect.height;
                float left=Mathf.Max(48,Screen.safeArea.xMin*canvas.rect.width/Mathf.Max(1,Screen.width)+18)/canvas.rect.width;
                float right=1-Mathf.Max(48,(Screen.width-Screen.safeArea.xMax)*canvas.rect.width/Mathf.Max(1,Screen.width)+18)/canvas.rect.width;
                Check(min.x>=left-.001f && max.x<=right+.001f && min.y>=bottom-.001f && max.y<=top+.001f,"Car is clipped or overlaps the header/feedback/cards");
            }
            else Check(Mathf.Abs(camera.projectionMatrix.m12)<.001f,"Closing upgrades did not restore camera framing");
        }
        public static void CheckPurchaseLayout(VoxelRepairUpgradeSceneController shop)
        {
            var canvas=(RectTransform)shop.transform.Find("Repair Upgrade UI");
            var dialog=(RectTransform)canvas.Find("Upgrade Purchase Confirmation/Purchase Dialog");
            Bounds cash=RectBounds(canvas,(RectTransform)canvas.Find("Cash Frame"));
            Bounds purchase=RectBounds(canvas,dialog);
            Check(Mathf.Abs(purchase.min.x-cash.min.x)<.1f && Mathf.Abs(purchase.max.x-cash.max.x)<.1f,
                "Purchase tab must match the cash tab's width and side alignment");
            Check(Mathf.Abs(cash.min.y-purchase.max.y-20)<.1f && purchase.size.y>purchase.size.x,
                "Purchase tab must be vertical and sit below cash");
            Bounds confirm=RectBounds(canvas,(RectTransform)dialog.Find("Confirm Upgrade Purchase"));
            Bounds cancel=RectBounds(canvas,(RectTransform)dialog.Find("Cancel Upgrade Purchase"));
            Check(confirm.min.y>=cancel.max.y+16 && confirm.max.x<purchase.max.x && confirm.min.x>purchase.min.x &&
                cancel.min.y>purchase.min.y,"Confirmation buttons must be stacked and contained within the tab");
            Bounds mission=RectBounds(canvas,(RectTransform)shop.NextRaceButton.transform);
            Check(purchase.min.y>=mission.max.y+19.9f,"Purchase tab overlaps the mission start button");
            var scroll=dialog.GetComponentInChildren<ScrollRect>();
            Check(scroll!=null && !scroll.horizontal && scroll.verticalScrollbar!=null,
                "Pending parts must have a vertical scrollable list");
            Bounds list=RectBounds(canvas,scroll.viewport);
            Check(list.min.y>confirm.max.y+16 && list.min.x>purchase.min.x && list.max.x<purchase.max.x,
                "Pending list overlaps the confirmation buttons or escapes the tab");
            if(scroll.content.rect.height>scroll.viewport.rect.height+1)
            {
                Check(scroll.vertical && Mathf.Abs(purchase.min.y-mission.max.y-20)<.1f,
                    "Purchase list scrolls before using all room above the mission button");
                scroll.verticalNormalizedPosition=0;
                Bounds bottom=RectBounds(canvas,scroll.content);
                Check(Mathf.Abs(bottom.min.y-list.min.y)<1,"Cannot scroll to the final pending part");
                Check(dialog.Find("Pending Parts Scroll Hint").gameObject.activeSelf,"Overflow list lacks a scroll hint");
                scroll.verticalNormalizedPosition=1;
            }
            else Check(!scroll.vertical && !dialog.Find("Pending Parts Scroll Hint").gameObject.activeSelf,
                "Purchase list scrolls when its text fits");
        }

        public static void CheckPurchaseGrowth(VoxelRepairUpgradeSceneController shop)
        {
            var canvas=(RectTransform)shop.transform.Find("Repair Upgrade UI");
            var dialog=(RectTransform)canvas.Find("Upgrade Purchase Confirmation/Purchase Dialog");
            var description=dialog.GetComponentInChildren<ScrollRect>().content.GetComponent<Text>();
            string original=description.text;
            void Layout(string text)
            {
                description.text=text;
                typeof(VoxelRepairUpgradeSceneController).GetField("purchaseListLayoutDirty",Instance).SetValue(shop,true);
                typeof(VoxelRepairUpgradeSceneController).GetMethod("LayoutPurchaseConfirmation",Instance).Invoke(shop,null);
                Canvas.ForceUpdateCanvases(); CheckPurchaseLayout(shop);
            }
            try
            {
                Layout("ENGINE"); float shortHeight=dialog.rect.height;
                Layout(string.Join("\n",Enumerable.Repeat("PART TO FIT",14)));
                Check(dialog.rect.height>shortHeight && !dialog.GetComponentInChildren<ScrollRect>().vertical,
                    "Confirmation must grow to fit medium text before scrolling: short="+shortHeight+", medium="+dialog.rect.height+", text="+description.preferredHeight);
                Layout(string.Join("\n",Enumerable.Repeat("PART TO FIT",40)));
                Check(dialog.GetComponentInChildren<ScrollRect>().vertical,"Long list must scroll after reaching the mission button");
            }
            finally { Layout(original); }
        }
        public static void CheckStaticCamera(VoxelRepairUpgradeSceneController shop,Camera camera,bool upgrades)
        {
            var car=shop.DisplayedCar.transform;
            var rotation=car.localRotation;
            var projection=camera.projectionMatrix;
            var position=camera.transform.position;
            var cameraRotation=camera.transform.rotation;
            float fov=camera.fieldOfView;
            void Apply()=>typeof(VoxelRepairUpgradeSceneController).GetMethod("ApplyGaragePreviewViewport",Instance).Invoke(shop,null);
            void Stable(Matrix4x4 expected,float expectedFov)
            {
                Check(camera.transform.position==position && Quaternion.Angle(camera.transform.rotation,cameraRotation)<.001f &&
                    Mathf.Approximately(camera.fieldOfView,expectedFov),"Car rotation moved the garage camera or changed its field of view");
                for(int i=0;i<16;i++)Check(Mathf.Abs(camera.projectionMatrix[i]-expected[i])<.00001f,
                    "Car rotation changed the garage framing/zoom");
            }
            void Zoom(float amount)
            {
                typeof(VoxelRepairUpgradeSceneController).GetMethod("ApplyGarageZoom",Instance).Invoke(shop,new object[]{amount});
                Apply();
            }
            try
            {
                for(int angle=0;angle<360;angle+=10)
                {
                    car.localRotation=Quaternion.Euler(0,angle,0);Apply();Stable(projection,fov);
                    CheckLayout(shop,camera,upgrades);
                }
                // Shop refreshes and re-opening the same panel must preserve the view.
                Refresh(shop);Apply();Stable(projection,fov);
                Zoom(1.25f);
                Check(camera.fieldOfView<fov && camera.projectionMatrix.m00>projection.m00,"Player zoom-in was cancelled by automatic fitting");
                var zoomed=camera.projectionMatrix;float zoomedFov=camera.fieldOfView;
                car.Rotate(0,67,0);Apply();Stable(zoomed,zoomedFov);
                Zoom(-2.5f);
                Check(camera.fieldOfView>fov && camera.projectionMatrix.m00<projection.m00,"Player zoom-out was cancelled by automatic fitting");
                Zoom(1.25f);Stable(projection,fov);
                if(upgrades)
                {
                    var panel=shop.transform.Find("Repair Upgrade UI/Car Upgrade Panel").gameObject;
                    var show=typeof(VoxelRepairUpgradeSceneController).GetMethod("ShowGaragePanel",Instance);
                    show.Invoke(shop,new object[]{null});show.Invoke(shop,new object[]{panel});
                    Stable(projection,fov);
                }
            }
            finally{car.localRotation=rotation;Apply();}
        }
        private static Bounds RectBounds(RectTransform canvas,RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var bounds=new Bounds(canvas.InverseTransformPoint(corners[0]),Vector3.zero);
            foreach(var corner in corners)bounds.Encapsulate(canvas.InverseTransformPoint(corner));
            return bounds;
        }
    }
}
