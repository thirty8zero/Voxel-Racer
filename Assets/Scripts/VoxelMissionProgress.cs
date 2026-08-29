using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Tracks mission score and presents the top-centre progress HUD.</summary>
    public sealed class VoxelMissionProgress : MonoBehaviour
    {
        public static VoxelMissionProgress Active { get; private set; }

        public VoxelMissionTuning Tuning { get; private set; }
        public int Points { get; private set; }
        public float Percent => Tuning == null ? 0f : Mathf.Clamp01((float)Points / Tuning.requiredPoints);
        public bool IsComplete { get; private set; }
        public float RemainingTime { get; private set; }
        public bool TimeBonusAvailable => RemainingTime > 0f;
        public int BaseCurrencyEarned { get; private set; }
        public int TimeBonusCurrencyEarned { get; private set; }
        public int TotalCurrencyEarned { get; private set; }

        private VoxelStartCountdown startCountdown;
        private bool rewardAwarded;

        public void Configure(VoxelMissionTuning tuning)
        {
            Tuning = tuning;
            Points = 0;
            IsComplete = false;
            RemainingTime = tuning != null ? tuning.timeLimitSeconds : 0f;
            rewardAwarded = false;
            BaseCurrencyEarned = 0;
            TimeBonusCurrencyEarned = 0;
            TotalCurrencyEarned = 0;
        }

        public void SetStartCountdown(VoxelStartCountdown countdown) => startCountdown = countdown;

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        public static void ReportEnemyVoxelDamage(int count = 1)
        {
            if (Active?.Tuning != null)
                Active.AddPoints(Active.Tuning.enemyVoxelDamagePoints * count);
        }

        public static void ReportEnemyVehicleDestroyed()
        {
            if (Active?.Tuning != null)
                Active.AddPoints(Active.Tuning.enemyVehicleDestroyedPoints);
        }

        public static void ReportCivilianVoxelDamage(int count = 1)
        {
            if (Active?.Tuning != null)
                Active.AddPoints(Active.Tuning.civilianVoxelDamagePoints * count);
        }

        public static void ReportCivilianVehicleDestroyed()
        {
            if (Active?.Tuning != null)
                Active.AddPoints(Active.Tuning.civilianVehicleDestroyedPoints);
        }

        private void AddPoints(int points)
        {
            if (Tuning == null || points == 0)
                return;

            Points = Mathf.Max(0, Points + points);
            if (Points >= Tuning.requiredPoints && !IsComplete)
                CompleteMission();
        }

        private void Update()
        {
            if (!Application.isPlaying || Tuning == null || IsComplete || RemainingTime <= 0f ||
                (startCountdown != null && !startCountdown.IsComplete))
                return;

            RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
        }

        private void CompleteMission()
        {
            IsComplete = true;
            if (rewardAwarded || Tuning == null)
                return;

            BaseCurrencyEarned = Tuning.completionCurrencyAward;
            TotalCurrencyEarned = BaseCurrencyEarned;
            if (TimeBonusAvailable)
            {
                TotalCurrencyEarned = Mathf.RoundToInt(BaseCurrencyEarned * Tuning.timeBonusCurrencyMultiplier);
                TimeBonusCurrencyEarned = Mathf.Max(0, TotalCurrencyEarned - BaseCurrencyEarned);
            }
            VoxelCurrencyState.Add(TotalCurrencyEarned);
            rewardAwarded = true;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || Tuning == null || VoxelPlayerDeathScreen.IsShowing)
                return;

            float hudAlpha = VoxelStartCountdown.CurrentGameplayHudAlpha;
            if (hudAlpha <= 0f)
                return;

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, hudAlpha);

            const float width = 540f;
            const float height = 70f;
            var area = new Rect((Screen.width - width) * 0.5f, 18f, width, height);
            GUI.Box(area, string.Empty, VoxelHudStyles.Box(30));

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = VoxelHudStyles.HudFont,
                fontSize = 40,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            labelStyle.normal.textColor = IsComplete ? new Color(0.25f, 1f, 0.38f) : Color.white;
            GUI.Label(new Rect(area.x + 8f, area.y, area.width - 16f, 42f),
                IsComplete ? "MISSION COMPLETE" : $"{Tuning.displayName}: {Mathf.RoundToInt(Percent * 100f)}%", labelStyle);

            var barBackground = new Rect(area.x + 24f, area.y + 48f, area.width - 48f, 13f);
            GUI.color = new Color(0.08f, 0.09f, 0.12f, hudAlpha);
            GUI.DrawTexture(barBackground, Texture2D.whiteTexture);
            GUI.color = IsComplete ? new Color(0.25f, 1f, 0.38f, hudAlpha) : new Color(1f, 0.72f, 0.14f, hudAlpha);
            GUI.DrawTexture(new Rect(barBackground.x + 2f, barBackground.y + 2f,
                Mathf.Max(0f, (barBackground.width - 4f) * Percent), barBackground.height - 4f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, hudAlpha);

            GUI.color = previousColor;
        }
    }
}
