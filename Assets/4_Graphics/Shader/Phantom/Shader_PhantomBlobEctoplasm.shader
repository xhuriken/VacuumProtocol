Shader "VacuumProtocol/PhantomBlobEctoplasm"
{
    Properties
    {
        [Header(Gelatinous Blob Vertex Displacement)]
        _BlobAmplitude ("Wobble Amplitude", Range(0.0, 0.4)) = 0.07
        _BlobFrequency ("Wobble Spatial Scale", Range(0.1, 10.0)) = 2.5
        _BlobSpeed ("Wobble Speed", Range(0.0, 10.0)) = 2.2
        _BlobSecondaryScale ("Micro Ripple Scale", Range(0.1, 25.0)) = 6.0
        _BlobSecondaryAmp ("Micro Ripple Amplitude", Range(0.0, 0.15)) = 0.02

        [Header(Strong Fresnel Glow)]
        [HDR] _FresnelColor ("Fresnel Rim Color (HDR)", Color) = (0.3, 0.95, 1.0, 1.0)
        _FresnelPower ("Fresnel Power", Range(0.1, 8.0)) = 2.2
        _FresnelIntensity ("Fresnel Intensity", Range(0.1, 8.0)) = 3.0
        _FresnelBias ("Fresnel Bias", Range(0.0, 0.5)) = 0.02

        [Header(Interior 3D Voronoi Cellular Noise)]
        [HDR] _VoronoiColor ("Voronoi Cell Color (HDR)", Color) = (0.1, 0.5, 0.9, 1.0)
        _VoronoiScale ("Voronoi Scale", Range(0.5, 20.0)) = 4.5
        _VoronoiSpeed ("Voronoi Flow Speed", Range(0.0, 5.0)) = 0.7
        _VoronoiPower ("Voronoi Cell Contrast", Range(0.2, 5.0)) = 2.0
        _VoronoiInnerOpacity ("Voronoi Core Opacity", Range(0.0, 1.0)) = 0.45

        [Header(Base Body Appearance)]
        [HDR] _BaseColor ("Ghost Base Color (HDR)", Color) = (0.12, 0.45, 0.75, 0.35)
        _Alpha ("Master Opacity", Range(0.0, 1.0)) = 0.85
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull [_Cull]

        Pass
        {
            Name "PhantomBlobPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionOS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 viewDirWS    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float _BlobAmplitude;
                float _BlobFrequency;
                float _BlobSpeed;
                float _BlobSecondaryScale;
                float _BlobSecondaryAmp;

                half4 _FresnelColor;
                float _FresnelPower;
                float _FresnelIntensity;
                float _FresnelBias;

                half4 _VoronoiColor;
                float _VoronoiScale;
                float _VoronoiSpeed;
                float _VoronoiPower;
                float _VoronoiInnerOpacity;

                half4 _BaseColor;
                float _Alpha;
                float _Cull;
            CBUFFER_END

            // Fast harmonic multi-frequency 3D sinusoidal wave displacement
            float CalculateBlobWobble(float3 pos, float time)
            {
                float w1 = sin(pos.x * _BlobFrequency + time * _BlobSpeed) 
                         * cos(pos.y * _BlobFrequency * 0.85 + time * _BlobSpeed * 1.15) 
                         * sin(pos.z * _BlobFrequency * 1.1 + time * _BlobSpeed * 0.9);

                float w2 = sin(pos.y * _BlobSecondaryScale + time * _BlobSpeed * 1.7) 
                         * cos(pos.z * _BlobSecondaryScale * 0.9 + time * _BlobSpeed * 1.4);

                return w1 * _BlobAmplitude + w2 * _BlobSecondaryAmp;
            }

            // Pseudo-random 3D hash for cellular points
            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            // 3D Voronoi / cellular distance field (operates in object space, 0 UV dependencies)
            float Voronoi3D(float3 p, float time)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float minDist = 1.0;

                for (int z = -1; z <= 1; z++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            float3 neighbor = float3(x, y, z);
                            float3 cellPoint = Hash33(i + neighbor);
                            // Animate gentle internal floating oscillation
                            cellPoint = 0.5 + 0.4 * sin(time * 2.0 + 6.2831 * cellPoint);
                            float3 diff = neighbor + cellPoint - f;
                            float dist = length(diff);
                            minDist = min(minDist, dist);
                        }
                    }
                }
                return minDist;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float time = _Time.y;
                float3 originalOS = input.positionOS.xyz;

                // 1. Compute vertex blob wobble displacement
                float wobble = CalculateBlobWobble(originalOS, time);
                float3 displacedOS = originalOS + input.normalOS * wobble;

                // 2. Normal displacement approximation via finite differences
                float eps = 0.02;
                float wobbleX = CalculateBlobWobble(originalOS + float3(eps, 0, 0), time);
                float wobbleY = CalculateBlobWobble(originalOS + float3(0, eps, 0), time);
                float wobbleZ = CalculateBlobWobble(originalOS + float3(0, 0, eps), time);
                float3 grad = float3(wobbleX - wobble, wobbleY - wobble, wobbleZ - wobble) / eps;
                float3 displacedNormalOS = normalize(input.normalOS - grad * 0.4);

                // Transform to clip and world space
                VertexPositionInputs vertexInput = GetVertexPositionInputs(displacedOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(displacedNormalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionOS = displacedOS;
                output.normalWS = normalize(normalInput.normalWS);
                output.viewDirWS = normalize(GetCameraPositionWS() - vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);

                // 1. Strong Fresnel rim calculation
                float NdotV = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);
                fresnel = saturate(_FresnelBias + (1.0 - _FresnelBias) * fresnel);
                float3 fresnelEmission = _FresnelColor.rgb * (fresnel * _FresnelIntensity);

                // 2. Interior 3D Voronoi cellular texture in object-space (0 UV stretching)
                float3 voronoiCoord = input.positionOS * _VoronoiScale + float3(0.0, _Time.y * _VoronoiSpeed, 0.0);
                float cellDist = Voronoi3D(voronoiCoord, _Time.y * _VoronoiSpeed);
                float cellFactor = pow(saturate(cellDist), _VoronoiPower);

                // Voronoi lives in the interior of the ghost, softly modulated by the Fresnel rim
                float interiorFactor = saturate(1.0 - fresnel * 0.7);
                float3 voronoiEmission = _VoronoiColor.rgb * (cellFactor * interiorFactor * _VoronoiInnerOpacity * 2.0);

                // 3. Composite lighting and coloration
                float3 finalColor = _BaseColor.rgb + voronoiEmission + fresnelEmission;

                // 4. Alpha: translucent gelatin body + crisp opaque glowing rim
                float interiorAlpha = _BaseColor.a + (cellFactor * _VoronoiInnerOpacity * 0.4);
                float finalAlpha = saturate(lerp(interiorAlpha, 1.0, fresnel)) * _Alpha;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
