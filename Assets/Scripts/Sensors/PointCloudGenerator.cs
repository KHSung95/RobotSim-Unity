using UnityEngine;
using System.Collections.Generic;

namespace RobotSim.Sensors
{
    [RequireComponent(typeof(Camera))]
    public class PointCloudGenerator : MonoBehaviour
    {
        private Transform RobotBaseReference;

        [Header("Scan Settings")]
        public int Width = 160;
        public int Height = 120;
        public float MaxDistance = 2.0f;
        public LayerMask ScanTypes = -1;
        public float Tolerance = 0.002f;

        [Header("Visualization")]
        public bool ShowPoints = true;
        [Range(0.001f, 10f)] public float PointSize = 0.01f;

        [Header("Sensor Simulation")]
        public float NoiseLevel = 0.0005f;

        // Separate lists using standard Unity types
        private List<Vector3> masterPoints = new List<Vector3>();
        private List<Color> masterColors = new List<Color>();
        
        private List<Vector3> scanPoints = new List<Vector3>();
        private List<Color> scanColors = new List<Color>();

        public List<Vector3> MasterPoints => masterPoints;
        public List<Vector3> ScanPoints => scanPoints;

        private Camera cam;

        // [구조 변경] 루트 컨테이너와 두 개의 자식 컨테이너
        private GameObject rootContainer;
        private MeshRenderer masterRenderer;
        private MeshFilter masterFilter;
        private MeshRenderer scanRenderer;
        private MeshFilter scanFilter;

        private void Start()
        {
            cam = GetComponent<Camera>();

            // 1. 루트 컨테이너 생성 (RobotBase를 따라다닐 녀석)
            rootContainer = new GameObject("PointCloud_Visualizer");

            // 2. Master와 Scan을 별도 오브젝트로 분리 생성
            CreateSubVisualizer("Master_Cloud", out masterFilter, out masterRenderer);
            CreateSubVisualizer("Scan_Cloud", out scanFilter, out scanRenderer);
        }

        // 반복되는 오브젝트 생성 코드를 함수로 분리
        private GameObject CreateSubVisualizer(string name, out MeshFilter filter, out MeshRenderer rend)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(rootContainer.transform, false); // 루트의 자식으로
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;

            filter = obj.AddComponent<MeshFilter>();
            rend = obj.AddComponent<MeshRenderer>();

            Mesh m = new Mesh();
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            filter.mesh = m;

            // 재질이 없으면 쉐이더 찾아서 생성
            var shader = Shader.Find("Custom/PointCloudShader");
            if (shader) rend.material = new Material(shader);

            return obj;
        }

        public void SetRobotBase(Transform robotBase)
        {
            // RobotBaseReference = robotBase; // No longer needed for capture
            if (rootContainer != null)
            {
                // Attach visualizer to the Camera (this.transform) so points move with it
                rootContainer.transform.SetParent(this.transform, false);
                rootContainer.transform.localPosition = Vector3.zero;
                rootContainer.transform.localRotation = Quaternion.identity;
            }
        }

        // [New] Only clear Scan visualization (Transient)
        public void ClearScan()
        {
            scanPoints.Clear();
            scanColors.Clear();
            if (scanFilter != null && scanFilter.mesh != null)
            {
                scanFilter.mesh.Clear();
            }
        }

        private void Update()
        {
            // 점 크기 실시간 반영 (두 재질 모두)
            if (masterRenderer != null && masterRenderer.material != null)
                masterRenderer.material.SetFloat("_PointSize", PointSize);

            if (scanRenderer != null && scanRenderer.material != null)
                scanRenderer.material.SetFloat("_PointSize", PointSize);
        }

        public void CaptureMaster()
        {
            // Master는 비교 대상이 없으므로 Heatmap 없이 흰색(1,1,1,1)으로 저장
            // Capture in Camera Local Space
            CaptureInternal(this.transform.worldToLocalMatrix, Color.white, true, ref masterPoints, ref masterColors);

            // Master 메쉬만 업데이트
            UpdateMesh(masterFilter.mesh, masterPoints, masterColors);
        }

        public void CaptureScan()
        {
            // Scan은 Heatmap 컬러(초록/빨강)가 버텍스에 구워짐
            // Capture in Camera Local Space
            CaptureInternal(this.transform.worldToLocalMatrix, Color.white, false, ref scanPoints, ref scanColors);

            // Scan 메쉬만 업데이트
            UpdateMesh(scanFilter.mesh, scanPoints, scanColors);
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
                        if (NoiseLevel > 0) hitPoint += Random.insideUnitSphere * NoiseLevel;

                        Vector3 pos = T_ref_world.MultiplyPoint3x4(hitPoint);
                        points.Add(pos);

                        if (isMaster)
                        {
                            colors.Add(Color.white);
                        }
                        else
                        {
                            colors.Add(CalculateHeatmapColor(pos));
                        }
                    }
                }
            }
        }

        private Color CalculateHeatmapColor(Vector3 pos)
        {
            if (masterPoints.Count == 0) return Color.green; // 비교 대상 없으면 그냥 초록

            float minSqDist = float.MaxValue;
            // 성능 최적화: 점이 많으면 여기서 병목 발생. 추후 KDTree 도입 필요.
            // 지금은 단순히 건너뛰기(Stride) 등으로 임시 최적화 가능
            int stride = 1; // 1이면 전수조사. 
            for (int i = 0; i < masterPoints.Count; i += stride)
            {
                float d = (masterPoints[i] - pos).sqrMagnitude;
                if (d < minSqDist) minSqDist = d;
            }
            return (minSqDist < Tolerance * Tolerance) ? Color.green : Color.red;
        }

        // [함수 분리] 특정 메쉬에 데이터를 그리는 로직
        private void UpdateMesh(Mesh targetMesh, List<Vector3> points, List<Color> colors)
        {
            if (!ShowPoints || points == null || points.Count == 0)
            {
                targetMesh.Clear();
                return;
            }

            Vector3[] vertices = points.ToArray();
            Color[] meshColors = colors.ToArray();
            int[] indices = new int[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                indices[i] = i;
            }

            targetMesh.Clear();
            targetMesh.vertices = vertices;
            targetMesh.colors = meshColors;
            targetMesh.SetIndices(indices, MeshTopology.Points, 0);
            targetMesh.RecalculateBounds();
        }

        public void ToggleMasterDataRender(bool visible)
        {
           masterRenderer.enabled = visible;
        }
    }
}