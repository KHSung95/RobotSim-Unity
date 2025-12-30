using UnityEngine;

public class Freecam : MonoBehaviour
{
    public float moveSpeed = 20f;         // 기본 이동 속도
    public float shiftMultiplier = 2.5f;  // Shift 누를 때 가속
    public float lookSensitivity = 2f;    // 마우스 민감도

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // 현재 카메라의 회전값으로 초기화
        Vector3 rot = transform.localRotation.eulerAngles;
        rotationX = rot.y;
        rotationY = rot.x;
    }

    void Update()
    {
        // 1. 회전 (마우스 우클릭 시)
        if (Input.GetMouseButton(1))
        {
            rotationX += Input.GetAxis("Mouse X") * lookSensitivity;
            rotationY -= Input.GetAxis("Mouse Y") * lookSensitivity;
            rotationY = Mathf.Clamp(rotationY, -90f, 90f); // 수직 회전 제한

            transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0);
        }

        // 2. 이동 속도 계산
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift)) currentSpeed *= shiftMultiplier;

        // 3. 이동 (WASD + QE)
        float moveH = Input.GetAxis("Horizontal"); // A, D
        float moveV = Input.GetAxis("Vertical");   // W, S

        Vector3 moveDir = (transform.forward * moveV) + (transform.right * moveH);

        // Q, E로 수직 상승/하강
        if (Input.GetKey(KeyCode.E)) moveDir += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) moveDir += Vector3.down;

        transform.position += moveDir * currentSpeed * Time.deltaTime;

        // 4. 마우스 휠로 이동 속도 실시간 조절
        moveSpeed += Input.GetAxis("Mouse ScrollWheel") * 10f;
        moveSpeed = Mathf.Max(moveSpeed, 0.1f);
    }
}