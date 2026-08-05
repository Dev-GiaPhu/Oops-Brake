using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyRoad
{
    [DefaultExecutionOrder(1000)]
    public sealed class EmergencyRoadTrafficCollisionRules : MonoBehaviour
    {
        private const float ScanInterval = 0.1f;
        private float nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ForceFatalSideCollisions();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureInstance();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ForceFatalSideCollisions();
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (FindFirstObjectByType<EmergencyRoadTrafficCollisionRules>() != null) return;
            var root = new GameObject("Emergency Road Mandatory Collision Rules");
            DontDestroyOnLoad(root);
            root.AddComponent<EmergencyRoadTrafficCollisionRules>();
        }

        private static void ForceFatalSideCollisions()
        {
            if (!EmergencyRoadProfile.Current.sideCollisionEnabled) return;
            EmergencyRoadProfile.Current.sideCollisionEnabled = false;
            EmergencyRoadProfile.Save();
        }

        private void Update()
        {
            ForceFatalSideCollisions();
            if (Time.unscaledTime < nextScanTime) return;
            nextScanTime = Time.unscaledTime + ScanInterval;
            HideObsoleteSideCollisionOption();
            AttachTrafficCrashResponders();
        }

        private static void HideObsoleteSideCollisionOption()
        {
            var view = FindFirstObjectByType<EmergencyRoadMenuView>(FindObjectsInactive.Include);
            if (view == null) return;

            if (view.sideCollision != null)
            {
                view.sideCollision.onClick.RemoveAllListeners();
                view.sideCollision.gameObject.SetActive(false);
            }

            if (view.settingsPanel == null) return;
            foreach (var label in view.settingsPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label == null || string.IsNullOrWhiteSpace(label.text)) continue;
                if (label.text.Trim().Equals("VA CHẠM BÊN HÔNG", System.StringComparison.OrdinalIgnoreCase))
                    label.gameObject.SetActive(false);
            }
        }

        private static void AttachTrafficCrashResponders()
        {
            foreach (var traffic in FindObjectsByType<SameDirectionTraffic>(FindObjectsSortMode.None))
            {
                if (traffic == null) continue;
                var responder = traffic.GetComponent<EmergencyTrafficCrashResponder>();
                if (responder == null) responder = traffic.gameObject.AddComponent<EmergencyTrafficCrashResponder>();
                responder.Configure(scrollWithRoad: true);
            }

            foreach (var traffic in FindObjectsByType<SideCrossingHazard>(FindObjectsSortMode.None))
            {
                if (traffic == null) continue;
                var responder = traffic.GetComponent<EmergencyTrafficCrashResponder>();
                if (responder == null) responder = traffic.gameObject.AddComponent<EmergencyTrafficCrashResponder>();
                responder.Configure(scrollWithRoad: false);
            }
        }
    }

    [DefaultExecutionOrder(2000)]
    public sealed class EmergencyTrafficCrashResponder : MonoBehaviour
    {
        private const int DetectionBufferSize = 32;
        private static readonly Collider[] OverlapBuffer = new Collider[DetectionBufferSize];
        private static readonly RaycastHit[] CastBuffer = new RaycastHit[DetectionBufferSize];

        private BoxCollider hitbox;
        private EmergencyRoadGame game;
        private bool initialized;
        private bool scrollWithRoad;
        private bool crashed;
        private Vector3 previousCenter;
        private Quaternion previousRotation;

        public bool Crashed => crashed;

        public void Configure(bool scrollWithRoad)
        {
            if (crashed) return;
            this.scrollWithRoad = scrollWithRoad;
            CacheReferences();
            previousCenter = WorldCenter;
            previousRotation = WorldRotation;
            initialized = true;
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void Start()
        {
            if (!initialized) Configure(GetComponent<SameDirectionTraffic>() != null);
        }

        private void CacheReferences()
        {
            if (hitbox == null) hitbox = GetComponent<BoxCollider>() ?? GetComponentInChildren<BoxCollider>();
            if (game == null) game = FindFirstObjectByType<EmergencyRoadGame>();
        }

        private void LateUpdate()
        {
            if (!initialized) Configure(GetComponent<SameDirectionTraffic>() != null);
            if (crashed)
            {
                FollowRoadAfterCrash();
                return;
            }

            if (hitbox == null || !hitbox.enabled) return;

            Vector3 currentCenter = WorldCenter;
            Quaternion currentRotation = WorldRotation;
            Vector3 movement = currentCenter - previousCenter;

            if (movement.sqrMagnitude > 0.0001f)
                DetectAlongMovement(previousCenter, previousRotation, movement);

            if (!crashed)
                DetectCurrentOverlap(currentCenter, currentRotation);

            previousCenter = currentCenter;
            previousRotation = currentRotation;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryResolveTrafficImpact(other);
        }

        private void DetectAlongMovement(Vector3 origin, Quaternion orientation, Vector3 movement)
        {
            float distance = movement.magnitude;
            if (distance <= 0.001f) return;

            int count = Physics.BoxCastNonAlloc(
                origin,
                WorldHalfExtents,
                movement / distance,
                CastBuffer,
                orientation,
                distance + 0.12f,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count && !crashed; i++)
                TryResolveTrafficImpact(CastBuffer[i].collider);
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

            for (int i = 0; i < count && !crashed; i++)
                TryResolveTrafficImpact(OverlapBuffer[i]);
        }

        private void TryResolveTrafficImpact(Collider otherCollider)
        {
            if (crashed || otherCollider == null || otherCollider.transform.IsChildOf(transform)) return;

            var other = otherCollider.GetComponentInParent<EmergencyTrafficCrashResponder>();
            if (other == null || other == this) return;

            Vector3 impactDirection = other.WorldCenter - WorldCenter;
            if (impactDirection.sqrMagnitude < 0.001f)
                impactDirection = transform.right;

            if (other.crashed)
            {
                Crash(impactDirection);
                return;
            }

            Crash(impactDirection);
            other.Crash(-impactDirection);
        }

        private void Crash(Vector3 impactDirection)
        {
            if (crashed) return;
            crashed = true;
            if (impactDirection.sqrMagnitude < 0.001f) impactDirection = Vector3.forward;
            impactDirection.Normalize();

            var sameDirection = GetComponent<SameDirectionTraffic>();
            if (sameDirection != null)
            {
                sameDirection.enabled = false;
                Destroy(sameDirection);
            }

            var crossing = GetComponent<SideCrossingHazard>();
            if (crossing != null)
            {
                crossing.enabled = false;
                Destroy(crossing);
            }

            var body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            DeformMeshes(impactDirection);
            ApplyPermanentCrashPose(impactDirection);

            Vector3 localImpact = transform.InverseTransformDirection(impactDirection);
            EmergencyImpactVfx.Attach(
                transform,
                new Vector3(Mathf.Clamp(localImpact.x, -1f, 1f) * 0.85f, 0.72f, Mathf.Clamp(localImpact.z, -1f, 1f) * 1.35f));
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
            float roll = -Mathf.Sign(Mathf.Abs(localImpact.x) < 0.01f ? 1f : localImpact.x) * Random.Range(4f, 8f);
            float pitch = Mathf.Sign(localImpact.z) * Random.Range(2f, 5f);
            transform.localRotation *= Quaternion.Euler(pitch, Random.Range(-3f, 3f), roll);
        }

        private void DeformMeshes(Vector3 impactDirection)
        {
            foreach (var filter in GetComponentsInChildren<MeshFilter>())
            {
                try
                {
                    Mesh source = filter.sharedMesh;
                    if (source == null) continue;

                    Mesh mesh = Instantiate(source);
                    mesh.name = source.name + " - Traffic Crash Deformed";
                    Vector3[] vertices = mesh.vertices;
                    Bounds bounds = mesh.bounds;
                    Vector3 extents = bounds.extents;
                    Vector3 localImpact = filter.transform.InverseTransformDirection(impactDirection).normalized;
                    float impactExtent = Mathf.Abs(localImpact.x) * extents.x + Mathf.Abs(localImpact.y) * extents.y + Mathf.Abs(localImpact.z) * extents.z;
                    float dentDepth = Mathf.Max(0.07f, impactExtent * 0.34f);
                    Vector3 creaseAxis = Vector3.Cross(Vector3.up, localImpact).normalized;

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 offset = vertices[i] - bounds.center;
                        Vector3 normalized = new Vector3(
                            offset.x / Mathf.Max(0.01f, extents.x),
                            offset.y / Mathf.Max(0.01f, extents.y),
                            offset.z / Mathf.Max(0.01f, extents.z));

                        float facing = Vector3.Dot(normalized, localImpact);
                        float influence = Mathf.Pow(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.92f, facing)), 1.2f);
                        float crease = Mathf.Sin(i * 12.9898f + offset.y * 13.7f);
                        vertices[i] -= localImpact * (dentDepth * influence * (0.78f + 0.14f * crease));
                        vertices[i] += creaseAxis * (dentDepth * 0.1f * influence * crease);
                        vertices[i].y -= extents.y * 0.15f * influence;
                    }

                    mesh.vertices = vertices;
                    mesh.RecalculateBounds();
                    mesh.RecalculateNormals();
                    filter.sharedMesh = mesh;
                }
                catch (UnityException)
                {
                    // Some imported meshes may have Read/Write disabled; fire and the permanent crash pose still remain visible.
                }
            }
        }

        private Vector3 WorldCenter => hitbox != null ? hitbox.transform.TransformPoint(hitbox.center) : transform.position;
        private Quaternion WorldRotation => hitbox != null ? hitbox.transform.rotation : transform.rotation;

        private Vector3 WorldHalfExtents
        {
            get
            {
                if (hitbox == null) return new Vector3(1.2f, 0.9f, 2f);
                Vector3 scale = hitbox.transform.lossyScale;
                Vector3 half = hitbox.size * 0.5f;
                return new Vector3(
                    Mathf.Abs(half.x * scale.x) + 0.08f,
                    Mathf.Abs(half.y * scale.y) + 0.04f,
                    Mathf.Abs(half.z * scale.z) + 0.08f);
            }
        }
    }
}
