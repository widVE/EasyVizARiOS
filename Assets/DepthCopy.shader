Shader "Unlit/CopyDepthShader"
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

            //with this off, the environment depth GPU texture comes into here as MainTex (it's 8 times as big as the CPU depth)
            //#define USE_CPU_DEPTH 1
            
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
            sampler2D_float _MainTex;
            //float _depthWidth = 192.0;
            //float _depthHeight = 256.0;
/*#if USE_CPU_DEPTH
            StructuredBuffer<float> _depthBuffer;

#endif*/
            float4 _MainTex_ST;
            int _Orientation;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
/*#if USE_CPU_DEPTH
                o.uv = v.uv;
                //o.uv = TRANSFORM_TEX(o.uv, _MainTex);
#else*/
                // Flip X
                o.uv = float2(1.0 - v.uv.x, v.uv.y);
           
                if (_Orientation == 1) {
                    // Portrait
                    o.uv = float2(1.0 - o.uv.y, o.uv.x);
                }
                else if (_Orientation == 3) {
                    // Landscape left
                    o.uv = float2(1.0 - o.uv.x, 1.0 - o.uv.y);
                }
           
                o.uv = TRANSFORM_TEX(o.uv, _MainTex);
//#endif
                return o;
            }
            
            //this writes to _renderTargetDepthV
            float4 frag (v2f i) : SV_Target
            {
/*#if USE_CPU_DEPTH
                //_depthWidth = 192, _depthHeight = 256
                int idx = (int)((1.0-i.uv.x) * (_depthWidth) * (_depthHeight) + (1.0-i.uv.y) * (_depthHeight));
                //int wIndex = (int)((1.0-i.uv.x) * _depthWidth);
                //int hIndex = (int)((1.0-i.uv.y) * _depthHeight);
                //int idx = wIndex * _depthHeight + hIndex;
                //int idx = (int)((1.0-i.uv.x) * (_depthWidth-1.0) * (_depthHeight) + (1.0-i.uv.y) * (_depthHeight-1.0));
                if(idx < _depthWidth * _depthHeight)
                {
                    float d = _depthBuffer[idx];
                    return float4(d, d, d, 1);
                }

                return float4(0,0,0,1);
#else*/
                return tex2D(_MainTex, i.uv).rrrr;//SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rrrr;
//#endif
            }
            ENDHLSL
        }
    }
}