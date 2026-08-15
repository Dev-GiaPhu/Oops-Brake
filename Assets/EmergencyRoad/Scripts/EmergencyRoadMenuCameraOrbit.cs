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
        [SerializeField, Min(0f)] private float returnDelay = 2.5f;
        [SerializeField, Min(.05f)] private float returnSmoothTime = .65f;

        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float yaw;
        private float pitch;
        private Vector3 dragStartOffset;
        private Vector3 dragStartRight;
        private Quaternion dragStartRotation;
        private Vector2 previousPointerPosition;
        private float releasedAt = float.NegativeInfinity;
        private bool dragging;

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
                dragging = mouse.position.ReadValue().x <= Screen.width * draggableScreenWidth;
                if (dragging)
                {
                    dragStartOffset = transform.position - vehicleCenter.position;
                    dragStartRight = transform.right;
                    dragStartRotation = transform.rotation;
                    previousPointerPosition = mouse.position.ReadValue();
                    yaw = 0f;
                    pitch = 0f;
                    return;
                }
            }

            if (dragging && mouse.leftButton.isPressed)
            {
                Vector2 pointerPosition = mouse.position.ReadValue();
                Vector2 delta = pointerPosition - previousPointerPosition;
                previousPointerPosition = pointerPosition;
                yaw = Mathf.Clamp(yaw + delta.x * sensitivity, -horizontalLimit, horizontalLimit);
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -verticalLimit, verticalLimit);
                ApplyOrbit();
                return;
            }

            if (dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
                releasedAt = Time.unscaledTime;
            }

            if (Time.unscaledTime - releasedAt < returnDelay) return;
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / returnSmoothTime * 4f);
            yaw = Mathf.Lerp(yaw, 0f, blend);
            pitch = Mathf.Lerp(pitch, 0f, blend);
            transform.position = Vector3.Lerp(transform.position, homePosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, homeRotation, blend);
        }

        private void ApplyOrbit()
        {
            Quaternion orbit = Quaternion.AngleAxis(yaw, Vector3.up) *
                               Quaternion.AngleAxis(pitch, dragStartRight);
            transform.position = vehicleCenter.position + orbit * dragStartOffset;
            transform.rotation = orbit * dragStartRotation;
        }
    }
}
