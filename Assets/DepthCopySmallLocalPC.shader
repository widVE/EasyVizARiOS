Shader "Unlit/CopyDepthShaderSmallLocalPC"
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

            float4x4 _camIntrinsics;
            float4x4 _theMatrix;

            //DECLARE_TEXTURE2D_FLOAT(_MainTex);
            //DECLARE_SAMPLER_FLOAT(sampler_MainTex);
            sampler2D_float _MainTex;

            float _depthWidth = 192.0;
            float _depthHeight = 256.0;
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
                int idx = (int)((1.0-i.uv.x) * (_depthWidth) * (_depthHeight) + (1.0-i.uv.y) * (_depthHeight));

                float4 v = float4(i.uv.x*_depthWidth, i.uv.y*_depthHeight, 1.0, 0.0);
                float d = tex2D(_MainTex, i.uv).r;
                //d = d / 1000.0;
                v = mul(_camIntrinsics, v) * d;
                v.w = 1.0;

                //float fX = (v.x * 1000.0f) + 4095.0f;       //-2047->2047 to 0->4095 - 12 bits
                //float fY = (v.y * 1000.0f) + 4095.0f;       //same as above
                
                //if(fX > 8191.0f)
                //{
                //    fX = 8191.0f;
                //}

                //if(fY > 8191.0f)
                //{
                //    fY = 8191.0f;
                //}

                float fZ = v.z * 1000.0;

                //v = mul(localToWorld, v);
                //v = v / v.w;

                //uint u16X = (uint)fX;
                //uint u16Y = (uint)fY;
                uint u16Z = (uint)fZ;
                uint u16A = 0.003 * 1000;
                
                uint u8X = (u16Z & 0x000000FF);
                uint u8Y = (u16Z & 0x0000FF00) >> 8;
                uint u8Z = (u16A & 0x000000FF);
                uint u8A = (u16A & 0x0000FF00) >> 8;
/*#if USE_CPU_DEPTH
                //_depthWidth = 192, _depthHeight = 256
                
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
                //return tex2D(_MainTex, i.uv).rrrr;//SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rrrr;
                return float4(u8X, u8Y, u8Z, u8A);
//#endif
            }
            ENDHLSL
        }
    }
}