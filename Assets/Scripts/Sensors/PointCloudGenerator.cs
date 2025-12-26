using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RobotSim.Sensors
{
    [RequireComponent(typeof(Camera))]
    public class PointCloudGenerator : MonoBehaviour
    {
        [Header("References")]
        public VirtualCameraMount Mount;
        
        [Header("Scan Settings")]
        public int Width = 160; 
        public int Height = 120;
        public float MaxDistance = 2.0f;
        public LayerMask ScanTypes = -1;
        public float Tolerance = 0.002f; // 2mm

        [Header("Visualization")]
        public bool ShowDebugPoints = true;
        public float PointSize = 0.003f;

        [Header("Export")]
        public string ExportPath = "Assets/PointClouds/";
        
        [Header("Sensor Simulation")]
        [Tooltip("Standard deviation of random noise in meters (e.g. 0.001 = 1mm).")]
        public float NoiseLevel = 0.0005f; 
        
        [Header("Frame References")]
        public Transform TargetFramePivot; // Visualization reference (Install Pose)

        public struct Point3D
        {
            public Vector3 Position; // Relative to Target Frame (e.g. Install Pose)
            public Vector3 Normal;   // Relative to Target Frame
            public Color Color;      
            public Color OriginalColor; 
        }

        private List<Point3D> masterCloud = new List<Point3D>();
        private List<Point3D> scanCloud = new List<Point3D>();
        private Camera cam;

        private void Start()
        {
            cam = GetComponent<Camera>();
            if (Mount == null) Mount = GetComponent<VirtualCameraMount>();
        }

        public void CaptureMaster(Matrix4x4 targetFrameMatrix)
        {
            masterCloud = CaptureInternal(targetFrameMatrix, Color.white, true);
            Debug.Log($"[PointCloudGenerator] Master Captured: {masterCloud.Count} points in Target Frame.");
        }

        public void CaptureScan(Matrix4x4 targetFrameMatrix)
        {
            scanCloud = CaptureInternal(targetFrameMatrix, Color.green, false);
            Debug.Log($"[PointCloudGenerator] Scan Captured: {scanCloud.Count} points in Target Frame.");
        }

        private List<Point3D> CaptureInternal(Matrix4x4 T_target_camera, Color defaultColor, bool isMaster)
        {
            List<Point3D> cloud = new List<Point3D>();
            
            // T_target_world = T_target_camera * T_camera_world
            Matrix4x4 T_target_world = T_target_camera * cam.worldToCameraMatrix;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector3 viewPos = new Vector3((float)x / Width, (float)y / Height, 0);
                    Ray ray = cam.ViewportPointToRay(viewPos);
                    
                    if (Physics.Raycast(ray, out RaycastHit hit, MaxDistance, ScanTypes))
                    {
                        Point3D p = new Point3D();
                        
                        // Convert hit.point (World) to Target Frame using T_ic logic
                        Vector3 hitPoint = hit.point;
                        
                        // Apply Industrial Noise if scale > 0
                        if (NoiseLevel > 0)
                        {
                            hitPoint += Random.insideUnitSphere * NoiseLevel;
                        }

                        p.Position = T_target_world.MultiplyPoint3x4(hitPoint);
                        
                        // Convert normal
                        p.Normal = T_target_world.MultiplyVector(hit.normal);
                        
                        Renderer rend = hit.collider.GetComponent<Renderer>();
                        p.OriginalColor = (rend != null && rend.material.HasProperty("_Color")) 
                            ? rend.material.color : Color.white;

                        if (isMaster)
                        {
                            p.Color = Color.white;
                        }
                        else
                        {
                            p.Color = CalculateHeatmapColor(p.Position);
                        }

                        cloud.Add(p);
                    }
                }
            }
            return cloud;
        }

        private Color CalculateHeatmapColor(Vector3 pos)
        {
            if (masterCloud.Count == 0) return Color.green;

            // Simple nearest neighbor (Brute force for now, optimized later if needed)
            float minSqDist = float.MaxValue;
            foreach (var mp in masterCloud)
            {
                float d = (mp.Position - pos).sqrMagnitude;
                if (d < minSqDist) minSqDist = d;
            }

            return (minSqDist < Tolerance * Tolerance) ? Color.green : Color.red;
        }

        private void OnDrawGizmos()
        {
            if (!ShowDebugPoints) return;

            // Draw Master
            Gizmos.color = Color.white;
            DrawCloud(masterCloud, false);

            // Draw Scan
            DrawCloud(scanCloud, true);
        }

        private void DrawCloud(List<Point3D> cloud, bool useHeatmapColor)
        {
            if (cloud == null) return;

            // Use TargetFramePivot if assigned, otherwise fallback to RobotBase 
            Matrix4x4 frameToWorld = Matrix4x4.identity;
            if (TargetFramePivot != null) frameToWorld = TargetFramePivot.localToWorldMatrix;
            else if (Mount != null && Mount.RobotBase != null) frameToWorld = Mount.RobotBase.localToWorldMatrix;

            foreach (var p in cloud)
            {
                Vector3 worldPos = frameToWorld.MultiplyPoint3x4(p.Position);
                Gizmos.color = useHeatmapColor ? p.Color : Color.white;
                Gizmos.DrawSphere(worldPos, PointSize);
            }
        }
    }
}
