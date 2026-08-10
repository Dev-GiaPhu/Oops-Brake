using UnityEngine;

namespace EmergencyRoad
{
    public sealed class RoadPickup : MonoBehaviour
    {
        public void Collect() => gameObject.SetActive(false);
    }
}
