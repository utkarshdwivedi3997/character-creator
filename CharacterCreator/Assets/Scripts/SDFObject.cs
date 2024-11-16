using System;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
public class SDFObject : MonoBehaviour
{
    public const int MAX_SDF_CHILDREN = 32;

    public enum SDFObjectType
    {
        Sphere = 0,
        Cube = 1,
        Torus = 2,
        Cylinder = 3,
        Capsule = 4,
        Octahedron = 5,
        Cone = 6,
        Compound = -1,
    }

    public enum BlendOperationEnum
    {
        Add = 0,
        Subtract = 1,
        Intersect = 2,
        ColorBlend = 3,
    }

    public enum ProceduralTextureType
    {
        Plain = 0,
        StripedTriplanar = 1,
        PolkaDotsTriplanar = 2,
        DiamondsTriplanar = 3,
        WavesTriplanar = 4,
        ProceduralPattern1 = 5,
        WorleySmooth3D = 6,
        WorleyCells3D = 7,
        FBM = 8,
        Perlin = 9,
    }

    [SerializeField] private SDFObjectType type;
    public SDFObjectType Type => type;

    [SerializeField, FormerlySerializedAs("data")] private Vector4 shapeData = new Vector4(0.3f, 0, 0, 0);
    public Vector4 ShapeData => shapeData;

    [SerializeField] private BlendOperationEnum blendOperation;
    public BlendOperationEnum BlendOperation => blendOperation;

    [Range(0.0001f, 10.0f)]
    [SerializeField] private float blendFactor = 0.02f;
    public float BlendFactor => blendFactor;

    [SerializeField] private Color color = Color.white;
    public Color PrimaryColor => color;

    [SerializeField] private Color secondaryColor = Color.black;
    public Color SecondaryColor => secondaryColor;

    [SerializeField] private ProceduralTextureType textureType = ProceduralTextureType.Plain;
    public ProceduralTextureType TextureType => textureType;

    [SerializeField] private Vector4 textureData = new Vector4(1.0f, 0, 0, 0);
    public Vector4 TextureData => textureData;

    [SerializeField, Range(0, 1)] private float smoothness = 0.0f;
    public float Smoothness => smoothness;

    [SerializeField, Range(0, 1)] private float metallic = 0.0f;
    public float Metallic => metallic;

    [SerializeField, ColorUsage(false, true)] private Color emissionColor;
    public Color EmissionColor => emissionColor;

    public SDFObject[] SDFChildren { get; private set; }
    public int NumSDFChildren { get; private set; } = 0;

    private SDFObjectType lastStoredType;
    public bool IsChildOfCompoundSDF { get; private set; } = false;
    private bool hasInitialized = false;

    private SDFCollection parentCollection;

    private void OnEnable()
    {
        Initialize();
    }

    public void SetAsChildOfCompound()
    {
        if (type == SDFObjectType.Compound)
        {
            type = SDFObjectType.Sphere;
        }
        IsChildOfCompoundSDF = true;
    }

    private void Initialize()
    {
        if (hasInitialized)
        {
            return;
        }

        SDFChildren = new SDFObject[MAX_SDF_CHILDREN];
        NumSDFChildren = 0;

        ValidateTransformChildrenChange();

        hasInitialized = true;
    }

    public void SetParentCollection(SDFCollection parentCollection)
    {
        this.parentCollection = parentCollection;
    }

    public SDFObject_GPU ToGPUStruct()
    {
        SDFObject_GPU gpuSdf = new SDFObject_GPU();
        gpuSdf.type = (int)Type;
        gpuSdf.blendOp = (int)BlendOperation;
        gpuSdf.blendFactor = BlendFactor;
        gpuSdf.shapeData = ShapeData;
        gpuSdf.transform = transform.worldToLocalMatrix;

        return gpuSdf;
    }

