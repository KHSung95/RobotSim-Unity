using UnityEngine;
using System.Collections.Generic;
using System;
using RosSharp;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Custom;
using RobotSim.ROS.Services;
using RobotSim.Utils;
using RosettaField = RosSharp.RosBridgeClient.MessageTypes.Sensor.PointField;

namespace RobotSim.Sensors
{
    [RequireComponent(typeof(Camera))]
    public class PointCloudGenerator : MonoBehaviour
    {
        private Transform RobotBaseReference;

        [Header("Analysis Settings")]
        public Color MatchColor = Color.green;
        public Color ErrorColor = Color.red;
        public float ErrorThreshold = 0.01f;

        [Header("Scan Settings")]
        public int Width = 160;
        public int Height = 120;
        public float MaxDistance = 2.0f;
        public LayerMask ScanTypes = -1;

        [Header("Visualization")]
        public bool ShowPoints = true;
        [Range(0.001f, 10f)] public float PointSize = 0.01f;

        [Header("Sensor Simulation")]
        public float NoiseLevel = 0.0005f;

        // Internal Data
        private List<Vector3> masterPoints = new List<Vector3>();
        private List<Color> masterColors = new List<Color>();
        private List<Vector3> scanPoints = new List<Vector3>();
        private List<Color> scanColors = new List<Color>();

        public List<Vector3> MasterPoints => masterPoints;
        public List<Vector3> ScanPoints => scanPoints;

        private Camera cam;

        // Visualizer
        private GameObject rootContainer;
        private MeshRenderer masterRenderer;
        private MeshFilter masterFilter;
        private MeshRenderer scanRenderer;
        private MeshFilter scanFilter;

        // ROS Client
        private SceneAnalysisClient analysisClient;

        private void Start()
        {
            cam = GetComponent<Camera>();
            analysisClient = GetComponent<SceneAnalysisClient>();
            if (analysisClient == null) analysisClient = FindFirstObjectByType<SceneAnalysisClient>();

            // Create Visualizers
            rootContainer = new GameObject("PointCloud_Visualizer");
            CreateSubVisualizer("Master_Cloud", out masterFilter, out masterRenderer);
            CreateSubVisualizer("Scan_Cloud", out scanFilter, out scanRenderer);
        }

        private void CreateSubVisualizer(string name, out MeshFilter filter, out MeshRenderer rend)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(rootContainer.transform, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;

            filter = obj.AddComponent<MeshFilter>();
            rend = obj.AddComponent<MeshRenderer>();

            Mesh m = new Mesh();
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            filter.mesh = m;

            var shader = Shader.Find("Custom/PointCloudShader");
            if (shader) rend.material = new Material(shader);
        }

        public void SetRobotBase(Transform robotBase)
        {
            RobotBaseReference = robotBase;
            // [Modified] Separate Sync Architecture
            // We NO LONGER parent the visualizer to the camera.
            // Instead, we sync its world pose in LateUpdate.
            if (rootContainer != null)
            {
                rootContainer.transform.SetParent(null); 
                SyncVisualizer();
            }
        }

        public void ClearScan()
        {
            scanPoints.Clear();
            scanColors.Clear();
            if (scanFilter != null && scanFilter.mesh != null) scanFilter.mesh.Clear();
        }

        private void Update()
        {
            if (masterRenderer != null && masterRenderer.material != null)
                masterRenderer.material.SetFloat("_PointSize", PointSize);

            if (scanRenderer != null && scanRenderer.material != null)
                scanRenderer.material.SetFloat("_PointSize", PointSize);
        }

        private void LateUpdate()
        {
            // Sync visualizer to camera pose every frame
            SyncVisualizer();
        }

        private void SyncVisualizer()
        {
            if (rootContainer != null)
            {
                rootContainer.transform.position = transform.position;
                rootContainer.transform.rotation = transform.rotation;
            }
        }

        // --- Capture Logic ---

