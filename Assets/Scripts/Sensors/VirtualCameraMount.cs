using UnityEngine;

using RobotSim.Robot;
using System.Collections.Generic;

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

        // Encapsulated Components
        private PointCloudGenerator _pcg;
        private Camera _cam;

        // Events Relayed from PCG
        public System.Action OnMasterCaptured;
        public System.Action OnScanCaptured;

        // Public Accessors
        public List<Vector3> MasterPoints => _pcg != null ? _pcg.MasterPoints : new List<Vector3>();
        public List<Vector3> ScanPoints => _pcg != null ? _pcg.ScanPoints : new List<Vector3>();
        public Transform SensorTransform => _pcg != null ? _pcg.transform : transform;

        /// <summary>
        /// Calculated matrix to convert Camera Local to Robot Base coordinates.
        /// </summary>
        public Matrix4x4 CamToBaseMatrix => StateProvider != null
            ? StateProvider.RobotBase.worldToLocalMatrix * transform.localToWorldMatrix
            : transform.localToWorldMatrix;

        private void InitializeReferences()
        {
            if (StateProvider == null) StateProvider = FindFirstObjectByType<RobotStateProvider>(FindObjectsInactive.Include);
            _cam = GetComponentInChildren<Camera>();
            _pcg = GetComponentInChildren<PointCloudGenerator>();

            if (_pcg != null)
            {
                // Relay Events
                _pcg.OnMasterCaptured += () => OnMasterCaptured?.Invoke();
                _pcg.OnScanCaptured += () => OnScanCaptured?.Invoke();
            }
        }
        private void Awake()
        {
            InitializeReferences();
        }
        private void Start()
        {
            // Empty Start to maintain compatibility if other scripts expect it
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
                        if (mode == CameraMountType.HandEye)
                        {
                            transform.localPosition = Vector3.zero;
                            transform.localRotation = Quaternion.identity;
                        }
                    }
                }

                // [추가] 모드 변경 시 데이터 초기화
                ClearAllData();
            }
        }

        public void ClearAllData()
        {
            if (_pcg != null)
            {
                _pcg.ClearMaster();
                _pcg.ClearScan();
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
        public void setTargetTexture(RenderTexture rt)
        {
            if (_cam)
                _cam.targetTexture = rt;
        }

        // --- Wrapped PCG Methods ---
        public void CaptureMaster()
        {
            if (_pcg != null) _pcg.CaptureMaster();
            else Debug.LogWarning("[VirtualCameraMount] PCG missing, cannot capture Master.");
        }

        public void CaptureScan()
        {
            if (_pcg != null) _pcg.CaptureScan();
            else Debug.LogWarning("[VirtualCameraMount] PCG missing, cannot capture Scan.");
        }
        public void ClearScan()
        {
            _pcg?.ClearScan();
        }
    }
}