    public SDFObjectWithMaterialProperties_GPU ToGPUStructWithMaterialProperties()
    {
        SDFObjectWithMaterialProperties_GPU gpuSdf = new SDFObjectWithMaterialProperties_GPU();
        gpuSdf.type = (int)Type;
        gpuSdf.blendOp = (int)BlendOperation;
        gpuSdf.blendFactor = BlendFactor;
        gpuSdf.shapeData = ShapeData;
        gpuSdf.transform = transform.worldToLocalMatrix;

        gpuSdf.primaryColor = PrimaryColor;
        gpuSdf.secondaryColor = SecondaryColor;
        gpuSdf.emissionColor = EmissionColor;
        gpuSdf.textureData = TextureData;
        gpuSdf.textureType = (int)TextureType;
        gpuSdf.smoothness = Smoothness;
        gpuSdf.metallic = Metallic;

        return gpuSdf;
    }

    private void OnDisable()
    {
        hasInitialized = false;
    }

    private void OnTransformChildrenChanged()
    {
        ValidateTransformChildrenChange();
    }

    public static event Action<SDFCollection> OnChildrenUpdated;
    private void ValidateTransformChildrenChange()
    {
        if (type != SDFObjectType.Compound)
        {
            if (NumSDFChildren > 0)
            {
                NumSDFChildren = 0;
                OnChildrenUpdated?.Invoke(parentCollection);
            }
            return;
        }

        SDFObject[] objects = GetComponentsInChildren<SDFObject>();
        NumSDFChildren = Mathf.Clamp(objects.Length - 1, 0, MAX_SDF_CHILDREN);  // -1 because self (parent) is included here and we don't want to count it
        
        for (int i = 0; i < NumSDFChildren; i++)
        {
            SDFChildren[i] = objects[i + 1];    // index into sdf children from i + 1 to ignore parent
            SDFChildren[i].SetParentCollection(parentCollection);
            SDFChildren[i].SetAsChildOfCompound();
        }

        OnChildrenUpdated?.Invoke(parentCollection);
    }

    private void OnValidate()
    {
        if (type == SDFObjectType.Compound && IsChildOfCompoundSDF)
        {
            type = lastStoredType;
            return;
        }

        lastStoredType = type;
        ValidateTransformChildrenChange();
    }

    [ContextMenu("Add SDF Child Object")]
    private void AddSDFChild()
    {
        if (IsChildOfCompoundSDF || NumSDFChildren == MAX_SDF_CHILDREN)
        {
            // Can't add any more to this SDFCollection!
            return;
        }

        type = SDFObjectType.Compound;
        SDFObject sdfObject = parentCollection.InstantiateSDFObject(transform);
        sdfObject.SetAsChildOfCompound();
    }
}

[System.Serializable]
public struct SDFObject_GPU
{
    public int type;                       // 4 byts
    public int blendOp;                    // 4 bytes
    public float blendFactor;              // 4 bytes
    public Vector4 shapeData;              // 16 bytes ( 4 per float * 4 elements )
    public Matrix4x4 transform;            // 64 bytes ( 4 per float * 4 per row * 4 per column)
}

[System.Serializable]
public struct SDFObjectWithMaterialProperties_GPU
{
    public int type;                        // 4 byts
    public int blendOp;                     // 4 bytes
    public float blendFactor;               // 4 bytes
    public Vector4 shapeData;               // 16 bytes ( 4 per float * 4 elements )
    public Matrix4x4 transform;             // 64 bytes ( 4 per float * 4 per row * 4 per column)

    public Vector4 primaryColor;            // 16 bytes
    public Vector4 secondaryColor;          // 16 bytes
    public Vector4 emissionColor;           // 16 bytes
    public Vector4 textureData;             // 16 bytes
                                            // .x is texture scale
                                            // .y
                                            // .z
                                            // .w is normal blending strength (for triplanar)
    public int textureType;                 // 4 bytes
    public float smoothness;                // 4 bytes
    public float metallic;                  // 4 bytes
}
