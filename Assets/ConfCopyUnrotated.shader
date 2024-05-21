Shader "Unlit/CopyConfUnrotatedShader"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        ZTest Always
        Cull Off
        ZWrite Off
   
        Pass
        {
            Name "Unlit"
           
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
       
            #include "UnityCG.cginc"

            //#define DECLARE_TEXTURE2D_FLOAT(texture) UNITY_DECLARE_TEX2D_FLOAT(texture)
            //#define DECLARE_SAMPLER_FLOAT(sampler)
            //#define SAMPLE_TEXTURE2D(texture,sampler,texcoord) UNITY_SAMPLE_TEX2D(texture,texcoord)

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
       
            //DECLARE_TEXTURE2D_FLOAT(_MainTex);
            //DECLARE_SAMPLER_FLOAT(sampler_MainTex);
            sampler2D _MainTex;
            float4 _MainTex_ST;
            int _Orientation;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
           
                // Flip X
                o.uv = v.uv.xy;//float2(1.0 - v.uv.x, v.uv.y);
           
                /*if (_Orientation == 1) {
                    // Portrait
                    o.uv = float2(1.0 - o.uv.y, o.uv.x);
                }
                else if (_Orientation == 3) {
                    // Landscape left
                    o.uv = float2(1.0 - o.uv.x, 1.0 - o.uv.y);
                }*/
           
                o.uv = TRANSFORM_TEX(o.uv, _MainTex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDHLSL
        }
    }
}