using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Routes Space to an explicitly chosen forward button in Editor Play Mode.</summary>
    internal static class VoxelEditorNavigation
    {
#if UNITY_EDITOR
        private static int advanceFrame = -1;
        private static KeyControl heldAdvanceKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            advanceFrame = -1;
            ClearHeldAdvanceKey();
        }

        private static void ReleaseAdvanceKey()
        {
            if (heldAdvanceKey == null || !heldAdvanceKey.device.added ||
                !heldAdvanceKey.isPressed || heldAdvanceKey.wasReleasedThisFrame) ClearHeldAdvanceKey();
        }

        private static void ClearHeldAdvanceKey()
        {
            InputSystem.onAfterUpdate -= ReleaseAdvanceKey;
            heldAdvanceKey = null;
        }
#endif

        internal static bool Activate(Button button)
        {
#if UNITY_EDITOR
            var space = Keyboard.current?.spaceKey;
            if (!Application.isPlaying || VoxelLoadingScreen.IsLoading || advanceFrame == Time.frameCount ||
                space?.wasPressedThisFrame != true || space == heldAdvanceKey || button == null ||
                !button.isActiveAndEnabled || !button.IsInteractable()) return false;

            // A button can reveal another screen before the same frame ends.
            advanceFrame = Time.frameCount;
            if (space.isPressed)
            {
                ClearHeldAdvanceKey();
                heldAdvanceKey = space;
                InputSystem.onAfterUpdate += ReleaseAdvanceKey;
            }
            button.onClick.Invoke();
            return true;
#else
            return false;
#endif
        }

        internal static bool IsSpaceBrakeHeld(Keyboard keyboard)
        {
            var space = keyboard?.spaceKey;
#if UNITY_EDITOR
            // Observe release even when the driving controller is gated or between scenes.
            if (advanceFrame == Time.frameCount || space == heldAdvanceKey) return false;
#endif
            return space?.isPressed == true;
        }
    }
}
