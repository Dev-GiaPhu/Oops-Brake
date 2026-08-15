using System.Collections;
using UnityEngine;

namespace EmergencyRoad
{
    public sealed class MotorRushHazard : MonoBehaviour
    {
        private EmergencyRoadGame game;
        private float speed = 34f;
        private bool exploded;
        private bool hasBeenInsideCamera;
        private float lockedTargetX;
        private float lateralVelocity;
        private Camera trackingCamera;
        private Renderer[] visualRenderers;
        private readonly Plane[] frustumPlanes = new Plane[6];
        private readonly RaycastHit[] sweepHits = new RaycastHit[16];
        private Rigidbody explodedBody;
        private bool compensateMapScroll;
        private float debrisPhysicsAge;
        private Vector3 previousPosition;
        private Rigidbody body;
        private BoxCollider hitbox;
        public int TargetLane { get; private set; }

        public void Initialize(float x, EmergencyRoadGame owner, Transform playerTransform, Camera camera)
        {
            InitializeLane(Mathf.Clamp(Mathf.RoundToInt(x / EmergencyRoadGame.LaneWidth), -1, 1), owner, camera);
        }

        public void InitializeLane(int lane, EmergencyRoadGame owner, Camera camera)
        {
            game = owner;
            trackingCamera = camera;
            TargetLane = Mathf.Clamp(lane, -1, 1);
            lockedTargetX = TargetLane * EmergencyRoadGame.LaneWidth;
            transform.position = new Vector3(lockedTargetX, .45f, -18f);
            EmergencyRoadGameplaySettings tuning = owner != null ? owner.Settings : null;
            speed = tuning != null ? tuning.motorSpeed : 34f;
            visualRenderers = GetComponentsInChildren<Renderer>(true);
            body = GetComponent<Rigidbody>();
            hitbox = GetComponent<BoxCollider>();
            if (hitbox != null) hitbox.isTrigger = false;
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
            previousPosition = transform.position;
        }

        private void Update()
        {
            if (exploded || game == null) return;
            UpdateCameraLifetime();
        }

        private void FixedUpdate()
        {
            if (!exploded && game != null)
            {
                previousPosition = body != null ? body.position : transform.position;
                Vector3 p = previousPosition;
                p.z += speed * Time.fixedDeltaTime;
                float desiredX = lockedTargetX + Mathf.Sin(p.z * .28f) * .32f;
                float previousX = p.x;
                p.x = Mathf.SmoothDamp(p.x, desiredX, ref lateralVelocity, .18f, 7f, Time.fixedDeltaTime);
                float lateral = (p.x - previousX) / Mathf.Max(.001f, Time.fixedDeltaTime);
                float yaw = Mathf.Atan2(lateral, speed) * Mathf.Rad2Deg;
                float lean = Mathf.Clamp(-lateral * 2f, -13f, 13f);
                Quaternion currentRotation = body != null ? body.rotation : transform.rotation;
                Quaternion rotation = Quaternion.Slerp(currentRotation, Quaternion.Euler(0, yaw, lean),
                    1f - Mathf.Exp(-8f * Time.fixedDeltaTime));
                if (body != null)
                {
                    Vector3 movement = p - body.position;
                    float moveDistance = movement.magnitude;
                    if (moveDistance > .001f)
                    {
                        Vector3 halfExtents = hitbox != null
                            ? Vector3.Scale(hitbox.size * .5f, transform.lossyScale)
                            : new Vector3(.48f, .68f, 1.13f);
                        Vector3 castCenter = hitbox != null ? hitbox.transform.TransformPoint(hitbox.center) : body.position;
                        int hitCount = Physics.BoxCastNonAlloc(castCenter, halfExtents, movement / moveDistance,
                            sweepHits, body.rotation, moveDistance + .08f, ~0, QueryTriggerInteraction.Collide);
                        for (int i = 0; i < hitCount && !exploded; i++) HandleImpact(sweepHits[i].collider);
                        if (exploded) return;
                    }
                    body.MovePosition(p);
                    body.MoveRotation(rotation);
                }
                else transform.SetPositionAndRotation(p, rotation);
                return;
            }
            if (!exploded || !compensateMapScroll || explodedBody == null || game == null) return;
            debrisPhysicsAge += Time.fixedDeltaTime;
            EmergencyRoadGameplaySettings tuning = game.Settings;
            float hold = tuning != null ? tuning.motorDebrisImpactHoldTime : .35f;
            if (debrisPhysicsAge < hold) return;
            float sharpness = tuning != null ? tuning.motorDebrisMapFollowSharpness : 2.2f;
            Vector3 velocity = explodedBody.linearVelocity;
            velocity.z = Mathf.Lerp(velocity.z, -game.CurrentSpeed, 1f - Mathf.Exp(-sharpness * Time.fixedDeltaTime));
            explodedBody.linearVelocity = velocity;
        }

