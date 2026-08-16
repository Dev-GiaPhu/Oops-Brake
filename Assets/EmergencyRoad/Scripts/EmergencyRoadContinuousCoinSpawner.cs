using System.Collections.Generic;
using UnityEngine;

namespace EmergencyRoad
{
    [DefaultExecutionOrder(1500)]
    public sealed class EmergencyRoadContinuousCoinSpawner : MonoBehaviour
    {
        private const float MinimumSpacing = 17f;
        private const float MaximumSpacing = 25f;
        private const float SpawnAheadDistance = 145f;
        private const float RemoveBehindDistance = -10f;

        [Header("DIRECT REFERENCES - DRAG IN INSPECTOR")]
        [SerializeField] private EmergencyRoadGame game;
        private GameObject coinPrefab;

        private readonly List<GameObject> activeCoins = new();
        private readonly Collider[] overlapHits = new Collider[24];
        private int previousLane = 99;
        private bool initialized;

        public void Configure(EmergencyRoadGame owner, GameObject prefab)
        {
            game = owner;
            coinPrefab = prefab;
            if (!ValidatePrefab())
            {
                enabled = false;
                return;
            }
            if (!initialized)
            {
                initialized = true;
                FillRoadAhead();
            }
        }

        private bool ValidatePrefab()
        {
            if (game == null)
            {
                Debug.LogError("[Emergency Road] Continuous Coin Spawner thiếu Game reference. Kéo EmergencyRoadGame vào Inspector.", this);
                return false;
            }
            if (coinPrefab == null)
            {
                Debug.LogError("[Emergency Road] Continuous Coin Spawner thiếu Coin Prefab. Kéo prefab vào Inspector/Catalog; runtime không tạo fallback.", this);
                return false;
            }
            if (coinPrefab.GetComponentInChildren<RoadPickup>(true) == null || coinPrefab.GetComponentInChildren<CoinSpinner>(true) == null || coinPrefab.GetComponentInChildren<Collider>(true) == null)
            {
                Debug.LogError("[Emergency Road] Coin Prefab phải chứa sẵn RoadPickup + CoinSpinner + Collider trigger. Không AddComponent runtime.", coinPrefab);
                return false;
            }
            return true;
        }

        private void Update()
        {
            if (!initialized || game == null || game.Ended || Time.timeScale <= 0f) return;
            MoveCoinsWithRoad();
            FillRoadAhead();
        }

        private void MoveCoinsWithRoad()
        {
            float deltaZ = game.CurrentSpeed * Time.deltaTime;
            for (int i = activeCoins.Count - 1; i >= 0; i--)
            {
                GameObject coin = activeCoins[i];
                if (coin == null || !coin.activeSelf)
                {
                    if (coin != null) Destroy(coin);
                    activeCoins.RemoveAt(i);
                    continue;
                }
                Vector3 position = coin.transform.position;
                position.z -= deltaZ;
                coin.transform.position = position;
                if (position.z < RemoveBehindDistance)
                {
                    Destroy(coin);
                    activeCoins.RemoveAt(i);
                }
            }
        }

        private void FillRoadAhead()
        {
            float farthestZ = 0f;
            bool foundCoin = false;
            for (int i = 0; i < activeCoins.Count; i++)
            {
                GameObject coin = activeCoins[i];
                if (coin == null || !coin.activeSelf) continue;
                farthestZ = foundCoin ? Mathf.Max(farthestZ, coin.transform.position.z) : coin.transform.position.z;
                foundCoin = true;
            }
            if (!foundCoin) farthestZ = 2f;

            int safety = 0;
            while (farthestZ < SpawnAheadDistance && safety++ < 16)
            {
                farthestZ += Random.Range(MinimumSpacing, MaximumSpacing);
                SpawnSparseCoin(farthestZ);
            }
        }

