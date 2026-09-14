Shader "UI/Random Wave Reveal Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _WaveAmplitude ("Wave Amplitude", Range(0, 0.5)) = 0.08
        _WaveFrequency ("Wave Frequency", Range(0, 20)) = 5
        _WaveSpeed ("Wave Speed", Range(-10, 10)) = 2
        _RandomStrength ("Random Strength", Range(0, 1)) = 0.45
        _RandomScale ("Random Scale", Range(1, 40)) = 12
        _RandomSpeed ("Random Speed", Range(-10, 10)) = 1.5
        _RandomSeed ("Random Seed", Float) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.2)) = 0.025

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
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

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float _RandomStrength;
            float _RandomScale;
            float _RandomSpeed;
            float _RandomSeed;
            float _EdgeSoftness;

            float Hash(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            float ValueNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(Hash(i + _RandomSeed), Hash(i + 1.0 + _RandomSeed), u);
            }

            float FractalNoise(float x)
            {
                float n = ValueNoise(x);
                n += ValueNoise(x * 2.07 + 19.17) * 0.5;
                n += ValueNoise(x * 4.13 + 73.41) * 0.25;
                return n / 1.75;
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                float time = _Time.y;
                float wave = sin((IN.texcoord.y * _WaveFrequency + time * _WaveSpeed + _RandomSeed) * 6.2831853);
                float noise = FractalNoise(IN.texcoord.y * _RandomScale + time * _RandomSpeed);
                noise = noise * 2.0 - 1.0;

                float irregularWave = lerp(wave, noise, _RandomStrength);
                float edge = saturate(_WaveAmplitude + irregularWave * _WaveAmplitude);
                float mask = smoothstep(edge - _EdgeSoftness, edge + _EdgeSoftness, IN.texcoord.x);

                color.a *= mask;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
