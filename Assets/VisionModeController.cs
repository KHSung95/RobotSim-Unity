using UnityEngine;

[RequireComponent(typeof(Camera))]
public class VisionModeController : MonoBehaviour
{
    [Header("Depth Settings")]
    public Shader DepthShader;

    // 원래 카메라 설정을 저장했다가 복구하기 위한 변수
    private CameraClearFlags _originalClearFlags;
    private Color _originalBackgroundColor;

    private Camera _cam;
    private bool _isRGB = true;

    void Start()
    {
        _cam = GetComponent<Camera>();

        // 초기 설정 저장
        _originalClearFlags = _cam.clearFlags;
        _originalBackgroundColor = _cam.backgroundColor;

        // Depth 모드를 위해 필요할 수 있음 (플랫폼 호환성)
        _cam.depthTextureMode |= DepthTextureMode.Depth;

        // 시작은 RGB 모드로
        SetVisionMode(true);
    }

    public void SetVisionMode(bool isRGB)
    {
        _isRGB = isRGB;

        if (_isRGB)
        {
            // [RGB 모드 복구]
            _cam.ResetReplacementShader();
            _cam.clearFlags = _originalClearFlags; // Skybox 복구
            _cam.backgroundColor = _originalBackgroundColor;
        }
        else
        {
            // [Depth 모드 진입]
            if (DepthShader != null)
            {
                // 1. 배경을 단색(검정)으로 변경 -> Skybox 제거 효과
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = Color.black;

                // 2. 쉐이더 교체 (모든 Opaque 물체)
                // 두 번째 인자가 ""이면 RenderType 태그가 있는 모든 쉐이더를 교체
                // 만약 특정 물체만 바꾸고 싶다면 "RenderType" 이라고 적고 쉐이더 태그를 맞춰야 함
                _cam.SetReplacementShader(DepthShader, "");
            }
            else
            {
                Debug.LogWarning("Depth Shader is missing!");
            }
        }
    }
}