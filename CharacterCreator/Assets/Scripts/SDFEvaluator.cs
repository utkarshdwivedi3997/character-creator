using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utkarsh.UnityCore;

public class SDFEvaluator : MonoBehaviour
{
	// This class has the same SDF functions as the shader
	const int MAX_ITERS = 1024;
	const float MAX_DIST = 100000f;
	const float MIN_DIST = 10e-4f;

	static bool intersectBox(Vector3 rayOrigin, Vector3 rayDirection, out float tNear, out float tFar)
	{
		Vector3 rinvDir = (1.0f).DivideBy(rayDirection);	// 1 / rayDir

		// for now we're assuming unit cube that is NOT moved!
		Vector3 tbot = Vector3.Scale(rinvDir, (new Vector3(-0.5f, -0.5f, -0.5f) - rayOrigin));
		Vector3 ttop = Vector3.Scale(rinvDir, (new Vector3(0.5f, 0.5f, 0.5f) - rayOrigin));
		Vector3 tmin = Vector3.Min(ttop, tbot);
		Vector3 tmax = Vector3.Max(ttop, tbot);

		Vector2 tmax_xx = new Vector2(tmin.x, tmin.x);
		Vector2 tmax_yz = new Vector2(tmin.y, tmin.z);
		Vector2 t = Vector2.Max(tmax_xx, tmax_yz);
		float t0 = Mathf.Max(t.x, t.y);
		t = Vector2.Min(tmax_xx, tmax_yz);
		float t1 = Mathf.Min(t.x, t.y);
		tNear = t0;
		tFar = t1;
		return t1 > Mathf.Max(t0, 0.0f);
	}

	// from IQ

	/// <summary>
	/// Returns the smooth minimum (square) based on a k blend factor.
	/// </summary>
	/// <param name="a"></param>
	/// <param name="b"></param>
	/// <param name="k"></param>
	/// <returns>Vector2 value. x component is the smooth minimum value, and y component is the blend factor [0,1]</returns>
	static Vector2 add_smoothMin2(float a, float b, float k)
	{
		float h = Mathf.Max(k - Mathf.Abs(a - b), 0.0f) / k;
		float m = h * h * 0.5f;
		float s = m * k * (1.0f / 2.0f);
		return (a < b) ? new Vector2(a - s, m) : new Vector2(b - s, m - 1.0f);
	}

	static Vector2 subtract(float a, float b, float k)
	{
		float h = Mathf.Clamp(0.5f - 0.5f * (a + b) / k, 0.0f, 1.0f);
		return new Vector2(Mathf.Lerp(a, -b, h) + k * h * (1.0f - h), 0f);
	}

	static Vector2 intersect(float a, float b)
	{
		return new Vector2(Mathf.Max(a, b), 0);
	}
	static float sdfCompoundObjectParent(Vector3 pos)
	{
		return 0.0f;
	}

