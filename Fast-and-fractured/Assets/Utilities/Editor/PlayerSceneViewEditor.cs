using UnityEngine;
using UnityEditor;
using FastAndFractured;

/// <summary>
/// This editor script adds a custom context menu to the Scene View
/// when right-clicking. This menu provides options to quickly
/// move the player object based on the scene camera or click position.
///
/// To use:
/// 1. Place this script in an "Editor" folder in your Unity project.
/// 2. Right-click anywhere in the Scene View to open the context menu.
///
/// This script assumes you have:
/// - A GameObject in your scene with a 'PlayerInputController' component.
/// - A 'CarMovementController' component on a child object of the 'PlayerInputController'.
/// </summary>
[InitializeOnLoad] // This attribute ensures our script runs when the editor loads.
public static class PlayerSceneViewEditor
{
    /// <summary>
    /// Static constructor called by [InitializeOnLoad]
    /// We subscribe our OnSceneGUI method to the SceneView's event delegate.
    /// </summary>
    static PlayerSceneViewEditor()
    {
        // Don't run in multiplayer clones - they don't have a main window
        if (IsMultiplayerClone())
        {
            return;
        }

        // --- DIAGNOSTIC LOG ---
        // If you see this message in your Console when Unity loads,
        // the script is running.
        // If not, this file is NOT in a folder named "Editor".
        Debug.Log("PlayerSceneViewEditor.cs loaded successfully!");

        // This hooks our custom method into the Scene View's update loop.
        SceneView.duringSceneGui += OnSceneGUI;
    }

    /// <summary>
    /// Check if this is a multiplayer clone instance
    /// </summary>
    private static bool IsMultiplayerClone()
    {
        return Application.dataPath.ToLower().Contains("library/vp/");
    }

