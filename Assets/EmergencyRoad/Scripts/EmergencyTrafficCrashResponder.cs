using UnityEngine;

namespace EmergencyRoad
{
    [DefaultExecutionOrder(2000)]
    public sealed class EmergencyTrafficCrashResponder : MonoBehaviour
    {
        private const int DetectionBufferSize = 32;
        private static readonly Collider[] OverlapBuffer = new Collider[DetectionBufferSize];
        private static readonly RaycastHit[] CastBuffer = new RaycastHit[DetectionBufferSize];

        [Header("PREFAB REFERENCES - DRAG DIRECTLY")]
        [SerializeField] private BoxCollider hitbox;
        [SerializeField] private Rigidbody body;
        [SerializeField] private SameDirectionTraffic sameDirectionTraffic;
        [SerializeField] private SideCrossingHazard sideCrossingTraffic;

        private EmergencyRoadGame game;
        private bool initialized;
        private bool scrollWithRoad;
        private bool crashed;
        private Vector3 previousCenter;
        private Quaternion previousRotation;
        private Vector3 previousRootPosition;
        private Quaternion previousRootRotation;

        public bool Crashed => crashed;

        public void ConfigurePrefabReferences(BoxCollider collider, Rigidbody rigidbody, SameDirectionTraffic sameDirection, SideCrossingHazard crossing)
        {
            hitbox = collider;
            body = rigidbody;
            sameDirectionTraffic = sameDirection;
            sideCrossingTraffic = crossing;
        }

        public void Configure(EmergencyRoadGame owner, bool shouldScrollWithRoad, GameObject impactVfxPrefab)
        {
            if (crashed) return;
            game = owner;
            scrollWithRoad = shouldScrollWithRoad;
            if (hitbox == null)
            {
                Debug.LogError("[Emergency Road] EmergencyTrafficCrashResponder thiếu BoxCollider reference trong prefab Inspector.", this);
                enabled = false;
                return;
            }
            previousCenter = WorldCenter;
            previousRotation = WorldRotation;
            previousRootPosition = transform.position;
            previousRootRotation = transform.rotation;
            initialized = true;
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            if (crashed)
            {
                FollowRoadAfterCrash();
                return;
            }
            if (hitbox == null || !hitbox.enabled) return;

            Vector3 currentCenter = WorldCenter;
            Quaternion currentRotation = WorldRotation;
            Vector3 movement = currentCenter - previousCenter;
            if (movement.sqrMagnitude > .0001f) DetectAlongMovement(previousCenter, previousRotation, movement);
            if (!crashed) DetectCurrentOverlap(currentCenter, currentRotation);
            previousCenter = currentCenter;
            previousRotation = currentRotation;
            previousRootPosition = transform.position;
            previousRootRotation = transform.rotation;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryResolveTrafficImpact(other);
        }

        private void DetectAlongMovement(Vector3 origin, Quaternion orientation, Vector3 movement)
        {
            float distance = movement.magnitude;
            if (distance <= .001f) return;
            int count = Physics.BoxCastNonAlloc(
                origin,
                WorldHalfExtents,
                movement / distance,
                CastBuffer,
                orientation,
                distance + .12f,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count && !crashed; i++) TryResolveTrafficImpact(CastBuffer[i].collider);
        }

        private void DetectCurrentOverlap(Vector3 center, Quaternion orientation)
        {
            int count = Physics.OverlapBoxNonAlloc(
                center,
                WorldHalfExtents,
                OverlapBuffer,
                orientation,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < count && !crashed; i++) TryResolveTrafficImpact(OverlapBuffer[i]);
        }

        private void TryResolveTrafficImpact(Collider otherCollider)
        {
            if (crashed || otherCollider == null || otherCollider.transform.IsChildOf(transform)) return;
            RoadHazard otherHazard = otherCollider.GetComponentInParent<RoadHazard>();
            if (otherHazard == null) return;
            EmergencyTrafficCrashResponder other = otherCollider.GetComponentInParent<EmergencyTrafficCrashResponder>();
            if (other == this) return;

            Vector3 otherCenter = other != null ? other.WorldCenter : otherCollider.bounds.center;
            Vector3 impactDirection = otherCenter - WorldCenter;
            if (impactDirection.sqrMagnitude < .001f) impactDirection = transform.right;
            if (other == null)
            {
                Crash(impactDirection, true);
                return;
            }
            if (other.crashed)
            {
                Crash(impactDirection, true);
                return;
            }

            Crash(impactDirection, true);
            other.Crash(-impactDirection, true);
        }

        private void Crash(Vector3 impactDirection, bool restorePreviousPose = false)
        {
            if (crashed) return;
            crashed = true;
            if (impactDirection.sqrMagnitude < .001f) impactDirection = Vector3.forward;
            impactDirection.Normalize();

            if (sameDirectionTraffic != null) sameDirectionTraffic.enabled = false;
            if (sideCrossingTraffic != null) sideCrossingTraffic.enabled = false;
            if (restorePreviousPose)
                transform.SetPositionAndRotation(previousRootPosition, previousRootRotation);
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            ApplyPermanentCrashPose(impactDirection);
            Vector3 localImpact = transform.InverseTransformDirection(impactDirection);
            if (game != null)
                game.SpawnImpactVfx(
                    transform,
                    new Vector3(
                        Mathf.Clamp(localImpact.x, -1f, 1f) * .85f,
                        .72f,
                        Mathf.Clamp(localImpact.z, -1f, 1f) * 1.35f));
        }

        private void FollowRoadAfterCrash()
        {
            if (!scrollWithRoad || game == null || game.Ended || Time.timeScale <= 0f) return;
            Vector3 position = transform.position;
            position.z -= game.CurrentSpeed * Time.deltaTime;
            transform.position = position;
            if (position.z < -48f) Destroy(gameObject);
        }

        private void ApplyPermanentCrashPose(Vector3 impactDirection)
        {
            Vector3 localImpact = transform.InverseTransformDirection(impactDirection);
            float roll = -Mathf.Sign(Mathf.Abs(localImpact.x) < .01f ? 1f : localImpact.x) * Random.Range(4f, 8f);
            float pitch = Mathf.Sign(localImpact.z) * Random.Range(2f, 5f);
            transform.localRotation *= Quaternion.Euler(pitch, Random.Range(-3f, 3f), roll);
        }

        private Vector3 WorldCenter => hitbox != null ? hitbox.transform.TransformPoint(hitbox.center) : transform.position;
        private Quaternion WorldRotation => hitbox != null ? hitbox.transform.rotation : transform.rotation;

        private Vector3 WorldHalfExtents
        {
            get
            {
                if (hitbox == null) return new Vector3(1.2f, .9f, 2f);
                Vector3 scale = hitbox.transform.lossyScale;
                Vector3 half = hitbox.size * .5f;
                return new Vector3(
                    Mathf.Abs(half.x * scale.x) + .08f,
                    Mathf.Abs(half.y * scale.y) + .04f,
                    Mathf.Abs(half.z * scale.z) + .08f);
            }
        }
    }
}
