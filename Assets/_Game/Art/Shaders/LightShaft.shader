// Луч света: аддитивные плоскости с цветом в вершинах. Плоскость, видимая ребром, гаснет (инвертированный
// Френель по нормали грани), а у пола и стен луч мягко растворяется (soft-пересечение по глубине сцены) —
// нет резких «лезвий» и чёткого конуса.
Shader "FalseParadise/LightShaft"
{
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (0.72, 0.84, 1, 1)
        _Intensity ("Intensity", Float) = 0.3
        _EdgePower ("Edge fade power", Float) = 1.6
        _SoftDistance ("Soft intersection, m", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Shaft"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _EdgePower;
                half _SoftDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Нормаль грани из производных: плоскость ребром к камере → 0
                float3 n = normalize(cross(ddy(i.positionWS), ddx(i.positionWS)));
                float3 v = normalize(GetCameraPositionWS() - i.positionWS);
                half facing = pow(saturate(abs(dot(n, v))), _EdgePower);

                // Мягкое пересечение с геометрией
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float fragZ = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                half soft = saturate((sceneZ - fragZ) / max(0.01, _SoftDistance));

                half a = i.color.a * facing * soft * _BaseColor.a;
                return half4(_BaseColor.rgb * i.color.rgb * _Intensity, a);
            }
            ENDHLSL
        }
    }
}
