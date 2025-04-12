Shader "Custom/HDRP_InstancedParticles"
{
    HLSLINCLUDE
    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch
    #pragma vertex Vert
    #pragma fragment Frag
    #pragma multi_compile_instancing

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    struct LavaPoint
    {
        float3 Position;
        float3 Velocity;
        float4 Color;
    };

    StructuredBuffer<LavaPoint> particleBuffer;

    float _Scale;

    struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    uint instanceID   : SV_InstanceID;
};

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float4 color : COLOR;
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;

        LavaPoint particle = particleBuffer[input.instanceID];
        float3 worldPosition = particle.Position + input.positionOS * _Scale;

        output.positionCS = TransformWorldToHClip(worldPosition);
        output.color = particle.Color;

        return output;
    }

    float4 Frag(Varyings input) : SV_Target
    {
        return input.color;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "ForwardUnlit" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #define SHADERPASS SHADERPASS_FORWARD_UNLIT
            #define USE_UNITY_RENDER_PIPELINE
            ENDHLSL
        }
    }
    FallBack Off
}
