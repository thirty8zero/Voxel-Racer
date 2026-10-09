using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Raised blue pushbutton above the brake; only the cap moves during recharge.</summary>
    public sealed class VoxelBoostDisplay : MonoBehaviour
    {
        private VoxelBoostController boost;
        private CanvasGroup canvasGroup;
        private Image boostRing;
        private Text boostText;
        private RectTransform cap;
        private Texture2D capTexture, stemTexture;
        private Sprite capSprite, stemSprite;
        private float capHeight = 1f;
        private Texture2D ringTexture;
        private Texture2D discTexture;
        private Sprite ringSprite;
        private Sprite discSprite;
        private const float ArcFraction = 300f / 360f;
        private static readonly Vector2 ControlPosition = new Vector2(260f, 630f);

        public void Configure(VoxelBoostController controller) => boost = controller;

        private void Awake() => BuildHud();

        private void Update()
        {
            if (canvasGroup == null)
                return;

            bool hide = !Application.isPlaying || boost == null || boost.Tuning == null ||
                boost.Target == null || boost.Target.IsDestroyed || VoxelPlayerDeathScreen.IsShowing ||
                VoxelMissionProgress.Active?.IsComplete == true;
            canvasGroup.alpha = hide ? 0f : VoxelStartCountdown.CurrentGameplayHudAlpha;
            canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.01f && !VoxelPauseMenu.IsPaused;
            if (canvasGroup.alpha <= 0f)
                return;

            // Active charge is remaining burst time, so hold the cap down until recharge begins.
            float height = boost.IsBoosting ? 0f : Mathf.Clamp01(boost.ChargePercent);
            capHeight = height < capHeight ? Mathf.MoveTowards(capHeight, height, Time.deltaTime / .08f) : height;
            RefreshPresentation(capHeight, boost.ChargePercent, boost.IsReady || boost.IsBoosting);
        }

        internal void Press()
        {
            if (canvasGroup == null || !canvasGroup.blocksRaycasts || canvasGroup.alpha <= .01f ||
                boost == null || !boost.TryActivateBoost()) return;
            capHeight = 0f;
            RefreshPresentation(0f, boost.ChargePercent, true);
        }

        private void RefreshPresentation(float height, float charge, bool illuminated)
        {
            cap.anchoredPosition = new Vector2(0f, Mathf.Lerp(-8f, 24f, height));
            cap.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, height);
            boostRing.fillAmount = Mathf.Clamp01(charge) * ArcFraction;
            boostText.color = new Color(.65f, .9f, 1f, illuminated ? 1f : .65f);
        }

        private void BuildHud()
        {
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Boost HUD");
            canvas.GetComponent<Canvas>().sortingOrder = 100;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.matchWidthOrHeight = 1f;
            scaler.enabled = false; scaler.enabled = true;
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            // Keep a fixed touch target while the raised artwork moves inside it.
            discSprite = Bake("Runtime Boost Metal Socket", out discTexture, false, false);
            var buttonObject = new GameObject("Boost Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(VoxelBoostButtonPointer));
            buttonObject.transform.SetParent(canvas, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.zero;
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = ControlPosition;
            buttonRect.sizeDelta = new Vector2(220f, 220f);
            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = Color.clear;
            Button button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            buttonObject.GetComponent<VoxelBoostButtonPointer>().Owner = this;
            stemSprite = Bake("Runtime Boost Ribbed Stem", out stemTexture, false, true);
            capSprite = Bake("Runtime Boost Blue Dome", out capTexture, true, false);
            Face(buttonRect, "Boost Socket", discSprite, new Vector2(0f, -36f), new Vector2(150f, 76f));
            Face(buttonRect, "Boost Stem", stemSprite, new Vector2(0f, -4f), new Vector2(94f, 66f));
            cap = Face(buttonRect, "Boost Cap", capSprite, new Vector2(0f, 24f), new Vector2(184f, 132f)).rectTransform;

            ringSprite = CreateRingSprite();
            Image track = CreateRingImage(canvas, ringSprite);
            track.name = "Boost Charge Track";
            track.color = new Color(0.04f, 0.16f, 0.27f, 0.8f);
            boostRing = CreateRingImage(canvas, ringSprite);
            boostRing.name = "Boost Charge Ring";
            boostRing.color = new Color(0.12f, 0.65f, 1f, 1f);

            boostText = VoxelMenuUi.CreateText(cap, "Boost Label", "BOOST", 43, TextAnchor.MiddleCenter,
                new Vector2(.5f, .5f), new Vector2(0f, 8f), new Vector2(152f, 66f));
            boostText.font = Resources.Load<Font>("Fonts/IMPACTED") ?? VoxelHudStyles.HudFont;
            boostText.resizeTextForBestFit = true;
            boostText.resizeTextMinSize = 12;
            boostText.resizeTextMaxSize = 43;
            boostText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            var shadow = boostText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.01f, .08f, .2f, .9f);
            shadow.effectDistance = new Vector2(1f, -2f);
            boostText.raycastTarget = false;
            RefreshPresentation(1f, 1f, true);
        }

        private static Image CreateRingImage(Transform parent, Sprite sprite)
        {
            var ringObject = new GameObject("Boost Radial Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringObject.transform.SetParent(parent, false);
            RectTransform rect = ringObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = ControlPosition;
            // Start at seven o'clock and sweep clockwise to five, leaving the bottom open.
            rect.localRotation = Quaternion.Euler(0f, 0f, -30f);
            rect.sizeDelta = new Vector2(220f, 220f);
            Image image = ringObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Bottom;
            image.fillClockwise = true;
            image.fillAmount = ArcFraction;
            image.raycastTarget = false;
            return image;
        }

        private Sprite CreateRingSprite()
        {
            const int textureSize = 256;
            ringTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "Runtime Boost Ring", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            float radius = textureSize * 0.5f;
            for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                float alpha = Mathf.Clamp01(0.5f + Mathf.Min(distance - 0.78f, 1f - distance) * radius);
                ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            ringTexture.Apply(false, true);
            return Sprite.Create(ringTexture, new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f), textureSize, 0, SpriteMeshType.FullRect);
        }

        private static Image Face(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.rectTransform.sizeDelta = size;
            image.rectTransform.anchoredPosition = position;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite Bake(string name, out Texture2D texture, bool blue, bool stem)
        {
            const int textureSize = 256;
            texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                float u = (x + .5f) / textureSize * 2f - 1f, v = (y + .5f) / textureSize * 2f - 1f;
                float distance = Mathf.Sqrt(u * u + v * v);
                float light = Mathf.Clamp01(.55f + v * .25f - u * .25f);
                Color colour;
                if (stem)
                {
                    distance = Mathf.Max(Mathf.Abs(u) / .9f, Mathf.Abs(v) / .98f);
                    float rib = .5f + .5f * Mathf.Sin(v * 55f);
                    colour = Color.Lerp(new Color(.1f, .15f, .22f), new Color(.65f, .75f, .84f),
                        Mathf.Clamp01((1f - Mathf.Abs(u)) * .7f + rib * .2f));
                }
                else if (blue)
                {
                    float dome = Mathf.Sqrt(Mathf.Clamp01(1f - distance * distance));
                    colour = Color.Lerp(new Color(.015f, .13f, .4f), new Color(.06f, .61f, 1f),
                        Mathf.Clamp01(dome * .7f + light * .3f));
                    if (distance > .87f)
                        colour = Color.Lerp(new Color(.005f, .05f, .19f), new Color(.14f, .73f, 1f), light);
                    float gloss = Mathf.Exp(-((u + .27f) * (u + .27f) * 16f + (v - .47f) * (v - .47f) * 45f));
                    colour = Color.Lerp(colour, new Color(.7f, .92f, 1f), gloss * .75f);
                }
                else
                {
                    colour = Color.Lerp(new Color(.07f, .1f, .15f), new Color(.7f, .8f, .89f), light);
                    if (distance < .72f) colour *= .35f;
                    else if (distance > .9f) colour *= .6f;
                }
                colour.a = Mathf.Clamp01(.5f + (1f - distance) * textureSize * .5f);
                pixels[y * textureSize + x] = colour;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f), textureSize, 0, SpriteMeshType.FullRect);
        }

        private void OnDestroy()
        {
            Release(ringSprite);
            Release(discSprite);
            Release(ringTexture);
            Release(discTexture);
            Release(capSprite); Release(stemSprite); Release(capTexture); Release(stemTexture);
        }

        private static void Release(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }

    internal sealed class VoxelBoostButtonPointer : MonoBehaviour, IPointerDownHandler
    {
        internal VoxelBoostDisplay Owner;
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button == PointerEventData.InputButton.Left) Owner?.Press();
        }
    }
}
