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
                new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(640f, 124f), OpenWorkshop);
        }

        private void BuildRewardSequence()
        {
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Mission Reward UI");
            canvas.GetComponent<Canvas>().sortingOrder = 101;
            Image panel = VoxelMenuUi.CreatePanel(canvas, "Mission Reward Panel", new Vector2(0f, 0.5f),
                new Vector2(540f, 0f), new Vector2(1040f, 620f));
            panel.color = new Color(0.02f, 0.025f, 0.04f, 0.72f);
            baseRewardText = VoxelMenuUi.CreateText(panel.transform, "Base Mission Reward", string.Empty, 84,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1000f, 130f));
            timeBonusText = VoxelMenuUi.CreateText(panel.transform, "Time Bonus Reward", string.Empty, 84,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1000f, 130f));
            totalRewardText = VoxelMenuUi.CreateText(panel.transform, "Total Mission Reward", string.Empty, 126,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -165f), new Vector2(1000f, 190f));

            baseRewardText.color = Color.white;
            timeBonusText.color = Color.white;
            totalRewardText.color = Color.white;
            int baseAward = missionProgress != null ? missionProgress.BaseCurrencyEarned : 0;
            int totalAward = missionProgress != null ? missionProgress.TotalCurrencyEarned : baseAward;
            float timeBonusMultiplier = missionProgress != null && missionProgress.Tuning != null
                ? missionProgress.Tuning.timeBonusCurrencyMultiplier
                : 1f;
            baseRewardText.text = "MISSION REWARD  <color=#FFD12A>+" + baseAward + "</color>";
            timeBonusText.text = "TIME BONUS  <color=#28A745>" + timeBonusMultiplier.ToString("0.##") + "x</color>";
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

            bool hasTimeBonus = missionProgress != null && missionProgress.TimeBonusCurrencyEarned > 0;
            const float bonusRevealTime = 1f;
            float totalRevealTime = hasTimeBonus ? 2f : 1f;
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
