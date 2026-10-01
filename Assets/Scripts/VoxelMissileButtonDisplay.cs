using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Bevelled red launch button; its face recharges clockwise from twelve o'clock.</summary>
    public sealed class VoxelMissileButtonDisplay : MonoBehaviour
    {
        private VoxelCarController target;
        private VoxelGunMount[] mounts;
        private Image charge;
        private Text label;
        private Texture2D casingTexture, faceTexture;
        private Sprite casingSprite, faceSprite;

        public float ChargePercent { get; private set; }
        public bool IsReady { get; private set; }

        public void Configure(VoxelCarController car)
        {
            target = car;
            // Race upgrades are installed before the HUD is configured. Cache once, not every frame.
            mounts = car != null ? car.GetComponentsInChildren<VoxelGunMount>(true) : null;
            Refresh();
        }

        public void Build()
        {
            if (charge != null) return;
            var casing = GetComponent<Image>();
            casingTexture = MakeTexture("Missile Button Bevel", true);
            casingSprite = MakeSprite(casingTexture);
            casing.sprite = casingSprite;
            casing.color = Color.white;

            faceTexture = MakeTexture("Missile Button Red Face", false);
            faceSprite = MakeSprite(faceTexture);
            var face = new GameObject("Missile Cooldown Face", typeof(RectTransform), typeof(Image));
            face.transform.SetParent(transform, false);
            var rect = face.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, 14);
            rect.sizeDelta = new Vector2(176, 128);
            charge = face.GetComponent<Image>();
            charge.sprite = faceSprite;
            charge.type = Image.Type.Filled;
            charge.fillMethod = Image.FillMethod.Radial360;
            charge.fillOrigin = (int)Image.Origin360.Top;
            charge.fillClockwise = true;
            charge.raycastTarget = false;

            label = GetComponentInChildren<Text>();
            label.rectTransform.anchoredPosition = new Vector2(0, 14);
            label.rectTransform.sizeDelta = new Vector2(128, 94);
            label.rectTransform.localScale = new Vector3(1.3f, 1f, 1f);
            label.fontSize = 46;
            label.lineSpacing = .82f;
            label.text = "FIRE\nMISSILE";
            label.transform.SetAsLastSibling();
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.12f, .01f, .02f, .85f);
            shadow.effectDistance = new Vector2(1.5f, -2f);
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            ChargePercent = 0f;
            IsReady = false;
            if (target != null && target.isActiveAndEnabled && !target.IsDestroyed &&
                VoxelMissionProgress.Active?.IsComplete != true &&
                (VoxelStartCountdown.Active == null || VoxelStartCountdown.Active.IsComplete) && mounts != null)
            {
                foreach (var mount in mounts)
                {
                    if (mount == null || !mount.isActiveAndEnabled || mount.tuning == null ||
                        mount.tuning.projectileKind != VoxelProjectileKind.Missile || !mount.HasAmmunition) continue;
                    // Either usable side can fire; exhausted/disabled launchers cannot make the face look ready.
                    ChargePercent = Mathf.Max(ChargePercent, mount.CooldownProgress);
                    IsReady |= mount.IsReady;
                }
            }
            if (charge != null) charge.fillAmount = ChargePercent;
            if (label != null) label.color = IsReady ? Color.white : new Color(.78f, .74f, .75f, 1f);
        }

        private static Texture2D MakeTexture(string name, bool bevel)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size, v = (y + .5f) / size;
                if (!bevel)
                {
                    float r = new Vector2((u - .5f) * 2f, (v - .5f) * 2f).magnitude;
                    float glow = Mathf.Exp(-((u - .3f) * (u - .3f) * 8f + (v - .82f) * (v - .82f) * 16f));
                    var c = Color.Lerp(new Color(.46f, .025f, .045f), new Color(.79f, .10f, .14f), .35f + glow * .6f);
                    c.a = Mathf.Clamp01((1f - r) * size / 2f);
                    pixels[y * size + x] = c;
                    continue;
                }

                float nx = (u - .5f) / .49f;
                float topRadius = new Vector2(nx, (v - .57f) / .365f).magnitude;
                Color colour = Color.clear;
                if (Mathf.Abs(nx) < 1f)
                {
                    float lowerTop = .57f - .365f * Mathf.Sqrt(1f - nx * nx);
                    float sideT = (v - (lowerTop - .14f)) / .14f;
                    if (sideT >= 0f && sideT <= 1f)
                    {
                        float sheen = Mathf.Pow(1f - Mathf.Abs(nx + .35f) / 1.35f, 4f);
                        colour = Color.Lerp(new Color(.24f, .015f, .03f), new Color(.82f, .13f, .18f), sheen);
                        if (sideT > .38f && sideT < .58f)
                            colour = Color.Lerp(new Color(.58f, .10f, .15f), new Color(.98f, .82f, .84f), sheen);
                        colour.a = Mathf.Clamp01(sideT * 30f);
                    }
                }
                if (topRadius <= 1f)
                {
                    float lighting = Mathf.Clamp01(.5f + v * .5f - u * .3f);
                    colour = topRadius > .9f
                        ? Color.Lerp(new Color(.38f, .035f, .06f), new Color(.94f, .28f, .32f), lighting)
                        : new Color(.16f, .025f, .04f);
                    colour.a = Mathf.Clamp01((1f - topRadius) * size / 2f);
                }
                pixels[y * size + x] = colour;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Sprite MakeSprite(Texture2D texture) => Sprite.Create(texture,
            new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), texture.width,
            0, SpriteMeshType.FullRect);

        private void OnDestroy()
        {
            Release(casingSprite); Release(faceSprite); Release(casingTexture); Release(faceTexture);
        }

        private static void Release(Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
