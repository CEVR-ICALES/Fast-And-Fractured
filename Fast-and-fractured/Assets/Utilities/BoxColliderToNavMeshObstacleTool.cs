using UnityEngine;
using UnityEditor;
using UnityEngine.AI;  

public class BoxColliderToNavMeshObstacleTool  
{ 
    private const string GAME_OBJECT_MENU_ITEM_PATH = "Tools/Utilities/Add NavMesh Obstacle from BoxCollider";
    // Path for the BoxCollider context menu
    private const string CONTEXT_MENU_ITEM_PATH = "CONTEXT/BoxCollider/Create NavMesh Obstacle (from this)";

    // Action method for the BoxCollider context menu
    [MenuItem(CONTEXT_MENU_ITEM_PATH, false, 1500)]
    private static void CreateNavMeshObstacleFromContext(MenuCommand command)
    {
        BoxCollider boxCollider = command.context as BoxCollider;

        if (boxCollider == null)
        {
            Debug.LogError("Command was not invoked on a BoxCollider component.");
            return;
        }

        GameObject selectedObject = boxCollider.gameObject;
        ConfigureNavMeshObstacle(selectedObject, boxCollider);
    }

    // Validation for the BoxCollider context menu
    [MenuItem(CONTEXT_MENU_ITEM_PATH, true)]
    private static bool ValidateCreateNavMeshObstacleFromContext(MenuCommand command)
    {
        return command.context is BoxCollider;
    }


    // --- Opcional: Mantener también la opción en el menú GameObject ---
    [MenuItem(GAME_OBJECT_MENU_ITEM_PATH, false, 10)]
    private static void CreateNavMeshObstacleFromGameObjectMenu()
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("No Object Selected", "Please select a GameObject in the scene.", "OK");
            return;
        }

        BoxCollider boxCollider = selectedObject.GetComponent<BoxCollider>();

        if (boxCollider == null)
        {
            EditorUtility.DisplayDialog("No BoxCollider Found",
                $"The selected GameObject '{selectedObject.name}' does not have a BoxCollider component.", "OK");
            return;
        }
        ConfigureNavMeshObstacle(selectedObject, boxCollider);
    }

    [MenuItem(GAME_OBJECT_MENU_ITEM_PATH, true)]
    private static bool ValidateCreateNavMeshObstacleFromGameObjectMenu()
    {
        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject != null)
        {
            return selectedObject.GetComponent<BoxCollider>() != null;
        }
        return false;
    }
    // --- Fin de la sección opcional ---


    // --- Lógica Central para NavMeshObstacle ---
    private static void ConfigureNavMeshObstacle(GameObject targetObject, BoxCollider sourceBoxCollider)
    {
        if (targetObject == null || sourceBoxCollider == null) return;

        NavMeshObstacle navMeshObstacle = targetObject.GetComponent<NavMeshObstacle>();
        if (navMeshObstacle == null)
        {
            navMeshObstacle = Undo.AddComponent<NavMeshObstacle>(targetObject);
        }
        else
        {
            Undo.RecordObject(navMeshObstacle, "Configure NavMeshObstacle");
        }

        // Configurar el NavMeshObstacle para que coincida con el BoxCollider
        navMeshObstacle.shape = NavMeshObstacleShape.Box;
        navMeshObstacle.center = sourceBoxCollider.center; // Usa el centro local del BoxCollider

        // El tamaño del NavMeshObstacle es un Vector3, pero el BoxCollider.size
        // ya es un Vector3 que representa las dimensiones completas.
        // Hay que tener en cuenta la escala del GameObject.
        // El NavMeshObstacle.size es en el espacio local del objeto.
        navMeshObstacle.size = sourceBoxCollider.size;


        // Opciones adicionales que podrías querer configurar (descomenta y ajusta según necesidad):
        navMeshObstacle.carving = true; // Para que talle el NavMesh (si el NavMeshSurface lo permite)
        // navMeshObstacle.carveOnlyStationary = true; // Si solo debe tallar cuando está quieto
        // navMeshObstacle.carvingMoveThreshold = 0.1f; // Distancia que debe moverse antes de volver a tallar
        // navMeshObstacle.carvingTimeToStationary = 0.5f; // Tiempo que debe estar quieto para ser considerado estacionario

        EditorUtility.SetDirty(navMeshObstacle);
        Debug.Log($"NavMeshObstacle configured on '{targetObject.name}' using BoxCollider dimensions. Carving: {navMeshObstacle.carving}", targetObject);
    }
}