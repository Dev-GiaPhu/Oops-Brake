using UnityEngine;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class DayNightLight : MonoBehaviour
    {
        [SerializeField] private bool invert;

        private Light controlledLight;

        private void Awake()
        {
            controlledLight = GetComponent<Light>();
        }

        private void OnEnable()
        {
            DayNightCycle.NightStateChanged += ApplyNightState;
            ApplyNightState(DayNightCycle.HasActiveCycle && DayNightCycle.IsNight);
        }

        private void OnDisable()
        {
            DayNightCycle.NightStateChanged -= ApplyNightState;
        }

        private void ApplyNightState(bool isNight)
        {
            controlledLight.enabled = invert ? !isNight : isNight;
        }
    }
}