        public void CaptureMaster()
        {
            // Capture in Camera Local Space (Eye-in-Hand)
            Matrix4x4 matrix = this.transform.worldToLocalMatrix;
            
            CaptureInternal(matrix, Color.white, true, ref masterPoints, ref masterColors);
            UpdateMesh(masterFilter.mesh, masterPoints, masterColors);
            Debug.Log($"[PointCloudGenerator] Captured Master: {masterPoints.Count} points. Frame: CameraLocal");

            // [NEW] Send SET_MASTER to ROS
            if (analysisClient != null)
            {
                if (masterPoints.Count > 0)
                {
                    PointCloud2 cloudMsg = ConvertToPointCloud2(masterPoints);
                    analysisClient.SendAnalysisRequest("SET_MASTER", 0, cloudMsg, (res) =>
                    {
                        if (res.success) Debug.Log("[PointCloudGenerator] Master Cloud Set on ROS.");
                        else Debug.LogError("[PointCloudGenerator] Failed to set Master Cloud on ROS.");
                    });
                }
            }
            else
            {
                Debug.LogWarning("[PointCloudGenerator] SceneAnalysisClient missing. Master not sent to ROS.");
            }
        }

        public void CaptureScan()
        {
            // Capture in Camera Local Space
            Matrix4x4 matrix = this.transform.worldToLocalMatrix;

            CaptureInternal(matrix, Color.white, false, ref scanPoints, ref scanColors);
            UpdateMesh(scanFilter.mesh, scanPoints, scanColors);
            Debug.Log($"[PointCloudGenerator] Captured Scan: {scanPoints.Count} points. Frame: CameraLocal");
        }

