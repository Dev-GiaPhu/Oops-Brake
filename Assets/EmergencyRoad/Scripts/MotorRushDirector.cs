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
            if (warningLinePrefab == null || motorRushHazardPrefab == null)
            {
                Debug.LogError("[Emergency Road] MotorRushDirector thiếu Warning Line Prefab hoặc Motor Rush Hazard Prefab trong Inspector.", this);
                timer = 10f;
                yield break;
            }

            active = true;
            GameObject warning = Instantiate(warningLinePrefab, transform, false);
            LineRenderer line = warning.GetComponent<LineRenderer>();
            if (line == null)
            {
                Debug.LogError("[Emergency Road] Motor Warning prefab phải chứa sẵn LineRenderer.", warning);
                Destroy(warning);
                active = false;
                yield break;
            }

            Transform playerTransform = game.Player.transform;
            float targetX = playerTransform.position.x;
            float t = 0f;
            game.SetHazardAlert("WARNING: MOTORCYCLE INCOMING");
            while (t < 2.2f)
            {
                t += Time.unscaledDeltaTime;
                targetX = Mathf.Lerp(targetX, playerTransform.position.x, 1f - Mathf.Exp(-3.2f * Time.unscaledDeltaTime));
                line.widthMultiplier = .22f + Mathf.Sin(t * 18f) * .08f;
                UpdateWarningPath(line, targetX);
                yield return null;
            }

            game.SetHazardAlert("DIRECTION LOCKED — DODGE NOW!");
            line.startColor = Color.red;
            line.endColor = new Color(1f, .05f, .01f, .7f);
            line.widthMultiplier = .42f;
            yield return new WaitForSecondsRealtime(.55f);

            GameObject motorObject = Instantiate(motorRushHazardPrefab, transform, false);
            MotorRushHazard motor = motorObject.GetComponent<MotorRushHazard>();
            if (motor == null)
            {
                Debug.LogError("[Emergency Road] Motor Rush Hazard Prefab phải chứa sẵn MotorRushHazard.", motorObject);
                Destroy(motorObject);
            }
            else
            {
                motor.Initialize(targetX, game, playerTransform, game.GameplayCamera);
                game.RegisterMotor(motor);
            }

            Destroy(warning);
            game.SetHazardAlert("");
            while (motor != null) yield return null;
            timer = Random.Range(15f, 23f);
            active = false;
        }

        private static void UpdateWarningPath(LineRenderer line, float targetX)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float z = Mathf.Lerp(-18f, 72f, (float)i / (line.positionCount - 1));
                line.SetPosition(i, new Vector3(targetX + Mathf.Sin(z * .28f) * .65f, .08f, z));
            }
        }
    }
}
