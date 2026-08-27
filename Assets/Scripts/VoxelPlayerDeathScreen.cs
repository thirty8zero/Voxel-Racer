using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Shows the retry flow after the player's car has been destroyed.</summary>
    public sealed class VoxelPlayerDeathScreen : MonoBehaviour
    {
        public static bool IsShowing { get; private set; }

        public VoxelCarController target;
        public string carSelectSceneName = "CarSelect";

        private bool isLoading;
        private bool isShown;

        public void Configure(VoxelCarController player) => target = player;

        private void OnDisable()
        {
            if (IsShowing)
                IsShowing = false;
        }

        private void Update()
        {
            if (!isShown && target != null && target.IsDestroyed)
                Show();
        }

        private void Show()
        {
            isShown = true;
            IsShowing = true;
            VoxelStartCountdown.Active?.HideForPlayerDeath();
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Mission Failed UI");
            canvas.GetComponent<Canvas>().sortingOrder = 1000;
            VoxelMenuUi.CreatePanel(canvas, "Failure Backdrop", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1280f, 520f));

            Text title = VoxelMenuUi.CreateText(canvas, "Mission Failed Title", "MISSION FAILED", 180,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 62f), new Vector2(1600f, 230f));
            Font brokenGlass = Resources.Load<Font>("Fonts/BrokenGlass");
            if (brokenGlass != null)
                title.font = brokenGlass;
            title.fontStyle = FontStyle.Normal;
            title.color = new Color(0.96f, 0.16f, 0.08f);

            VoxelMenuUi.CreateButton(canvas, "Retry Button", "RETRY", 78,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -145f), new Vector2(520f, 120f), Retry);
        }

        private void Retry()
        {
            if (isLoading)
                return;

            int buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + carSelectSceneName + ".unity");
            if (buildIndex < 0)
            {
                Debug.LogError("Car Select scene is not enabled in Build Settings: " + carSelectSceneName);
                return;
            }

            VoxelCarRunState.BeginNewRun(VoxelCarSelectionState.GetSelectedOrDefault());
            isLoading = true;
            SceneManager.LoadScene(buildIndex);
        }
    }
}
