using UnityEngine;
using RosSharp.Urdf;
using RosSharp; // [New] For HingeJointLimitsManager
using System.Collections.Generic;
using RobotSim.Utils;

namespace RobotSim.Robot
{
    /// <summary>
    /// Centralized provider for the robot's current state (FK and IK).
    /// Attach this to the robot root (e.g., ur5e).
    /// </summary>
    public class RobotStateProvider : MonoBehaviour
    {
        [Header("References")]
        public Transform RobotBase;
        public string MoveGroupName = "ur_manipulator";
        public string ToolLinkName = "tool0";
        public string FallbackToolLinkName = "wrist_3_link";
        
        [Header("ROS Frame IDs")]
        public string BaseFrameId = "base_link";
        public string FlangeLinkName = "tool0"; 
        
        [Tooltip("Optional: Manually define the joints and their order. If empty, all URDF joints will be auto-discovered.")]
        public List<UrdfJoint> OverrideJoints = new List<UrdfJoint>();

        [Header("State (Read Only via Inspector for Debug)")]
        [SerializeField] private float[] jointAnglesDegrees;
        [SerializeField] private Vector3 tcpPositionRos;
        [SerializeField] private Vector3 tcpRotationEulerRos;

        private Transform _tcpTransform;
        private List<UrdfJoint> _joints = new List<UrdfJoint>();
        // [New] Limit Managers Cache
        private List<HingeJointLimitsManager> _limitManagers = new List<HingeJointLimitsManager>();
        
        private string[] _jointNames;
        private Dictionary<string, UrdfJoint> _jointMap = new Dictionary<string, UrdfJoint>();

        // Public Properties for reference by UI and other scripts
        public float[] JointAnglesDegrees => jointAnglesDegrees;
        public string[] JointNames => _jointNames;
        public UrdfJoint[] JointReferences => _joints.ToArray();
        public Dictionary<string, UrdfJoint> JointMap => _jointMap;
        public Transform TcpTransform => _tcpTransform;
        public Vector3 TcpPositionRos => tcpPositionRos;
        public Vector3 TcpRotationEulerRos => tcpRotationEulerRos;

        private void Awake()
        {
            InitializeReferences();
        }

        public void InitializeReferences()
        {
            // Try to find 'base_link' as the coordinate origin, otherwise fallback to root
            if (RobotBase == null)
            {
                RobotBase = this.transform.FindDeepChild("base_link");
                if (RobotBase == null) RobotBase = this.transform;
            }

            // Always fetch joints relative to this GameObject
            var urdfJoints = GetComponentsInChildren<UrdfJoint>();
            
            _joints.Clear();
            _jointMap.Clear();
            _limitManagers.Clear(); // [New] Clear cache
            List<string> names = new List<string>();

            // Helper to add joint and limit manager
            void AddJoint(UrdfJoint j)
            {
                _joints.Add(j);
                _jointMap.Add(j.JointName, j);
                _limitManagers.Add(j.GetComponent<HingeJointLimitsManager>()); // [New] Cache Limit Manager
                names.Add(j.JointName);
            }

            // 1. Check for manual overrides first
            if (OverrideJoints != null && OverrideJoints.Count > 0)
            {
                foreach (var j in OverrideJoints)
                {
                    if (j != null && !string.IsNullOrEmpty(j.JointName) && !names.Contains(j.JointName))
                    {
                        AddJoint(j);
                    }
                }
                Debug.Log($"[RobotStateProvider] Using {names.Count} manually assigned joints.");
            }
            // 2. Fallback to automatic discovery
            else
            {
                foreach (var j in urdfJoints)
                {
                    if (!string.IsNullOrEmpty(j.JointName) && !names.Contains(j.JointName))
                    {
                        if (j.JointType == UrdfJoint.JointTypes.Fixed) continue;
                        AddJoint(j);
                    }
                }
            }

            _jointNames = names.ToArray();
            jointAnglesDegrees = new float[_joints.Count];

            // Cache TCP
            _tcpTransform = this.transform.FindDeepChild(ToolLinkName);
            if (_tcpTransform == null) _tcpTransform = this.transform.FindDeepChild(FallbackToolLinkName);
            
            Debug.Log($"[RobotStateProvider] Initialized with {_joints.Count} joints.");
        }

        private void Update()
        {
            UpdateState();
        }

        private void UpdateState()
        {
            // 1. Update Joint Angles (FK) with Clamping
            for (int i = 0; i < _joints.Count; i++)
            {
                float rawAngle = (float)_joints[i].GetPosition() * Mathf.Rad2Deg;
                
                // [New] Clamp detection
                if (_limitManagers[i] != null)
                {
                    // ROS Limits are inverted from Unity Limits
                    // Unity Min = -ROS_Upper -> ROS_Upper = -Unity_Min
                    // Unity Max = -ROS_Lower -> ROS_Lower = -Unity_Max
                    // So ROS Range is [-Unity_Max, -Unity_Min]
                    
                    float rosLimitMin = -_limitManagers[i].LargeAngleLimitMax;
                    float rosLimitMax = -_limitManagers[i].LargeAngleLimitMin;

                    // Ensure min < max just in case
                    if (rosLimitMin > rosLimitMax) 
                    {
                        float temp = rosLimitMin; rosLimitMin = rosLimitMax; rosLimitMax = temp;
                    }

                    // Apply Clamp
                    rawAngle = Mathf.Clamp(rawAngle, rosLimitMin, rosLimitMax);
                }

                jointAnglesDegrees[i] = rawAngle;
            }

            // 2. Update TCP Pose (IK) relative to Base
            if (_tcpTransform != null && RobotBase != null)
            {
                Vector3 relativePos = RobotBase.InverseTransformPoint(_tcpTransform.position);
                UnityEngine.Quaternion relativeRot = UnityEngine.Quaternion.Inverse(RobotBase.rotation) * _tcpTransform.rotation;

                // Unity -> ROS Coordinate Conversion using ROS# standard extensions
                tcpPositionRos = relativePos.Unity2Ros();

                // Convert to ROS Euler (using conversion extensions)
                UnityEngine.Quaternion rosRot = relativeRot.Unity2Ros();
                tcpRotationEulerRos = rosRot.eulerAngles; 
            }
        }
    }
}
