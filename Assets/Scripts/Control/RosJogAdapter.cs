using UnityEngine;
using RosSharp.RosBridgeClient;
using RobotSim.ROS.Services;

namespace RobotSim.Control
{
    /// <summary>
    /// Jog Adapter that uses ROS 'compute_cartesian_path' service service instead of local Unity IK.
    /// </summary>
    public class RosJogAdapter : MonoBehaviour
    {
        [Header("ROS Connection")]
        public RobotSim.ROS.ServoTwistPublisher ServoPublisher;

        [Header("Settings")]
        public float LinearSpeed = 0.2f; // m/s
        public float AngularSpeed = 0.5f; // rad/s
        public float SpeedMultiplier = 1.0f; // Multiplied by UI slider
        
        private Vector3 _currentLin;
        private Vector3 _currentAng;
        private float _lastInputTime;
        private bool _isMoving = false;

        private void Start()
        {
            if (ServoPublisher == null) ServoPublisher = FindObjectOfType<RobotSim.ROS.ServoTwistPublisher>();
            _lastInputTime = Time.time;
        }

        public void Jog(Vector3 linearDir, Vector3 angularDir)
        {
            _currentLin = linearDir * LinearSpeed * SpeedMultiplier;
            _currentAng = angularDir * AngularSpeed * SpeedMultiplier;
            _lastInputTime = Time.time;
        }

        private void Update()
        {
            if (ServoPublisher == null) return;

            // Deadman Switch: Snappier response (50ms)
            float dt = Time.time - _lastInputTime;
            if (dt > 0.05f)
            {
                if (_isMoving)
                {
                    ServoPublisher.PublishCommand(UnityEngine.Vector3.zero, UnityEngine.Vector3.zero);
                    _currentLin = UnityEngine.Vector3.zero;
                    _currentAng = UnityEngine.Vector3.zero;
                    _isMoving = false;
                }
                return;
            }

            // Stream Twist
            ServoPublisher.PublishCommand(_currentLin, _currentAng);
            _isMoving = true;
        }
        
        private RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose GetRosPoseFromUnity(Vector3 pos, Quaternion rot)
        {
            // Standard ROS# Conversion (Unity -> ROS)
            // Pos: Z, -X, Y
            // Rot: -Z, X, -Y, W
            
            return new RosSharp.RosBridgeClient.MessageTypes.Geometry.Pose
            {
                position = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Point { x = pos.z, y = -pos.x, z = pos.y },
                orientation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion { x = -rot.z, y = rot.x, z = -rot.y, w = rot.w }
            };
        }
    }
}
