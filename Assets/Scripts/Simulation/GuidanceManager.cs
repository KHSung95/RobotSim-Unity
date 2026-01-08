using UnityEngine;
using System.Collections.Generic;
using RobotSim.Sensors;
using RobotSim.ROS;
using RobotSim.Robot;
using RobotSim.Utils;
using RobotSim.ROS.Services;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Custom;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

namespace RobotSim.Simulation
{
    public class GuidanceManager : MonoBehaviour
    {
        [Header("References")]
        public VirtualCameraMount CamMount; // Replaces PCG
        public PointCloudVisualizer PCV; 
        public SceneAnalysisClient AnalysisClient;

        [Header("Industrial Logic (T_ic)")]
        public MoveRobotToPoseClient Mover;
        public RobotStateProvider RobotState;

        [Header("ROS Connection")]
        public RosConnector Connector;
        public string ServiceName = "/calculate_icp";
        
        [Header("Settings")]
        [SerializeField] private float _errorThreshold = 0.002f;
        public float ErrorThreshold 
        { 
            get => _errorThreshold; 
            set 
            { 
                _errorThreshold = value; 
                Debug.Log($"[GuidanceManager] ErrorThreshold changed to: {value}");
            } 
        }

        private Transform _tcpTransform;    // Robot Flange (End-Effector)
        private Transform _camTransform;
        private Matrix4x4 m_T_tcp_to_cam;

        private void Start()
        {
            if (CamMount == null) CamMount = FindFirstObjectByType<VirtualCameraMount>();
            if (PCV == null) PCV = FindFirstObjectByType<PointCloudVisualizer>();
            if (AnalysisClient == null) AnalysisClient = FindFirstObjectByType<SceneAnalysisClient>();
            
            if (Mover == null) Mover = FindFirstObjectByType<MoveRobotToPoseClient>();
            if (RobotState == null) RobotState = FindFirstObjectByType<RobotStateProvider>();
            if (Connector == null) Connector = FindFirstObjectByType<RosConnector>();

            // Auto-assign transforms if missing
            if (RobotState != null) _tcpTransform = RobotState.TcpTransform;
            if (CamMount != null) _camTransform = CamMount.SensorTransform;

            // Calculate Hand-Eye Offset (Static assumption)
            if (_tcpTransform != null && CamMount != null)
            {
                m_T_tcp_to_cam = _tcpTransform.worldToLocalMatrix * _camTransform.localToWorldMatrix;
            }

            // Subscribe to CamMount events
            if (CamMount != null)
            {
                CamMount.OnMasterCaptured += HandleMasterCaptured;
                CamMount.OnScanCaptured += HandleScanCaptured;
            }
        }

        private void OnDestroy()
        {
            if (CamMount != null)
            {
                CamMount.OnMasterCaptured -= HandleMasterCaptured;
                CamMount.OnScanCaptured -= HandleScanCaptured;
            }
        }

        private void HandleMasterCaptured()
        {
            if (PCV != null) PCV.UpdateMasterMesh(CamMount.MasterPoints);
            
            // Send Master to ROS Analysis
            if (AnalysisClient != null && CamMount.MasterPoints.Count > 0)
            {
                PointCloud2 cloudMsg = CamMount.MasterPoints.ToPointCloud2();
                AnalysisClient.SendAnalysisRequest("SET_MASTER", 0, cloudMsg, (res) =>
                {
                    if (res.success) Debug.Log("[GuidanceManager] Master Cloud Set on ROS.");
                    else Debug.LogError("[GuidanceManager] Failed to set Master Cloud on ROS.");
                });
            }
        }

        private void HandleScanCaptured()
        {
            PCV?.UpdateScanMesh(CamMount.ScanPoints);
        }

        [ContextMenu("Capture Master")]
        public void CaptureMaster()
        {
            if (CamMount != null) CamMount.CaptureMaster();
        }

        [ContextMenu("Capture Scan (Current)")]
        public void CaptureCurrent()
        {
            if (CamMount != null) CamMount.CaptureScan();
        }

