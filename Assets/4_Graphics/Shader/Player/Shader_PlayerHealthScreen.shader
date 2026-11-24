Shader "VacuumProtocol/PlayerHealthScreen"
{
    // Description: Robot CRT health screen. Remaining health = phosphor area. Lost health = ectoplasm
    // (Voronoi cells, same visual language as PhantomBlobEctoplasm) eating the screen from one edge.
    // Context: M_HP slot (submesh 3) of SKM_Robot. Runtime values pushed by PlayerHealthScreen.cs through a
    // MaterialPropertyBlock: _Health, _Impact, _HealFlash. Everything else animates with _Time.
    Properties
    {
        [Header(Runtime (driven by PlayerHealthScreen.cs))]
        _Health ("Health Displayed (0-1)", Range(0.0, 1.0)) = 1.0
        _Impact ("Damage Impact Energy (0-1)", Range(0.0, 1.0)) = 0.0
        _HealFlash ("Heal Flash (0-1)", Range(0.0, 1.0)) = 0.0

        [Header(Screen Mapping)]
        _UVRect ("Screen UV Rect (xMin, yMin, xMax, yMax)", Vector) = (0.0, 0.335, 1.0, 0.665)
        _Aspect ("Screen Aspect (width / height)", Range(0.5, 8.0)) = 3.05
        [Toggle] _FlipV ("Flip Vertical", Float) = 1
        [Toggle] _InvertDirection ("Invert Fill Direction (depends on mesh UVs)", Float) = 0

        [Header(Phosphor Health Colors)]
        [HDR] _HealthyColor ("Healthy Phosphor (HDR)", Color) = (0.25, 2.2, 0.6, 1.0)
        [HDR] _WarningColor ("Warning Phosphor (HDR)", Color) = (2.4, 1.3, 0.15, 1.0)
        [HDR] _CriticalColor ("Critical Phosphor (HDR)", Color) = (2.8, 0.25, 0.15, 1.0)
        _LowHealthThreshold ("Low Health Threshold", Range(0.0, 1.0)) = 0.25
        _LowHealthPulseSpeed ("Low Health Pulse Speed", Range(0.0, 30.0)) = 9.0

        [Header(Ectoplasm Contamination)]
        [HDR] _EctoColor ("Ectoplasm Cell Color (HDR)", Color) = (0.3, 1.6, 2.2, 1.0)
        [HDR] _EctoDeepColor ("Ectoplasm Deep Color", Color) = (0.02, 0.08, 0.18, 1.0)
        [HDR] _EctoRimColor ("Contamination Front Rim (HDR)", Color) = (1.2, 3.5, 4.0, 1.0)
        _EctoCellScale ("Cell Scale", Range(1.0, 30.0)) = 7.0
        _EctoSpeed ("Cell Boil Speed", Range(0.0, 5.0)) = 1.1
        _EctoBorderWidth ("Cell Border Width", Range(0.0, 0.5)) = 0.12
        _FrontWobble ("Front Idle Wobble", Range(0.0, 0.5)) = 0.06
        _FrontNoiseScale ("Front Noise Scale", Range(0.5, 20.0)) = 4.0
        _FrontImpactReach ("Front Reach On Impact", Range(0.0, 1.5)) = 0.45
        _SplatterAmount ("Impact Splatter Amount", Range(0.0, 1.0)) = 0.55
        _RimWidth ("Front Rim Width", Range(0.005, 0.3)) = 0.06

        [Header(CRT Look)]
        _Brightness ("Master Brightness", Range(0.0, 4.0)) = 1.0
        _ScanlineCount ("Scanline Count", Range(10.0, 400.0)) = 90.0
        _ScanlineStrength ("Scanline Strength", Range(0.0, 1.0)) = 0.35
        _PixelGridStrength ("Phosphor Grid Strength", Range(0.0, 1.0)) = 0.25
        _FlickerStrength ("Flicker Strength", Range(0.0, 0.5)) = 0.04
        _RollBandStrength ("Rolling Band Strength", Range(0.0, 1.0)) = 0.15
        _VignetteWidth ("Bezel Vignette Width", Range(0.0, 0.5)) = 0.08

        [Header(Impact Glitch)]
        _ChromaticAmount ("Chromatic Aberration On Impact", Range(0.0, 0.1)) = 0.025
        _GlitchAmount ("Line Tear On Impact", Range(0.0, 0.3)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _Health;
            float _Impact;
            float _HealFlash;

            float4 _UVRect;
            float _Aspect;
            float _FlipV;
            float _InvertDirection;

            half4 _HealthyColor;
            half4 _WarningColor;
            half4 _CriticalColor;
            float _LowHealthThreshold;
            float _LowHealthPulseSpeed;

            half4 _EctoColor;
            half4 _EctoDeepColor;
            half4 _EctoRimColor;
            float _EctoCellScale;
            float _EctoSpeed;
            float _EctoBorderWidth;
            float _FrontWobble;
            float _FrontNoiseScale;
            float _FrontImpactReach;
            float _SplatterAmount;
            float _RimWidth;

            float _Brightness;
            float _ScanlineCount;
            float _ScanlineStrength;
            float _PixelGridStrength;
            float _FlickerStrength;
            float _RollBandStrength;
            float _VignetteWidth;

            float _ChromaticAmount;
            float _GlitchAmount;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "HealthScreenForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ---------- Noise helpers ----------
            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash22(i).x;
                float b = Hash22(i + float2(1, 0)).x;
                float c = Hash22(i + float2(0, 1)).x;
                float d = Hash22(i + float2(1, 1)).x;
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float2 CellPoint(float2 cell, float time)
            {
                float2 h = Hash22(cell);
                return 0.5 + 0.42 * sin(time + 6.2831 * h);
            }

            // Animated 2D Voronoi. Returns (F1, exact distance to the cell border, cell hash).
            // Border distance uses Inigo Quilez's method: F2 - F1 is not a true border distance
            // and draws straight streaks inside cells.
            float3 Voronoi2D(float2 p, float time)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                // Pass 1: nearest point.
                float2 nearestCell = 0.0;
                float2 nearestVec = 0.0;
                float f1 = 8.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 r = neighbor + CellPoint(i + neighbor, time) - f;
                        float d = dot(r, r);
                        if (d < f1)
                        {
                            f1 = d;
                            nearestVec = r;
                            nearestCell = neighbor;
                        }
                    }
                }

                // Pass 2: distance to the closest bisector around the nearest cell.
                float border = 8.0;
                for (int by = -2; by <= 2; by++)
                {
                    for (int bx = -2; bx <= 2; bx++)
                    {
                        float2 neighbor = nearestCell + float2(bx, by);
                        float2 r = neighbor + CellPoint(i + neighbor, time) - f;
                        float2 diff = r - nearestVec;
                        if (dot(diff, diff) > 1e-5)
                        {
                            border = min(border, dot(0.5 * (nearestVec + r), normalize(diff)));
                        }
                    }
                }

                return float3(sqrt(f1), border, Hash22(i + nearestCell).x);
            }

            // ---------- Screen shading ----------
            // p.x: 0 = healthy edge, 1 = edge the ectoplasm enters from. p.y: 0 top, 1 bottom.
            float3 ShadeScreen(float2 p, float time)
            {
                float health = saturate(_Health);
                float impact = saturate(_Impact);

                // Front amplitude fades to 0 at full and empty health so the screen is fully clean / fully eaten.
                float edgeFade = saturate((1.0 - health) * 25.0) * saturate(health * 25.0);
                float amplitude = (_FrontWobble + impact * _FrontImpactReach) * edgeFade;

                float frontNoise = ValueNoise(float2(p.y * _FrontNoiseScale, time * _EctoSpeed * 1.7)) * 0.65
                                 + ValueNoise(float2(p.y * _FrontNoiseScale * 2.3 + 17.0, time * _EctoSpeed * 3.1)) * 0.35;
                // Bites point into the healthy zone (towards x = 0).
                float frontX = health - frontNoise * amplitude / _Aspect;

                // Signed distance to the front in aspect space: > 0 = contaminated.
                float sd = (p.x - frontX) * _Aspect;

                // Ectoplasm cells (aspect corrected, boiling).
                float2 cellCoord = float2(p.x * _Aspect, p.y) * _EctoCellScale;
                float3 vor = Voronoi2D(cellCoord + float2(0.0, time * _EctoSpeed * 0.35), time * _EctoSpeed * 2.0);

                // Impact splatter: whole cells ahead of the front get bitten, nearest ones first.
                float splatterReach = impact * _FrontImpactReach * 1.6 * edgeFade;
                float aheadDist = -sd;
                float splatter = step(0.0, aheadDist) * step(aheadDist, splatterReach)
                               * step(vor.z, impact * _SplatterAmount * (1.0 - aheadDist / max(splatterReach, 1e-4)));

                float contaminated = saturate(step(0.0, sd) + splatter);

                // ----- Healthy phosphor -----
                float3 phosphor = lerp(_CriticalColor.rgb, _WarningColor.rgb, saturate(health / 0.5));
                phosphor = lerp(phosphor, _HealthyColor.rgb, saturate((health - 0.5) / 0.5));

                float lowPulse = 1.0;
                if (health < _LowHealthThreshold && health > 0.0)
                {
                    float urgency = 1.0 - health / max(_LowHealthThreshold, 1e-4);
                    lowPulse = lerp(1.0, 0.35 + 0.65 * (0.5 + 0.5 * sin(time * _LowHealthPulseSpeed)), urgency);
                }

                float healGlow = 1.0 + _HealFlash * 1.5;
                float3 healthyCol = phosphor * lowPulse * healGlow;

                // ----- Ectoplasm -----
                float cellBody = saturate(1.0 - vor.x * 1.3);
                float border = 1.0 - smoothstep(0.0, _EctoBorderWidth, vor.y);
                float bubble = 0.75 + 0.25 * sin(time * _EctoSpeed * 4.0 + vor.z * 40.0);
                float3 ectoCol = lerp(_EctoDeepColor.rgb, _EctoColor.rgb * 0.45, cellBody * bubble)
                               + _EctoColor.rgb * border * 0.9;
                // Dead screen: cells slow down and dim.
                ectoCol *= lerp(0.45, 1.0, saturate(health * 10.0 + impact));

                float3 col = lerp(healthyCol, ectoCol, contaminated);

                // ----- Glowing contamination front -----
                float rim = exp(-abs(sd) / _RimWidth) * edgeFade;
                float3 rimCol = lerp(_EctoRimColor.rgb, phosphor * 2.0, _HealFlash);
                col += rimCol * rim * (0.6 + impact * 1.4);

                return col;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float time = _Time.y;

                // Remap mesh UVs to 0-1 screen space.
                float2 uv01 = (input.uv - _UVRect.xy) / max(_UVRect.zw - _UVRect.xy, 1e-4);
                uv01 = saturate(uv01);
                if (_FlipV > 0.5) uv01.y = 1.0 - uv01.y;

                float2 p = uv01;
                if (_InvertDirection > 0.5) p.x = 1.0 - p.x;
                // Ectoplasm enters from p.x = 1, healthy side is p.x = 0.

                float impact = saturate(_Impact);

                // Horizontal line tear on impact.
                float tick = floor(time * 24.0);
                float band = floor(p.y * 14.0);
                float tearOn = step(0.72, Hash11(band * 1.73 + tick * 0.37));
                p.x += (Hash11(band + tick) - 0.5) * _GlitchAmount * impact * tearOn;

                // Chromatic aberration on impact: shade three times with horizontal offsets.
                float ca = _ChromaticAmount * impact;
                float3 col;
                if (ca > 1e-4)
                {
                    col.r = ShadeScreen(p + float2(ca, 0.0), time).r;
                    col.g = ShadeScreen(p, time).g;
                    col.b = ShadeScreen(p - float2(ca, 0.0), time).b;
                }
                else
                {
                    col = ShadeScreen(p, time);
                }

                // ----- CRT layer -----
                float scan = 0.5 + 0.5 * sin(uv01.y * _ScanlineCount * 6.2831);
                col *= lerp(1.0, scan, _ScanlineStrength);

                float gridX = 0.5 + 0.5 * sin(uv01.x * _ScanlineCount * _Aspect * 6.2831);
                col *= lerp(1.0, gridX, _PixelGridStrength);

                float roll = frac(uv01.y - time * 0.35);
                col *= 1.0 + _RollBandStrength * smoothstep(0.85, 1.0, roll);

                float flicker = 1.0 + (Hash11(floor(time * 60.0)) - 0.5) * 2.0 * _FlickerStrength * (1.0 + impact * 4.0);
                col *= flicker;

                float2 bezel = min(uv01, 1.0 - uv01) * float2(_Aspect, 1.0);
                float vignette = smoothstep(0.0, _VignetteWidth, min(bezel.x, bezel.y));
                col *= vignette;

                return half4(col * _Brightness, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
