using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class SDFCollection : MonoBehaviour
{
    #region Shader Properties
    private const int MAX_SDF_OBJECTS = 256;
    private static readonly int SDFTypeShaderPropertyID = Shader.PropertyToID("SDFType");
    private static readonly int SDFTransformMatricesPropertyID = Shader.PropertyToID("SDFTransformMatrices");
    private static readonly int SDFDataShaderPropertyID = Shader.PropertyToID("SDFData");
    private static readonly int SDFBlendOperationShaderPropertyID = Shader.PropertyToID("SDFBlendOperation");
    private static readonly int SDFBlendFactorShaderPropertyID = Shader.PropertyToID("SDFBlendFactor");
    private static readonly int SDFCountShaderPropertyID = Shader.PropertyToID("SDFCount");
    private static readonly int SDFPrimaryColorsPropertyID = Shader.PropertyToID("SDFPrimaryColors");
    private static readonly int SDFSecondaryColorsPropertyID = Shader.PropertyToID("SDFSecondaryColors");
    private static readonly int SDFTextureTypePropertyID = Shader.PropertyToID("SDFTextureType");
    private static readonly int SDFTextureDataShaderPropertyID = Shader.PropertyToID("SDFTextureData");

    private static readonly int SDFEmissionColorsPropertyID = Shader.PropertyToID("SDFEmissionColors");
    private static readonly int SDFSmoothnessPropertyID = Shader.PropertyToID("SDFSmoothness");
    private static readonly int SDFMetallicPropertyID = Shader.PropertyToID("SDFMetallic");
    #endregion

    private SDFObject[] sdfObjects;
    int numSDFObjects;
    [SerializeField] private SDFObject sdfObjectPrefab;

    private Renderer renderer;
    private MaterialPropertyBlock materialPropertyBlock;

    private Matrix4x4[] sdfTransformMatrices = new Matrix4x4[MAX_SDF_OBJECTS];
    private float[] sdfTypes = new float[MAX_SDF_OBJECTS];
    private Vector4[] sdfData = new Vector4[MAX_SDF_OBJECTS];
    private float[] sdfBlendOperations = new float[MAX_SDF_OBJECTS];
    private float[] sdfBlends = new float[MAX_SDF_OBJECTS];
    private Vector4[] sdfPrimaryColors = new Vector4[MAX_SDF_OBJECTS];
    private Vector4[] sdfSecondaryColors = new Vector4[MAX_SDF_OBJECTS];
    private float[] sdfTextureTypes = new float[MAX_SDF_OBJECTS];
    private Vector4[] sdfTextureData = new Vector4[MAX_SDF_OBJECTS];
    private float[] sdfSmoothnessValues = new float[MAX_SDF_OBJECTS];
    private float[] sdfMetallicValues = new float[MAX_SDF_OBJECTS];
    private Vector4[] sdfEmissionColors = new Vector4[MAX_SDF_OBJECTS];

    private bool hasInitialized = false;

    private void OnEnable()
    {
        Initialize();
    }

    private void OnTransformChildrenChanged()
    {
        ValidateTransformChildrenChange();
    }

    private void OnValidate()
    {
        hasInitialized = false;
        Initialize();
    }

    private void Initialize()
    {
        if (hasInitialized)
        {
            return;
        }

        sdfObjects = new SDFObject[MAX_SDF_OBJECTS];
        sdfTransformMatrices = new Matrix4x4[MAX_SDF_OBJECTS];
        sdfTypes = new float[MAX_SDF_OBJECTS];
        sdfData = new Vector4[MAX_SDF_OBJECTS];
        sdfBlendOperations = new float[MAX_SDF_OBJECTS];
        sdfBlends = new float[MAX_SDF_OBJECTS];
        sdfPrimaryColors = new Vector4[MAX_SDF_OBJECTS];
        sdfSmoothnessValues = new float[MAX_SDF_OBJECTS];
        sdfTextureTypes = new float[MAX_SDF_OBJECTS];
        sdfTextureData = new Vector4[MAX_SDF_OBJECTS];
        sdfMetallicValues = new float[MAX_SDF_OBJECTS];
        sdfEmissionColors = new Vector4[MAX_SDF_OBJECTS];

        renderer = GetComponent<Renderer>();

        materialPropertyBlock = new MaterialPropertyBlock();

        ValidateTransformChildrenChange();

        hasInitialized = true;
    }

    private void Update()
    {
        if (!hasInitialized)
        {
            return;
        }

        for (int i = 0; i < numSDFObjects; i++)
        {
            SDFObject sdf = sdfObjects[i];
            sdfTypes[i] = (int)sdf.Type;
            sdfTransformMatrices[i] = sdf.transform.worldToLocalMatrix;
            sdfData[i] = sdf.ShapeData;
            sdfBlendOperations[i] = (int)sdf.BlendOperation;
            sdfBlends[i] = sdf.BlendFactor;
            sdfPrimaryColors[i] = sdf.PrimaryColor;
            sdfSecondaryColors[i] = sdf.SecondaryColor;
            sdfTextureTypes[i] = (int)sdf.TextureType;
            sdfTextureData[i] = sdf.TextureData;
            sdfSmoothnessValues[i] = sdf.Smoothness;
            sdfMetallicValues[i] = sdf.Metallic;
            sdfEmissionColors[i] = sdf.EmissionColor;
        }

        materialPropertyBlock.SetFloatArray(SDFTypeShaderPropertyID, sdfTypes);
        materialPropertyBlock.SetMatrixArray(SDFTransformMatricesPropertyID, sdfTransformMatrices);
        materialPropertyBlock.SetVectorArray(SDFDataShaderPropertyID, sdfData);
        materialPropertyBlock.SetFloatArray(SDFBlendOperationShaderPropertyID, sdfBlendOperations);
        materialPropertyBlock.SetFloatArray(SDFBlendFactorShaderPropertyID, sdfBlends);
        materialPropertyBlock.SetInt(SDFCountShaderPropertyID, numSDFObjects);
        materialPropertyBlock.SetVectorArray(SDFPrimaryColorsPropertyID, sdfPrimaryColors);
        materialPropertyBlock.SetVectorArray(SDFSecondaryColorsPropertyID, sdfSecondaryColors);
        materialPropertyBlock.SetFloatArray(SDFTextureTypePropertyID, sdfTextureTypes);
        materialPropertyBlock.SetVectorArray(SDFTextureDataShaderPropertyID, sdfTextureData);
        materialPropertyBlock.SetFloatArray(SDFSmoothnessPropertyID, sdfSmoothnessValues);
        materialPropertyBlock.SetFloatArray(SDFMetallicPropertyID, sdfMetallicValues);
        materialPropertyBlock.SetVectorArray(SDFEmissionColorsPropertyID, sdfEmissionColors);

        renderer.SetPropertyBlock(materialPropertyBlock);
    }

    private void OnDisable()
    {
        if (!hasInitialized)
        {
            return;
        }

        hasInitialized = false;
        materialPropertyBlock.Clear();
        materialPropertyBlock = null;
        renderer.SetPropertyBlock(null);
    }

    #region HELPERS
    [ContextMenu("Add SDF Object")]
    private void AddSDFObject()
    {
        if (numSDFObjects == MAX_SDF_OBJECTS)
        {
            // Can't add any more to this SDFCollection!
            return;
        }

        SDFObject sdfObject = Instantiate(sdfObjectPrefab, transform);
        sdfObjects[numSDFObjects] = sdfObject;
        numSDFObjects++;
    }

    private void ValidateTransformChildrenChange()
    {
        SDFObject[] objects = GetComponentsInChildren<SDFObject>();
        numSDFObjects = Mathf.Clamp(objects.Length, 0, MAX_SDF_OBJECTS);
        for (int i = 0; i < numSDFObjects; i++)
        {
            sdfObjects[i] = objects[i];
        }
    }
    #endregion

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}
