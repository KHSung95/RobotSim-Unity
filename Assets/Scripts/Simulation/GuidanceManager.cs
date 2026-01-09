using RobotSim.Control;
using RobotSim.Robot;
using RobotSim.ROS;
using RobotSim.ROS.Services;
using RobotSim.Sensors;
using RobotSim.Utils;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.CustomServices;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using System.Collections.Generic;
using UnityEngine;

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
        private List<Vector3> _masterPoints_TCP = new List<Vector3>(); // Cached Master in TCP Frame

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
            if (_pcv == null) return;

            // [추가] 마스터 캡처 당시의 TCP 위치 저장
            if (_tcpTransform != null)
            {
                m_T_tcp_master = Matrix4x4.TRS(_tcpTransform.position, _tcpTransform.rotation, Vector3.one);
                Debug.Log($"[GuidanceManager] Master TCP Pose Saved: {m_T_tcp_master.GetColumn(3)}");
            }

            // 2. Prepare Master Points for Visualization & ROS
            if (CamMount.MountType == CameraMountType.BirdEye && _tcpTransform != null)
            {
                // [Bird-Eye] Convert Camera -> Master-TCP Frame
                // T_tcp_to_cam_at_capture = T_tcp_master_inv * T_cam_world
                Matrix4x4 T_cam_to_tcp = m_T_tcp_master.inverse * CamMount.SensorTransform.localToWorldMatrix;
                _masterPoints_TCP = TransformPoints(CamMount.MasterPoints, T_cam_to_tcp);
                
                _pcv.UpdateMasterMesh(_masterPoints_TCP);
            }
            else
            {
                // [Hand-Eye] Camera Frame is already the relative frame
                _masterPoints_TCP = new List<Vector3>(CamMount.MasterPoints);
                _pcv.UpdateMasterMesh(_masterPoints_TCP);
            }
            
            // 3. Send relative Master to ROS for future Comparisons
            if (_analysis != null && _masterPoints_TCP.Count > 0)
            {
                _analysis.SendAnalysisRequest("SET_MASTER", 0, _masterPoints_TCP.ToPointCloud2(), (res) =>
                {
                    if (res.success) Debug.Log("[GuidanceManager] TCP-Relative Master Cloud Set on ROS.");
                    else Debug.LogError("[GuidanceManager] Failed to set Master Cloud on ROS.");
                });
            }
        }

        private void HandleScanCaptured()
        {
            if (_pcv == null || CamMount == null) return;

            if (CamMount.MountType == CameraMountType.BirdEye && _tcpTransform != null)
            {
                // [Bird-Eye] Current Camera -> Current TCP Frame
                Matrix4x4 T_cam_to_tcp_current = _tcpTransform.worldToLocalMatrix * CamMount.SensorTransform.localToWorldMatrix;
                List<Vector3> tcpScan = TransformPoints(CamMount.ScanPoints, T_cam_to_tcp_current);
                _pcv.UpdateScanMesh(tcpScan);
            }
            else
            {
                // [Hand-Eye] Raw Camera Points
                _pcv.UpdateScanMesh(CamMount.ScanPoints);
            }
        }

        private List<Vector3> TransformPointsCamToTcp(List<Vector3> camPoints)
        {
            if (_tcpTransform == null || CamMount == null) return camPoints;
            
            // T_tcp_inv * T_cam
            Matrix4x4 T_tcp_to_cam = _tcpTransform.worldToLocalMatrix * CamMount.SensorTransform.localToWorldMatrix;
            
            List<Vector3> tcpPoints = new List<Vector3>(camPoints.Count);
            foreach (var p in camPoints)
            {
                tcpPoints.Add(T_tcp_to_cam.MultiplyPoint3x4(p));
            }
            return tcpPoints;
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

            // Use TCP-relative points for comparison in Bird-Eye mode
            List<Vector3> ptsToSend = CamMount.ScanPoints;
            if (CamMount.MountType == CameraMountType.BirdEye && _tcpTransform != null)
            {
                Matrix4x4 T_cam_to_tcp = _tcpTransform.worldToLocalMatrix * CamMount.SensorTransform.localToWorldMatrix;
                ptsToSend = TransformPoints(CamMount.ScanPoints, T_cam_to_tcp);
            }

            PointCloud2 cloudMsg = ptsToSend.ToPointCloud2();
            Debug.Log($"[GuidanceManager] Sending COMPARE request (TCP-Space) with Threshold: {ErrorThreshold:F4}");
            _analysis.SendAnalysisRequest("COMPARE", ErrorThreshold, cloudMsg, (response) => {
                if (response != null && response.result_cloud != null && response.result_cloud.data.Length > 0)
                {
                    Debug.Log($"[GuidanceManager] Received analysis result cloud: {response.result_cloud.width * response.result_cloud.height} points");
                    if (_pcv != null) 
                    {
                        // Heatmap points from ROS are now in TCP space (because we sent TCP space points)
                        _pcv.ColorizeFromAnalysis(response.result_cloud, out List<Vector3> pts, out List<Color> colors);
                        _pcv.UpdateScanMesh(pts, colors);
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

            // Sync Scene Objects before planning for safety
            SyncSceneToRos();

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

            // Transform Points for ICP based on Mode
            // In Bird-Eye, we use TCP-relative clouds for both Master and Current.
            // This calculates a TCP-local delta.
            PointCloud2 masterMsg;
            PointCloud2 currentMsg;

            if (CamMount.MountType == CameraMountType.BirdEye && _tcpTransform != null)
            {
                masterMsg = _masterPoints_TCP.ToPointCloud2();
                Matrix4x4 T_cam_to_tcp = _tcpTransform.worldToLocalMatrix * CamMount.SensorTransform.localToWorldMatrix;
                currentMsg = TransformPoints(CamMount.ScanPoints, T_cam_to_tcp).ToPointCloud2();
                Debug.Log("[GuidanceManager] ICP: Sending TCP-relative points (Bird-Eye)");
            }
            else
            {
                masterMsg = CamMount.MasterPoints.ToPointCloud2();
                currentMsg = CamMount.ScanPoints.ToPointCloud2();
                Debug.Log("[GuidanceManager] ICP: Sending Camera-relative points (Hand-Eye)");
            }

            var req = new CalculateICPRequest();
            req.master_point_cloud = masterMsg;
            req.current_point_cloud = currentMsg;

            Connector.RosSocket.CallService<CalculateICPRequest, CalculateICPResponse>(
                ServiceName,
                OnGuidanceResponse,
                req
            );
        }

        private void Update()
        {
            // Update Visualizer Pose (Manually sync world pose as a separate object)
            if (_pcv != null && CamMount != null)
            {
                Transform targetPoseSource = (CamMount.MountType == CameraMountType.BirdEye && _tcpTransform != null) 
                    ? _tcpTransform 
                    : CamMount.SensorTransform;

                _pcv.SetPose(targetPoseSource.position, targetPoseSource.rotation);
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

            if (CamMount.MountType == CameraMountType.HandEye)
            {
                // [Hand-Eye] T_icp is in Camera Frame. Revert to Camera viewpoint matching.
                Matrix4x4 T_cam_current = Matrix4x4.TRS(_camTransform.position, _camTransform.rotation, Vector3.one);
                Matrix4x4 T_cam_target = T_cam_current * T_icp.inverse;
                T_tcp_target = T_cam_target * m_T_tcp_to_cam.inverse;
            }
            else if (CamMount.MountType == CameraMountType.BirdEye)
            {
                // [Bird-Eye] T_icp is now already in TCP Frame!
                // Target = CurrentTCP * (MasterRelative * CurrentRelative.inv)
                // ICP gives T_icp such that Points_Master = T_icp * Points_Current.
                // This T_icp is exactly the displacement we need to apply to the TCP.
                
                Matrix4x4 T_tcp_current = Matrix4x4.TRS(_tcpTransform.position, _tcpTransform.rotation, Vector3.one);
                
                // Direction Logic: Use T_icp.inverse based on previous "moving away" feedback.
                T_tcp_target = T_tcp_current * T_icp.inverse;
                
                Debug.Log($"[Guidance] Bird-Eye Correction (TCP-local). Delta: {T_icp.inverse.GetColumn(3)}");
            }

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

        private List<Vector3> TransformPoints(List<Vector3> points, Matrix4x4 transform)
        {
            List<Vector3> result = new List<Vector3>(points.Count);
            foreach (var p in points) result.Add(transform.MultiplyPoint3x4(p));
            return result;
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