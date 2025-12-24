using UnityEngine;

namespace RobotSim.UI
{
    public class OrbitCameraController : MonoBehaviour
    {
        [Header("Orbit Settings")]
        public Transform Target; // Center of rotation
        public float OrbitSpeed = 5.0f;
        public float ZoomSpeed = 10.0f;
        public float MoveSpeed = 2.0f;

        [Header("Constraints")]
        public float MinDist = 0.5f;
        public float MaxDist = 5.0f;
        public float MinPitch = -20f;
        public float MaxPitch = 80f;

        private float yaw = 0f;
        private float pitch = 20f;
        private float distance = 2.0f;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
            distance = Vector3.Distance(transform.position, Target != null ? Target.position : Vector3.zero);
        }

        private void LateUpdate()
        {
            if (Target == null) return;

            // Rotate with Right Mouse
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * OrbitSpeed;
                pitch -= Input.GetAxis("Mouse Y") * OrbitSpeed;
                pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
            }

            // Zoom with Scroll
            distance -= Input.GetAxis("Mouse ScrollWheel") * ZoomSpeed;
            distance = Mathf.Clamp(distance, MinDist, MaxDist);

            // Move Target with WASD/Direction Keys
            float h = Input.GetAxis("Horizontal"); // A/D or Left/Right
            float v = Input.GetAxis("Vertical");   // W/S or Up/Down
            
            Vector3 camRight = transform.right;
            camRight.y = 0;
            Vector3 camForward = transform.forward;
            camForward.y = 0;

            Target.position += (camRight.normalized * h + camForward.normalized * v) * MoveSpeed * Time.deltaTime;

            // Final Position
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            transform.position = Target.position - (rotation * Vector3.forward * distance);
            transform.LookAt(Target.position);
        }
    }
}
