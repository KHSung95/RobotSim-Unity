using UnityEngine;

namespace RobotSim.UI
{
    public class ObjectManipulator : MonoBehaviour
    {
        public LayerMask InteractiveLayer;
        public GameObject SelectedObject;
        public float MoveSpeed = 1.0f;
        public float RotateSpeed = 50.0f;

        [Header("Status")]
        public bool IsDragging = false;
        
        private Camera cam;

        private void Start()
        {
            cam = Camera.main;
        }

        private void Update()
        {
            // Click to Select
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, InteractiveLayer))
                {
                    SelectedObject = hit.collider.gameObject;
                    Debug.Log($"[ObjectManipulator] Selected: {SelectedObject.name}");
                }
                else
                {
                    SelectedObject = null;
                }
            }

            // Move Selected with I/K, J/L, U/O (Simulating Gizmo)
            if (SelectedObject != null)
            {
                float moveX = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
                float moveZ = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
                
                if (moveX != 0 || moveZ != 0)
                {
                    SelectedObject.transform.Translate(new Vector3(moveX, 0, moveZ) * MoveSpeed * Time.deltaTime, Space.World);
                }
            }
        }
        
        private void OnDrawGizmos()
        {
            if (SelectedObject != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(SelectedObject.transform.position, SelectedObject.GetComponent<Renderer>().bounds.size * 1.1f);
            }
        }
    }
}
