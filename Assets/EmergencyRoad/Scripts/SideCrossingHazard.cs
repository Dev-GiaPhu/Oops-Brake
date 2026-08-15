using UnityEngine;

namespace EmergencyRoad
{
    public sealed class SideCrossingHazard : MonoBehaviour
    {
        private float targetX;
        private bool moving;
        private float moveSpeed = 11.5f;
        private float startDistance = 44f;

        public void Configure(float x, bool fromLeft, float speed, float activationDistance)
        {
            targetX = x;
            moveSpeed = Mathf.Max(4f, speed);
            startDistance = Mathf.Max(20f, activationDistance);
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
