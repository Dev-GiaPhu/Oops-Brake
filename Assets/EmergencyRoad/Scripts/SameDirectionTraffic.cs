using UnityEngine;

namespace EmergencyRoad
{
    public sealed class SameDirectionTraffic : MonoBehaviour
    {
        private EmergencyRoadGame game;
        private int lane;
        private float targetX;
        private float roadSpeed;
        private float cruiseSpeed;
        private float laneChangeChance;
        private float decisionInterval;
        private float brakingDistance;
        private float brakingStrength;
        private float nextDecision;
        private float nextSafetyDecision;
        private float xVelocity;
        private bool occupyingReservedEscape;
        private readonly bool[] reservedLanes = new bool[3];
        private Rigidbody body;

        public void Initialize(EmergencyRoadGame owner, int startLane, float speed, float changeChance, float interval, float brakeDistance, float brakeStrength)
        {
            game = owner;
            lane = Mathf.Clamp(startLane, -1, 1);
            targetX = lane * EmergencyRoadGame.LaneWidth;
            roadSpeed = cruiseSpeed = speed;
            laneChangeChance = changeChance;
            decisionInterval = Mathf.Max(.25f, interval);
            brakingDistance = Mathf.Max(4f, brakeDistance);
            brakingStrength = Mathf.Max(1f, brakeStrength);
            nextDecision = Time.time + Random.Range(decisionInterval * .65f, decisionInterval * 1.35f);
            body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        private void Update()
        {
            if (game == null || game.Ended) return;

            if (Time.time >= nextSafetyDecision)
            {
                nextSafetyDecision = Time.time + .16f;
                occupyingReservedEscape = LaneIsReservedEscape(lane);
                if (occupyingReservedEscape) EvacuateReservedEscapeLane();
            }

            float clearance = ForwardClearance(lane, brakingDistance + 4f);
            float desiredSpeed = clearance < brakingDistance && !occupyingReservedEscape ? 0f : cruiseSpeed;
            roadSpeed = Mathf.MoveTowards(
                roadSpeed,
                desiredSpeed,
                (desiredSpeed < roadSpeed ? brakingStrength : brakingStrength * .35f) * Time.deltaTime);

            if (Time.time < nextDecision || transform.position.z < 9f) return;
            nextDecision = Time.time + Random.Range(decisionInterval * .75f, decisionInterval * 1.35f);

            int soleRoute = FindSoleOpenRoute();
            if (soleRoute >= -1)
            {
                if (lane == soleRoute) LeaveSoleEscapeLane();
                return;
            }

            if (Random.value > laneChangeChance) return;
            int direction = Random.value < .5f ? -1 : 1;
            int candidate = Mathf.Clamp(lane + direction, -1, 1);
            if (candidate == lane) candidate = Mathf.Clamp(lane - direction, -1, 1);
            if (candidate != lane && !LaneIsReservedEscape(candidate) && LaneIsClear(candidate))
            {
                lane = candidate;
                targetX = lane * EmergencyRoadGame.LaneWidth;
            }
        }

        private void FixedUpdate()
        {
            if (game == null || game.Ended) return;
            Vector3 position = body != null ? body.position : transform.position;
            position.z += (roadSpeed - game.CurrentSpeed) * Time.fixedDeltaTime;
            position.x = Mathf.SmoothDamp(position.x, targetX, ref xVelocity, .42f,
                EmergencyRoadGame.LaneWidth * 1.65f, Time.fixedDeltaTime);
            if (body != null) body.MovePosition(position);
            else transform.position = position;
            if (position.z < -32f || position.z > 165f) Destroy(gameObject);
        }

        private int FindSoleOpenRoute()
        {
            int count = 0;
            int last = -2;
            for (int candidate = -1; candidate <= 1; candidate++)
            {
                if (ForwardClearance(candidate, brakingDistance + 7f) < brakingDistance) continue;
                count++;
                last = candidate;
            }
            return count == 1 ? last : -2;
        }

        private void LeaveSoleEscapeLane()
        {
            int left = lane - 1;
            int right = lane + 1;
            if (left >= -1 && !LaneIsReservedEscape(left) && LaneIsClear(left))
            {
                lane = left;
                targetX = lane * EmergencyRoadGame.LaneWidth;
                return;
            }
            if (right <= 1 && !LaneIsReservedEscape(right) && LaneIsClear(right))
            {
                lane = right;
                targetX = lane * EmergencyRoadGame.LaneWidth;
            }
        }

        private bool LaneIsReservedEscape(int candidate)
        {
            float back = transform.position.z - 5f;
            float front = transform.position.z + brakingDistance + EmergencyRoadGame.ChunkSpacing * 1.6f;
            return game.IsLaneReserved(candidate, back, front);
        }

        private void EvacuateReservedEscapeLane()
        {
            float back = transform.position.z - 5f;
            float front = transform.position.z + brakingDistance + EmergencyRoadGame.ChunkSpacing * 1.6f;
            game.FillReservedLanes(back, front, reservedLanes);

            int best = -99;
            for (int candidate = -1; candidate <= 1; candidate++)
            {
                if (reservedLanes[candidate + 1] || !LaneIsClear(candidate)) continue;
                if (best == -99 || Mathf.Abs(candidate - lane) < Mathf.Abs(best - lane)) best = candidate;
            }

            if (best == -99 || best == lane) return;
            int step = lane + System.Math.Sign(best - lane);
            if (!LaneIsClear(step)) return;

            lane = step;
            targetX = lane * EmergencyRoadGame.LaneWidth;
            occupyingReservedEscape = LaneIsReservedEscape(lane);
        }

        private float ForwardClearance(int candidate, float distance)
        {
            Physics.SyncTransforms();
            float nearest = float.PositiveInfinity;
            Vector3 center = new(
                candidate * EmergencyRoadGame.LaneWidth,
                .9f,
                transform.position.z + distance * .5f);
            Collider[] hits = Physics.OverlapBox(
                center,
                new Vector3(1.25f, .9f, distance * .5f),
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform)) continue;
                if (hit.GetComponentInParent<RoadHazard>() == null) continue;
                float gap = hit.bounds.min.z - (transform.position.z + 1.78f);
                if (gap >= -.1f) nearest = Mathf.Min(nearest, gap);
            }
            return nearest;
        }

        private bool LaneIsClear(int candidate)
        {
            Physics.SyncTransforms();
            Vector3 center = new(candidate * EmergencyRoadGame.LaneWidth, .9f, transform.position.z);
            Collider[] hits = Physics.OverlapBox(
                center,
                new Vector3(1.3f, .9f, 5.5f),
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                if (hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform)) continue;
                if (hit.GetComponentInParent<RoadHazard>() != null ||
                    hit.GetComponentInParent<EmergencyVehicleController>() != null)
                    return false;
            }
            return true;
        }
    }
}
