using UnityEngine;
using System.Collections.Generic;
using RobotSim.Sensors;
using RosSharp.RosBridgeClient;
using RobotSim.ROS;
using RobotSim.Robot;

namespace RobotSim.Simulation
{
    public class GuidanceManager : MonoBehaviour
    {
        [Header("References")]
        public PointCloudGenerator PCG;
        public Transform TargetObject;

        [Header("Industrial Logic (T_ic)")]
        public Transform RobotFlange; // TCP
        public VirtualCameraMount CamMount;
        public MoveRobotToPoseClient Mover;
        public RobotStateProvider RobotState;

        [Header("Saved Masters")]
        public Matrix4x4 T_ib = Matrix4x4.identity;
        public Matrix4x4 T_tb_master = Matrix4x4.identity;

        [Header("Results")]
        public Matrix4x4 LastCorrectionMatrix = Matrix4x4.identity;
        public float LastDeviationDist = 0f;

        [Header("ROS Connection")]
        public RosConnector Connector;
        public string ServiceName = "/calculate_icp";

        [Header("Hand-Eye Calibration")]
        public Transform tcpTransform;    // Robot Flange (End-Effector)
        public Transform cameraTransform; // Camera attached to the robot
        private Matrix4x4 m_T_tcp_to_cam;

        // ... Existing Start ...
        private void Start()
        {
            if (PCG == null) PCG = FindObjectOfType<PointCloudGenerator>();
            if (CamMount == null) CamMount = FindObjectOfType<VirtualCameraMount>();
            if (Mover == null) Mover = FindObjectOfType<MoveRobotToPoseClient>();
            if (RobotState == null) RobotState = FindObjectOfType<RobotStateProvider>();
            
            if (Connector == null) Connector = FindObjectOfType<RosConnector>();

            // Auto-assign transforms if missing
            if (tcpTransform == null && RobotState != null) tcpTransform = RobotState.TcpTransform;
            if (cameraTransform == null && CamMount != null) cameraTransform = CamMount.transform;

            // Calculate Hand-Eye Offset (Static assumption)
            if (tcpTransform != null && cameraTransform != null)
            {
                m_T_tcp_to_cam = tcpTransform.worldToLocalMatrix * cameraTransform.localToWorldMatrix;
            }

            // 시작 시 PCG에게 "우리의 기준은 로봇 베이스다"라고 알려줌
            if (PCG != null && RobotState.RobotBase != null)
            {
                PCG.SetRobotBase(RobotState.RobotBase);
            }
        }

        [ContextMenu("Capture Master")]
        public void CaptureMaster()
        {
            if (PCG != null)
            {
                PCG.CaptureMaster();
                Debug.Log("[GuidanceManager] Master Cloud Captured (Base Frame).");
            }
        }

        [ContextMenu("Capture Scan (Current)")]
        public void CaptureCurrent()
        {
            if (PCG != null)
            {
                PCG.CaptureScan();
                Debug.Log("[GuidanceManager] Current Scan Captured (Base Frame).");
            }
        }

        [ContextMenu("Run Guidance (Service)")]
        public void RunGuidance()
        {
            if (Connector == null || PCG == null || Mover == null) 
            {
                Debug.LogError("[GuidanceManager] Missing dependencies.");
                return;
            }

            Debug.Log($"[GuidanceManager] Preparing to call CalculateICP Service... (Master: {PCG.MasterPoints.Count}, Scan: {PCG.ScanPoints.Count})");

            if (PCG.MasterPoints.Count == 0 || PCG.ScanPoints.Count == 0)
            {
                Debug.LogWarning("[GuidanceManager] Cannot run guidance with empty point clouds.");
                return;
            }

            // 1. Convert Clouds to PointCloud2
            var req = new RosSharp.RosBridgeClient.MessageTypes.CustomServices.CalculateICPRequest();
            req.master_point_cloud = ToPointCloud2(PCG.MasterPoints);
            req.current_point_cloud = ToPointCloud2(PCG.ScanPoints);

            // 2. Call Service
            Connector.RosSocket.CallService<RosSharp.RosBridgeClient.MessageTypes.CustomServices.CalculateICPRequest, RosSharp.RosBridgeClient.MessageTypes.CustomServices.CalculateICPResponse>(
                ServiceName,
                OnGuidanceResponse,
                req
            );
        }

        private bool _hasPendingCorrection = false;
        private float[] _pendingMatrixData;

        // Movement Detection Logic
        private Vector3 _lastTcpPos;
        private Quaternion _lastTcpRot;
        private bool _isFirstUpdate = true;
        private const float MovementThresholdPos = 0.005f; // 5mm (Increased from 0.1mm)
        private const float MovementThresholdRot = 0.1f;   // 0.1 degrees (Increased from 0.01)

        private void Update()
        {
            // 1. Check for Robot Movement -> Clear Scan if moved
            if (RobotState != null && PCG != null && RobotState.TcpTransform != null)
            {
                if (_isFirstUpdate)
                {
                    _lastTcpPos = RobotState.TcpTransform.position;
                    _lastTcpRot = RobotState.TcpTransform.rotation;
                    _isFirstUpdate = false;
                }
                else
                {
                    float dist = Vector3.Distance(RobotState.TcpTransform.position, _lastTcpPos);
                    float angle = Quaternion.Angle(RobotState.TcpTransform.rotation, _lastTcpRot);

                    if (dist > MovementThresholdPos || angle > MovementThresholdRot)
                    {
                        // Robot is moving or has moved significantly
                        if (PCG.ScanPoints.Count > 0)
                        {
                            Debug.Log($"[GuidanceManager] Clearing scan due to movement (Dist: {dist:F6}, Angle: {angle:F4})");
                            PCG.ClearScan();
                        }
                        
                        // Update last pose
                        _lastTcpPos = RobotState.TcpTransform.position;
                        _lastTcpRot = RobotState.TcpTransform.rotation;
                    }
                }
            }

            // 2. Guidance Manager Update
            if (_hasPendingCorrection)
            {
                _hasPendingCorrection = false;
                if (_pendingMatrixData != null)
                {
                    ApplyCorrection(_pendingMatrixData);
                    _pendingMatrixData = null;
                }
            }
        }

