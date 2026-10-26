Shader "VacuumProtocol/PhantomSuctionEcho"
{
    Properties
    {
        [HDR] _Color ("Tint Color (HDR)", Color) = (0.25, 0.85, 1.0, 0.8)
        _FresnelPower ("Fresnel Power", Range(0.1, 10.0)) = 2.5
        _FresnelBias ("Fresnel Bias", Range(0.0, 1.0)) = 0.05
        _InnerAlpha ("Inner Alpha", Range(0.0, 1.0)) = 0.08
        _Alpha ("Global Alpha", Range(0.0, 1.0)) = 1.0
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

        LOD 100
        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "SuctionEchoPass"
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
                float3 normalWS     : TEXCOORD0;
                float3 viewDirWS    : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FresnelPower;
                float _FresnelBias;
                float _InnerAlpha;
                float _Alpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalize(normalInput.normalWS);
                output.viewDirWS = normalize(GetCameraPositionWS() - vertexInput.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);

                // Fresnel rim glow
                float NdotV = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);
                fresnel = saturate(_FresnelBias + (1.0 - _FresnelBias) * fresnel);

                float finalAlpha = (fresnel + _InnerAlpha) * _Color.a * _Alpha;
                half3 finalColor = _Color.rgb * (fresnel * 2.0 + _InnerAlpha) * finalAlpha;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
