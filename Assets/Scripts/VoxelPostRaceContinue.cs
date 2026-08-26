using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Reveals the workshop transition only after a finished car has stopped.</summary>
    public sealed class VoxelPostRaceContinue : MonoBehaviour
    {
        public VoxelRunFinish runFinish;
        public string repairSceneName = "RepairUpgrade";
        public Button ContinueButton { get; private set; }

        private bool isLoading;

        public void Configure(VoxelRunFinish finish) => runFinish = finish;

        private void Update()
        {
            if (ContinueButton != null || runFinish == null || !runFinish.HasFinished ||
                runFinish.target == null || runFinish.target.CurrentSpeed > 0.05f ||
                !runFinish.FinishCameraComplete)
                return;

            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Post Race UI");
            ContinueButton = VoxelMenuUi.CreateButton(canvas, "Continue Button", "CONTINUE", 52,
                new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(640f, 124f), OpenWorkshop);
        }

        public void OpenWorkshop()
        {
            if (isLoading || runFinish == null || runFinish.target == null)
                return;

            int buildIndex = SceneUtility.GetBuildIndexByScenePath(
                "Assets/Scenes/" + repairSceneName + ".unity");
            if (buildIndex < 0)
            {
                Debug.LogError("Repair scene is not enabled in Build Settings: " + repairSceneName);
                return;
            }

            VoxelCarRunState.Capture(runFinish.target);
            isLoading = true;
            SceneManager.LoadScene(buildIndex);
        }
    }
}
