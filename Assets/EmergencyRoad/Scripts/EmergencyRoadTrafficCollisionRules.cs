using UnityEngine;

namespace EmergencyRoad
{
    /// <summary>
    /// Optional scene-authored rules component. It never creates or scans scene objects.
    /// Traffic prefabs must already contain EmergencyTrafficCrashResponder.
    /// </summary>
    public sealed class EmergencyRoadTrafficCollisionRules : MonoBehaviour
    {
        [SerializeField] private EmergencyRoadMenuView menuView;
        [SerializeField] private bool forceFatalSideCollisions = true;

        private void Awake()
        {
            if (forceFatalSideCollisions && EmergencyRoadProfile.Current.sideCollisionEnabled)
            {
                EmergencyRoadProfile.Current.sideCollisionEnabled = false;
                EmergencyRoadProfile.Save();
            }

            if (forceFatalSideCollisions && menuView != null)
            {
                if (menuView.sideCollision != null) menuView.sideCollision.gameObject.SetActive(false);
                if (menuView.sideCollisionLabel != null) menuView.sideCollisionLabel.gameObject.SetActive(false);
            }
        }
    }
}
