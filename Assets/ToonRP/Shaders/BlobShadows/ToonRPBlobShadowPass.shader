﻿Shader "Hidden/Toon RP/Blob Shadow Pass"
{
	Properties
	{
	}
	SubShader
	{
        Pass
		{
		    Name "Shadow Caster"
		    
		    // BlobBlendingMode.MetaBalls => (BlendMode.SrcColor, BlendMode.One, BlendOp.Add),
            // BlobBlendingMode.Voronoi => (BlendMode.One, BlendMode.One, BlendOp.Max),
		    Blend SrcColor One
		    BlendOp Add
		    
			HLSLPROGRAM

			#pragma enable_d3d11_debug_symbols
			
			#pragma multi_compile_instancing

	        #pragma vertex VS
		    #pragma fragment PS

            #include "../../ShaderLibrary/Common.hlsl"

            struct appdata
            {
                float3 position : POSITION;
			    UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
		    {
                float4 positionCs : SV_POSITION;
                float2 centeredUV : TEXCOORD0;
            };

            v2f VS(const appdata IN)
            {
                v2f OUT;

                UNITY_SETUP_INSTANCE_ID(IN);

                OUT.centeredUV = IN.position.xy * 2.0f;
                OUT.positionCs = float4(TransformObjectToWorld(IN.position), 1);
                return OUT;
            }

			float4 PS(const v2f IN) : SV_TARGET
            {
                return 1.0f - length(IN.centeredUV); 
            }

			ENDHLSL
		}
	}
}