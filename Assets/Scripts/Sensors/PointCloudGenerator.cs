using UnityEngine;
using System.Collections.Generic;

namespace RobotSim.Sensors
{
    [RequireComponent(typeof(Camera))]
    // [수정] Mesh 컴포넌트는 이제 이 스크립트가 있는 카메라에 붙이지 않습니다.
    // 대신 별도의 오브젝트를 생성해서 거기에 붙일 것입니다.
    public class PointCloudGenerator : MonoBehaviour
    {
        [Header("Frame References")]
        public Transform RobotBaseReference; // 로봇 베이스 (필수)

        [Header("Scan Settings")]
        public int Width = 160;
        public int Height = 120;
        public float MaxDistance = 2.0f;
        public LayerMask ScanTypes = -1;
        public float Tolerance = 0.002f;

        [Header("Visualization")]
        public bool ShowPoints = true;
        [Range(0.001f, 0.1f)] public float PointSize = 0.01f;
        public Material PointCloudMat;

        [Header("Sensor Simulation")]
        public float NoiseLevel = 0.0005f;

        public struct Point3D
        {
            public Vector3 Position; // Robot Base Frame Local Coordinates
            public Color Color;
        }

        private List<Point3D> masterCloud = new List<Point3D>();
        private List<Point3D> scanCloud = new List<Point3D>();

        // References
        private Camera cam;

        // [핵심] 시각화 전용 오브젝트 (카메라와 분리됨)
        private GameObject visualContainer;
        private Mesh mesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private void Start()
        {
            cam = GetComponent<Camera>();

            // [초기화 로직 변경]
            // 1. 시각화용 전용 GameObject 생성
            visualContainer = new GameObject("PointCloud_Visualizer");

            // 2. 만약 RobotBase가 할당되어 있다면, 그 자식으로 설정 (Base 기준 좌표계 동기화)
            if (RobotBaseReference != null)
            {
                visualContainer.transform.SetParent(RobotBaseReference, false);
                visualContainer.transform.localPosition = Vector3.zero;
                visualContainer.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogWarning("RobotBaseReference가 없습니다! 시각화가 월드 기준으로 생성됩니다.");
            }

            // 3. 렌더링 컴포넌트를 'visualContainer'에 추가
            meshFilter = visualContainer.AddComponent<MeshFilter>();
            meshRenderer = visualContainer.AddComponent<MeshRenderer>();

            // 4. 메쉬 초기화
            mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.mesh = mesh;

            if (PointCloudMat != null) meshRenderer.material = PointCloudMat;
            else
            {
                // 재질이 없으면 기본 생성 (디버그용)
                var shader = Shader.Find("Particles/Standard Unlit");
                if (shader) meshRenderer.material = new Material(shader);
            }
        }

        public void SetRobotBase(Transform robotBase)
        {
            RobotBaseReference = robotBase;
            // 런타임에 베이스가 할당되면 컨테이너를 그쪽으로 이동
            if (visualContainer != null)
            {
                visualContainer.transform.SetParent(RobotBaseReference, false);
                visualContainer.transform.localPosition = Vector3.zero;
                visualContainer.transform.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            // 점 크기 업데이트
            if (meshRenderer != null && meshRenderer.material != null)
            {
                meshRenderer.material.SetFloat("_PointSize", PointSize);
            }

            // [중요] 더 이상 transform.position을 강제로 옮기지 않습니다.
            // visualContainer가 이미 RobotBase의 자식으로 들어가 있기 때문입니다.
        }

        public void CaptureMaster()
        {
            if (RobotBaseReference == null) return;
            masterCloud = CaptureInternal(RobotBaseReference.worldToLocalMatrix, Color.white, true);
            UpdateVisualization();
        }

        public void CaptureScan()
        {
            if (RobotBaseReference == null) return;
            scanCloud = CaptureInternal(RobotBaseReference.worldToLocalMatrix, Color.green, false);
            UpdateVisualization();
        }

        private List<Point3D> CaptureInternal(Matrix4x4 T_ref_world, Color defaultColor, bool isMaster)
        {
            List<Point3D> cloud = new List<Point3D>();

            // 카메라 뷰포트 스캔 루프
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

                        // [좌표 변환] World -> RobotBase Local
                        p.Position = T_ref_world.MultiplyPoint3x4(hitPoint);

                        if (isMaster) p.Color = Color.white;
                        else p.Color = CalculateHeatmapColor(p.Position);

                        cloud.Add(p);
                    }
                }
            }
            return cloud;
        }

        private Color CalculateHeatmapColor(Vector3 pos)
        {
            if (masterCloud.Count == 0) return Color.green;
            float minSqDist = float.MaxValue;
            // 성능 최적화가 필요한 부분 (현재 O(N^2))
            foreach (var mp in masterCloud)
            {
                float d = (mp.Position - pos).sqrMagnitude;
                if (d < minSqDist) minSqDist = d;
            }
            return (minSqDist < Tolerance * Tolerance) ? Color.green : Color.red;
        }

        private void UpdateVisualization()
        {
            if (!ShowPoints) { mesh.Clear(); return; }

            int totalPoints = masterCloud.Count + scanCloud.Count;
            if (totalPoints == 0) return;

            Vector3[] vertices = new Vector3[totalPoints];
            Color[] colors = new Color[totalPoints];
            int[] indices = new int[totalPoints];

            int idx = 0;
            // Master Points
            foreach (var p in masterCloud)
            {
                vertices[idx] = p.Position;
                colors[idx] = p.Color;
                indices[idx] = idx;
                idx++;
            }
            // Scan Points
            foreach (var p in scanCloud)
            {
                vertices[idx] = p.Position;
                colors[idx] = p.Color;
                indices[idx] = idx;
                idx++;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.SetIndices(indices, MeshTopology.Points, 0);
            mesh.RecalculateBounds();
        }
    }
}