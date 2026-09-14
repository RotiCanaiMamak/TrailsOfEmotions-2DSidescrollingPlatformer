Shader "UI/FYP/RainLensRawImage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _RainIntensity ("Intensity", Range(0, 1)) = 0.75
        _RainDistortion ("Distortion", Range(0, 2)) = 0.45
        _RainBlur ("Blur", Range(0, 1)) = 0.35
        _DropletDensity ("Droplet Density", Range(0, 4)) = 1
        _StreakDensity ("Streak Density", Range(0, 4)) = 0.8
        _FallSpeed ("Fall Speed", Range(0, 4)) = 1.25
        _WaterVisibility ("Water Visibility", Range(0, 1)) = 0.22
        _DropSlide ("Drop Slide", Range(0, 1)) = 0.45
        _DropStickiness ("Drop Stickiness", Range(0, 1)) = 0.35
        _StreakLength ("Streak Length", Range(0, 1)) = 0.55
        _EdgeFade ("Edge Fade", Range(0, 1)) = 0.18
        _Vignette ("Vignette", Range(0, 1)) = 0.2

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Rain Lens RawImage"

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;

            float _RainIntensity;
            float _RainDistortion;
            float _RainBlur;
            float _DropletDensity;
            float _StreakDensity;
            float _FallSpeed;
            float _WaterVisibility;
            float _DropSlide;
            float _DropStickiness;
            float _StreakLength;
            float _EdgeFade;
            float _Vignette;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f Vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float TextureAspect()
            {
                return _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);
            }

            float SpawnFade(float life, float fadeIn, float fadeOutStart)
            {
                float fadeInMask = smoothstep(0.0, fadeIn, life);
                float fadeOutMask = 1.0 - smoothstep(fadeOutStart, 1.0, life);
                return fadeInMask * fadeOutMask;
            }

            float EllipseDistance(float2 p, float2 radius)
            {
                return length(float2(p.x / max(radius.x, 0.0001), p.y / max(radius.y, 0.0001)));
            }

            float SoftEllipse(float2 p, float2 radius, float innerEdge)
            {
                return smoothstep(1.0, innerEdge, EllipseDistance(p, radius));
            }

            float HoldWindow(float life, float holdStart, float duration)
            {
                float holdIn = smoothstep(holdStart - 0.035, holdStart, life);
                float holdOut = 1.0 - smoothstep(holdStart + duration, holdStart + duration + 0.045, life);
                return holdIn * holdOut;
            }

            float StickSlipProgress(float life, float seed, float stickiness, out float motionAmount)
            {
                float firstStart = lerp(0.18, 0.36, Hash12(float2(seed, 2.7)));
                float secondStart = lerp(0.48, 0.68, Hash12(float2(seed, 6.1)));
                float firstDuration = lerp(0.035, 0.095, Hash12(float2(seed, 9.4)));
                float secondDuration = lerp(0.025, 0.08, Hash12(float2(seed, 13.8)));

                float firstHold = HoldWindow(life, firstStart, firstDuration);
                float secondHold = HoldWindow(life, secondStart, secondDuration);
                float holdAmount = saturate(max(firstHold, secondHold * 0.85) * stickiness);
                float delayed = firstHold * firstDuration * 0.48 + secondHold * secondDuration * 0.42;

                motionAmount = saturate(1.0 - holdAmount * 0.9);
                return saturate(lerp(life, smoothstep(0.0, 1.0, life), 0.18) - delayed * stickiness);
            }

            float DropletLayer(float2 uv, float scale, float speed, out float2 normalOffset)
            {
                uv.x *= TextureAspect();

                float2 cellUv = uv * scale;
                float2 cell = floor(cellUv);
                float2 local = frac(cellUv) - 0.5;

                float cycleTime = _Time.y * speed + Hash12(float2(cell.x, 31.7));
                float cycle = floor(cycleTime);
                float life = frac(cycleTime);
                float baseSeed = Hash12(cell);
                float runnerSeed = Hash12(float2(cell.x, cycle * 13.7));
                float runnerSizeSeed = Hash12(float2(cell.x, cycle * 5.9 + 9.4));
                float density = saturate(_DropletDensity * 0.25);
                float beadDensity = saturate(density * 1.18);
                float runnerDensity = saturate(density * 0.82);

                float beadPresence = step(Hash12(cell + 5.3), beadDensity);
                float extraBeadPresence = step(Hash12(cell + 43.8), saturate(beadDensity * 0.72));
                float runnerPresence = step(Hash12(float2(cell.x, cycle * 7.1 + 21.6)), runnerDensity);
                float runnerAlive = smoothstep(0.0, 0.08, life) * runnerPresence;

                float2 beadCenter = float2(Hash12(cell + 11.2) - 0.5, Hash12(cell + 17.2) - 0.5) * 0.62;
                beadCenter.y += sin(_Time.y * (0.17 + baseSeed * 0.08) + baseSeed * 6.2831) * 0.018;

                float beadRadius = lerp(0.045, 0.095, Hash12(cell + 23.4));
                float2 beadDelta = local - beadCenter;
                beadDelta.x *= 1.08;
                float beadBody = SoftEllipse(beadDelta, float2(beadRadius * 1.08, beadRadius), 0.42);
                float beadRim = saturate(smoothstep(1.16, 0.78, EllipseDistance(beadDelta, float2(beadRadius * 1.18, beadRadius * 1.08))) - beadBody);
                float beadShimmer = 0.78 + 0.22 * sin(_Time.y * (0.45 + baseSeed * 0.2) + baseSeed * 12.0);

                float2 extraBeadCenter = float2(Hash12(cell + 61.4) - 0.5, Hash12(cell + 72.9) - 0.5) * 0.7;
                extraBeadCenter += float2(
                    sin(_Time.y * (0.11 + baseSeed * 0.06) + Hash12(cell + 84.2) * 6.2831),
                    cos(_Time.y * (0.1 + baseSeed * 0.05) + Hash12(cell + 91.5) * 6.2831)
                ) * 0.012;
                float extraBeadRadius = lerp(0.028, 0.065, Hash12(cell + 101.6));
                float2 extraBeadDelta = local - extraBeadCenter;
                extraBeadDelta.x *= 1.12;
                float extraBeadBody = SoftEllipse(extraBeadDelta, float2(extraBeadRadius * 1.1, extraBeadRadius), 0.46);
                float extraBeadRim = saturate(smoothstep(1.18, 0.82, EllipseDistance(extraBeadDelta, float2(extraBeadRadius * 1.22, extraBeadRadius * 1.08))) - extraBeadBody);
                float extraBeadMask = saturate(extraBeadBody + extraBeadRim * 0.45) * extraBeadPresence * (0.58 + density * 0.22);

                float microBeadPresence = step(Hash12(cell + 119.3), saturate((density - 0.35) * 0.85));
                float2 microBeadCenter = float2(Hash12(cell + 127.7) - 0.5, Hash12(cell + 139.2) - 0.5) * 0.76;
                float microBeadRadius = lerp(0.018, 0.04, Hash12(cell + 151.8));
                float2 microBeadDelta = local - microBeadCenter;
                microBeadDelta.x *= 1.12;
                float microBeadBody = SoftEllipse(microBeadDelta, float2(microBeadRadius * 1.12, microBeadRadius), 0.5);
                float microBeadRim = saturate(smoothstep(1.2, 0.84, EllipseDistance(microBeadDelta, float2(microBeadRadius * 1.24, microBeadRadius * 1.1))) - microBeadBody);
                float microBeadMask = saturate(microBeadBody + microBeadRim * 0.4) * microBeadPresence * 0.42;

                float baseRunnerRadius = lerp(0.065, 0.14, runnerSizeSeed);
                float sizeSpeed = lerp(0.96, 1.28, runnerSizeSeed);
                float sizeStickiness = saturate(_DropStickiness * lerp(1.35, 0.45, runnerSizeSeed) + (1.0 - runnerSizeSeed) * 0.16);
                float motionAmount;
                float slideLife = StickSlipProgress(life, runnerSeed, sizeStickiness, motionAmount);

                float startY = 1.18 + runnerSeed * 0.08;
                float baseTravel = lerp(1.72, 2.08, saturate(_DropSlide)) * sizeSpeed;
                float beadScreenY = (cell.y + 0.5 + beadCenter.y) / scale;
                float mergeLife = saturate((startY - beadScreenY) / max(baseTravel, 0.0001));
                float mergeContact = smoothstep(mergeLife - 0.08, mergeLife + 0.02, slideLife) * beadPresence * runnerPresence;
                float mergeAbsorb = smoothstep(mergeLife + 0.01, mergeLife + 0.18, slideLife) * mergeContact;
                float mergeRelease = smoothstep(mergeLife + 0.14, mergeLife + 0.34, slideLife) * mergeContact;
                float mergeBulge = mergeContact * (1.0 - mergeAbsorb);
                float mergeBoost = saturate((slideLife - mergeLife) * lerp(2.0, 3.1, runnerSizeSeed)) * mergeRelease;

                motionAmount = saturate(motionAmount + mergeRelease * 0.42 + runnerSizeSeed * 0.12);
                float runnerX = lerp((runnerSeed - 0.5) * 0.56, beadCenter.x, saturate(mergeContact * 0.5 + mergeAbsorb * 0.55));
                float runnerScreenY = startY - slideLife * baseTravel - mergeBoost * lerp(0.08, 0.28, saturate(_DropSlide));
                float runnerY = runnerScreenY * scale - cell.y - 0.5;
                runnerY += sin(_Time.y * (0.55 + runnerSeed * 0.4) + runnerSeed * 6.2831) * 0.018 * (1.0 - mergeContact);

                float stretchAmount = saturate((1.0 - motionAmount) * -0.25 + motionAmount * lerp(0.28, 0.9, runnerSizeSeed) + mergeRelease * 0.42);
                float pauseRoundness = saturate((1.0 - motionAmount) * sizeStickiness);
                float runnerRadius = baseRunnerRadius + beadRadius * (mergeAbsorb * 0.95 + mergeBulge * 0.55);
                float bottomAlive = smoothstep(-0.32, -0.06, runnerScreenY + runnerRadius / scale * 2.4);
                float2 runnerCenter = float2(runnerX, runnerY);
                float2 runnerDelta = local - runnerCenter;
                runnerDelta.x *= lerp(1.08, 0.82, stretchAmount);

                float runnerWidth = runnerRadius * lerp(1.0, 0.72, stretchAmount) * lerp(1.0, 1.12, saturate(pauseRoundness + mergeBulge));
                float runnerHeight = runnerRadius * lerp(1.02, 1.85, stretchAmount) * lerp(1.0, 0.9, pauseRoundness);
                float runnerBody = SoftEllipse(runnerDelta, float2(runnerWidth, runnerHeight), 0.4);
                float runnerRim = saturate(smoothstep(1.14, 0.78, EllipseDistance(runnerDelta, float2(runnerWidth * 1.08, runnerHeight * 1.06))) - runnerBody);
                float tailLength = lerp(0.5, 2.45, stretchAmount) * lerp(0.65, 1.25, mergeRelease);
                float2 tailCenter = runnerCenter + float2(0.0, -runnerRadius * tailLength);
                float tailMask = SoftEllipse(local - tailCenter, float2(runnerRadius * lerp(0.22, 0.38, pauseRoundness), runnerRadius * lerp(0.7, 2.25, stretchAmount)), 0.6) * saturate(stretchAmount + mergeRelease * 0.35);

                float2 bridgeCenter = lerp(beadCenter, runnerCenter, 0.5);
                float bridgeHeight = abs(runnerCenter.y - beadCenter.y) * 0.5 + max(beadRadius, runnerRadius) * 0.55;
                float bridgeMask = SoftEllipse(local - bridgeCenter, float2(max(beadRadius, runnerRadius) * lerp(0.36, 0.58, mergeBulge), bridgeHeight), 0.62) * mergeContact * (1.0 - mergeRelease * 0.7);

                float beadAbsorbFade = saturate(1.0 - mergeAbsorb * 0.86 - mergeRelease * 0.28);
                float beadMask = saturate(beadBody + beadRim * 0.5) * beadPresence * beadShimmer * beadAbsorbFade;
                beadMask = saturate(beadMask + extraBeadMask + microBeadMask);
                float runnerMask = saturate(runnerBody + runnerRim * 0.55 + tailMask * 0.48 + bridgeMask * 0.62) * runnerAlive * bottomAlive;
                float mask = saturate(beadMask + runnerMask * (1.0 + mergeAbsorb * 0.22 + mergeRelease * 0.38));

                float2 beadNormal = normalize(beadDelta + 0.0001) * (beadBody + beadRim * 0.5) * beadPresence * 0.55 * beadAbsorbFade;
                beadNormal += normalize(extraBeadDelta + 0.0001) * (extraBeadBody + extraBeadRim * 0.45) * extraBeadPresence * 0.34;
                beadNormal += normalize(microBeadDelta + 0.0001) * (microBeadBody + microBeadRim * 0.4) * microBeadPresence * 0.22;
                float2 runnerNormal = normalize(runnerDelta + 0.0001) * (runnerBody + runnerRim * 0.55 + tailMask * 0.35 + bridgeMask * 0.4) * runnerAlive * bottomAlive;
                normalOffset = beadNormal + runnerNormal * (1.0 + mergeAbsorb * 0.5 + mergeRelease * 0.85);
                normalOffset.y -= (runnerBody + tailMask) * runnerAlive * bottomAlive * (0.2 + stretchAmount * 0.24 + mergeRelease * 0.38);

                return mask;
            }

            float StreakLayer(float2 uv, float scale, float speed, out float2 normalOffset)
            {
                uv.x *= TextureAspect();

                float xCell = floor(uv.x * scale);
                float xLocal = frac(uv.x * scale);
                float cycleTime = _Time.y * speed + Hash12(float2(xCell, 47.1));
                float cycle = floor(cycleTime);
                float life = frac(cycleTime);
                float seed = Hash12(float2(xCell, cycle + 3.9));
                float density = saturate(_StreakDensity * 0.25) * 0.9;
                float densityMask = step(Hash12(float2(xCell, cycle + 19.4)), density);
                float alive = smoothstep(0.0, 0.08, life) * densityMask;
                float head = life * 1.35 - 0.16;
                float tail = head + lerp(0.18, 0.75, saturate(_StreakLength));
                float y = 1.0 - uv.y;

                float xCenter = 0.5 + (seed - 0.5) * 0.5;
                float width = lerp(0.025, 0.065, Hash12(float2(xCell, cycle + 11.1)));
                float streakLine = smoothstep(width, width * 0.18, abs(xLocal - xCenter));
                float segment = smoothstep(head, head + 0.05, y) * (1.0 - smoothstep(tail - 0.08, tail, y));
                float broken = smoothstep(0.08, 0.92, Hash12(float2(xCell, floor(y * 18.0) + seed * 23.0)));

                float mask = streakLine * segment * broken * alive;
                normalOffset = float2((xLocal - xCenter) * 0.55, -0.9) * mask;
                return mask;
            }

            fixed4 Frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;

                float2 dropNormalA;
                float2 dropNormalB;
                float2 streakNormal;

                float dropsA = DropletLayer(uv + float2(0.03, 0.0), 7.5, _FallSpeed * 0.18, dropNormalA);
                float dropsB = DropletLayer(uv + float2(0.41, 0.29), 13.0, _FallSpeed * 0.28, dropNormalB);
                float streaks = StreakLayer(uv + float2(0.17, 0.0), 9.0, _FallSpeed * 0.24, streakNormal);

                float lensMask = saturate(dropsA + dropsB * 0.75 + streaks);
                float2 normalOffset = dropNormalA * dropsA + dropNormalB * dropsB * 0.7 + streakNormal;

                float edgeSize = max(_EdgeFade, 0.0001);
                float edgeMask = smoothstep(0.0, edgeSize, uv.x)
                    * smoothstep(0.0, edgeSize, 1.0 - uv.x)
                    * smoothstep(0.0, edgeSize, 1.0 - uv.y);
                lensMask *= edgeMask * saturate(_RainIntensity);

                float distortionMask = saturate(lensMask * 1.35);
                float visibleMask = lensMask * saturate(_WaterVisibility);

                float2 distortion = normalOffset * (_RainDistortion * 0.028) * distortionMask;
                float2 sampleUv = saturate(uv + distortion);

                fixed4 sharp = tex2D(_MainTex, sampleUv);

                float2 texel = _MainTex_TexelSize.xy * lerp(0.5, 3.0, saturate(_RainBlur)) * distortionMask;
                fixed4 blurred = sharp;
                blurred += tex2D(_MainTex, saturate(sampleUv + texel * float2(1.5, 0.5)));
                blurred += tex2D(_MainTex, saturate(sampleUv + texel * float2(-1.0, 1.0)));
                blurred += tex2D(_MainTex, saturate(sampleUv + texel * float2(0.5, -1.5)));
                blurred *= 0.25;

                float highlight = saturate(pow(lensMask, 1.8) * 0.22 + dropsA * 0.08 + streaks * 0.05) * saturate(_WaterVisibility);
                fixed3 color = lerp(sharp.rgb, blurred.rgb, visibleMask * saturate(_RainBlur) * 0.55);
                color += highlight.xxx;

                float2 centeredUv = uv * 2.0 - 1.0;
                float vignette = saturate(dot(centeredUv, centeredUv) * _Vignette);
                color *= 1.0 - vignette * 0.35 * saturate(_RainIntensity);

                fixed4 output = fixed4(color * input.color.rgb, sharp.a * input.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                output.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(output.a - 0.001);
                #endif

                return output;
            }
            ENDCG
        }
    }
}
