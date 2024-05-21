
Shader "Unlit/ARKitBackgroundCustom"
{
    Properties
    {
        _textureY ("TextureY", 2D) = "white" {}
        _textureCbCr ("TextureCbCr", 2D) = "black" {}
        _EnvironmentDepth ("EnvironmentDepth", 2D) = "black" {}
        _EnvironmentConf ("EnvironmentConf", 2D) = "black" {}
        _texturePoints ("TexturePoints", 2D) = "white" {}
        _isScanning("Is Scanning", float) = 0.0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Cull Off
            ZTest Always
            ZWrite On
            Lighting Off
            LOD 100
            Tags
            {
                "LightMode" = "Always"
            }


            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local __ ARKIT_BACKGROUND_URP ARKIT_BACKGROUND_LWRP
            #pragma multi_compile_local __ ARKIT_HUMAN_SEGMENTATION_ENABLED ARKIT_ENVIRONMENT_DEPTH_ENABLED

            #define ARKIT_ENVIRONMENT_DEPTH_ENABLED 1
            #define LAPLACIAN 1
            #define IPAD 1
            #define HAIL 1
            //#define USE_CPU_DEPTH 1
            //#define VISUALIZE_DEPTH 1
/*#if ARKIT_BACKGROUND_URP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            #define ARKIT_TEXTURE2D_HALF(texture) TEXTURE2D(texture)
            #define ARKIT_SAMPLER_HALF(sampler) SAMPLER(sampler)
            #define ARKIT_TEXTURE2D_FLOAT(texture) TEXTURE2D(texture)
            #define ARKIT_SAMPLER_FLOAT(sampler) SAMPLER(sampler)
            #define ARKIT_SAMPLE_TEXTURE2D(texture,sampler,texcoord) SAMPLE_TEXTURE2D(texture,sampler,texcoord)

#elif ARKIT_BACKGROUND_LWRP

            #include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            #define ARKIT_TEXTURE2D_HALF(texture) TEXTURE2D(texture)
            #define ARKIT_SAMPLER_HALF(sampler) SAMPLER(sampler)
            #define ARKIT_TEXTURE2D_FLOAT(texture) TEXTURE2D(texture)
            #define ARKIT_SAMPLER_FLOAT(sampler) SAMPLER(sampler)
            #define ARKIT_SAMPLE_TEXTURE2D(texture,sampler,texcoord) SAMPLE_TEXTURE2D(texture,sampler,texcoord)

#else // Legacy RP*/

            #include "UnityCG.cginc"

            #define real4 half4
            #define real4x4 half4x4
            #define TransformObjectToHClip UnityObjectToClipPos
            #define FastSRGBToLinear GammaToLinearSpace

            #define ARKIT_TEXTURE2D_HALF(texture) UNITY_DECLARE_TEX2D_HALF(texture)
            #define ARKIT_SAMPLER_HALF(sampler)
            #define ARKIT_TEXTURE2D_FLOAT(texture) UNITY_DECLARE_TEX2D_FLOAT(texture)
            #define ARKIT_SAMPLER_FLOAT(sampler)
            #define ARKIT_SAMPLE_TEXTURE2D(texture,sampler,texcoord) UNITY_SAMPLE_TEX2D(texture,texcoord)

//#endif


            struct appdata
            {
                float3 position : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
            };

            struct fragment_output
            {
                real4 color : SV_Target;
                float depth : SV_Depth;
            };

            CBUFFER_START(UnityARFoundationPerFrame)
            // Device display transform is provided by the AR Foundation camera background renderer.
            float4x4 _UnityDisplayTransform;
            float _UnityCameraForwardScale;
            CBUFFER_END

            v2f vert (appdata v)
            {
                // Transform the position from object space to clip space.
                float4 position = TransformObjectToHClip(v.position);

                // Remap the texture coordinates based on the device rotation.
                float2 texcoord = mul(float3(v.texcoord, 1.0f), _UnityDisplayTransform).xy;

                v2f o;
                o.position = position;
                o.texcoord = texcoord;
                o.texcoord1 = v.texcoord;
                return o;
            }


            CBUFFER_START(ARKitColorTransformations)
            static const real4x4 s_YCbCrToSRGB = real4x4(
                real4(1.0h,  0.0000h,  1.4020h, -0.7010h),
                real4(1.0h, -0.3441h, -0.7141h,  0.5291h),
                real4(1.0h,  1.7720h,  0.0000h, -0.8860h),
                real4(0.0h,  0.0000h,  0.0000h,  1.0000h)
            );
            CBUFFER_END


            inline float ConvertDistanceToDepth(float d)
            {
                // Account for scale
                d = _UnityCameraForwardScale > 0.0 ? _UnityCameraForwardScale * d : d;
                // Clip any distances smaller than the near clip plane, and compute the depth value from the distance.
                return (d < _ProjectionParams.y) ? 0.0f : ((1.0f / _ZBufferParams.z) * ((1.0f / d) - _ZBufferParams.w));
            }

            //UNITY_DECLARE_TEX2D(_texturePoints);
            //ARKIT_SAMPLER_HALF(sampler_texturePoints);
            ARKIT_TEXTURE2D_FLOAT(_texturePoints);
            ARKIT_SAMPLER_FLOAT(sampler_texturePoints);
            ARKIT_TEXTURE2D_FLOAT(_ourDepth);
            ARKIT_SAMPLER_FLOAT(sampler_ourDepth);
            ARKIT_TEXTURE2D_HALF(_textureY);
            ARKIT_SAMPLER_HALF(sampler_textureY);
            ARKIT_TEXTURE2D_HALF(_textureCbCr);
            ARKIT_SAMPLER_HALF(sampler_textureCbCr);
#if ARKIT_ENVIRONMENT_DEPTH_ENABLED
            ARKIT_TEXTURE2D_FLOAT(_EnvironmentDepth);
            ARKIT_SAMPLER_FLOAT(sampler_EnvironmentDepth);
            ARKIT_TEXTURE2D_HALF(_EnvironmentConf);
            ARKIT_SAMPLER_HALF(sampler_EnvironmentConf);
#endif // ARKIT_HUMAN_SEGMENTATION_ENABLED

            float _isScanning;
            float _isHailMode;
            float _depthMin;
            float _depthMax;
            float _passthroughAmount;
            
            float4x4 _camIntrinsicsInverse;
            float4x4 _localToWorld;
 
 			float3 yuv2rgb(uint y, uint u, uint v) {
				int r = y + (1.370705 * (v - 128));
				int g = y - (0.698001 * (v - 128)) - (0.337633 * (u - 128));
				int b = y + (1.732446 * (u - 128));
				r = clamp(r, 0, 255);
				g = clamp(g, 0, 255);
				b = clamp(b, 0, 255);
				return float3(r, g, b);
			}
			
            /*float4 GetPixelValue(in float2 uv) {
                half3 normal = half3(0,0,0);
                float depth = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentDepth, sampler_EnvironmentDepth, uv).r;
                //DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, uv), depth, normal); 

                return fixed4(normal, depth);
            }*/
            
           // float convertToFloat(uint i)
            //{
           //     return float(i) / float(0xFFFFFFFF);
           // }

            fragment_output frag (v2f i)
            {
                
                real4 videoColor = real4(0,0,0,1);
                float depthValue = 0.0f;
                if(_isHailMode > 0)
                {
                    
                    const float2 offsets[8] = {
                            float2(-1, -1),
                            float2(-1, 0),
                            float2(-1, 1),
                            float2(0, -1),
                            float2(0, 1),
                            float2(1, -1),
                            float2(1, 0),
                            float2(1, 1)
                        };
#if LAPLACIAN
                    //const float2 oneOWH = float2(1.0/1284.0, 1.0/2778.0);
                    //below number were taken from writing out the display matrix in debug... basically it's used to fit the full
                    float2 tc2 = i.texcoord1;

                    //texturePoints has the result of what has gone through compute shaders...
                    float2 tcScale = float2(0.6163, 1.0);//float2(1536.0, 2048.0) / float2(1440.0, 1920.0);
                    float2 tcOffset = float2(0.1919, 0.0);
#if IPAD
                    tcScale.x = 1.0;
                    tcOffset.x = 0.0;
#endif
                    //float2 ourTC = i.texcoord1;
                    videoColor = float4(ARKIT_SAMPLE_TEXTURE2D(_texturePoints, sampler_texturePoints, tc2 * tcScale + tcOffset).rgb, 1.0);//(ourTC * (1.0 / tcScale)) );// * float2(0.8359, 1.356));

#if HAIL
                    //if(length(videoColor.xyz) == 0 || _passthroughAmount != 0.0)
                    {
                        real4 ycbcr = real4(ARKIT_SAMPLE_TEXTURE2D(_textureY, sampler_textureY, i.texcoord).r,
                                    ARKIT_SAMPLE_TEXTURE2D(_textureCbCr, sampler_textureCbCr, i.texcoord).rg,
                                    1.0h);

                    // Convert from YCbCr to sRGB.
                        real4 videoColorPassthrough = mul(s_YCbCrToSRGB, ycbcr);

#if !UNITY_COLORSPACE_GAMMA
                    // If rendering in linear color space, convert from sRGB to RGB.
                        videoColorPassthrough.xyz = FastSRGBToLinear(videoColorPassthrough.xyz);
#endif // !UNITY_COLORSPACE_GAMMA
                        videoColor = (videoColor * _passthroughAmount) + videoColorPassthrough * (1.0 - _passthroughAmount);
                    }
#endif
                    //need to pass in the raw depth to test here...
                    //const float2 oneOWH = float2(1.0/1536.0, 1.0/2048.0);
#if USE_CPU_DEPTH
                    float envDistance = ARKIT_SAMPLE_TEXTURE2D(_ourDepth, sampler_ourDepth, tc2 * tcScale + tcOffset).r;
                    depthValue = ConvertDistanceToDepth(envDistance);
#else
                    float envDistance = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentDepth, sampler_EnvironmentDepth, i.texcoord).r;
                    depthValue = ConvertDistanceToDepth(envDistance);
#endif

#if VISUALIZE_DEPTH
                    
#if USE_CPU_DEPTH
                    /*i.texcoord1.y = i.texcoord1.y + 0.5;
                    if(i.texcoord1.y > 1.0)
                    {
                        i.texcoord1.y = i.texcoord1.y - 1.0;
                    }*/

                    envDistance = ARKIT_SAMPLE_TEXTURE2D(_ourDepth, sampler_ourDepth, i.texcoord1 * tcScale + tcOffset).r;//float2(0.1919, 0.0)).r;
                    depthValue = ConvertDistanceToDepth(envDistance);
#endif
                    const float f = (envDistance - _depthMin) / (_depthMax - _depthMin);
                    half hue = lerp(0.7, -0.15, saturate(f));
                    if(hue < 0.0h)
                    {
                        hue += 1.0h;
                    }
                    float3 c = float3(hue, 0.9, 0.6);
                    float4 K = float4(1.0, 0.6666, 0.3333, 3.0);
                    float3 P = abs(frac(c.xxx + K.rgb) * 6.0 - K.www);
                    const float3 sampledColor = c.z * lerp(K.xxx, saturate(P-K.xxx), c.y);
                    videoColor = float4(sampledColor.xyz, 1.0);
#endif
#else   //if not laplacian
                    const float2 oneOWH = float2(1.0/1536.0, 1.0/2048.0);
                    float envDistance = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentDepth, sampler_EnvironmentDepth, i.texcoord).r;
                    const float f = (envDistance - _depthMin) / (_depthMax - _depthMin);
                    depthValue = ConvertDistanceToDepth(envDistance);

                    //float weight = (maxD - (uniforms.ambient-uniforms.ambientColor)) / maxD;
                
                    half hue = lerp(0.7, -0.15, saturate(f));
                    if(hue < 0.0h)
                    {
                        hue += 1.0h;
                    }
                    float3 c = float3(hue, 0.9, 0.6);
                    float4 K = float4(1.0, 0.6666, 0.3333, 3.0);
                    float3 P = abs(frac(c.xxx + K.rgb) * 6.0 - K.www);
                    const float3 sampledColor = c.z * lerp(K.xxx, saturate(P-K.xxx), c.y);
                
                    //const auto sampledColorActual = (yCbCrToRGB * ycbcr).rgb;
                    float3 sampledColorNorm = float3(0,0,0);//norm;//(yCbCrToRGB * float4(norm, 1.0)).rgb;

                    float2 wh = float2(1536.0, 2048.0);
                    
                    float3 camPoint = float3(i.texcoord * wh, 1.0);
                    float3 dP = mul(_camIntrinsicsInverse, float4(camPoint, 0)).xyz * envDistance;
                    float4 dP4 = float4(dP.xyz, 1.0);
                    float4 wp = mul(_localToWorld, dP4);
                    wp.xyz /= wp.w;

                    float dR = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentDepth, sampler_EnvironmentDepth, i.texcoord + offsets[6] * oneOWH).r;
                    float3 camPointdR = float3(i.texcoord * wh + offsets[6], 1.0);
                    float3 dRP = mul(_camIntrinsicsInverse, float4(camPointdR, 0)).xyz * dR;
                    
                    float4 dRP4 = float4(dRP.xyz, 1.0);
                    float4 wpR = mul(_localToWorld, dRP4);
                    wpR.xyz /= wpR.w;

                    float dL = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentDepth, sampler_EnvironmentDepth, i.texcoord + offsets[1] * oneOWH).r;
                    float3 camPointdL = float3(i.texcoord * wh + offsets[1], 1.0);
                    float3 dLP = mul(_camIntrinsicsInverse, float4(camPointdL, 0)).xyz * dL; 
                    
                    float4 dLP4 = float4(dLP.xyz, 1.0);
                    float4 wpL = mul(_localToWorld, dLP4);
                    wpL.xyz /= wpL.w;

                    float dT = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentDepth, sampler_EnvironmentDepth, i.texcoord + offsets[4] * oneOWH).r;
                    float3 camPointdT = float3(i.texcoord * wh + offsets[4], 1.0);
                    float3 dTP = mul(_camIntrinsicsInverse, float4(camPointdT, 0)).xyz * dT; 
                    
                    float4 dTP4 = float4(dTP.xyz, 1.0);
                    float4 wpT = mul(_localToWorld, dTP4);
                    wpT.xyz /= wpT.w;

                    float dB = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentDepth, sampler_EnvironmentDepth, i.texcoord + offsets[3] * oneOWH).r;
                    float3 camPointdB = float3(i.texcoord * wh + offsets[3], 1.0);
                    float3 dBP = mul(_camIntrinsicsInverse, float4(camPointdB, 0)).xyz * dB;

                    float4 dBP4 = float4(dBP.xyz, 1.0);
                    float4 wpB = mul(_localToWorld, dBP4);
                    wpB.xyz /= wpB.w;

                    //instead average the 4 corner values...

                    float3 vecT = normalize(wpT.xyz - wp.xyz);//normalize(dLP - dRP);
                    float3 vecB = normalize(wpB.xyz - wp.xyz);//normalize(dBP - dTP);//
                    float3 vecR = normalize(wpR.xyz - wp.xyz);
                    float3 vecL = normalize(wpL.xyz - wp.xyz);


                    sampledColorNorm = normalize(cross(vecT, vecR));
                    sampledColorNorm += normalize(cross(vecR, vecB));
                    sampledColorNorm += normalize(cross(vecB, vecL));
                    sampledColorNorm += normalize(cross(vecL, vecT));
                    sampledColorNorm /= 4.0;

                    //sampledColorNorm = abs(sampledColorNorm);
                    sampledColorNorm.xyz = sampledColorNorm.xyz * 0.5 + 0.5;
                    //sampledColorNorm.z = abs(sampledColorNorm.z);

                    //final weight by depth color and normal color...
                    videoColor.rgb = sampledColorNorm;//sampledColor*0.5 + sampledColorNorm*0.5;
