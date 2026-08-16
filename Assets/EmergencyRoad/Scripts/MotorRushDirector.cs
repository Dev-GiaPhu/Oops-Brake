using System.Collections;
using UnityEngine;

namespace EmergencyRoad
{
    public sealed class MotorRushDirector : MonoBehaviour
    {
        [Header("DIRECT REFERENCES - DRAG IN INSPECTOR")]
        [SerializeField] private EmergencyRoadGame game;
        [SerializeField] private GameObject warningLinePrefab;
        [SerializeField] private GameObject motorRushHazardPrefab;
        [SerializeField] private MotorLaneWarningView laneWarningView;

        private float timer;
        private bool active;
        private GameObject motorcycleVisualPrefab;
        private int reservedLane = int.MinValue;

        public void Configure(EmergencyRoadGame owner, GameObject visualPrefab)
        {
            game = owner;
            motorcycleVisualPrefab = visualPrefab;
            timer = Random.Range(10f, 15f);
        }

        private void Update()
        {
            if (game == null || game.Player == null || Time.timeScale == 0f || active) return;
            timer -= Time.deltaTime;
            if (timer <= 0f) StartCoroutine(RunEvent());
        }

        private IEnumerator RunEvent()
        {
            if (motorRushHazardPrefab == null || laneWarningView == null || !laneWarningView.IsConfigured)
            {
                Debug.LogError("[Emergency Road] MotorRushDirector thiếu Motor Rush Hazard Prefab hoặc Motor Lane Warning View chưa gắn đủ 3 Image + TMP_Text.", this);
                timer = 10f;
                yield break;
            }

            EmergencyRoadGameplaySettings tuning = game.Settings;
            float warningDuration = tuning != null ? tuning.motorWarningTrackTime : 3f;
            float motorSpeed = tuning != null ? tuning.motorSpeed : 34f;
            if (!game.TryPlanMotorRush(warningDuration, motorSpeed, out int targetLane))
            {
                timer = tuning != null ? tuning.motorPlanningRetryDelay : 1f;
                yield break;
            }

            active = true;
            reservedLane = targetLane;
            game.ReserveMotorLane(targetLane);
            float elapsed = 0f;
            game.SetHazardAlert("MÔ TÔ SẮP XUẤT HIỆN");
            while (elapsed < warningDuration)
            {
                if (game == null || game.Ended)
                {
                    CancelWarning();
                    yield break;
                }
                elapsed += Time.deltaTime;
                laneWarningView.Show(targetLane, warningDuration - elapsed);
                yield return null;
            }

            laneWarningView.Hide();
            game.SetHazardAlert("");
            float catchTime = Mathf.Max(.1f, (game.Player.transform.position.z + 18f) / Mathf.Max(1f, motorSpeed));
            float projectedZ = game.Player.transform.position.z + game.CurrentSpeed * catchTime;
            float obstacleSafety = tuning != null ? tuning.motorObstacleSafetyDistance : 9f;
            if (!game.IsMotorLaneCorridorClear(targetLane, projectedZ - obstacleSafety, projectedZ + obstacleSafety))
            {
                ReleaseReservation();
                timer = tuning != null ? tuning.motorPlanningRetryDelay : 1f;
                active = false;
                yield break;
            }
            GameObject motorObject = Instantiate(motorRushHazardPrefab, transform, false);
            MotorRushHazard motor = motorObject.GetComponent<MotorRushHazard>();
            if (motor == null)
            {
                Debug.LogError("[Emergency Road] Motor Rush Hazard Prefab phải chứa sẵn MotorRushHazard.", motorObject);
                Destroy(motorObject);
            }
            else
            {
                motor.InitializeLane(targetLane, game, game.GameplayCamera, motorcycleVisualPrefab);
                Debug.Assert(motor.TargetLane == targetLane, "Motor warning lane and spawned motorcycle lane must match.", motor);
                game.RegisterMotor(motor);
            }

            while (motor != null && game != null && !game.Ended) yield return null;
            ReleaseReservation();
            timer = Random.Range(15f, 23f);
            active = false;
        }

        private void CancelWarning()
        {
            if (laneWarningView != null) laneWarningView.Hide();
            if (game != null) game.SetHazardAlert("");
            ReleaseReservation();
            active = false;
        }

        private void ReleaseReservation()
        {
            if (reservedLane < -1 || reservedLane > 1) return;
            if (game != null) game.ReleaseMotorLane(reservedLane);
            reservedLane = int.MinValue;
        }

        private void OnDisable()
        {
            if (laneWarningView != null) laneWarningView.Hide();
            ReleaseReservation();
            active = false;
        }
    }

}
