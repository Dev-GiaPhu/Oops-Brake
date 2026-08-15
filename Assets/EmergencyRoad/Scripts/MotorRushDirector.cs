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

        public void Configure(EmergencyRoadGame owner, GameObject warningPrefab, GameObject hazardPrefab)
        {
            game = owner;
            warningLinePrefab = warningPrefab;
            motorRushHazardPrefab = hazardPrefab;
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
            float elapsed = 0f;
            game.SetHazardAlert("MÔ TÔ SẮP XUẤT HIỆN");
            while (elapsed < warningDuration)
            {
                elapsed += Time.deltaTime;
                laneWarningView.Show(targetLane, warningDuration - elapsed);
                yield return null;
            }

            laneWarningView.Hide();
            game.SetHazardAlert("");
            GameObject motorObject = Instantiate(motorRushHazardPrefab, transform, false);
            MotorRushHazard motor = motorObject.GetComponent<MotorRushHazard>();
            if (motor == null)
            {
                Debug.LogError("[Emergency Road] Motor Rush Hazard Prefab phải chứa sẵn MotorRushHazard.", motorObject);
                Destroy(motorObject);
            }
            else
            {
                motor.InitializeLane(targetLane, game, game.GameplayCamera);
                Debug.Assert(motor.TargetLane == targetLane, "Motor warning lane and spawned motorcycle lane must match.", motor);
                game.RegisterMotor(motor);
            }

            while (motor != null) yield return null;
            timer = Random.Range(15f, 23f);
            active = false;
        }

        private void OnDisable()
        {
            if (laneWarningView != null) laneWarningView.Hide();
            active = false;
        }
    }

}
