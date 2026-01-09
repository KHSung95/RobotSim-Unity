using UnityEngine;
using System.Collections.Generic;
using System;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp;

namespace RobotSim.Sensors
{
    public class PointCloudVisualizer : MonoBehaviour
    {
        [Header("Appearance Settings")]
        public bool ShowMaster = true;
        public bool ShowScan = true;
        
        [Range(0.001f, 1.0f)] public float MasterPointSize = 0.01f;
        [Range(0.001f, 1.0f)] public float ScanPointSize = 0.05f; // Increased for better visibility
        public float SurfaceOffset = 0.001f; // Slight push towards camera to avoid Z-fighting

        [Header("Heatmap Colors")]
        public Color MatchColor = Color.green;
        public Color ErrorColor = new Color(1f, 0.2f, 0.2f); // Brighter red


        private GameObject rootContainer;

        private MeshFilter masterFilter;
        private MeshRenderer masterRenderer;

        private MeshFilter scanFilter;
        private MeshRenderer scanRenderer;

        private MaterialPropertyBlock _propBlock;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            UpdateMaterialProperties();
        }

        private void EnsureInitialized()
        {
            if (rootContainer != null) return;

            // Check if it already exists (e.g. from previous run in Editor)
            Transform existing = transform.Find("PointCloud_Rendering");
            if (existing != null)
            {
                rootContainer = existing.gameObject;
                masterFilter = rootContainer.transform.Find("Master_Cloud")?.GetComponent<MeshFilter>();
                masterRenderer = rootContainer.transform.Find("Master_Cloud")?.GetComponent<MeshRenderer>();
                scanFilter = rootContainer.transform.Find("Scan_Cloud")?.GetComponent<MeshFilter>();
                scanRenderer = rootContainer.transform.Find("Scan_Cloud")?.GetComponent<MeshRenderer>();
                
                if (masterFilter != null && scanFilter != null) return;
            }

            CreateVisualizers();
        }

        private void CreateVisualizers()
        {
            if (rootContainer != null) return;

            rootContainer = new GameObject("PointCloud_Rendering");
            rootContainer.transform.SetParent(this.transform, false);

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
            m.name = name + "_Mesh";
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            filter.mesh = m;

            var shader = Shader.Find("Custom/PointCloudShader");
            if (shader)
            {
                // Use .material to create a unique instance
                rend.material = new Material(shader);
            }
        }

        private void UpdateMaterialProperties()
        {
            if (masterRenderer != null && masterRenderer.material != null)
            {
                masterRenderer.enabled = ShowMaster;
                masterRenderer.material.SetFloat("_PointSize", MasterPointSize);
            }

            if (scanRenderer != null && scanRenderer.material != null)
            {
                scanRenderer.enabled = ShowScan;
                scanRenderer.material.SetFloat("_PointSize", ScanPointSize);
            }
        }

        public void SetPose(Vector3 position, Quaternion rotation)
        {
            if (rootContainer != null)
            {
                rootContainer.transform.position = position;
                rootContainer.transform.rotation = rotation;
            }
        }

        public void UpdateMasterMesh(List<Vector3> points)
        {
            if (masterFilter == null) return;
            
            List<Color> colors = new List<Color>();
            for (int i = 0; i < points.Count; i++) colors.Add(Color.white);
            
            UpdateMeshInternal(masterFilter.mesh, points, colors);
        }

        public void UpdateScanMesh(List<Vector3> points, List<Color> colors = null)
        {
            if (scanFilter == null) return;

            if (colors == null)
            {
                colors = new List<Color>();
                for (int i = 0; i < points.Count; i++) colors.Add(Color.white);
            }

            UpdateMeshInternal(scanFilter.mesh, points, colors);
        }

        private void UpdateMeshInternal(Mesh targetMesh, List<Vector3> points, List<Color> colors)
        {
            if (targetMesh == null) return;
            if (points == null || points.Count == 0)
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

        public void ColorizeFromAnalysis(PointCloud2 resultCloud, out List<Vector3> points, out List<Color> colors)
        {
            points = new List<Vector3>();
            colors = new List<Color>();

            uint pointCount = resultCloud.width * resultCloud.height;
            int pointStep = (int)resultCloud.point_step;
            byte[] data = resultCloud.data;

            int xOff = -1, yOff = -1, zOff = -1, iOff = -1;
            string fieldsFound = "";
            foreach (var field in resultCloud.fields)
            {
                fieldsFound += field.name + " ";
                if (field.name == "x") xOff = (int)field.offset;
                if (field.name == "y") yOff = (int)field.offset;
                if (field.name == "z") zOff = (int)field.offset;
                if (field.name == "intensity" || field.name == "i") iOff = (int)field.offset;
            }

            if (iOff == -1)
            {
                Debug.LogError($"[PointCloudVisualizer] No 'intensity' field in result cloud. Fields: {fieldsFound}");
                return;
            }

            for (int i = 0; i < pointCount; i++)
            {
                int baseIdx = i * pointStep;

                float x = BitConverter.ToSingle(data, baseIdx + xOff);
                float y = BitConverter.ToSingle(data, baseIdx + yOff);
                float z = BitConverter.ToSingle(data, baseIdx + zOff);

                // Use Raw Coordinates (since they were sent from Unity in local camera frame)
                points.Add(new Vector3(x, y, z));

                float intensity = BitConverter.ToSingle(data, baseIdx + iOff);

                bool isMatch = intensity >= 0.9f;
                Color c = isMatch ? MatchColor : ErrorColor;
                colors.Add(c);
            }

            if (points.Count > 0)
            {
                // Apply Offset to avoid Z-fighting with mesh surfaces
                if (SurfaceOffset != 0)
                {
                    Vector3 camPos = Camera.main != null ? Camera.main.transform.position : transform.position;
                    for (int i = 0; i < points.Count; i++)
                    {
                        Vector3 worldPos = transform.TransformPoint(points[i]);
                        Vector3 dirToCam = (camPos - worldPos).normalized;
                        points[i] = transform.InverseTransformPoint(worldPos + dirToCam * SurfaceOffset);
                    }
                }

            }
        }

        public void Clear()
        {
            if (masterFilter != null && masterFilter.mesh != null) masterFilter.mesh.Clear();
            if (scanFilter != null && scanFilter.mesh != null) scanFilter.mesh.Clear();
        }
    }
}
