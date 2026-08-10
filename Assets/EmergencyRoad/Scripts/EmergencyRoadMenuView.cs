using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyRoad
{
    public sealed class EmergencyRoadMenuView : MonoBehaviour
    {
        [Header("WORLD - DRAG DIRECTLY")]
        [Tooltip("Empty GameObject nằm đúng tâm bàn xoay.")]
        public Transform vehiclePreviewPivot;

        [Header("TEXT (TMP) - DRAG DIRECTLY")]
        public TMP_Text vehicleName;
        public TMP_Text wallet;
        public TMP_Text price;
        public TMP_Text vehicleActionLabel;
        public TMP_Text sideCollisionLabel;

        [Header("BUTTONS - DRAG DIRECTLY")]
        public Button previous;
        public Button next;
        public Button vehicleAction;
        public Button play;
        public Button selectVehicle;
        public Button garageBack;
        public Button settingsOpen;
        public Button quit;
        public Button sideCollision;
        public Button controlsOpen;
        public Button settingsClose;
        public Button controlsBack;

        [Header("SLIDERS - DRAG DIRECTLY")]
        public Slider music;
        public Slider sfx;

        [Header("PANELS - DRAG DIRECTLY")]
        public GameObject mainMenuPanel;
        public GameObject garagePanel;
        public GameObject settingsPanel;
        public GameObject controlsPanel;
    }
}
