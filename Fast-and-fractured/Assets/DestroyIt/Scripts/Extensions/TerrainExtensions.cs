using UnityEngine;
// ReSharper disable SuggestVarOrType_Elsewhere
// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable SuggestVarOrType_BuiltInTypes

namespace DestroyIt
{
    public static class TerrainExtensions
    {
        public static TerrainTree ClosestTreeToPoint(this Terrain terrain, Vector3 point)
        {
            TreeInstance[] trees = terrain.terrainData.treeInstances;
            if (trees.Length == 0) return null;

            TerrainTree closestTree = new TerrainTree {Index = -1};
            float closestTreeDist = float.MaxValue;

            for (int i = 0; i < trees.Length; i++)
            {
                Vector3 treePos = Vector3.Scale(trees[i].position, terrain.terrainData.size) + terrain.transform.position;
                float treeDist = Vector3.Distance(treePos, point);

                if (treeDist < closestTreeDist)
                {
                    closestTreeDist = treeDist;
                    closestTree.Index = i;
                    closestTree.Position = treePos;
                    closestTree.TreeInstance = trees[i];
                }
            }

            return closestTree;
        }

        public static Vector3 WorldPositionOfTree(this Terrain terrain, int treeIndex)
        {
            TreeInstance[] trees = terrain.terrainData.treeInstances;
            if (trees.Length == 0) return Vector3.zero;

            return Vector3.Scale(trees[treeIndex].position, terrain.terrainData.size) + terrain.transform.position;
        }
    }
}
