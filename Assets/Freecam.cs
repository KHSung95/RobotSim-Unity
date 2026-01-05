using UnityEngine;

public class Freecam : MonoBehaviour
{
    public float moveSpeed = 20f;         // Base Move Speed
    public float shiftMultiplier = 2.5f;  // Shift Multiplier
    public float lookSensitivity = 2f;    // Mouse Sensitivity

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // Initialize camera rotation
        Vector3 rot = transform.localRotation.eulerAngles;
        rotationX = rot.y;
        rotationY = rot.x;
    }

    void Update()
    {
        // 1. Rotation (Right Click)
        if (Input.GetMouseButton(1))
        {
            rotationX += Input.GetAxis("Mouse X") * lookSensitivity;
            rotationY -= Input.GetAxis("Mouse Y") * lookSensitivity;
            rotationY = Mathf.Clamp(rotationY, -90f, 90f);

            transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0);
        }

        // 2. Move Speed
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) currentSpeed *= shiftMultiplier;

        // [MODIFIED] Block Camera Movement if Robot is Selected
        if (SelectionManager.Instance != null && SelectionManager.Instance.IsSelected)
        {
            // Do nothing, let SelectionManager handle Gizmo movement
        }
        else
        {
            // 3. Movement (WASD + QE)
            float moveH = Input.GetAxis("Horizontal"); // A, D
            float moveV = Input.GetAxis("Vertical");   // W, S

            Vector3 moveDir = (transform.forward * moveV) + (transform.right * moveH);

            // Q, E for Up/Down
            if (Input.GetKey(KeyCode.E)) moveDir += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) moveDir += Vector3.down;

            transform.position += moveDir * currentSpeed * Time.deltaTime;
        }

        // 4. Adjust Speed with Scroll Wheel
        moveSpeed += Input.GetAxis("Mouse ScrollWheel") * 10f;
        moveSpeed = Mathf.Max(moveSpeed, 0.1f);
    }
}