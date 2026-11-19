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
            typeof(VoxelPerformanceWheelUpgradeState),typeof(VoxelBoostUpgradeState),typeof(VoxelEngineUpgradeState),typeof(VoxelPloughUpgradeState),typeof(VoxelMissileUpgradeState),typeof(VoxelUnderbodyArmorUpgradeState)};
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        internal static Button RevealUpgradeCard(Button button)
        {
            var card = button.GetComponent<VoxelGarageUpgradeCard>();
            var shop = card.GetComponentInParent<VoxelRepairUpgradeSceneController>();
            var canvas = shop.transform.Find("Repair Upgrade UI");
            var dock = canvas.Find("Car Upgrade Panel");
            if (!dock.gameObject.activeSelf) canvas.Find("Garage Home/Upgrade Menu Button").GetComponent<Button>().onClick.Invoke();
            if (!button.gameObject.activeSelf) dock.Find("Category " + card.Category).GetComponent<Button>().onClick.Invoke();
            return button;
        }
        private static void Refresh(VoxelRepairUpgradeSceneController shop)
        {
            typeof(VoxelRepairUpgradeSceneController).GetMethod("RefreshUi",Instance).Invoke(shop,null);
            CheckCardOrder(shop.GetComponentsInChildren<VoxelGarageUpgradeCard>(true));
        }
        private static void CheckCardOrder(VoxelGarageUpgradeCard[] cards)
        {
            var visible = cards.Where(c => c.gameObject.activeSelf).OrderBy(c => ((RectTransform)c.transform).anchoredPosition.x).ToArray();
            var expected = visible.OrderBy(c => c.State != VoxelGarageUpgradeState.Affordable)
                .ThenBy(c => c.State == VoxelGarageUpgradeState.Locked)
                .ThenBy(c => c.State == VoxelGarageUpgradeState.Affordable ? 0 : c.Level)
                .ThenBy(c => c.Cost).ThenBy(c => c.CatalogueOrder);
            Check(visible.SequenceEqual(expected), "Cards lost affordable-first, unlocked-before-locked ordering");
            Check(cards.All(c => !c.FullyPurchased || !c.gameObject.activeSelf), "A fully purchased card is still visible");
            for (int i = 0; i < visible.Length; i++)
            {
                Check(Mathf.Abs(((RectTransform)visible[i].transform).anchoredPosition.x - (105 + i * 224)) < .1f, "Upgrade row contains gaps");
                if (i > 0) Check(visible[i].transform.GetSiblingIndex() > visible[i - 1].transform.GetSiblingIndex(),
                    "Card hierarchy order and visual order disagree");
            }
        }
        public static void CheckCategories(VoxelRepairUpgradeSceneController shop)
        {
            var cards=shop.GetComponentsInChildren<VoxelGarageUpgradeCard>(true);
            var expected=new[] {
                new[]{"Left Door Armor Purchase Button","Extended Door Armor Purchase Button","Underbody Armor Purchase Button","Plough Purchase Button","Bull Bar Purchase Button"},
                new[]{"Performance Wheel Purchase Button","Gold Track Wheel Purchase Button","Wheel Spike Purchase Button","Star Wheel Spike Purchase Button"},
                new[]{"Long Gun Purchase Button","Minigun Purchase Button"},new[]{"Left Missile Purchase Button"},
                new[]{"Boost Bottle Purchase Button","Dual Boost Bottle Purchase Button"},new[]{"V6 Engine Purchase Button"} };
            var labels=new[]{"Armor","Wheels &\nTires","Guns","Missiles","Boost","Engines"};
            var categoryField=typeof(VoxelRepairUpgradeSceneController).GetField("upgradeCategory",Instance);
            var original=(VoxelGarageUpgradeCategory)categoryField.GetValue(shop);
            var previewField=typeof(VoxelRepairUpgradeSceneController).GetField("upgradePlacement",Instance);
            var preview=(VoxelGarageUpgradePlacement)previewField.GetValue(shop);
            var selections=preview?.Selections.ToArray();int wallet=VoxelCurrencyState.Balance;
            var originalTuning = shop.cameraTuning;
            var modeTuning = Object.Instantiate(originalTuning != null ? originalTuning : VoxelRepairUpgradeTuning.Load());
            try
            {
                shop.cameraTuning = modeTuning;
                modeTuning.showUpgradeSectionTabs = false;
                typeof(VoxelRepairUpgradeSceneController).GetMethod("LayoutGarageDock",Instance).Invoke(shop,null);
                var allVisible = cards.Where(c=>c.gameObject.activeSelf).OrderBy(c=>((RectTransform)c.transform).anchoredPosition.x).ToArray();
                Check(allVisible.Length == cards.Count(c => !c.FullyPurchased), "Continuous row must show every remaining upgrade");
                Check(shop.GetComponentsInChildren<Button>(true).Where(b=>b.name.StartsWith("Category ")).All(b=>!b.gameObject.activeSelf), "Continuous row still shows section tabs");
                CheckCardOrder(cards);
                var allScroll=(ScrollRect)typeof(VoxelRepairUpgradeSceneController).GetField("upgradeCategoryScroll",Instance).GetValue(shop);
                Check(Mathf.Abs(allScroll.content.rect.width-Mathf.Max(0,allVisible.Length*224-14))<.1f, "Continuous row cannot scroll through the remaining upgrades");
                modeTuning.showUpgradeSectionTabs = true;
                typeof(VoxelRepairUpgradeSceneController).GetMethod("LayoutGarageDock",Instance).Invoke(shop,null);
                for(int i=0;i<expected.Length;i++)
                {
                    var category=(VoxelGarageUpgradeCategory)i;
                    var tab=shop.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Category "+category);
                    Check(tab.GetComponentInChildren<Text>().text==labels[i],"Category tab label incorrect");
                    tab.onClick.Invoke();
                    var visible=cards.Where(c=>c.gameObject.activeSelf).ToArray();
                    var hidden = cards.Where(c => c.FullyPurchased).Select(c => c.name);
                    Check(visible.Select(c=>c.name).OrderBy(n=>n).SequenceEqual(expected[i].Except(hidden).OrderBy(n=>n)),"Incorrect parts in category "+category);
                    Check(visible.All(c=>c.Category==category),"Category metadata mismatch");
                    var scroll=(ScrollRect)typeof(VoxelRepairUpgradeSceneController).GetField("upgradeCategoryScroll",Instance).GetValue(shop);
                    Check(Mathf.Abs(scroll.content.rect.width-Mathf.Max(0,visible.Length*224-14))<.1f && scroll.content.anchoredPosition==Vector2.zero,
                        "Category leaves blank card gaps or stale scroll position");
                    CheckCardOrder(cards);
                    Check(VoxelCurrencyState.Balance==wallet && previewField.GetValue(shop)==preview &&
                        (preview==null || preview.Selections.SequenceEqual(selections)),"Switching categories changed the pending cart or spent cash");
                }
            }
            finally
            {
                shop.cameraTuning = originalTuning;
                Object.DestroyImmediate(modeTuning);
                typeof(VoxelRepairUpgradeSceneController).GetMethod("ShowUpgradeCategory",Instance).Invoke(shop,new object[]{original});
                typeof(VoxelRepairUpgradeSceneController).GetMethod("LayoutGarageDock",Instance).Invoke(shop,null);
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
            Check(cards.Length==15,"Some upgrade types are missing or side cards were not combined");
            CheckRarityBorders(shop.transform);
            CheckCategories(shop);
            ResetPurchases();VoxelCurrencyState.Reset();Refresh(shop);
            Check(cards.All(c=>c.State==(c.Level == 1 ? VoxelGarageUpgradeState.Unaffordable : VoxelGarageUpgradeState.Locked) &&
                c.GetComponent<Button>().interactable),"Locked and unaffordable cards must still be inspectable");
            var dock = shop.transform.Find("Repair Upgrade UI/Car Upgrade Panel");
            RevealUpgradeCard(cards[0].GetComponent<Button>());
            var details = dock.Find("Upgrade Details");
            Check(!details.gameObject.activeSelf, "Opening upgrades selected a part by default");
            foreach (var card in cards)
            {
                RevealUpgradeCard(card.GetComponent<Button>()).onClick.Invoke();
                string description = details.Find("Upgrade Effect").GetComponent<Text>().text;
                Check(!string.IsNullOrEmpty(details.Find("Detail Title").GetComponent<Text>().text) &&
                    !string.IsNullOrEmpty(description) &&
                    details.GetComponentsInChildren<Text>().All(t => !t.text.Contains("$")),
                    "Part information must contain a name and description without pricing");
                var stats = details.Find("Part Stats/Upgrade Stats").GetComponent<Text>();
                if (card.State == VoxelGarageUpgradeState.Locked)
                {
                    string prerequisite = card.UpgradeKind switch
                    {
                        VoxelGarageUpgradeKind.ExtendedArmour => "both level 1 Door Armour panels",
                        VoxelGarageUpgradeKind.Minigun => "both Thump Cannons",
                        VoxelGarageUpgradeKind.GoldWheels => "Sports Wheels",
                        VoxelGarageUpgradeKind.StarSpikes => "Wheel Spikes",
                        VoxelGarageUpgradeKind.DualBoost => "the level 1 Boost Bottle",
                        VoxelGarageUpgradeKind.Plough => "the Bull Bar",
                        _ => throw new InvalidOperationException("Missing prerequisite for " + card.UpgradeKind)
                    };
                    Check(description == $"Requires purchase of {prerequisite}." && !stats.transform.parent.gameObject.activeSelf,
                        "Locked details must show only the prerequisite for " + card.UpgradeKind);
                }
                else
                {
                    Check(stats.gameObject.activeInHierarchy && stats.text.Any(char.IsDigit),
                        "Missing tuned stats for " + card.UpgradeKind);
                }
                if (card.UpgradeKind == VoxelGarageUpgradeKind.BullBar)
                {
                    var tuning = VoxelGarageUpgradePlacement.PloughTuning(card.UpgradeKind);
                    Check(stats.text == $"+{tuning.impactDamageBonusPercent:0.#}% FRONT RAM DAMAGE\n-{tuning.playerDamageReductionPercent:0.#}% FRONT DAMAGE TAKEN",
                        "Bull bar's displayed effects must use its authored tuning.");
                }
                Check(typeof(VoxelRepairUpgradeSceneController).GetField("upgradePlacement", Instance).GetValue(shop) == null,
                    "An unaffordable upgrade started placement");
            }
            Check(VoxelCurrencyState.Balance==0 && VoxelGunUpgradeState.PurchasedLongGunCount==0 && !VoxelEngineUpgradeState.IsPurchased &&
                !VoxelMissileUpgradeState.IsPurchased(false) && !VoxelMissileUpgradeState.IsPurchased(true),"Unaffordable callbacks spent or equipped upgrades");
            VoxelCurrencyState.Add(100000);Refresh(shop);
            Check(cards.All(c => c.State == (c.Level == 1 ? VoxelGarageUpgradeState.Affordable : VoxelGarageUpgradeState.Locked)),
                "A fresh run must offer all level 1 parts and lock the higher levels even with enough cash");
            int LitSegments(VoxelGarageUpgradeCard card) => card.GetComponentsInChildren<Image>(true)
                .Count(segment => segment.name.StartsWith("Level Segment", StringComparison.Ordinal) && segment.color == VoxelUpgradeRarity.ForLevel(card.Level));
            Check(LitSegments(cards.Single(c => c.UpgradeKind == VoxelGarageUpgradeKind.Wheels)) == 1 &&
                LitSegments(cards.Single(c => c.UpgradeKind == VoxelGarageUpgradeKind.GoldWheels)) == 2 &&
                LitSegments(cards.Single(c => c.UpgradeKind == VoxelGarageUpgradeKind.Boost)) == 1 &&
                LitSegments(cards.Single(c => c.UpgradeKind == VoxelGarageUpgradeKind.DualBoost)) == 2,
                "Level markers must come from the part's tier before it is fitted");
            var sportsCard = cards.Single(c => c.UpgradeKind == VoxelGarageUpgradeKind.Wheels);
            var goldCard = cards.Single(c => c.UpgradeKind == VoxelGarageUpgradeKind.GoldWheels);
            var starCard = cards.Single(c => c.UpgradeKind == VoxelGarageUpgradeKind.StarSpikes);
            VoxelCurrencyState.Reset();
            VoxelCurrencyState.Add(sportsCard.Cost);
            Refresh(shop);
            RevealUpgradeCard(sportsCard.GetComponent<Button>()).onClick.Invoke();
            Check(goldCard.State == VoxelGarageUpgradeState.Locked, "Previewing the prerequisite unlocked the next level");
            shop.GetComponentsInChildren<Button>(true).Single(b => b.name == "Confirm Upgrade Purchase").onClick.Invoke();
            Check(goldCard.State == VoxelGarageUpgradeState.Unaffordable && starCard.State == VoxelGarageUpgradeState.Locked &&
                goldCard.transform.GetSiblingIndex() < starCard.transform.GetSiblingIndex(),
                "Confirming the prerequisite must move the newly unlocked card ahead of locked parts immediately");
            CheckCardOrder(cards);
            CheckCategories(shop);
            VoxelCurrencyState.Add(100000);
            Refresh(shop);
            RevealUpgradeCard(starCard.GetComponent<Button>()).onClick.Invoke();
            Check(VoxelWheelSpikeUpgradeState.TryPurchase(VoxelWheelSpikeTuning.Load()), "Could not set up affordable level 2 check");
            VoxelCurrencyState.Reset();
            VoxelCurrencyState.Add(VoxelWheelSpikeTuning.LoadStar().purchasePrice);
            Refresh(shop);
            Check(cards.Any(c => c.Level == 1 && c.State == VoxelGarageUpgradeState.Unaffordable) &&
                starCard.State == VoxelGarageUpgradeState.Affordable,
                "Affordable level 2 cards must sort before unaffordable level 1 cards");
            Check(!details.Find("Upgrade Effect").GetComponent<Text>().text.StartsWith("Requires purchase of", StringComparison.Ordinal) &&
                details.Find("Part Stats").gameObject.activeInHierarchy, "Unlocking the inspected part did not restore its description and stats");
            CheckCategories(shop);
            foreach(var type in States)foreach(var f in type.GetFields(Static))
                if(!f.IsLiteral && !f.IsInitOnly && f.FieldType==typeof(bool))f.SetValue(null,true);
            typeof(VoxelGunUpgradeState).GetField("installed",Static).SetValue(null,
                new[] { VoxelGunUpgradeState.MinigunTuning, VoxelGunUpgradeState.MinigunTuning });
            typeof(VoxelWheelSpikeUpgradeState).GetField("installedTuning",Static).SetValue(null,VoxelWheelSpikeTuning.LoadStar());
            typeof(VoxelPerformanceWheelUpgradeState).GetField("<InstalledTuning>k__BackingField",Static).SetValue(null,VoxelPerformanceWheelTuning.LoadGold());
            typeof(VoxelPloughUpgradeState).GetField("<InstalledTuning>k__BackingField",Static).SetValue(null,VoxelPloughTuning.Load());
            typeof(VoxelBoostUpgradeState).GetField("installedTuning",Static).SetValue(null,VoxelBoostUpgradeTuning.LoadDualUpgrade());
            typeof(VoxelArmorUpgradeState).GetField("rightTuning",Static).SetValue(null,VoxelArmorTuning.LoadExtended());
            typeof(VoxelArmorUpgradeState).GetField("leftTuning",Static).SetValue(null,VoxelArmorTuning.LoadExtended());
            VoxelCurrencyState.Reset();Refresh(shop);
            Check(cards.All(c => c.FullyPurchased && !c.gameObject.activeSelf), "Fully fitted and superseded upgrades must disappear");
            CheckCategories(shop);
            ResetPurchases();typeof(VoxelGunUpgradeState).GetField("installed",Static).SetValue(null,
                new[] { VoxelGunUpgradeState.LongGunTuning, null });
            VoxelCurrencyState.Add(100000);Refresh(shop);
            Check(shop.GetComponentsInChildren<VoxelGarageUpgradeCard>(true).First(c=>c.name=="Long Gun Purchase Button").State==VoxelGarageUpgradeState.Affordable,
                "One equipped gun must leave the next gun slot purchasable");
            var incompatible=ScriptableObject.CreateInstance<VoxelCarDefinition>();
            try
            {
                typeof(VoxelRepairUpgradeSceneController).GetField("definition",Instance).SetValue(shop,incompatible);Refresh(shop);
                Check(cards.Where(c=>c.name.Contains("Armor") || c.name.Contains("Missile") || c.name.Contains("Engine") || c.name.Contains("Performance") || c.UpgradeKind == VoxelGarageUpgradeKind.GoldWheels)
                    .All(c=>c.State==VoxelGarageUpgradeState.Incompatible),"Compatibility gates were lost");
            }
            finally{typeof(VoxelRepairUpgradeSceneController).GetField("definition",Instance).SetValue(shop,definition);Object.DestroyImmediate(incompatible);}
            CheckMissionPreview(shop);
            ResetPurchases();VoxelCurrencyState.Reset();VoxelCurrencyState.Add(100000);Refresh(shop);
            var damaged=car.GetComponentsInChildren<MeshRenderer>().First(r=>r.GetComponentInParent<VoxelIndestructiblePart>()==null);
            damaged.gameObject.SetActive(false);int missing=car.RepairableIntegrityVoxels;
            int balance=VoxelCurrencyState.Balance;
            RevealUpgradeCard(cards.First(c=>c.name=="V6 Engine Purchase Button").GetComponent<Button>()).onClick.Invoke();
            Check(VoxelCurrencyState.Balance==balance && !VoxelEngineUpgradeState.IsPurchased,"Preview purchased the engine before confirmation");
            typeof(VoxelRepairUpgradeSceneController).GetMethod("ConfirmUpgradePurchase",Instance).Invoke(shop,null);
            RevealUpgradeCard(cards.First(c=>c.name=="Left Missile Purchase Button").GetComponent<Button>()).onClick.Invoke();
            typeof(VoxelRepairUpgradeSceneController).GetMethod("SelectUpgradePlacement",Instance).Invoke(shop,new object[]{0});
            typeof(VoxelRepairUpgradeSceneController).GetMethod("ConfirmUpgradePurchase",Instance).Invoke(shop,null);
            Check(VoxelEngineUpgradeState.IsPurchased && VoxelMissileUpgradeState.IsPurchased(false) && !VoxelMissileUpgradeState.IsPurchased(true),"Actual callbacks failed to install independent upgrades");
            CheckCardOrder(cards);
            Check(VoxelCurrencyState.Balance==balance-VoxelEngineUpgradeTuning.Load().purchasePrice-VoxelMissileLauncherTuning.Load().weapon.purchasePrice,"Purchase prices charged incorrectly");
            Check(!damaged.gameObject.activeSelf && car.RepairableIntegrityVoxels==missing,"Purchase repaired existing damage");
            damaged.gameObject.SetActive(true);car.ResetIntegrityBaseline();
            VoxelCurrencyState.Reset();VoxelCurrencyState.Add(200);Refresh(shop);
        }

        private static void CheckRarityBorders(Transform parent)
        {
            var root = new GameObject("Temporary rarity cards");
            root.transform.SetParent(parent, false);
            var colours = new Color[] { new Color32(80, 208, 72, 255), new Color32(0, 160, 224, 255),
                new Color32(184, 72, 224, 255), new Color32(248, 232, 0, 255) };
            try
            {
                var button = VoxelMenuUi.CreateButton(root.transform, "Rarity card", "TEST PART", 24,
                    new Vector2(.5f, .5f), Vector2.zero, new Vector2(210, 234), () => { });
                var card = button.gameObject.AddComponent<VoxelGarageUpgradeCard>();
                card.Build(button, "TEST PART", VoxelGarageIconKind.Guns, VoxelHudStyles.HudFont, VoxelHudStyles.HudFont);
                var frame = card.GetComponentInChildren<VoxelGaragePanel>();
                for (int level = 1; level <= 4; level++)
                {
                    typeof(VoxelGarageUpgradeCard).GetMethod("SetLevel", Instance).Invoke(card, new object[] { level });
                    foreach (VoxelGarageUpgradeState state in Enum.GetValues(typeof(VoxelGarageUpgradeState)))
                    {
                        card.Refresh(10, state != VoxelGarageUpgradeState.Incompatible,
                            state == VoxelGarageUpgradeState.Equipped ? 2 : 0, 2,
                            state == VoxelGarageUpgradeState.Pending ? 2 : 0,
                            state == VoxelGarageUpgradeState.Unaffordable ? 0 : 100, true,
                            state == VoxelGarageUpgradeState.Locked ? ("LOCKED", "prerequisite") : default);
                        Check(card.State == state && frame.edgeColor == colours[level - 1],
                            "Rarity border was replaced by the purchase state.");
                        foreach (bool selected in new[] { true, false })
                        {
                            typeof(VoxelGarageUpgradeCard).GetMethod("SetSelected", Instance).Invoke(card, new object[] { selected });
                            Check(frame.useRarityFrame && frame.edgeColor == colours[level - 1] && frame.borderWidth == (selected ? 8f : 6f),
                                "Selection lost rarity colour or its thickness feedback.");
                            Check(frame.selectionGlow == selected, "Rarity border glow did not follow selection.");
                        }
                    }
                    Check(card.GetComponentsInChildren<Image>(true).Count(segment =>
                        segment.name.StartsWith("Level Segment", StringComparison.Ordinal) && segment.color == colours[level - 1]) == level,
                        "Rarity level markers did not match their card border.");
                }
            }
            finally { Object.DestroyImmediate(root); }
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
                    Check(shop.NextRaceButton.GetComponentInChildren<Text>().text=="NEXT","Next button label changed when campaign refreshed");
                    CheckLevelPreview(shop, sequence.tracks[expected]);
                    Check(VoxelTrackProgressState.AdvanceToNextTrack()==sequence.tracks[expected],"Mission preview and next race disagree");
                }
                sequence.tracks=Array.Empty<VoxelTrackDefinition>();indexField.SetValue(null,0);
                Check(VoxelTrackProgressState.NextTrack==null && VoxelTrackProgressState.NextTrackIndex==0,"Empty sequence preview failed");
                Refresh(shop);
                Check(shop.NextRaceButton.GetComponentInChildren<Text>().text=="NEXT","Empty sequence changed the Next button label");
                CheckLevelPreview(shop, null);
            }
            finally{sequenceField.SetValue(null,oldSequence);indexField.SetValue(null,oldIndex);Object.DestroyImmediate(sequence);Refresh(shop);}
        }
        private static void CheckLevelPreview(VoxelRepairUpgradeSceneController shop, VoxelTrackDefinition expected)
        {
            int index = VoxelTrackProgressState.CurrentTrackIndex, cash = VoxelCurrencyState.Balance;
            int missing = VoxelCarRunState.MissingVoxelCount;
            var canvas = shop.transform.Find("Repair Upgrade UI").gameObject;
            var hud = shop.transform.Find("Player Integrity HUD")?.gameObject;
            bool hudActive = hud != null && hud.activeSelf;
            typeof(VoxelRepairUpgradeSceneController).GetMethod("ShowGaragePanel", Instance).Invoke(shop, new object[] { null });
            var previewButton = shop.GetComponentsInChildren<Button>().Single(button => button.name == "Level Preview Button");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                previewButton.onClick.Invoke();
                var view = VoxelMissionBriefing.Active;
                Check(view != null && view.Track == expected && !canvas.activeSelf && (hud == null || !hud.activeSelf),
                    "Garage preview showed the wrong mission or left garage UI/integrity visible.");
                Check(view.GetComponentsInChildren<Button>().Length == 1 && view.GetComponentsInChildren<Button>()[0].name == "Back",
                    "Garage preview exposed Continue.");
                view.Accept(); shop.StartNextRace();
                Check(VoxelMissionBriefing.IsShowing && VoxelTrackProgressState.CurrentTrackIndex == index,
                    "Garage preview allowed the next mission to start.");
                if (repeat == 0) view.GoBack(); else view.Close();
                Check(!VoxelMissionBriefing.IsShowing && canvas.activeSelf && (hud == null || hud.activeSelf == hudActive),
                    "Closing garage preview failed to restore garage UI/integrity.");
            }
            Check(VoxelCurrencyState.Balance == cash && VoxelCarRunState.MissingVoxelCount == missing &&
                VoxelTrackProgressState.CurrentTrackIndex == index, "Garage preview changed wallet, damage or campaign progress.");
        }
        public static void CheckLayout(VoxelRepairUpgradeSceneController shop,Camera camera,bool upgrades)
        {
            var canvas=shop.transform.Find("Repair Upgrade UI").GetComponent<RectTransform>();
            var homeUpgrade = (RectTransform)canvas.Find("Garage Home/Upgrade Menu Button");
            var homePreview = (RectTransform)canvas.Find("Garage Home/Level Preview Button");
            Bounds upgradeBounds = RectBounds(canvas, homeUpgrade), previewBounds = RectBounds(canvas, homePreview);
            Check(homeUpgrade.sizeDelta == homePreview.sizeDelta && Mathf.Abs(upgradeBounds.center.x - previewBounds.center.x) < .1f &&
                previewBounds.max.y + 30 < upgradeBounds.min.y && previewBounds.min.y >= canvas.rect.yMin + 24,
                "Level Preview must share the upgrade button footprint and fit beneath it.");
            Check(homeUpgrade.GetComponentInChildren<VoxelGaragePanel>().edgeColor != homePreview.GetComponentInChildren<VoxelGaragePanel>().edgeColor,
                "Level Preview needs its own silver/blue accent.");
            var panel=canvas.Find("Car Upgrade Panel").GetComponent<RectTransform>();
            var next=(RectTransform)shop.NextRaceButton.transform;
            var back=panel.Find("Close Panel").GetComponent<RectTransform>();
            Check(back.GetComponentInChildren<Text>().text=="BACK" && back.rect.height>=90 &&
                Mathf.Abs(back.rect.width*back.rect.height/(next.rect.width*next.rect.height)-.3f)<.01f,
                "Upgrade Back must be readable and about 30% of the mission button's footprint");
            Bounds backBounds=RectBounds(canvas,back);
            Bounds headingBounds=RectBounds(canvas,(RectTransform)panel.Find("Upgrade Title"));
            Check(backBounds.max.x+10<headingBounds.min.x,"Back overlaps Vehicle Upgrades heading");
            var repair=(RectTransform)canvas.Find("Repair Panel");
            var dial=shop.transform.Find("Player Integrity HUD/Integrity Dial Backplate") as RectTransform;
            if (dial != null)
            {
                Bounds dialBounds=RectBounds(canvas,dial), repairPanelBounds=RectBounds(canvas,repair);
                Check(repairPanelBounds.max.y <= dialBounds.min.y - 20, "Repair panel overlaps the integrity meter or lacks clearance");
                Check(repairPanelBounds.min.y >= canvas.rect.yMin + 19 && repairPanelBounds.max.x <= canvas.rect.xMax,
                    "Repair panel escapes the screen");
            }
            var repairBack=(RectTransform)repair.Find("Close Panel");
            Check(repairBack.sizeDelta==back.sizeDelta && repairBack.GetComponentInChildren<Text>().text=="BACK",
                "Repair and upgrade menus must share the Back button");
            Bounds repairBounds=RectBounds(canvas,repair), repairBackBounds=RectBounds(canvas,repairBack);
            Bounds fullRepair=RectBounds(canvas,(RectTransform)repair.Find("Full Repair Button"));
            Check(repairBackBounds.max.y+16<=fullRepair.min.y && repairBackBounds.min.y>repairBounds.min.y,
                "Repair Back must sit below the repair options inside the panel");
            Bounds cash=RectBounds(canvas,(RectTransform)canvas.Find("Cash Frame"));
            Bounds settings=RectBounds(canvas,(RectTransform)canvas.Find("Settings Button"));
            Check(settings.max.x+16<=cash.min.x && Mathf.Abs(settings.center.y-cash.center.y)<.1f,
                "Settings cog must sit immediately left of cash");
            Check(cash.max.x<=canvas.rect.xMax && cash.max.y<=canvas.rect.yMax,"Cash tab escapes the screen");
            Bounds a=RectBounds(canvas,panel),b=RectBounds(canvas,next);
            Check(a.max.x+10<b.min.x,"Upgrade strip overlaps next mission button");
            Check(a.min.y>=canvas.rect.yMin && b.min.y>=canvas.rect.yMin && b.max.x<=canvas.rect.xMax+.1f,"Dock escapes screen bounds");
            var scroll=panel.GetComponentInChildren<ScrollRect>(true);
            Check(scroll.horizontal && !scroll.vertical && scroll.content.GetComponentsInChildren<VoxelGarageUpgradeCard>(true).Length==15,
                "Horizontal catalogue missing");
            var tabs=panel.GetComponentsInChildren<Button>(true).Where(t=>t.name.StartsWith("Category ")).ToArray();
            Check(tabs.Length==6 && tabs.All(t=>t.gameObject.activeSelf == shop.UpgradeSectionTabsEnabled),"Garage section-tab visibility mismatch");
            Bounds previousTab=default;
            for(int i=0;i<tabs.Length;i++)
            {
                if (!tabs[i].gameObject.activeSelf) continue;
                Bounds tab=RectBounds(canvas,(RectTransform)tabs[i].transform);
                Bounds viewport=RectBounds(canvas,scroll.viewport);
                Check(tab.min.x>=a.min.x && tab.max.x<=a.max.x && tab.min.y>viewport.max.y+8 && tab.max.y<backBounds.min.y-8,
                    "Category tab escapes its row or overlaps cards/Back");
                if(i>0)Check(tab.min.x>previousTab.max.x+4,"Category tabs overlap");
                previousTab=tab;
            }
            Check(camera.rect==new Rect(0,0,1,1),"Garage must render the floor behind the translucent cards");
            if(upgrades)
            {
                var detail = (RectTransform)panel.Find("Upgrade Details");
                Bounds details = RectBounds(canvas, detail);
                Check(detail.gameObject.activeInHierarchy && details.size.x > 0f && details.size.x <= 434f &&
                    details.min.x >= a.min.x && details.min.y >= a.max.y + 15f, "Upgrade details escape the space above two cards");
                if (dial != null) Check(details.max.y + 20f <= RectBounds(canvas, dial).min.y, "Upgrade details overlap integrity meter");
                var rim = shop.GetComponentInChildren<VoxelCarTurntable>()?.transform.Find("Stationary outer rim");
                if (rim != null)
                {
                    float rimLeft = rim.GetComponent<MeshFilter>().sharedMesh.vertices
                        .Min(vertex => camera.WorldToViewportPoint(rim.TransformPoint(vertex)).x);
                    Check(details.max.x <= canvas.rect.xMin + rimLeft * canvas.rect.width - 15f, "Upgrade details overlap turntable");
                }
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
