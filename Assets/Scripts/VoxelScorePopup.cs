using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>
    /// A small pooled, world-space score label.  It is deliberately short lived so
    /// weapon fire can provide feedback without leaving UI objects active on screen.
    /// </summary>
    public sealed class VoxelScorePopup : MonoBehaviour
    {
        public enum Style
        {
            WeaponDamage,
            RamDamage,
            EnemyDestroyed,
            FuelDrumDestroyed,
            NearMiss,
            BonusCash
        }

        private const float QuickLifetime = 0.2f;
        private const float WeaponHorizontalSpread = 1.3f;
        private const float StandardHorizontalSpread = 0.7f;
        private const float WeaponRiseSpeed = 6f;
        private const float WeaponRiseDuration = 0.6f;
        private const float WeaponFadeOutDuration = 0.2f;
        private const float WeaponPopupLifetime = WeaponRiseDuration + WeaponFadeOutDuration;
        // Weapon awards are locked into screen space so their motion remains visibly
        // vertical even while the chase camera and target vehicle are both moving.
        private const float WeaponScreenRisePixelsPerSecond = 180f;
        private const float DestroyedScoreSideDriftSpeed = 5.5f;
        private const float InterruptedTravelDuration = .3f;
        private static readonly Queue<VoxelScorePopup> Pool = new();
        private static Transform poolRoot;
        private static VoxelScorePopup heldHudPopup;

        private float elapsed;
        private float lifetime;
        private float riseSpeed;
        private float riseDuration;
        private float fadeOutDuration;
        private float holdDuration;
        private Vector3 sideDriftDirection;
        private Camera viewCamera;
        private string displayText;
        private Style displayStyle;
        private bool isNegativeScore;
        private bool screenPositionLocked;
        private Vector3 lockedScreenPosition;
        private VoxelEnemyHealthBar sourceHealthBar;
        private bool usesHealthBarOrigin, healthBarOriginReady, hasInitialHealthBarRect;
        private Rect initialHealthBarRect;
        private Rect destructionSlot;
        private Vector2 destructionOffset;
        private bool hasDestructionSlot;
        private const float HealthBarGapPixels = 6f;
        private static GUIStyle weaponStyle;
        private static GUIStyle cashStyle;
        private static GUIStyle ramStyle;
        private static GUIStyle destroyedStyle;
        private static GUIStyle nearMissTitleStyle;
        private static GUIStyle nearMissPointsStyle;
        private static Font penaltyFont;
        private static readonly Dictionary<Style, GUIStyle> PenaltyStyles = new();
        private static readonly Dictionary<int, float> HudTopExtents = new();
        private static readonly List<VoxelScorePopup> ActiveMissionPopups = new();
        private static readonly List<Rect> DestructionObstacles = new();
        private static readonly List<Rect> DrawnPopupRects = new();
        private static int popupLayoutFrame = -1;
        private static float HudScale => Mathf.Max(.1f, Screen.height / 1080f);
        private static float DestructionScale => Mathf.Min(1f, HudScale);
        private static float DestructionOutline => Mathf.Max(1f, 2f * DestructionScale);
        private static int DestructionFontSize => Mathf.RoundToInt(192f * DestructionScale);
        private static bool IsDestruction(Style style) => style == Style.EnemyDestroyed || style == Style.FuelDrumDestroyed;
        private float FlightOutline => IsDestruction(displayStyle) ? DestructionOutline : 2f;
        private bool FliesToMission => displayStyle == Style.WeaponDamage || displayStyle == Style.RamDamage ||
            displayStyle == Style.EnemyDestroyed || displayStyle == Style.FuelDrumDestroyed ||
            displayStyle == Style.NearMiss;
        private bool UsesHudSlot => !usesHealthBarOrigin && FliesToMission && (displayStyle != Style.WeaponDamage || isNegativeScore);

        public static float MissionFlightProgress(float age, float duration, float holdDuration = .12f) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - holdDuration) / Mathf.Max(.01f, duration - holdDuration)));

        public static void Show(Vector3 position, int points, Style style, float lifetimeOverride = -1f,
            VoxelEnemyHealthBar healthBar = null)
        {
            if (!Application.isPlaying || points == 0)
                return;

            // Static pools survive a scene reload, while Unity destroys their scene
            // objects. Discard those stale entries before attempting to reuse one.
            VoxelScorePopup popup = null;
            while (Pool.Count > 0 && popup == null)
                popup = Pool.Dequeue();
            // Do not use ??= here: it checks only a CLR null reference, while a
            // Unity-destroyed Component remains a CLR object until collected.
            if (popup == null)
                popup = Create();
            popup.BeginAtHealthBar(position, points, style, lifetimeOverride, healthBar);
        }

        public static void ShowNearMiss(Vector3 position, int points, float duration) =>
            Show(position, points, Style.NearMiss, duration);

        public static void ShowFuelDrumDestroyed(Vector3 position, int points, float duration) =>
            Show(position, points, Style.FuelDrumDestroyed, duration);

        private static VoxelScorePopup Create()
        {
            if (poolRoot == null)
            {
                var root = new GameObject("Voxel Score Popups");
                poolRoot = root.transform;
            }

            var popupObject = new GameObject("Score Popup");
            popupObject.transform.SetParent(poolRoot, false);
            var popup = popupObject.AddComponent<VoxelScorePopup>();
            popupObject.SetActive(false);
            return popup;
        }

        private void Begin(Vector3 position, int points, Style style, float lifetimeOverride)
            => BeginAtHealthBar(position, points, style, lifetimeOverride, null);

        private void BeginAtHealthBar(Vector3 position, int points, Style style, float lifetimeOverride,
            VoxelEnemyHealthBar healthBar)
        {
            ReleaseHeldSlot();
            ActiveMissionPopups.Remove(this);
            hasDestructionSlot = false;
            destructionOffset = Vector2.zero;
            sourceHealthBar = healthBar;
            usesHealthBarOrigin = healthBar != null;
            healthBarOriginReady = hasInitialHealthBarRect = false;
            elapsed = 0f;
            viewCamera = Camera.main;
            if (sourceHealthBar != null)
                hasInitialHealthBarRect = sourceHealthBar.TryGetScreenRect(viewCamera, out initialHealthBarRect);
            // Scatter on the camera's screen-horizontal axis so simultaneous awards
            // remain readable regardless of the road's current heading.
            Vector3 screenRight = viewCamera != null ? viewCamera.transform.right : Vector3.right;
            float horizontalSpread = style == Style.BonusCash ? 0f : style == Style.WeaponDamage ? WeaponHorizontalSpread : StandardHorizontalSpread;
            transform.position = position + screenRight * Random.Range(-horizontalSpread, horizontalSpread);
            float sideDirection = viewCamera != null && viewCamera.WorldToScreenPoint(transform.position).x < Screen.width * 0.5f
                ? -1f
                : 1f;
            sideDriftDirection = screenRight * sideDirection;
            displayText = points > 0 ? $"+{points}" : points.ToString();
            if (style == Style.BonusCash) displayText = $"+${points}";
            displayStyle = style;
            isNegativeScore = points < 0;
            screenPositionLocked = false;

            if ((FliesToMission || style == Style.BonusCash) && viewCamera != null)
            {
                // Keep the requested random left/right spawn offset, but only use it
                // as an initial placement. The value must never drift sideways after
                // that, regardless of vehicle or camera movement.
                lockedScreenPosition = viewCamera.WorldToScreenPoint(transform.position);
                screenPositionLocked = true;
            }

            switch (style)
            {
                case Style.BonusCash:
                    riseSpeed = 3f;
                    lifetime = 1.2f;
                    riseDuration = 1.2f;
                    fadeOutDuration = .4f;
                    break;
                case Style.RamDamage:
                    riseSpeed = 8.5f;
                    lifetime = 2f;
                    riseDuration = QuickLifetime;
                    fadeOutDuration = 0f;
                    break;
                case Style.EnemyDestroyed:
                    riseSpeed = 10f;
                    lifetime = lifetimeOverride > 0f ? lifetimeOverride : 2f;
                    riseDuration = QuickLifetime;
                    fadeOutDuration = 0f;
                    break;
                case Style.FuelDrumDestroyed:
                    riseSpeed = 9f;
                    lifetime = lifetimeOverride > 0f ? lifetimeOverride : 2f;
                    riseDuration = QuickLifetime;
                    fadeOutDuration = 0f;
                    break;
                case Style.NearMiss:
                    riseSpeed = 9f;
                    lifetime = lifetimeOverride > 0f ? lifetimeOverride : 2f;
                    riseDuration = QuickLifetime;
                    fadeOutDuration = 0f;
                    break;
                default:
                    riseSpeed = WeaponRiseSpeed;
                    lifetime = WeaponPopupLifetime;
                    riseDuration = WeaponRiseDuration;
                    fadeOutDuration = WeaponFadeOutDuration;
                    break;
            }

            gameObject.SetActive(true);
            if (FliesToMission)
            {
                if (Application.isPlaying) ActiveMissionPopups.Add(this);
                var tuning = VoxelMissionProgress.Active?.Tuning;
                holdDuration = Mathf.Max(0f, tuning == null ? .12f : style == Style.WeaponDamage
                    ? tuning.voxelDamagePopupHoldSeconds : tuning.damagePopupHoldSeconds);
                lifetime = holdDuration + Mathf.Max(.01f, lifetime - .12f);
                fadeOutDuration = 0f;
            }
            if (UsesHudSlot)
            {
                if (heldHudPopup != null) heldHudPopup.LaunchEarly();
                heldHudPopup = this;
                float topExtent = GetHudPopupTopExtent(style, isNegativeScore);
                Vector2 origin = VoxelMissionProgress.Active != null
                    ? VoxelMissionProgress.Active.GetNonVoxelDamagePopupOrigin(topExtent)
                    : new Vector2(Screen.width * .5f, 129f * Screen.height / 1080f + topExtent);
                lockedScreenPosition = new Vector3(origin.x, Screen.height - origin.y, 1f);
                screenPositionLocked = true;
            }
        }

        private static Font PenaltyFont => penaltyFont != null ? penaltyFont :
            penaltyFont = Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");

        private static float GetHudPopupTopExtent(Style style, bool negative)
        {
            if (style == Style.NearMiss) return 100f; // Title above its score line.
            bool destruction = IsDestruction(style);
            float outline = destruction ? DestructionOutline : 2f;
            int size = destruction ? DestructionFontSize : style == Style.WeaponDamage ? 60 : 152;
            int key = size * 2 + (negative ? 1 : 0);
            if (HudTopExtents.TryGetValue(key, out float extent)) return extent + outline;
            Font font = negative ? PenaltyFont : VoxelHudStyles.HudFont;
            font.RequestCharactersInTexture("0123456789", size, FontStyle.Normal);
            float height = 0f;
            foreach (char digit in "0123456789")
                if (font.GetCharacterInfo(digit, out CharacterInfo glyph, size, FontStyle.Normal))
                    height = Mathf.Max(height, glyph.glyphHeight);
            extent = (height > 0f ? height : size) * .5f;
            HudTopExtents[key] = extent;
            return extent + outline;
        }

        private void LaunchEarly()
        {
            if (elapsed < holdDuration)
            {
                // Leave the shared reading spot promptly while keeping the award's tally flight.
                float travelDuration = Mathf.Min(InterruptedTravelDuration, lifetime - holdDuration);
                elapsed = holdDuration;
                lifetime = holdDuration + travelDuration;
            }
            ReleaseHeldSlot();
        }

        private void ReleaseHeldSlot() { if (heldHudPopup == this) heldHudPopup = null; }
        private void OnDisable()
        {
            ReleaseHeldSlot();
            ActiveMissionPopups.Remove(this);
            sourceHealthBar = null;
            usesHealthBarOrigin = healthBarOriginReady = hasInitialHealthBarRect = false;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (FliesToMission)
            {
                if (elapsed >= holdDuration) ReleaseHeldSlot();
                // Keep the spawn point fixed while the camera and damaged vehicle move.
                if (!screenPositionLocked && Camera.main != null)
                {
                    lockedScreenPosition = Camera.main.WorldToScreenPoint(transform.position);
                    screenPositionLocked = true;
                }
                if (elapsed >= lifetime)
                {
                    gameObject.SetActive(false);
                    Pool.Enqueue(this);
                }
                return;
            }
            if ((displayStyle == Style.WeaponDamage || displayStyle == Style.BonusCash) && screenPositionLocked && elapsed <= riseDuration)
                lockedScreenPosition.y += (displayStyle == Style.BonusCash ? 90f : WeaponScreenRisePixelsPerSecond) * Time.deltaTime;
            else if (elapsed <= riseDuration)
                transform.position += Vector3.up * (riseSpeed * Time.deltaTime);
            else if (displayStyle == Style.FuelDrumDestroyed || displayStyle == Style.EnemyDestroyed)
                transform.position += sideDriftDirection * (DestroyedScoreSideDriftSpeed * Time.deltaTime);

            // Freeze the final screen position after the short rise. This makes all
            // score awards readable for their complete lifetime even after the player
            // drives past, or their source vehicle/obstacle is destroyed.
            bool driftsTowardScreenEdge = displayStyle == Style.FuelDrumDestroyed ||
                displayStyle == Style.EnemyDestroyed;
            if (!screenPositionLocked && !driftsTowardScreenEdge && elapsed >= riseDuration && viewCamera != null)
            {
                lockedScreenPosition = viewCamera.WorldToScreenPoint(transform.position);
                screenPositionLocked = true;
            }

            if (elapsed >= lifetime)
            {
                gameObject.SetActive(false);
                Pool.Enqueue(this);
            }
        }

        private void OnGUI()
        {
            if (VoxelLoadingScreen.IsLoading || VoxelBossIntroduction.IsShowing) return;
            // Absolute-position labels only need to run on repaint.
            if (Event.current.type != EventType.Repaint)
                return;

            if (viewCamera == null)
                viewCamera = Camera.main;
            if ((!UsesHudSlot && viewCamera == null) || string.IsNullOrEmpty(displayText))
                return;

            Vector3 screenPosition = screenPositionLocked
                ? lockedScreenPosition
                : viewCamera.WorldToScreenPoint(transform.position);
            if (screenPosition.z <= 0f)
                return;

            if (FliesToMission)
            {
                float progress = MissionFlightProgress(elapsed, lifetime, holdDuration);
                float scale = 1f - progress;
                if (scale <= .0001f) return;
                GUIStyle numberStyle = GetStyle(displayStyle, isNegativeScore);
                Vector2 numberSize = numberStyle.CalcSize(new GUIContent(displayText));
                if (IsDestruction(displayStyle) && !hasDestructionSlot)
                    PlaceDestructionLabel(screenPosition, numberSize);
                Vector2 center = GetMissionFlightCenter(screenPosition, numberSize.y, progress);
                var matrix = GUI.matrix;
                try
                {
                    GUIUtility.ScaleAroundPivot(Vector2.one * scale, center);
                    if (displayStyle == Style.NearMiss)
                    {
                        DrawOutlinedLabel(new Rect(center.x - 130f, center.y - 112f, 260f, 54f), "NEAR MISS!",
                            nearMissTitleStyle ??= CreateStyle(38, Color.white));
                        DrawOutlinedLabel(new Rect(center.x - 260f, center.y - 58f, 520f, 156f), displayText,
                            nearMissPointsStyle ??= CreateStyle(152, new Color(1f, .86f, .08f)));
                    }
                    else if (usesHealthBarOrigin || IsDestruction(displayStyle))
                        DrawOutlinedLabel(new Rect(center - numberSize * .5f, numberSize), displayText, numberStyle, FlightOutline);
                    else
                        DrawOutlinedLabel(new Rect(center.x - 280f, center.y - 120f, 560f, 240f), displayText, numberStyle);
                }
                finally { GUI.matrix = matrix; }
                return;
            }

            GUIStyle style = GetStyle(displayStyle, isNegativeScore);
            float width = displayStyle == Style.WeaponDamage ? 150f : 240f;
            float height = displayStyle == Style.WeaponDamage ? 64f : 120f;
            bool driftsOffscreen = displayStyle == Style.FuelDrumDestroyed ||
                displayStyle == Style.EnemyDestroyed;
            float centerX = driftsOffscreen ? screenPosition.x :
                Mathf.Clamp(screenPosition.x, width * 0.5f, Screen.width - width * 0.5f);
            float centerY = Mathf.Clamp(Screen.height - screenPosition.y,
                height * 0.5f, Screen.height - height * 0.5f);
            Rect drawRect = new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height);
            if (displayStyle == Style.WeaponDamage || driftsOffscreen)
            {
                // Gun hits are frequent and intentionally form a loose stream of
                // small numbers, rather than jumping away from one another.
                DrawOutlinedLabel(drawRect, displayText, style);
                return;
            }

            // The draw rect includes generous alignment padding. Only reserve the
            // actual glyph area so small gun-damage labels do not leap far apart.
            Vector2 textSize = style.CalcSize(new GUIContent(displayText));
            Rect visibleRect = new Rect(drawRect.center.x - (textSize.x + 8f) * 0.5f,
                drawRect.center.y - (textSize.y + 8f) * 0.5f, textSize.x + 8f, textSize.y + 8f);
            Rect resolvedVisibleRect = ReserveVisibleRect(visibleRect);
            drawRect.position += resolvedVisibleRect.center - visibleRect.center;
            DrawOutlinedLabel(drawRect, displayText, style);
        }

        private Vector2 GetMissionFlightCenter(Vector3 screenPosition, float labelHeight, float progress)
        {
            if (usesHealthBarOrigin && !healthBarOriginReady) PrepareHealthBarOrigin(labelHeight);
            if (screenPositionLocked) screenPosition = lockedScreenPosition;
            Vector2 destination = VoxelMissionProgress.Active != null
                ? VoxelMissionProgress.Active.ScoreScreenPosition : new Vector2(Screen.width * .5f, 39f);
            Vector2 start = new Vector2(screenPosition.x, Screen.height - screenPosition.y) + destructionOffset;
            Vector2 center = Vector2.Lerp(start, destination, progress);
            if (usesHealthBarOrigin && sourceHealthBar != null && sourceHealthBar.gameObject.activeInHierarchy &&
                sourceHealthBar.TryGetScreenRect(viewCamera, out Rect barRect))
                center.y = Mathf.Min(center.y, HealthBarPopupCenterY(barRect, labelHeight, 1f - progress));
            return center;
        }

        private float HealthBarPopupCenterY(Rect barRect, float labelHeight, float scale = 1f) =>
            barRect.yMin - HealthBarGapPixels * HudScale - (labelHeight * .5f + FlightOutline) * scale;

        private void PlaceDestructionLabel(Vector3 screenPosition, Vector2 labelSize)
        {
            if (!screenPositionLocked)
            {
                lockedScreenPosition = screenPosition;
                screenPositionLocked = true;
            }
            Vector2 center = GetMissionFlightCenter(screenPosition, labelSize.y, 0f);
            healthBarOriginReady = true;
            Vector2 size = labelSize + Vector2.one * (2f * FlightOutline + 6f * HudScale);
            Rect desired = new Rect(center - size * .5f, size);
            desired.x = Mathf.Clamp(desired.x, 0f, Mathf.Max(0f, Screen.width - size.x));

            DestructionObstacles.Clear();
            foreach (var popup in ActiveMissionPopups)
            {
                if (popup == this) continue;
                if (popup.hasDestructionSlot)
                    DestructionObstacles.Add(popup.destructionSlot);
                else if (IsDestruction(popup.displayStyle))
                    // It will make room when first drawn.
                    continue;
                Camera camera = popup.viewCamera != null ? popup.viewCamera : viewCamera;
                if (!popup.screenPositionLocked && camera == null) continue;
                Vector3 position = popup.screenPositionLocked ? popup.lockedScreenPosition : camera.WorldToScreenPoint(popup.transform.position);
                if (position.z <= 0f) continue;
                float progress = MissionFlightProgress(popup.elapsed, popup.lifetime, popup.holdDuration);
                if (popup.hasDestructionSlot && progress <= 0f) continue;
                GUIStyle style = GetStyle(popup.displayStyle, popup.isNegativeScore);
                Vector2 otherSize = style.CalcSize(new GUIContent(popup.displayText));
                Vector2 otherCenter = popup.GetMissionFlightCenter(position, otherSize.y, progress);
                otherSize = (otherSize + Vector2.one * (2f * popup.FlightOutline)) * (1f - progress);
                DestructionObstacles.Add(new Rect(otherCenter - otherSize * .5f, otherSize));
            }
            // Keep the selected slot until the popup is recycled, even as neighbours expire.
            destructionSlot = FindDestructionSlot(desired, DestructionObstacles, Screen.width);
            destructionOffset = destructionSlot.center - center;
            hasDestructionSlot = true;
        }

        private static Rect FindDestructionSlot(Rect desired, List<Rect> obstacles, float screenWidth)
        {
            // Prefer a slot on the same row. Move up only when the row is full.
            Rect row = desired;
            do
            {
                if (IsDestructionSlotFree(row, obstacles)) return row;

                Rect closest = row;
                float bestDistance = float.PositiveInfinity;
                foreach (Rect occupied in obstacles)
                {
                    if (row.yMin >= occupied.yMax || row.yMax <= occupied.yMin) continue;
                    ConsiderSlot(occupied.xMin - row.width - .01f);
                    ConsiderSlot(occupied.xMax + .01f);
                }
                if (bestDistance < float.PositiveInfinity) return closest;
                row.y -= row.height;

                void ConsiderSlot(float x)
                {
                    float distance = Mathf.Abs(x - row.x);
                    if (distance >= bestDistance || x < 0f || x + row.width > screenWidth) return;
                    Rect candidate = new Rect(x, row.y, row.width, row.height);
                    if (!IsDestructionSlotFree(candidate, obstacles)) return;
                    closest = candidate;
                    bestDistance = distance;
                }
            } while (row.yMin >= 0f);

            return desired;
        }

        private static bool IsDestructionSlotFree(Rect candidate, List<Rect> obstacles)
        {
            foreach (Rect occupied in obstacles)
                if (candidate.Overlaps(occupied)) return false;
            return true;
        }

        private void PrepareHealthBarOrigin(float labelHeight)
        {
            if (sourceHealthBar != null && sourceHealthBar.gameObject.activeInHierarchy &&
                sourceHealthBar.TryGetScreenRect(viewCamera, out Rect current))
            { initialHealthBarRect = current; hasInitialHealthBarRect = true; }
            if (!hasInitialHealthBarRect) return;
            float spread = initialHealthBarRect.width * .25f;
            float x = initialHealthBarRect.center.x + Mathf.Clamp(lockedScreenPosition.x - initialHealthBarRect.center.x, -spread, spread);
            lockedScreenPosition = new Vector3(x, Screen.height - HealthBarPopupCenterY(initialHealthBarRect, labelHeight), 1f);
            screenPositionLocked = healthBarOriginReady = true;
        }

        /// <summary>Reserves a popup rectangle for this repaint, moving later score labels clear of earlier ones.</summary>
        private static Rect ReserveVisibleRect(Rect desired)
        {
            if (popupLayoutFrame != Time.frameCount)
            {
                popupLayoutFrame = Time.frameCount;
                DrawnPopupRects.Clear();
            }

            Rect resolved = desired;
            const int maximumAttempts = 8;
            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                bool overlaps = false;
                foreach (Rect occupied in DrawnPopupRects)
                {
                    if (!resolved.Overlaps(occupied))
                        continue;

                    // Keep the horizontal random offset intact, then select the
                    // shortest vertical nudge that clears this specific overlap.
                    const float gap = 3f;
                    float moveUp = occupied.yMin - resolved.yMax - gap;
                    float moveDown = occupied.yMax - resolved.yMin + gap;
                    resolved.y += Mathf.Abs(moveUp) <= Mathf.Abs(moveDown) ? moveUp : moveDown;
                    overlaps = true;
                    break;
                }
                if (!overlaps)
                    break;
            }

            resolved.y = Mathf.Clamp(resolved.y, 0f, Mathf.Max(0f, Screen.height - resolved.height));
            DrawnPopupRects.Add(resolved);
            return resolved;
        }

        private void DrawOutlinedLabel(Rect rect, string text, GUIStyle style, float outlinePixels = 2f)
        {
            Color originalColour = GUI.color;
            float opacity = GetOpacity();
            GUI.color = new Color(0f, 0f, 0f, originalColour.a * opacity);
            for (int horizontal = -1; horizontal <= 1; horizontal++)
            for (int vertical = -1; vertical <= 1; vertical++)
            {
                if (horizontal == 0 && vertical == 0)
                    continue;
                GUI.Label(new Rect(rect.x + horizontal * outlinePixels, rect.y + vertical * outlinePixels,
                    rect.width, rect.height), text, style);
            }
            GUI.color = new Color(1f, 1f, 1f, originalColour.a * opacity);
            GUI.Label(rect, text, style);
            GUI.color = originalColour;
        }

        private float GetOpacity()
        {
            if (fadeOutDuration <= 0f || elapsed < lifetime - fadeOutDuration)
                return 1f;
            return Mathf.Clamp01((lifetime - elapsed) / fadeOutDuration);
        }

        private static GUIStyle GetStyle(Style style, bool negative = false)
        {
            if (negative)
            {
                if (!PenaltyStyles.TryGetValue(style, out var penalty))
                {
                    penalty = CreateStyle(GetStyle(style).fontSize, new Color(1f, .15f, .1f), IsDestruction(style));
                    penalty.font = PenaltyFont;
                    PenaltyStyles[style] = penalty;
                }
                if (IsDestruction(style)) penalty.fontSize = DestructionFontSize;
                return penalty;
            }
            switch (style)
            {
                case Style.BonusCash:
                    if (cashStyle == null)
                    {
                        cashStyle = CreateStyle(42, new Color(1f, .82f, .12f));
                        cashStyle.font = Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
                    }
                    return cashStyle;
                case Style.RamDamage:
                    return ramStyle ??= CreateStyle(152, new Color(1f, 0.38f, 0.04f));
                case Style.EnemyDestroyed:
                case Style.FuelDrumDestroyed:
                    destroyedStyle ??= CreateStyle(192, new Color(0.96f, 0.08f, 0.04f), true);
                    destroyedStyle.fontSize = DestructionFontSize;
                    return destroyedStyle;
                default:
                    return weaponStyle ??= CreateStyle(60, new Color(1f, 0.86f, 0.08f));
            }
        }

        private static GUIStyle CreateStyle(int fontSize, Color colour, bool singleLine = false)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = VoxelHudStyles.HudFont,
                fontSize = fontSize,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = colour }
            };
            if (singleLine)
            {
                style.wordWrap = false;
                style.clipping = TextClipping.Overflow;
            }
            // Labels can use hover/active states while a finger or mouse remains
            // over the hit. Every state must keep the same intended popup colour.
            style.hover.textColor = colour;
            style.active.textColor = colour;
            style.focused.textColor = colour;
            style.onNormal.textColor = colour;
            style.onHover.textColor = colour;
            style.onActive.textColor = colour;
            style.onFocused.textColor = colour;
            return style;
        }
    }
}
