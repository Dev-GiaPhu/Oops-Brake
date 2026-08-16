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
                    previousPointerPosition = pointerPosition;
                    // Continue from the current absolute orbit. Limits must remain
                    // relative to the authored home camera across repeated drags.
                    yaw = smoothYaw;
                    pitch = smoothPitch;
                    yawVelocity = 0f;
                    pitchVelocity = 0f;
                    returningHome = false;
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
            yaw = 0f;
            pitch = 0f;
            ApplyOrbit(returnSmoothTime);
            if (Mathf.Abs(smoothYaw) < .01f && Mathf.Abs(smoothPitch) < .01f)
            {
                smoothYaw = 0f;
                smoothPitch = 0f;
                transform.SetPositionAndRotation(homePosition, homeRotation);
                returningHome = false;
            }
        }

        private void ApplyOrbit() => ApplyOrbit(dragSmoothTime);

        private void ApplyOrbit(float smoothTime)
        {
            smoothYaw = Mathf.SmoothDamp(smoothYaw, yaw, ref yawVelocity, smoothTime,
                Mathf.Infinity, Time.unscaledDeltaTime);
            smoothPitch = Mathf.SmoothDamp(smoothPitch, pitch, ref pitchVelocity, smoothTime,
                Mathf.Infinity, Time.unscaledDeltaTime);
            Vector3 homeOffset = homePosition - vehicleCenter.position;
            Vector3 homeRight = homeRotation * Vector3.right;
            Quaternion orbit = Quaternion.AngleAxis(smoothYaw, Vector3.up) *
                               Quaternion.AngleAxis(smoothPitch, homeRight);
            transform.position = vehicleCenter.position + orbit * homeOffset;
            transform.rotation = orbit * homeRotation;
        }
    }
}
