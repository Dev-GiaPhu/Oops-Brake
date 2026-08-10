using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyRoad
{
    public enum EmergencyRoadAutoTestMode { AvoidObstacles, ValidateCollisions, Alternate }

    /// <summary>Scene-authored autonomous smoke tester. It never searches the scene at runtime.</summary>
    public sealed class EmergencyRoadAutoTester : MonoBehaviour
    {
        [Header("SCENE REFERENCE - DRAG DIRECTLY")]
        [SerializeField] private EmergencyRoadGame game;

        [Header("RUN")]
        [SerializeField] private bool runOnPlay;
        [SerializeField, Tooltip("-1 repeats until Play Mode is stopped or this component is disabled.")] private int repeatCount = 10;
        [SerializeField] private EmergencyRoadAutoTestMode mode = EmergencyRoadAutoTestMode.Alternate;
        [SerializeField, Min(5f)] private float secondsPerRun = 30f;
        [SerializeField, Min(.05f)] private float decisionInterval = .12f;
        [SerializeField, Min(8f)] private float scanDistance = 38f;

        [Header("REPORTING")]
        [SerializeField] private bool recordErrorsOnly = true;
        [SerializeField] private bool requestRepairAfterFinalRun = true;
        [SerializeField] private string reportFileName = "EmergencyRoadAutoTestReport.json";
        [SerializeField, Min(20)] private int maxUniqueConsoleIssues = 250;

        private static Session session;
        private EmergencyVehicleController driver;
        private float nextDecision;
        private bool reloading;

        [Serializable] private sealed class Failure { public int run; public string test; public string message; public float distance; }
        [Serializable] private sealed class ConsoleIssue { public int firstRun; public string severity; public string message; public string stackTrace; public int occurrences = 1; }
        [Serializable] private sealed class Report { public int completedRuns; public int passedRuns; public int failedRuns; public int warningCount; public int errorCount; public bool infinite; public List<Failure> failures = new(); public List<ConsoleIssue> consoleIssues = new(); }
        private sealed class Session { public bool active; public int completed; public int passed; public readonly List<Failure> failures = new(); public readonly List<ConsoleIssue> consoleIssues = new(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => session = null;

        private void Awake()
        {
            if (!runOnPlay || !enabled) return;
            session ??= new Session { active = true };
            Application.logMessageReceived -= CaptureConsoleIssue;
            Application.logMessageReceived += CaptureConsoleIssue;
        }

        private IEnumerator Start()
        {
            if (!runOnPlay || !enabled) yield break;
            if (session != null && !session.active) yield break;
            session ??= new Session { active = true };

            if (game == null)
            {
                FinishRun(false, "Khởi tạo", "EmergencyRoadAutoTester chưa được kéo EmergencyRoadGame vào Inspector.");
                yield break;
            }

            yield return null;
            driver = game.Player;
            if (driver == null)
            {
                FinishRun(false, "Khởi tạo", "Game Controller chưa khởi tạo Player.");
                yield break;
            }

            yield return RunOneTest();
        }

        private IEnumerator RunOneTest()
        {
            int runNumber = session.completed + 1;
            bool collisionTest = mode == EmergencyRoadAutoTestMode.ValidateCollisions || (mode == EmergencyRoadAutoTestMode.Alternate && runNumber % 2 == 0);
            float elapsed = 0f;
            while (elapsed < secondsPerRun && game != null && !game.Ended)
            {
                elapsed += Time.unscaledDeltaTime;
                if (Time.unscaledTime >= nextDecision)
                {
                    nextDecision = Time.unscaledTime + decisionInterval;
                    Drive(collisionTest);
                }
                yield return null;
            }

            bool collisionObserved = game != null && game.Ended;
            if (collisionTest)
                FinishRun(collisionObserved, "Kiểm tra va chạm", collisionObserved ? null : "Không ghi nhận Game Over sau khi chủ động hướng xe vào chướng ngại.");
            else
                FinishRun(!collisionObserved, "Kiểm tra né chướng ngại", collisionObserved ? "Xe tự lái va chạm trong chế độ né chướng ngại." : null);
        }

        private void Drive(bool seekCollision)
        {
            float[] nearest = { float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity };
            IReadOnlyList<RoadHazard> hazards = game.ActiveHazards;
            for (int i = 0; i < hazards.Count; i++)
            {
                RoadHazard hazard = hazards[i];
                if (hazard == null) continue;
                float z = hazard.transform.position.z - driver.transform.position.z;
                if (z < -.5f || z > scanDistance) continue;
                int lane = Mathf.Clamp(Mathf.RoundToInt(hazard.transform.position.x / EmergencyRoadGame.LaneWidth), -1, 1);
                nearest[lane + 1] = Mathf.Min(nearest[lane + 1], z);
            }

            if (!seekCollision)
            {
                IReadOnlyList<MotorRushHazard> motors = game.ActiveMotorHazards;
                for (int i = 0; i < motors.Count; i++)
                {
                    MotorRushHazard motor = motors[i];
                    if (motor == null) continue;
                    float z = motor.transform.position.z - driver.transform.position.z;
                    if (z < -30f || z > scanDistance) continue;
                    int lane = Mathf.Clamp(Mathf.RoundToInt(motor.transform.position.x / EmergencyRoadGame.LaneWidth), -1, 1);
                    nearest[lane + 1] = Mathf.Min(nearest[lane + 1], Mathf.Max(0, z));
                }
            }

            int target = driver.CurrentLane;
            if (seekCollision)
            {
                float best = float.PositiveInfinity;
                for (int i = 0; i < 3; i++) if (nearest[i] < best) { best = nearest[i]; target = i - 1; }
            }
            else
            {
                float safest = -1f;
                for (int i = 0; i < 3; i++) if (nearest[i] > safest) { safest = nearest[i]; target = i - 1; }
            }
            driver.AutomationMoveTowardLane(target, seekCollision);
        }

        private void FinishRun(bool passed, string test, string failureMessage)
        {
            session.completed++;
            if (passed) session.passed++;
            else session.failures.Add(new Failure { run = session.completed, test = test, message = failureMessage, distance = game != null ? game.Distance : 0 });
            if (!recordErrorsOnly || !passed)
                Debug.Log(passed ? $"[AUTO TEST] Lượt {session.completed}: PASS - {test}" : $"[AUTO TEST] Lượt {session.completed}: FAIL - {failureMessage}");
            bool final = repeatCount >= 0 && session.completed >= Mathf.Max(1, repeatCount);
            WriteReport(final);
            if (!final && enabled && runOnPlay) StartCoroutine(ReloadForNextRun());
            else session.active = false;
        }

        private IEnumerator ReloadForNextRun()
        {
            yield return new WaitForSecondsRealtime(.35f);
            reloading = true;
            SceneManager.LoadScene("Game");
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= CaptureConsoleIssue;
            if (!reloading && session != null) session.active = false;
        }

        private void CaptureConsoleIssue(string condition, string stackTrace, LogType type)
        {
            if (session == null || condition.StartsWith("[AUTO TEST]", StringComparison.Ordinal) || type == LogType.Log) return;
            string severity = type == LogType.Warning ? "Warning" : type.ToString();
            foreach (ConsoleIssue issue in session.consoleIssues)
            {
                if (issue.severity != severity || issue.message != condition || issue.stackTrace != stackTrace) continue;
                issue.occurrences++;
                return;
            }
            if (session.consoleIssues.Count >= Mathf.Max(20, maxUniqueConsoleIssues)) return;
            session.consoleIssues.Add(new ConsoleIssue { firstRun = session.completed + 1, severity = severity, message = condition, stackTrace = stackTrace });
        }

        private void WriteReport(bool final)
        {
            int warnings = 0;
            int errors = 0;
            foreach (ConsoleIssue issue in session.consoleIssues)
            {
                if (issue.severity == "Warning") warnings += issue.occurrences;
                else errors += issue.occurrences;
            }
            var report = new Report
            {
                completedRuns = session.completed,
                passedRuns = session.passed,
                failedRuns = session.failures.Count,
                warningCount = warnings,
                errorCount = errors,
                infinite = repeatCount < 0,
                failures = new List<Failure>(session.failures),
                consoleIssues = new List<ConsoleIssue>(session.consoleIssues)
            };
            string directory = Application.persistentDataPath;
            string reportPath = Path.Combine(directory, string.IsNullOrWhiteSpace(reportFileName) ? "EmergencyRoadAutoTestReport.json" : reportFileName);
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            if (final && requestRepairAfterFinalRun && (session.failures.Count > 0 || session.consoleIssues.Count > 0))
            {
                string requestPath = Path.Combine(directory, "EmergencyRoadAutoTest_REPAIR_REQUEST.json");
                File.WriteAllText(requestPath, JsonUtility.ToJson(report, true));
                Debug.LogError($"[AUTO TEST] Completed {session.completed} runs: {session.failures.Count} gameplay failures, {warnings} warnings, {errors} errors/exceptions. Repair request: {requestPath}");
            }
            else if (final)
            {
                Debug.Log($"[AUTO TEST] Completed {session.completed} runs. Report: {reportPath}");
            }
        }
    }
}
