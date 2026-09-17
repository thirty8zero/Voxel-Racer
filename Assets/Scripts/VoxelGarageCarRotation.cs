using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        [Tooltip("Rotation from dragging across the full screen width.")]
        [Min(1f)] public float garageRotationDegreesPerScreen = 360f;
        [Min(0f)] public float garageAutoRotationDegreesPerSecond = 12f;
        [Min(0f)] public float garageAutoRotationResumeDelay = 5f;
        private float resumeAutoRotationAt;
        private bool rotatingCar, rotationUsesTouch;
        private int rotationTouchId;
        private Vector2 previousRotationPointer;
        private readonly List<RaycastResult> rotationUiHits = new();

        private void UpdateCarRotationInput()
        {
            if (!Application.isPlaying || DisplayedCar == null || workshopCamera == null || isLoading)
            {
                rotatingCar = false;
                return;
            }
            var touch = Touchscreen.current?.primaryTouch;
            if (!rotatingCar && Time.unscaledTime >= resumeAutoRotationAt)
                DisplayedCar.transform.Rotate(Vector3.up, garageAutoRotationDegreesPerSecond * Time.unscaledDeltaTime, Space.World);
            if (rotatingCar)
            {
                resumeAutoRotationAt = Time.unscaledTime + garageAutoRotationResumeDelay;
                bool held = rotationUsesTouch
                    ? touch != null && touch.press.isPressed && touch.touchId.ReadValue() == rotationTouchId
                    : Mouse.current != null && Mouse.current.leftButton.isPressed;
                if (!held) { rotatingCar = false; return; }
                Vector2 position = rotationUsesTouch ? touch.position.ReadValue() : Mouse.current.position.ReadValue();
                if (BlocksCarRotation(position)) { rotatingCar = false; return; }
                float delta = position.x - previousRotationPointer.x;
                DisplayedCar.transform.Rotate(Vector3.up, -delta / Mathf.Max(1, Screen.width) * garageRotationDegreesPerScreen, Space.World);
                previousRotationPointer = position;
                return;
            }
            bool touchDown = touch != null && touch.press.wasPressedThisFrame;
            bool mouseDown = (touch == null || !touch.press.isPressed) && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (!touchDown && !mouseDown) return;
            Vector2 start = touchDown ? touch.position.ReadValue() : Mouse.current.position.ReadValue();
            if (BlocksCarRotation(start) || !PointerOverCar(start)) return;
            rotationUsesTouch = touchDown;
            rotationTouchId = touchDown ? touch.touchId.ReadValue() : 0;
            previousRotationPointer = start;
            rotatingCar = true;
        }

        private bool PointerOverCar(Vector2 position)
        {
            Ray ray = workshopCamera.ScreenPointToRay(position);
            foreach (var renderer in DisplayedCar.GetComponentsInChildren<Renderer>())
                if (renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.bounds.IntersectRay(ray)) return true;
            return false;
        }

        private bool BlocksCarRotation(Vector2 position)
        {
            foreach (var panel in new[] { garageRepairPanel, garageUpgradePanel, garageSettingsPanel })
                if (panel != null && panel.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint((RectTransform)panel.transform, position)) return true;
            var events = EventSystem.current;
            if (events == null) return false;
            rotationUiHits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = position }, rotationUiHits);
            foreach (var hit in rotationUiHits)
                if (hit.gameObject.GetComponentInParent<Selectable>() != null || hit.gameObject.GetComponentInParent<ScrollRect>() != null) return true;
            return false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) rotatingCar = false;
        }
    }
}
