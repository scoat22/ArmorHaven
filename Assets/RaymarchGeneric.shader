// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/RaymarchGeneric"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// Compile one version of the shader with performance debugging
			// This way we can simply set a shader keyword to test perf
			#pragma multi_compile __ DEBUG_PERFORMANCE
			// You may need to use an even later shader model target, depending on how many instructions you have
			// or if you need variable-length for loops.
			#pragma target 3.0

			#include "UnityCG.cginc"
			#include "DistanceFunc.cginc"
			
			uniform sampler2D _CameraDepthTexture;
			// These are are set by our script (see RaymarchGeneric.cs)
			uniform sampler2D _MainTex;
			uniform float4 _MainTex_TexelSize;

			uniform float4x4 _CameraInvViewMatrix;
			uniform float4x4 _FrustumCornersES;
			uniform float4 _CameraWS;

			uniform float3 _LightDir;
			uniform float4x4 _MatTorus_InvModel;
			uniform sampler2D _ColorRamp_Material;
			uniform sampler2D _ColorRamp_PerfMap;

			uniform float _DrawDistance;

			struct appdata
			{
				// Remember, the z value here contains the index of _FrustumCornersES to use
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 ray : TEXCOORD1;
			};

			v2f vert (appdata v)
			{
				v2f o;
				
				// Index passed via custom blit function in RaymarchGeneric.cs
				half index = v.vertex.z;
				v.vertex.z = 0.1;
				
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv.xy;
				
				#if UNITY_UV_STARTS_AT_TOP
				if (_MainTex_TexelSize.y < 0)
					o.uv.y = 1 - o.uv.y;
				#endif

				// Get the eyespace view ray (normalized)
				o.ray = _FrustumCornersES[(int)index].xyz;
				// Dividing by z "normalizes" it in the z axis
				// Therefore multiplying the ray by some number i gives the viewspace position
				// of the point on the ray with [viewspace z]=i
				o.ray /= abs(o.ray.z);

				// Transform the ray from eyespace to worldspace
				o.ray = mul(_CameraInvViewMatrix, o.ray);

				return o;
			}

			// This is the distance field function.  The distance field represents the closest distance to the surface
			// of any object we put in the scene.  If the given point (point p) is inside of an object, we return a
			// negative answer.
			// return.x: result of distance field
			// return.y: material data for closest object
			float2 map(float3 p) {
				// Apply inverse model matrix to point when sampling torus
				// This allows for more complex transformations/animation on the torus
				float3 torus_point = mul(_MatTorus_InvModel, float4(p,1)).xyz;
				float2 d_torus = float2(sdTorus(torus_point, float2(1, 0.2)), 0.5);

				float2 d_box = float2(sdBox(p - float3(-3,0,0), float3(0.75,0.5,0.5)), 0.25);
				float2 d_sphere = float2(sdSphere(p - float3(3,0,0), 1), 0.75);

				float2 ret = opU_mat(d_torus, d_box);
				ret = opU_mat(ret, d_sphere);
				
				return ret;
			}

			float3 calcNormal(in float3 pos)
			{
				const float2 eps = float2(0.001, 0.0);
				// The idea here is to find the "gradient" of the distance field at pos
				// Remember, the distance field is not boolean - even if you are inside an object
				// the number is negative, so this calculation still works.
				// Essentially you are approximating the derivative of the distance field at this point.
				float3 nor = float3(
					map(pos + eps.xyy).x - map(pos - eps.xyy).x,
					map(pos + eps.yxy).x - map(pos - eps.yxy).x,
					map(pos + eps.yyx).x - map(pos - eps.yyx).x);
				return normalize(nor);
			}

			float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir) {
                // Adapted from: http://jcgt.org/published/0007/03/04/
                float3 t0 = (boundsMin - rayOrigin) * invRaydir;
                float3 t1 = (boundsMax - rayOrigin) * invRaydir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                
                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(tmax.x, min(tmax.y, tmax.z));

                // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
                // dstA is dst to nearest intersection, dstB dst to far intersection

                // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
                // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

                // CASE 3: ray misses box (dstA > dstB)

                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float2(dstToBox, dstInsideBox);
            }

			float remap(float value, float ol, float oh, float nl, float nh) {
                return nl + (value - ol) * (nh - nl) / (oh - ol);
            }

			float3 boundsMin;
            float3 boundsMax;

            bool raymarchByCount; // determines weather to raymarch by step count or step size
            int raymarchStepCount;
            float raymarchStepSize;
            Texture2D<float4> BlueNoise;
            SamplerState samplerBlueNoise;


            float time;
            float baseNoiseScale;
            float3 baseNoiseOffset;
            // float densityThreshold;
            float densityMultiplier;

            float detailNoiseScale;
            float3 detailNoiseOffset;

            float globalCoverage;
            float anvilBias;

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

            Buffer<float> SmokeMap;
            float SmokeRes;

			float shapeAltering(float heightPercent, float4 weatherMapSample){
                // float heightDensityGradient = saturate(remap(heightPercent, 0., .2, 0., 1.)) * saturate(remap(heightPercent, 1., .7,  0., 1.));
                float wh= weatherMapSample.b; // max height from the weathermaps' blue channel
                
                float shapeAlter = saturate(remap(heightPercent, 0., 0.07, 0., 1.));
                shapeAlter*= saturate(remap(heightPercent, wh * 0.2, wh, 1., 0.));

                // float heightDensityGradient = 1;
                // float stratus =  saturate(remap(heightPercent, 0.01, 0.03, 0., 1.)) * saturate(remap(heightPercent, 0.03, 0.15, 1., 0.));
                // float stratocumulus = saturate(remap(heightPercent, 0, .1, 0, 1)) * saturate(remap(heightPercent, .2, .3, 1, 0));
                // float cumulus = remap(heightPercent, 0, .1, 0, 1) * remap(heightPercent, 0.1, 0.8, 1, 0); 

                // float type = weatherMapSample.b;
                // if(type <= 0){ //stratus
                //     heightDensityGradient *= stratus;
                // } else if(type > 0  && type < 0.5){
                //     heightDensityGradient *= lerp(stratus, stratocumulus, type * 2); //stratocumulus
                // } else if(type == 0.5){
                //     heightDensityGradient *= stratocumulus;
                // } else if(type > 0.5 && type < 1){
                //     heightDensityGradient *= lerp(stratocumulus, cumulus,  type * 2 - 1); //stratocumulus
                // }else if( type >= 1){
                //     heightDensityGradient *= cumulus;
                // }

                return shapeAlter;
            }

			float densityAltering(float heightPercent, float4 weatherMapSample){
                float densityAlter = heightPercent * saturate(remap(heightPercent, 0., 0.15, 0. ,1.));
                densityAlter *= densityMultiplier * saturate(remap(heightPercent, .9, 1., 1., 0.));

                return densityAlter;

            }

            // d: dimension (width/height/depth)
            float Index(Buffer<float> Source, int d, int3 p) {
                // Bounds check.
                if(p.x < 0 || p.x > d ||
                   p.y < 0 || p.y > d ||
                   p.z < 0 || p.z > d) return 0;
                
                int index = (p.z * d * d) + (p.y * d) + p.x;
                // Assume size of (float).
                //uint raw = Source.Load(index << 2);  return asfloat(raw);
                return Source[index];
            }

            float Sample(int3 p)
            {
                return SmokeMap[(p.z * SmokeRes * SmokeRes) + (p.y * SmokeRes) + p.x];
            }

            float SampleTrilinear(Buffer<float> Source, int d, float3 p)
            {
                if(p.x < 0 || p.x > d ||
                   p.y < 0 || p.y > d ||
                   p.z < 0 || p.z > d) return 0;

                // Input: position in integer space (e.g. voxel grid coordinates)
                int3 base = int3(floor(p));
                float3 frac = p - base;

                // Sample the 8 corners around the base point
                float c000 = Sample(int3(base.x + 0, base.y + 0, base.z + 0));
                float c100 = Sample(int3(base.x + 1, base.y + 0, base.z + 0));
                float c010 = Sample(int3(base.x + 0, base.y + 1, base.z + 0));
                float c110 = Sample(int3(base.x + 1, base.y + 1, base.z + 0));
                float c001 = Sample(int3(base.x + 0, base.y + 0, base.z + 1));
                float c101 = Sample(int3(base.x + 1, base.y + 0, base.z + 1));
                float c011 = Sample(int3(base.x + 0, base.y + 1, base.z + 1));
                float c111 = Sample(int3(base.x + 1, base.y + 1, base.z + 1));

                // Interpolate along X
                float c00 = lerp(c000, c100, frac.x);
                float c10 = lerp(c010, c110, frac.x);
                float c01 = lerp(c001, c101, frac.x);
                float c11 = lerp(c011, c111, frac.x);

                // Interpolate along Y
                float c0 = lerp(c00, c10, frac.y);
                float c1 = lerp(c01, c11, frac.y);

                // Interpolate along Z
                return lerp(c0, c1, frac.z);
            }   

            float sampleDensity(float3 rayPosition) {
                float3 boxSize = abs(boundsMax - boundsMin);
                float3 boxCentre = (boundsMax + boundsMin) /2;

                // Test:
                //float3 SamplePos = (boxSize * 0.5 + rayPosition);
                float3 SamplePos = rayPosition;
                //float3 SamplePos = rayPosition + SmokeRes / 2;
                //return saturate(rayPosition.y);
                //float SmokeSample = Index(SmokeMap, SmokeRes, SamplePos);
                float SmokeSample = SampleTrilinear(SmokeMap, SmokeRes, SamplePos);
                //return SmokeSample;
                //return saturate(2 - length(rayPosition));

                float3 heightPercent = saturate(abs(rayPosition.y - boundsMin.y) / boxSize.y);


                float3 baseShapeSamplePosition = (boxSize * 0.5 + rayPosition) * baseNoiseScale * 0.0001 + baseNoiseOffset * 0.0001;

                // wind settings
                float3 windDirection = float3(1.0, 0.0, 0.0);
                float cloudSpeed = 10.0;
                // cloud_top offset - push the tops of the clouds along this wind direction by this many units.
                float cloudTopOffset = .5;

                baseShapeSamplePosition += heightPercent * windDirection * cloudTopOffset;
                baseShapeSamplePosition += (windDirection + float3(0., 1., 0.)) * time  * 0.001 * cloudSpeed;

                float4 baseNoiseValue = BaseNoise.SampleLevel(samplerBaseNoise, baseShapeSamplePosition, 0);


                // weather map is 10km x 10km, assume that each unit is 1km
                float2 wmSamplePosition = (rayPosition.xz - boundsMin.xz)  * 0.00005 ;
                float4 weatherMapSample = WeatherMap.SampleLevel(samplerWeatherMap, wmSamplePosition, 0);


                // create fbm from the base noise (rgb channels).
                float lowFreqFBM = (baseNoiseValue.r * 0.625) + (baseNoiseValue.g * 0.25) + (baseNoiseValue.b * 0.125);
                // get the base cloud shape with the base noise (alpha channel).
                float baseCloud = remap(baseNoiseValue.a, - (1.0 - lowFreqFBM), 1.0, 0.0, 1.0);

                //return lowFreqFBM;
  

                // Changed:
                float SA = 1;//shapeAltering(heightPercent, weatherMapSample); 
                float DA = densityAltering(heightPercent, weatherMapSample);
                // baseCloud = max(0, baseCloud - densityThreshold) * densityMultiplier;
                // baseCloud *= heightGradient;

                float coverage = weatherMapSample.g;
                // coverage = pow(coverage, remap(heightPercent, 0.7, 0.8, 1.0, lerp(1.0, 0.5, anvilBias)));
                float baseCloudWithCoverage = saturate(remap(baseCloud * SA, 1 - globalCoverage * coverage, 1.0, 0.0, 1.0));
                
                //baseCloudWithCoverage *= coverage;

                // float density = shape.a;
                // return  baseCloudWithCoverage ;
                float finalCloud = baseCloudWithCoverage;
                //return finalCloud;
                // add detailed noise
                //if(baseCloudWithCoverage > 0)
                {
                    float3 detailNoiseSamplePos = rayPosition * detailNoiseScale * 0.001 + detailNoiseOffset;
                    float3 detailNoise = DetailNoise.SampleLevel(samplerDetailNoise, detailNoiseSamplePos, 0).rgb;

                    float highFreqFbm = (detailNoise.r * 0.625) + (detailNoise.g * 0.25) + (detailNoise.b * 0.125);

                    //return SmokeSample;
                    //return step(0.8, highFreqFbm);
                    return step(1.0 - SmokeSample, highFreqFbm);
                    
                    if(SmokeSample >= 0.5)
                    {
                        return highFreqFbm;
                    }
                    else return 0;

                    float detailNoiseModifier = 0.35 * exp(-globalCoverage * 0.75) * lerp(highFreqFbm, 1. - highFreqFbm, saturate(heightPercent * 5.0));

                    finalCloud = saturate(remap(baseCloudWithCoverage, detailNoiseModifier, 1.0, 0.0, 1.0));

                }
                
                return saturate(finalCloud * densityMultiplier);
                //return finalCloud * DA;
            }

			// lighting
            
            float beer(float d, float b) {
                float beer = exp(-d * b);
                return beer;
            }

            float henyeyGreenstein(float cosAngle, float g){
                
                // g is the eccentricity

                float g2 = g * g;
                float pi = 3.14159265358979;
                float hg = (1.0 - g2)/(4 * pi *pow(1 + g2 - 2 * g * cosAngle, 1.5)) ;
                // TODO: pow 1.5 could be expensive, consider replacing with schlick phase function

                return hg;
            };

            // credits to sebastian lague
            float phaseFunction(float a){
                float blend = .5;
                // blend between forward and backward scattering
                float hgBlend = henyeyGreenstein(a,phaseParams.x) * (1-blend) + henyeyGreenstein(a,-phaseParams.y) * blend;
                return phaseParams.z + hgBlend * phaseParams.w;
            };

            // march from sample point to light source
            float lightMarch(float3 samplePos, float randomSample){
                
                // uses raymarch to sample accumulative density from light to source sample;
                float3 dirToLight = _WorldSpaceLightPos0.xyz;

                // get distance to box from inside;
                float2 rayBoxInfo = rayBoxDst(boundsMin, boundsMax, samplePos, 1/dirToLight);
                float dstInsideBox = rayBoxInfo.y;

                float stepSize = dstInsideBox / 6.0;
                float dstTravelled = stepSize;

                float totalDensity = 0;
                
                for(int i = 0; i < 6; i++){
                    samplePos += dirToLight * stepSize; 
                    totalDensity += max(0, sampleDensity(samplePos ) * stepSize);
                    //dstTravelled += stepSize;
                }

                float transmittance = max(beer(totalDensity, lightAbsorptionTowardSun), beer(totalDensity * 0.25, lightAbsorptionTowardSun) * 0.7);

                return  darknessThreshold + transmittance * (1-darknessThreshold);

            }

			// Raymarch along given ray
			// ro: ray origin
			// rd: ray direction
			// s: unity depth buffer
			fixed4 raymarch2(float2 uv, float3 ro, float3 rd, float s) {
				fixed4 ret = fixed4(0,0,0,0);

				const int maxstep = 64;
				float t = 0; // current distance traveled along ray
				for (int i = 0; i < maxstep; ++i) {
					// If we run past the depth buffer, or if we exceed the max draw distance,
					// stop and return nothing (transparent pixel).
					// this way raymarched objects and traditional meshes can coexist.
					if (t >= s || t > _DrawDistance) {
						ret = fixed4(0, 0, 0, 0);
						break;
					}

					float3 p = ro + rd * t; // World space position of sample
					float2 d = map(p);		// Sample of distance field (see map())

					// If the sample <= 0, we have hit something (see map()).
					if (d.x < 0.001) {
						float3 n = calcNormal(p);
						float light = dot(-_LightDir.xyz, n);
						ret = fixed4(tex2D(_ColorRamp_Material, float2(d.y,0)).xyz * light, 1);
						break;
					}

					// If the sample > 0, we haven't hit anything yet so we should march forward
					// We step forward by distance d, because d is the minimum distance possible to intersect
					// an object (see map()).
					t += d;
				}

				return ret;
			}

			fixed4 raymarch(float2 uv, float3 ro, float3 rd, float s)
			{
				float2 boxInfo = rayBoxDst(boundsMin, boundsMax, ro, 1/rd);
                float dstToBox = boxInfo.x;
                float dstInsideBox = boxInfo.y;

				// if(dstInsideBox > 0){
                //     col = 0;
                // }
                float stepSize;

                if(raymarchByCount){
                    int numSteps = raymarchStepCount;
                    stepSize = dstInsideBox / numSteps;
                }else{
                    stepSize = dstInsideBox == 0 ? 0 : raymarchStepSize;
                }
				
				// random offset on the starting position to remove the layering artifact
                float randomOffset = BlueNoise.SampleLevel(samplerBlueNoise, uv * 500 + _Time.x * 5000, 0);
                float dstTravelled = (randomOffset - 0.5) * 2 * stepSize * 2;
                //float dstTravelled = 0;
                
                float cosAngle = dot(normalize(rd), normalize(_WorldSpaceLightPos0.xyz));
                float phaseVal = phaseFunction(cosAngle);
                float transmittance = 1; // extinction
                float3 lightEnergy = 0; // the amount of light reaches the eye  
                float totalDensity = 0;

                // sample march through volume
                while (dstTravelled < dstInsideBox) {

                    if (dstTravelled >= s)  break;

                    float3 p = ro + rd * (dstToBox + dstTravelled);
                    float density  = sampleDensity(p);
                    if(density > 0) {
                        // totalDensity += density;
                        float lightTransmittance = lightMarch(p, randomOffset);
                        
                        lightEnergy += density * stepSize * transmittance * lightTransmittance * phaseVal;

                        transmittance *= beer(density * stepSize, lightAbsorptionThroughCloud);// as the ray marches further in, the more the light will be lost
                        // Exit early if T is close to zero as further samples won't affect the result much
                        if (transmittance < 0.01) {
                            break;
                        }
                    }
                    
                    dstTravelled += stepSize;
                }


                // float transmittance = exp(totalDensity);
                // float3 col = tex2D(_MainTex, uv) * transmittance;
                // return float4(col, 0);
                
                float3 cloudCol = lightEnergy;
                float3 col = tex2D(_MainTex, uv) * transmittance + cloudCol;
                return fixed4(col, 1);
			}

			// Modified raymarch loop that displays a heatmap of ray sample counts
			// Useful for performance testing and analysis
			// ro: ray origin
			// rd: ray direction
			// s: unity depth buffer
			fixed4 raymarch_perftest(float3 ro, float3 rd, float s) {
				const int maxstep = 64;
				float t = 0; // current distance traveled along ray

				for (int i = 0; i < maxstep; ++i) {
					float3 p = ro + rd * t; // World space position of sample
					float2 d = map(p);      // Sample of distance field (see map())

					// If the sample <= 0, we have hit something (see map()).
					// If t > drawdist, we can safely bail because we have reached the max draw distance
					if (d.x < 0.001 || t > _DrawDistance) {
						// Simply return the number of steps taken, mapped to a color ramp.
						float perf = (float)i / maxstep;
						return fixed4(tex2D(_ColorRamp_PerfMap, float2(perf, 0)).xyz, 1);
					}

					t += d;
				}
				// By this point the loop guard (i < maxstep) is false.  Therefore
				// we have reached maxstep steps.
				return fixed4(tex2D(_ColorRamp_PerfMap, float2(1, 0)).xyz, 1);
			}

			fixed4 frag (v2f i) : SV_Target
			{
				// ray direction
				float3 rd = normalize(i.ray.xyz);
				// ray origin (camera position)
				float3 ro = _CameraWS;

				float2 duv = i.uv;
				#if UNITY_UV_STARTS_AT_TOP
				if (_MainTex_TexelSize.y < 0)
					duv.y = 1 - duv.y;
				#endif

				// Convert from depth buffer (eye space) to true distance from camera
				// This is done by multiplying the eyespace depth by the length of the "z-normalized"
				// ray (see vert()).  Think of similar triangles: the view-space z-distance between a point
				// and the camera is proportional to the absolute distance.
				float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, duv).r);
				depth *= length(i.ray);
                //return depth;

				fixed3 col = tex2D(_MainTex,i.uv);

				#if defined (DEBUG_PERFORMANCE)
				fixed4 add = raymarch_perftest(ro, rd, depth);
				#else
				fixed4 add = raymarch(i.uv, ro, rd, depth);
				#endif

				// Returns final color using alpha blending
				return fixed4(col*(1.0 - add.w) + add.xyz * add.w,1.0);
			}
			ENDCG
		}
	}
}
