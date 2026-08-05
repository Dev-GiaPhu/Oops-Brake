using UnityEngine;

namespace EmergencyRoad
{
    /// <summary>Scene-owned entry point for gameplay tuning. The referenced asset is edited inline by its custom Inspector.</summary>
    [DisallowMultipleComponent]
    public sealed class EmergencyRoadGameplaySettingsComponent : MonoBehaviour
    {
        [SerializeField] private EmergencyRoadGameplaySettings settings;

        public EmergencyRoadGameplaySettings Settings => settings;

        public void Assign(EmergencyRoadGameplaySettings value)
        {
            settings = value;
        }
    }
}