    /// <summary>
    /// This method is called during the Scene View's OnGUI event.
    /// We use it to detect right-clicks and show our custom menu.
    /// </summary>
    private static void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        // Check for the 'ContextClick' event instead of 'MouseDown'.
        // This is the semantically correct event for context menus.
        if (e.type == EventType.ContextClick)
        {
            // --- NEW DIAGNOSTIC LOG ---
            // Check if this message appears in the console on right-click.
            Debug.Log("ContextClick event detected! Creating menu...");

            // Create the context menu.
            GenericMenu menu = new GenericMenu();

            // Store the mouse position from the event, as the event
            // object won't be available inside the menu item's callback.
            Vector2 mousePosition = e.mousePosition;

            // --- Menu Item 1: Move Player to Scene View Camera ---
            // The callback is an anonymous function that calls our logic.
            menu.AddItem(new GUIContent("Move Player Here (Match Camera)"), false, () =>
            {
                MovePlayerToSceneView(sceneView);
            });

            // --- Menu Item 2: Move Player to Right-Clicked Point ---
            menu.AddItem(new GUIContent("Move Player to Point"), false, () =>
            {
                MovePlayerToPoint(sceneView, mousePosition);
            });

            // --- Add default menu items ---
            // This is the standard way to "add" to the menu. We must replace
            // the default menu and then re-add the common items ourselves.
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Copy"), false, () => EditorApplication.ExecuteMenuItem("Edit/Copy"));
            menu.AddItem(new GUIContent("Paste"), false, () => EditorApplication.ExecuteMenuItem("Edit/Paste"));
            menu.AddItem(new GUIContent("Duplicate"), false, () => EditorApplication.ExecuteMenuItem("Edit/Duplicate"));
            menu.AddItem(new GUIContent("Delete"), false, () => EditorApplication.ExecuteMenuItem("Edit/Delete"));

            // Show the menu at the mouse position.
            menu.ShowAsContext();

            // --- NEW DIAGNOSTIC LOG ---
            // Check if this message appears right after the one above.
            Debug.Log("Menu shown. Consuming event.");

            // Consume the event so no other Unity window processes this right-click.
            e.Use();
        }
    }

    /// <summary>
    /// Finds the player's CarMovementController Rigidbody based on the user's specific request.
    /// </summary>
    /// <returns>The Rigidbody of the CarMovementController, or null if not found.</returns>
    private static Rigidbody GetPlayerRigidbody()
    {
        // 1. Find the PlayerInputController in the entire scene.
        // Note: FindObjectOfType is slow, but acceptable for an editor tool.
        PlayerInputController inputController = Object.FindObjectOfType<PlayerInputController>();
        if (inputController == null)
        {
            Debug.LogWarning("PlayerSceneViewEditor: Could not find 'PlayerInputController' in the scene.");
            return null;
        }

        // 2. Find the CarMovementController in its children.
        CarMovementController carController = inputController.GetComponentInChildren<CarMovementController>();
        if (carController == null)
        {
            Debug.LogWarning($"PlayerSceneViewEditor: Could not find 'CarMovementController' in children of {inputController.name}.");
            return null;
        }

        // 3. Get the Rigidbody on the same object as the CarMovementController.
        Rigidbody rb = carController.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"PlayerSceneViewEditor: 'CarMovementController' ({carController.name}) does not have a Rigidbody component.");
            return null;
        }

        // 4. Return the Rigidbody.
        return rb;
    }
 
    

    

    /// <summary>
    /// Logic for "Move Player Here (Match Camera)"
    /// Moves the player to the exact position and rotation of the scene view camera.
    /// </summary>
    private static void MovePlayerToSceneView(SceneView sceneView)
    {
        Rigidbody playerRb = GetPlayerRigidbody();
        if (playerRb == null) return; // Helper method already logged a warning.

        Transform sceneCamTransform = sceneView.camera.transform;

        // Register an undo state for the Rigidbody.
        Undo.RecordObject(playerRb, "Move Player to Scene View");
        // Also good to record the transform, as some properties might live there.
        Undo.RecordObject(playerRb.transform, "Move Player to Scene View");

        // Apply the position and rotation via the Rigidbody.
        playerRb.position = sceneCamTransform.position;
        playerRb.rotation = sceneCamTransform.rotation;

        // Clear physics velocities to prevent weird "drifting" after the teleport.
        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;

        Debug.Log("Player moved to Scene View camera position.");
    }

    /// <summary>
    /// Logic for "Move Player to Point"
    /// Raycasts from the mouse click into the scene and moves the player to the hit point.
    /// </summary>
    private static void MovePlayerToPoint(SceneView sceneView, Vector2 mousePosition)
    {
        Rigidbody playerRb = GetPlayerRigidbody();
        if (playerRb == null) return; // Helper method already logged a warning.

        // Convert the 2D GUI mouse position into a 3D ray from the scene camera.
        Ray worldRay = HandleUtility.GUIPointToWorldRay(mousePosition);

        // Perform a physics raycast to find the first collider we hit.
        if (Physics.Raycast(worldRay, out RaycastHit hit, 10000f)) // 10k unit range
        {
            // Register an undo state for the Rigidbody.
            Undo.RecordObject(playerRb, "Move Player to Point");
            Undo.RecordObject(playerRb.transform, "Move Player to Point");

            // Apply the position of the hit point via the Rigidbody.
            // This does NOT change the player's rotation.
            playerRb.position = hit.point;

            // Clear physics velocities.
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;

            Debug.Log($"Player moved to point: {hit.point} (Hit: {hit.collider.name})");
        }
        else
        {
            // Fallback: If we don't hit any geometry, try raycasting against a
            // conceptual ground plane at Y=0.
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(worldRay, out float enter))
            {
                Vector3 hitPoint = worldRay.GetPoint(enter);

                Undo.RecordObject(playerRb, "Move Player to Y=0 Point");
                Undo.RecordObject(playerRb.transform, "Move Player to Y=0 Point");
                playerRb.position = hitPoint;

                // Clear physics velocities.
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

