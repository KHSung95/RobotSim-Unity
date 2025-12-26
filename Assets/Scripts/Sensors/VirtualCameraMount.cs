using UnityEngine;

namespace RobotSim.Sensors
{
    public enum CameraMountType
    {
        HandEye, // Mounted on Robot Flange (End-Effector)
        BirdEye, // Fixed relative to Robot Base
        FreeCam  // Independent world movement
    }

    [RequireComponent(typeof(Camera))]
    public class VirtualCameraMount : MonoBehaviour
    {
        [Header("Mount Configuration")]
        public CameraMountType MountType = CameraMountType.BirdEye;
        
        [Tooltip("The reference robot base (usually top-level robot object).")]
        public Transform RobotBase;
        
        [Tooltip("The Transform to attach to when in Hand-Eye mode (e.g. tool0).")]
        public Transform RobotFlange;
        
        [Header("Off-set Configuration")]
        [Tooltip("Relative Position (from Flange in HandEye, from Base in BirdEye).")]
        public Vector3 RelativePosition = new Vector3(0, 0.05f, 0.05f);
        [Tooltip("Relative Rotation (Euler)")]
        public Vector3 RelativeRotation = new Vector3(0, 0, 0);

        /// <summary>
        /// Calculated matrix to convert Camera Local to Robot Base coordinates.
        /// </summary>
        public Matrix4x4 CamToBaseMatrix => RobotBase != null 
            ? RobotBase.worldToLocalMatrix * transform.localToWorldMatrix 
            : transform.localToWorldMatrix;

        private void LateUpdate()
        {
            if (MountType == CameraMountType.HandEye && RobotFlange != null)
            {
                transform.position = RobotFlange.TransformPoint(RelativePosition);
                transform.rotation = RobotFlange.rotation * Quaternion.Euler(RelativeRotation);
            }
            else if (MountType == CameraMountType.BirdEye && RobotBase != null)
            {
                transform.position = RobotBase.TransformPoint(RelativePosition);
                transform.rotation = RobotBase.rotation * Quaternion.Euler(RelativeRotation);
            }
        }
        
        public void SetMountMode(CameraMountType mode)
        {
            // When switching, we might want to keep the current world position 
            // by recalculating the relative offset.
            if (MountType != mode)
            {
                MountType = mode;
                UpdateRelativeFromCurrent();
            }
        }

        [ContextMenu("Update Relative From Current")]
        public void UpdateRelativeFromCurrent()
        {
            if (MountType == CameraMountType.HandEye && RobotFlange != null)
            {
                RelativePosition = RobotFlange.InverseTransformPoint(transform.position);
                RelativeRotation = (Quaternion.Inverse(RobotFlange.rotation) * transform.rotation).eulerAngles;
            }
            else if (MountType == CameraMountType.BirdEye && RobotBase != null)
            {
                RelativePosition = RobotBase.InverseTransformPoint(transform.position);
                RelativeRotation = (Quaternion.Inverse(RobotBase.rotation) * transform.rotation).eulerAngles;
            }
        }
    }
}
