Shader "Custom/DepthGrayscale"
{
    Properties
    {
        _Power("Depth Power (Contrast)", Range(0.1, 5)) = 1.0
        [Toggle] _Invert("Invert Colors (White is Near)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0; // 화면 좌표
            };

            float _Power;
            float _Invert;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos); // 화면상의 위치 계산
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 화면 좌표계에서의 깊이 값 추출 (Non-Linear)
                float2 uv = i.screenPos.xy / i.screenPos.w;
                
                // 2. View Space에서의 실제 깊이(거리) 계산
                // Unity의 내장 매크로를 사용하여 안전하게 Linear Depth(0~1)를 구합니다.
                // COMPUTE_DEPTH_01 등은 텍스처를 읽는 방식이라 Replacement Shader에서는
                // 직접 w값을 사용하는 것이 더 안전할 때가 많습니다.
                
                // i.pos.w는 View Space에서의 깊이(카메라로부터의 거리)와 비례합니다.
                // _ProjectionParams.w = 1.0 / FarPlane
                // _ProjectionParams.y = NearPlane
                // _ProjectionParams.z = FarPlane
                
                // 현재 픽셀의 카메라로부터의 실제 거리 (미터 단위)
                // LinearEyeDepth 방식과 유사하게 w 성분을 활용
                float depthValue = i.screenPos.w; 
                
                // 3. 0~1 정규화 (거리 / FarPlane)
                // 예: 거리가 10m이고 Far가 100m면 0.1
                float linearDepth = depthValue * _ProjectionParams.w;

                // 4. 값 클램핑 (0~1 사이로 강제)
                linearDepth = saturate(linearDepth);

                // 5. 반전 처리 (옵션)
                // 기본: 가까우면 검정(0), 멀면 흰색(1)
                // 반전: 가까우면 흰색(1), 멀면 검정(0) -> 시각적으로 더 잘 보임
                if (_Invert > 0.5)
                {
                    linearDepth = 1.0 - linearDepth;
                }

                // 6. 명암비 조절 (Power)
                // Far Plane이 너무 멀면 모든게 검게 보일 수 있으므로 곡선을 휨
                linearDepth = pow(linearDepth, _Power);

                return fixed4(linearDepth, linearDepth, linearDepth, 1);
            }
            ENDCG
        }
    }
}