Shader "Custom/FogOfWarFullScreen"
{
    Properties
    {
        _FogTexture("Fog of War", 2D) = "black" {}
        _WorldSize("World Size", Vector) = (100, 100, 0, 0)
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "FogOfWarPass"
            ZTest Always
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            TEXTURE2D(_FogTexture);
            SAMPLER(sampler_FogTexture);
            float4 _WorldSize;
            float4 _CameraBounds;
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample the screen
                half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                
                // Convert screen UV to world position
                float2 worldPos;
                worldPos.x = _CameraBounds.x + input.texcoord.x * _CameraBounds.z;
                worldPos.y = _CameraBounds.y + input.texcoord.y * _CameraBounds.w;
                
                // Convert world position to fog texture UV
                float2 fogUV = worldPos / _WorldSize.xy;
                
                // Sample fog texture (0=unexplored, 0.3=explored, 1=visible)
                half fogValue = SAMPLE_TEXTURE2D(_FogTexture, sampler_FogTexture, fogUV).r;
                
                // Simply multiply by fog value
                color.rgb *= fogValue;
                
                return color;
            }
            ENDHLSL
        }
    }
}