#ifndef COMMONSHADERINCLUDE
#define COMMONSHADERINCLUDE

#define MAX_SDF_OBJECTS 128

// Uniforms set using MaterialPropertyBlock

struct SDFObject
{
	int type;
	int blendOp;
	float blendFactor;
	float4 shapeData;								// .x is texture scale
													// .y
													// .z
													// .w is normal blending strength (for triplanar)
	float4x4 transform;
};

struct SDFObjectWithMaterialProps
{
	int type;
	int blendOp;
	float blendFactor;
	float4 shapeData;				// .x is texture scale
									// .y
									// .z
									// .w is normal blending strength (for triplanar)
	float4x4 transform;

	float4 primaryColor;			// 16 bytes
	float4 secondaryColor;			// 16 bytes
	float4 emissionColor;			// 16 bytes
	float4 textureData;				// 16 bytes
									// .x is texture scale
									// .y
									// .z
									// .w is normal blending strength (for triplanar)
	int textureType;				// 4 bytes
	float smoothness;               // 4 bytes
	float metallic;                 // 4 bytes
};

float SDFCountTotal;								// in this number each compound object counts itself but also its children		
float SDFCountCompounded;							// in this number compound objects = 1

StructuredBuffer<SDFObjectWithMaterialProps> SDFObjects;
StructuredBuffer<int> OffsetsToNextSdf;

#endif