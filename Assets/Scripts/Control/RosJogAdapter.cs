using RobotSim.ROS.Services;
using RosSharp;
using RosSharp.RosBridgeClient;
using RosSharp.Urdf;
using UnityEngine;

namespace RobotSim.Control
{
    /// <summary>
    /// Jog Adapter that centralizes all real-time velocity commands (Twist for IK, JointJog for FK).
    /// Includes software joint limit clipping based on URDF definitions.
    /// </summary>
    public class RosJogAdapter : MonoBehaviour
    {
        [Header("ROS Connection")]
        public RobotSim.ROS.ServoTwistPublisher ServoPublisher;
        public RobotSim.ROS.JointJogPublisher JointPublisher;
        public RobotSim.Robot.RobotStateProvider StateProvider;

        [Header("Settings")]
        public float CartesianLinearSpeed = 0.2f; // m/s
        public float CartesianAngularSpeed = 0.5f; // rad/s
        public float JointRotationSpeed = 0.5f; // rad/s
        public float SpeedMultiplier = 1.0f; // Multiplied by UI slider
        
        [Header("Safety")]
        public bool EnableLimitClipping = true;
        [Tooltip("Buffer in radians to stop before hitting the hard limit.")]
        public float LimitBuffer = 0.01f; 

        // Internal movement state
        private Vector3 _currentLin;
        private Vector3 _currentAng;
        private int _currentJointIndex = -1;
        private float _currentJointVel = 0;
        
        private float _lastInputTime;
        private bool _isMoving = false;
        private bool _isJointMode = false;

        private void Start()
        {
            if (ServoPublisher == null) ServoPublisher = FindObjectOfType<RobotSim.ROS.ServoTwistPublisher>();
            if (JointPublisher == null) JointPublisher = FindObjectOfType<RobotSim.ROS.JointJogPublisher>();
            if (StateProvider == null) StateProvider = FindObjectOfType<RobotSim.Robot.RobotStateProvider>();
            
            _lastInputTime = Time.time;
        }

        /// <summary>
        /// Trigger Cartesian (IK) Jogging (Twist)
        /// </summary>
        public void Jog(Vector3 linearDir, Vector3 angularDir)
        {
            _isJointMode = false;
            _currentLin = linearDir * CartesianLinearSpeed * SpeedMultiplier;
            _currentAng = angularDir * CartesianAngularSpeed * SpeedMultiplier;
            _lastInputTime = Time.time;
        }

        /// <summary>
        /// Trigger Joint (FK) Jogging (JointJog)
        /// </summary>
        public void JointJog(int jointIndex, float direction)
        {
            _isJointMode = true;
            _currentJointIndex = jointIndex;
            _currentJointVel = direction * JointRotationSpeed * SpeedMultiplier;
            _lastInputTime = Time.time;
        }

        private void Update()
        {
            // Deadman Switch: Stop if no signal received within 100ms
            float dt = Time.time - _lastInputTime;
            if (dt > 0.1f)
            {
                if (_isMoving) StopMovement();
                return;
            }

            if (_isJointMode)
            {
                if (JointPublisher != null && _currentJointIndex != -1)
                {
                    float velToPublish = _currentJointVel;

                    // Joint Limit Clipping
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
            }
            else
            {
                if (ServoPublisher != null)
                {
                    ServoPublisher.PublishCommand(_currentLin, _currentAng);
                    _isMoving = true;
                }
            }
        }

        private void StopMovement()
        {
            if (!_isJointMode && ServoPublisher != null)
                ServoPublisher.PublishCommand(Vector3.zero, Vector3.zero);
            else if (_isJointMode && JointPublisher != null && _currentJointIndex != -1)
                JointPublisher.PublishJog(_currentJointIndex, 0);

            _currentLin = Vector3.zero;
            _currentAng = Vector3.zero;
            _currentJointVel = 0;
            _currentJointIndex = -1;
            _isMoving = false;
        }
    }
}