        private void CaptureInternal(Matrix4x4 T_ref_world, Color defaultColor, bool isMaster, ref List<Vector3> points, ref List<Color> colors)
        {
            points.Clear();
            colors.Clear();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector3 viewPos = new Vector3((float)x / Width, (float)y / Height, 0);
                    Ray ray = cam.ViewportPointToRay(viewPos);

                    if (Physics.Raycast(ray, out RaycastHit hit, MaxDistance, ScanTypes))
                    {
                        Vector3 hitPoint = hit.point;
                        if (NoiseLevel > 0) hitPoint += UnityEngine.Random.insideUnitSphere * NoiseLevel;

                        Vector3 pos = T_ref_world.MultiplyPoint3x4(hitPoint);
                        points.Add(pos);
                        colors.Add(defaultColor); // Default color until analyzed
                    }
                }
            }
        }

        // --- Analysis Workflow ---

        [ContextMenu("Analyze Scene")]
        public void AnalyzeScene()
        {
            if (analysisClient == null)
            {
                Debug.LogError("[PointCloudGenerator] No SceneAnalysisClient found. Cannot analyze.");
                return;
            }

            // 1. Capture current scan first (if needed, or use existing)
            CaptureScan();

            if (scanPoints.Count == 0)
            {
                Debug.LogWarning("[PointCloudGenerator] Scan is empty. Nothing to analyze.");
                return;
            }

            // 2. Convert to ROS Message
            PointCloud2 cloudMsg = ConvertToPointCloud2(scanPoints);

            // 3. Send Request
            analysisClient.SendAnalysisRequest("COMPARE", ErrorThreshold, cloudMsg, OnAnalysisResponse);
        }

        private void OnAnalysisResponse(ProcessSceneResponse response)
        {
            if (response == null) return;

            Debug.Log($"[PointCloudGenerator] Analysis Result: Success={response.success}, Match={response.match_score * 100:F1}%");

            if (response.result_cloud != null && response.result_cloud.data.Length > 0)
            {
                ColorizeFromROS(response.result_cloud);
            }
            else
            {
                Debug.LogWarning("[PointCloudGenerator] Received empty result cloud.");
            }
        }

        private void ColorizeFromROS(PointCloud2 resultCloud)
        {
            // Parse Intensity and update colors
            uint pointCount = resultCloud.width * resultCloud.height;
            int pointStep = (int)resultCloud.point_step;
            byte[] data = resultCloud.data;

            // Find offsets
            int xOff = -1, yOff = -1, zOff = -1, iOff = -1;
            foreach (var field in resultCloud.fields)
            {
                if (field.name == "x") xOff = (int)field.offset;
                if (field.name == "y") yOff = (int)field.offset;
                if (field.name == "z") zOff = (int)field.offset;
                if (field.name == "intensity") iOff = (int)field.offset;
            }

            if (iOff == -1)
            {
                Debug.LogError("[PointCloudGenerator] No 'intensity' field in result cloud.");
                return;
            }

            scanPoints.Clear();
            scanColors.Clear();

            scanPoints.Clear();
            scanColors.Clear();

            int matchCount = 0;
            for (int i = 0; i < pointCount; i++)
            {
                int baseIdx = i * pointStep;

                // Read Position (ROS FLU)
                float x = BitConverter.ToSingle(data, baseIdx + xOff);
                float y = BitConverter.ToSingle(data, baseIdx + yOff);
                float z = BitConverter.ToSingle(data, baseIdx + zOff);

                // Convert ROS -> Unity
                Vector3 pMsg = new Vector3(x, y, z);
                scanPoints.Add(pMsg.Ros2Unity());

                // Read Intensity
                float intensity = BitConverter.ToSingle(data, baseIdx + iOff);

                // Apply Color
                bool isMatch = intensity >= 0.9f;
                if (isMatch) matchCount++;
                Color c = isMatch ? MatchColor : ErrorColor;
                scanColors.Add(c);
                
                // Debug first few points
                if (i < 5) Debug.Log($"[PCG Debug] Point {i}: Int={intensity:F2}, Color={c}");
            }
            
            Debug.Log($"[PointCloudGenerator] Colorized {scanPoints.Count} points. Matches: {matchCount}");

            UpdateMesh(scanFilter.mesh, scanPoints, scanColors);
        }

        // --- Helper: Conversion ---

        private PointCloud2 ConvertToPointCloud2(List<Vector3> points)
        {
            PointCloud2 msg = new PointCloud2();
            msg.header = new RosSharp.RosBridgeClient.MessageTypes.Std.Header { frame_id = "camera_link" };
            msg.height = 1;
            msg.width = (uint)points.Count;
            msg.is_bigendian = false;
            msg.point_step = 16; // x(4) + y(4) + z(4) + i(4)
            msg.row_step = msg.point_step * msg.width;
            msg.is_dense = true;

            msg.fields = new RosettaField[] // using RosettaField = RosSharp.RosBridgeClient.MessageTypes.Sensor.PointField;
            {
                new PointField { name = "x", offset = 0, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "y", offset = 4, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "z", offset = 8, datatype = PointField.FLOAT32, count = 1 },
                new PointField { name = "intensity", offset = 12, datatype = PointField.FLOAT32, count = 1 }
            };

            byte[] data = new byte[msg.width * msg.point_step];

            for (int i = 0; i < points.Count; i++)
            {
                int offset = i * (int)msg.point_step;
                
                // Convert Unity -> ROS (FLU)
                Vector3 pRos = points[i].Unity2Ros();

                BitConverter.GetBytes(pRos.x).CopyTo(data, offset + 0);
                BitConverter.GetBytes(pRos.y).CopyTo(data, offset + 4);
                BitConverter.GetBytes(pRos.z).CopyTo(data, offset + 8);
                BitConverter.GetBytes(0.0f).CopyTo(data, offset + 12); // Initial intensity 0
            }

            msg.data = data;
            return msg;
        }

        private void UpdateMesh(Mesh targetMesh, List<Vector3> points, List<Color> colors)
        {
            if (!ShowPoints || points == null || points.Count == 0)
            {
                targetMesh.Clear();
                return;
            }

            targetMesh.Clear();
            targetMesh.vertices = points.ToArray();
            targetMesh.colors = colors.ToArray();

            int[] indices = new int[points.Count];
            for (int i = 0; i < points.Count; i++) indices[i] = i;

            targetMesh.SetIndices(indices, MeshTopology.Points, 0);
            targetMesh.RecalculateBounds();
        }

        public void ToggleMasterDataRender(bool visible)
        {
            masterRenderer.enabled = visible;
        }

    }
}