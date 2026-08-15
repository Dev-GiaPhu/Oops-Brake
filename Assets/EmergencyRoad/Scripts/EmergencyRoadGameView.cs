using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyRoad
{
    public sealed class EmergencyRoadGameView : MonoBehaviour
    {
        [Header("TEXT (TMP)")] public TMP_Text score, coins, hazardAlert, gameOverScore;
        [Header("BUTTONS")] public Button pause, resume, restartFromPause, menuFromPause, retry, garage;
        [Header("PANELS")] public GameObject pausePanel, gameOverPanel;
        [Header("FIRST PERSON UI")] public EmergencyFirstPersonMirrorView firstPersonMirrors;
    }
}
