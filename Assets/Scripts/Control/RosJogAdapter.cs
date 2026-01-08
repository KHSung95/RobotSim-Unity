using UnityEngine;

using RosSharp;
using RosSharp.Urdf;

using RobotSim.ROS;
using RobotSim.Robot;

namespace RobotSim.Control
{
    public class RosJogAdapter : MonoBehaviour
    {
        [Header("ROS Connection")]
        public RobotStateProvider StateProvider;
        public ServoTwistPublisher ServoPublisher;
        public JointJogPublisher JointPublisher;

        [Header("Settings")]
        public float CartesianLinearSpeed = 0.2f;  // m/s
        public float CartesianAngularSpeed = 0.5f; // rad/s
        public float JointRotationSpeed = 0.5f;    // rad/s
        public float SpeedMultiplier = 1.0f;

        [Header("Safety")]
        public bool EnableLimitClipping = true;
        public float LimitBuffer = 0.01f;

        // 내부 상태는 이미 ROS 좌표계로 변환된 값을 들고 있도록 설계
        private Vector3 _rosLinVel;
        private Vector3 _rosAngVel;

        private int _currentJointIndex = -1;
        private float _currentJointVel = 0;

        private float _lastInputTime;
        private bool _isMoving = false;
        private bool _isJointMode = false;

        private void Start()
        {
            StateProvider ??= FindObjectOfType<RobotStateProvider>();

            ServoPublisher ??= FindObjectOfType<ServoTwistPublisher>();
            JointPublisher ??= FindObjectOfType<JointJogPublisher>();

            ServoPublisher?.SetRobotStateProvider(StateProvider);
            JointPublisher?.SetRobotStateProvider(StateProvider);

            _lastInputTime = -1f;
        }

        // ---------------------------------------------------------
        // IK Control (Twist)
        // ---------------------------------------------------------
        public void Jog(Vector3 unityLinearDir, Vector3 unityAngularDir)
        {
            _isJointMode = false;
            _lastInputTime = Time.time;

            _rosLinVel = unityLinearDir * CartesianLinearSpeed * SpeedMultiplier;
            _rosAngVel = unityAngularDir * CartesianAngularSpeed * SpeedMultiplier;
        }

        // ---------------------------------------------------------
        // FK Control (Joint)
        // ---------------------------------------------------------
        public void JointJog(int jointIndex, float direction)
        {
            _isJointMode = true;
            _lastInputTime = Time.time;

            _currentJointIndex = jointIndex;
            _currentJointVel = direction * JointRotationSpeed * SpeedMultiplier;
        }

        private void Update()
        {
            // Deadman Switch (100ms 타임아웃)
            if (Time.time - _lastInputTime > 0.1f)
            {
                if (_isMoving) StopMovement();
                return;
            }

            if (_isJointMode)
            {
                HandleJointJog();
            }
            else
            {
                HandleCartesianJog();
            }
        }

        private void HandleCartesianJog()
        {
            if (ServoPublisher != null)
            {
                // 이미 변환된 ROS 좌표계 값을 전송
                ServoPublisher.PublishCommand(_rosLinVel, _rosAngVel);
                _isMoving = true;
            }
        }

        private void HandleJointJog()
        {
            if (JointPublisher == null || _currentJointIndex == -1) return;

            float velToPublish = _currentJointVel;

            if (EnableLimitClipping && StateProvider != null && _currentJointIndex < StateProvider.JointReferences.Length)
            {

                var joint = StateProvider.JointReferences[_currentJointIndex];
                if (joint.JointType != UrdfJoint.JointTypes.Continuous)
                {
                    // 1. Try HingeJointLimitsManager (for large angle limits > 180)

                    var limitsManager = joint.GetComponent<HingeJointLimitsManager>();
                    if (limitsManager != null)
                    {
                        float currentTotalAngle = limitsManager.AngleActual + limitsManager.RotationNumberActual * 360f;
                        float limitMin = limitsManager.LargeAngleLimitMin;
                        float limitMax = limitsManager.LargeAngleLimitMax;
                        float bufferDeg = LimitBuffer * Mathf.Rad2Deg;

                        // Note: ROS# swap limits in InitializeLimits sometimes, 
                        // so we check against both.
                        float lower = Mathf.Min(limitMin, limitMax);
                        float upper = Mathf.Max(limitMin, limitMax);

                        if (velToPublish > 0 && currentTotalAngle >= (upper - bufferDeg))
                        {
                            velToPublish = 0;
                            if (Time.frameCount % 30 == 0) Debug.LogWarning($"[RosJogAdapter] Large Angle Upper Limit reached on {joint.JointName}");
                        }
                        else if (velToPublish < 0 && currentTotalAngle <= (lower + bufferDeg))
                        {
                            velToPublish = 0;
                            if (Time.frameCount % 30 == 0) Debug.LogWarning($"[RosJogAdapter] Large Angle Lower Limit reached on {joint.JointName}");
                        }
                    }
                }
            }
            JointPublisher.PublishJog(_currentJointIndex, velToPublish);
            _isMoving = true;
        }

        private void StopMovement()
        {
            if (!_isJointMode && ServoPublisher != null)
                ServoPublisher.PublishCommand(Vector3.zero, Vector3.zero);
            else if (_isJointMode && JointPublisher != null && _currentJointIndex != -1)
                JointPublisher.PublishJog(_currentJointIndex, 0);

            _rosLinVel = Vector3.zero;
            _rosAngVel = Vector3.zero;
            _currentJointVel = 0;
            _currentJointIndex = -1;
            _isMoving = false;
        }
    }
}