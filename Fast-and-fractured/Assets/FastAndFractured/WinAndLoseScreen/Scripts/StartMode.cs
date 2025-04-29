using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Enums;
using Utilities;
using UnityEngine.Playables;
namespace FastAndFractured
{
    public class StartMode : MonoBehaviour
    {
        [SerializeField] private GameEndData gameEndData;
        [SerializeField] private GameObject mainMenuTimeline;
        [SerializeField] private MenuSkipInitialCutscene menuSkipInitialCutscene;
        [SerializeField] private GameObject skipText;


        public void ExecuteAfterMainMenuManager()
        {
            if (gameEndData.finalAnimation != null)
            {
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.WIN_LOSE,-1);
                mainMenuTimeline.SetActive(false);
                menuSkipInitialCutscene.timeLine = gameEndData.finalAnimation.GetComponentInChildren<PlayableDirector>();
                skipText.SetActive(true);
            }
            else
            {
                MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MAIN_MENU,-1);
                mainMenuTimeline.SetActive(true);
            }
        }
    }
}

