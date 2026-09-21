Shader "XTap/UI/JellyTouch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _TouchUV ("Touch UV", Vector) = (0.5,0.5,0,0)
        _TouchStrength ("Touch Strength", Range(-0.35,0.35)) = 0
        _TouchRadius ("Touch Radius", Range(0.02,0.5)) = 0.18
        _TouchDirection ("Touch Direction", Vector) = (0,0,0,0)
        _SwipeStrength ("Swipe Strength", Range(-0.2,0.2)) = 0
        _Aspect ("Image Aspect", Float) = 1

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
            Name "JellyUI"

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
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float4 _TouchUV;
            float _TouchStrength;
            float _TouchRadius;
            float4 _TouchDirection;
            float _SwipeStrength;
            float _Aspect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 delta = uv - _TouchUV.xy;

                // Correct the influence circle for portrait/landscape image aspect.
                float2 metric = float2(delta.x * max(_Aspect, 0.001), delta.y);
                float dist = length(metric);
                float radius = max(_TouchRadius, 0.001);

                float falloff = saturate(1.0 - dist / radius);
                falloff = falloff * falloff * (3.0 - 2.0 * falloff);

                // Positive strength pinches inward; negative strength rebounds outward.
                float radial = _TouchStrength * falloff;
                uv += delta * radial;

                // Swipe gives a tiny elastic drag in the gesture direction.
                float2 dir = _TouchDirection.xy;
                uv += dir * (_SwipeStrength * falloff);

                // Secondary ripple makes the return feel springy instead of rubber-stamp flat.
                float ripple = sin(saturate(dist / radius) * 3.14159265) * falloff;
                uv += delta * (_TouchStrength * ripple * 0.16);

                uv = saturate(uv);

                fixed4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
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
