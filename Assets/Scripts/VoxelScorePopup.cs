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
        private static readonly Queue<VoxelScorePopup> Pool = new();
        private static Transform poolRoot;

        private float elapsed;
        private float lifetime;
        private float riseSpeed;
        private float riseDuration;
        private float fadeOutDuration;
        private Vector3 sideDriftDirection;
        private Camera viewCamera;
        private string displayText;
        private Style displayStyle;
        private bool screenPositionLocked;
        private Vector3 lockedScreenPosition;
        private static GUIStyle weaponStyle;
        private static GUIStyle cashStyle;
        private static GUIStyle ramStyle;
        private static GUIStyle destroyedStyle;
        private static GUIStyle nearMissTitleStyle;
        private static GUIStyle nearMissPointsStyle;
        private static readonly List<Rect> DrawnPopupRects = new();
        private static int popupLayoutFrame = -1;
        private bool FliesToMission => displayStyle == Style.WeaponDamage || displayStyle == Style.RamDamage ||
            displayStyle == Style.EnemyDestroyed || displayStyle == Style.FuelDrumDestroyed;

        public static float MissionFlightProgress(float age, float duration) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - .12f) / Mathf.Max(.01f, duration - .12f)));

        public static void Show(Vector3 position, int points, Style style, float lifetimeOverride = -1f)
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
            popup.Begin(position, points, style, lifetimeOverride);
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
        {
            elapsed = 0f;
            viewCamera = Camera.main;
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
            if (FliesToMission) fadeOutDuration = 0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (FliesToMission)
            {
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
            // These are absolute-position IMGUI labels, so calculating and drawing
            // only during repaint prevents Layout/Repaint from registering a popup
            // twice in the overlap list.
            if (Event.current.type != EventType.Repaint)
                return;

            if (viewCamera == null)
                viewCamera = Camera.main;
            if (viewCamera == null || string.IsNullOrEmpty(displayText))
                return;

            Vector3 screenPosition = screenPositionLocked
                ? lockedScreenPosition
                : viewCamera.WorldToScreenPoint(transform.position);
            if (screenPosition.z <= 0f)
                return;

            if (FliesToMission)
            {
                float progress = MissionFlightProgress(elapsed, lifetime);
                float scale = 1f - progress;
                if (scale <= .0001f) return;
                Vector2 destination = VoxelMissionProgress.Active != null
                    ? VoxelMissionProgress.Active.PercentageScreenPosition : new Vector2(Screen.width * .5f, 39f);
                Vector2 start = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
                Vector2 center = Vector2.Lerp(start, destination, progress);
                var matrix = GUI.matrix;
                try
                {
                    GUIUtility.ScaleAroundPivot(Vector2.one * scale, center);
                    DrawOutlinedLabel(new Rect(center.x - 140f, center.y - 60f, 280f, 120f), displayText, GetStyle(displayStyle));
                }
                finally { GUI.matrix = matrix; }
                return;
            }

            if (displayStyle == Style.NearMiss)
            {
                const float nearMissWidth = 260f;
                const float nearMissHeight = 150f;
                float nearMissCenterX = Mathf.Clamp(screenPosition.x, nearMissWidth * 0.5f,
                    Screen.width - nearMissWidth * 0.5f);
                float nearMissCenterY = Mathf.Clamp(Screen.height - screenPosition.y, nearMissHeight * 0.5f,
                    Screen.height - nearMissHeight * 0.5f);
                Rect groupRect = ReserveVisibleRect(new Rect(nearMissCenterX - nearMissWidth * 0.5f,
                    nearMissCenterY - 72f, nearMissWidth, nearMissHeight));
                DrawOutlinedLabel(new Rect(groupRect.x, groupRect.y, nearMissWidth, 54f), "NEAR MISS!",
                    nearMissTitleStyle ??= CreateStyle(38, Color.white));
                DrawOutlinedLabel(new Rect(groupRect.x, groupRect.y + 52f, nearMissWidth, 78f), displayText,
                    nearMissPointsStyle ??= CreateStyle(76, new Color(1f, 0.86f, 0.08f)));
                return;
            }

            GUIStyle style = GetStyle(displayStyle);
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

        private void DrawOutlinedLabel(Rect rect, string text, GUIStyle style)
        {
            Color originalColour = GUI.color;
            float opacity = GetOpacity();
            GUI.color = new Color(0f, 0f, 0f, originalColour.a * opacity);
            const float outlinePixels = 2f;
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

        private static GUIStyle GetStyle(Style style)
        {
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
                    return ramStyle ??= CreateStyle(76, new Color(1f, 0.38f, 0.04f));
                case Style.EnemyDestroyed:
                case Style.FuelDrumDestroyed:
                    return destroyedStyle ??= CreateStyle(96, new Color(0.96f, 0.08f, 0.04f));
                default:
                    return weaponStyle ??= CreateStyle(30, new Color(1f, 0.86f, 0.08f));
            }
        }

        private static GUIStyle CreateStyle(int fontSize, Color colour)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = VoxelHudStyles.HudFont,
                fontSize = fontSize,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = colour }
            };
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
