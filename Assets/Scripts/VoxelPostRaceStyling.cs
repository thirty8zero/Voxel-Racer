using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    public sealed partial class VoxelPostRaceContinue
    {
        private static readonly Color ResultSilver = new(.88f, .93f, .98f);

        private void BuildContinueButton()
        {
            if (ContinueButton != null) return;
            var canvas = VoxelMenuUi.CreateCanvas(transform, "Post Race UI");
            MatchPostRaceCanvasHeight(canvas);
            ContinueButton = VoxelMenuUi.CreateButton(canvas, "Continue Button", "CONTINUE", 64,
                Vector2.zero, new Vector2(470, 78), new Vector2(700, 104), OpenWorkshop);
            var frame = StyleResultSurface(ContinueButton.image, new Color(.80f, .90f, .98f),
                new Color(.12f, .38f, .62f), new Color(.70f, .86f, .96f));
            ContinueButton.targetGraphic = frame;
            var colours = ContinueButton.colors;
            colours.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colours.selectedColor = Color.white; colours.pressedColor = new Color(.70f, .77f, .84f);
            ContinueButton.colors = colours;
            var label = ContinueButton.GetComponentInChildren<Text>();
            label.font = Resources.Load<Font>("Fonts/Square") ?? VoxelHudStyles.HudFont;
            label.color = new Color(.025f, .075f, .13f);
            label.rectTransform.anchoredPosition = new Vector2(-40, 0);
            label.rectTransform.sizeDelta = new Vector2(530, 88);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 30; label.resizeTextMaxSize = 64;
            var arrow = new GameObject("Continue Chevrons", typeof(RectTransform), typeof(VoxelGarageUpgradeIcon));
            arrow.transform.SetParent(ContinueButton.transform, false);
            var rect = (RectTransform)arrow.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(277, 0); rect.sizeDelta = new Vector2(54, 62);
            var graphic = arrow.GetComponent<VoxelGarageUpgradeIcon>();
            graphic.kind = VoxelGarageIconKind.Next; graphic.color = label.color; graphic.raycastTarget = false;
        }

        private static VoxelGaragePanel StyleResultSurface(Image image, Color edge, Color fill, Color top)
        {
            image.color = fill; image.enabled = false;
            var child = new GameObject("Blue Silver Bevel", typeof(RectTransform), typeof(VoxelGaragePanel));
            child.transform.SetParent(image.transform, false); child.transform.SetAsFirstSibling();
            var rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var frame = child.GetComponent<VoxelGaragePanel>();
            frame.edgeColor = edge; frame.color = fill; frame.topColor = top; frame.useGradient = true;
            frame.raycastTarget = image.raycastTarget;
            return frame;
        }

        private static void StyleResultPanel(Image panel, bool breakdown)
        {
            StyleResultSurface(panel, new Color(.44f, .59f, .73f, .90f),
                new Color(.015f, .035f, .065f, .90f), new Color(.045f, .095f, .15f, .90f));
            ResultDivider(panel.transform, "Header Divider", breakdown ? 171 : 135, breakdown);
            if (!breakdown) ResultDivider(panel.transform, "Total Divider", -94, false);
        }

        private static void ResultDivider(Transform parent, string name, float y, bool breakdown)
        {
            var line = VoxelMenuUi.CreatePanel(parent, name, new Vector2(.5f, .5f),
                new Vector2(breakdown ? 0 : -35, y), new Vector2(breakdown ? 820 : 740, 2));
            line.color = new Color(.44f, .68f, .84f, .28f); line.raycastTarget = false;
        }

        private static void StyleResultNavigation(Button button)
        {
            button.targetGraphic = StyleResultSurface(button.image, new Color(.48f, .64f, .76f),
                new Color(.055f, .12f, .19f, .96f), new Color(.10f, .22f, .32f, .96f));
            button.GetComponentInChildren<Text>().color = ResultSilver;
        }
    }
}
