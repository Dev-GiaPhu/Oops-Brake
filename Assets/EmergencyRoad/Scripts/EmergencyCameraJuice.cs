using UnityEngine;

namespace EmergencyRoad
{
    public sealed class EmergencyCameraJuice : MonoBehaviour
    {
        [Header("SCENE REFERENCES - DRAG DIRECTLY")]
        [SerializeField] private Camera controlledCamera;

        private Transform target;
        private Vector3 basePosition;
        private float shakeTime;
        private float shakeStrength;
        private bool crashView;
        private Vector3 crashOffset;

        public void ConfigureCamera(Camera camera) => controlledCamera = camera;

        public void Initialize(Transform value)
        {
            target = value;
            basePosition = transform.position;
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
            float side = impactDirection.x >= 0f ? -1f : 1f;
            crashOffset = new Vector3(side * 6.8f, 4.4f, -6.8f);
            Shake(.48f, .28f);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime;
            if (crashView)
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
                return;
            }

            float targetX = target.position.x * .16f;
            Vector3 follow = basePosition + Vector3.right * targetX;
            if (shakeTime > 0)
            {
                shakeTime -= dt;
                follow += (Vector3)Random.insideUnitCircle * shakeStrength * (shakeTime / .42f);
            }
            transform.position = Vector3.Lerp(transform.position, follow, 1f - Mathf.Exp(-5f * dt));
            if (controlledCamera != null) controlledCamera.fieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, 58f + Mathf.Abs(target.position.x) * .22f, 1f - Mathf.Exp(-3f * dt));
        }
    }
}
