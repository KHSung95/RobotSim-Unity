Shader "Custom/DepthGrayscale_Fixed"
{
    Properties
    {
        _DepthRange("Visible Distance (Meters)", Float) = 50.0  // 0~1이 되는 기준 거리 (미터)
        _Power("Contrast Power", Range(0.1, 5)) = 1.0
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
                float depth : TEXCOORD0; // 화면 좌표 대신 순수 깊이값만 전달
            };

            float _DepthRange;
            float _Power;
            float _Invert;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                
                // [핵심 변경] View Space에서의 Z값(카메라 앞쪽 거리)을 직접 계산
                // UnityObjectToViewPos 결과의 z값은 음수일 수 있으므로 -를 붙여 양수로 만듦
                o.depth = -UnityObjectToViewPos(v.vertex).z;

                // 혹은 유니티 내장 매크로 사용 (결과는 위와 같음)
                // COMPUTE_EYEDEPTH(o.depth); 
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 선형 깊이 값 가져오기 (단위: 미터)
                float rawDepth = i.depth;
                
                // 2. 사용자가 설정한 거리(_DepthRange)로 정규화 (0~1)
                // 예: 물체가 10m에 있고 Range가 50m면 -> 0.2
                float linear01 = rawDepth / _DepthRange;

                // 3. 0~1 사이로 자르기
                linear01 = saturate(linear01);

                // 4. 반전 처리
                if (_Invert > 0.5)
                {
                    linear01 = 1.0 - linear01;
                }

                // 5. 명암비 조절
                float result = pow(linear01, _Power);

                return fixed4(result, result, result, 1);
            }
            ENDCG
        }
    }
}