using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Data class to hold settings for each mesh piece used in the generation.
/// This allows for unique offsets, rotations, and scales per prefab.
/// </summary>
[Serializable]
public class OffsetForDeformingMeshes
{
    public GameObject actualObject;
    public Vector3 translation = Vector3.zero;
    public Vector3 scale = Vector3.one;
    public Quaternion rotation = Quaternion.identity;
    public float nextObjectOffset = 0f;
}

/// <summary>
/// This component procedurally generates a mesh by instantiating and combining a series of prefabs,
/// and then deforms that mesh to follow the path of a Unity Spline.
/// It uses the C# Job System for high-performance deformation.
/// </summary>
public class SplineMeshDeformer : MonoBehaviour
{
    [Header("Source Spline")]
    [Tooltip("The SplineContainer that defines the path for the mesh deformation.")]
    public SplineContainer splineContainer;

    [Header("Mesh Prefabs")]
    [Tooltip("The prefab to use at the start of the spline.")]
    public OffsetForDeformingMeshes startPrefab;
    [Tooltip("A list of prefabs to be used repeatedly for the middle sections.")]
    public List<OffsetForDeformingMeshes> inBetweenPrefabs = new List<OffsetForDeformingMeshes>();
    [Tooltip("The prefab to use at the end of the spline.")]
    public OffsetForDeformingMeshes endPrefab;

    [Header("Generation Settings")]
    [Tooltip("A base GameObject to instantiate for the final mesh. If null, an empty one is created.")]
    public GameObject basePrefab;
    [Tooltip("The parent transform for the generated mesh GameObjects.")]
    public Transform parent;
    public string layerForObject = "Default";
    public string tagForObject = "Untagged";

    [Header("Mesh Processing")]
    public bool makeObjectStatic = false;
    public bool addMeshCollider = false;
    public bool makeMeshColliderConvex = false;
    public bool makeMeshColliderTrigger = false;
    public bool disableMeshRenderer = false;
    public bool recalculateNormals = false;
    public float combinationSplitLength = -1;
    public bool evenlySplitsWhenMakingSubCurves = true;

    [Header("Transform Modifiers")]
    public Vector3 axisForOffset;
    public Quaternion rotationToApply = Quaternion.identity;

    [Header("Debug")]
    public bool debugAcumulation = false;

    [Header("Output")]
    [SerializeField]
    private List<GameObject> lastMeshesObjs = new List<GameObject>();

    private struct JobDeformationData : IDisposable
    {
        public GameObject targetObject;
        public Mesh targetMeshInstance;
        public JobHandle handle;
        public CalculateJob job;

        public void Dispose()
        {
            job.Dispose();
        }
    }

    private void Reset()
    {
        splineContainer = GetComponent<SplineContainer>();
        parent = transform;
    }

