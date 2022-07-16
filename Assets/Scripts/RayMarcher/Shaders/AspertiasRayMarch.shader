Shader "Unlit/AsperitasRaymarch"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
		SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			#define MAX_STEPS 100
			#define MAX_DIST 15000
			#define SURF_DIST 1e-5

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float3 ro : TEXCOORD1;
				float3 rd :TEXCOORD2;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			sampler2D _HeightMap;
			float4 _HeightMap_ST;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _HeightMap);


				// o.ro = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.));
				// o.hitPos = v.vertex;

				o.ro = _WorldSpaceCameraPos;
				// o.hitPos = mul(unity_ObjectToWorld, v.vertex);
				float3 rd = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
				o.rd = mul(unity_CameraToWorld, float4(rd, 0));
				return o;
			}

			float remap(float value, float ol, float oh, float nl, float nh) {
				return nl + (value - ol) * (nh - nl) / (oh - ol);
			}

			// raymarch through density volume control params
			int raymarchStepCount;
			float raymarchStepSize;
			Texture2D<float4> BlueNoise;
			SamplerState samplerBlueNoise;

			// base noise params
			float time;
			float baseNoiseScale;
			float3 baseNoiseOffset;
			float densityMultiplier;

			// detail noise params
			float detailNoiseScale;
			float3 detailNoiseOffset;

			float globalCoverage;


			float4 lightColor;
			float darknessThreshold;
			float lightAbsorptionThroughCloud;
			float lightAbsorptionTowardSun;
			float4 phaseParams;

			Texture3D<float4> BaseNoise;
			SamplerState samplerBaseNoise;

			Texture3D<float4> DetailNoise;
			SamplerState samplerDetailNoise;

			Texture2D<float4> WeatherMap;
			SamplerState samplerWeatherMap;

			Texture2D<float4> HeightMap;
			SamplerState samplerHeightMap;


			float sampleDensity(float3 rayPosition) {


				//float3 boxSize = abs(boundsMax - boundsMin);
				//float3 boxCentre = (boundsMax + boundsMin) / 2;
				//float3 heightPercent = saturate(abs(rayPosition.y - boundsMin.y) / boxSize.y);


				float3 baseShapeSamplePosition = rayPosition * baseNoiseScale * 0.0001 +
					baseNoiseOffset * 0.0001;

				//// wind settings
				//float3 windDirection = float3(1.0, 0.0, 0.0);
				//float cloudSpeed = 10.0;
				//// cloud_top offset - push the tops of the clouds along this wind direction by this many units.
				//float cloudTopOffset = .5;

				//baseShapeSamplePosition += heightPercent * windDirection * cloudTopOffset;
				//baseShapeSamplePosition += (windDirection + float3(0., 1., 0.)) * time * 0.001 * cloudSpeed;

				float4 baseNoiseValue = BaseNoise.SampleLevel(samplerBaseNoise, baseShapeSamplePosition, 0);


				// weather map is 10km x 10km, assume that each unit is 1km
				float2 wmSamplePosition = rayPosition.xz * 0.00005;
				float4 weatherMapSample = WeatherMap.SampleLevel(samplerWeatherMap, wmSamplePosition, 0);


				// create fbm from the base noise
				float lowFreqFBM = (baseNoiseValue.r * 0.625) + (baseNoiseValue.g * 0.25) + (baseNoiseValue.b * 0.125);
				// get the base cloud shape with the base noise
				float baseCloud = remap(baseNoiseValue.a, -(1.0 - lowFreqFBM), 1.0, 0.0, 1.0);

				//float SA = shapeAltering(heightPercent, weatherMapSample);
				//float DA = densityAltering(heightPercent, weatherMapSample);
				 //baseCloud *= heightGradient;

				float coverage = weatherMapSample.r;
				float baseCloudWithCoverage = saturate(remap(baseCloud, 1 - globalCoverage * coverage, 1.0, 0.0, 1.0));

				baseCloudWithCoverage *= coverage;

				// float density = shape.a;
				float finalCloud = baseCloudWithCoverage;

				//// add detailed noise
				//if (baseCloudWithCoverage > 0) {
				//	float3 detailNoiseSamplePos = rayPosition * detailNoiseScale * 0.001 + detailNoiseOffset;
				//	float3 detailNoise = DetailNoise.SampleLevel(samplerDetailNoise, detailNoiseSamplePos, 0).rgb;

				//	float highFreqFbm = (detailNoise.r * 0.625) + (detailNoise.g * 0.25) + (detailNoise.b * 0.125);

				//	float detailNoiseModifier = 0.35 * exp(-globalCoverage * 0.75) * lerp(highFreqFbm, 1. - highFreqFbm, saturate(heightPercent * 5.0));

				//	finalCloud = saturate(remap(baseCloudWithCoverage, detailNoiseModifier, 1.0, 0.0, 1.0));

				//}

				return finalCloud;
			}



			// gets the uv from the position in space, normalise by factor d
			float2 GetUV(float3 p) {
				int d = 100;
				float u = (p.x + d / 2) / d;
				float v = (p.z + d / 2) / d;
				return float2(u, v);
			}

			float GetHeight(float3 p) {
				float4 color = tex2Dlod(_HeightMap, float4(GetUV(p), 0, 0) * 0.01);
				return color.r * 150;
			}

			float sdSphere(float3 p, float s) {
				return length(p) - s;
			}

			float GetDist(float3 p) {
				// p *= 0.01;
				float h = GetHeight(p) - 1000;
				float d1 = -(p.y + h);
				// float d2 = p.y -h;
				// float d = sdSphere(p, 10000);

				// return min(d1, d2);
				return d1;
			}

			float Raymarch(float3 ro, float3 rd) {
				float dO = 0;
				float dS;
				for (int i = 0; i < MAX_STEPS; i++) {
					float3 p = ro + dO * rd;
					dS = GetDist(p);
					dO += dS;
					if (dS < SURF_DIST || dO > MAX_DIST) {
						break;
					}
				}
				return dO;
			}

			// get normal of p
			float3 GetNormal(float3 p) {
				float2 e = float2(1e-2, 0); // epsilon
				float3 n = GetDist(p) - float3(GetDist(p - e.xyy), GetDist(p - e.yxy), GetDist(p - e.yyx));
				return normalize(n);
			}

			//// march from sample point to light source
			//float lightMarch(float3 samplePos) {

			//	// uses raymarch to sample accumulative density from light to source sample;
			//	float3 dirToLight = _WorldSpaceLightPos0.xyz;

			//	// get distance to box from inside;
			//	float2 rayBoxInfo = rayBoxDst(boundsMin, boundsMax, samplePos, 1 / dirToLight);
			//	float dstInsideBox = rayBoxInfo.y;

			//	float stepSize = dstInsideBox / 6.0;
			//	float dstTravelled = stepSize;

			//	float totalDensity = 0;

			//	for (int i = 0; i < 6; i++) {
			//		samplePos += dirToLight * stepSize;
			//		totalDensity += max(0, sampleDensity(samplePos) * stepSize);
			//		// dstTravelled += stepSize;
			//	}

			//	float transmittance = max(beer(totalDensity, lightAbsorptionTowardSun), beer(totalDensity * 0.25, lightAbsorptionTowardSun) * 0.7);

			//	return  darknessThreshold + transmittance * (1 - darknessThreshold);

			//}

			fixed4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv - .5;
				float3 ro = i.ro;
				float3 rd = i.rd;

				float d = Raymarch(ro, rd);
				fixed4 col = 0;

				if (d < MAX_DIST) {

					//// random offset on the starting position to remove the layering artifact
					//float randomOffset = BlueNoise.SampleLevel(samplerBlueNoise, i.uv * 1000, 0);
					//float dstTravelled = (randomOffset - 0.5) * 2 * raymarchStepSize * 2;
					//// float dstTravelled = 0;

					//float cosAngle = dot(normalize(rd), normalize(_WorldSpaceLightPos0.xyz));
					//float phaseVal = phaseFunction(cosAngle);
					//float transmittance = 1; // extinction
					//float3 lightEnergy = 0; // the amount of light reaches the eye  
					//float totalDensity = 0;
					//// start cloud ray march here

					//// change how ray march is terminated{
					//for (int i = 0; i < raymarchStepCount; i++) {
					//	float3 startPos = startPos + rd * (dstTravelled);
					//	float density = sampleDensity(p);
					//	if (density > 0) {
					//		// totalDensity += density;
					//		float lightTransmittance = lightMarch(p);

					//		lightEnergy += density * raymarchStepSize * transmittance * lightTransmittance * phaseVal;

					//		transmittance *= beer(density * raymarchStepSize, lightAbsorptionThroughCloud);// as the ray marches further in, the more the light will be lost
					//		// Exit early if T is close to zero as further samples won't affect the result much
					//		if (transmittance < 0.01) {
					//			break;
					//		}
					//	}

					//	dstTravelled += raymarchStepSize;

					//}

				}
				else
					col.rgb = tex2D(_MainTex, i.uv);
				// float3 color = tex2Dlod (_MainTex, float4(i.uv, 0, 0)).rgb;
				// col.rgb = color;
				return col;
			}
			ENDCG
		}
	}
}
