Shader "Hidden/EmergencyRoad/GlobalWetness"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Emergency Global Wetness"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _EmergencyWetness;
            float _EmergencyRainIntensity;
            float _EmergencyPuddleAmount;
            float _EmergencyWetDarkening;
            float _EmergencyPuddleScale;
            float _EmergencyRippleStrength;
            float _EmergencyRippleSize;
            float _EmergencyTrackDistance;

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 blend = frac(value);
                blend = blend * blend * (3.0 - 2.0 * blend);

                float bottom = lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), blend.x);
                float top = lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + 1.0), blend.x);
                return lerp(bottom, top, blend.y);
            }

            float Fbm(float2 value)
            {
                float result = ValueNoise(value) * .58;
                result += ValueNoise(value * 2.07 + 11.7) * .29;
                result += ValueNoise(value * 4.13 - 7.2) * .13;
                return result;
            }

            float Ripple(float2 worldXZ, out float2 direction)
            {
                float2 gridPosition = worldXZ * .42;
                float2 cell = floor(gridPosition);
                float2 local = frac(gridPosition) - .5;
                float2 center = float2(Hash21(cell + 1.71), Hash21(cell + 8.37)) - .5;
                float2 delta = local - center * .62;
                float distanceToDrop = length(delta);
                direction = delta / max(distanceToDrop, .001);

                float speed = lerp(1.1, 1.8, Hash21(cell + 19.3));
                float phase = frac(_Time.y * speed + Hash21(cell + 4.2));
                float rippleSize = clamp(_EmergencyRippleSize, .15, 1.0);
                float radius = phase * .52 * rippleSize;
                float ring = 1.0 - smoothstep(.012, lerp(.026, .055, rippleSize), abs(distanceToDrop - radius));
                float life = smoothstep(1.0, .72, phase) * smoothstep(0.0, .08, phase);
                return ring * life;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord.xy;
                half4 sceneColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);

                if (_EmergencyWetness <= .0001)
                    return sceneColor;

                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (rawDepth <= .00001) return sceneColor;
                #else
                    if (rawDepth >= .99999) return sceneColor;
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 normalWS = normalize(SampleSceneNormals(uv));
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - positionWS);

                float upward = pow(saturate(normalWS.y), 5.0);
                float groundMask = 1.0 - smoothstep(.55, 1.35, positionWS.y);
                float2 trackAnchoredXZ = positionWS.xz + float2(0.0, _EmergencyTrackDistance);
                float noise = Fbm(trackAnchoredXZ * max(.02, _EmergencyPuddleScale));
                float puddleThreshold = lerp(.88, .38, saturate(_EmergencyPuddleAmount * _EmergencyWetness));
                float puddle = smoothstep(puddleThreshold, puddleThreshold + .13, noise)
                    * upward
                    * groundMask
                    * _EmergencyWetness;

                float generalWet = _EmergencyWetness * lerp(.48, 1.0, upward);
                float3 darkened = sceneColor.rgb * (1.0 - _EmergencyWetDarkening * generalWet);

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirection)), 3.0);
                float skySheen = (fresnel * .18 + upward * .035) * generalWet;
                float3 wetColor = darkened + float3(.48, .58, .68) * skySheen;

                float2 rippleDirection;
                float ripple = Ripple(trackAnchoredXZ, rippleDirection)
                    * puddle
                    * _EmergencyRainIntensity
                    * _EmergencyRippleStrength;

                float2 distortedUv = uv + rippleDirection * ripple * _BlitTexture_TexelSize.xy * 1.7;
                half3 distortedColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, distortedUv, 0).rgb;
                float3 puddleColor = lerp(wetColor, distortedColor * .88 + float3(.13, .17, .21), .22);
                wetColor = lerp(wetColor, puddleColor, puddle);
                wetColor += ripple * float3(.19, .24, .28);

                return half4(wetColor, sceneColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
