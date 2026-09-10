using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Reveals the workshop transition only after a finished car has stopped.</summary>
    public sealed class VoxelPostRaceContinue : MonoBehaviour
    {
        public VoxelRunFinish runFinish;
        public VoxelMissionProgress missionProgress;
        public string repairSceneName = "RepairUpgrade";
        public Button ContinueButton { get; private set; }

        private bool isLoading;
        private Text baseRewardText;
        private Text timeBonusText;
        private Text totalRewardText;
        private GameObject bonusCashPanel;
        private float rewardSequenceStartedAt = -1f;

        public void Configure(VoxelRunFinish finish, VoxelMissionProgress mission)
        {
            runFinish = finish;
            missionProgress = mission;
        }

        private void Update()
        {
            if (runFinish == null || !runFinish.HasFinished ||
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

            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Post Race UI");
            ContinueButton = VoxelMenuUi.CreateButton(canvas, "Continue Button", "CONTINUE", 78,
                new Vector2(0f, 0f), new Vector2(470f, 78f), new Vector2(700f, 104f), OpenWorkshop);
        }

        private void BuildRewardSequence()
        {
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Mission Reward UI");
            canvas.GetComponent<Canvas>().sortingOrder = 101;
            Image panel = VoxelMenuUi.CreatePanel(canvas, "Mission Reward Panel", new Vector2(0f, 0.5f),
                new Vector2(470f, 0f), new Vector2(900f, 520f));
            panel.color = new Color(0.02f, 0.025f, 0.04f, 0.72f);
            baseRewardText = VoxelMenuUi.CreateText(panel.transform, "Base Mission Reward", string.Empty, 84,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 185f), new Vector2(860f, 90f));
            timeBonusText = VoxelMenuUi.CreateText(panel.transform, "Time Bonus Reward", string.Empty, 84,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 85f), new Vector2(860f, 90f));
            var cashPanel = VoxelMenuUi.CreatePanel(panel.transform, "Bonus Cash Panel", new Vector2(.5f, .5f),
                new Vector2(0, -15f), new Vector2(860f, 90f));
            cashPanel.color = new Color(.12f, .20f, .10f, .9f);
            bonusCashPanel = cashPanel.gameObject;
            var cashText = VoxelMenuUi.CreateText(cashPanel.transform, "Bonus Cash Reward",
                "BONUS CASH  <color=#FFD12A>+" + (missionProgress != null ? missionProgress.BonusCashEarned : 0) + "</color>",
                72, TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(840f, 85f));
            bonusCashPanel.SetActive(false);
            totalRewardText = VoxelMenuUi.CreateText(panel.transform, "Total Mission Reward", string.Empty, 126,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(860f, 150f));

            baseRewardText.color = Color.white;
            timeBonusText.color = Color.white;
            totalRewardText.color = Color.white;
            int baseAward = missionProgress != null ? missionProgress.BaseCurrencyEarned : 0;
            int totalAward = missionProgress != null ? missionProgress.TotalCurrencyEarned : baseAward;
            float timeBonusMultiplier = missionProgress != null && missionProgress.Tuning != null
                ? missionProgress.EffectiveTimeBonusMultiplier
                : 1f;
            baseRewardText.text = "MISSION REWARD  <color=#FFD12A>+" + baseAward + "</color>";
            timeBonusText.font = Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
            timeBonusText.fontSize = 40;
            timeBonusText.text = missionProgress != null && !missionProgress.TimeBonusAvailable
                ? "<color=#FF4936>TIME UP - BONUS LOST</color>"
                : "TIME BONUS " + timeBonusMultiplier.ToString("0.00") + "x  <color=#28A745>+$" +
                    (missionProgress != null ? missionProgress.TimeBonusCurrencyEarned : 0) + "</color>";
            totalRewardText.text = "TOTAL EARNED  <color=#FFD12A>+" + totalAward + "</color>";
            baseRewardText.gameObject.SetActive(false);
            timeBonusText.gameObject.SetActive(false);
            totalRewardText.gameObject.SetActive(false);
            rewardSequenceStartedAt = Time.unscaledTime;
        }

        private void UpdateRewardSequence()
        {
            float elapsed = Time.unscaledTime - rewardSequenceStartedAt;
            if (baseRewardText != null && !baseRewardText.gameObject.activeSelf)
                baseRewardText.gameObject.SetActive(true);

            bool hasTimeBonus = missionProgress != null;
            const float bonusRevealTime = 1f;
            float cashRevealTime = hasTimeBonus ? 2f : 1f;
            float totalRevealTime = cashRevealTime + 1f;
            if (elapsed >= cashRevealTime && bonusCashPanel != null) bonusCashPanel.SetActive(true);
            if (hasTimeBonus && elapsed >= bonusRevealTime && timeBonusText != null)
                timeBonusText.gameObject.SetActive(true);
            if (elapsed >= totalRevealTime && totalRewardText != null)
                totalRewardText.gameObject.SetActive(true);
        }

        public void OpenWorkshop()
        {
            if (isLoading || runFinish == null || runFinish.target == null)
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
            SceneManager.LoadScene(buildIndex);
        }
    }
}
