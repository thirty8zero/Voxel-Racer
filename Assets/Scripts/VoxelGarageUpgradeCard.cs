using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    public enum VoxelGarageUpgradeState { Affordable, Unaffordable, Equipped, Incompatible }

    /// <summary>Presentation only; purchase callbacks and ownership remain with the garage shop.</summary>
    public sealed class VoxelGarageUpgradeCard : MonoBehaviour
    {
        public VoxelGarageUpgradeState State { get; private set; }
        public int Cost { get; private set; }
        public bool FullyPurchased { get; private set; }
        public int CatalogueOrder { get; private set; }
        private Button button;
        private VoxelGaragePanel frame;
        private VoxelGarageUpgradeIcon icon;
        private Text price, status, slots;
        private readonly Image[] segments = new Image[4];
        public void Build(Button purchaseButton, string title, VoxelGarageIconKind kind, Font heading, Font body, int catalogueOrder = 0)
        {
            CatalogueOrder = catalogueOrder;
            button = purchaseButton;
            // Retain the original label for the existing shop refresh/validation paths.
            button.GetComponentInChildren<Text>().enabled = false;
            button.GetComponent<Image>().enabled = false;
            var surface = new GameObject("Card Surface", typeof(RectTransform), typeof(VoxelGaragePanel));
            surface.transform.SetParent(transform, false); surface.transform.SetAsFirstSibling();
            var r = (RectTransform)surface.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            frame = surface.GetComponent<VoxelGaragePanel>(); button.targetGraphic = frame;
            var colors = button.colors; colors.normalColor = colors.selectedColor = colors.disabledColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); colors.pressedColor = new Color(.7f, .7f, .7f);
            button.colors = colors;
            var symbol = new GameObject("Upgrade Icon", typeof(RectTransform), typeof(VoxelGarageUpgradeIcon));
            symbol.transform.SetParent(transform, false);
            var sr = (RectTransform)symbol.transform; sr.sizeDelta = new Vector2(56, 56); sr.anchoredPosition = new Vector2(0, 76);
            icon = symbol.GetComponent<VoxelGarageUpgradeIcon>(); icon.kind = kind; icon.raycastTarget = false;
            var name = Label("Card Title", title, 27, 25, 56, heading); name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 21; name.resizeTextMaxSize = 27;
            slots = Label("Fitted Slots", "", 18, -17, 24, body);
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = VoxelMenuUi.CreatePanel(transform, "Fit Segment " + i, new Vector2(.5f, .5f),
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
        public void Refresh(int cost, bool compatible, int owned, int capacity = 1)
        {
            capacity = Mathf.Max(1, capacity);
            Cost = cost;
            FullyPurchased = owned >= capacity;
            State = !compatible ? VoxelGarageUpgradeState.Incompatible : owned >= capacity ? VoxelGarageUpgradeState.Equipped :
                VoxelCurrencyState.Balance >= cost ? VoxelGarageUpgradeState.Affordable : VoxelGarageUpgradeState.Unaffordable;
            button.interactable = State == VoxelGarageUpgradeState.Affordable;
            Color accent = State == VoxelGarageUpgradeState.Equipped ? new Color(.24f, .85f, .55f) :
                State == VoxelGarageUpgradeState.Affordable ? new Color(.64f, .74f, .87f) : new Color(.31f, .36f, .44f);
            frame.edgeColor = accent;
            frame.color = State == VoxelGarageUpgradeState.Equipped ? new Color(.035f, .10f, .095f, .58f) : new Color(.035f, .045f, .065f, .50f);
            frame.SetVerticesDirty();
            icon.color = compatible ? new Color(.87f, .90f, .98f) : new Color(.39f, .43f, .50f);
            slots.text = compatible ? Mathf.Min(owned, capacity) + " / " + capacity + " FITTED" : "NOT COMPATIBLE";
            slots.color = new Color(.57f, .63f, .72f);
            price.text = State == VoxelGarageUpgradeState.Equipped ? "INSTALLED" : compatible ? "$ " + cost.ToString("N0") : "—";
            price.color = State == VoxelGarageUpgradeState.Unaffordable ? new Color(.86f, .48f, .48f) : Color.white;
            status.text = State == VoxelGarageUpgradeState.Equipped ? "EQUIPPED" : State == VoxelGarageUpgradeState.Affordable ? "BUY +" :
                State == VoxelGarageUpgradeState.Unaffordable ? "NO FUNDS" : "UNAVAILABLE";
            status.color = State == VoxelGarageUpgradeState.Affordable || State == VoxelGarageUpgradeState.Equipped
                ? new Color(.32f, .91f, .52f) : new Color(.58f, .62f, .69f);
            float fraction = compatible ? Mathf.Clamp01((float)owned / capacity) : 0;
            for (int i = 0; i < segments.Length; i++) segments[i].color = i < Mathf.RoundToInt(fraction * segments.Length)
                ? new Color(.24f, .9f, .40f) : new Color(.16f, .20f, .27f);
        }
    }
}
