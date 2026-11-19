using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    public enum VoxelGarageUpgradeState { Affordable, Unaffordable, Equipped, Incompatible, Pending, Locked }
    public enum VoxelGarageUpgradeCategory { Armor, WheelsAndTires, Guns, Missiles, Boost, Engines }

    /// <summary>Presentation only; purchase callbacks and ownership remain with the garage shop.</summary>
    public sealed class VoxelGarageUpgradeCard : MonoBehaviour
    {
        public VoxelGarageUpgradeState State { get; private set; }
        public int Cost { get; private set; }
        public int Level { get; private set; }
        public bool FullyPurchased { get; private set; }
        public int CatalogueOrder { get; private set; }
        public VoxelGarageUpgradeCategory Category { get; private set; }
        public VoxelGarageUpgradeKind UpgradeKind { get; private set; }
        internal string RequiredPurchase { get; private set; }
        internal Color EdgeColor => frame.edgeColor;
        internal Color SurfaceColor => frame.color;
        internal VoxelGarageIconKind IconKind => icon.kind;
        private Button button;
        private VoxelGaragePanel frame;
        private VoxelGarageUpgradeIcon icon;
        private Text titleLabel, price, status, slots;
        private bool selected;
        private readonly Image[] segments = new Image[4];
        public void Build(Button purchaseButton, string title, VoxelGarageIconKind kind, Font heading, Font body, int catalogueOrder = 0,
            VoxelGarageUpgradeCategory category = VoxelGarageUpgradeCategory.Armor,
            VoxelGarageUpgradeKind upgradeKind = VoxelGarageUpgradeKind.Armour)
        {
            CatalogueOrder = catalogueOrder;
            Category = category;
            UpgradeKind = upgradeKind;
            button = purchaseButton;
            // Retain the original label for the existing shop refresh/validation paths.
            button.GetComponentInChildren<Text>().enabled = false;
            button.GetComponent<Image>().enabled = false;
            var surface = new GameObject("Card Surface", typeof(RectTransform), typeof(VoxelGaragePanel));
            surface.transform.SetParent(transform, false); surface.transform.SetAsFirstSibling();
            var r = (RectTransform)surface.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            frame = surface.GetComponent<VoxelGaragePanel>(); frame.useRarityFrame = true; button.targetGraphic = frame;
            var colors = button.colors; colors.normalColor = colors.selectedColor = colors.disabledColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); colors.pressedColor = new Color(.7f, .7f, .7f);
            button.colors = colors;
            var symbol = new GameObject("Upgrade Icon", typeof(RectTransform), typeof(VoxelGarageUpgradeIcon));
            symbol.transform.SetParent(transform, false);
            var sr = (RectTransform)symbol.transform; sr.sizeDelta = new Vector2(56, 56); sr.anchoredPosition = new Vector2(0, 76);
            icon = symbol.GetComponent<VoxelGarageUpgradeIcon>(); icon.kind = kind; icon.raycastTarget = false;
            titleLabel = Label("Card Title", title, 27, 25, 56, heading); titleLabel.resizeTextForBestFit = true;
            titleLabel.resizeTextMinSize = 21; titleLabel.resizeTextMaxSize = 27;
            slots = Label("Fitted Slots", "", 18, -17, 24, body);
            slots.resizeTextForBestFit = true;
            slots.resizeTextMinSize = 12;
            slots.resizeTextMaxSize = 18;
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = VoxelMenuUi.CreatePanel(transform, "Level Segment " + i, new Vector2(.5f, .5f),
                    new Vector2(-48 + i * 32, -39), new Vector2(27, 9));
                segments[i].raycastTarget = false;
            }
            price = Label("Card Price", "", 28, -66, 36, body);
            status = Label("Purchase State", "", 19, -99, 28, body);
        }
        private Text Label(string name, string text, int size, float y, float height, Font font)
        {
            var label = VoxelMenuUi.CreateText(transform, name, text, size, TextAnchor.MiddleCenter,
                new Vector2(.5f, .5f), new Vector2(0, y), new Vector2(190, height));
            label.font = font; return label;
        }
        public void SetTitle(string title)
        {
            if(titleLabel!=null && titleLabel.text!=title)titleLabel.text=title;
        }
        internal void SetLevel(int level)
        {
            Level = Mathf.Max(1, level);
            for (int i = 0; i < segments.Length; i++)
                segments[i].color = i < Level ? VoxelUpgradeRarity.ForLevel(Level) : new Color(.16f, .20f, .27f);
            RefreshSurface();
        }
        internal void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;
            RefreshSurface();
        }

        private void RefreshSurface()
        {
            frame.edgeColor = VoxelUpgradeRarity.ForLevel(Level);
            frame.borderWidth = selected ? 8f : 6f;
            frame.selectionGlow = selected;
            frame.color = State switch
            {
                VoxelGarageUpgradeState.Equipped => new Color(.035f, .10f, .095f, .58f),
                VoxelGarageUpgradeState.Unaffordable or VoxelGarageUpgradeState.Locked => new Color(.018f, .022f, .032f, .75f),
                _ => new Color(.035f, .045f, .065f, .50f)
            };
            if (selected)
            {
                frame.color = Color.Lerp(frame.color, new Color(.10f, .18f, .25f, .78f), .45f);
            }
            frame.SetVerticesDirty();
        }

        public void Refresh(int cost, bool compatible, int owned, int capacity, int pending, int availableCash,
            bool offered, (string CardLabel, string RequiredPurchase) requirement)
        {
            capacity = Mathf.Max(1, capacity);
            Cost = cost;
            FullyPurchased = owned >= capacity;
            State = FullyPurchased ? VoxelGarageUpgradeState.Equipped :
                !compatible ? VoxelGarageUpgradeState.Incompatible :
                pending > 0 && (!offered || owned + pending >= capacity) ? VoxelGarageUpgradeState.Pending :
                requirement.RequiredPurchase != null ? VoxelGarageUpgradeState.Locked :
                !offered ? VoxelGarageUpgradeState.Incompatible :
                availableCash >= cost ? VoxelGarageUpgradeState.Affordable : VoxelGarageUpgradeState.Unaffordable;
            button.interactable = true;
            RequiredPurchase = State == VoxelGarageUpgradeState.Locked ? requirement.RequiredPurchase : null;
            bool muted = State is VoxelGarageUpgradeState.Unaffordable or VoxelGarageUpgradeState.Locked;
            Color mutedText = new Color(.43f, .46f, .50f);
            RefreshSurface();
            Color activeIcon = icon.kind == VoxelGarageIconKind.GoldWheels ? new Color(.94f, .77f, .46f) : new Color(.87f, .90f, .98f);
            icon.color = muted ? new Color(.38f, .41f, .46f) : compatible ? activeIcon : new Color(.39f, .43f, .50f);
            titleLabel.color = muted ? mutedText : Color.white;
            slots.text = State == VoxelGarageUpgradeState.Locked ? requirement.CardLabel :
                !compatible ? "NOT COMPATIBLE" : State == VoxelGarageUpgradeState.Incompatible ? "UNAVAILABLE" :
                Mathf.Min(owned, capacity) + " / " + capacity + " FITTED";
            slots.color = State == VoxelGarageUpgradeState.Unaffordable ? mutedText : new Color(.57f, .63f, .72f);
            price.text = State == VoxelGarageUpgradeState.Pending ? "IN PREVIEW" : State == VoxelGarageUpgradeState.Equipped ? "INSTALLED" : compatible ? "$ " + cost.ToString("N0") : "—";
            price.color = muted ? mutedText : Color.white;
            status.text = pending > 0 ? "PREVIEW X" + pending : State == VoxelGarageUpgradeState.Equipped ? "EQUIPPED" : State == VoxelGarageUpgradeState.Affordable ? "FIT" :
                State == VoxelGarageUpgradeState.Unaffordable ? "NO FUNDS" : State == VoxelGarageUpgradeState.Locked ? "LOCKED" : "UNAVAILABLE";
            status.color = muted ? mutedText : State == VoxelGarageUpgradeState.Affordable || State == VoxelGarageUpgradeState.Equipped
                ? new Color(.32f, .91f, .52f) : new Color(.58f, .62f, .69f);
        }
    }
}
