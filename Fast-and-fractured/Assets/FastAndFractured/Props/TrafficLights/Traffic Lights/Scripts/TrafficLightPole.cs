using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public class TrafficLightPole : MonoBehaviour
{
    [Header("Pole Settings")]
    [Tooltip("Renderer de la malla del poste del sem?foro.")]
    public MeshRenderer poleRenderer;
    [Tooltip("Material para el poste cuando se configura como 'Verde'.")]
    public Material greenPoleMaterial;
    [Tooltip("Material para el poste cuando se configura como 'Gris'.")]
    public Material greyPoleMaterial;
    [Tooltip("Color inicial del poste.")]
    public PoleMaterialType initialPoleColor = PoleMaterialType.Grey;

    [Header("Light Materials")]
    [Tooltip("Material a usar cuando una luz est? ENCENDIDA. Este material debe tener la emisi?n configurada para brillar.")]
    public Material lightOnMaterial;
    [Tooltip("Material a usar cuando una luz est? APAGADA.")]
    public Material lightOffMaterial;
    // Ya no necesitamos LightEmissiveColor si los materiales ON/OFF ya est?n definidos.

    [Header("Traffic Light Units")]
    [Tooltip("Lista de unidades de sem?foro controladas por este poste. Rellenar en el editor o usar 'Auto-Collect Units'.")]
    public List<TrafficLightUnit> trafficUnits = new List<TrafficLightUnit>();

    [Header("Runtime Control")]
    [Tooltip("Si el sem?foro debe empezar a funcionar autom?ticamente al iniciar la escena.")]
    public bool activateOnStart = true;
    [Tooltip("Establece el estado inicial de las luces al iniciar (el primer estado de su secuencia). Si est? desactivado, las luces comenzar?n apagadas hasta que se active el sistema.")]
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
            // Asegurarse de que todo est? apagado si no se activa al inicio y no se establece estado inicial
            if (!setInitialStateOnStart) DeactivateSystem(false); // false para no apagar luces si ya est?n en estado inicial
        }
    }

    public void InitializePoleMaterial()
    {
        if (poleRenderer != null)
        {
            poleRenderer.material = initialPoleColor == PoleMaterialType.Green ? greenPoleMaterial : greyPoleMaterial;
        }
    }

    public void InitializeLightUnits()
    {
        if (lightOnMaterial == null || lightOffMaterial == null)
        {
            Debug.LogError($"TrafficLightPole en {gameObject.name} no tiene asignados LightOnMaterial y/o LightOffMaterial. Las luces no funcionar?n.", gameObject);
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
                unit.SetInitialState(); // Establece el primer estado de la secuencia
            }
        }
    }


    public void ActivateSystem()
    {
        if (_isSystemActive) return;
        _isSystemActive = true;
        Debug.Log($"Sistema de sem?foros {gameObject.name} activado.");
        foreach (var unit in trafficUnits)
        {
            if (unit != null) unit.ActivateUnit();
        }
    }

    public void DeactivateSystem(bool turnLightsOff = true)
    {
        if (!_isSystemActive && !turnLightsOff) return; // Si ya est? inactivo y no queremos forzar apagado, salir
        _isSystemActive = false;
        Debug.Log($"Sistema de sem?foros {gameObject.name} desactivado.");
        foreach (var unit in trafficUnits)
        {
            if (unit != null)
            {
                unit.DeactivateUnit(); // Esto ya apaga las luces de la unidad
            }
        }
    }
    // En TrafficLightPole.cs
