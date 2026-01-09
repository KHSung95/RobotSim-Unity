using UnityEngine;
using System.Collections.Generic;

using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.CustomServices;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

using RobotSim.Utils;
using RobotSim.Robot;
using RobotSim.ROS;
using RobotSim.ROS.Services;
using RobotSim.Sensors;

namespace RobotSim.Simulation
{
    [RequireComponent(typeof(SceneAnalysisClient))]
    [RequireComponent(typeof(PointCloudVisualizer))]
    public class GuidanceManager : MonoBehaviour
    {

        [Header("References")]
        public VirtualCameraMount CamMount;

        public MoveRobotToPoseClient Mover;
        public RobotStateProvider RobotState;

        [Header("ROS Connection")]
        public RosConnector Connector;
        public string ServiceName = "/calculate_icp";

        private SceneAnalysisClient _analysis;
        private PointCloudVisualizer _pcv;

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
        private Matrix4x4 m_T_tcp_master;     // Saved TCP Pose at Master Capture time
        private Matrix4x4 m_T_tcp_current;    // Saved TCP Pose at Current Scan Capture time
        
        private List<PointData> _masterPoints_TCP = new List<PointData>(); // Cached Master in TCP Frame
        private List<PointData> _currentScan_TCP = new List<PointData>();  // Cached Scan in TCP Frame
        
        private List<Vector3> _analyzedPoints_TCP = new List<Vector3>();   // Cached Analysis result (Points)
        private List<Color> _analyzedColors = new List<Color>();           // Cached Analysis result (Colors)
        private bool _hasAnalysisResult = false;

        private void Start()
        {
            if (CamMount == null) CamMount = FindFirstObjectByType<VirtualCameraMount>();
            if (_pcv == null) _pcv = GetComponent<PointCloudVisualizer>();
            if (_analysis == null) _analysis = GetComponent<SceneAnalysisClient>();
            
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
            if (_pcv == null || _tcpTransform == null) return;

            // 1. 촬영 당시의 TCP 포즈 저장 (추후 역변환 시각화용)
            m_T_tcp_master = Matrix4x4.TRS(_tcpTransform.position, _tcpTransform.rotation, Vector3.one);

            // 2. TCP 상대 좌표로 변환하여 저장 (표준화)
            Matrix4x4 T_cam_to_tcp = _tcpTransform.worldToLocalMatrix * CamMount.SensorTransform.localToWorldMatrix;
            _masterPoints_TCP = CamMount.MasterPoints.TransformPoints(T_cam_to_tcp);
            
            // 시각화 업데이트 (TCP 기준)
            _pcv.UpdateMasterMesh(_masterPoints_TCP.Points());
            
            Debug.Log($"[GuidanceManager] Master Stored (TCP-Relative): {_masterPoints_TCP.Count} points.");

            // 3. ROS에 마스터 설정 전송
            if (_analysis != null && _masterPoints_TCP.Count > 0)
            {
                _analysis.SendAnalysisRequest("SET_MASTER", 0, _masterPoints_TCP.ToPointCloud2(), (res) =>
                {
                    if (res.success) Debug.Log("[GuidanceManager] Master Cloud Set on ROS (TCP Frame).");
                    else Debug.LogError("[GuidanceManager] Failed to set Master Cloud on ROS.");
                });
            }
        }

        private void HandleScanCaptured()
        {
            if (_pcv == null || CamMount == null || _tcpTransform == null) return;

            // 1. 촬영 당시의 TCP 포즈 저장 (버드아이 시각화 고정용)
            m_T_tcp_current = Matrix4x4.TRS(_tcpTransform.position, _tcpTransform.rotation, Vector3.one);

            // 2. TCP 상대 좌표로 변환하여 저장 (표준화)
            Matrix4x4 T_cam_to_tcp = _tcpTransform.worldToLocalMatrix * CamMount.SensorTransform.localToWorldMatrix;
            _currentScan_TCP = CamMount.ScanPoints.TransformPoints(T_cam_to_tcp);
            
            // 비주얼라이저에 업데이트 (TCP 기준)
            _pcv.UpdateScanMesh(_currentScan_TCP.Points());
            
            _hasAnalysisResult = false; // New scan, old analysis is invalid
            Debug.Log($"[GuidanceManager] Scan Stored (TCP-Relative): {_currentScan_TCP.Count} points.");
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

            CaptureCurrent();

            if (!runAnalysis || _analysis == null) return;
            if (CamMount.ScanPoints.Count == 0) return;

            // Use the pre-converted TCP-relative points
            PointCloud2 cloudMsg = _currentScan_TCP.ToPointCloud2();
            
            Debug.Log($"[GuidanceManager] Sending COMPARE request (TCP-Space) with Threshold: {ErrorThreshold:F4}");
            _analysis.SendAnalysisRequest("COMPARE", ErrorThreshold, cloudMsg, (response) => {
                if (response != null && response.result_cloud != null && response.result_cloud.data.Length > 0)
                {
                    Debug.Log($"[GuidanceManager] Received analysis result cloud: {response.result_cloud.width * response.result_cloud.height} points");
                    if (_pcv != null) 
                    {
                        // ROS 결과를 TCP 기반 캐시에 저장
                        _pcv.ColorizeFromAnalysis(response.result_cloud, out _analyzedPoints_TCP, out _analyzedColors);
                        _hasAnalysisResult = true;
                        
                        // 즉시 시각화 업데이트
                        _pcv.UpdateScanMesh(_analyzedPoints_TCP, _analyzedColors);
                    }
                }
                else
                {
                    Debug.LogWarning("[GuidanceManager] Analysis response contained no cloud data.");
                }
            });
        }

