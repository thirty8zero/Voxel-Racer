using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Tracks mission score and presents the top-centre progress HUD.</summary>
    public sealed partial class VoxelMissionProgress : MonoBehaviour
    {
        public static VoxelMissionProgress Active { get; private set; }

        public VoxelMissionTuning Tuning { get; private set; }
        public int Points { get; private set; }
        public float Percent => Tuning == null ? 0f : Mathf.Clamp01((float)Points / Tuning.requiredPoints);
        public bool IsComplete { get; private set; }
        public float RemainingTime { get; private set; }
        public bool TimeBonusAvailable => RemainingTime > 0f;
        public Vector2 PercentageScreenPosition { get; private set; }
        public int BaseCurrencyEarned { get; private set; }
        public int TimeBonusCurrencyEarned { get; private set; }
        public int TotalCurrencyEarned { get; private set; }
        public int BonusCashEarned { get; private set; }
        public void AddBonusCash(int amount)
        {
            if (Tuning != null && !IsComplete && amount > 0) BonusCashEarned += amount;
        }
        public float DestructionMultiplierBonus { get; private set; }
        public float EffectiveTimeBonusMultiplier { get; private set; } = 1f;
        private readonly System.Collections.Generic.List<MultiplierFlight> multiplierFlights = new();
        private struct MultiplierFlight { public float amount, startedAt, valueAfter; public string reason; public Vector2 origin; }
        private float displayedMultiplierBonus, multiplierPulseUntil;
        private const float MultiplierFlightDuration = .85f;
        private float pendingVoxelChange, lastVoxelDamageAt;
        private Vector2 pendingVoxelOrigin;
        private int enemyVoxelsTowardsReward;
        private const int EnemyVoxelsPerReward = 10;

        private void FlushVoxelPopup()
        {
            if (Mathf.Abs(pendingVoxelChange) < .0001f || Time.unscaledTime - lastVoxelDamageAt < 1f) return;
            multiplierFlights.Add(new MultiplierFlight { amount = pendingVoxelChange, startedAt = Time.unscaledTime,
                valueAfter = EffectiveTimeBonusMultiplier, reason = "VOXEL DAMAGE", origin = pendingVoxelOrigin });
            pendingVoxelChange = 0;
        }

        public void AddMultiplierBonus(float amount)
        {
            if (amount > 0) ChangeMultiplier(amount, "BOX PRIZE");
        }

        public void ChangeMultiplier(float amount, string reason, Vector3? worldPosition = null)
        {
            if (Tuning == null || IsComplete || !TimeBonusAvailable || amount == 0 ||
                (startCountdown != null && !startCountdown.IsComplete)) return;
            float before = EffectiveTimeBonusMultiplier;
            EffectiveTimeBonusMultiplier = Mathf.Round(Mathf.Clamp(before + amount, 0f,
                Mathf.Max(1f, Tuning.maximumTimeMultiplier)) * 1000f) / 1000f;
            float delta = EffectiveTimeBonusMultiplier - before;
            DestructionMultiplierBonus = EffectiveTimeBonusMultiplier - Tuning.timeBonusCurrencyMultiplier;
            if (Mathf.Abs(delta) < .0001f) return;
            var origin = new Vector2(.5f, .45f);
            if (worldPosition.HasValue && Camera.main != null)
            {
                var screen = Camera.main.WorldToViewportPoint(worldPosition.Value);
                if (screen.z > 0) origin = new Vector2(Mathf.Clamp(screen.x, .15f, .85f), Mathf.Clamp(1f-screen.y, .3f, .8f));
            }
            if (reason == "CIVILIAN DAMAGE")
            {
                pendingVoxelChange += delta;
                lastVoxelDamageAt = Time.unscaledTime;
                pendingVoxelOrigin = origin;
                return;
            }
            int last = multiplierFlights.Count - 1;
            if (last >= 0 && multiplierFlights[last].reason == reason &&
                Time.unscaledTime - multiplierFlights[last].startedAt < .15f)
            {
                var grouped = multiplierFlights[last]; grouped.amount += delta;
                grouped.valueAfter = EffectiveTimeBonusMultiplier; multiplierFlights[last] = grouped;
            }
            else multiplierFlights.Add(new MultiplierFlight { amount = delta, reason = reason,
                startedAt = Time.unscaledTime, valueAfter = EffectiveTimeBonusMultiplier, origin = origin });
        }

        public static void ReportEnemyVoxelDestroyed(int count, Vector3 position)
        {
            var mission = Active;
            if (count <= 0 || mission?.Tuning == null || mission.IsComplete || !mission.TimeBonusAvailable ||
                (mission.startCountdown != null && !mission.startCountdown.IsComplete)) return;
            // Carry partial groups across hits and enemies, but never across missions.
            long total = (long)mission.enemyVoxelsTowardsReward + count;
            int rewards = (int)(total / EnemyVoxelsPerReward);
            mission.enemyVoxelsTowardsReward = (int)(total % EnemyVoxelsPerReward);
            if (rewards > 0)
                mission.ChangeMultiplier(mission.Tuning.enemyVoxelMultiplier * rewards, "ENEMY VOXELS", position);
        }
        public static void ReportCivilianVoxelDestroyed(int count, Vector3 position)
        {
            if (count > 0 && Active?.Tuning != null)
                Active.ChangeMultiplier(-Active.Tuning.civilianVoxelMultiplierPenalty * count, "CIVILIAN DAMAGE", position);
        }

        private VoxelStartCountdown startCountdown;
        private bool rewardAwarded;
        private Font percentageSymbolFont;

        public void Configure(VoxelMissionTuning tuning)
        {
            Tuning = tuning;
            Points = 0;
            BonusCashEarned = 0;
            DestructionMultiplierBonus = 0;
            EffectiveTimeBonusMultiplier = tuning != null ? Mathf.Clamp(tuning.timeBonusCurrencyMultiplier, 0, Mathf.Max(1, tuning.maximumTimeMultiplier)) : 1;
            displayedMultiplierBonus = EffectiveTimeBonusMultiplier;
            multiplierPulseUntil = 0;
            multiplierFlights.Clear();
            enemyVoxelsTowardsReward = 0;
            pendingVoxelChange = 0;
            PercentageScreenPosition = new Vector2(Screen.width * .5f, 39f);
            IsComplete = false;
            timeExtensionStartedAt = float.NegativeInfinity;
            timeExtensionAmount = 0;
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
            if (count > 0 && Active != null) Active.lastVoxelDamageAt = Time.unscaledTime;
            if (Active?.Tuning != null)
                Active.AddPoints(Active.Tuning.enemyVoxelDamagePoints * count);
        }

        /// <summary>Returns the score currently awarded by one successful enemy weapon hit.</summary>
        public static int GetEnemyVoxelDamagePoints(int count = 1) =>
            Active?.Tuning != null ? Active.Tuning.enemyVoxelDamagePoints * count : 0;

        /// <summary>Award ram score directly from the enemy's configured ram-damage amount, rather than its visual voxel debris count.</summary>
        public static void ReportEnemyRamDamage(float damage)
        {
            if (Active?.Tuning != null && damage > 0f)
                Active.AddPoints(Mathf.RoundToInt(damage));
        }

        /// <summary>Ram score is intentionally tied to the enemy's configured player-ram damage.</summary>
        public static int GetEnemyRamDamagePoints(float damage) =>
            Active?.Tuning != null && damage > 0f ? Mathf.RoundToInt(damage) : 0;

        public static void ReportEnemyVehicleDestroyed(Vector3? position = null)
        {
            if (Active?.Tuning != null)
            {
                Active.ChangeMultiplier(Active.Tuning.enemyDestroyedMultiplier, "ENEMY DESTROYED", position);
                Active.AddPoints(Active.Tuning.enemyVehicleDestroyedPoints);
            }
        }

        public static int GetEnemyVehicleDestroyedPoints() =>
            Active?.Tuning != null ? Active.Tuning.enemyVehicleDestroyedPoints : 0;

        public static void ReportFuelDrumDestroyed(int drumCount = 1, Vector3? position = null)
        {
            if (Active?.Tuning != null)
            {
                Active.ChangeMultiplier(Active.Tuning.barrelMultiplier * Mathf.Max(0, drumCount), "BARRELS DESTROYED", position);
                Active.AddPoints(Active.Tuning.fuelDrumDestroyedPoints);
            }
        }

        public static int GetFuelDrumDestroyedPoints() =>
            Active?.Tuning != null ? Active.Tuning.fuelDrumDestroyedPoints : 0;

        public static void ReportCivilianNearMiss(int points, Vector3? position = null)
        {
            if (Active?.Tuning != null && points > 0)
            {
                Active.ChangeMultiplier(Active.Tuning.nearMissMultiplier, "CLOSE CALL", position);
                Active.AddPoints(points);
            }
        }

        public static void ReportCivilianVoxelDamage(int count = 1)
        {
            if (count > 0 && Active != null) Active.lastVoxelDamageAt = Time.unscaledTime;
            if (Active?.Tuning != null)
                Active.AddPoints(Active.Tuning.civilianVoxelDamagePoints * count);
        }

        public static void ReportCivilianVehicleDestroyed(Vector3? position = null)
        {
            if (Active?.Tuning != null)
            {
                Active.ChangeMultiplier(-Active.Tuning.civilianDestroyedMultiplierPenalty, "CIVILIAN DESTROYED", position);
                Active.AddPoints(Active.Tuning.civilianVehicleDestroyedPoints);
            }
        }

        private void AddPoints(int points)
        {
            if (Tuning == null || IsComplete || points == 0)
                return;

            Points = Mathf.Max(0, Points + points);
            if (Points >= Tuning.requiredPoints && !IsComplete)
                CompleteMission();
        }

        private void Update()
        {
            FlushVoxelPopup();
            for (int i = 0; i < multiplierFlights.Count;)
            {
                var flight = multiplierFlights[i];
                if (Time.unscaledTime - flight.startedAt < MultiplierFlightDuration) break;
                displayedMultiplierBonus = EffectiveTimeBonusMultiplier;
                lastMultiplierWasNegative = flight.amount < 0;
                multiplierPulseUntil = Time.unscaledTime + .32f;
                multiplierFlights.RemoveAt(i);
            }
            if (!Application.isPlaying || Tuning == null || IsComplete || RemainingTime <= 0f ||
                (startCountdown != null && !startCountdown.IsComplete))
                return;

            AdvanceBonusClock(Time.deltaTime);
        }

        public void AdvanceBonusClock(float seconds)
        {
            if (Tuning == null || IsComplete || !TimeBonusAvailable || seconds <= 0 ||
                (startCountdown != null && !startCountdown.IsComplete)) return;
            RemainingTime = Mathf.Max(0, RemainingTime - seconds);
            if (!TimeBonusAvailable)
            {
                EffectiveTimeBonusMultiplier = 0; displayedMultiplierBonus = 0;
                pendingVoxelChange = 0;
                multiplierFlights.Clear(); multiplierPulseUntil = Time.unscaledTime + .6f;
                lastMultiplierWasNegative = true;
            }
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
                TotalCurrencyEarned = Mathf.Max(BaseCurrencyEarned, Mathf.RoundToInt(BaseCurrencyEarned * EffectiveTimeBonusMultiplier));
                TimeBonusCurrencyEarned = Mathf.Max(0, TotalCurrencyEarned - BaseCurrencyEarned);
            }
            TotalCurrencyEarned += BonusCashEarned;
            VoxelCurrencyState.Add(TotalCurrencyEarned);
            rewardAwarded = true;
            displayedMultiplierBonus = EffectiveTimeBonusMultiplier;
            multiplierFlights.Clear();
            pendingVoxelChange = 0;
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
            var labelRect = new Rect(area.x + 8f, area.y, area.width - 16f, 42f);
            if (IsComplete)
                GUI.Label(labelRect, "MISSION COMPLETE", labelStyle);
            else
            {
                // IMPACTED lacks a visible percent glyph. Keep the heading font and
                // draw the entire numeric percentage in one font, matching the integrity HUD.
                percentageSymbolFont ??= Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
                var symbolStyle = new GUIStyle(labelStyle) { font = percentageSymbolFont };
                string heading = $"{Tuning.displayName}: ";
                string percentage = $"{Mathf.RoundToInt(Percent * 100f)}%";
                float headingWidth = labelStyle.CalcSize(new GUIContent(heading)).x;
                float symbolWidth = symbolStyle.CalcSize(new GUIContent(percentage)).x;
                float left = labelRect.center.x - (headingWidth + symbolWidth) * 0.5f;
                PercentageScreenPosition = new Vector2(left + headingWidth + symbolWidth * .5f, labelRect.center.y);
                GUI.Label(new Rect(left, labelRect.y, headingWidth, labelRect.height), heading, labelStyle);
                GUI.Label(new Rect(left + headingWidth, labelRect.y, symbolWidth, labelRect.height), percentage, symbolStyle);
            }

            var barBackground = new Rect(area.x + 24f, area.y + 48f, area.width - 48f, 13f);
            GUI.color = new Color(0.08f, 0.09f, 0.12f, hudAlpha);
            GUI.DrawTexture(barBackground, Texture2D.whiteTexture);
            GUI.color = IsComplete ? new Color(0.25f, 1f, 0.38f, hudAlpha) : new Color(1f, 0.72f, 0.14f, hudAlpha);
            GUI.DrawTexture(new Rect(barBackground.x + 2f, barBackground.y + 2f,
                Mathf.Max(0f, (barBackground.width - 4f) * Percent), barBackground.height - 4f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, hudAlpha);

            DrawMultiplier(area, hudAlpha);
            GUI.color = previousColor;
        }

    }
}
