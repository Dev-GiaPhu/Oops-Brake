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
        [SerializeField] private GameObject coinPrefab;

        private readonly List<GameObject> activeCoins = new();
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
            int lane = ChooseLane();
            float spawnZ = requestedZ;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 candidate = new(lane * EmergencyRoadGame.LaneWidth, 1.1f, spawnZ);
                if (!IsBlocked(candidate))
                {
                    CreateCoin(candidate);
                    previousLane = lane;
                    return;
                }
                lane = ChooseAlternativeLane(lane);
                spawnZ += 2.5f;
            }
            CreateCoin(new Vector3(lane * EmergencyRoadGame.LaneWidth, 1.1f, spawnZ));
            previousLane = lane;
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

        private static bool IsBlocked(Vector3 position)
        {
            Collider[] hits = Physics.OverlapBox(position, new Vector3(1.05f, .8f, 2.1f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hits)
            {
                if (hit == null) continue;
                if (hit.GetComponentInParent<RoadHazard>() != null || hit.GetComponentInParent<MotorRushHazard>() != null) return true;
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
