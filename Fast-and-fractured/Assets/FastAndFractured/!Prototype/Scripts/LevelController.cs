using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game { 
public class LevelController : MonoBehaviour
{
       
    [SerializeField] private List<ObjectPoolSO> poolSOList;
    [SerializeField] private List<StatsController> characters;
        [SerializeField] private List<KillCharacterNotify> killCharacterHandles;
    // Start is called before the first frame update
    void Start()
    {
        StartLevel();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void StartLevel()
    {
        Cursor.lockState = CursorLockMode.Locked;
        ObjectPoolManager.Instance.CustomStart();
        foreach (var poolSO in poolSOList)
        {
            ObjectPoolManager.Instance.CreateObjectPool(poolSO);
        }
        foreach(var character in characters)
        {
           character.CustomStart();
        }
            foreach (var killCharacterHandle in killCharacterHandles)
            {
                killCharacterHandle.onTouchCharacter += EliminatePlayer;
            }
    }

    public void EliminatePlayer(StatsController characterstats)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
            float delay = characterstats.Dead();
            TimerManager.Instance.StartTimer(delay, () => {
                if (IsThePlayer(characterstats.gameObject))
                {
                    SceneManager.LoadScene(currentSceneName);
                }
                else
                    characterstats.gameObject.SetActive(false);
            },null,"Character " + characterstats.name + " Dead",false);
    }

    private bool IsThePlayer(GameObject character)
    {
      if (character.CompareTag("Player"))
           return true;
      return false;
    }
  }
}