        [ContextMenu("Sync Scene Collision Objects")]
        public void SyncSceneToRos()
        {
            var publishers = FindObjectsByType<CollisionObjectPublisher>(FindObjectsSortMode.None);
            foreach (var pub in publishers) pub.PublishNow();
            
            // 추후 여러 attachables가 존재한다면 별도의 클래스로 구현 필요
            var attachables = FindObjectsByType<AttachableCollisionObjectPublisher>(FindObjectsSortMode.None);
            foreach (var pub in attachables)
            {
                if (pub != null && CamMount != null) pub.Synchronize(CamMount.MountType);
            }

            Debug.Log($"[GuidanceManager] Synced {publishers.Length} collision objects and {attachables.Length} tools to ROS.");
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
            if (_pcv != null)
            {
                _originalShowScanState = _pcv.ShowScan;
                _pcv.ShowScan = false;
            }

            if (CamMount.ScanPoints.Count == 0)
            {
                // Just capture without analysis
                AnalyzeScene(false);
            }

            // Sync Scene Objects before planning for safety
            SyncSceneToRos();

            // Transform Points for ICP (Both are now standardized to TCP)
            Debug.Log($"[GuidanceManager] ICP: Sending standardized TCP-relative points (Mode: {CamMount.MountType})");

            var req = new CalculateICPRequest(
                _masterPoints_TCP.ToPointCloud2(),
                _currentScan_TCP.ToPointCloud2());

            Connector.RosSocket.CallService<CalculateICPRequest, CalculateICPResponse>(
                ServiceName,
                OnGuidanceResponse,
                req
            );
        }

        private void Update()
        {
            if (_pcv == null || CamMount == null || _tcpTransform == null) return;

            // 1. 비주얼라이저 컨테이너 포즈 설정 (항상 현재 TCP 추종)
            _pcv.SetPose(_tcpTransform.position, _tcpTransform.rotation);

            // [Analysis Results Stability]: 히트맵 결과가 있으면 분석 당시의 월드 위치에 고정
            if (_hasAnalysisResult && _analyzedPoints_TCP.Count > 0)
            {
                Matrix4x4 T_fix = _tcpTransform.worldToLocalMatrix * m_T_tcp_current;
                List<Vector3> stablePoints = _analyzedPoints_TCP.TransformPoints(T_fix);
                _pcv.UpdateScanMesh(stablePoints, _analyzedColors);
            }
            // [Scan Stability]: 분석 결과가 없으면 원본 스캔 데이터를 월드 위치에 고정
            else if (_currentScan_TCP != null && _currentScan_TCP.Count > 0)
            {
                Matrix4x4 T_fix_scan = _tcpTransform.worldToLocalMatrix * m_T_tcp_current;
                List<PointData> stableScan = _currentScan_TCP.TransformPoints(T_fix_scan);
                _pcv.UpdateScanMesh(stableScan.Points());
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

        public void SetPointCloudVisible(bool visible)
        {
            _pcv.ShowMaster = _pcv.ShowScan = visible;
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

        private void OnGuidanceResponse(CalculateICPResponse response)
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
            if (_pcv != null) _pcv.ShowScan = _originalShowScanState;
            _waitingForStop = false;
            _movementStarted = false;
        }

        private void ApplyCorrection(float[] matrixData)
        {
            // T_icp: Delta from Source(Current) -> Target(Master)
            Matrix4x4 T_icp = ArrayToMatrix(matrixData);
            
            Matrix4x4 T_tcp_target = Matrix4x4.identity;

            // T_icp is now already in TCP Frame!
            // Target = CurrentTCP * (MasterRelative * CurrentRelative.inv)
            // ICP gives T_icp such that Points_Master = T_icp * Points_Current.
            // This T_icp is exactly the displacement we need to apply to the TCP.
                
            Matrix4x4 T_tcp_current = Matrix4x4.TRS(_tcpTransform.position, _tcpTransform.rotation, Vector3.one);
                
            // Direction Logic: Use T_icp.inverse based on previous "moving away" feedback.
            T_tcp_target = T_tcp_current * T_icp.inverse;
                
            Debug.Log($"[Guidance] Correction Delta: {T_icp.inverse.GetColumn(3)}");

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
            if (_pcv != null) _pcv.ShowScan = _originalShowScanState;
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