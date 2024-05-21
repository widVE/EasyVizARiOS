Shader "Unlit/CopyColorShader" {
    Properties
    {
        _MainTex ("Texture", any) = "" {}
        _SecondTex ("Texture2", any) = "" {}
        _Color("Multiplicative color", Color) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader {
        Pass {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_SecondTex);
            uniform float4 _MainTex_ST;
            uniform float4 _Color;
            uniform int _Orientation;
            

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

			float3 yuv2rgb(uint y, uint u, uint v) {
				int r = y + (1.370705 * (v - 128));
				int g = y - (0.698001 * (v - 128)) - (0.337633 * (u - 128));
				int b = y + (1.732446 * (u - 128));
				r = clamp(r, 0, 255);
				g = clamp(g, 0, 255);
				b = clamp(b, 0, 255);
				return float3(r, g, b);
			}
			
            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = float2(1.0 - v.texcoord.x, v.texcoord.y);
           
                if (_Orientation == 1) {
                    // Portrait
                    o.texcoord = float2(1.0 - o.texcoord.y, o.texcoord.x);
                }
                else if (_Orientation == 3) {
                    // Landscape left
                    o.texcoord = float2(1.0 - o.texcoord.x, 1.0 - o.texcoord.y);
                }
                o.texcoord = TRANSFORM_TEX(o.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
#if UNITY_ANDROID
				fixed4 c1 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.texcoord);
				return fixed4(yuv2rgb((uint)(c1.r*255), (uint)(c1.g*255), (uint)(c1.b*255)).xyz/255.0, 1.0);
#else
                fixed4 c1 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.texcoord); 
                fixed4 c2 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_SecondTex, i.texcoord);
                //return UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.texcoord) * _Color;
                const float4x4 ycbcrToRGBTransform = float4x4(
                        float4(1.0, +0.0000, +1.4020, -0.7010),
                        float4(1.0, -0.3441, -0.7141, +0.5291),
                        float4(1.0, +1.7720, +0.0000, -0.8860),
                        float4(0.0, +0.0000, +0.0000, +1.0000)
                    );
                return mul(ycbcrToRGBTransform, float4(c1.r, c2.rg, 1.0));
#endif
            }
            ENDCG

        }
    }
    Fallback Off
}