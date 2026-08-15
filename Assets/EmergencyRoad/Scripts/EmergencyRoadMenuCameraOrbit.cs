using UnityEngine;
using UnityEngine.InputSystem;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class EmergencyRoadMenuCameraOrbit : MonoBehaviour
    {
        [Header("SCENE REFERENCES - DRAG DIRECTLY")]
        [SerializeField] private Transform vehicleCenter;

        [Header("MOUSE ORBIT")]
        [SerializeField, Range(.2f, 1f)] private float draggableScreenWidth = .75f;
        [SerializeField, Range(5f, 45f)] private float horizontalLimit = 24f;
        [SerializeField, Range(3f, 25f)] private float verticalLimit = 10f;
        [SerializeField, Min(.01f)] private float sensitivity = .11f;
        [SerializeField, Min(1f)] private float dragThresholdPixels = 6f;
        [SerializeField, Range(.02f, .5f)] private float dragSmoothTime = .12f;
        [SerializeField, Min(0f)] private float returnDelay = 2.5f;
        [SerializeField, Min(.05f)] private float returnSmoothTime = .65f;

        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float yaw;
        private float pitch;
        private float smoothYaw;
        private float smoothPitch;
        private float yawVelocity;
        private float pitchVelocity;
        private Vector3 dragStartOffset;
        private Vector3 dragStartRight;
        private Quaternion dragStartRotation;
        private Vector2 previousPointerPosition;
        private Vector2 pressedPointerPosition;
        private float releasedAt = float.NegativeInfinity;
        private bool pointerCaptured;
        private bool dragging;
        private bool returningHome;

        public void Configure(Transform target)
        {
            vehicleCenter = target;
        }

        private void Awake()
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        private void Update()
        {
            if (vehicleCenter == null || Mouse.current == null) return;
            Mouse mouse = Mouse.current;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressedPointerPosition = mouse.position.ReadValue();
                pointerCaptured = pressedPointerPosition.x <= Screen.width * draggableScreenWidth;
                dragging = false;
                return;
            }

            if (pointerCaptured && mouse.leftButton.isPressed)
            {
                Vector2 pointerPosition = mouse.position.ReadValue();
                if (!dragging)
                {
                    if ((pointerPosition - pressedPointerPosition).sqrMagnitude < dragThresholdPixels * dragThresholdPixels)
                        return;

                    dragging = true;
                    dragStartOffset = transform.position - vehicleCenter.position;
                    dragStartRight = transform.right;
                    dragStartRotation = transform.rotation;
                    previousPointerPosition = pointerPosition;
                    yaw = 0f;
                    pitch = 0f;
                    smoothYaw = 0f;
                    smoothPitch = 0f;
                    yawVelocity = 0f;
                    pitchVelocity = 0f;
                    return;
                }

                Vector2 delta = pointerPosition - previousPointerPosition;
                previousPointerPosition = pointerPosition;
                yaw = Mathf.Clamp(yaw + delta.x * sensitivity, -horizontalLimit, horizontalLimit);
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -verticalLimit, verticalLimit);
                ApplyOrbit();
                return;
            }

            if (pointerCaptured && mouse.leftButton.wasReleasedThisFrame)
            {
                bool completedDrag = dragging;
                pointerCaptured = false;
                dragging = false;
                if (completedDrag)
                {
                    releasedAt = Time.unscaledTime;
                    returningHome = true;
                }
                else
                {
                    return;
                }
            }

            if (!returningHome) return;
            if (Time.unscaledTime - releasedAt < returnDelay) return;
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / returnSmoothTime * 4f);
            yaw = Mathf.Lerp(yaw, 0f, blend);
            pitch = Mathf.Lerp(pitch, 0f, blend);
            transform.position = Vector3.Lerp(transform.position, homePosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, homeRotation, blend);
            if ((transform.position - homePosition).sqrMagnitude < .000001f &&
                Quaternion.Angle(transform.rotation, homeRotation) < .01f)
            {
                transform.SetPositionAndRotation(homePosition, homeRotation);
                returningHome = false;
            }
        }

        private void ApplyOrbit()
        {
            smoothYaw = Mathf.SmoothDamp(smoothYaw, yaw, ref yawVelocity, dragSmoothTime,
                Mathf.Infinity, Time.unscaledDeltaTime);
            smoothPitch = Mathf.SmoothDamp(smoothPitch, pitch, ref pitchVelocity, dragSmoothTime,
                Mathf.Infinity, Time.unscaledDeltaTime);
            Quaternion orbit = Quaternion.AngleAxis(smoothYaw, Vector3.up) *
                               Quaternion.AngleAxis(smoothPitch, dragStartRight);
            transform.position = vehicleCenter.position + orbit * dragStartOffset;
            transform.rotation = orbit * dragStartRotation;
        }
    }
}
