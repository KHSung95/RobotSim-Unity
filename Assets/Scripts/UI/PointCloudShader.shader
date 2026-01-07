Shader "Custom/PointCloudShader"
{
    Properties
    {
        _PointSize("Point Size", Float) = 0.01
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2g
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float3 normal : NORMAL;
            };

            float _PointSize;

            v2g vert (appdata v)
            {
                v2g o;
                o.pos = v.vertex;
                o.color = v.color;
                return o;
            }

            void addCubeFace(float4 center, float3 n, float3 u, float3 v, float4 color, inout TriangleStream<g2f> stream)
            {
                float halfSize = _PointSize * 0.5;
                g2f o;
                o.color = color;
                o.normal = n;

                float3 p1 = center.xyz + (u - v) * halfSize;
                float3 p2 = center.xyz + (u + v) * halfSize;
                float3 p3 = center.xyz + (-u - v) * halfSize;
                float3 p4 = center.xyz + (-u + v) * halfSize;

                o.pos = UnityObjectToClipPos(float4(p1, 1)); stream.Append(o);
                o.pos = UnityObjectToClipPos(float4(p2, 1)); stream.Append(o);
                o.pos = UnityObjectToClipPos(float4(p3, 1)); stream.Append(o);
                o.pos = UnityObjectToClipPos(float4(p4, 1)); stream.Append(o);
                stream.RestartStrip();
            }

            [maxvertexcount(24)]
            void geom(point v2g input[1], inout TriangleStream<g2f> outStream)
            {
                float4 p = input[0].pos;
                float4 c = input[0].color;

                // Simple Box: 6 faces
                // +X, -X, +Y, -Y, +Z, -Z
                addCubeFace(p, float3(1,0,0),  float3(0,0,-1), float3(0,1,0), c, outStream);
                addCubeFace(p, float3(-1,0,0), float3(0,0,1),  float3(0,1,0), c, outStream);
                addCubeFace(p, float3(0,1,0),  float3(1,0,0),  float3(0,0,1), c, outStream);
                addCubeFace(p, float3(0,-1,0), float3(1,0,0),  float3(0,0,-1), c, outStream);
                addCubeFace(p, float3(0,0,1),  float3(1,0,0),  float3(0,1,0), c, outStream);
                addCubeFace(p, float3(0,0,-1), float3(-1,0,0), float3(0,1,0), c, outStream);
            }

            fixed4 frag (g2f i) : SV_Target
            {
                // Simple lighting based on normal
                float3 lightDir = normalize(float3(1, 1, -1));
                float diff = max(0.2, dot(i.normal, lightDir));
                return i.color * diff;
            }
            ENDCG
        }
    }
}