        private void OnGuidanceResponse(RosSharp.RosBridgeClient.MessageTypes.CustomServices.CalculateICPResponse response)
        {
            if (response == null || response.transformation_matrix == null || response.transformation_matrix.Length != 16)
            {
                Debug.LogError("[GuidanceManager] Invalid service response.");
                return;
            }

            Debug.Log("[GuidanceManager] Received ICP correction matrix.");

            // Dispatch to Main Thread via Update loop
            _pendingMatrixData = response.transformation_matrix;
            _hasPendingCorrection = true;
        }

        private void ApplyCorrection(float[] matrixData)
        {
            // 3-1. Parse Matrix (Row-Major from Open3D/Numpy usually)
            // T_icp moves Source (Current) -> Target (Master)
            Matrix4x4 T_icp = ArrayToMatrix(matrixData);
            
            // 3-2. Calculate Target Camera Pose
            // Since points are in Camera-Local frame, T_icp is a local transformation.
            // To make the views match, we need to move the camera by T_icp.inverse (Local).
            // T_cam_target = T_cam_current * T_icp.inverse
            
            Matrix4x4 T_cam_current = Matrix4x4.TRS(cameraTransform.position, cameraTransform.rotation, Vector3.one);
            Matrix4x4 T_cam_target = T_cam_current * T_icp.inverse;

            // 3-3. Calculate Target TCP Pose
            // T_tcp_world = T_cam_world * T_tcp_to_cam^-1
            Matrix4x4 T_offset_inv = m_T_tcp_to_cam.inverse;
            Matrix4x4 T_tcp_target = T_cam_target * T_offset_inv;

            // 4. Command Robot
            Vector3 targetPos = T_tcp_target.GetColumn(3);
            Quaternion targetRot = T_tcp_target.rotation;

            Debug.Log($"[GuidanceManager] Applying Correction: Pos={targetPos}, Rot={targetRot.eulerAngles}");

            // Apply to Mover's Target Ghost
            Mover.targetTransform.position = targetPos;
            Mover.targetTransform.rotation = targetRot;

            Debug.Log($"[GuidanceManager] Sending Move Request. (Gap: {Vector3.Distance(tcpTransform.position, targetPos):F4}m)");
            Mover.SendMoveRequest();
        }

        private Matrix4x4 ArrayToMatrix(float[] arr)
        {
            // Assume Row-Major (standard C/Python/ROS)
            Matrix4x4 m = new Matrix4x4();
            m.SetRow(0, new Vector4(arr[0], arr[1], arr[2], arr[3]));
            m.SetRow(1, new Vector4(arr[4], arr[5], arr[6], arr[7]));
            m.SetRow(2, new Vector4(arr[8], arr[9], arr[10], arr[11]));
            m.SetRow(3, new Vector4(arr[12], arr[13], arr[14], arr[15]));
            return m; 
        }

        private RosSharp.RosBridgeClient.MessageTypes.Sensor.PointCloud2 ToPointCloud2(List<Vector3> points)
        {
            var msg = new RosSharp.RosBridgeClient.MessageTypes.Sensor.PointCloud2();
            msg.header = new RosSharp.RosBridgeClient.MessageTypes.Std.Header { frame_id = "unity_camera" }; // Changed to Camera Frame
            msg.height = 1;
            msg.width = (uint)points.Count;
            msg.is_bigendian = false;
            msg.is_dense = true;
            
            // Define Fields: x, y, z
            msg.fields = new RosSharp.RosBridgeClient.MessageTypes.Sensor.PointField[3];
            msg.fields[0] = new RosSharp.RosBridgeClient.MessageTypes.Sensor.PointField { name = "x", offset = 0, datatype = 7, count = 1 }; // FLOAT32
            msg.fields[1] = new RosSharp.RosBridgeClient.MessageTypes.Sensor.PointField { name = "y", offset = 4, datatype = 7, count = 1 }; 
            msg.fields[2] = new RosSharp.RosBridgeClient.MessageTypes.Sensor.PointField { name = "z", offset = 8, datatype = 7, count = 1 };
            msg.point_step = 12;
            msg.row_step = msg.point_step * msg.width;

            // Convert Data
            byte[] byteArray = new byte[msg.row_step * msg.height];
            int offset = 0;
            foreach (var p in points)
            {
                // Unity is Left-handed, ROS is Right-handed.
                // BUT user said: "Send Unity coordinates directly (RUF)". So we just send x,y,z as is.
                
                System.Buffer.BlockCopy(System.BitConverter.GetBytes(p.x), 0, byteArray, offset + 0, 4);
                System.Buffer.BlockCopy(System.BitConverter.GetBytes(p.y), 0, byteArray, offset + 4, 4);
                System.Buffer.BlockCopy(System.BitConverter.GetBytes(p.z), 0, byteArray, offset + 8, 4);
                offset += 12;
            }
            msg.data = byteArray;

            return msg;
        }
    }
}