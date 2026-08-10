using System.Collections;
using UnityEngine;

namespace EmergencyRoad
{
    public sealed class MotorRushHazard : MonoBehaviour
    {
        private EmergencyRoadGame game;
        private Transform player;
        private float speed = 34f;
        private bool exploded;
        private bool pathLocked;
        private bool hasBeenInsideCamera;
        private float lockedTargetX;
        private float lateralVelocity;
        private float trackingVelocity;
        private Camera trackingCamera;
        private Renderer[] visualRenderers;
        private readonly Plane[] frustumPlanes = new Plane[6];
        private Rigidbody explodedBody;
        private bool compensateMapScroll;
        private float debrisPhysicsAge;

        public void Initialize(float x, EmergencyRoadGame owner, Transform playerTransform, Camera camera)
        {
            game = owner;
            player = playerTransform;
            trackingCamera = camera;
            lockedTargetX = x;
            transform.position = new Vector3(x + Mathf.Sin(-18f * .28f) * .5f, .45f, -18f);
            EmergencyRoadGameplaySettings tuning = owner != null ? owner.Settings : null;
            speed = tuning != null ? tuning.motorSpeed : 34f;
            visualRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private void Update()
        {
            if (exploded || game == null) return;
            Vector3 p = transform.position;
            p.z += speed * Time.deltaTime;
            if (!pathLocked && player != null)
            {
                EmergencyRoadGameplaySettings tuning = game.Settings;
                float smooth = tuning != null ? tuning.motorTrackingSmoothTime : .65f;
                float maxLateral = tuning != null ? tuning.motorMaxLateralSpeed : 3.5f;
                lockedTargetX = Mathf.SmoothDamp(lockedTargetX, player.position.x, ref trackingVelocity, smooth, maxLateral);
                if (p.z >= -4f) pathLocked = true;
            }

            float desiredX = lockedTargetX + Mathf.Sin(p.z * .28f) * .5f;
            float previousX = p.x;
            p.x = Mathf.SmoothDamp(p.x, desiredX, ref lateralVelocity, .18f, 7f);
            transform.position = p;
            float lateral = (p.x - previousX) / Mathf.Max(.001f, Time.deltaTime);
            float yaw = Mathf.Atan2(lateral, speed) * Mathf.Rad2Deg;
            float lean = Mathf.Clamp(-lateral * 2f, -13f, 13f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, yaw, lean), 1f - Mathf.Exp(-8f * Time.deltaTime));
            UpdateCameraLifetime();
        }

        private void FixedUpdate()
        {
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
            if (exploded) return;
            if (other.GetComponentInParent<EmergencyVehicleController>() != null)
            {
                hasBeenInsideCamera = true;
                game.Crash(transform.position - other.transform.position);
                StartCoroutine(Explode(true));
                return;
            }
            if (other.GetComponentInParent<RoadHazard>() != null) StartCoroutine(Explode(false));
        }

        private IEnumerator Explode(bool preserveAfterPlayerHit)
        {
            exploded = true;
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
                float mapSpeed = compensateMapScroll ? game.CurrentSpeed : 0f;
                debrisPhysicsAge = 0f;
                body.linearVelocity = new Vector3(Random.Range(-1.25f, 1.25f), 3.6f, 6.5f - mapSpeed);
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
