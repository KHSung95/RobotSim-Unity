using UnityEngine;
using RosSharp.Urdf;
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
            List<string> names = new List<string>();

            // 1. Check for manual overrides first
            if (OverrideJoints != null && OverrideJoints.Count > 0)
            {
                foreach (var j in OverrideJoints)
                {
                    if (j != null && !string.IsNullOrEmpty(j.JointName) && !names.Contains(j.JointName))
                    {
                        _joints.Add(j);
                        _jointMap.Add(j.JointName, j);
                        names.Add(j.JointName);
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

                        _joints.Add(j);
                        _jointMap.Add(j.JointName, j);
                        names.Add(j.JointName);
                    }
                }
                Debug.Log($"[RobotStateProvider] Auto-discovered {names.Count} joints.");
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
            // 1. Update Joint Angles (FK)
            for (int i = 0; i < _joints.Count; i++)
            {
                jointAnglesDegrees[i] = (float)_joints[i].GetPosition() * Mathf.Rad2Deg;
            }

            // 2. Update TCP Pose (IK) relative to Base
            if (_tcpTransform != null && RobotBase != null)
            {
                Vector3 relativePos = RobotBase.InverseTransformPoint(_tcpTransform.position);
                
                // Unity -> ROS Coordinate Conversion
                // ROS X (Forward) = Unity Z
                // ROS Y (Left) = Unity -X
                // ROS Z (Up) = Unity Y
                tcpPositionRos = new Vector3(relativePos.z, -relativePos.x, relativePos.y);

                // Rotation conversion
                Quaternion relativeRot = Quaternion.Inverse(RobotBase.rotation) * _tcpTransform.rotation;
                // Convert to ROS Euler (extrinsic XYZ or similar, simplified for UI)
                // In ROS, often RPY is used. Here we provide a consistent mapping.
                Vector3 euler = relativeRot.eulerAngles;
                tcpRotationEulerRos = new Vector3(euler.z, -euler.x, euler.y); 
            }
        }
    }
}
