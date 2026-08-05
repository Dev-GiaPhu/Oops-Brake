using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyRoad
{
    public sealed class EmergencyRoadMenuView : MonoBehaviour
    {
        [Header("WORLD COMPOSITION")]
        [Tooltip("Horizontal screen position for the podium. 0.375 is the center of the left 75% when UI occupies the right 25%.")]
        [Range(.25f,.5f)] public float worldContentCenterX=.375f;
        [Header("TEXT (TMP)")] public TMP_Text vehicleName, wallet, price;
        [Header("BUTTONS")] public Button previous, next, vehicleAction, play, selectVehicle, garageBack, settingsOpen, quit, sideCollision, controlsOpen, settingsClose, controlsBack;
        [Header("SLIDERS")] public Slider music, sfx;
        [Header("PANELS")] public GameObject mainMenuPanel, garagePanel, settingsPanel, controlsPanel;
    }
}
