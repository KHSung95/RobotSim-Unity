using RobotSim.Control;
using RobotSim.Robot;
using RobotSim.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace RobotSim.Sensors
{
    public enum CameraMountType
    {
        HandEye, // Mounted on Robot Flange (End-Effector)
        BirdEye, // Fixed relative to Robot Base
        FreeCam  // Independent world movement
    }
    public class VirtualCameraMount : MonoBehaviour
    {
        [Header("Mount Configuration")]
        public CameraMountType MountType = CameraMountType.BirdEye;

        [Header("Controllers")]
        public RobotStateProvider StateProvider;
        
        [Header("Off-set Configuration")]
        [Tooltip("Relative Position (from Flange in HandEye, from Base in BirdEye).")]
        public Vector3 RelativePosition = new Vector3(0, 0.05f, 0.05f);
        [Tooltip("Relative Rotation (Euler)")]
        public Vector3 RelativeRotation = new Vector3(0, 0, 0);
        public Camera Cam { get; private set; }
        
        /// <summary>
        /// Calculated matrix to convert Camera Local to Robot Base coordinates.
        /// </summary>
        public Matrix4x4 CamToBaseMatrix => StateProvider != null 
            ? StateProvider.RobotBase.worldToLocalMatrix * transform.localToWorldMatrix 
            : transform.localToWorldMatrix;

        private void InitializeReferences()
        {
            if (StateProvider == null) StateProvider = FindFirstObjectByType<RobotStateProvider>(FindObjectsInactive.Include);
            Cam = GetComponentInChildren<Camera>();
        }
        private void Start()
        {
            InitializeReferences();
        }

        private void setManualPoint() // unused for now
        {
            if (MountType == CameraMountType.HandEye && StateProvider.TcpTransform != null)
            {
                transform.position = StateProvider.TcpTransform.TransformPoint(RelativePosition);
                transform.rotation = StateProvider.TcpTransform.rotation * Quaternion.Euler(RelativeRotation);
            }
            else if (MountType == CameraMountType.BirdEye && StateProvider.RobotBase != null)
            {
                transform.position = StateProvider.RobotBase.TransformPoint(RelativePosition);
                transform.rotation = StateProvider.RobotBase.rotation * Quaternion.Euler(RelativeRotation);
            }
        }

        public void SetMountMode(CameraMountType mode)
        {
            if (MountType != mode)
            {
                MountType = mode;
                
                // [추가] 부모 트랜스폼 변경 로직
                if (StateProvider != null)
                {
                    Transform targetParent = (mode == CameraMountType.HandEye) ? StateProvider.TcpTransform : StateProvider.RobotBase;
                    if (targetParent != null)
                    {
                        transform.SetParent(targetParent, true);
                        if(mode == CameraMountType.HandEye)
                        {
                            transform.localPosition = Vector3.zero;
                            transform.localRotation = Quaternion.identity;
                        }
                    }
                }
            }
        }
        public void ResetPosition()
        {
            if (MountType == CameraMountType.HandEye)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else if (MountType == CameraMountType.BirdEye)
            {
                transform.position = Vector3.zero;
                transform.rotation = Quaternion.identity;
            }
        }
    }
}
