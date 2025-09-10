Shader "Unlit/RainbowBackground"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MoveSpeed ("Move speed", float) = 1.0
        _Amplitude ("Amplitude", float) = 1.0
        _CycleSpeed ("Cycle speed", Vector) = (1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _MoveSpeed;
            float _Amplitude;
            float3 _CycleSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float sinVal(v2f i, float speedOffset){
                float dist = distance(i.uv, float2(0.5, 0.5));
                return 0.5 + sin(dist * _Amplitude + _Time.y * _MoveSpeed * speedOffset) * 0.5;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 color = float3(sinVal(i, _CycleSpeed.x), sinVal(i, _CycleSpeed.y), sinVal(i, _CycleSpeed.z));
                return float4(color, 1.0);
            }
            ENDCG
        }
    }
}
