using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyRoad
{
    public sealed class MotorLaneWarningView : MonoBehaviour
    {
        [System.Serializable]
        private sealed class LaneSlot
        {
            public Image image = null;
            public TMP_Text countdown = null;
        }

        [Header("LEFT / CENTER / RIGHT - DRAG SCENE OBJECTS HERE")]
        [SerializeField] private LaneSlot left = new();
        [SerializeField] private LaneSlot center = new();
        [SerializeField] private LaneSlot right = new();
        [Header("BLINK")]
        [SerializeField, Min(1f)] private float blinkSpeed = 8f;
        [SerializeField, Range(.05f, 1f)] private float minimumAlpha = .2f;
        private LaneSlot[] slots;

        public bool IsConfigured => left.image != null && left.countdown != null &&
                                    center.image != null && center.countdown != null &&
                                    right.image != null && right.countdown != null;

        private void Awake()
        {
            slots = new[] { left, center, right };
            Hide();
        }

        public void Show(int lane, float secondsRemaining)
        {
            EnsureSlots();
            int selected = Mathf.Clamp(lane + 1, 0, 2);
            for (int i = 0; i < slots.Length; i++)
            {
                LaneSlot slot = slots[i];
                if (slot.image == null) continue;
                bool visible = i == selected;
                slot.image.gameObject.SetActive(visible);
                if (!visible) continue;
                Color color = slot.image.color;
                color.a = Mathf.Lerp(minimumAlpha, 1f, .5f + .5f * Mathf.Sin(Time.unscaledTime * blinkSpeed));
                slot.image.color = color;
                if (slot.countdown != null) slot.countdown.text = $"{Mathf.Max(0f, secondsRemaining):0.0}s";
            }
        }

        public void Hide()
        {
            EnsureSlots();
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].image != null) slots[i].image.gameObject.SetActive(false);
        }

        private void EnsureSlots()
        {
            if (slots == null || slots.Length != 3) slots = new[] { left, center, right };
        }
    }
}