    [ContextMenu("Process")]
    public void Process()
    {
        if (splineContainer == null || splineContainer.Spline == null || splineContainer.Spline.Count < 2)
        {
            Debug.LogWarning("SplineContainer is not set or the spline has fewer than 2 knots.", this);
            return;
        }

        Clear();

        var spline = splineContainer.Spline;
        float totalLength = splineContainer.CalculateLength();
        if (totalLength <= 0.01f) return;

        int amountOfSplitSteps = Mathf.CeilToInt(combinationSplitLength > 0 ? (totalLength / combinationSplitLength) : 1);

        var jobDataList = new List<JobDeformationData>(amountOfSplitSteps);
        var meshGameObjects = new List<GameObject>();

        try
        {
            for (int i = 0; i < amountOfSplitSteps; i++)
            {
                GetSegment(totalLength, amountOfSplitSteps, i, out float startDist, out float endDist);
                float segmentLength = endDist - startDist;
                if (segmentLength <= 0.001f) continue;

                float startT = SplineUtility.GetNormalizedInterpolation(spline, startDist, PathIndexUnit.Distance);
                float endT = SplineUtility.GetNormalizedInterpolation(spline, endDist, PathIndexUnit.Distance);

                GameObject meshGO = CreateBaseGameObject(i);
                meshGameObjects.Add(meshGO);

                GameObject temporaryHolder = InstantiateMeshesForLength(segmentLength);
                CombineAndGenerateNMeshObjects(temporaryHolder, meshGO);

                var meshFilter = meshGO.GetComponentInChildren<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                Mesh meshInstance = Instantiate(meshFilter.sharedMesh);
                meshFilter.mesh = meshInstance;

                var jobData = ScheduleDeformationJob(meshGO, meshInstance, spline, startT, endT);
                if (!jobData.handle.IsCompleted) jobDataList.Add(jobData);
            }

            var handles = new NativeArray<JobHandle>(jobDataList.Select(j => j.handle).ToArray(), Allocator.Temp);
            if (handles.Length > 0)
            {
                JobHandle.CompleteAll(handles);
            }
            handles.Dispose();

            foreach (var jobData in jobDataList)
            {
                ApplyJobResultsToMesh(jobData);
                FinalizeGameObject(jobData.targetObject);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during spline mesh generation: {e.Message}\n{e.StackTrace}", this);
            foreach (var go in meshGameObjects)
            {
                if (go != null) DestroyImmediate(go);
            }
        }
        finally
        {
            foreach (var jobData in jobDataList)
            {
                jobData.Dispose();
            }
        }
    }

    private GameObject InstantiateMeshesForLength(float targetLength)
    {
        GameObject objectAccumulation = new GameObject("Temporary Mesh Holder");
        objectAccumulation.transform.SetParent(transform, false);
        objectAccumulation.transform.localPosition = Vector3.zero;

        float zPos = 0f;
        int prefabCounter = 0;
        const int maxPrefabs = 5000;

        if (startPrefab != null && startPrefab.actualObject != null)
        {
            zPos += InstantiateOnePrefabAndAdvance(startPrefab, objectAccumulation.transform, zPos);
            prefabCounter++;
        }

        float endPieceLength = 0f;
        if (endPrefab != null && endPrefab.actualObject != null)
        {
            Bounds endBounds = GetMaxBounds(endPrefab.actualObject);
            endPieceLength = endBounds.size.z * endPrefab.scale.z;
        }

        if (inBetweenPrefabs != null && inBetweenPrefabs.Count > 0)
        {
            while (zPos < (targetLength - endPieceLength) && prefabCounter < maxPrefabs)
            {
                OffsetForDeformingMeshes prefabToUse = inBetweenPrefabs[prefabCounter % inBetweenPrefabs.Count];
                if (prefabToUse == null || prefabToUse.actualObject == null)
                {
                    prefabCounter++;
                    continue;
                }
                zPos += InstantiateOnePrefabAndAdvance(prefabToUse, objectAccumulation.transform, zPos);
                prefabCounter++;
            }
        }

        if (endPrefab != null && endPrefab.actualObject != null)
        {
            InstantiateOnePrefabAndAdvance(endPrefab, objectAccumulation.transform, zPos);
        }

        objectAccumulation.transform.SetParent(null);
        return objectAccumulation;
    }

    private float InstantiateOnePrefabAndAdvance(OffsetForDeformingMeshes prefabInfo, Transform parent, float currentZPos)
    {
        GameObject objectInstance = Instantiate(prefabInfo.actualObject);

        objectInstance.transform.localScale = Vector3.Scale(objectInstance.transform.localScale, prefabInfo.scale);
        objectInstance.transform.localRotation *= prefabInfo.rotation;

        Bounds objectBounds = GetMaxBounds(objectInstance);
        float pivotOffset = objectBounds.center.z - objectBounds.min.z;

        objectInstance.transform.position = new Vector3(0, 0, currentZPos + pivotOffset) + prefabInfo.translation + axisForOffset;
        objectInstance.transform.SetParent(parent, false);

        float advanceAmount = objectBounds.size.z + prefabInfo.nextObjectOffset;

        return Mathf.Max(advanceAmount, 0.001f);
    }

    private void GetSegment(float totalLength, int amountOfSplitSteps, int index, out float startDist, out float endDist)
    {
        if (amountOfSplitSteps > 1)
        {
            if (evenlySplitsWhenMakingSubCurves)
            {
                startDist = (totalLength / amountOfSplitSteps) * index;
                endDist = (totalLength / amountOfSplitSteps) * (index + 1);
            }
            else
            {
                startDist = index * combinationSplitLength;
                endDist = Mathf.Min((index + 1) * combinationSplitLength, totalLength);
            }
        }
        else
        {
            startDist = 0;
            endDist = totalLength;
        }
    }

    private void CombineAndGenerateNMeshObjects(GameObject objectAccumulator, GameObject resultObject)
    {
        var meshFilters = objectAccumulator.GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0)
        {
            if (!debugAcumulation) DestroyImmediate(objectAccumulator);
            return;
        }

        var combineInstances = new List<CombineInstance>();
        var materials = new List<Material>();
        var worldToLocal = resultObject.transform.worldToLocalMatrix;

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            var renderer = mf.GetComponentInChildren<MeshRenderer>();
            if (renderer == null || !renderer.enabled) continue;

            for (int i = 0; i < mf.sharedMesh.subMeshCount; i++)
            {
                combineInstances.Add(new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    subMeshIndex = i,
                    transform = worldToLocal * mf.transform.localToWorldMatrix
                });
            }
            materials.AddRange(renderer.sharedMaterials);
        }

        var resultFilter = resultObject.GetComponent<MeshFilter>() ?? resultObject.AddComponent<MeshFilter>();
        var resultRenderer = resultObject.GetComponent<MeshRenderer>() ?? resultObject.AddComponent<MeshRenderer>();

        var combinedMesh = new Mesh();
        if (combineInstances.Sum(c => c.mesh.vertexCount) > 65535)
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        combinedMesh.CombineMeshes(combineInstances.ToArray(), false, true);
        resultFilter.sharedMesh = combinedMesh;
        resultRenderer.sharedMaterials = materials.ToArray();

        if (!debugAcumulation) DestroyImmediate(objectAccumulator);
    }

    private void FinalizeGameObject(GameObject meshGameObject)
    {
        if (meshGameObject == null) return;
        if (parent) meshGameObject.transform.SetParent(parent, false);

        meshGameObject.transform.localPosition = Vector3.zero;
        meshGameObject.transform.localRotation = Quaternion.identity;
        meshGameObject.layer = LayerMask.NameToLayer(string.IsNullOrEmpty(layerForObject) ? "Default" : layerForObject);
        meshGameObject.tag = string.IsNullOrEmpty(tagForObject) ? "Untagged" : tagForObject;
        meshGameObject.isStatic = makeObjectStatic;

        if (addMeshCollider)
        {
            var meshCollider = meshGameObject.GetComponent<MeshCollider>() ?? meshGameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshGameObject.GetComponent<MeshFilter>().mesh;
            meshCollider.convex = makeMeshColliderConvex;
            meshCollider.isTrigger = makeMeshColliderTrigger;
        }

        if (disableMeshRenderer)
        {
            var meshRenderer = meshGameObject.GetComponent<MeshRenderer>();
            if (meshRenderer) meshRenderer.enabled = false;
        }

#if UNITY_EDITOR
        Undo.RecordObject(this, "Add Last Generated Mesh Object");
#endif
        lastMeshesObjs.Add(meshGameObject);
    }

    private GameObject CreateBaseGameObject(int index)
    {
        GameObject go = basePrefab ? Instantiate(basePrefab) : new GameObject("GeneratedMesh_" + index);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(go, "Create Mesh Holder");
#endif
        return go;
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        foreach (var meshObj in lastMeshesObjs)
        {
            if (meshObj != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) Undo.DestroyObjectImmediate(meshObj); else Destroy(meshObj);
#else
                Destroy(meshObj);
#endif
            }
        }
        lastMeshesObjs.Clear();
    }

    #region JOBS

    [Unity.Burst.BurstCompile]
    public struct CalculateJob : IJobParallelFor, IDisposable
    {
        [ReadOnly] public NativeArray<Vector3> sourceVertices;
        [ReadOnly] public NativeArray<Vector3> sourceNormals;
        [ReadOnly] public NativeSpline spline;
        [ReadOnly] public float jobMinZ;
        [ReadOnly] public float jobMaxZ;
        [ReadOnly] public float jobStartT;
        [ReadOnly] public float jobEndT;
        [ReadOnly] public float4x4 objectWorldToLocalMatrix;

        [WriteOnly] public NativeArray<Vector3> vertices;
        [WriteOnly] public NativeArray<Vector3> normals;

        public void Execute(int i)
        {
            float mappedValue = math.unlerp(jobMinZ, jobMaxZ, sourceVertices[i].z);
            float t = math.lerp(jobStartT, jobEndT, mappedValue);

            spline.Evaluate(t, out float3 position, out float3 forward, out float3 up);

            if (math.lengthsq(forward) < 0.0001f) forward = new float3(0, 0, 1);
            if (math.lengthsq(up) < 0.0001f) up = new float3(0, 1, 0);

            quaternion rotation = quaternion.LookRotation(forward, up);

            float3 localOffset = sourceVertices[i];
            localOffset.z = 0;

            float3 deformedVertexWorld = position + math.mul(rotation, localOffset);

            float3 deformedNormalWorld = math.mul(rotation, sourceNormals[i]);

            vertices[i] = math.transform(objectWorldToLocalMatrix, deformedVertexWorld);
            normals[i] = math.rotate(objectWorldToLocalMatrix, deformedNormalWorld);
        }

        public void Dispose()
        {
            if (sourceVertices.IsCreated) sourceVertices.Dispose();
            if (sourceNormals.IsCreated) sourceNormals.Dispose();
          //  if (spline.IsCreated) spline.Dispose();
        }
    }

    private JobDeformationData ScheduleDeformationJob(GameObject meshGameObject, Mesh meshInstance, Spline spline, float startT, float endT)
    {
        if (meshInstance == null) return new JobDeformationData { handle = new JobHandle() };

        int vertexCount = meshInstance.vertexCount;
        if (vertexCount == 0) return new JobDeformationData { handle = new JobHandle() };

        var dataArray = Mesh.AcquireReadOnlyMeshData(meshInstance);
        var data = dataArray[0];

        var sourceVertices = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
        data.GetVertices(sourceVertices);
        var sourceNormals = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
        data.GetNormals(sourceNormals);
        dataArray.Dispose();

        float jobMinZ = float.MaxValue;
        float jobMaxZ = float.MinValue;
        FindMinMaxZ(sourceVertices, ref jobMinZ, ref jobMaxZ);

        var vertices = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
        var normals = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);

        var job = new CalculateJob
        {
            sourceVertices = sourceVertices,
            sourceNormals = sourceNormals,
          
            spline = new NativeSpline(spline, splineContainer.transform.localToWorldMatrix, Allocator.Persistent),
            jobMinZ = jobMinZ,
            jobMaxZ = jobMaxZ,
            jobStartT = startT,
            jobEndT = endT,
            objectWorldToLocalMatrix = meshGameObject.transform.worldToLocalMatrix,
            vertices = vertices,
            normals = normals
        };

        var handle = job.Schedule(vertexCount, 32);

        return new JobDeformationData
        {
            targetObject = meshGameObject,
            targetMeshInstance = meshInstance,
            handle = handle,
            job = job
        };
    }

    private void ApplyJobResultsToMesh(JobDeformationData data)
    {
        var mesh = data.targetMeshInstance;
        if (mesh == null) return;

        mesh.SetVertices(data.job.vertices);
        mesh.SetNormals(data.job.normals);

        mesh.RecalculateBounds();
        if (recalculateNormals)
        {
            mesh.RecalculateNormals();
        }
        mesh.RecalculateTangents();
        mesh.UploadMeshData(false);
    }

    private void FindMinMaxZ(NativeArray<Vector3> vertices, ref float minZ, ref float maxZ)
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            float z = vertices[i].z;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }
    }
    #endregion

    #region UTILITIES
    private static Bounds GetMaxBounds(GameObject g)
    {
        var renderers = g.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(g.transform.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            b.Encapsulate(renderers[i].bounds);
        }
        return b;
    }
    #endregion

