﻿Shader "Hidden/Toon RP/Fake Additional Lights"
{
	Properties
	{
    }
	SubShader
	{
	    ColorMask RGB
	    Cull Off
    
        Blend One One 
        
        HLSLINCLUDE

        //#pragma enable_d3d11_debug_symbols
		
        #include "../../ShaderLibrary/Common.hlsl"
        #include "../../ShaderLibrary/FakeAdditionalLights.hlsl"
        #include "../../ShaderLibrary/Lighting.hlsl"

        struct PackedLightData
        {
            half4 params1;
            half4 params2;
        };

        #define BATCH_SIZE 256

        CBUFFER_START(_ToonRP_FakeAdditionalLights_PackedData)
        float4 _FakeAdditionalLights[BATCH_SIZE];
        CBUFFER_END

        half4 ScreenUvToHClip(const half2 screenUv)
		{
		    half4 positionCs = float4(screenUv * 2.0f - 1.0f, 0.5f, 1.0f);

		    #ifdef UNITY_UV_STARTS_AT_TOP
		    positionCs.y *= -1;
		    #endif // UNITY_UV_STARTS_AT_TOP

		    return positionCs;
		}

        uint4 UnpackBytes(const uint packedValue)
		{
		    return uint4(
		        packedValue & 0xFF,
		        packedValue >> 8 & 0xFF,
		        packedValue >> 16 & 0xFF,
		        packedValue >> 24 & 0xFF
		    );
		}

        half2 UnpackHalf2(const uint packedValue)
		{
		    return half2(f16tof32(packedValue), f16tof32(packedValue >> 16));
		}

        half4 UnpackHalf4(const uint2 packedValue)
		{
		    return half4(UnpackHalf2(packedValue.x), UnpackHalf2(packedValue.y));
		}

        struct InverpolatedParams
        {
            half2 positionWsXz;
            half3 color;
            half invSqrRange;
            half3 center;
        };

        void GetVertexData(const uint vertexId, out float4 positionCs, out InverpolatedParams inverpolatedParams)
		{
		    const uint quadVertexId = vertexId % 4;
            const uint instanceId = vertexId / 4;

		    half2 positionOs;
		    positionOs.x = quadVertexId % 3 == 0 ? -1 : 1;
		    positionOs.y = quadVertexId < 2 ? 1 : -1;

		    const float4 rawPackedData = _FakeAdditionalLights[instanceId];
            PackedLightData packedLightData;
            packedLightData.params1 = UnpackHalf4(asuint(rawPackedData.xy));
            packedLightData.params2 = UnpackHalf4(asuint(rawPackedData.zw));

            const half3 center = packedLightData.params1.xyz;
            const half range = packedLightData.params1.w;
            const half3 color = packedLightData.params2.xyz;
            const half invSqrRange = packedLightData.params2.w;

            const half2 positionWs = positionOs * range + center.xz;
		    const half2 screenUv = FakeAdditionalLights_PositionToUV(positionWs);
		    positionCs = ScreenUvToHClip(screenUv);

            inverpolatedParams.positionWsXz = positionWs;
            inverpolatedParams.center = center;
            inverpolatedParams.color = color;
            inverpolatedParams.invSqrRange = invSqrRange;
        }

        float2 Rotate(const float2 value, const float angleRad)
		{
            const float2x2 rotationMatrix = float2x2(cos(angleRad), -sin(angleRad), sin(angleRad), cos(angleRad));
		    return mul(rotationMatrix, value);
		}
        
        ENDHLSL
            
        Pass
		{
		    Name "Fake Additional Light"
		    
			HLSLPROGRAM

            #pragma vertex VS
		    #pragma fragment PS

            struct v2f
		    {
                half4 positionCs : SV_POSITION;
            
                half2 positionWsXz : POSITION_WS;
                half3 color : COLOR;
                half invSqrRange : INV_SQR_RANGE;
                half3 center : CENTER_XZ;
            };

            v2f VS(const uint vertexId : SV_VertexID)
            {
                v2f OUT;
                
                InverpolatedParams inverpolatedParams;
                GetVertexData(vertexId, OUT.positionCs, inverpolatedParams);

                OUT.positionWsXz = inverpolatedParams.positionWsXz;
                OUT.color = inverpolatedParams.color;
                OUT.invSqrRange = inverpolatedParams.invSqrRange;
                OUT.center = inverpolatedParams.center;
                
                return OUT;
            }

			half4 PS(const v2f IN) : SV_TARGET
            {
                half3 receiverPosition;
                receiverPosition.xz = IN.positionWsXz;
                receiverPosition.y = _ReceiverPlaneY;
                
                const half3 offset = IN.center - receiverPosition;
                const half distanceSqr = max(dot(offset, offset), 0.00001);
                half distanceAttenuation = Sq(
                    saturate(1.0f - Sq(distanceSqr * IN.invSqrRange))
                );
                distanceAttenuation = distanceAttenuation / distanceSqr;
                distanceAttenuation = distanceAttenuation * _AdditionalLightRampOffset.z;

                const half3 color = IN.color * distanceAttenuation;
                return half4(color, 1.0f);
            }

			ENDHLSL
		}
	}
}