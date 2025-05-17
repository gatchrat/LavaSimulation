Shader "Fluid/ParticleBillboard" {
	Properties {
		
	}
	SubShader {

		Tags {"Queue"="Geometry" }

		Pass {

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.5

			#include "UnityCG.cginc"
			struct LavaPoint
            {
                 float3 Position;
                 float3 Velocity;
                 float4 Color;
				 int active;
				 float age;
            };
			StructuredBuffer<LavaPoint> Points;
			Texture2D<float4> ColourMap;
			SamplerState linear_clamp_sampler;
			float velocityMax;

			float scale;
			float3 colour;

			float4x4 localToWorld;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 colour : TEXCOORD1;
				float3 normal : NORMAL;
				float3 posWorld : TEXCOORD2;
			};

			v2f vert (appdata_full v, uint instanceID : SV_InstanceID)
			{
				v2f o;
				o.uv = v.texcoord;
				o.normal = v.normal;
				
				float3 centreWorld = Points[instanceID].Position;
				float3 objectVertPos = v.vertex * scale * 2;
				float4 viewPos = mul(UNITY_MATRIX_V, float4(centreWorld, 1)) + float4(objectVertPos, 0);
				o.pos = mul(UNITY_MATRIX_P, viewPos);


				float age = Points[instanceID].age;
				float ageT = saturate(velocityMax /age );
				o.colour = ColourMap.SampleLevel(linear_clamp_sampler, float2(ageT, 0.5), 0);
				o.posWorld = centreWorld;

				return o;
			}

			float LinearDepthToUnityDepth(float linearDepth)
			{
				float depth01 = (linearDepth - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y);
				return (1.0 - (depth01 * _ZBufferParams.y)) / (depth01 * _ZBufferParams.x);
			}

			
			float4 frag (v2f i) : SV_Target
			{
				if (length(i.uv-0.5) * 2 > 1) discard;
				
				float shading = saturate(dot(_WorldSpaceLightPos0.xyz, i.normal));
				shading = (shading + 0.6) / 1.4;

				float dstFromCam = length(_WorldSpaceCameraPos - i.posWorld);
				//Depth = Depth = LinearDepthToUnityDepth(dstFromCam);
				return float4(i.colour, -dstFromCam);
			}

			ENDCG
		}
	}
}