        private void SpawnSparseCoin(float requestedZ)
        {
            int preferredLane = ChooseLane();
            for (int longitudinalAttempt = 0; longitudinalAttempt < 3; longitudinalAttempt++)
            {
                float spawnZ = requestedZ + longitudinalAttempt * 3.5f;
                for (int laneOffset = 0; laneOffset < 3; laneOffset++)
                {
                    int lane = ((preferredLane + 1 + laneOffset) % 3) - 1;
                    Vector3 candidate = new(lane * EmergencyRoadGame.LaneWidth, 1.1f, spawnZ);
                    if (IsBlocked(candidate)) continue;

                    CreateCoin(candidate);
                    previousLane = lane;
                    return;
                }
            }

            // Không cưỡng ép tạo coin khi cả ba làn đều đang có xe/chướng ngại.
            // Vòng FillRoadAhead tiếp theo sẽ thử một vị trí xa hơn.
        }

        private int ChooseLane()
        {
            int lane = Random.Range(-1, 2);
            if (lane != previousLane || Random.value > .7f) return lane;
            return ChooseAlternativeLane(lane);
        }

        private static int ChooseAlternativeLane(int currentLane)
        {
            int offset = Random.value < .5f ? 1 : 2;
            return ((currentLane + 1 + offset) % 3) - 1;
        }

        internal bool IsSameDirectionPathClear(int lane, float vehicleZ, float roadSpeed)
        {
            lane = Mathf.Clamp(lane, -1, 1);
            for (int i = 0; i < activeCoins.Count; i++)
            {
                GameObject coin = activeCoins[i];
                if (coin == null || !coin.activeSelf) continue;
                Vector3 coinPosition = coin.transform.position;
                int coinLane = Mathf.Clamp(Mathf.RoundToInt(coinPosition.x / EmergencyRoadGame.LaneWidth), -1, 1);
                if (coinLane != lane) continue;

                float distanceAhead = coinPosition.z - vehicleZ;
                if (distanceAhead < -3.8f) continue;
                float contactTime = Mathf.Max(0f, distanceAhead - 3.8f) / Mathf.Max(1f, roadSpeed);
                float coinZAtContact = coinPosition.z - game.CurrentSpeed * contactTime;
                if (coinZAtContact > RemoveBehindDistance) return false;
            }
            return true;
        }

        internal bool IsCrossingPathClear(bool fromLeft, int targetLane, float worldZ)
        {
            float startX = fromLeft ? -12f : 12f;
            float targetX = Mathf.Clamp(targetLane, -1, 1) * EmergencyRoadGame.LaneWidth;
            float minimumX = Mathf.Min(startX, targetX) - 1.35f;
            float maximumX = Mathf.Max(startX, targetX) + 1.35f;
            for (int i = 0; i < activeCoins.Count; i++)
            {
                GameObject coin = activeCoins[i];
                if (coin == null || !coin.activeSelf) continue;
                Vector3 position = coin.transform.position;
                if (Mathf.Abs(position.z - worldZ) <= 3f && position.x >= minimumX && position.x <= maximumX)
                    return false;
            }
            return true;
        }

        private bool IsBlocked(Vector3 position)
        {
            IReadOnlyList<RoadHazard> hazards = game.ActiveHazards;
            for (int i = 0; i < hazards.Count; i++)
            {
                RoadHazard hazard = hazards[i];
                if (hazard == null) continue;
                SameDirectionTraffic sameDirection = hazard.GetComponent<SameDirectionTraffic>();
                if (sameDirection != null && sameDirection.WillIntersectCoin(position, RemoveBehindDistance)) return true;
                SideCrossingHazard crossing = hazard.GetComponent<SideCrossingHazard>();
                if (crossing != null && crossing.WillIntersectCoin(position)) return true;
            }

            int hitCount = Physics.OverlapBoxNonAlloc(
                position,
                new Vector3(1.25f, .9f, 2.8f),
                overlapHits,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = overlapHits[i];
                overlapHits[i] = null;
                if (hit == null) continue;
                if (hit.GetComponentInParent<RoadHazard>() != null ||
                    hit.GetComponentInParent<MotorRushHazard>() != null)
                    return true;
            }
            return false;
        }

        private void CreateCoin(Vector3 worldPosition)
        {
            GameObject coin = Instantiate(coinPrefab, transform, false);
            coin.name = "Sparse Continuous Coin";
            coin.transform.position = worldPosition;
            foreach (Collider collider in coin.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
                collider.isTrigger = true;
            }
            activeCoins.Add(coin);
        }
    }
}
