using UnityEngine;
using UnityEditor;
using FastAndFractured;  
public static class PlayerSceneViewTools
{ 
    [MenuItem("Tools/Player/Move Player to Scene View")]
    private static void MenuMovePlayerToSceneView()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogError("PlayerSceneViewTools: No active Scene View window found.");
            return;
        }

        Rigidbody playerRb = GetPlayerRigidbody();
        if (playerRb == null) return;  

        Transform sceneCamTransform = sceneView.camera.transform;
 
        Undo.RecordObject(playerRb, "Move Player to Scene View");
        Undo.RecordObject(playerRb.transform, "Move Player to Scene View");
 
        playerRb.position = sceneCamTransform.position;
        playerRb.rotation = sceneCamTransform.rotation;
 
        playerRb.linearVelocity = Vector3.zero;
        playerRb.angularVelocity = Vector3.zero;

        Debug.Log("Player moved to Scene View camera position.");
    }
 
    [MenuItem("Tools/Player/Move Player to Ground (Center of View)")]
    private static void MenuMovePlayerToCenterRaycast()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogError("PlayerSceneViewTools: No active Scene View window found.");
            return;
        }

        Rigidbody playerRb = GetPlayerRigidbody();
        if (playerRb == null) return;  
        Transform camTransform = sceneView.camera.transform;
        Ray worldRay = new Ray(camTransform.position, camTransform.forward);
  
        if (Physics.Raycast(worldRay, out RaycastHit hit, 10000f))  
        { 
            Undo.RecordObject(playerRb, "Move Player to Point");
            Undo.RecordObject(playerRb.transform, "Move Player to Point");
 
            playerRb.position = hit.point;
 
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;

            Debug.Log($"Player moved to center raycast point: {hit.point} (Hit: {hit.collider.name})");
        }
        else
        {
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
                Debug.LogWarning("PlayerSceneViewTools: Center raycast did not hit any geometry or the Y=0 plane.");
            }
        }
    }
 
    [MenuItem("Tools/Player/Move Player to Scene View", true)]
    [MenuItem("Tools/Player/Move Player to Ground (Center of View)", true)]
    private static bool ValidateMovePlayerMenus()
    {
         return SceneView.lastActiveSceneView != null;
    }

 
    private static Rigidbody GetPlayerRigidbody()
    { 
        PlayerInputController inputController = Object.FindObjectOfType<PlayerInputController>();
        if (inputController == null)
        {
            Debug.LogWarning("PlayerSceneViewTools: Could not find 'PlayerInputController' in the scene.");
            return null;
        }
 
        CarMovementController carController = inputController.GetComponentInChildren<CarMovementController>();
        if (carController == null)
        {
            Debug.LogWarning($"PlayerSceneViewTools: Could not find 'CarMovementController' in children of {inputController.name}.");
            return null;
        }
 
        Rigidbody rb = carController.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"PlayerSceneViewTools: 'CarMovementController' ({carController.name}) does not have a Rigidbody component.");
            return null;
        }
 
        return rb;
    }
}