#if UNITY_EDITOR
    private bool AssertModelHasReadWriteEnabled(OffsetForDeformingMeshes offset)
    {
        if (offset == null || offset.actualObject == null) return true;
        foreach (var mf in offset.actualObject.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh != null)
            {
                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!string.IsNullOrEmpty(path))
                {
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer != null && !importer.isReadable) return false;
                }
            }
        }
        return true;
    }

    public bool AssertAllModelsHaveReadWriteEnabled()
    {
        bool allOk = AssertModelHasReadWriteEnabled(startPrefab);
        allOk &= AssertModelHasReadWriteEnabled(endPrefab);
        foreach (var prefab in inBetweenPrefabs) allOk &= AssertModelHasReadWriteEnabled(prefab);
        return allOk;
    }

    private void FixModelReadWriteToggle(OffsetForDeformingMeshes offset)
    {
        if (offset == null || offset.actualObject == null) return;
        foreach (var mf in offset.actualObject.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh != null)
            {
                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!string.IsNullOrEmpty(path))
                {
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer != null && !importer.isReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                }
            }
        }
    }

    public void FixAllModelsReadWriteToggle()
    {
        FixModelReadWriteToggle(startPrefab);
        FixModelReadWriteToggle(endPrefab);
        foreach (var prefab in inBetweenPrefabs) FixModelReadWriteToggle(prefab);
        Debug.Log("Finished attempting to fix 'Read/Write Enabled' on all models.");
    }
#endif
}

