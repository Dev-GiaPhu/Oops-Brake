using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyRoad
{
    [DefaultExecutionOrder(1500)]
    public sealed class EmergencyRoadContinuousCoinSpawner : MonoBehaviour
    {
        private const float MinimumSpacing = 17f;
        private const float MaximumSpacing = 25f;
        private const float SpawnAheadDistance = 145f;
        private const float RemoveBehindDistance = -10f;
        private const float LegacyCleanupInterval = 0.25f;

        private readonly List<GameObject> activeCoins = new();
        private EmergencyRoadGame game;
        private float nextLegacyCleanupTime;
        private int previousLane = 99;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureForActiveScene()
        {
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static void EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != "Game") return;
            if (FindFirstObjectByType<EmergencyRoadContinuousCoinSpawner>() != null) return;

            var root = new GameObject("Sparse Continuous Road Coins");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<EmergencyRoadContinuousCoinSpawner>();
        }

        private void Update()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<EmergencyRoadGame>();
                if (game == null) return;

                RemoveLegacyChunkCoins();
                FillRoadAhead();
            }

            if (Time.unscaledTime >= nextLegacyCleanupTime)
            {
                nextLegacyCleanupTime = Time.unscaledTime + LegacyCleanupInterval;
                RemoveLegacyChunkCoins();
            }

            if (game.Ended || Time.timeScale <= 0f) return;

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
                Vector3 candidate = new Vector3(lane * EmergencyRoadGame.LaneWidth, 1.1f, spawnZ);
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
            if (lane != previousLane || Random.value > 0.7f) return lane;
            return ChooseAlternativeLane(lane);
        }

        private static int ChooseAlternativeLane(int currentLane)
        {
            int offset = Random.value < 0.5f ? 1 : 2;
            return ((currentLane + 1 + offset) % 3) - 1;
        }

        private static bool IsBlocked(Vector3 position)
        {
            Collider[] hits = Physics.OverlapBox(
                position,
                new Vector3(1.05f, 0.8f, 2.1f),
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (Collider hit in hits)
            {
                if (hit == null) continue;
                if (hit.GetComponentInParent<RoadHazard>() != null) return true;
                if (hit.GetComponentInParent<MotorRushHazard>() != null) return true;
            }

            return false;
        }

        private void CreateCoin(Vector3 worldPosition)
        {
            GameObject coin;
            EmergencyRoadCatalog catalog = game.Catalog;

            if (catalog != null && catalog.coinPrefab != null)
            {
                coin = Instantiate(catalog.coinPrefab, transform);
                coin.name = "Sparse Continuous Coin";
            }
            else
            {
                coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                coin.name = "Sparse Continuous Coin - Fallback";
                coin.transform.SetParent(transform);
                coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                coin.transform.localScale = new Vector3(0.48f, 0.13f, 0.48f);
                coin.GetComponent<Renderer>().material.color = EmergencyRoadUI.Yellow;
            }

            coin.transform.position = worldPosition;

            Collider[] colliders = coin.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                var sphere = coin.AddComponent<SphereCollider>();
                sphere.radius = 0.55f;
                sphere.isTrigger = true;
            }
            else
            {
                foreach (Collider collider in colliders)
                {
                    collider.enabled = true;
                    collider.isTrigger = true;
                }
            }

            if (coin.GetComponent<RoadPickup>() == null)
                coin.AddComponent<RoadPickup>();

            if (coin.GetComponent<CoinSpinner>() == null)
                coin.AddComponent<CoinSpinner>();

            activeCoins.Add(coin);
        }

        private void RemoveLegacyChunkCoins()
        {
            foreach (RoadPickup pickup in FindObjectsByType<RoadPickup>(FindObjectsSortMode.None))
            {
                if (pickup == null || pickup.transform.IsChildOf(transform)) continue;
                Destroy(pickup.gameObject);
            }
        }
    }
}
