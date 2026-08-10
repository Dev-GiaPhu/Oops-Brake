using UnityEngine;

namespace EmergencyRoad
{
    public sealed class CoinSpinner : MonoBehaviour
    {
        private float baseY;
        private float phase;

        private void Awake()
        {
            baseY = transform.localPosition.y;
            phase = Random.value * 6.28f;
        }

        private void Update()
        {
            transform.Rotate(0, 180f * Time.deltaTime, 0, Space.Self);
            Vector3 p = transform.localPosition;
            p.y = baseY + Mathf.Sin(Time.time * 3.2f + phase) * .13f;
            transform.localPosition = p;
        }
    }
}
