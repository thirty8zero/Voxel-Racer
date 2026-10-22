using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Persistent loading presentation shared by startup and every game scene transition.</summary>
    public sealed class VoxelLoadingScreen : MonoBehaviour
    {
        public static VoxelLoadingScreen Active { get; private set; }
        public static bool IsLoading => Active != null && Active.shown;
        private static readonly System.Random ImageRandom = new System.Random();
        private RectTransform canvas, imageArea, bottomBar, safeFooter;
        private RawImage artwork;
        private Text loadingLabel;
        private VoxelLoadingScreenSettings settings;
        private Texture2D previousImage;
        private bool shown;
        private double shownAt;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ShowStartup()
        {
            var screen = EnsureExists();
            screen.ShowRandomImage();
            SceneManager.sceneLoaded -= screen.InitialSceneLoaded;
            SceneManager.sceneLoaded += screen.InitialSceneLoaded;
        }

        private static VoxelLoadingScreen EnsureExists()
        {
            if (Active == null) new GameObject("Loading Screen").AddComponent<VoxelLoadingScreen>();
            return Active;
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            if (Active != null && Active != this) { Destroy(gameObject); return; }
            Active = this;
            DontDestroyOnLoad(gameObject);
        }

        private void InitialSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= InitialSceneLoaded;
            StartCoroutine(FinishWhenReady());
        }

        public static bool LoadScene(int buildIndex)
        {
            if (IsLoading) return false;
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError("Cannot load a scene outside Build Settings: " + buildIndex);
                return false;
            }
            EnsureExists().StartCoroutine(LoadRoutine(buildIndex));
            return true;
        }

        private static IEnumerator LoadRoutine(int buildIndex)
        {
            var screen = Active;
            screen.ShowRandomImage();
            // Stop the departing scene before asynchronous loading can let it award points,
            // damage the car or read input after its run state has already been captured/reset.
            // Existing OnDisable cleanup releases pause/death/mission clock ownership normally.
            var departingRoots = new List<GameObject>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
                foreach (var root in SceneManager.GetSceneAt(index).GetRootGameObjects())
                    if (root != null && root != screen.gameObject && root.activeSelf) { departingRoots.Add(root); root.SetActive(false); }
            // Present the overlay before starting work that can stall the main thread.
            yield return null;
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(buildIndex); }
            catch (System.Exception error) { Debug.LogException(error); }
            if (operation == null)
            {
                foreach (var root in departingRoots) if (root != null) root.SetActive(true);
                screen.Hide(); yield break;
            }
            while (!operation.isDone) yield return null;
            yield return screen.FinishWhenReady();
        }

        private IEnumerator FinishWhenReady()
        {
            // Cover scene Awake/OnEnable/bootstrap and the first Start/Update/layout pass.
            yield return null;
            yield return null;
            float minimum = settings != null ? settings.minimumDisplaySeconds : .65f;
            while (Time.realtimeSinceStartupAsDouble - shownAt < minimum) yield return null;
            Hide();
        }

        private void ShowRandomImage()
        {
            settings = VoxelLoadingScreenSettings.Load();
            var image = settings != null ? settings.ChooseImage(ImageRandom, previousImage) : null;
            previousImage = image;
            Show(settings, image);
        }

        /// <summary>Builds the same presentation for runtime and editor previews, without changing settings.</summary>
        public void Show(VoxelLoadingScreenSettings tuning, Texture2D image)
        {
            settings = tuning;
            if (tuning != null && !tuning.showImages) image = null;
            BuildView();
            shown = true;
            shownAt = Time.realtimeSinceStartupAsDouble;
            canvas.gameObject.SetActive(true);
            float top = tuning != null ? tuning.topBarFraction : .10f;
            float bottom = tuning != null ? tuning.bottomBarFraction : .18f;
            imageArea.anchorMin = new Vector2(0f, bottom);
            imageArea.anchorMax = new Vector2(1f, 1f - top);
            bottomBar.anchorMax = new Vector2(1f, bottom);
            artwork.texture = image;
            artwork.gameObject.SetActive(image != null);
            if (image != null) artwork.GetComponent<AspectRatioFitter>().aspectRatio = image.width / (float)image.height;
            loadingLabel.fontSize = tuning != null ? tuning.loadingFontSize : 88;
            ApplySafeArea();
            AnimateLabel(0f);
            Canvas.ForceUpdateCanvases();
        }

        private void Hide()
        {
            shown = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private void BuildView()
        {
            if (canvas != null) return;
            var canvasObject = new GameObject("Loading Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = (RectTransform)canvasObject.transform;
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f; scaler.enabled = false; scaler.enabled = true;
            var backdrop = new GameObject("Opaque black background", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvas, false);
            Stretch((RectTransform)backdrop.transform);
            backdrop.GetComponent<Image>().color = Color.black;
            imageArea = Rect("Loading image area", canvas);
            var imageObject = new GameObject("Random loading image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            imageObject.transform.SetParent(imageArea, false);
            artwork = imageObject.GetComponent<RawImage>(); artwork.raycastTarget = false;
            imageObject.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            bottomBar = Rect("Bottom black bar", canvas);
            safeFooter = Rect("Safe footer", bottomBar);
            loadingLabel = VoxelMenuUi.CreateText(safeFooter, "Jiggling LOADING", "LOADING", 88,
                TextAnchor.MiddleCenter, new Vector2(.5f, .5f), Vector2.zero, new Vector2(600f, 140f));
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform)); item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform; Stretch(rect); return rect;
        }

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private void Update()
        {
            if (!shown) return;
            if (Screen.safeArea != appliedSafeArea || appliedScreenSize != new Vector2Int(Screen.width, Screen.height)) ApplySafeArea();
            AnimateLabel((float)(Time.realtimeSinceStartupAsDouble - shownAt));
        }

        public void AnimateLabel(float unscaledSeconds)
        {
            if (loadingLabel == null) return;
            loadingLabel.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(unscaledSeconds * 24f) * 3f, Mathf.Sin(unscaledSeconds * 31f) * 2f);
            loadingLabel.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(unscaledSeconds * 19f) * 1.8f);
        }

        private void ApplySafeArea()
        {
            appliedSafeArea = Screen.safeArea;
            appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
            float scale = Mathf.Max(.01f, canvas.GetComponent<Canvas>().scaleFactor);
            safeFooter.offsetMin = new Vector2(Screen.safeArea.xMin / scale, Screen.safeArea.yMin / scale);
            safeFooter.offsetMax = new Vector2(-(Screen.width - Screen.safeArea.xMax) / scale, 0f);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= InitialSceneLoaded;
            if (Active == this) Active = null;
        }
    }
}
