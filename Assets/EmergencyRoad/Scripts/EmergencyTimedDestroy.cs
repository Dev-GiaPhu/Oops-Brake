using UnityEngine;

namespace EmergencyRoad
{
    public sealed class EmergencyTimedDestroy : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float lifetime = 1.5f;
        private void OnEnable() => Destroy(gameObject, lifetime);
    }
}
