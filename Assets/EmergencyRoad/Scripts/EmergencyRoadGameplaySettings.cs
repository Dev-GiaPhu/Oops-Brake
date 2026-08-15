using UnityEngine;
using UnityEngine.Serialization;

namespace EmergencyRoad
{
    [CreateAssetMenu(menuName="Emergency Road/Gameplay Settings",fileName="EmergencyRoadGameplaySettings")]
    public sealed class EmergencyRoadGameplaySettings:ScriptableObject
    {
        [Header("Speed")]
        [Min(1)] public float startSpeed=18f;
        [Min(1)] public float maxSpeed=45f;
        [Min(10)] public float distanceToMaxSpeed=800f;
        [Header("Obstacle Density")]
        [FormerlySerializedAs("minimumSpeedGapMultiplier"), FormerlySerializedAs("minimumObstacleGap"), FormerlySerializedAs("gapAtStart"), Min(0), Tooltip("Khoảng cách hàng chướng ngại khi xe đang ở tốc độ tối thiểu.")] public float obstacleGapAtMinimumSpeed=28f;
        [FormerlySerializedAs("maximumSpeedGapMultiplier"), FormerlySerializedAs("maximumObstacleGap"), FormerlySerializedAs("gapAtMaxSpeed"), Min(0), Tooltip("Khoảng cách hàng chướng ngại khi xe đạt tốc độ tối đa.")] public float obstacleGapAtMaximumSpeed=70f;
        [HideInInspector] public float minimumObstacleReactionTime=.75f;
        [Range(0,1)] public float twoLaneBlockChanceAtStart=.58f;
        [Range(0,1)] public float twoLaneBlockChanceAtMaxSpeed=.32f;
        [Header("Intersection Spacing")]
        [Min(6)] public int minStraightChunksBetweenIntersections=10;
        [Min(7)] public int maxStraightChunksBetweenIntersections=14;
        [Range(1,3)] public int intersectionClearanceChunks=1;
        [Header("Obstacle Mix and Size")]
        [Range(0,1)] public float stoppedVehicleChance=.68f;
        [Range(0,1)] public float barrierChance=.24f;
        [Range(.7f,.9f)] public float obstacleLaneWidth=.86f;
        public Vector2 stoppedVehicleSize=new(2.18f,4.05f);
        public Vector3 stoppedVehicleHitbox=new(2.54f,1.45f,3.55f);
        [Header("Same Direction Traffic")]
        [Range(0,1)] public float sameDirectionTrafficChance=.28f;
        [Min(1)] public float trafficMinimumRoadSpeed=8f;
        [Min(1)] public float trafficMaximumRoadSpeed=14f;
        [Range(0,1)] public float trafficLaneChangeChance=.38f;
        [Min(.25f)] public float trafficLaneDecisionInterval=2.2f;
        [Min(4f)] public float trafficBrakingDistance=11f;
        [Min(1f)] public float trafficBrakingStrength=10f;
        [Header("Cross Traffic")]
        [Min(4f)] public float crossTrafficSpeed=11.5f;
        [Min(20f)] public float crossTrafficStartDistance=44f;
        [Min(8f)] public float crossTrafficRouteLookDistance=24f;
        [Header("Roadside Ground and Buildings")]
        [Min(18f), Tooltip("Chiều rộng đất cỏ tính từ mép trong hiện tại và chỉ mở rộng ra ngoài.")] public float roadsideGrassWidth=42f;
        [Range(1,2)] public int roadsideBuildingRows=2;
        [Min(0f)] public float roadsideBuildingGap=1.25f;
        [Min(6f)] public float roadsideBuildingSetback=10f;
        [Header("Side Collision")]
        [Min(.2f)] public float sideCheckHalfLength=.68f;
        public float releaseBehindPlayer=.32f;
        [Header("Motorcycle")]
        [Min(1)] public float motorSpeed=34f;
        [Min(.05f)] public float motorTrackingSmoothTime=.65f;
        [Min(.1f)] public float motorMaxLateralSpeed=3.5f;
        [Min(.5f), Tooltip("Thời gian đếm ngược cảnh báo ở làn trước khi mô tô xuất hiện.")] public float motorWarningTrackTime=3f;
        [Min(.1f)] public float motorLockedWarningTime=.55f;
        [Min(.1f)] public float motorPlanningRetryDelay=1f;
        [Min(0f)] public float motorCrossroadSafetyDistance=24f;
        [Min(1f)] public float motorObstacleSafetyDistance=9f;
        [Header("Motorcycle Debris Physics")]
        [Min(0)] public float motorDebrisImpactHoldTime=.35f;
        [Min(.25f)] public float motorDebrisMapFollowSharpness=2.2f;
    }
}
