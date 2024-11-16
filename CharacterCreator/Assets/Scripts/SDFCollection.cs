using System.Runtime.CompilerServices;
using UnityEditor.UI;
using UnityEngine;
using Utkarsh.UnityCore.ShaderUtils;

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class SDFCollection : MonoBehaviour
{
    #region Shader Properties
    private const int MAX_SDF_OBJECTS = 128;
    private static readonly int SDFCountTotalShaderPropertyID = Shader.PropertyToID("SDFCountTotal");
    private static readonly int SDFCountCompoundedShaderPropertyID = Shader.PropertyToID("SDFCountCompounded");
    private static readonly int SDFOffsetsShaderPropertyID = Shader.PropertyToID("OffsetsToNextSdf");
    private static readonly int SDFObjectsPropertyID = Shader.PropertyToID("SDFObjects");
    #endregion

    public SDFObject[] SdfObjects { get; private set; }
    private SDFObject_GPU[] sdfObjects_GPU;
    private SDFObjectWithMaterialProperties_GPU[] sdfObjectsWithMaterialProperties_GPU;
    private int sdfCountTotal;      // compound objects are counted = number of the children in that compound
    public int SdfCountCompounded { get; private set; } // compound objects are counted as 1 object here

    [SerializeField] private SDFObject sdfObjectPrefab;

    private Renderer renderer;
    private MaterialPropertyBlock materialPropertyBlock;

    public int[] OffsetsToNextSdf { get; private set; } = new int[MAX_SDF_OBJECTS];

    private bool hasInitialized = false;

    private void OnEnable()
    {
        Initialize();
    }

    private void OnTransformChildrenChanged()
    {
        // OnTransformChildrenChanged() is NOT fired if a grand-child changes.
        // Only the immediate child hierarchy below this parent is evaluated and fires this.
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

        SdfObjects = new SDFObject[MAX_SDF_OBJECTS];
        sdfObjects_GPU = new SDFObject_GPU[MAX_SDF_OBJECTS];
        sdfObjectsWithMaterialProperties_GPU = new SDFObjectWithMaterialProperties_GPU[MAX_SDF_OBJECTS];
        OffsetsToNextSdf = new int[MAX_SDF_OBJECTS];

        renderer = GetComponent<Renderer>();

        materialPropertyBlock = new MaterialPropertyBlock();

        ValidateTransformChildrenChange();

        SDFObject.OnChildrenUpdated += OnCompoundSDFObjectUpdated;

        hasInitialized = true;
    }

    private void Update()
    {
        if (!hasInitialized)
        {
            return;
        }

        for (int i = 0; i < sdfCountTotal; i++)
        {
            SDFObject sdf = SdfObjects[i];
            sdfObjects_GPU[i] = sdf.ToGPUStruct();
            sdfObjectsWithMaterialProperties_GPU[i] = sdf.ToGPUStructWithMaterialProperties();
        }

        materialPropertyBlock.SetInt(SDFCountTotalShaderPropertyID, sdfCountTotal);
        materialPropertyBlock.SetInt(SDFCountCompoundedShaderPropertyID, SdfCountCompounded);

        // Build buffers
        ComputeBuffer sdfObjectsGpuBuffer = ShaderUtility.BuildComputeBuffer(sdfObjectsWithMaterialProperties_GPU);
        ComputeBuffer offsetsToNextSdfBuffer = ShaderUtility.BuildComputeBuffer(OffsetsToNextSdf);

        materialPropertyBlock.SetBuffer(SDFObjectsPropertyID, sdfObjectsGpuBuffer);
        materialPropertyBlock.SetBuffer(SDFOffsetsShaderPropertyID, offsetsToNextSdfBuffer);

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

    private void OnDestroy()
    {
        SDFObject.OnChildrenUpdated -= OnCompoundSDFObjectUpdated;
    }

    #region HELPERS
    [ContextMenu("Add SDF Object")]
    private void AddSDFObject()
    {
        if (SdfCountCompounded == MAX_SDF_OBJECTS)
        {
            // Can't add any more to this SDFCollection!
            return;
        }

        SDFObject sdfObject = InstantiateSDFObject(transform);
    }

    public SDFObject InstantiateSDFObject(Transform parent)
    {
        SDFObject sdfObject = Instantiate(sdfObjectPrefab, parent);
        sdfObject.SetParentCollection(this);
        return sdfObject;
    }

    private void OnCompoundSDFObjectUpdated(SDFCollection parentCollection)
    {
        if (parentCollection != this)
        {
            // someone else's sdf child
            return;
        }

        ValidateTransformChildrenChange();
    }

    private void ValidateTransformChildrenChange()
    {
        SDFObject[] objects = GetComponentsInChildren<SDFObject>();
        int childCount = Mathf.Clamp(objects.Length, 0, MAX_SDF_OBJECTS);

        sdfCountTotal = 0;
        SdfCountCompounded = 0;
        
        for (int i = 0; i < childCount; i++)
        {
            SDFObject curObject = objects[i];
            if (curObject.IsChildOfCompoundSDF)
            {
                // this case happens when a child object is destroyed
                continue;
            }
            OffsetsToNextSdf[SdfCountCompounded] = sdfCountTotal + curObject.NumSDFChildren + 1;
            SdfObjects[sdfCountTotal] = curObject;
            curObject.SetParentCollection(this);
            if (curObject.Type == SDFObject.SDFObjectType.Compound)
            {
                // for compound objects we don't send both parent and children to the GPU
                InsertCompoundObject(curObject);

                i += curObject.NumSDFChildren;
            }
            sdfCountTotal += curObject.NumSDFChildren + 1;
            SdfCountCompounded++;
        }
    }

    private void InsertCompoundObject(SDFObject compoundSdf)
    {
        for (int i = 0; i < compoundSdf.NumSDFChildren; i++)
        {
            SDFObject compObjChild = compoundSdf.SDFChildren[i];
            SdfObjects[sdfCountTotal + i + 1] = compObjChild;
            compObjChild.SetParentCollection(this);
        }
    }
    #endregion

    [ContextMenu("Generate Mesh")]
    public void Meshify()
    {
        GameObject go = new GameObject("Mesh_" + this.name);
        MarchingCubes cubes = go.AddComponent<MarchingCubes>();
        SDFEvaluator sdfEvaluator = new SDFEvaluator();
        sdfEvaluator.Initialize(this);
        cubes.PerformMarchingCubes(new AABB { LowerLeftCorner = new Vector3(-1, -1, -1),
                                              UpperRightCorner = new Vector3(1, 1, 1) },
                                   sdfEvaluator.sceneSdf, 10, true);
    }
    private void TraverseCompound(int index, int lastIndex)
    {
        for (int i = index; i < lastIndex; i++)
        {
            int crap = 0;
        }
    }

    [ContextMenu("Run test")]
    private void Traverse()
    {
        int i = 0;
        int elementIndex = 0;
        int sdfsDone = 0;
        while (sdfsDone < SdfCountCompounded)
        {
            elementIndex = i;
            if (SdfObjects[i].Type == SDFObject.SDFObjectType.Compound)
            {
                elementIndex++;
            }
            TraverseCompound(elementIndex, (int)OffsetsToNextSdf[sdfsDone]);
            i = (int)OffsetsToNextSdf[sdfsDone];
            sdfsDone++;
        }

        Debug.Log("done");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}
