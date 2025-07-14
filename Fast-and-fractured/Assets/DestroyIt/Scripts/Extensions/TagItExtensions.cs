using System.Collections.Generic;
using UnityEngine;
// ReSharper disable ForCanBeConvertedToForeach

namespace DestroyIt
{
    /// <summary>These extension methods work with the TagIt multi-tag system to make it easier to search for and add tags.</summary>
    public static class TagItExtensions
    {
        public static bool HasTag(this GameObject go, params Tag[] searchTags)
        {
            TagIt tagIt = go.GetComponent<TagIt>();
            if (tagIt == null) return false;

            for (int i = 0; i < searchTags.Length; i++)
            {
                for (int j = 0; j < tagIt.tags.Count; j++)
                    if (searchTags[i] == tagIt.tags[j])
                        return true;
            }

            return false;
        }

        public static bool HasTagInParent(this GameObject go, params Tag[] searchTags)
        {
            TagIt tagIt = go.GetComponentInParent<TagIt>();
            if (tagIt == null) return false;

            for (int i = 0; i < searchTags.Length; i++)
            {
                for (int j = 0; j < tagIt.tags.Count; j++)
                    if (searchTags[i] == tagIt.tags[j])
                        return true;
            }

            return false;
        }

        public static void AddTag(this GameObject go, Tag tag)
        {
            TagIt tagIt = go.GetComponent<TagIt>();
            if (tagIt == null)
            {
                tagIt = go.AddComponent<TagIt>();
                tagIt.tags = new List<Tag>();
            }
            else if (tagIt.tags.Contains(tag))
                return;
            
            tagIt.tags.Add(tag);
        }

        public static void RemoveTag(this GameObject go, Tag tag)
        {
            TagIt tagIt = go.GetComponent<TagIt>();
            if (tagIt == null) return;

            tagIt.tags.Remove(tag);
        }

        /// <summary>Search for the highest parent found containing this tag.</summary>
        public static GameObject GetHighestParentWithTag(this GameObject go, Tag tag)
        {
            // First, get all parents of this gameobject.
            List<Transform> parents = new List<Transform>();
            Transform trans = go.transform;
            while (trans != null)
            {
                parents.Add(trans);
                trans = trans.parent;
            }

            // Now check each parent, starting with the oldest one, to see if any contains the Tag.
            TagIt tagIt = null;
            for (int i = parents.Count - 1; i >= 0; i--)
            {
                tagIt = parents[i].GetComponent<TagIt>();
                if (tagIt != null && tagIt.tags.Contains(tag))
                    return parents[i].gameObject;
            }

            return null;
        }
    }
}