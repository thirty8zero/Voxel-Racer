using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Reveals the workshop transition only after a finished car has stopped.</summary>
    public sealed partial class VoxelPostRaceContinue : MonoBehaviour
    {
        public VoxelRunFinish runFinish;
        public VoxelMissionProgress missionProgress;
        public string repairSceneName = "RepairUpgrade";
        public Button ContinueButton { get; private set; }

        private bool isLoading;
        private Text baseRewardText;
        private GameObject timeBonusPanel;
        private Text totalRewardText;
        private GameObject bonusCashPanel;
        private float rewardSequenceStartedAt = -1f;
        // Height-based canvases match the integrity dial: its bottom is y=763,
        // while Continue's top is y=130 in the 1080-high reference layout.
        // A 520-high panel centred at 446.5 leaves 56.5 units on each side.
        private static readonly Vector2 PostRacePanelPosition = new Vector2(470f, -93.5f);

        private static void MatchPostRaceCanvasHeight(RectTransform canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.matchWidthOrHeight = 1f;
            scaler.enabled = false;
            scaler.enabled = true;
        }

        public void Configure(VoxelRunFinish finish, VoxelMissionProgress mission)
        {
            runFinish = finish;
            missionProgress = mission;
        }

        private void Update()
        {
            if (runFinish == null || !runFinish.HasFinished ||
                (missionProgress != null && missionProgress.IsCompletionSlowMotionActive) ||
                runFinish.target == null || runFinish.target.CurrentSpeed > 0.05f ||
                !runFinish.FinishCameraComplete)
                return;

            if (rewardSequenceStartedAt < 0f)
            {
                BuildRewardSequence();
                return;
            }

            UpdateRewardSequence();
            if (ContinueButton != null || totalRewardText == null || !totalRewardText.gameObject.activeSelf)
                return;

            BuildContinueButton();
        }

        private void BuildRewardSequence()
        {
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Mission Reward UI");
            MatchPostRaceCanvasHeight(canvas);
            canvas.GetComponent<Canvas>().sortingOrder = 101;
            Image panel = VoxelMenuUi.CreatePanel(canvas, "Mission Reward Panel", new Vector2(0f, 0.5f),
                PostRacePanelPosition, new Vector2(900f, 520f));
            panel.color = new Color(.015f, .035f, .065f, .90f);
            baseRewardText = VoxelMenuUi.CreateText(panel.transform, "Base Mission Reward", string.Empty, 84,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 185f), new Vector2(860f, 90f));
            timeBonusPanel = new GameObject("Time Bonus Reward", typeof(RectTransform));
            var multiplierRect = (RectTransform)timeBonusPanel.transform;
            multiplierRect.SetParent(panel.transform, false);
            var multiplierLayout = timeBonusPanel.AddComponent<VerticalLayoutGroup>();
            multiplierLayout.childAlignment = TextAnchor.MiddleCenter;
            multiplierLayout.childControlWidth = multiplierLayout.childControlHeight = true;
            multiplierLayout.childForceExpandWidth = true;
            multiplierLayout.childForceExpandHeight = false;
            var cashPanel = VoxelMenuUi.CreatePanel(panel.transform, "Bonus Cash Panel", new Vector2(.5f, .5f),
                new Vector2(0, -45f), new Vector2(860f, 80f));
            StyleResultSurface(cashPanel, new Color(.29f, .47f, .59f, .65f),
                new Color(.035f, .09f, .14f, .90f), new Color(.06f, .14f, .20f, .90f));
            bonusCashPanel = cashPanel.gameObject;
            var cashText = VoxelMenuUi.CreateText(cashPanel.transform, "Bonus Cash Reward",
                "BONUS CASH  <color=#A9DFFF>+" + (missionProgress != null ? missionProgress.BonusCashEarned : 0) + "</color>",
                72, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(840f, 75f));
            bonusCashPanel.SetActive(false);
            totalRewardText = VoxelMenuUi.CreateText(panel.transform, "Total Mission Reward", string.Empty, 126,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(860f, 140f));

            baseRewardText.color = ResultSilver;
            totalRewardText.color = ResultSilver;
            int baseAward = missionProgress != null ? missionProgress.BaseCurrencyEarned : 0;
            int totalAward = missionProgress != null ? missionProgress.TotalCurrencyEarned : baseAward;
            float timeBonusMultiplier = missionProgress != null && missionProgress.Tuning != null
                ? missionProgress.EffectiveTimeBonusMultiplier
                : 1f;
            baseRewardText.text = "MISSION REWARD  <color=#A9DFFF>+" + baseAward + "</color>";
            float remainingTimeBonus = missionProgress != null ? missionProgress.RemainingTimeMultiplierBonus : 0f;
            string extraCash = "  <color=#74E5B0>+$" +
                (missionProgress != null ? missionProgress.TimeBonusCurrencyEarned : 0) + "</color>";
            if (remainingTimeBonus > 0f)
            {
                BuildMultiplierRow("MISSION MULTIPLIER", timeBonusMultiplier.ToString("0.00") + "x");
                BuildMultiplierRow("TIME BONUS", "<color=#A9DFFF>+" + remainingTimeBonus.ToString("0.00") + "x</color>");
                BuildMultiplierRow("FINAL MULTIPLIER", missionProgress.FinalRewardMultiplier.ToString("0.00") + "x" + extraCash, true);
            }
            else BuildMultiplierRow("MULTIPLIER", timeBonusMultiplier.ToString("0.00") + "x" + extraCash);
            // Reserve bonus cash's space before its reveal so the rows never jump during the sequence.
            var lowerBoundary = missionProgress != null && missionProgress.BonusCashEarned > 0
                ? (RectTransform)bonusCashPanel.transform : totalRewardText.rectTransform;
            float top = baseRewardText.rectTransform.anchoredPosition.y - baseRewardText.rectTransform.sizeDelta.y * .5f - 10f;
            float bottom = lowerBoundary.anchoredPosition.y + lowerBoundary.sizeDelta.y * .5f + 10f;
            multiplierRect.sizeDelta = new Vector2(860f, top - bottom);
            multiplierRect.anchoredPosition = new Vector2(0f, (top + bottom) * .5f);
            totalRewardText.text = "TOTAL EARNED  <color=#A9DFFF>+" + totalAward + "</color>";
            baseRewardText.gameObject.SetActive(false);
            timeBonusPanel.SetActive(false);
            totalRewardText.gameObject.SetActive(false);
            rewardSequenceStartedAt = Time.unscaledTime;
            BuildBreakdownPage(canvas, panel);
            // Style after the breakdown builder has reserved its navigation gutter.
            StyleResultPanel(panel, false);
        }

        private void BuildMultiplierRow(string label, string value, bool separate = false)
        {
            if (separate)
            {
                var spacer = new GameObject("Final Multiplier Gap", typeof(RectTransform), typeof(LayoutElement));
                spacer.transform.SetParent(timeBonusPanel.transform, false);
                spacer.GetComponent<LayoutElement>().minHeight = spacer.GetComponent<LayoutElement>().preferredHeight = 20f;
            }
            var row = new GameObject(label + " Row", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(timeBonusPanel.transform, false);
            row.GetComponent<LayoutElement>().minHeight = 22f;
            row.GetComponent<LayoutElement>().preferredHeight = 40f;
            var font = Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
            Column(" Label", label, TextAnchor.MiddleLeft, 0f, .6f);
            Column(" Total", value, TextAnchor.MiddleRight, .6f, 1f);

            void Column(string suffix, string text, TextAnchor alignment, float left, float right)
            {
                var column = VoxelMenuUi.CreateText(row.transform, label + suffix, text, 36,
                    alignment, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
                column.font = font;
                column.rectTransform.anchorMin = new Vector2(left, 0f);
                column.rectTransform.anchorMax = new Vector2(right, 1f);
                column.rectTransform.sizeDelta = new Vector2(-8f, 0f);
                column.resizeTextForBestFit = true;
                column.resizeTextMinSize = 22;
                column.resizeTextMaxSize = 36;
                column.horizontalOverflow = HorizontalWrapMode.Wrap;
                column.raycastTarget = false;
            }
        }

        private void UpdateRewardSequence()
        {
            float elapsed = Time.unscaledTime - rewardSequenceStartedAt;
            if (baseRewardText != null && !baseRewardText.gameObject.activeSelf)
                baseRewardText.gameObject.SetActive(true);

            bool hasTimeBonus = missionProgress != null;
            bool hasBonusCash = missionProgress != null && missionProgress.BonusCashEarned > 0;
            const float revealInterval = .5f;
            const float bonusRevealTime = revealInterval;
            float cashRevealTime = (hasTimeBonus ? 2f : 1f) * revealInterval;
            float totalRevealTime = cashRevealTime + (hasBonusCash ? revealInterval : 0f);
            if (bonusCashPanel != null) bonusCashPanel.SetActive(hasBonusCash && elapsed >= cashRevealTime);
            if (hasTimeBonus && elapsed >= bonusRevealTime && timeBonusPanel != null)
                timeBonusPanel.SetActive(true);
            if (elapsed >= totalRevealTime && totalRewardText != null)
                totalRewardText.gameObject.SetActive(true);
        }

        public void OpenWorkshop()
        {
            if (isLoading || VoxelLoadingScreen.IsLoading || runFinish == null || runFinish.target == null)
                return;

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(
                "Assets/Scenes/" + repairSceneName + ".unity");
            if (buildIndex < 0)
            {
                Debug.LogError("Repair scene is not enabled in Build Settings: " + repairSceneName);
                return;
            }

            VoxelCarRunState.Capture(runFinish.target);
            isLoading = true;
            VoxelLoadingScreen.LoadScene(buildIndex);
        }
    }
}
