
Shader "Unlit/ARKitBackgroundCustom2"
{
    Properties
    {
        _textureY ("TextureY", 2D) = "white" {}
        _textureCbCr ("TextureCbCr", 2D) = "black" {}
        _EnvironmentDepth ("EnvironmentDepth", 2D) = "black" {}
        _EnvironmentConf ("EnvironmentConf", 2D) = "black" {}
        _TopHeight("Top Height", float) = 500.0
        _BottomHeight("Bottom Height", float) = -500.0
        _TintColor("Tint color", Color) = (0,1,0,1)
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

            float _TopHeight;
            float _BottomHeight;
            float _OffsetValue;
            float _ScaleValue;
            float4 _TintColor;
            float4 _PressPoint;
            float4 _WindowSize;
            //float4 _lightDir;
            float _PressOffset;
            float4 _ScreenWidthHeight;
            float4 _ColorWidthHeight;
            
            float4x4 _camIntrinsicsInverse;
            float4x4 _localToWorld;

            fragment_output frag (v2f i)
            {
                float2 tc2 = i.texcoord;
                int shouldBeGreen = 0;
                float3 gColor = float3(0,0,0);

#if ARKIT_ENVIRONMENT_DEPTH_ENABLED
                float envDistance = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentDepth, sampler_EnvironmentDepth, i.texcoord).r;
#endif

                if(length(_PressPoint) > 0)
                {    
                    float2 tcScale = float2(_ScaleValue, 1.0);
                    float2 tc3 = i.texcoord1 * tcScale + float2(_OffsetValue, 0.0);
                    float2 screenSpace = tc3 * _ColorWidthHeight.xy;

                    float2 ppNorm = (_PressPoint.xy ) / _ScreenWidthHeight.xy;
                    float2 ppTC = ppNorm * tcScale + float2(_OffsetValue, 0.0);
                    float2 ppScreen = ppTC * _ColorWidthHeight.xy;

                    float2 ppNormMin = (_PressPoint.xy - float2(_WindowSize.z * 0.5, _WindowSize.w * 0.5) + float2(0, _PressOffset)) / _ScreenWidthHeight.xy;
                    float2 ppTCMin = ppNormMin * tcScale + float2(_OffsetValue, 0.0);
                    float2 ppScreenMin = ppTCMin * _ColorWidthHeight.xy;

                    float2 ppNormMax = (_PressPoint.xy + float2(_WindowSize.z * 0.5, _WindowSize.w * 0.5) + float2(0, _PressOffset)) / _ScreenWidthHeight.xy;
                    float2 ppTCMax = ppNormMax * tcScale + float2(_OffsetValue, 0.0);
                    float2 ppScreenMax = ppTCMax * _ColorWidthHeight.xy;
                    //float2 windowNorm = _WindowSize.zw / float2(1284.0, 2778.0);
                    //float2 windowTC = windowNorm * tcScale + float2(_OffsetValue, 0.0);
                    //float2 windowScreen = windowTC * float2(1440.0, 1920.0);

                    if(screenSpace.x > ppScreenMin.x && screenSpace.x < ppScreenMax.x && screenSpace.y > ppScreenMin.y && screenSpace.y < ppScreenMax.y)
                    {
                        tc2.x = tc2.x + 0.12f;
                        if(distance(float2(screenSpace.x, screenSpace.y), float2(ppScreen.x, ((ppScreenMax.y + ppScreenMin.y) * 0.5))) < (67.5 / envDistance))
                        //if(abs(ppScreen.x - screenSpace.x) < 5 || abs(((ppScreenMax.y + ppScreenMin.y) * 0.5 ) - screenSpace.y) < 5)
                        {
                            shouldBeGreen = 1;
                            //float3 sphereN = float3(screenSpace.x - ppScreen.x, screenSpace.y - ppScreen.y, 0);
                            //sphereN = normalize(sphereN);
                            //sphereN.z = -1;
                            //sphereN = normalize(sphereN);
                            //float3 lightDir = float3(0.75, 0.75, -0.75);
                            //lightDir = normalize(lightDir);
                            //gColor = float3(0, clamp(dot(sphereN, lightDir.xyz), 0, 1), 0);
                            gColor = float3(0,1,0);
                        }
                        else if((screenSpace.x < (ppScreenMin.x + 10.0)) || (screenSpace.x > (ppScreenMax.x - 10.0)) || (screenSpace.y < (ppScreenMin.y + 10.0)) || (screenSpace.y > (ppScreenMax.y - 10.0)))
                        {
                            shouldBeGreen = 1;
                            gColor = float3(0,1,0);
                        }

                        if(tc2.x < 0)
                        {
                            tc2.x = 0.0;
                        }
                    }
                }

                // Sample the video textures (in YCbCr).
                real4 ycbcr = real4(ARKIT_SAMPLE_TEXTURE2D(_textureY, sampler_textureY, tc2).r,
                                    ARKIT_SAMPLE_TEXTURE2D(_textureCbCr, sampler_textureCbCr, tc2).rg,
                                    1.0h);

                // Convert from YCbCr to sRGB.
                real4 videoColor = mul(s_YCbCrToSRGB, ycbcr);
                if(shouldBeGreen == 1)
                {
                    //could assume a light somewhere and shade accordingly...
                    //videoColor = real4(gColor.rgb, 0.5);
                    videoColor = real4(videoColor.rgb * float3(1.0 - gColor.g, gColor.g, 1.0 - gColor.g), 1.0);
                }

#if !UNITY_COLORSPACE_GAMMA
                // If rendering in linear color space, convert from sRGB to RGB.
                //videoColor.xyz = FastSRGBToLinear(videoColor.xyz);
#endif // !UNITY_COLORSPACE_GAMMA
                
                // Assume the background depth is the back of the depth clipping volume.
                
#if ARKIT_ENVIRONMENT_DEPTH_ENABLED
                // Sample the environment depth (in meters).
                
                // Convert the distance to depth.
                float depthValue = ConvertDistanceToDepth(envDistance);

                /*if(envDistance > 5.0)
                {
                    videoColor.xyz = real4(0.0, 0.0, 0.0, 1.0);
                }
                else
                {*/
                //this scaler has to do with aspect ratios...
                //1284 / 2778 = 0.4622
                //1536 / 2048 = 0.75
                // 0.4622 / 0.75 = 0.61627...
                float2 tcScale = float2(_ScaleValue, 1.0);
                float2 tc = i.texcoord1 * tcScale + float2(_OffsetValue, 0.0);
                //resolutions:  depth: 1536x2048, color: 1440x1920, screen: 1284x2778
                    float dX = tc.x * _ColorWidthHeight.x;
                    float dY = tc.y * _ColorWidthHeight.y;
                    
                    float4 localPoint = mul(_camIntrinsicsInverse, float4(dX + 0.5, dY + 0.5, 1.0, 0.0)) * envDistance;
                    localPoint.w = 1.0;
                    float4 worldPoint = mul(_localToWorld, localPoint);
                    if(worldPoint.w != 0)
                    {
                        worldPoint = worldPoint / worldPoint.w;

                        if(worldPoint.y >= _TopHeight || worldPoint.y <= _BottomHeight)
                        {
                            //could also do greyscale here instead...
                            videoColor *= _TintColor;
                        }
                    }
                //}

                /*half envConf = ARKIT_SAMPLE_TEXTURE2D(_EnvironmentConf, sampler_EnvironmentConf, i.texcoord).r;
                if(envConf == 0.0 || envDistance > 5.0)
                {
                    videoColor.xyz = real4(0.0, 0.0, 0.0, 1.0);
                }
                else if(envConf > (1.0/255.0)-0.001 && envConf < (1.0/255.0)+0.001)
                {
                    float mean = (videoColor.r + videoColor.g + videoColor.b) * 0.3333333;
                    float3 dev  = videoColor - mean;
                    videoColor.xyz = mean + dev * 0.1;
                    videoColor.z *= 0.1;
                    //videoColor.xyz = real4(0.5, 0.5, 0.5, 1.0);
                }
                else
                {

                }*/
#endif
                fragment_output o;
                o.color = videoColor;
                o.depth = depthValue;
                return o;
            }

            ENDHLSL
        }
    }
}