	static float sdfBox(Vector3 pos, Vector3 size)
	{
		//if (pos.y > 0.1)
		//{
		//	pos.y += sin(10 * pos.x) * sin(20 * pos.z) * 0.05;
		//}
		Vector3 q = pos.Abs() - size;
		return (Vector3.Max(q, Vector3.zero).magnitude + 
			Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0.0f));
	}

	static float sdfSphere(Vector3 pos, float radius)
	{
		return (pos.sqrMagnitude - radius * radius);
	}

	static float sdfTorus(Vector3 pos, Vector2 size)
	{
		Vector2 q = new Vector2(new Vector2(pos.x, pos.z).magnitude - size.x, pos.y);
		return q.magnitude - size.y;
	}

	static float sdfCylinder(Vector3 pos, float height, float radius)
	{
		Vector2 d = (new Vector2(new Vector2(pos.x, pos.z).magnitude, pos.y)).Abs() - new Vector2(radius, height);
		return Mathf.Min(Mathf.Max(d.x, d.y), 0.0f) + Vector2.Max(d, Vector2.zero).magnitude;
	}

	static float sdfCapsule(Vector3 pos, float height, float radius)
	{
		float halfHeight = height * 0.5f;
		pos.y -= Mathf.Clamp(pos.y, -halfHeight, halfHeight);     // modified from IQ's version: move this to the center of axis!
		return pos.magnitude - radius;
	}

	static float sdfOctahedron(Vector3 pos, float size)
	{
		pos = pos.Abs();
		return (pos.x + pos.y + pos.z - size) * 0.57735027f;
	}

	static float sdfCappedCone(Vector3 pos, float height, float r1, float r2)
	{
		Vector2 q = new Vector2(new Vector2(pos.x, pos.z).magnitude, pos.y);
		Vector2 k1 = new Vector2(r2, height);
		Vector2 k2 = new Vector2(r2 - r1, 2.0f * height);
		Vector2 ca = new Vector2(q.x - Mathf.Min(q.x, (q.y < 0.0) ? r1 : r2), Mathf.Abs(q.y) - height);
		Vector2 cb = q - k1 + k2 * Mathf.Clamp(Vector2.Dot(k1 - q, k2) / Vector2.Dot(k2, k2), 0.0f, 1.0f);
		float s = (cb.x < 0.0 && ca.y < 0.0) ? -1.0f : 1.0f;
		return s * Mathf.Sqrt(Mathf.Min(Vector2.Dot(ca, ca), Vector2.Dot(cb, cb)));
	}

	static float sdfRoundness(float sdf, float roundness)
	{
		return sdf - roundness;
	}

	public static bool IsInsideSDF(Vector3 pt)
    {
		return sdfSphere(pt, 0.7f) <= 0.0f;
		//return sdfCappedCone(pt, 0.7f, 0.2f, 0.6f) <= 0.0f;
    }

	//float sdfElement(Vector3 pos, int index, int lastIndex)
	//{
	//	Vector3 posTransformed;
	//	Vector2 sdf = new Vector2(MAX_DIST, 1.0f);

	//	for (int i = index; i < lastIndex; i++)
	//	{
	//		float size = SDFData[i].x;
	//		int blendOp = i == index ? 0 : SDFBlendOperation[i];    // the first individual element should always ADD 
	//		int sdfType = SDFType[i];

	//		float4x4 transform = SDFTransformMatrices[i];
	//		posTransformed = mul(transform, float4(pos, 1.0)).xyz;

	//		float roundness = SDFData[i].w; // .w is always roundness

	//		float curSdf = 0.0f;
	//		float blendT = 0.0f;

	//		if (sdfType == 0)
	//		{
	//			curSdf = sdfSphere(posTransformed, size);
	//		}
	//		else if (sdfType == 1)
	//		{
	//			curSdf = sdfBox(posTransformed, size);
	//		}
	//		else if (sdfType == 2)
	//		{
	//			curSdf = sdfTorus(posTransformed, Vector2(size, SDFData[i].y));
	//		}
	//		else if (sdfType == 3)
	//		{
	//			curSdf = sdfCylinder(posTransformed, SDFData[i].y, size);
	//		}
	//		else if (sdfType == 4)
	//		{
	//			curSdf = sdfCapsule(posTransformed, SDFData[i].y, size);
	//		}
	//		else if (sdfType == 5)
	//		{
	//			curSdf = sdfOctahedron(posTransformed, size);
	//		}
	//		else if (sdfType == 6)
	//		{
	//			curSdf = sdfCappedCone(posTransformed, size, SDFData[i].y, SDFData[i].z);
	//		}

	//		if (roundness > 0.0)
	//		{
	//			curSdf = sdfRoundness(curSdf, roundness);
	//		}

	//		if (blendOp == 0)   // add
	//		{
	//			sdf = add_smoothMin2(sdf, curSdf, SDFBlendFactor[i]);
	//			blendT = sdf.y;
	//		}
	//		else if (blendOp == 1)  // subtract
	//		{
	//			sdf = subtract(sdf, curSdf, SDFBlendFactor[i]);
	//			blendT = sdf.y;
	//		}
	//		else if (blendOp == 2)  // intersect
	//		{
	//			sdf = intersect(sdf, curSdf);
	//			blendT = sdf.y;
	//		}
	//		else    // colour blend
	//		{
	//			// sdf doesn't change! we only update colors
	//			Vector2 tmp = add_smoothMin2(sdf, curSdf, SDFBlendFactor[i]);
	//			blendT = tmp.y;
	//		}
	//	}

	//	return sdf.x;
	//}
}
