using UnityEngine;

namespace EmergencyRoad
{
    public sealed class RoadRouteMarker : MonoBehaviour
    {
        public int SoleOpenLane { get; private set; }
        public void Configure(int lane) => SoleOpenLane = Mathf.Clamp(lane, -1, 1);
    }
}
