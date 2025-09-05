using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured
{
    public class CharacterSkinPrefabProvider : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        //public GameObject GetCharacterSkinGameObjet(string name, string skinNum,GameObject defaultPrefab)
        //{
        //    string skinPath = LevelConstants.SKINS_LOADER_PATH + "/" + name + "/" + "_" + skinNum;
            
        //}

        private Material[] LoadResources(string folder)
        {
            return Resources.LoadAll<Material>(folder);
        }
    }
}
