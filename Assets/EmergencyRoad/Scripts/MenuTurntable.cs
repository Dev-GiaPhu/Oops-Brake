using UnityEngine;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class MenuTurntable : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float rotationSpeed = 24f;

        private void Update()
        {
            transform.Rotate(0f, rotationSpeed * Time.unscaledDeltaTime, 0f, Space.Self);
        }
    }
}
