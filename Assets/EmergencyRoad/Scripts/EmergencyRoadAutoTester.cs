using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyRoad
{
    public enum EmergencyRoadAutoTestMode { AvoidObstacles, ValidateCollisions, Alternate }

    /// <summary>Scene-authored autonomous smoke tester. It never runs unless Run On Play is enabled.</summary>
    public sealed class EmergencyRoadAutoTester : MonoBehaviour
    {
        [Header("RUN")]
        [SerializeField] private bool runOnPlay;
        [SerializeField, Tooltip("-1 repeats until Play Mode is stopped or this component is disabled.")] private int repeatCount=10;
        [SerializeField] private EmergencyRoadAutoTestMode mode=EmergencyRoadAutoTestMode.Alternate;
        [SerializeField, Min(5f)] private float secondsPerRun=30f;
        [SerializeField, Min(.05f)] private float decisionInterval=.12f;
        [SerializeField, Min(8f)] private float scanDistance=38f;

        [Header("REPORTING")]
        [SerializeField, Tooltip("Suppress passing-run messages and retain only detected failures.")] private bool recordErrorsOnly=true;
        [SerializeField, Tooltip("On the final run, write a Codex repair-request JSON beside the report. Runtime does not rewrite C# source files.")] private bool requestRepairAfterFinalRun=true;
        [SerializeField] private string reportFileName="EmergencyRoadAutoTestReport.json";

        private static Session session;
        private EmergencyRoadGame game;
        private EmergencyVehicleController driver;
        private float nextDecision;
        private bool reloading;

        [Serializable] private sealed class Failure { public int run; public string test; public string message; public float distance; }
        [Serializable] private sealed class Report { public int completedRuns; public int passedRuns; public int failedRuns; public bool infinite; public List<Failure> failures=new(); }
        private sealed class Session { public bool active; public int completed; public int passed; public readonly List<Failure> failures=new(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()=>session=null;

        private IEnumerator Start()
        {
            if(!runOnPlay||!enabled)yield break;
            if(session!=null&&!session.active)yield break;
            session??=new Session{active=true};
            yield return new WaitUntil(()=>Object.FindFirstObjectByType<EmergencyRoadGame>()!=null);
            game=Object.FindFirstObjectByType<EmergencyRoadGame>();driver=game!=null?game.Player:null;
            if(game==null||driver==null){FinishRun(false,"Khởi tạo","Không tìm thấy EmergencyRoadGame hoặc Player.");yield break;}
            yield return RunOneTest();
        }

        private IEnumerator RunOneTest()
        {
            int runNumber=session.completed+1;bool collisionTest=mode==EmergencyRoadAutoTestMode.ValidateCollisions||(mode==EmergencyRoadAutoTestMode.Alternate&&runNumber%2==0);
            float elapsed=0;bool collisionObserved=false;
            while(elapsed<secondsPerRun&&game!=null&&!game.Ended)
            {
                elapsed+=Time.unscaledDeltaTime;
                if(Time.unscaledTime>=nextDecision){nextDecision=Time.unscaledTime+decisionInterval;Drive(collisionTest);}
                yield return null;
            }
            collisionObserved=game!=null&&game.Ended;
            if(collisionTest)FinishRun(collisionObserved,"Kiểm tra va chạm",collisionObserved?null:"Không ghi nhận Game Over sau khi chủ động hướng xe vào chướng ngại.");
            else FinishRun(!collisionObserved,"Kiểm tra né chướng ngại",collisionObserved?"Xe tự lái va chạm trong chế độ né chướng ngại.":null);
        }

        private void Drive(bool seekCollision)
        {
            var hazards=Object.FindObjectsByType<RoadHazard>(FindObjectsSortMode.None);float[] nearest={float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity};
            foreach(var hazard in hazards)
            {
                if(hazard==null)continue;float z=hazard.transform.position.z-driver.transform.position.z;if(z<-.5f||z>scanDistance)continue;int lane=Mathf.Clamp(Mathf.RoundToInt(hazard.transform.position.x/EmergencyRoadGame.LaneWidth),-1,1);nearest[lane+1]=Mathf.Min(nearest[lane+1],z);
            }
            int target=driver.CurrentLane;
            if(seekCollision){float best=float.PositiveInfinity;for(int i=0;i<3;i++)if(nearest[i]<best){best=nearest[i];target=i-1;}}
            else{float safest=-1;for(int i=0;i<3;i++)if(nearest[i]>safest){safest=nearest[i];target=i-1;}}
            driver.AutomationMoveTowardLane(target);
        }

        private void FinishRun(bool passed,string test,string failureMessage)
        {
            session.completed++;if(passed)session.passed++;else session.failures.Add(new Failure{run=session.completed,test=test,message=failureMessage,distance=game!=null?game.Distance:0});
            if(!recordErrorsOnly||!passed)Debug.Log(passed?$"[AUTO TEST] Lượt {session.completed}: PASS - {test}":$"[AUTO TEST] Lượt {session.completed}: FAIL - {failureMessage}");
            bool final=repeatCount>=0&&session.completed>=Mathf.Max(1,repeatCount);
            WriteReport(final);
            if(!final&&enabled&&runOnPlay)StartCoroutine(ReloadForNextRun());else session.active=false;
        }

        private IEnumerator ReloadForNextRun(){yield return new WaitForSecondsRealtime(.35f);reloading=true;SceneManager.LoadScene("Game");}

        private void OnDisable()
        {
            if(!reloading&&session!=null)session.active=false;
        }

        private void WriteReport(bool final)
        {
            var report=new Report{completedRuns=session.completed,passedRuns=session.passed,failedRuns=session.failures.Count,infinite=repeatCount<0,failures=new List<Failure>(session.failures)};
            string directory=Application.persistentDataPath;string reportPath=Path.Combine(directory,string.IsNullOrWhiteSpace(reportFileName)?"EmergencyRoadAutoTestReport.json":reportFileName);File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
            if(final&&requestRepairAfterFinalRun&&session.failures.Count>0){string requestPath=Path.Combine(directory,"EmergencyRoadAutoTest_REPAIR_REQUEST.json");File.WriteAllText(requestPath,JsonUtility.ToJson(report,true));Debug.LogError($"[AUTO TEST] Hoàn tất {session.completed} lượt, phát hiện {session.failures.Count} lỗi. Repair request: {requestPath}");}
            else if(final)Debug.Log($"[AUTO TEST] Hoàn tất {session.completed} lượt. Báo cáo: {reportPath}");
        }
    }
}