#endif
                }
                else
                {
                    //only show the textured point color if we're scanning...   
                    
                    //videoColor.xyz /= 255.0;
                    // Sample the video textures (in YCbCr).
#if UNITY_ANDROID
                    real4 t = ARKIT_SAMPLE_TEXTURE2D(_textureY, sampler_textureY, i.texcoord).rgba;
					real4 videoColorPassthrough = real4(yuv2rgb(uint(t.x*255), uint(t.y*255), uint(t.z*255))/255.0, 1.0);
					
#else
                    real4 ycbcr = real4(ARKIT_SAMPLE_TEXTURE2D(_textureY, sampler_textureY, i.texcoord).r,
                                    ARKIT_SAMPLE_TEXTURE2D(_textureCbCr, sampler_textureCbCr, i.texcoord).rg,
                                    1.0h);

                    // Convert from YCbCr to sRGB.
                    real4 videoColorPassthrough = mul(s_YCbCrToSRGB, ycbcr);
#endif

					
#if !UNITY_COLORSPACE_GAMMA
                    // If rendering in linear color space, convert from sRGB to RGB.
                    videoColorPassthrough.xyz = FastSRGBToLinear(videoColorPassthrough.xyz);
#endif // !UNITY_COLORSPACE_GAMMA
                
                    // Assume the background depth is the back of the depth clipping volume.
                
#if ARKIT_ENVIRONMENT_DEPTH_ENABLED
                    // Sample the environment depth (in meters).
                    float envDistance = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentDepth, sampler_EnvironmentDepth, i.texcoord).r;

                    // Convert the distance to depth.
                    depthValue = ConvertDistanceToDepth(envDistance);

                    
                    /*half envConf = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentConf, sampler_EnvironmentConf, i.texcoord).r;
                    //if(envConf > (2.0/255.0)-0.001 && envConf < (2.0/255.0)+0.001)// || envDistance > 8.0)
                    if(envConf == 2)
                    {
                        videoColor.xyz = videoColorPassthrough.xyz;
                       
                    }
                    //else if(envConf > (1.0/255.0)-0.001 && envConf < (1.0/255.0)+0.001)
                    else if(envConf == 1)
                    {
                        videoColor.xyz = float3(1,1,0);
                        //float mean = (videoColorPassthrough.r + videoColorPassthrough.g + videoColorPassthrough.b) * 0.3333333;
                        //float3 dev  = videoColorPassthrough - mean;
                        //videoColorPassthrough.xyz = mean + dev * 0.1;
                        //videoColorPassthrough.z *= 0.1;
                        //videoColor.xyz = videoColorPassthrough.xyz;
                        //videoColor.xyz = real4(0.5, 0.5, 0.5, 1.0);
                    }
                    else
                    {
                        videoColor.xyz = float3(1,0,0);//videoColorPassthrough.xyz;
                    }*/
                    //else
                    {
                        if(_isScanning == 0.0)
                        {
                            /*const float f = (envDistance - _depthMin) / (_depthMax - _depthMin);
                            //depthValue = ConvertDistanceToDepth(envDistance);

                            //float weight = (maxD - (uniforms.ambient-uniforms.ambientColor)) / maxD;
                        
                            half hue = lerp(0.7, -0.15, saturate(f));
                            if(hue < 0.0h)
                            {
                                hue += 1.0h;
                            }
                            float3 c = float3(hue, 0.9, 0.6);
                            float4 K = float4(1.0, 0.6666, 0.3333, 3.0);
                            float3 P = abs(frac(c.xxx + K.rgb) * 6.0 - K.www);
                            const float3 sampledColor = c.z * lerp(K.xxx, saturate(P-K.xxx), c.y);
                        
                            videoColor.xyz = sampledColor;//videoColorPassthrough.xyz;*/
                            videoColor.xyz = videoColorPassthrough.xyz;
                        }
                        else
                        {
                            videoColor = ARKIT_SAMPLE_TEXTURE2D(_texturePoints, sampler_texturePoints, i.texcoord1);
                            //could augment the scanned color with something here if desired...
                        }

                        //fixed4 orValue = //GetPixelValue(i.texcoord);
                        //orValue.w = orValue.w;
                        /*float2 offsets[8] = {
                            float2(-1, -1),
                            float2(-1, 0),
                            float2(-1, 1),
                            float2(0, -1),
                            float2(0, 1),
                            float2(1, -1),
                            float2(1, 0),
                            float2(1, 1)
                        };
                        //fixed4 sampledValue = fixed4(0,0,0,0);
                        for(int j = 0; j < 8; j++) 
                        {
                            float d2 = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentDepth, sampler_EnvironmentDepth, i.texcoord + offsets[j]).r;
                            if(abs(envDistance - d2) > 0.04)
                            {
                                videoColor = float4(0.0,0.0,0.0,1.0);
                            }
                            //sampledValue += GetPixelValue(i.texcoord + offsets[j] * wh * 8.0);
                        }*/
                        //sampledValue.w += orValue.w;
                        //sampledValue.w /= 9;
                        //videoColor = lerp(videoColor, _EdgeColor, length(orValue.w - sampledValue.w)/orValue.w);//step(_Threshold, length(orValue.w - sampledValue.w)));
                        //desaturated view of scene...?
                    }
#endif
                }
                fragment_output o;
                o.color = videoColor;
                o.depth = depthValue;
                return o;
            }

            ENDHLSL
        }
    }
}
