using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        private VoxelGarageUpgradePlacement upgradePlacement;
        private GameObject purchaseModal;
        private Text purchaseDescription;
        private Button purchaseConfirmButton;
        private int quotedUpgradePrice;
        private readonly List<PendingUpgrade> pendingUpgrades = new();
        private ScrollRect purchaseList;
        private Text purchaseScrollHint;
        private int placementWallet;
        private bool purchaseListLayoutDirty;
        private Vector2 purchaseLayoutSize;
        private float purchaseLayoutMaxHeight;
        private readonly struct PendingUpgrade
        {
            public readonly VoxelGarageUpgradeKind Kind;
            public readonly int Slot, Price;
            public readonly string Title;
            public PendingUpgrade(VoxelGarageUpgradeKind kind, int slot, int price, string title)
            { Kind = kind; Slot = slot; Price = price; Title = title; }
        }
        private int PendingCount(VoxelGarageUpgradeKind kind) => upgradePlacement?.PendingCount(kind) ?? 0;
        private int AvailableUpgradeCash => Mathf.Max(0, VoxelCurrencyState.Balance - quotedUpgradePrice);
        private bool placementPointerHeld, placementPointerTouch;
        private Vector2 placementPointerStart;
        private int placementPointerId;
        private bool placementPointerDragged;

        private bool UpgradeOffer(VoxelGarageUpgradeKind kind, out int price, out string title)
        {
            price = 0; title = string.Empty;
            switch (kind)
            {
                case VoxelGarageUpgradeKind.Armour:
                    var armour = VoxelArmorTuning.Load();
                    if (armour == null) return false;
                    price = armour.panelPurchasePrice; title = "DOOR ARMOUR";
                    return armour.panelPrefab != null && armour.Fits(definition) && !VoxelArmorUpgradeState.IsFullyPurchased;
                case VoxelGarageUpgradeKind.Guns:
                    var gun = VoxelGunUpgradeState.LongGunTuning;
                    if (gun == null) return false;
                    price = gun.purchasePrice; title = gun.displayName.ToUpperInvariant();
                    return VoxelGunUpgradeState.CanPurchase(gun);
                case VoxelGarageUpgradeKind.Missiles:
                    var missile = VoxelMissileLauncherTuning.Load();
                    if (missile == null || missile.weapon == null) return false;
                    price = missile.weapon.purchasePrice; title = "MISSILE LAUNCHER";
                    return missile.Fits(definition) && (!VoxelMissileUpgradeState.IsPurchased(false) || !VoxelMissileUpgradeState.IsPurchased(true));
                case VoxelGarageUpgradeKind.Wheels:
                    var wheels = VoxelPerformanceWheelTuning.Load();
                    if (wheels == null) return false;
                    price = wheels.purchasePrice; title = "PERFORMANCE TIRES";
                    return wheels.Fits(definition) && !VoxelPerformanceWheelUpgradeState.IsPurchased;
                case VoxelGarageUpgradeKind.Spikes:
                    var spikes = VoxelWheelSpikeTuning.Load();
                    if (spikes == null) return false;
                    price = spikes.purchasePrice; title = spikes.displayName.ToUpperInvariant();
                    return VoxelWheelSpikeUpgradeState.CanPurchase(spikes);
                case VoxelGarageUpgradeKind.Boost:
                    var boost = VoxelBoostUpgradeTuning.LoadUpgrade();
                    if (boost == null) return false;
                    price = boost.purchasePrice; title = "BOOST BOTTLE";
                    return boost.Fits(definition) && !VoxelBoostUpgradeState.IsPurchased;
                case VoxelGarageUpgradeKind.Engine:
                    var engine = VoxelEngineUpgradeTuning.Load();
                    if (engine == null) return false;
                    price = engine.purchasePrice; title = engine.displayName.ToUpperInvariant();
                    return engine.Fits(definition) && !VoxelEngineUpgradeState.IsPurchased;
                case VoxelGarageUpgradeKind.Plough:
                    var plough = VoxelPloughTuning.Load();
                    if (plough == null) return false;
                    price = plough.purchasePrice; title = plough.displayName.ToUpperInvariant();
                    return VoxelPloughUpgradeState.CanPurchase(plough, definition);
            }
            return false;
        }

        private void BeginUpgradePlacement(VoxelGarageUpgradeKind kind)
        {
            if (isLoading || DisplayedCar == null || MissionStartWarningVisible) return;
            if (PendingCount(kind) > 0) { RemovePendingUpgrade(kind, -1); return; }
            if (!UpgradeOffer(kind, out int price, out string title) || AvailableUpgradeCash < price) return;
            int capacity = kind == VoxelGarageUpgradeKind.Guns ? Mathf.Clamp(VoxelGunUpgradeState.LongGunTuning.maximumPurchases, 1, 2) :
                kind == VoxelGarageUpgradeKind.Armour || kind == VoxelGarageUpgradeKind.Missiles ? 2 : 1;
            int owned = kind == VoxelGarageUpgradeKind.Guns ? VoxelGunUpgradeState.PurchasedLongGunCount :
                kind == VoxelGarageUpgradeKind.Armour ? (VoxelArmorUpgradeState.IsLeftPurchased ? 1 : 0) + (VoxelArmorUpgradeState.IsRightPurchased ? 1 : 0) :
                kind == VoxelGarageUpgradeKind.Missiles ? (VoxelMissileUpgradeState.IsPurchased(false) ? 1 : 0) + (VoxelMissileUpgradeState.IsPurchased(true) ? 1 : 0) : 0;
            if (PendingCount(kind) + owned >= capacity) return;
            try
            {
                if (upgradePlacement == null) upgradePlacement = new VoxelGarageUpgradePlacement(DisplayedCar.gameObject, kind);
                else upgradePlacement.ShowKind(kind);
                if (!upgradePlacement.RequiresPlacement) AddPendingUpgrade(kind, 0, price, title);
                RefreshPlacementPresentation();
            }
            catch (Exception e)
            {
                CancelUpgradePlacement();
                feedbackText.text = "UPGRADE PREVIEW UNAVAILABLE";
                Debug.LogException(e);
            }
        }

        private void SelectUpgradePlacement(int slot)
        {
            if (MissionStartWarningVisible || upgradePlacement == null || !upgradePlacement.RequiresPlacement ||
                !UpgradeOffer(upgradePlacement.Kind, out int price, out string title) || AvailableUpgradeCash < price ||
                upgradePlacement.IsPending(upgradePlacement.Kind, slot)) return;
            upgradePlacement.Select(slot);
            AddPendingUpgrade(upgradePlacement.Kind, slot, price, title);
            RefreshPlacementPresentation();
        }

        private void AddPendingUpgrade(VoxelGarageUpgradeKind kind, int slot, int price, string title)
        {
            price = Mathf.Max(0, price);
            pendingUpgrades.Add(new PendingUpgrade(kind, slot, price, title));
            quotedUpgradePrice += price;
        }

        private void RemovePendingUpgrade(VoxelGarageUpgradeKind kind, int slot)
        {
            if (upgradePlacement == null || PendingCount(kind) == 0) return;
            for (int i = pendingUpgrades.Count - 1; i >= 0; i--)
            {
                var part = pendingUpgrades[i];
                if (part.Kind != kind || slot >= 0 && part.Slot != slot) continue;
                quotedUpgradePrice -= part.Price;
                pendingUpgrades.RemoveAt(i);
            }
            if (pendingUpgrades.Count == 0) { CancelUpgradePlacement(); return; }
            upgradePlacement.Remove(kind, slot);
            RefreshPlacementPresentation();
        }

        private void RefreshPlacementPresentation()
        {
            placementWallet = VoxelCurrencyState.Balance;
            bool canAdd = UpgradeOffer(upgradePlacement.Kind, out int price, out _) && AvailableUpgradeCash >= price;
            upgradePlacement.SetCanAdd(canAdd);
            if (pendingUpgrades.Count > 0) ShowPurchaseConfirmation();
            feedbackText.text = upgradePlacement.AvailableSpotCount > 0
                ? "TAP A GREEN PART TO PLACE IT  ·  DRAG TO TURN"
                : "PREVIEW ONLY  ·  ADD PARTS OR CONFIRM";
            RefreshUpgradeCards();
        }

        private void BuildPurchaseConfirmation()
        {
            var blocker = VoxelMenuUi.CreatePanel(garageCanvas, "Upgrade Purchase Confirmation",
                new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            blocker.color = Color.clear;
            blocker.raycastTarget = false;
            var rect = blocker.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            purchaseModal = blocker.gameObject;
            var panel = VoxelMenuUi.CreatePanel(blocker.transform, "Purchase Dialog",
                new Vector2(1, 1), Vector2.zero, new Vector2(325, 460));
            Frame(panel.gameObject, new Color(.38f, .94f, .55f), new Color(.025f, .045f, .06f, .98f));
            Caption(panel.transform, "Purchase Heading", "PARTS TO FIT", 24,
                new Vector2(.5f, 1), new Vector2(0, -32), new Vector2(285, 32));
            var viewport = new GameObject("Pending Parts Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
            viewport.transform.SetParent(panel.transform, false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(20, 200); viewportRect.offsetMax = new Vector2(-20, -64);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, .01f);
            purchaseDescription = Caption(viewport.transform, "Purchase Description", "", 20,
                new Vector2(.5f, 1), Vector2.zero, new Vector2(285, 148));
            purchaseDescription.color = Color.white;
            purchaseDescription.alignment = TextAnchor.UpperLeft;
            purchaseDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            purchaseDescription.verticalOverflow = VerticalWrapMode.Overflow;
            var descriptionRect = purchaseDescription.rectTransform;
            descriptionRect.anchorMin = new Vector2(0, 1); descriptionRect.anchorMax = Vector2.one;
            descriptionRect.pivot = new Vector2(.5f, 1); descriptionRect.sizeDelta = new Vector2(-16, 148);
            descriptionRect.anchoredPosition = new Vector2(-8, 0);
            purchaseList = viewport.GetComponent<ScrollRect>(); purchaseList.viewport = viewportRect; purchaseList.content = descriptionRect;
            purchaseList.horizontal = false; purchaseList.vertical = true;
            purchaseList.movementType = ScrollRect.MovementType.Clamped; purchaseList.scrollSensitivity = 30;
            var track = new GameObject("Pending Parts Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            track.transform.SetParent(viewport.transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(1, 0); trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(-8, 0); trackRect.offsetMax = Vector2.zero;
            track.GetComponent<Image>().color = new Color(.14f, .21f, .20f, .8f);
            var handle = new GameObject("Scroll Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(track.transform, false);
            var handleRect = (RectTransform)handle.transform;
            handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
            handle.GetComponent<Image>().color = new Color(.48f, .72f, .57f);
            var scrollbar = track.GetComponent<Scrollbar>(); scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle.GetComponent<Image>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            purchaseList.verticalScrollbar = scrollbar; purchaseList.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            purchaseScrollHint = Caption(panel.transform, "Pending Parts Scroll Hint", "SWIPE / SCROLL LIST", 16,
                new Vector2(.5f, 0), new Vector2(0, 190), new Vector2(285, 18));
            purchaseConfirmButton = VoxelMenuUi.CreateButton(panel.transform, "Confirm Upgrade Purchase", "", 24,
                new Vector2(.5f, 0), new Vector2(0, 140), new Vector2(285, 78), ConfirmUpgradePurchase);
            Frame(purchaseConfirmButton.gameObject, new Color(.32f, .92f, .49f), new Color(.045f, .22f, .105f));
            var cancel = VoxelMenuUi.CreateButton(panel.transform, "Cancel Upgrade Purchase", "CANCEL", 26,
                new Vector2(.5f, 0), new Vector2(0, 44), new Vector2(285, 64), CancelUpgradePlacement);
            Frame(cancel.gameObject, new Color(.48f, .55f, .63f), new Color(.07f, .09f, .12f));
            foreach (var label in panel.GetComponentsInChildren<Text>()) label.font = GarageMono;
            purchaseModal.SetActive(false);
        }

        private void ShowPurchaseConfirmation()
        {
            if (purchaseModal == null) BuildPurchaseConfirmation();
            var text = new StringBuilder();
            var listed = new HashSet<VoxelGarageUpgradeKind>();
            foreach (var part in pendingUpgrades)
            {
                if (!listed.Add(part.Kind)) continue;
                bool left = false, right = false;
                foreach (var selected in pendingUpgrades) if (selected.Kind == part.Kind)
                { if (selected.Slot == 0) left = true; else right = true; }
                if (text.Length > 0) text.Append("\n\n");
                text.Append(part.Title);
                if (part.Kind == VoxelGarageUpgradeKind.Armour || part.Kind == VoxelGarageUpgradeKind.Guns || part.Kind == VoxelGarageUpgradeKind.Missiles)
                    text.Append("\n").Append(left && right ? "LEFT + RIGHT  X2" : left ? "LEFT" : "RIGHT");
            }
            purchaseDescription.text = text.ToString();
            purchaseListLayoutDirty = true;
            purchaseConfirmButton.GetComponentInChildren<Text>().text = "CONFIRM\n$ " + quotedUpgradePrice.ToString("N0");
            purchaseConfirmButton.interactable = VoxelCurrencyState.Balance >= quotedUpgradePrice;
            purchaseModal.transform.SetAsLastSibling(); purchaseModal.SetActive(true);
            LayoutPurchaseConfirmation();
            purchaseList.verticalNormalizedPosition = 1;
        }

        private void LayoutPurchaseConfirmation()
        {
            if (purchaseModal == null || !purchaseModal.activeSelf || garageCanvas == null || garageCashFrame == null) return;
            var panel = (RectTransform)purchaseModal.transform.Find("Purchase Dialog");
            // Share the cash tab's left/right edges and sit just below it.
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 1);
            panel.anchoredPosition = garageCashFrame.anchoredPosition +
                new Vector2(garageCashFrame.rect.width * .5f, -garageCashFrame.rect.height * .5f - 20);
            var missionButton = (RectTransform)NextRaceButton.transform;
            float missionTop = garageCanvas.InverseTransformPoint(missionButton.TransformPoint(new Vector3(0, missionButton.rect.yMax, 0))).y;
            float availableHeight = Mathf.Max(1, garageCanvas.rect.yMax + panel.anchoredPosition.y - missionTop - 20);
            panel.localScale = Vector3.one;
            if (purchaseListLayoutDirty || purchaseLayoutSize.x != garageCashFrame.rect.width ||
                !Mathf.Approximately(purchaseLayoutMaxHeight, availableHeight))
            {
                panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, garageCashFrame.rect.width);
                float contentHeight = purchaseDescription.preferredHeight;
                // Use the whole right-hand column before clipping the list. The
                // fixed header/footer reserve 64 + 200 units inside the panel.
                panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(availableHeight, Mathf.Max(460, contentHeight + 264)));
                purchaseListLayoutDirty = false; purchaseLayoutSize = panel.rect.size; purchaseLayoutMaxHeight = availableHeight;
                purchaseDescription.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(purchaseList.viewport.rect.height, contentHeight));
                bool overflow = contentHeight > purchaseList.viewport.rect.height + 1;
                purchaseList.vertical = overflow;
                purchaseList.verticalScrollbar.gameObject.SetActive(overflow);
                purchaseScrollHint.gameObject.SetActive(overflow);
            }
        }

        private void ConfirmUpgradePurchase()
        {
            if (upgradePlacement == null || pendingUpgrades.Count == 0) return;
            bool available = VoxelCurrencyState.Balance >= quotedUpgradePrice;
            foreach (var part in pendingUpgrades)
            {
                available &= UpgradeOffer(part.Kind, out int price, out _) && Mathf.Max(0, price) == part.Price;
                if (part.Kind == VoxelGarageUpgradeKind.Guns) available &= VoxelGunUpgradeState.CanPurchase(VoxelGunUpgradeState.LongGunTuning, part.Slot) &&
                    PendingCount(part.Kind) + VoxelGunUpgradeState.PurchasedLongGunCount <= Mathf.Clamp(VoxelGunUpgradeState.LongGunTuning.maximumPurchases, 1, 2);
                if (part.Kind == VoxelGarageUpgradeKind.Armour) available &= !VoxelArmorUpgradeState.IsPurchasedFor(part.Slot == 0 ? VoxelArmorSide.Left : VoxelArmorSide.Right);
                if (part.Kind == VoxelGarageUpgradeKind.Missiles) available &= !VoxelMissileUpgradeState.IsPurchased(part.Slot == 1);
            }
            var purchases = pendingUpgrades.ToArray();
            CancelUpgradePlacement(); // Restore the original car before using the existing purchase APIs.
            if (!available)
            {
                feedbackText.text = "UPGRADE UNAVAILABLE OR NOT ENOUGH CURRENCY"; RefreshUi(); return;
            }
            foreach (var part in purchases)
            switch (part.Kind)
            {
                case VoxelGarageUpgradeKind.Armour: TryPurchaseDoorArmor(part.Slot == 0 ? VoxelArmorSide.Left : VoxelArmorSide.Right); break;
                case VoxelGarageUpgradeKind.Guns: TryPurchaseLongGun(part.Slot); break;
                case VoxelGarageUpgradeKind.Missiles: PurchaseMissile(part.Slot == 1); break;
                case VoxelGarageUpgradeKind.Wheels: TryPurchasePerformanceWheels(); break;
                case VoxelGarageUpgradeKind.Spikes: TryPurchaseWheelSpikes(); break;
                case VoxelGarageUpgradeKind.Boost: TryPurchaseBoostBottle(); break;
                case VoxelGarageUpgradeKind.Engine: TryPurchaseEngine(); break;
                case VoxelGarageUpgradeKind.Plough: TryPurchasePlough(); break;
            }
            VoxelCarRunState.Capture(DisplayedCar, definition);
            feedbackText.text = purchases.Length + (purchases.Length == 1 ? " UPGRADE INSTALLED" : " UPGRADES INSTALLED");
        }

        private void CancelUpgradePlacement() => DiscardUpgradePlacement(true);

        private void DiscardUpgradePlacement(bool refresh)
        {
            HideMissionStartWarning();
            bool hadPreview = upgradePlacement != null;
            upgradePlacement?.Dispose(); upgradePlacement = null;
            pendingUpgrades.Clear(); quotedUpgradePrice = 0;
            placementPointerHeld = false; rotatingCar = pinching = false;
            if (purchaseModal != null) purchaseModal.SetActive(false);
            if (feedbackText != null) feedbackText.text = string.Empty;
            if (refresh && hadPreview && DisplayedCar != null && garageCanvas != null) RefreshUi();
        }

        private void UpdateUpgradePlacementInput()
        {
            LayoutPurchaseConfirmation();
            if (GarageSettingsVisible)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HideGarageSettings();
                return;
            }
            if (MissionStartWarningVisible)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HideMissionStartWarning();
                return;
            }
            if (upgradePlacement == null || !Application.isPlaying || workshopCamera == null) return;
            if (placementWallet != VoxelCurrencyState.Balance) RefreshPlacementPresentation();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { CancelUpgradePlacement(); return; }
            var touch = Touchscreen.current?.primaryTouch;
            bool touchDown = touch != null && touch.press.wasPressedThisFrame;
            bool mouseDown = (touch == null || !touch.press.isPressed) && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (touchDown || mouseDown)
            {
                Vector2 position = touchDown ? touch.position.ReadValue() : Mouse.current.position.ReadValue();
                if (!BlocksCarRotation(position))
                {
                    placementPointerHeld = true; placementPointerTouch = touchDown; placementPointerStart = position;
                    placementPointerId = touchDown ? touch.touchId.ReadValue() : 0; placementPointerDragged = false;
                }
            }
            if (!placementPointerHeld) return;
            bool held = placementPointerTouch ? touch != null && touch.press.isPressed && touch.touchId.ReadValue() == placementPointerId :
                Mouse.current != null && Mouse.current.leftButton.isPressed;
            Vector2 current = placementPointerTouch && touch != null ? touch.position.ReadValue() : Mouse.current != null ? Mouse.current.position.ReadValue() : placementPointerStart;
            float tapTolerance = Mathf.Max(12, Screen.height * .02f);
            if ((current - placementPointerStart).sqrMagnitude > tapTolerance * tapTolerance || pinching || TryGetTouchPair(out _, out _))
                placementPointerDragged = true;
            if (held) return;
            placementPointerHeld = false;
            if (!placementPointerDragged && !BlocksCarRotation(current) &&
                upgradePlacement.TryPickPart(workshopCamera.ScreenPointToRay(current), out var part, out bool pending))
            {
                if (pending) RemovePendingUpgrade(part.Kind, part.Slot);
                else SelectUpgradePlacement(part.Slot);
            }
        }

        private void OnDisable()
        {
            HideGarageSettings();
            DiscardUpgradePlacement(false);
        }
        private void OnEnable()
        {
            resumeAutoRotationAt = 0;
            if (garageCanvas != null && DisplayedCar != null) RefreshUi();
        }
    }
}
