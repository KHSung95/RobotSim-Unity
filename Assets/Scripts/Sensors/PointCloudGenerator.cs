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

        public struct Point3D
        {
            public Vector3 Position;
            public Color Color;
        }

        private List<Point3D> masterCloud = new List<Point3D>();
        private List<Point3D> scanCloud = new List<Point3D>();
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
        private void CreateSubVisualizer(string name, out MeshFilter filter, out MeshRenderer rend)
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
        }

        public void SetRobotBase(Transform robotBase)
        {
            RobotBaseReference = robotBase;
            if (rootContainer != null)
            {
                rootContainer.transform.SetParent(RobotBaseReference, false);
                rootContainer.transform.localPosition = Vector3.zero;
                rootContainer.transform.localRotation = Quaternion.identity;
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
            if (RobotBaseReference == null) return;
            // Master는 비교 대상이 없으므로 Heatmap 없이 흰색(1,1,1,1)으로 저장
            // -> 이렇게 하면 Material의 색상(_Tint)이 그대로 적용됨
            masterCloud = CaptureInternal(RobotBaseReference.worldToLocalMatrix, Color.white, true);

            // Master 메쉬만 업데이트
            UpdateMesh(masterFilter.mesh, masterCloud);
        }

        public void CaptureScan()
        {
            if (RobotBaseReference == null) return;
            // Scan은 Heatmap 컬러(초록/빨강)가 버텍스에 구워짐
            // -> ScanMat의 색상을 흰색으로 두면 Heatmap이 보이고, 색을 섞으면 틴트가 됨
            scanCloud = CaptureInternal(RobotBaseReference.worldToLocalMatrix, Color.white, false);

            // Scan 메쉬만 업데이트
            UpdateMesh(scanFilter.mesh, scanCloud);
        }

        private List<Point3D> CaptureInternal(Matrix4x4 T_ref_world, Color defaultColor, bool isMaster)
        {
            List<Point3D> cloud = new List<Point3D>();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector3 viewPos = new Vector3((float)x / Width, (float)y / Height, 0);
                    Ray ray = cam.ViewportPointToRay(viewPos);

                    if (Physics.Raycast(ray, out RaycastHit hit, MaxDistance, ScanTypes))
                    {
                        Point3D p = new Point3D();
                        Vector3 hitPoint = hit.point;
                        if (NoiseLevel > 0) hitPoint += Random.insideUnitSphere * NoiseLevel;

                        p.Position = T_ref_world.MultiplyPoint3x4(hitPoint);

                        if (isMaster)
                        {
                            // Master는 버텍스 컬러를 흰색으로 통일 -> 그래야 Material 색상이 100% 적용됨
                            p.Color = Color.white;
                        }
                        else
                        {
                            // Scan은 히트맵 컬러 사용
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
            if (masterCloud.Count == 0) return Color.green; // 비교 대상 없으면 그냥 초록

            float minSqDist = float.MaxValue;
            // 성능 최적화: 점이 많으면 여기서 병목 발생. 추후 KDTree 도입 필요.
            // 지금은 단순히 건너뛰기(Stride) 등으로 임시 최적화 가능
            int stride = 1; // 1이면 전수조사. 
            for (int i = 0; i < masterCloud.Count; i += stride)
            {
                float d = (masterCloud[i].Position - pos).sqrMagnitude;
                if (d < minSqDist) minSqDist = d;
            }
            return (minSqDist < Tolerance * Tolerance) ? Color.green : Color.red;
        }

        // [함수 분리] 특정 메쉬에 데이터를 그리는 로직
        private void UpdateMesh(Mesh targetMesh, List<Point3D> points)
        {
            if (!ShowPoints || points == null || points.Count == 0)
            {
                targetMesh.Clear();
                return;
            }

            Vector3[] vertices = new Vector3[points.Count];
            Color[] colors = new Color[points.Count];
            int[] indices = new int[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                vertices[i] = points[i].Position;
                colors[i] = points[i].Color;
                indices[i] = i;
            }

            targetMesh.Clear();
            targetMesh.vertices = vertices;
            targetMesh.colors = colors;
            targetMesh.SetIndices(indices, MeshTopology.Points, 0);
            targetMesh.RecalculateBounds();
        }
    }
}