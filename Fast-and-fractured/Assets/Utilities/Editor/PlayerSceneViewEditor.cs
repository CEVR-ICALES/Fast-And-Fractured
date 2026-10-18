using UnityEngine;
using UnityEditor;
using FastAndFractured;

/// <summary>Scene View right-click menu that moves the player's car to the scene camera or to the clicked point.</summary>
[InitializeOnLoad]
public static class PlayerSceneViewEditor
{
    private const float MAX_RAYCAST_DISTANCE = 10000f;

    static PlayerSceneViewEditor()
    {
        // ParrelSync clones have no main window.
        if (IsMultiplayerClone())
        {
            return;
        }

        Debug.Log("PlayerSceneViewEditor.cs loaded successfully!");

        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static bool IsMultiplayerClone()
    {
        return Application.dataPath.ToLower().Contains("library/vp/");
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        if (e.type == EventType.ContextClick)
        {
            Debug.Log("ContextClick event detected! Creating menu...");

            GenericMenu menu = new GenericMenu();

            // The event is gone by the time the menu callback runs.
            Vector2 mousePosition = e.mousePosition;

            menu.AddItem(new GUIContent("Move Player Here (Match Camera)"), false, () =>
            {
                MovePlayerToSceneView(sceneView);
            });

            menu.AddItem(new GUIContent("Move Player to Point"), false, () =>
            {
                MovePlayerToPoint(sceneView, mousePosition);
            });

            // Replacing the default menu: re-add the common items.
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Copy"), false, () => EditorApplication.ExecuteMenuItem("Edit/Copy"));
            menu.AddItem(new GUIContent("Paste"), false, () => EditorApplication.ExecuteMenuItem("Edit/Paste"));
            menu.AddItem(new GUIContent("Duplicate"), false, () => EditorApplication.ExecuteMenuItem("Edit/Duplicate"));
            menu.AddItem(new GUIContent("Delete"), false, () => EditorApplication.ExecuteMenuItem("Edit/Delete"));

            menu.ShowAsContext();

            Debug.Log("Menu shown. Consuming event.");

            e.Use();
        }
    }

    /// <summary>The Rigidbody of the scene's player car (the CarMovementController under PlayerInputController).</summary>
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
    private static void MovePlayerToPoint(SceneView sceneView, Vector2 mousePosition)
    {
        Rigidbody playerRb = GetPlayerRigidbody();
        if (playerRb == null) return; // Helper method already logged a warning.

        Ray worldRay = HandleUtility.GUIPointToWorldRay(mousePosition);

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

