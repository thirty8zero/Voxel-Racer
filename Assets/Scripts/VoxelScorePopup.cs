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
            EnemyDestroyed
        }

        private const float QuickLifetime = 0.2f;
        private static readonly Queue<VoxelScorePopup> Pool = new();
        private static Transform poolRoot;

        private float elapsed;
        private float lifetime;
        private float riseSpeed;
        private Camera viewCamera;
        private string displayText;
        private Style displayStyle;
        private static GUIStyle weaponStyle;
        private static GUIStyle ramStyle;
        private static GUIStyle destroyedStyle;

        public static void Show(Vector3 position, int points, Style style)
        {
            if (!Application.isPlaying || points == 0)
                return;

            VoxelScorePopup popup = Pool.Count > 0 ? Pool.Dequeue() : Create();
            popup.Begin(position, points, style);
        }

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

        private void Begin(Vector3 position, int points, Style style)
        {
            elapsed = 0f;
            viewCamera = Camera.main;
            // Scatter on the camera's screen-horizontal axis so simultaneous awards
            // remain readable regardless of the road's current heading.
            Vector3 screenRight = viewCamera != null ? viewCamera.transform.right : Vector3.right;
            transform.position = position + screenRight * Random.Range(-0.7f, 0.7f);
            displayText = points > 0 ? $"+{points}" : points.ToString();
            displayStyle = style;

            switch (style)
            {
                case Style.RamDamage:
                    riseSpeed = 8.5f;
                    lifetime = 2f;
                    break;
                case Style.EnemyDestroyed:
                    riseSpeed = 10f;
                    lifetime = 2f;
                    break;
                default:
                    riseSpeed = 7.5f;
                    lifetime = QuickLifetime;
                    break;
            }

            gameObject.SetActive(true);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed <= QuickLifetime)
                transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

            if (elapsed >= lifetime)
            {
                gameObject.SetActive(false);
                Pool.Enqueue(this);
            }
        }

        private void OnGUI()
        {
            if (viewCamera == null)
                viewCamera = Camera.main;
            if (viewCamera == null || string.IsNullOrEmpty(displayText))
                return;

            Vector3 screenPosition = viewCamera.WorldToScreenPoint(transform.position);
            if (screenPosition.z <= 0f)
                return;

            GUIStyle style = GetStyle(displayStyle);
            float width = displayStyle == Style.WeaponDamage ? 150f : 240f;
            float height = displayStyle == Style.WeaponDamage ? 64f : 120f;
            float centerX = Mathf.Clamp(screenPosition.x, width * 0.5f, Screen.width - width * 0.5f);
            float centerY = Mathf.Clamp(Screen.height - screenPosition.y,
                height * 0.5f, Screen.height - height * 0.5f);
            GUI.Label(new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height), displayText, style);
        }

        private static GUIStyle GetStyle(Style style)
        {
            switch (style)
            {
                case Style.RamDamage:
                    return ramStyle ??= CreateStyle(76, new Color(1f, 0.38f, 0.04f));
                case Style.EnemyDestroyed:
                    return destroyedStyle ??= CreateStyle(96, new Color(0.96f, 0.08f, 0.04f));
                default:
                    return weaponStyle ??= CreateStyle(30, new Color(1f, 0.86f, 0.08f));
            }
        }

        private static GUIStyle CreateStyle(int fontSize, Color colour)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = VoxelHudStyles.HudFont,
                fontSize = fontSize,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = colour }
            };
        }
    }
}
