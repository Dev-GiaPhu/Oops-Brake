using UnityEngine;
using UnityEngine.InputSystem;

namespace EmergencyRoad
{
    public sealed class EmergencyCameraJuice : MonoBehaviour
    {
        [Header("SCENE REFERENCES - DRAG DIRECTLY")]
        [SerializeField] private Camera controlledCamera;
        [Header("VIEW SWITCH")]
        [SerializeField] private Key switchViewKey = Key.C;
        [SerializeField, Min(.15f)] private float transitionDuration = .45f;

        private Transform target;
        private EmergencyVehicleFirstPersonRig vehicleRig;
        private EmergencyFirstPersonMirrorView mirrorView;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float shakeTime;
        private float shakeStrength;
        private bool crashView;
        private Vector3 crashOffset;
        private bool firstPerson;
        private bool transitioning;
        private bool transitionToFirstPerson;
        private float transitionTime;
        private Vector3 transitionStartPosition;
        private Quaternion transitionStartRotation;
        private float transitionStartFov;

        public bool CanSwitchView => vehicleRig != null && vehicleRig.HasCameraPivot;
        public bool IsFirstPerson => firstPerson;

        public void ConfigureCamera(Camera camera) => controlledCamera = camera;

        public void Initialize(Transform value) => Initialize(value, null);

        public void Initialize(Transform value, EmergencyVehicleFirstPersonRig rig) => Initialize(value, rig, null);

        public void Initialize(Transform value, EmergencyVehicleFirstPersonRig rig, EmergencyFirstPersonMirrorView mirrors)
        {
            target = value;
            vehicleRig = rig;
            mirrorView = mirrors;
            basePosition = transform.position;
            baseRotation = transform.rotation;
            firstPerson = EmergencyRoadProfile.Current.firstPersonView && CanSwitchView;
            transitioning = false;
            mirrorView?.SetVisible(firstPerson);
            if (controlledCamera == null)
                Debug.LogError("[Emergency Road] EmergencyCameraJuice thiếu Camera reference trong Inspector.", this);
        }

        public void Shake(float duration, float strength)
        {
            shakeTime = duration;
            shakeStrength = strength;
        }

        public void EnterCrashView(Vector3 impactDirection)
        {
            crashView = true;
            firstPerson = false;
            transitioning = false;
            mirrorView?.SetVisible(false);
            float side = impactDirection.x >= 0f ? -1f : 1f;
            crashOffset = new Vector3(side * 6.8f, 4.4f, -6.8f);
            Shake(.48f, .28f);
        }

        private void Update()
        {
            if (crashView || !CanSwitchView || Keyboard.current == null) return;
            if (Keyboard.current[switchViewKey].wasPressedThisFrame) BeginViewTransition(!firstPerson);
        }

        private void BeginViewTransition(bool toFirstPerson)
        {
            transitionToFirstPerson = toFirstPerson;
            transitioning = true;
            transitionTime = 0f;
            transitionStartPosition = transform.position;
            transitionStartRotation = transform.rotation;
            transitionStartFov = controlledCamera != null ? controlledCamera.fieldOfView : 58f;
            EmergencyRoadProfile.Current.firstPersonView = toFirstPerson;
            EmergencyRoadProfile.Save();
            mirrorView?.SetVisible(toFirstPerson);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            if (crashView)
            {
                UpdateCrashView(dt);
                return;
            }
            if (transitioning && CanSwitchView)
            {
                UpdateViewTransition(dt);
                return;
            }
            if (firstPerson && CanSwitchView)
            {
                transform.SetPositionAndRotation(vehicleRig.StableCameraPosition, vehicleRig.StableCameraRotation);
                if (controlledCamera != null)
                    controlledCamera.fieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, vehicleRig.FirstPersonFieldOfView, 1f - Mathf.Exp(-10f * dt));
                return;
            }
            UpdateThirdPerson(dt);
        }

        private void UpdateViewTransition(float dt)
        {
            transitionTime += dt;
            float t = Mathf.Clamp01(transitionTime / Mathf.Max(.15f, transitionDuration));
            float smooth = t * t * (3f - 2f * t);
            Vector3 thirdTarget = ThirdPersonTargetPosition;
            Vector3 destination = transitionToFirstPerson ? vehicleRig.StableCameraPosition : thirdTarget;
            Quaternion destinationRotation = transitionToFirstPerson ? vehicleRig.StableCameraRotation : baseRotation;
            Vector3 window = vehicleRig.LeftWindowEntryPosition;
            Vector3 controlA = Vector3.Lerp(transitionStartPosition, window, .58f);
            Vector3 controlB = window;
            transform.position = CubicBezier(transitionStartPosition, controlA, controlB, destination, smooth);
            transform.rotation = Quaternion.Slerp(transitionStartRotation, destinationRotation, smooth);
            if (controlledCamera != null)
            {
                float targetFov = transitionToFirstPerson ? vehicleRig.FirstPersonFieldOfView : ThirdPersonFieldOfView;
                controlledCamera.fieldOfView = Mathf.Lerp(transitionStartFov, targetFov, smooth);
            }
            if (t < 1f) return;
            firstPerson = transitionToFirstPerson;
            transitioning = false;
        }

        private void UpdateThirdPerson(float dt)
        {
            Vector3 follow = ThirdPersonTargetPosition;
            if (shakeTime > 0)
            {
                shakeTime -= dt;
                follow += (Vector3)Random.insideUnitCircle * shakeStrength * (shakeTime / .42f);
            }
            transform.position = Vector3.Lerp(transform.position, follow, 1f - Mathf.Exp(-5f * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, baseRotation, 1f - Mathf.Exp(-7f * dt));
            if (controlledCamera != null)
                controlledCamera.fieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, ThirdPersonFieldOfView, 1f - Mathf.Exp(-3f * dt));
        }

        private void UpdateCrashView(float dt)
        {
            Vector3 desired = target.position + crashOffset;
            if (shakeTime > 0)
            {
                shakeTime -= dt;
                desired += (Vector3)Random.insideUnitCircle * shakeStrength * Mathf.Clamp01(shakeTime / .48f);
            }
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-3.5f * dt));
            Vector3 aim = target.position + Vector3.left * 1.65f + Vector3.up * .75f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aim - transform.position, Vector3.up), 1f - Mathf.Exp(-4f * dt));
            if (controlledCamera != null) controlledCamera.fieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, 49f, 1f - Mathf.Exp(-3f * dt));
        }

        private Vector3 ThirdPersonTargetPosition => basePosition + Vector3.right * (target.position.x * .16f);
        private float ThirdPersonFieldOfView => 58f + Mathf.Abs(target.position.x) * .22f;

        private static Vector3 CubicBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
    }
}