        [ContextMenu("Analyze Scene")]
        public void AnalyzeScene(bool runAnalysis = true)
        {
            if (CamMount == null) return;

            // Capture always happens
            CaptureCurrent();

            // Only proceed to ROS analysis if requested and client exists
            if (!runAnalysis || AnalysisClient == null) return;

            if (CamMount.ScanPoints.Count == 0) return;

            PointCloud2 cloudMsg = CamMount.ScanPoints.ToPointCloud2();
            Debug.Log($"[GuidanceManager] Sending COMPARE request with Threshold: {ErrorThreshold:F4}");
            AnalysisClient.SendAnalysisRequest("COMPARE", ErrorThreshold, cloudMsg, (response) => {
                if (response != null && response.result_cloud != null && response.result_cloud.data.Length > 0)
                {
                    Debug.Log($"[GuidanceManager] Received analysis result cloud: {response.result_cloud.width * response.result_cloud.height} points");
                    if (PCV != null) PCV.ColorizeFromAnalysis(response.result_cloud, out _, out _);
                }
                else
                {
                    Debug.LogWarning("[GuidanceManager] Analysis response contained no cloud data.");
                }
            });
        }

        [ContextMenu("Run Guidance (Service)")]
        public void RunGuidance()
        {
            if (Connector == null || CamMount == null || Mover == null) 
            {
                Debug.LogError("[GuidanceManager] Missing dependencies.");
                return;
            }

            if (CamMount.MasterPoints.Count == 0)
            {
                Debug.LogWarning("[GuidanceManager] Empty data. Capture first.");
                return;
            }

            // Save state and hide scan during guidance
            if (PCV != null)
            {
                _originalShowScanState = PCV.ShowScan;
                PCV.ShowScan = false;
            }

            if (CamMount.ScanPoints.Count == 0)
            {
                // Just capture without analysis
                AnalyzeScene(false);
            }

            var req = new RosSharp.RosBridgeClient.MessageTypes.CustomServices.CalculateICPRequest();
            req.master_point_cloud = CamMount.MasterPoints.ToPointCloud2();
            req.current_point_cloud = CamMount.ScanPoints.ToPointCloud2();

            Connector.RosSocket.CallService<RosSharp.RosBridgeClient.MessageTypes.CustomServices.CalculateICPRequest, RosSharp.RosBridgeClient.MessageTypes.CustomServices.CalculateICPResponse>(
                ServiceName,
                OnGuidanceResponse,
                req
            );
        }

        private void Update()
        {
            // Update Visualizer Pose
            if (PCV != null && CamMount != null)
            {
                PCV.SetPose(CamMount.SensorTransform.position, CamMount.SensorTransform.rotation);
            }

            // Robot Movement Detection and Stop Logic
            UpdateRobotStateAndStopDetection();

            if (_hasPendingCorrection)
            {
                _hasPendingCorrection = false;
                ApplyCorrection(_pendingMatrixData);
                _pendingMatrixData = null;
            }
        }

        private void UpdateRobotStateAndStopDetection()
        {
            if (RobotState == null || RobotState.TcpTransform == null) return;

            if (_isFirstUpdate)
            {
                _lastTcpPos = RobotState.TcpTransform.position;
                _lastTcpRot = RobotState.TcpTransform.rotation;
                _isFirstUpdate = false;
                return;
            }

            float dist = Vector3.Distance(RobotState.TcpTransform.position, _lastTcpPos);
            float angle = Quaternion.Angle(RobotState.TcpTransform.rotation, _lastTcpRot);

            // Thresholds for movement
            bool isActuallyMoving = dist > 0.0001f || angle > 0.01f;

            // Clear scan if movement is significant
            if (dist > 0.005f || angle > 0.1f)
            {
                CamMount?.ClearScan();
            }

            // Detect Stop for Guidance Completion
            if (_waitingForStop)
            {
                if (!_movementStarted)
                {
                    // Phase 1: Waiting for robot to begin moving
                    if (isActuallyMoving)
                    {
                        _movementStarted = true;
                        Debug.Log("[GuidanceManager] Robot movement detected.");
                    }
                    else
                    {
                        _startWaitTimer += Time.deltaTime;
                        if (_startWaitTimer >= START_TIMEOUT)
                        {
                            Debug.LogWarning("[GuidanceManager] Robot didn't move within timeout. Proceeding to analysis.");
                            _movementStarted = true; // Force transition to stop detection
                        }
                    }
                }
                
                if (_movementStarted)
                {
                    // Phase 2: Detecting when robot finally stops
                    if (!isActuallyMoving)
                    {
                        _stopTimer += Time.deltaTime;
                        if (_stopTimer >= STOP_DELAY)
                        {
                            CompleteGuidance();
                        }
                    }
                    else
                    {
                        _stopTimer = 0; // Reset if still moving
                    }
                }
            }

            _lastTcpPos = RobotState.TcpTransform.position;
            _lastTcpRot = RobotState.TcpTransform.rotation;
        }

