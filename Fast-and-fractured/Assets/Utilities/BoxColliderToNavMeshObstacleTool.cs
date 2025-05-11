using UnityEngine;
using UnityEditor;
using UnityEngine.AI;  

public class BoxColliderToNavMeshObstacleTool  
{ 
    private const string GAME_OBJECT_MENU_ITEM_PATH = "Tools/Utilities/Add NavMesh Obstacle from BoxCollider";
    private const string CONTEXT_MENU_ITEM_PATH = "CONTEXT/BoxCollider/Create NavMesh Obstacle (from this)";

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

    [MenuItem(CONTEXT_MENU_ITEM_PATH, true)]
    private static bool ValidateCreateNavMeshObstacleFromContext(MenuCommand command)
    {
        return command.context is BoxCollider;
    }


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

        navMeshObstacle.shape = NavMeshObstacleShape.Box;
        navMeshObstacle.center = sourceBoxCollider.center; 
        navMeshObstacle.size = sourceBoxCollider.size;
        navMeshObstacle.carving = true; 
        EditorUtility.SetDirty(navMeshObstacle);
        Debug.Log($"NavMeshObstacle configured on '{targetObject.name}' using BoxCollider dimensions. Carving: {navMeshObstacle.carving}", targetObject);
    }
}