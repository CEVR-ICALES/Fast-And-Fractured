using UnityEditor;
using UnityEngine;

namespace DestroyIt
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public class TerrainPreserver : MonoBehaviour 
	{
		public void Awake ()
		{
			#if UNITY_EDITOR
			if (Application.isPlaying) return;
			
			// Get the terrain to try to preserve.
			TreeManager treeManager = gameObject.GetComponent<TreeManager>();
			if (treeManager == null) return;
			
			Terrain terrain = treeManager.terrain;
			if (terrain == null)
				terrain = Terrain.activeTerrain;
			if (terrain == null) return;
			
			// Check to see if a terrainData backup exists.
			string terrainDataPath = AssetDatabase.GetAssetPath(terrain.terrainData);
			string terrainDataBkpPath = terrainDataPath.Replace(".asset", "") + "_bkp.asset";
			TerrainData terrainDataBkp = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataBkpPath);
			
			// If a terrainData backup exists, ask the user if they want to restore tree data from the backup.
			if (terrainDataBkp != null)
			{	
				int option = EditorUtility.DisplayDialogComplex("Restore Terrain Trees from Backup?",
					"The Unity Editor may not have shut down correctly last time, which could mean destructible terrain trees were removed from terrain [" + terrain.terrainData.name + "]. Fortunately, there is a backup.\n\n" +
					"Choose 'Restore' to restore trees from the backup. Choose 'Don't Restore' to keep existing trees as they are " +  
					"and delete the backup. Choose 'Ask Later' to do nothing right now, so you can inspect your terrain and make the decision later.",
					"Restore", "Ask Later", "Don't Restore");

				switch (option)
				{
					// Restore trees from backup
					case 0:
						terrain.terrainData.treeInstances = terrainDataBkp.treeInstances;
						AssetDatabase.DeleteAsset(terrainDataBkpPath);
						Debug.Log("Tree instances restored on [" + terrain.terrainData.name + "].");
						break;
					// Don't do anything - the user will be prompted again next time this script runs
					case 1:
						break;
					// Don't restore trees and delete backup
					case 2:
						AssetDatabase.DeleteAsset(terrainDataBkpPath);
						break;
					default:
						Debug.LogError("Unrecognized option.");
						break;
				}
			}
			#endif
		}
	}
}