        private Vector3 _lastTcpPos;
        private Quaternion _lastTcpRot;
        private bool _isFirstUpdate = true;
        private bool _hasPendingCorrection = false;
        private float[] _pendingMatrixData;

        // Guidance state tracking
        private bool _waitingForStop = false;
        private bool _movementStarted = false; // New: tracking if movement actually began
        private float _stopTimer = 0;
        private float _startWaitTimer = 0;     // New: safety timeout for starting
        private bool _originalShowScanState = true;
        
        private const float STOP_DELAY = 0.6f;      // Wait 0.6s after stop
        private const float START_TIMEOUT = 3.0f;   // Max wait for movement to start

        private void OnGuidanceResponse(RosSharp.RosBridgeClient.MessageTypes.CustomServices.CalculateICPResponse response)
        {
            if (response == null || response.transformation_matrix == null || response.transformation_matrix.Length != 16)
            {
                Debug.LogWarning("[GuidanceManager] ICP Calculation failed or returned invalid data.");
                RestoreScanState();
                return;
            }
            _pendingMatrixData = response.transformation_matrix;
            _hasPendingCorrection = true;
        }

        private void RestoreScanState()
        {
            if (PCV != null) PCV.ShowScan = _originalShowScanState;
            _waitingForStop = false;
            _movementStarted = false;
        }

        private void ApplyCorrection(float[] matrixData)
        {
            Matrix4x4 T_icp = ArrayToMatrix(matrixData);
            Matrix4x4 T_cam_current = Matrix4x4.TRS(_camTransform.position, _camTransform.rotation, Vector3.one);
            Matrix4x4 T_cam_target = T_cam_current * T_icp.inverse;
            Matrix4x4 T_tcp_target = T_cam_target * m_T_tcp_to_cam.inverse;

            Mover.targetTransform.position = T_tcp_target.GetColumn(3);
            Mover.targetTransform.rotation = T_tcp_target.rotation;
            
            Mover.SendMoveRequest((success) => {
                if (success)
                {
                    Debug.Log("[GuidanceManager] Guidance command accepted. Waiting for robot to move then stop...");
                    _waitingForStop = true;
                    _movementStarted = false;
                    _stopTimer = 0;
                    _startWaitTimer = 0;
                }
                else
                {
                    Debug.LogWarning("[GuidanceManager] Path planning failed. Robot will not move.");
                    RestoreScanState();
                }
            });
        }

        private void CompleteGuidance()
        {
            _waitingForStop = false;
            _movementStarted = false;
            _stopTimer = 0;
            _startWaitTimer = 0;
            
            Debug.Log("[GuidanceManager] Guidance workflow complete. Performing final analysis.");
            AnalyzeScene(true);
            if (PCV != null) PCV.ShowScan = _originalShowScanState;
        }

        private Matrix4x4 ArrayToMatrix(float[] arr)
        {
            Matrix4x4 m = new Matrix4x4();
            m.SetRow(0, new Vector4(arr[0], arr[1], arr[2], arr[3]));
            m.SetRow(1, new Vector4(arr[4], arr[5], arr[6], arr[7]));
            m.SetRow(2, new Vector4(arr[8], arr[9], arr[10], arr[11]));
            m.SetRow(3, new Vector4(arr[12], arr[13], arr[14], arr[15]));
            return m; 
        }
    }
}