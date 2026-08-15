using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EmergencyRoad
{
    public sealed class EmergencyVehicleController : MonoBehaviour
    {
        private Transform visual;
        private EmergencyRoadGame game;
        private AudioClip vehicleHorn;
        private int vehicleIndex;
        private int lane;
        private int previousLane;
        private float targetX;
        private float bump;
        private bool crashed;
        private Coroutine edgeEffect;
        private Coroutine hornEffect;
        private Vector3 baseScale;
        private Vector3 baseLocalPosition;
        private double nextHornDspTime;
        private EmergencyVehicleFirstPersonRig firstPersonRig;

        public int CurrentLane => lane;
        public bool HornHeld { get; private set; }

        public void AutomationMoveTowardLane(int desiredLane, bool bypassSideSafety = false)
        {
            desiredLane = Mathf.Clamp(desiredLane, -1, 1);
            if (bypassSideSafety)
            {
                lane = desiredLane;
                targetX = lane * EmergencyRoadGame.LaneWidth;
                return;
            }
            if (desiredLane != lane) Shift(desiredLane > lane ? 1 : -1);
        }

        public void Initialize(Transform view, EmergencyRoadGame owner, AudioClip horn, int selectedVehicleIndex)
        {
            visual = view;
            game = owner;
            vehicleHorn = horn;
            vehicleIndex = selectedVehicleIndex;
            targetX = 0f;
            baseScale = visual.localScale;
            baseLocalPosition = visual.localPosition;
            firstPersonRig = visual.GetComponentInChildren<EmergencyVehicleFirstPersonRig>(true);
            if (firstPersonRig != null) firstPersonRig.BindStableCameraReference(transform);
        }

        private void Update()
        {
            if (crashed || Time.timeScale == 0f || Keyboard.current == null || visual == null)
            {
                HornHeld = false;
                return;
            }
            if (Keyboard.current.aKey.wasPressedThisFrame) Shift(-1);
            if (Keyboard.current.dKey.wasPressedThisFrame) Shift(1);
            HornHeld = Keyboard.current.spaceKey.isPressed;
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                Honk();
            else if (HornHeld && AudioSettings.dspTime >= nextHornDspTime)
                Honk();
            Vector3 p = transform.position;
            p.x = Mathf.SmoothDamp(p.x, targetX, ref bump, .12f);
            transform.position = p;
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(0, 0, (targetX - p.x) * -5f), Time.deltaTime * 8f);
            if (firstPersonRig != null)
                firstPersonRig.SetSteering((targetX - p.x) / Mathf.Max(.1f, EmergencyRoadGame.LaneWidth * .45f));
        }

        private void Shift(int dir)
        {
            int next = Mathf.Clamp(lane + dir, -1, 1);
            if (next == lane)
            {
                StartEdgeSqueeze(dir);
                return;
            }
            if (EmergencyRoadProfile.Current.sideCollisionEnabled && IsLaneBlockedBesidePlayer(next))
            {
                StartEdgeSqueeze(dir);
                return;
            }
            previousLane = lane;
            lane = next;
            targetX = lane * EmergencyRoadGame.LaneWidth;
        }

        private bool IsLaneBlockedBesidePlayer(int targetLane)
        {
            float playerZ = transform.position.z;
            Vector3 center = new(targetLane * EmergencyRoadGame.LaneWidth, 1f, playerZ + .15f);
            EmergencyRoadGameplaySettings tuning = game.Settings;
            float halfLength = tuning != null ? tuning.sideCheckHalfLength : .68f;
            float release = tuning != null ? tuning.releaseBehindPlayer : .32f;
            Collider[] hits = Physics.OverlapBox(center, new Vector3(EmergencyRoadGame.LaneWidth * .36f, .85f, halfLength), Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || hit.bounds.center.z < playerZ - release || hit.bounds.min.z >= playerZ + 1.15f) continue;
                if (hit.GetComponentInParent<RoadHazard>() != null || hit.GetComponentInParent<MotorRushHazard>() != null) return true;
            }
            return false;
        }

        private void StartEdgeSqueeze(int dir)
        {
            if (visual == null) return;
            if (edgeEffect != null) StopCoroutine(edgeEffect);
            if (hornEffect != null)
            {
                StopCoroutine(hornEffect);
                hornEffect = null;
            }
            visual.localScale = baseScale;
            visual.localPosition = baseLocalPosition;
            edgeEffect = StartCoroutine(EdgeSqueeze(dir));
        }

        private IEnumerator EdgeSqueeze(int dir)
        {
            const float duration = .14f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float s = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                visual.localPosition = baseLocalPosition + Vector3.right * (dir * .16f * s);
                visual.localScale = SuppressScaleEffects
                    ? baseScale
                    : Vector3.Scale(baseScale, new Vector3(1f - .34f * s, 1f + .035f * s, 1f + .08f * s));
                yield return null;
            }
            visual.localPosition = baseLocalPosition;
            visual.localScale = baseScale;
            edgeEffect = null;
        }

        private void Honk()
        {
            nextHornDspTime = game.Audio.Horn(vehicleHorn, vehicleIndex);
            if (hornEffect != null) StopCoroutine(hornEffect);
            if (edgeEffect != null)
            {
                StopCoroutine(edgeEffect);
                edgeEffect = null;
            }
            visual.localPosition = baseLocalPosition;
            visual.localScale = baseScale;
            hornEffect = StartCoroutine(HonkBounce());
        }

        private IEnumerator HonkBounce()
        {
            float t = 0f;
            while (t < .38f)
            {
                t += Time.deltaTime;
                float s = Mathf.Sin(t / .38f * Mathf.PI);
                visual.localScale = SuppressScaleEffects
                    ? baseScale
                    : Vector3.Scale(baseScale, new Vector3(1f - .08f * s, 1f + .32f * s, 1f - .08f * s));
                yield return null;
            }
            visual.localScale = baseScale;
            hornEffect = null;
        }

        private bool SuppressScaleEffects => game != null && game.IsFirstPersonView;

        private void OnTriggerEnter(Collider other)
        {
            RoadPickup pickup = other.GetComponentInParent<RoadPickup>();
            if (pickup != null)
            {
                game.AddCoin();
                pickup.Collect();
                return;
            }
            Vector3 impact = other.bounds.center - transform.position;
            if (other.GetComponentInParent<MotorRushHazard>() != null)
            {
                game.Crash(impact);
                return;
            }
            if (other.GetComponentInParent<RoadHazard>() == null) return;
            bool sideContact = Mathf.Abs(impact.x) > 1.15f && Mathf.Abs(impact.z) < 1.65f;
            if (sideContact && EmergencyRoadProfile.Current.sideCollisionEnabled)
            {
                int side = impact.x >= 0f ? 1 : -1;
                lane = previousLane;
                targetX = lane * EmergencyRoadGame.LaneWidth;
                StartEdgeSqueeze(side);
                return;
            }
            game.Crash(impact);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.GetComponentInParent<MotorRushHazard>() != null || collision.gameObject.GetComponentInParent<RoadHazard>() != null)
                game.Crash(collision.collider.bounds.center - transform.position);
        }

        public void CrashVisual(Vector3 impactDirection)
        {
            crashed = true;
            HornHeld = false;
            if (firstPersonRig != null) firstPersonRig.SetSteering(0f);
            EnableHeavyCrashPhysics(impactDirection);
            Vector3 localImpact = transform.InverseTransformDirection(impactDirection.normalized);
            game.SpawnImpactVfx(transform, new Vector3(Mathf.Clamp(localImpact.x, -1f, 1f) * .8f, .72f, Mathf.Clamp(localImpact.z, -1f, 1f) * 1.35f));
            StartCoroutine(Crumple(impactDirection));
        }

        private void OnDisable()
        {
            HornHeld = false;
        }

        private void EnableHeavyCrashPhysics(Vector3 impactDirection)
        {
            Rigidbody body = GetComponent<Rigidbody>();
            BoxCollider hitbox = GetComponent<BoxCollider>();
            if (body == null) return;
            Vector3 flat = new(impactDirection.x, 0, impactDirection.z);
            if (flat.sqrMagnitude < .001f) flat = Vector3.forward;
            Vector3 push = -flat.normalized;
            if (hitbox != null) hitbox.isTrigger = false;
            body.isKinematic = false;
            body.useGravity = true;
            body.mass = 1650f;
            body.linearDamping = 2.4f;
            body.angularDamping = 3.8f;
            body.maxAngularVelocity = 2.8f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.centerOfMass = new Vector3(0, .28f, 0);
            body.linearVelocity = push * 1.65f + Vector3.up * 1.15f;
            body.angularVelocity = new Vector3(push.z * .42f, -push.x * .24f, push.x * .82f);
        }

        private IEnumerator Crumple(Vector3 impactDirection)
        {
            if (visual == null) yield break;
            float t = 0f;
            Vector3 localImpact = visual.parent != null ? visual.parent.InverseTransformDirection(impactDirection).normalized : impactDirection.normalized;
            while (t < .55f)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .42f));
                float recoil = Mathf.Sin(Mathf.Clamp01(t / .55f) * Mathf.PI) * .06f;
                visual.localPosition = baseLocalPosition - localImpact * (.2f * s + recoil);
                visual.localRotation = Quaternion.Euler((5f + Mathf.Abs(localImpact.z) * 7f) * s, localImpact.x * 11f * s, -localImpact.x * 18f * s);
                yield return null;
            }
        }
    }
}
