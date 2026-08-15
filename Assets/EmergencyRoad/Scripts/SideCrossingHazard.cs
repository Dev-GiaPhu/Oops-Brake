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
        }

        private void FixedUpdate()
        {
            if (!moving) return;
            Vector3 p = body != null ? body.position : transform.position;
            p.x = Mathf.MoveTowards(p.x, targetX, moveSpeed * Time.fixedDeltaTime);
            if (body != null) body.MovePosition(p);
            else transform.position = p;
        }
    }
}
