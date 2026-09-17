using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        [Tooltip("Rotation from dragging across the full screen width.")]
        [Min(1f)] public float garageRotationDegreesPerScreen = 360f;
        [Min(0f)] public float garageAutoRotationDegreesPerSecond = 12f;
        [Min(0f)] public float garageAutoRotationResumeDelay = 5f;
        [Header("Garage Zoom")]
        [Tooltip("Maximum field-of-view change from the camera tuning value in either direction.")]
        [Range(0f, 20f)] public float garageZoomFieldOfViewRange = 10f;
        [Tooltip("Field-of-view degrees changed by each mouse-wheel step.")]
        [Min(0f)] public float garageMouseWheelZoomStep = 1.25f;
        [Tooltip("Field-of-view degrees changed when the pinch distance changes by one screen height.")]
        [Min(0f)] public float garagePinchZoomDegreesPerScreen = 10f;
        private float resumeAutoRotationAt;
        private bool rotatingCar, rotationUsesTouch;
        private int rotationTouchId;
        private Vector2 previousRotationPointer;
        private bool pinching;
        private float previousPinchDistance;
        private float garageZoomAmount;
        private readonly List<RaycastResult> rotationUiHits = new();

        private void UpdateCarRotationInput()
        {
            if (!Application.isPlaying || DisplayedCar == null || workshopCamera == null || isLoading)
            {
                rotatingCar = false;
                pinching = false;
                return;
            }

            if (Mouse.current != null && !BlocksCarRotation(Mouse.current.position.ReadValue()))
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    ApplyGarageZoom(Mathf.Sign(scroll) * garageMouseWheelZoomStep);
            }

            if (TryGetTouchPair(out TouchControl firstTouch, out TouchControl secondTouch))
            {
                Vector2 firstPosition = firstTouch.position.ReadValue();
                Vector2 secondPosition = secondTouch.position.ReadValue();
                if (!pinching)
                {
                    if (BlocksCarRotation(firstPosition) || BlocksCarRotation(secondPosition))
                        return;

                    pinching = true;
                    rotatingCar = false;
                    previousPinchDistance = Vector2.Distance(firstPosition, secondPosition);
                    resumeAutoRotationAt = Time.unscaledTime + garageAutoRotationResumeDelay;
                }
                else
                {
                    float distance = Vector2.Distance(firstPosition, secondPosition);
                    float screenHeight = Mathf.Max(1f, Screen.height);
                    ApplyGarageZoom((distance - previousPinchDistance) / screenHeight * garagePinchZoomDegreesPerScreen);
                    previousPinchDistance = distance;
                }
                return;
            }

            if (pinching)
            {
                pinching = false;
                rotatingCar = false;
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

        private bool TryGetTouchPair(out TouchControl first, out TouchControl second)
        {
            first = null;
            second = null;
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
                return false;

            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                    continue;
                if (first == null)
                    first = touch;
                else
                {
                    second = touch;
                    return true;
                }
            }
            return false;
        }

        private void ApplyGarageZoom(float zoomAmount)
        {
            if (cameraTuning == null || workshopCamera == null)
                return;

            float range = Mathf.Max(0f, garageZoomFieldOfViewRange);
            garageZoomAmount = Mathf.Clamp(garageZoomAmount + zoomAmount, -range, range);
            workshopCamera.fieldOfView = Mathf.Clamp(
                cameraTuning.cameraFieldOfView - garageZoomAmount, 10f, 90f);
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
