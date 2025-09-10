Shader "Unlit/ShaderTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FlickerSpeed ("Flicker speed", float) = 1
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
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata 
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _FlickerSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float movePos = 0.5 * sin(_Time.y) + 0.5;
                float movePosSlow = 0.5 * sin(_Time.y / 2) + 0.5;

                if (distance(i.uv, float2(movePos,movePosSlow)) < 0.2){
                    return float4(0.0, 0.0, 1.0, 1.0);
                }
                return float4(1.0, 0.0, 0.0, 1.0);
                
            }
            ENDCG
        }
    }
}
