using UnityEngine;

namespace EmergencyRoad
{
    public sealed class SideCrossingHazard : MonoBehaviour
    {
        private float targetX;
        private bool moving;
        private float moveSpeed = 11.5f;
        private float startDistance = 44f;

        internal int TargetLane => Mathf.Clamp(Mathf.RoundToInt(targetX / EmergencyRoadGame.LaneWidth), -1, 1);

        internal bool WillCrossLane(int lane)
        {
            float laneX = Mathf.Clamp(lane, -1, 1) * EmergencyRoadGame.LaneWidth;
            float minimumX = Mathf.Min(transform.position.x, targetX) - 1.35f;
            float maximumX = Mathf.Max(transform.position.x, targetX) + 1.35f;
            return laneX >= minimumX && laneX <= maximumX;
        }

        public void Configure(float x, bool fromLeft, float speed, float activationDistance)
        {
            targetX = x;
            moveSpeed = Mathf.Max(4f, speed);
            startDistance = Mathf.Max(20f, activationDistance);
        }

        internal bool WillIntersectCoin(Vector3 coinPosition)
        {
            if (Mathf.Abs(coinPosition.z - transform.position.z) > 3f) return false;
            float minimumX = Mathf.Min(transform.position.x, targetX) - 1.35f;
            float maximumX = Mathf.Max(transform.position.x, targetX) + 1.35f;
            return coinPosition.x >= minimumX && coinPosition.x <= maximumX;
        }

        public void Tick(float roadDelta)
        {
            if (!moving && transform.position.z < startDistance) moving = true;
            if (!moving) return;
            Vector3 p = transform.localPosition;
            p.x = Mathf.MoveTowards(p.x, targetX, moveSpeed * Time.deltaTime);
            transform.localPosition = p;
        }
    }
}
