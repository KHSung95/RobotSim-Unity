using UnityEngine;

public class Freecam : MonoBehaviour
{
    public float moveSpeed = 20f;         // Base Move Speed
    public float shiftMultiplier = 2.5f;  // Shift Multiplier
    public float lookSensitivity = 2f;    // Mouse Sensitivity

    private float rotationX = 0f;
    private float rotationY = 0f;

    [Header("FOV Settings")]
    public float zoomSpeed = 10f;
    public float minFOV = 10f;
    public float maxFOV = 90f;

    private Camera _cam;
    private Vector3 _initialPos;
    private Quaternion _initialRot;
    private float _initialFOV;
    private float _initialRotX;
    private float _initialRotY;

    void Start()
    {
        _cam = GetComponent<Camera>();
        
        // Initialize camera rotation
        Vector3 rot = transform.localRotation.eulerAngles;
        rotationX = rot.y;
        rotationY = rot.x;

        // Backup initial state
        _initialPos = transform.position;
        _initialRot = transform.rotation;
        if (_cam != null) _initialFOV = _cam.fieldOfView;
        _initialRotX = rotationX;
        _initialRotY = rotationY;
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

        // 3. Movement (WASD + QE)
        float moveH = Input.GetAxis("Horizontal"); // A, D
        float moveV = Input.GetAxis("Vertical");   // W, S

        Vector3 moveDir = (transform.forward * moveV) + (transform.right * moveH);

        // Q, E for Up/Down
        if (Input.GetKey(KeyCode.E)) moveDir += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) moveDir += Vector3.down;

        transform.position += moveDir * currentSpeed * Time.deltaTime;

        // 4. Adjust FOV with Scroll Wheel
        if (_cam != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _cam.fieldOfView = Mathf.Clamp(_cam.fieldOfView - scroll * zoomSpeed, minFOV, maxFOV);
            }
        }
    }

    public void ResetView()
    {
        transform.position = _initialPos;
        transform.rotation = _initialRot;
        if (_cam != null) _cam.fieldOfView = _initialFOV;
        rotationX = _initialRotX;
        rotationY = _initialRotY;
    }
}