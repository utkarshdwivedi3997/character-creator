#ifndef RAYMARCHSHADERINCLUDE
#define RAYMARCHSHADERINCLUDE

#include "Includes/SDF/SDFEvaluator.hlsl"
#include "Includes/SDF/Texturing.hlsl"

#define RAYMARCH_CONSTANT_STEPS 0
#define RAYMARCH_SPHERE_TRACE 1

#define MAX_ITERS 1024
#define MAX_DIST 100000
#define MIN_DIST 10e-4
#define EPSILON float3(0.0f, MIN_DIST, 0.0f)

struct Ray
{
	float3 origin;
	float3 direction;
};

float3 CalculateNormal(float3 pos)
{
	return normalize(float3(sceneSdf(pos + EPSILON.yxx) - sceneSdf(pos - EPSILON.yxx),
							sceneSdf(pos + EPSILON.xyx) - sceneSdf(pos - EPSILON.xyx),
							sceneSdf(pos + EPSILON.xxy) - sceneSdf(pos - EPSILON.xxy)));
}

void Raymarch_float(float3 rayOriginObjectSpace, float3 rayDirectionObjectSpace, out float3 outPosition, out float4 outColor, out float3 objectSpaceNormal, out float outSmoothness, out float outMetallic, out float4 outEmissionColor)
{
	float tNear, tFar;
	if (!intersectBox(rayOriginObjectSpace, rayDirectionObjectSpace, tNear, tFar))
	{
		// don't raymarch if ray doesn't intersect the AABB of the object
		outPosition = rayOriginObjectSpace + rayDirectionObjectSpace;
		outColor = float4(0, 0, 0, 0);
		objectSpaceNormal = float3(0, 0, 0);
		outSmoothness = 0;
		outMetallic = 0;
		outEmissionColor = float4(0, 0, 0, 0);
		return;
	}

	tFar = min(tFar, MAX_DIST);
	float dist = max(MIN_DIST, tNear);

	for (int i = 0; i < MAX_ITERS; i++)
	{
		float3 p = rayOriginObjectSpace + rayDirectionObjectSpace * dist;

		float m = sceneSdf(p);

		if (m <= MIN_DIST)
		{
			// hit the sphere
			outPosition = p;
			objectSpaceNormal = CalculateNormal(p);

			// now get material properties
			sceneSdf(p, objectSpaceNormal, outColor, outSmoothness, outMetallic, outEmissionColor);

			break;
		}

#if RAYMARCH_CONSTANT_STEPS
		dist += MIN_DIST;
#elif RAYMARCH_SPHERE_TRACE
		dist += clamp(m, MIN_DIST, m);
#endif
		if (dist >= tFar)
		{
			break;
		}
	}
}

void GetLighting_float(float3 worldSpaceNormal, out float3 outColor)
{
#if defined(SHADERGRAPH_PREVIEW)
	outColor = float3(1, 1, 1);
#else
	Light mainLight = GetMainLight();

	half NdotL = saturate(dot(worldSpaceNormal, mainLight.direction));
	NdotL += 0.2;	// ambient term
	outColor = mainLight.color * NdotL;
#endif
}
#endif