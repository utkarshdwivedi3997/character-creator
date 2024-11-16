using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utkarsh.UnityCore;

public class SDFEvaluator : MonoBehaviour
{
	private SDFCollection sdfCollection;
#region BASE SDFs
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

	public static float GetSDFValue(Vector3 pt)
    {
        return sdfSphere(pt, 0.5f);
        //return sdfCappedCone(pt, 0.7f, 0.2f, 0.6f);
    }
#endregion

    public void Initialize(SDFCollection sdfCollection)
	{
		this.sdfCollection = sdfCollection;
	}

	float sdfElement(Vector3 pos, int index, int lastIndex)
    {
        Vector3 posTransformed;
        Vector2 sdf = new Vector2(MAX_DIST, 1.0f);

        for (int i = index; i < lastIndex; i++)
        {
			SDFObject sdfObject = sdfCollection.SdfObjects[i];
            float size = sdfObject.ShapeData.x;
            SDFObject.BlendOperationEnum blendOp = i == index ? SDFObject.BlendOperationEnum.Add : sdfObject.BlendOperation;    // the first individual element should always ADD 
            SDFObject.SDFObjectType sdfType = sdfObject.Type;

            Matrix4x4 transform = sdfObject.transform.worldToLocalMatrix;
			posTransformed = transform.MultiplyPoint(pos);

            float roundness = sdfObject.ShapeData.w; // .w is always roundness

            float curSdf = 0.0f;
            float blendT = 0.0f;

            if (sdfType == SDFObject.SDFObjectType.Sphere)
            {
                curSdf = sdfSphere(posTransformed, size);
            }
            else if (sdfType == SDFObject.SDFObjectType.Cube)
            {
                curSdf = sdfBox(posTransformed, new Vector3(size, size, size));
            }
            else if (sdfType == SDFObject.SDFObjectType.Torus)
            {
                curSdf = sdfTorus(posTransformed, new Vector2(size, sdfObject.ShapeData.y));
            }
            else if (sdfType == SDFObject.SDFObjectType.Cylinder)
            {
                curSdf = sdfCylinder(posTransformed, sdfObject.ShapeData.y, size);
            }
            else if (sdfType == SDFObject.SDFObjectType.Capsule)
            {
                curSdf = sdfCapsule(posTransformed, sdfObject.ShapeData.y, size);
            }
            else if (sdfType == SDFObject.SDFObjectType.Octahedron)
            {
                curSdf = sdfOctahedron(posTransformed, size);
            }
            else if (sdfType == SDFObject.SDFObjectType.Cone)
            {
                curSdf = sdfCappedCone(posTransformed, size, sdfObject.ShapeData.y, sdfObject.ShapeData.z);
            }

            if (roundness > 0.0)
            {
                curSdf = sdfRoundness(curSdf, roundness);
            }

            if (blendOp == SDFObject.BlendOperationEnum.Add)   // add
            {
                sdf = add_smoothMin2(sdf.x, curSdf, sdfObject.BlendFactor);
                blendT = sdf.y;
            }
            else if (blendOp == SDFObject.BlendOperationEnum.Subtract)  // subtract
            {
                sdf = subtract(sdf.x, curSdf, sdfObject.BlendFactor);
                blendT = sdf.y;
            }
            else if (blendOp == SDFObject.BlendOperationEnum.Intersect)  // intersect
            {
                sdf = intersect(sdf.x, curSdf);
                blendT = sdf.y;
            }
            else    // colour blend
            {
                // sdf doesn't change! we only update colors
                Vector2 tmp = add_smoothMin2(sdf.x, curSdf, sdfObject.BlendFactor);
                blendT = tmp.y;
            }
        }

        return sdf.x;
    }

	public float sceneSdf(Vector3 pos)
	{
		Vector2 sdf = new Vector2(float.MaxValue, 1.0f);

		int i = 0;
		int sdfsDone = 0;
		while (sdfsDone < sdfCollection.SdfCountCompounded)
		{
			SDFObject sdfObject = sdfCollection.SdfObjects[i];
			SDFObject.SDFObjectType sdfType = sdfObject.Type;
			int elementIndex = i;
			SDFObject.BlendOperationEnum blendOp = sdfObject.BlendOperation;

			if (sdfType == SDFObject.SDFObjectType.Compound)  // compound sdf
			{
				elementIndex++; // this is the parent. ignore and go to children
			}

			float curSdf = sdfElement(pos, elementIndex, (int)sdfCollection.OffsetsToNextSdf[sdfsDone]);
			float blendT = 0.0f;

			if (blendOp == SDFObject.BlendOperationEnum.Add)   // add
			{
				sdf = add_smoothMin2(sdf.x, curSdf, sdfObject.BlendFactor);
				blendT = sdf.y;
			}
			else if (blendOp == SDFObject.BlendOperationEnum.Subtract)  // subtract
			{
				sdf = subtract(sdf.x, curSdf, sdfObject.BlendFactor);
				blendT = sdf.y;
			}
			else if (blendOp == SDFObject.BlendOperationEnum.Intersect)  // intersect
			{
				sdf = intersect(sdf.x, curSdf);
				blendT = sdf.y;
			}
			else    // colour blend
			{
				// sdf doesn't change! we only update colors
				Vector2 tmp = add_smoothMin2(sdf.x, curSdf, sdfObject.BlendFactor);
				blendT = tmp.y;
			}

			i = (int)sdfCollection.OffsetsToNextSdf[sdfsDone];
			sdfsDone++;
		}

		return sdf.x;
	}
}
