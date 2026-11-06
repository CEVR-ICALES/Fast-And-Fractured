using UnityEngine;
using UnityEditor;
using FastAndFractured;

/// <summary>Scene View right-click entries that move the player's car to the scene camera or to the clicked point.</summary>
[InitializeOnLoad]
public static class PlayerSceneViewEditor
{
    private const float MAX_RAYCAST_DISTANCE = 10000f;

    private const string SCENE_CONTEXT_MENU = "CONTEXT/GameObjectToolContext/";

    private static Ray _lastRightClickRay;

    static PlayerSceneViewEditor()
    {
        SceneView.duringSceneGui += RememberRightClick;
    }

    private static void RememberRightClick(SceneView sceneView)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 1)
        {
            _lastRightClickRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        }
    }

    [MenuItem(SCENE_CONTEXT_MENU + "Move Player Here (Match Camera)")]
    private static void ContextMovePlayerToSceneView(MenuCommand command)
    {
        if (SceneView.lastActiveSceneView != null) MovePlayerToSceneView(SceneView.lastActiveSceneView);
    }

    [MenuItem(SCENE_CONTEXT_MENU + "Move Player to Point")]
    private static void ContextMovePlayerToPoint(MenuCommand command)
    {
        MovePlayerToPoint(_lastRightClickRay);
    }

    private static Rigidbody GetPlayerRigidbody()
    {
        PlayerInputController inputController = Object.FindObjectOfType<PlayerInputController>();
        if (inputController == null)
        {
            Debug.LogWarning("PlayerSceneViewEditor: Could not find 'PlayerInputController' in the scene.");
            return null;
        }

        CarMovementController carController = inputController.GetComponentInChildren<CarMovementController>();
        if (carController == null)
        {
            Debug.LogWarning($"PlayerSceneViewEditor: Could not find 'CarMovementController' in children of {inputController.name}.");
            return null;
        }

        Rigidbody rb = carController.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"PlayerSceneViewEditor: 'CarMovementController' ({carController.name}) does not have a Rigidbody component.");
            return null;
        }

        return rb;
    }
 
    

    

    private static void MovePlayerToSceneView(SceneView sceneView)
    {
        Rigidbody playerRb = GetPlayerRigidbody();
        if (playerRb == null) return; // Helper method already logged a warning.

        Transform sceneCamTransform = sceneView.camera.transform;

        Undo.RecordObject(playerRb, "Move Player to Scene View");
        Undo.RecordObject(playerRb.transform, "Move Player to Scene View");

        playerRb.position = sceneCamTransform.position;
        playerRb.rotation = sceneCamTransform.rotation;

        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;

        Debug.Log("Player moved to Scene View camera position.");
    }

    /// <summary>Moves the car to the clicked point, keeping its rotation.</summary>
    private static void MovePlayerToPoint(Ray worldRay)
    {
        Rigidbody playerRb = GetPlayerRigidbody();
        if (playerRb == null) return; // Helper method already logged a warning.


        if (Physics.Raycast(worldRay, out RaycastHit hit, MAX_RAYCAST_DISTANCE))
        {
            Undo.RecordObject(playerRb, "Move Player to Point");
            Undo.RecordObject(playerRb.transform, "Move Player to Point");

            playerRb.position = hit.point;

            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;

            Debug.Log($"Player moved to point: {hit.point} (Hit: {hit.collider.name})");
        }
        else
        {
            // Nothing hit: use the Y=0 plane.
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(worldRay, out float enter))
            {
                Vector3 hitPoint = worldRay.GetPoint(enter);

                Undo.RecordObject(playerRb, "Move Player to Y=0 Point");
                Undo.RecordObject(playerRb.transform, "Move Player to Y=0 Point");
                playerRb.position = hitPoint;

                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;

                Debug.Log($"Player moved to point on Y=0 plane: {hitPoint}");
            }
            else
            {
                Debug.LogWarning("PlayerSceneViewEditor: Right-click raycast did not hit any geometry or the Y=0 plane.");
            }
        }
    }
}

