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
        private bool passedPlayer;
        private bool postPassLaneDecisionMade;
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
            GameObject visualPrefab = owner != null && owner.Catalog != null ? owner.Catalog.motorcyclePrefab : null;
            InitializeLane(Mathf.Clamp(Mathf.RoundToInt(x / EmergencyRoadGame.LaneWidth), -1, 1), owner, camera, visualPrefab);
        }

        public void InitializeLane(int lane, EmergencyRoadGame owner, Camera camera, GameObject motorcycleVisualPrefab = null)
        {
            game = owner;
            trackingCamera = camera;
            TargetLane = Mathf.Clamp(lane, -1, 1);
            lockedTargetX = TargetLane * EmergencyRoadGame.LaneWidth;
            transform.position = new Vector3(lockedTargetX, .45f, -18f);
            EmergencyRoadGameplaySettings tuning = owner != null ? owner.Settings : null;
            speed = tuning != null ? tuning.motorSpeed : 34f;
            passedPlayer = false;
            postPassLaneDecisionMade = false;
            lateralVelocity = 0f;
            BuildVisualFromCatalog(motorcycleVisualPrefab);
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

        private void BuildVisualFromCatalog(GameObject visualPrefab)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject obsoleteVisual = transform.GetChild(i).gameObject;
                obsoleteVisual.SetActive(false);
                Destroy(obsoleteVisual);
            }

            if (visualPrefab == null) return;
            GameObject visual = Instantiate(visualPrefab, transform, false);
            visual.name = "Motorcycle Visual";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            visual.transform.localScale = Vector3.one;
            foreach (Collider visualCollider in visual.GetComponentsInChildren<Collider>(true))
                visualCollider.enabled = false;
            EmergencyRoadGame.FitVehicle(visual, .95f, 2.25f);
        }

        private void Update()
        {
            if (exploded || game == null) return;
            UpdateCameraLifetime();
        }

        private void FixedUpdate()
        {
            if (!exploded)
            {
                if (game == null || game.Ended) return;
                previousPosition = body != null ? body.position : transform.position;
                Vector3 p = previousPosition;
                p.z += speed * Time.fixedDeltaTime;

                if (!passedPlayer && game.Player != null && p.z >= game.Player.transform.position.z + 2f)
                {
                    passedPlayer = true;
                    TryChoosePostPassLane(p.z);
                }

                // Stay exactly in the warned lane until the motorcycle has fully passed the player.
                // Any lane change is decided only after that point and only into a clear corridor.
                float desiredX = lockedTargetX;
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

            if (explodedBody == null || game == null) return;
            if (game.Ended)
            {
                StopMapScrollCompensation();
                return;
            }
            if (!compensateMapScroll) return;
            debrisPhysicsAge += Time.fixedDeltaTime;
            EmergencyRoadGameplaySettings tuning = game.Settings;
            float hold = tuning != null ? tuning.motorDebrisImpactHoldTime : .35f;
            if (debrisPhysicsAge < hold) return;
            float sharpness = tuning != null ? tuning.motorDebrisMapFollowSharpness : 2.2f;
            Vector3 velocity = explodedBody.linearVelocity;
            velocity.z = Mathf.Lerp(velocity.z, -game.CurrentSpeed, 1f - Mathf.Exp(-sharpness * Time.fixedDeltaTime));
            explodedBody.linearVelocity = velocity;
        }

        private void TryChoosePostPassLane(float currentZ)
        {
            if (postPassLaneDecisionMade || game == null) return;
            postPassLaneDecisionMade = true;

            int currentLane = Mathf.Clamp(Mathf.RoundToInt(lockedTargetX / EmergencyRoadGame.LaneWidth), -1, 1);
            int firstCandidate;
            int secondCandidate = int.MinValue;
            if (currentLane == 0)
            {
                firstCandidate = Random.value < .5f ? -1 : 1;
                secondCandidate = -firstCandidate;
            }
            else
            {
                firstCandidate = 0;
            }

            EmergencyRoadGameplaySettings tuning = game.Settings;
            float obstacleSafety = tuning != null ? tuning.motorObstacleSafetyDistance : 9f;
            float minimumZ = currentZ - 2f;
            float maximumZ = currentZ + Mathf.Max(12f, obstacleSafety * 1.5f);

            if (TryUsePostPassLane(firstCandidate, minimumZ, maximumZ)) return;
            if (secondCandidate != int.MinValue) TryUsePostPassLane(secondCandidate, minimumZ, maximumZ);
        }

        private bool TryUsePostPassLane(int lane, float minimumZ, float maximumZ)
        {
            lane = Mathf.Clamp(lane, -1, 1);
            if (game.IsLaneReserved(lane, minimumZ, maximumZ)) return false;
            if (!game.IsMotorLaneCorridorClear(lane, minimumZ, maximumZ)) return false;
            TargetLane = lane;
            lockedTargetX = lane * EmergencyRoadGame.LaneWidth;
            return true;
        }

        private void StopMapScrollCompensation()
        {
            if (!compensateMapScroll || explodedBody == null) return;
            compensateMapScroll = false;
            Vector3 velocity = explodedBody.linearVelocity;
            velocity.z = 0f;
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
            if (other.GetComponentInParent<RoadHazard>() != null ||
                other.GetComponentInParent<SameDirectionTraffic>() != null ||
                other.GetComponentInParent<SideCrossingHazard>() != null)
                StartCoroutine(Explode(false, other));
        }

        private IEnumerator Explode(bool preserveAfterPlayerHit, Collider impactedCollider)
        {
            exploded = true;
            if (!preserveAfterPlayerHit && impactedCollider != null)
            {
                // Return to the last safe physics position before releasing the rigidbody.
                // The road scroll is simulated, so retaining forward motor speed here
                // would visually carry the bike through the object it just struck.
                transform.position = previousPosition;
                impactedCollider.enabled = true;
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
                float impactLongitudinalVelocity = preserveAfterPlayerHit
                    ? 6.5f
                    : -Mathf.Max(2.5f, game != null ? game.CurrentSpeed * .45f : speed * .2f);
                body.linearVelocity = new Vector3(Random.Range(-1.25f, 1.25f), 3.6f, impactLongitudinalVelocity);
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
