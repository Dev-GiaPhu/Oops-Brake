using UnityEngine;

namespace EmergencyRoad
{
    public sealed class EmergencyVehicleFirstPersonRig : MonoBehaviour
    {
        [Header("FIRST PERSON - DRAG FROM THIS VEHICLE PREFAB")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform steeringWheel;

        [Header("CAMERA ENTRY THROUGH LEFT WINDOW")]
        [SerializeField] private Vector3 leftWindowEntryOffset = new(-1.15f, .08f, -.35f);
        [SerializeField, Min(0f)] private float entryCurveSideClearance = .9f;
        [SerializeField, Range(45f, 90f)] private float firstPersonFieldOfView = 64f;

        [Header("STEERING WHEEL")]
        [SerializeField] private Vector3 steeringLocalAxis = Vector3.up;
        [SerializeField, Range(15f, 240f)] private float maximumSteeringAngle = 105f;
        [SerializeField, Min(1f)] private float steeringSharpness = 11f;
        [SerializeField, Min(1f)] private float returnSharpness = 8f;

        private Quaternion steeringBaseRotation;
        private Transform stableVehicleRoot;
        private Vector3 stableVisualOriginLocalPosition;
        private Vector3 cameraOffsetFromVisualOrigin;
        private Quaternion cameraRotationRelativeToVisual;
        private float targetSteering;
        private float currentSteering;

        public bool HasCameraPivot => cameraPivot != null;
        public Transform CameraPivot => cameraPivot;
        public Vector3 StableCameraPosition => stableVehicleRoot != null
            ? stableVehicleRoot.TransformPoint(stableVisualOriginLocalPosition) + transform.rotation * cameraOffsetFromVisualOrigin
            : cameraPivot.position;
        public Quaternion StableCameraRotation => stableVehicleRoot != null
            ? transform.rotation * cameraRotationRelativeToVisual
            : cameraPivot.rotation;
        public float FirstPersonFieldOfView => firstPersonFieldOfView;
        public Vector3 LeftWindowEntryPosition => cameraPivot != null
            ? StableCameraPosition + StableCameraRotation * leftWindowEntryOffset
            : transform.position;
        public Vector3 OuterWindowEntryPosition => cameraPivot != null
            ? LeftWindowEntryPosition + StableCameraRotation * (Vector3.left * entryCurveSideClearance)
            : transform.position;

        private void Awake()
        {
            if (steeringWheel != null) steeringBaseRotation = steeringWheel.localRotation;
        }

        public void SetSteering(float normalized)
        {
            targetSteering = Mathf.Clamp(normalized, -1f, 1f);
        }

        public void BindStableCameraReference(Transform vehicleRoot)
        {
            if (cameraPivot == null || vehicleRoot == null) return;
            stableVehicleRoot = vehicleRoot;
            stableVisualOriginLocalPosition = vehicleRoot.InverseTransformPoint(transform.position);
            cameraOffsetFromVisualOrigin = Quaternion.Inverse(transform.rotation) * (cameraPivot.position - transform.position);
            cameraRotationRelativeToVisual = Quaternion.Inverse(transform.rotation) * cameraPivot.rotation;
        }

        private void LateUpdate()
        {
            if (steeringWheel == null) return;
            float sharpness = Mathf.Abs(targetSteering) > .01f ? steeringSharpness : returnSharpness;
            currentSteering = Mathf.Lerp(currentSteering, targetSteering, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
            Vector3 axis = steeringLocalAxis.sqrMagnitude > .001f ? steeringLocalAxis.normalized : Vector3.up;
            steeringWheel.localRotation = steeringBaseRotation * Quaternion.AngleAxis(currentSteering * maximumSteeringAngle, axis);
        }

        private void OnDisable()
        {
            targetSteering = 0f;
            currentSteering = 0f;
            if (steeringWheel != null) steeringWheel.localRotation = steeringBaseRotation;
        }
    }
}