        private void UpdateCameraLifetime()
        {
            if (IsInsideCameraView())
            {
                hasBeenInsideCamera = true;
                return;
            }
            if (hasBeenInsideCamera) Destroy(gameObject);
        }

        private bool IsInsideCameraView()
        {
            if (trackingCamera == null || visualRenderers == null || visualRenderers.Length == 0) return false;
            int first = -1;
            for (int i = 0; i < visualRenderers.Length; i++)
            {
                if (visualRenderers[i] == null) continue;
                first = i;
                break;
            }
            if (first < 0) return false;

            Bounds bounds = visualRenderers[first].bounds;
            for (int i = first + 1; i < visualRenderers.Length; i++)
                if (visualRenderers[i] != null) bounds.Encapsulate(visualRenderers[i].bounds);
            GeometryUtility.CalculateFrustumPlanes(trackingCamera, frustumPlanes);
            return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleImpact(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision != null) HandleImpact(collision.collider);
        }

        private void HandleImpact(Collider other)
        {
            if (exploded || other == null) return;
            if (other.GetComponentInParent<EmergencyVehicleController>() != null)
            {
                hasBeenInsideCamera = true;
                game.Crash(transform.position - other.transform.position);
                StartCoroutine(Explode(true, null));
                return;
            }
            if (other.GetComponentInParent<RoadHazard>() != null) StartCoroutine(Explode(false, other));
        }

        private IEnumerator Explode(bool preserveAfterPlayerHit, Collider impactedCollider)
        {
            exploded = true;
            if (!preserveAfterPlayerHit && impactedCollider != null)
            {
                transform.position = previousPosition;
                impactedCollider.enabled = true;
                impactedCollider.isTrigger = false;
                Physics.SyncTransforms();
            }
            BoxCollider physicsCollider = GetComponent<BoxCollider>();
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
                collider.isTrigger = false;
            }

            if (physicsCollider != null)
            {
                physicsCollider.enabled = true;
                physicsCollider.isTrigger = false;
                physicsCollider.size = new Vector3(.9f, 1.15f, 1.9f);
                physicsCollider.center = new Vector3(0, .58f, 0);
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.mass = 280f;
                body.linearDamping = 1.15f;
                body.angularDamping = 2.4f;
                body.maxAngularVelocity = 5f;
                body.centerOfMass = new Vector3(0, .32f, 0);
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                compensateMapScroll = !preserveAfterPlayerHit && game != null;
                debrisPhysicsAge = 0f;
                float impactForwardMomentum = preserveAfterPlayerHit ? 6.5f : Mathf.Max(3f, speed * .18f);
                body.linearVelocity = new Vector3(Random.Range(-1.25f, 1.25f), 3.6f, impactForwardMomentum);
                body.angularVelocity = new Vector3(Random.Range(2.2f, 4.2f), Random.Range(-2.2f, 2.2f), Random.Range(-4.2f, 4.2f));
                explodedBody = body;
            }

            game.SpawnImpactVfx(transform, new Vector3(0, .7f, 0));
            game.SpawnMotorImpactVfx(transform.position);
            if (preserveAfterPlayerHit) yield break;

            float neverSeenSafety = 0f;
            while (this != null)
            {
                bool inside = IsInsideCameraView();
                if (inside) hasBeenInsideCamera = true;
                if (hasBeenInsideCamera && !inside)
                {
                    Destroy(gameObject);
                    yield break;
                }
                if (!hasBeenInsideCamera)
                {
                    neverSeenSafety += Time.deltaTime;
                    if (neverSeenSafety > 10f)
                    {
                        Destroy(gameObject);
                        yield break;
                    }
                }
                yield return null;
            }
        }
    }
}
