using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyRoad
{
    public sealed class EmergencyRoadMenuView : MonoBehaviour
    {
        [Header("TEXT (TMP)")] public TMP_Text vehicleName, wallet, price;
        [Header("BUTTONS")] public Button previous, next, vehicleAction, play, selectVehicle, garageBack, settingsOpen, quit, sideCollision, controlsOpen, settingsClose, controlsBack;
        [Header("SLIDERS")] public Slider music, sfx;
        [Header("PANELS")] public GameObject mainMenuPanel, garagePanel, settingsPanel, controlsPanel;
    }
}
