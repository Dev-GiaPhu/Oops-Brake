using UnityEngine;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class DayNightCycle : MonoBehaviour
    {
        private enum CyclePhase { Day, Sunset, Night, Sunrise }

        [Header("THOI GIAN")]
        [SerializeField, Min(0f)] private float dayDuration = 30f;
        [SerializeField, Min(0f)] private float nightDuration = 30f;
        [SerializeField, Min(.1f)] private float transitionDuration = 3f;

        [Header("ANH SANG")]
        [SerializeField, Min(0f)] private float dayIntensity = 1f;
        [SerializeField, Min(0f)] private float nightIntensity;
        [SerializeField, Min(1f)] private float halfCycleRotation = 180f;

        private Light sun;
        private CyclePhase phase;
        private float phaseTime;
        private float currentX;
        private float transitionStartX;
        private float fixedY;
        private float fixedZ;

        private void Awake()
        {
            sun = GetComponent<Light>();
            Vector3 angles = transform.localEulerAngles;
            currentX = angles.x;
            fixedY = angles.y;
            fixedZ = angles.z;
            phase = CyclePhase.Day;
            sun.intensity = dayIntensity;
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            phaseTime += delta;

            switch (phase)
            {
                case CyclePhase.Day:
                    if (phaseTime >= dayDuration) Begin(CyclePhase.Sunset);
                    break;
                case CyclePhase.Sunset:
                    Transition(dayIntensity, nightIntensity, CyclePhase.Night);
                    break;
                case CyclePhase.Night:
                    if (phaseTime >= nightDuration) Begin(CyclePhase.Sunrise);
                    break;
                case CyclePhase.Sunrise:
                    Transition(nightIntensity, dayIntensity, CyclePhase.Day);
                    break;
            }
        }

        private void Transition(float fromIntensity, float toIntensity, CyclePhase next)
        {
            float progress = Mathf.Clamp01(phaseTime / transitionDuration);
            currentX = transitionStartX + halfCycleRotation * progress;
            transform.localRotation = Quaternion.Euler(currentX, fixedY, fixedZ);
            sun.intensity = Mathf.Lerp(fromIntensity, toIntensity, progress);
            if (phaseTime < transitionDuration) return;

            currentX = transitionStartX + halfCycleRotation;
            transform.localRotation = Quaternion.Euler(currentX, fixedY, fixedZ);
            sun.intensity = toIntensity;
            Begin(next);
        }

        private void Begin(CyclePhase next)
        {
            phase = next;
            phaseTime = 0f;
            if (next == CyclePhase.Sunset || next == CyclePhase.Sunrise)
                transitionStartX = currentX;
        }
    }
}
