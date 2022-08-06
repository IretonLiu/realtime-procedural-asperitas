Shader "Unlit/HeightMapRaymarch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma enable_d3d11_debug_symbols

            #include "UnityCG.cginc"

            #define MAX_STEPS 100
            #define MAX_DIST 20000
            #define SURF_DIST 1e-5
            #define SPHERE_RADIUS 20000
            #define HORIZON_HEIGHT 19980


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
            sampler2D _CameraDepthTexture;
            
            sampler2D _Displacement;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.ro = _WorldSpaceCameraPos;
                float3 rd = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                o.rd =  mul(unity_CameraToWorld, float4(rd, 0));
                return o;
            }
        
        
            float remap(float value, float ol, float oh, float nl, float nh) {
                return nl + (value - ol) * (nh - nl) / (oh - ol);
            }

            // raymarch settings

            float heightMultiplier;
            float raymarchStepCount;
            float raymarchStepSize;
            float cloudThickness;
            float distanceDampingFactor;

            float2 GetUV(float3 p){
                int d = 25;
                float u = (p.x + d/2)/d;
                float v = (p.z + d/2) /d;
                return float2(u, v);
            }

    /*        float3 GetHeightFromMap(float3 p) {
                float4 height = tex2Dlod(_DisplacementY, float4(GetUV(p) * 0.1, 0, 0));
                return float3(0, height.r, 0);
            }*/

            float3 GetDisplacementFromMap(float3 p){
                float2 uv = GetUV(p)* 0.1;

                float3 displacement = tex2Dlod(_Displacement, float4(uv, 0, 0)).xyz;
                //float y = heightMultiplier * tex2Dlod(_DisplacementY, float4(uv, 0, 0)).r;

                //float x = tex2Dlod(_DisplacementX, float4(uv, 0, 0)).r;
                //float z = tex2Dlod(_DisplacementZ, float4(uv, 0, 0)).r;

                return displacement;
            }

            float sdSphere(float3 p, float s){
                return length(p) - s; 
            }

            float GetDist(float3 p, bool isInner, float distanceDamping){

                float3 translation = float3(0, -HORIZON_HEIGHT, 0);

                float d1;
                if (isInner) {
                    float3 displacement = distanceDamping * GetDisplacementFromMap(p);
                    d1 = -sdSphere(p - translation + displacement, SPHERE_RADIUS);
                }

                else
                    d1 = -sdSphere(p - translation, SPHERE_RADIUS + cloudThickness);
                
                // float d2 = p.y -h;
                // float d = sdSphere(p, 10000);

                // return min(d1, d2);
                return d1;
            }


            float Raymarch(float3 ro, float3 rd, float cullDepth, bool isInner){
                float dO = 0;
                float dS;
                float damping = remap(2 *dot(rd, float3(0.0, 1.0, 0.0)), 0.0, 2.0, distanceDampingFactor, 1.0);
                for (int i = 0; i < MAX_STEPS; i++){
                    float3 p = ro + dO * rd;
                    dS = GetDist(p, isInner, damping);
                    dO += dS;

                    if (abs(dS) < SURF_DIST || abs(dO) > MAX_DIST || abs(dO) > cullDepth ){
                        break;
                    }
                }
                return dO;
            }

            //float3 GetNormal(float3 p){
            //    float2 e = float2(1e-2, 0); // epsilon
            //    float3 n = GetDist(p) - float3(GetDist(p-e.xyy), GetDist(p-e.yxy), GetDist(p-e.yyx));
            //    return normalize(n);
            //}



            float time;
            float baseNoiseScale;
            float3 baseNoiseOffset;
            // float densityThreshold;
            float densityMultiplier;

            float detailNoiseScale;
            float3 detailNoiseOffset;

            float globalCoverage;
            float anvilBias;

            float4 lightColor;
            float darknessThreshold;
            float lightAbsorptionThroughCloud;
            float lightAbsorptionTowardSun;
            float4 phaseParams;


            Texture2D<float4> BlueNoise;
            SamplerState samplerBlueNoise;

            Texture3D<float4> BaseNoise;
            SamplerState samplerBaseNoise;

            Texture3D<float4> DetailNoise;
            SamplerState samplerDetailNoise;

            Texture2D<float4> WeatherMap;
            SamplerState samplerWeatherMap;

            // lighting
            float beer(float d, float b) {
                float beer = exp(-d * b);
                return beer;
            }

            float henyeyGreenstein(float cosAngle, float g) {

                // g is the eccentricity

                float g2 = g * g;
                float pi = 3.14159265358979;
                float hg = (1.0 - g2) / (4 * pi * pow(1 + g2 - 2 * g * cosAngle, 1.5));
                // TODO: pow 1.5 could be expensive, consider replacing with schlick phase function

                return hg;
            };

            // credits to sebastian lague
            float phaseFunction(float a) {
                float blend = .5;
                // blend between forward and backward scattering
                float hgBlend = henyeyGreenstein(a, phaseParams.x) * (1 - blend) + henyeyGreenstein(a, -phaseParams.y) * blend;
                return phaseParams.z + hgBlend * phaseParams.w;
            };

            float sampleDensity(float3 rayPosition) {


                //float3 boxSize = abs(boundsMax - boundsMin);
                //float3 boxCentre = (boundsMax + boundsMin) / 2;
                //float3 heightPercent = saturate(abs(rayPosition.y - boundsMin.y) / boxSize.y);


                float scale = 0.0001;

                float3 baseShapeSamplePosition = rayPosition * baseNoiseScale * scale +
                    baseNoiseOffset * scale;

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
                float finalCloud = baseCloud*densityMultiplier;

                //// add detailed noise
                //if (baseCloudWithCoverage > 0) {
                //	float3 detailNoiseSamplePos = rayPosition * detailNoiseScale * 0.001 + detailNoiseOffset;
                //	float3 detailNoise = DetailNoise.SampleLevel(samplerDetailNoise, detailNoiseSamplePos, 0).rgb;

                //	float highFreqFbm = (detailNoise.r * 0.625) + (detailNoise.g * 0.25) + (detailNoise.b * 0.125);

                //	float detailNoiseModifier = 0.35 * exp(-globalCoverage * 0.75) * lerp(highFreqFbm, 1. - highFreqFbm, saturate(heightPercent * 5.0));

                //	finalCloud = saturate(remap(baseCloudWithCoverage, detailNoiseModifier, 1.0, 0.0, 1.0));

                //}

                return finalCloud * 0.1;
            }

            // march from sample point to light source
            float lightMarch(float3 samplePos) {

                // uses raymarch to sample accumulative density from light to source sample;
                float3 dirToLight = _WorldSpaceLightPos0.xyz;

                float totalDensity = 0;

                for (int i = 0; i < 6; i++) {
                    samplePos += dirToLight * raymarchStepSize;
                    totalDensity += max(0, sampleDensity(samplePos) * raymarchStepSize);
                    //totalDensity += max(0, 0.1 * raymarchStepSize);
                    // dstTravelled += stepSize;
                }

                float transmittance = max(beer(totalDensity, lightAbsorptionTowardSun), beer(totalDensity * 0.25, lightAbsorptionTowardSun) * 0.7);

                return  darknessThreshold + transmittance * (1 - darknessThreshold);

            }
            

            //float MaxMarchDist(float3 rd) {
            //    float c = sqrt(pow(SPHERE_RADIUS + cloudThickness, 2) - pow(HORIZON_HEIGHT, 2));
            //    float3 up = float3 (0, 1, 0);
            //    float cosTheta = dot(rd, up);
            //    float cos2Theta = 2 * cosTheta * cosTheta - 1;
            //    float maxDist = (SPHERE_RADIUS - HORIZON_HEIGHT + cloudThickness) * (1 + cos2Theta) +  c * (1 - cos2Theta);

            //    return maxDist*0.5;

            //}

            fixed4 frag (v2f input) : SV_Target
            {
                float3 ro = input.ro;
                float rayLength = length(input.rd);

                float3 rd = input.rd / rayLength; // normalise ray direction

        
                fixed4 col = 0;

                float nonLinearDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.uv);
                float depth = LinearEyeDepth(nonLinearDepth) * rayLength;
                float dInner = Raymarch(ro, rd, depth, true);  // distance to inner cloud
                float dOuter = Raymarch(ro, rd, depth, false); // distance to outer sphere
                if (dInner < MAX_DIST && dInner < depth){

                    float3 p = ro + rd * abs(dInner);
                    // random offset on the starting position to remove the layering artifact
                    float randomOffset = BlueNoise.SampleLevel(samplerBlueNoise, input.uv * 1000, 0);
                    float dstTravelled = (randomOffset - 0.5)  * raymarchStepSize + dInner;
                    // float dstTravelled = 0;

                    float cosAngle = dot(normalize(rd), normalize(_WorldSpaceLightPos0.xyz));
                    float phaseVal = phaseFunction(cosAngle);
                    float transmittance = 1; // extinction
                    float3 lightEnergy = 0; // the amount of light reaches the eye  
                    float totalDensity = 0;
                    // start cloud ray march here

                    // change how ray march is terminated{
                    //for (int i = 0; i < raymarchStepCount; i++) {
                    while(dstTravelled < dOuter){
                    	float3 startPos = p + rd * (dstTravelled);
                    	float density = sampleDensity(startPos) ;
                        //float density = 0.005 * (d * 0.01);
                    	if (density > 0) {
                    		// totalDensity += density;
                    		float lightTransmittance = lightMarch(startPos);

                    		lightEnergy += density * raymarchStepSize * transmittance * lightTransmittance * phaseVal;

                    		transmittance *= beer(density * raymarchStepSize, lightAbsorptionThroughCloud);// as the ray marches further in, the more the light will be lost
                    		// Exit early if T is close to zero as further samples won't affect the result much
                    		if (transmittance < 0.001) {
                    			break;
                    		}
                    	}

                        //if (density == 0.0) {
                        //    return float4(1.0, 0.0, 0.0, 0.0);
                        //    
                        //}
                    	dstTravelled += raymarchStepSize;

                    }
                    /*float4 color = tex2Dlod (_HeightMap, float4(GetUV(p), 0, 0)*0.01);
                    col.rgb = 0.6 - remap(color.r, -1.0, 1.0, 0.0, .5);*/

                    //float c = 1.0/(d * 0.01);
                    //col.rgb = c;

                    //if (transmittance == 1.0) {
                    //    col.rgb = float3(1.0, 0.0, 0.0);
                    //}
                    //else {

                    //   
                    //}
                    float3 cloudCol = lightEnergy * lightColor.xyz;
                    col.rgb = tex2D(_MainTex, input.uv) * transmittance + cloudCol *(dInner / dOuter)* 3;
                    //col.rgb = d * 0.0001;

                }else col.rgb = tex2D(_MainTex, input.uv);
                // float3 color = tex2Dlod (_MainTex, float4(i.uv, 0, 0)).rgb;
                 //col.rgb = d *0.1;
                return col;
            }
            ENDCG
        }
    }
}
