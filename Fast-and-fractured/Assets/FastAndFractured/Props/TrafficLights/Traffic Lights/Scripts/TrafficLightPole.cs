using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class TrafficLightPole : MonoBehaviour
{
    [Header("Pole Settings")]
    public MeshRenderer poleRenderer;
    public Material greenPoleMaterial;
    public Material greyPoleMaterial;
    public PoleMaterialType initialPoleColor = PoleMaterialType.GREY;

    [Header("Light Materials")]
    public Material lightOnMaterial;
    public Material lightOffMaterial;

    [Header("Traffic Light Units")]
    public List<TrafficLightUnit> trafficUnits = new List<TrafficLightUnit>();

    [Header("Runtime Control")]
    public bool activateOnStart = true;
    public bool setInitialStateOnStart = true;

    private bool _isSystemActive = false;

    void Awake()
    {
        InitializePoleMaterial();
        InitializeLightUnits();
    }

    void Start()
    {
        if (setInitialStateOnStart && !activateOnStart)
        {
            SetUnitsToInitialState();
        }

        if (activateOnStart)
        {
            ActivateSystem();
        }
        else
        {
            if (!setInitialStateOnStart) DeactivateSystem(false);
        }
    }

    public void InitializePoleMaterial()
    {
        if (poleRenderer != null)
        {
            poleRenderer.material = initialPoleColor == PoleMaterialType.GREEN ? greenPoleMaterial : greyPoleMaterial;
        }
    }

    public void InitializeLightUnits()
    {
        if (lightOnMaterial == null || lightOffMaterial == null)
        {
            Debug.LogError("Missing LightOnMaterial or LightOffMaterial", this);
            return;
        }

        foreach (var unit in trafficUnits)
        {
            if (unit != null)
            {
                unit.Initialize(lightOnMaterial, lightOffMaterial);
            }
        }
    }

    public void SetUnitsToInitialState()
    {
        foreach (var unit in trafficUnits)
        {
            if (unit != null)
            {
                unit.SetInitialState();
            }
        }
    }


    public void ActivateSystem()
    {
        if (_isSystemActive) return;
        _isSystemActive = true;
        foreach (var unit in trafficUnits)
        {
            if (unit != null) unit.ActivateUnit();
        }
    }

    public void DeactivateSystem(bool turnLightsOff = true)
    {
        if (!_isSystemActive && !turnLightsOff) return;
        _isSystemActive = false;
        foreach (var unit in trafficUnits)
        {
            if (unit != null)
            {
                unit.DeactivateUnit();
            }
        }
    }
#if UNITY_EDITOR
    public void Editor_CollectUnitsAndLamps()
    {
        trafficUnits.Clear();

        TrafficLightUnit[] existingUnits = GetComponentsInChildren<TrafficLightUnit>(true);
        foreach (var unit in existingUnits)
        {
            if (unit.transform.IsChildOf(this.transform) && unit != this.GetComponent<TrafficLightUnit>())
            {
                if (!trafficUnits.Contains(unit))
                {
                    trafficUnits.Add(unit);
                }
            }
        }

        if (trafficUnits.Count == 0)
        {
            Queue<Transform> toSearch = new Queue<Transform>();
            toSearch.Enqueue(transform);

            List<Transform> potentialUnitRoots = new List<Transform>();

            while (toSearch.Count > 0)
            {
                Transform current = toSearch.Dequeue();

                bool hasRendererChildren = false;
                int childMeshRenderers = 0;
                for (int i = 0; i < current.childCount; i++)
                {
                    Transform child = current.GetChild(i);
                    if (child.GetComponent<MeshRenderer>() != null)
                    {
                        hasRendererChildren = true;
                        childMeshRenderers++;
                    }
                    if (child.GetComponent<TrafficLightLamp>() == null)
                    {
                        toSearch.Enqueue(child);
                    }
                }

                if (current != this.transform &&
                    current.GetComponent<MeshRenderer>() == null &&
                    hasRendererChildren &&
                    childMeshRenderers > 1 &&
                    current.GetComponent<TrafficLightUnit>() == null &&
                    current.GetComponent<TrafficLightLamp>() == null)
                {

                    potentialUnitRoots.Add(current);
                }
            }

            foreach (var unitRoot in potentialUnitRoots)
            {
                bool ancestorIsAlreadyUnit = false;
                Transform tempParent = unitRoot.parent;
                while (tempParent != null && tempParent != this.transform)
                {
                    if (potentialUnitRoots.Contains(tempParent))
                    {
                        ancestorIsAlreadyUnit = true;
                        break;
                    }
                    tempParent = tempParent.parent;
                }
                if (ancestorIsAlreadyUnit) continue;


                TrafficLightUnit unitComponent = unitRoot.gameObject.GetComponent<TrafficLightUnit>();
                if (unitComponent == null)
                {
                    unitComponent = unitRoot.gameObject.AddComponent<TrafficLightUnit>();
                }
                if (!trafficUnits.Contains(unitComponent))
                {
                    trafficUnits.Add(unitComponent);
                }
            }
        }

        foreach (var unit in trafficUnits)
        {
            if (unit != null)
            {

                unit.Editor_CollectLamps();
            }
        }
        EditorUtility.SetDirty(this);
        foreach (var unit in trafficUnits) if (unit != null) EditorUtility.SetDirty(unit);
    }
    public void Editor_ApplyInitialPoleColor()
    {
        InitializePoleMaterial();
    }

    public void Editor_ApplyInitialLightStates()
    {
        if (lightOnMaterial == null || lightOffMaterial == null)
        {
            Debug.LogError("Missing LightOnMaterial or LightOffMaterial", this);
            return;
        }
        foreach (var unit in trafficUnits)
        {
            if (unit != null)
            {
                unit.Initialize(lightOnMaterial, lightOffMaterial);
                unit.SetInitialState();
            }
        }
    }
#endif
}