#if UNITY_EDITOR
    public void Editor_CollectUnitsAndLamps()
    {
        trafficUnits.Clear();

        // 1. Buscar TrafficLightUnit existentes en toda la jerarquía descendiente.
        TrafficLightUnit[] existingUnits = GetComponentsInChildren<TrafficLightUnit>(true);
        foreach (var unit in existingUnits)
        {
            // Solo añadir si aún no está en la lista (GetComponentsInChildren puede devolver el mismo si hay múltiples llamadas o estructuras complejas)
            // y asegurarnos de que no estamos añadiendo unidades de un sub-poste si alguna vez se anidaran (poco probable para este caso).
            // La condición principal es que sea un descendiente.
            if (unit.transform.IsChildOf(this.transform) && unit != this.GetComponent<TrafficLightUnit>()) // Evitar añadirse a sí mismo si tuviera el componente
            {
                if (!trafficUnits.Contains(unit))
                {
                    trafficUnits.Add(unit);
                }
            }
        }

        // 2. Fallback: Si no se encontraron unidades con el componente, intentar identificar
        //    GameObjects que deberían ser unidades y añadirles el componente.
        //    Esto es útil para una configuración inicial más rápida.
        if (trafficUnits.Count == 0)
        {
            Queue<Transform> toSearch = new Queue<Transform>();
            toSearch.Enqueue(transform); // Empezar la búsqueda desde el propio poste

            List<Transform> potentialUnitRoots = new List<Transform>();

            while (toSearch.Count > 0)
            {
                Transform current = toSearch.Dequeue();
                // Un candidato a "unidad" podría ser un GameObject que:
                // - No tenga él mismo un MeshRenderer (las unidades son contenedores).
                // - Tenga hijos que SÍ tengan MeshRenderers (las luces).
                // - No tenga ya un TrafficLightUnit o TrafficLightLamp.
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
                    // Añadir hijos a la cola para búsqueda recursiva, si no son hojas obvias de luz
                    if (child.GetComponent<TrafficLightLamp>() == null)
                    { // No seguir buscando dentro de lo que ya es una lámpara
                        toSearch.Enqueue(child);
                    }
                }

                if (current != this.transform && // No el poste raíz en sí mismo
                    current.GetComponent<MeshRenderer>() == null &&
                    hasRendererChildren && // Tiene al menos un hijo con renderer
                    childMeshRenderers > 1 && // Heurística: una unidad suele tener más de una luz
                    current.GetComponent<TrafficLightUnit>() == null &&
                    current.GetComponent<TrafficLightLamp>() == null)
                {
                    // Podría ser una unidad. Lo añadimos como candidato.
                    // Podríamos tener una estructura como Models -> Semaforo1. Semaforo1 es la unidad.
                    potentialUnitRoots.Add(current);
                }
            }

            // Añadir TrafficLightUnit a los candidatos identificados
            foreach (var unitRoot in potentialUnitRoots)
            {
                // Evitar añadir si un ancestro ya fue marcado como unidad (prevenir unidades anidadas incorrectamente por el discovery)
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
                { // Doble chequeo por si acaso
                    unitComponent = unitRoot.gameObject.AddComponent<TrafficLightUnit>();
                }
                if (!trafficUnits.Contains(unitComponent))
                {
                    trafficUnits.Add(unitComponent);
                }
            }
        }

        // 3. Para cada unidad encontrada o creada, recolectar sus lámparas.
        foreach (var unit in trafficUnits)
        {
            if (unit != null)
            {
                // Pasar los materiales de luz del poste a la unidad para su inicialización, si no lo hacemos aquí
                // la unidad no tendrá los materiales correctos cuando llame a Editor_CollectLamps y trate de inicializar.
                // Es mejor que la unidad los tome en su Initialize real.
                unit.Editor_CollectLamps();
            }
        }
        // Mark dirty para que los cambios se guarden
        EditorUtility.SetDirty(this);
        foreach (var unit in trafficUnits) if (unit != null) EditorUtility.SetDirty(unit);
    }
    public void Editor_ApplyInitialPoleColor()
    {
        InitializePoleMaterial();
    }

    public void Editor_ApplyInitialLightStates()
    {
        // Primero, asegurarse de que los materiales est?n disponibles para las unidades
        if (lightOnMaterial == null || lightOffMaterial == null)
        {
            Debug.LogError("Asigna LightOnMaterial y LightOffMaterial en el TrafficLightPole antes de aplicar estados iniciales.", this);
            return;
        }
        foreach (var unit in trafficUnits)
        {
            if (unit != null)
            {
                // Re-inicializar con los materiales actuales del pole por si han cambiado
                unit.Initialize(lightOnMaterial, lightOffMaterial);
                unit.SetInitialState();
            }
        }
    }
#endif
}