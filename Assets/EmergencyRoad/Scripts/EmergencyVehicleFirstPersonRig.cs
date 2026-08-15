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
        [SerializeField, Range(45f, 90f)] private float firstPersonFieldOfView = 64f;

        [Header("STEERING WHEEL")]
        [SerializeField] private Vector3 steeringLocalAxis = Vector3.up;
        [SerializeField, Range(15f, 240f)] private float maximumSteeringAngle = 105f;
        [SerializeField, Min(1f)] private float steeringSharpness = 11f;
        [SerializeField, Min(1f)] private float returnSharpness = 8f;

        private Quaternion steeringBaseRotation;
        private Transform stableVehicleRoot;
        private Vector3 stableCameraLocalPosition;
        private Quaternion stableCameraLocalRotation;
        private Quaternion visualBaseRotationRelativeToRoot;
        private float targetSteering;
        private float currentSteering;

        public bool HasCameraPivot => cameraPivot != null;
        public Transform CameraPivot => cameraPivot;
        public Vector3 StableCameraPosition => stableVehicleRoot != null
            ? stableVehicleRoot.TransformPoint(CurrentVisualRotationDelta * stableCameraLocalPosition)
            : cameraPivot.position;
        public Quaternion StableCameraRotation => stableVehicleRoot != null
            ? stableVehicleRoot.rotation * CurrentVisualRotationDelta * stableCameraLocalRotation
            : cameraPivot.rotation;
        public float FirstPersonFieldOfView => firstPersonFieldOfView;
        public Vector3 LeftWindowEntryPosition => cameraPivot != null
            ? StableCameraPosition + StableCameraRotation * leftWindowEntryOffset
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
            stableCameraLocalPosition = vehicleRoot.InverseTransformPoint(cameraPivot.position);
            stableCameraLocalRotation = Quaternion.Inverse(vehicleRoot.rotation) * cameraPivot.rotation;
            visualBaseRotationRelativeToRoot = Quaternion.Inverse(vehicleRoot.rotation) * transform.rotation;
        }

        private Quaternion CurrentVisualRotationDelta
        {
            get
            {
                Quaternion currentRelative = Quaternion.Inverse(stableVehicleRoot.rotation) * transform.rotation;
                return currentRelative * Quaternion.Inverse(visualBaseRotationRelativeToRoot);
            }
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
