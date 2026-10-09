using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Touch controls for steering, braking and firing on mobile builds.</summary>
    public sealed class VoxelMobileControls : MonoBehaviour
    {
        private VoxelCarController target;
        private CanvasGroup canvasGroup;
        private Texture2D fireButtonTexture;
        private Sprite fireButtonSprite;
        private Texture2D brakeButtonTexture;
        private Sprite brakeButtonSprite;
        private VoxelMissileButtonDisplay missileDisplay;
        private static bool fireHeld, missileHeld, brakeHeld;
        private static bool HasMissileUpgrade => VoxelMissileUpgradeState.IsPurchased(false) ||
            VoxelMissileUpgradeState.IsPurchased(true);

        /// <summary>Read by all mounted guns in addition to the keyboard Ctrl binding.</summary>
        public static bool IsFireHeld => fireHeld && !VoxelPauseMenu.IsPaused;
        public static bool IsMissileHeld => missileHeld && !VoxelPauseMenu.IsPaused;
        public static bool IsBrakeHeld => brakeHeld && !VoxelPauseMenu.IsPaused;
        public static void ClearHeldInput() { fireHeld = missileHeld = brakeHeld = false; }

        public void Configure(VoxelCarController controller)
        {
            brakeHeld = false;
            target = controller;
            missileDisplay?.Configure(controller);
            UpdateMissileVisibility();
        }

        private void Awake() => BuildHud();

        private void OnDisable() => ClearHeldInput();
        private void OnApplicationFocus(bool focused) { if (!focused) ClearHeldInput(); }

        private void Update()
        {
            if (canvasGroup == null)
                return;

            UpdateMissileVisibility();
            bool hidden = !Application.isPlaying || target == null || target.IsDestroyed ||
                VoxelPauseMenu.IsPaused || VoxelPlayerDeathScreen.IsShowing || VoxelMissionProgress.Active?.IsComplete == true;
            canvasGroup.alpha = hidden ? 0f : VoxelStartCountdown.CurrentGameplayHudAlpha;
            canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.01f;
            if (hidden)
                ClearHeldInput();
            if (canvasGroup.alpha <= 0.01f)
                brakeHeld = false;
        }

        internal void ChangeLane(int direction)
        {
            target?.RequestLaneChangeDirection(direction);
        }

        internal void SetFireHeld(bool value)
        {
            fireHeld = value && !VoxelPauseMenu.IsPaused && target != null && !target.IsDestroyed &&
                VoxelMissionProgress.Active?.IsComplete != true;
        }

        internal void SetMissileHeld(bool value)
        {
            missileHeld = value && !VoxelPauseMenu.IsPaused && HasMissileUpgrade && target != null && !target.IsDestroyed &&
                VoxelMissionProgress.Active?.IsComplete != true;
        }

        internal void SetBrakeHeld(bool value)
        {
            brakeHeld = value && !VoxelPauseMenu.IsPaused && target != null && !target.IsDestroyed &&
                !VoxelPlayerDeathScreen.IsShowing && VoxelMissionProgress.Active?.IsComplete != true;
        }

        private void UpdateMissileVisibility()
        {
            if (missileDisplay == null) return;
            bool visible = target != null && HasMissileUpgrade;
            if (missileDisplay.gameObject.activeSelf != visible)
                missileDisplay.gameObject.SetActive(visible);
            if (!visible) missileHeld = false;
        }

        private void BuildHud()
        {
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Temporary Mobile Controls");
            canvas.GetComponent<Canvas>().sortingOrder = 105;
            // Match the integrity dial and boost's height-based scale on wide phones.
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.matchWidthOrHeight = 1f;
            scaler.enabled = false; scaler.enabled = true;
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            // Keep generous 200px touch targets, with a 20px gap between them.
            CreateSteeringControl(canvas, "Move Left Button", new Vector2(150f, 150f), MobileControlAction.Left);
            CreateSteeringControl(canvas, "Move Right Button", new Vector2(370f, 150f), MobileControlAction.Right);
            CreateControl(canvas, "Brake Button", null, Vector2.zero,
                new Vector2(260f, 390f), new Vector2(200f, 200f), MobileControlAction.Brake,
                Color.white, 0, CreateBrakeButtonSprite());
            CreateControl(canvas, "Fire Button", null, new Vector2(1f, 0f),
                new Vector2(-150f, 150f), new Vector2(200f, 200f), MobileControlAction.Fire,
                Color.white, 0, CreateFireButtonSprite());
            Image missileButton = CreateControl(canvas, "Missile Button", "FIRE\nMISSILE", new Vector2(1f, 0f),
                new Vector2(-150f, 390f), new Vector2(200f, 200f), MobileControlAction.Missile,
                Color.white, 33);
            missileDisplay = missileButton.gameObject.AddComponent<VoxelMissileButtonDisplay>();
            missileDisplay.Build();
            missileButton.rectTransform.localScale = Vector3.one * 1.15f;
            missileDisplay.Configure(target);
            UpdateMissileVisibility();
        }

        private void CreateSteeringControl(Transform parent, string name, Vector2 position, MobileControlAction action)
        {
            Image button = CreateControl(parent, name, null, Vector2.zero, position,
                new Vector2(200f, 200f), action, Color.clear, 0);
            var chevronObject = new GameObject("Chevron", typeof(RectTransform), typeof(VoxelSteeringChevron));
            chevronObject.transform.SetParent(button.transform, false);
            RectTransform rect = chevronObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(88f, 128f);
            if (action == MobileControlAction.Left)
                rect.localRotation = Quaternion.Euler(0f, 0f, 180f);
            VoxelSteeringChevron chevron = chevronObject.GetComponent<VoxelSteeringChevron>();
            chevron.color = new Color(1f, 1f, 1f, 0.55f);
            chevron.raycastTarget = false;
        }

        private Image CreateControl(Transform parent, string name, string label, Vector2 anchor,
            Vector2 position, Vector2 size, MobileControlAction action, Color colour, int fontSize,
            Sprite backgroundSprite = null)
        {
            var controlObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(VoxelMobileControlButton));
            controlObject.transform.SetParent(parent, false);
            RectTransform rect = controlObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = controlObject.GetComponent<Image>();
            image.color = colour;
            image.sprite = backgroundSprite;

            var input = controlObject.GetComponent<VoxelMobileControlButton>();
            input.Configure(this, action);
            if (!string.IsNullOrEmpty(label))
            {
                Text text = VoxelMenuUi.CreateText(controlObject.transform, "Label", label, fontSize,
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
                text.font = Resources.Load<Font>("Fonts/IMPACTED") ?? VoxelHudStyles.HudFont;
                text.fontStyle = FontStyle.Normal;
                text.color = Color.white;
                text.raycastTarget = false;
            }
            return image;
        }

        private Sprite CreateFireButtonSprite()
        {
            if (fireButtonSprite != null) return fireButtonSprite;
            const int textureSize = 256;
            fireButtonTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "Runtime Cockpit GUNS Button",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            // Bake the bezel, red cap, screws and white reticule once per HUD.
            var pixels = new Color[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                float u = (x + 0.5f) / textureSize * 2f - 1f;
                float v = (y + 0.5f) / textureSize * 2f - 1f;
                float radius = new Vector2(u, v).magnitude;
                float outline = Mathf.Max(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)),
                    (Mathf.Abs(u) + Mathf.Abs(v)) / 1.65f);
                float light = Mathf.Clamp01(0.5f + v * 0.3f - u * 0.2f);
                Color colour = Color.Lerp(new Color(0.065f, 0.075f, 0.085f),
                    new Color(0.20f, 0.22f, 0.24f), light);
                if (outline > 0.94f)
                    colour = Color.Lerp(new Color(0.12f, 0.14f, 0.16f), new Color(0.42f, 0.45f, 0.48f), light);
                if (radius < 0.87f)
                    colour = new Color(0.025f, 0.03f, 0.035f);
                if (radius < 0.82f)
                    colour = Color.Lerp(new Color(0.16f, 0.17f, 0.18f), new Color(0.60f, 0.62f, 0.64f), light);
                if (radius < 0.77f)
                {
                    float glow = Mathf.Exp(-((u + 0.22f) * (u + 0.22f) * 3f +
                        (v - 0.38f) * (v - 0.38f) * 5f));
                    colour = Color.Lerp(new Color(0.46f, 0.018f, 0.03f),
                        new Color(0.93f, 0.12f, 0.10f), 0.25f + glow * 0.65f);
                    if (radius > 0.70f)
                        colour = Color.Lerp(new Color(0.32f, 0.01f, 0.02f),
                            new Color(1f, 0.30f, 0.25f), light);
                }

                float screwX = Mathf.Abs(u) - 0.68f;
                float screwY = Mathf.Abs(v) - 0.68f;
                float screwRadius = new Vector2(screwX, screwY).magnitude;
                if (screwRadius < 0.062f)
                {
                    colour = screwRadius > 0.049f ? new Color(0.025f, 0.03f, 0.035f) :
                        Color.Lerp(new Color(0.26f, 0.28f, 0.30f), new Color(0.58f, 0.60f, 0.62f), light);
                    if (screwRadius < 0.042f && Mathf.Abs(screwX + screwY) < 0.012f)
                        colour = new Color(0.06f, 0.07f, 0.08f);
                }
                // Reference-style open ring with four short cardinal ticks; keep the centre clear.
                float ringDistance = Mathf.Abs(radius - 0.40f) - 0.025f;
                float verticalTick = Mathf.Max(Mathf.Abs(u) - 0.025f,
                    Mathf.Abs(Mathf.Abs(v) - 0.405f) - 0.125f);
                float horizontalTick = Mathf.Max(Mathf.Abs(v) - 0.025f,
                    Mathf.Abs(Mathf.Abs(u) - 0.405f) - 0.125f);
                float reticuleDistance = Mathf.Min(ringDistance, Mathf.Min(verticalTick, horizontalTick));
                float reticuleAlpha = Mathf.Clamp01(0.5f - reticuleDistance * textureSize * 0.5f);
                colour = Color.Lerp(colour, Color.white, reticuleAlpha);
                colour.a = Mathf.Clamp01((1f - outline) * textureSize * 0.5f);
                pixels[y * textureSize + x] = colour;
            }
            fireButtonTexture.SetPixels(pixels);
            fireButtonTexture.Apply(false, true);
            return fireButtonSprite = Sprite.Create(fireButtonTexture, new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f), textureSize, 0, SpriteMeshType.FullRect);
        }

        private Sprite CreateBrakeButtonSprite()
        {
            if (brakeButtonSprite != null) return brakeButtonSprite;
            const int size = 256;
            brakeButtonTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Brake Disc Button",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            var caliperBolts = new Vector2[3];
            for (int i = 0; i < caliperBolts.Length; i++)
            {
                float angle = (20f + i * 27f) * Mathf.Deg2Rad;
                caliperBolts[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.665f;
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                var point = new Vector2(u, v);
                float radius = point.magnitude;
                float angle = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                float light = Mathf.Clamp01(0.55f + v * 0.25f - u * 0.15f);
                Color face = new Color(0.055f, 0.07f, 0.085f);
                Color colour = radius > 0.9f
                    ? Color.Lerp(new Color(0.18f, 0.21f, 0.24f), new Color(0.50f, 0.54f, 0.58f), light)
                    : face;

                // Rotor silhouette and separated upper-right caliper follow the reference.
                if (radius < 0.74f && !(radius > 0.43f && angle > -5f && angle < 94f))
                {
                    colour = Color.Lerp(new Color(0.58f, 0.63f, 0.68f), new Color(0.96f, 0.98f, 1f), light);
                    if ((radius > 0.36f && radius < 0.41f) || radius < 0.135f ||
                        new Vector2(Mathf.Abs(u) - 0.20f, Mathf.Abs(v) - 0.20f).magnitude < 0.035f)
                        colour = face;
                }
                if (radius > 0.48f && radius < 0.86f && angle > 8f && angle < 86f)
                {
                    colour = Color.Lerp(new Color(0.50f, 0.025f, 0.035f), new Color(0.97f, 0.16f, 0.12f), light);
                    foreach (Vector2 bolt in caliperBolts)
                        if (Vector2.Distance(point, bolt) < 0.047f)
                            colour = new Color(0.92f, 0.95f, 0.98f);
                }
                colour.a = Mathf.Clamp01(0.5f + (0.98f - radius) * size * 0.5f);
                pixels[y * size + x] = colour;
            }
            brakeButtonTexture.SetPixels(pixels);
            brakeButtonTexture.Apply(false, true);
            return brakeButtonSprite = Sprite.Create(brakeButtonTexture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }

        private void OnDestroy()
        {
            if (brakeButtonSprite != null)
            {
                if (Application.isPlaying) Destroy(brakeButtonSprite);
                else DestroyImmediate(brakeButtonSprite);
            }
            if (brakeButtonTexture != null)
            {
                if (Application.isPlaying) Destroy(brakeButtonTexture);
                else DestroyImmediate(brakeButtonTexture);
            }
            if (fireButtonSprite != null)
            {
                if (Application.isPlaying) Destroy(fireButtonSprite);
                else DestroyImmediate(fireButtonSprite);
            }
            if (fireButtonTexture != null)
            {
                if (Application.isPlaying) Destroy(fireButtonTexture);
                else DestroyImmediate(fireButtonTexture);
            }
        }
    }

    internal enum MobileControlAction { Left, Right, Fire, Missile, Brake }

    /// <summary>Pointer-down handling lets FIRE remain active for the duration of a touch.</summary>
    internal sealed class VoxelMobileControlButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private VoxelMobileControls owner;
        private MobileControlAction action;

        public void Configure(VoxelMobileControls controls, MobileControlAction controlAction)
        {
            owner = controls;
            action = controlAction;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (action == MobileControlAction.Fire)
                owner?.SetFireHeld(true);
            else if (action == MobileControlAction.Missile)
                owner?.SetMissileHeld(true);
            else if (action == MobileControlAction.Brake)
                owner?.SetBrakeHeld(true);
            else
                owner?.ChangeLane(action == MobileControlAction.Left ? -1 : 1);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (action == MobileControlAction.Fire)
                owner?.SetFireHeld(false);
            else if (action == MobileControlAction.Missile)
                owner?.SetMissileHeld(false);
            else if (action == MobileControlAction.Brake)
                owner?.SetBrakeHeld(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (action == MobileControlAction.Fire)
                owner?.SetFireHeld(false);
            else if (action == MobileControlAction.Missile)
                owner?.SetMissileHeld(false);
            else if (action == MobileControlAction.Brake)
                owner?.SetBrakeHeld(false);
        }
    }
}
