using UnityEngine;

namespace EmergencyRoad
{
    public sealed class SideCrossingHazard : MonoBehaviour
    {
        private float targetX;
        private bool moving;
        private float moveSpeed = 11.5f;
        private float startDistance = 44f;
        private Rigidbody body;

        public void Configure(float x, bool fromLeft, float speed, float activationDistance)
        {
            targetX = x;
            moveSpeed = Mathf.Max(4f, speed);
            startDistance = Mathf.Max(20f, activationDistance);
            body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        public void Tick(float roadDelta)
        {
            if (!moving && transform.position.z < startDistance) moving = true;
            if (!moving) return;
            // This vehicle belongs to a scrolling road chunk. Local movement preserves
            // the fake road scroll; writing Rigidbody world position would cancel it.
            Vector3 p = transform.localPosition;
            p.x = Mathf.MoveTowards(p.x, targetX, moveSpeed * Time.deltaTime);
            transform.localPosition = p;
        }
    }
}
