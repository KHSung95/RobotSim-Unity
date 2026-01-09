using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using RobotSim.Utils;
using RobotSim.Robot;

namespace RobotSim.Sensors
{
    [System.Serializable]
    public struct PointData
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Color32 Color;

        public PointData(Vector3 pos, Vector3 norm, Color32 col)
        {
            Position = pos;
            Normal = norm;
            Color = col;
        }
    }

    [RequireComponent(typeof(Camera))]
    public class PointCloudGenerator : MonoBehaviour
    {
        [Header("Scan Settings")]
        public int Width = 160;
        public int Height = 120;
        public float MaxDistance = 2.0f;
        public LayerMask ScanTypes = -1;

        [Header("Sensor Simulation")]
        public float NoiseLevel = 0.0005f;

        [Header("I/O Settings")]
        public string FileName = "MasterScan.ply";
        public bool LoadOnStart = true;
        
        // Reference needed for Base Coordinate
        public RobotStateProvider RobotState; 

        // Internal Data
        private List<PointData> masterPoints = new List<PointData>();
        private List<PointData> scanPoints = new List<PointData>();

        public List<PointData> MasterPoints => masterPoints;
        public List<PointData> ScanPoints => scanPoints;

        private Camera cam;

        // Events for consumers
        public event Action OnMasterCaptured;
        public event Action OnScanCaptured;

        private void Start()
        {
            cam = GetComponent<Camera>();
            
            if (RobotState == null) RobotState = FindFirstObjectByType<RobotStateProvider>();

            if (LoadOnStart && !string.IsNullOrEmpty(FileName))
            {
                LoadFromPly(FileName);
            }
        }

        public void ClearScan()
        {
            scanPoints.Clear();
            OnScanCaptured?.Invoke();
        }

        public void ClearMaster()
        {
            masterPoints.Clear();
            OnMasterCaptured?.Invoke();
        }

        // --- Capture Logic ---

        public void CaptureMaster()
        {
            // Capture in Camera Local Space (Eye-in-Hand)
            Matrix4x4 matrix = this.transform.worldToLocalMatrix;
            
            CaptureInternal(matrix, ref masterPoints);
            if (!string.IsNullOrEmpty(FileName)) SaveToPly(FileName, masterPoints);
            OnMasterCaptured?.Invoke();
            Debug.Log($"[PointCloudGenerator] Captured Master: {masterPoints.Count} points. Frame: CameraLocal");
        }

        public void CaptureScan()
        {
            // Capture in Camera Local Space
            Matrix4x4 matrix = this.transform.worldToLocalMatrix;

            CaptureInternal(matrix, ref scanPoints);
            OnScanCaptured?.Invoke();
            Debug.Log($"[PointCloudGenerator] Captured Scan: {scanPoints.Count} points. Frame: CameraLocal");
        }

        private void CaptureInternal(Matrix4x4 T_ref_world, ref List<PointData> points)
        {
            points.Clear();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector3 viewPos = new Vector3((float)x / Width, (float)y / Height, 0);
                    Ray ray = cam.ViewportPointToRay(viewPos);

                    if (Physics.Raycast(ray, out RaycastHit hit, MaxDistance, ScanTypes))
                    {
                        Vector3 hitPoint = hit.point;
                        Vector3 hitNormal = hit.normal;
                        Color32 hitColor = Color.white;

                        // Add Noise
                        if (NoiseLevel > 0) hitPoint += UnityEngine.Random.insideUnitSphere * NoiseLevel;

                        // Get Color
                        Renderer rend = hit.collider.GetComponent<Renderer>();
                        if (rend != null)
                        {
                            // Try to get Main Texture color at UV?
                            // Or simple material color (requested in prompt as simple option)
                            if (rend.material != null) hitColor = rend.material.color;
                            
                            // Advanced: Texture lookup if mesh collider
                            if (hit.collider is MeshCollider && rend.material.mainTexture is Texture2D tex)
                            {
                                Vector2 uv = hit.textureCoord;
                                hitColor = tex.GetPixelBilinear(uv.x, uv.y);
                            }
                        }

                        // Transform to Reference Frame (Robot Base)
                        Vector3 pos = T_ref_world.MultiplyPoint3x4(hitPoint);
                        Vector3 norm = T_ref_world.MultiplyVector(hitNormal).normalized; // Rotate normal

                        points.Add(new PointData(pos, norm, hitColor));
                    }
                }
            }
        }

        // --- PLY I/O ---

        public void SaveToPly(string relativePath, List<PointData> data)
        {
            string path = GetPath(relativePath);
            try
            {
                using (StreamWriter sw = new StreamWriter(path))
                {
                    // Header
                    sw.WriteLine("ply");
                    sw.WriteLine("format ascii 1.0");
                    sw.WriteLine($"element vertex {data.Count}");
                    sw.WriteLine("property float x");
                    sw.WriteLine("property float y");
                    sw.WriteLine("property float z");
                    sw.WriteLine("property float nx");
                    sw.WriteLine("property float ny");
                    sw.WriteLine("property float nz");
                    sw.WriteLine("property uchar red");
                    sw.WriteLine("property uchar green");
                    sw.WriteLine("property uchar blue");
                    sw.WriteLine("end_header");

                    // Data
                    foreach (var p in data)
                    {
                        sw.WriteLine($"{p.Position.x:F6} {p.Position.y:F6} {p.Position.z:F6} " +
                                     $"{p.Normal.x:F6} {p.Normal.y:F6} {p.Normal.z:F6} " +
                                     $"{p.Color.r} {p.Color.g} {p.Color.b}");
                    }
                }
                Debug.Log($"[PointCloudGenerator] Saved PLY to: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PointCloudGenerator] Failed to save PLY: {e.Message}");
            }
        }

        public void LoadFromPly(string relativePath)
        {
            string path = GetPath(relativePath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[PointCloudGenerator] File not found: {path}");
                return;
            }

            masterPoints.Clear();
            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    string line;
                    bool headerEnded = false;
                    int vertexCount = 0;

                    // Parse Header
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.StartsWith("element vertex"))
                        {
                            string[] parts = line.Split(' ');
                            if (parts.Length > 2) int.TryParse(parts[2], out vertexCount);
                        }
                        if (line.Trim() == "end_header")
                        {
                            headerEnded = true;
                            break;
                        }
                    }

                    if (!headerEnded) return;

                    // Parse Data
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] tokens = line.Split(' ');
                        if (tokens.Length < 9) continue;

                        Vector3 pos = new Vector3(
                            float.Parse(tokens[0]), float.Parse(tokens[1]), float.Parse(tokens[2]));
                        Vector3 norm = new Vector3(
                            float.Parse(tokens[3]), float.Parse(tokens[4]), float.Parse(tokens[5]));
                        Color32 col = new Color32(
                            byte.Parse(tokens[6]), byte.Parse(tokens[7]), byte.Parse(tokens[8]), 255);

                        masterPoints.Add(new PointData(pos, norm, col));
                    }
                }
                Debug.Log($"[PointCloudGenerator] Loaded {masterPoints.Count} points from {path}");
                OnMasterCaptured?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PointCloudGenerator] Failed to load PLY: {e.Message}");
            }
        }

        private string GetPath(string fileName)
        {
            // Use streaming assets for ease of access, or persistence
            string dir = Application.streamingAssetsPath;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, fileName);
        }
    }
}
