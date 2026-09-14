Shader "UI/Wobble Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Radius ("Radius", Range(0, 1)) = 0.5
        _Softness ("Edge Softness", Range(0.001, 0.2)) = 0.02
        _Strength ("Wobble Strength", Range(0, 0.2)) = 0.03
        _Speed ("Wobble Speed", Float) = 3
        _Frequency ("Wobble Frequency", Float) = 8

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
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
            float4 _ClipRect;
            float _Radius;
            float _Softness;
            float _Strength;
            float _Speed;
            float _Frequency;

            v2f vert(appdata_t v)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = v.vertex;
                output.vertex = UnityObjectToClipPos(v.vertex);
                output.texcoord = v.texcoord;
                output.color = v.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 centeredUv = input.texcoord * 2 - 1;
                float angle = atan2(centeredUv.y, centeredUv.x);
                float distanceFromCenter = length(centeredUv);

                float waveA = sin(angle * _Frequency + _Time.y * _Speed);
                float waveB = sin(angle * (_Frequency * 1.7) - _Time.y * (_Speed * 0.65));
                float wobble = (waveA + waveB * 0.5) * _Strength;
                float edge = saturate(_Radius + wobble);
                float alpha = 1 - smoothstep(edge - _Softness, edge + _Softness, distanceFromCenter);

                fixed4 color = tex2D(_MainTex, input.texcoord) * input.color;
                color.a *= alpha;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }
}
