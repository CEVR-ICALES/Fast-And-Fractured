using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Enums;
using Utilities;

namespace FastAndFractured
{
    public class RecoverSelectedMenuController : MonoBehaviour
    {   
        private MenuScreen[] screensInScene;
        private GameObject lastButtonSelected;
        private bool isControllerInput = false;
        private bool areScreensLoaded = false;
        private float waitingTimeUntilEverythingIsLoaded = 1f;

        void OnEnable()
        {
            if(!areScreensLoaded)
            {
                TimerSystem.Instance.CreateTimer(waitingTimeUntilEverythingIsLoaded, onTimerDecreaseComplete: () =>
                {
                    Debug.Log("RecoverSelectedMenuController: Loading Screens in Scene");
                    LoadScreensInScene();
                });
            }
            InputSystem.onEvent += OnInputEvent;
        }
        private void OnDisable()
        {
            InputSystem.onEvent -= OnInputEvent;
        }

        void Update()
        {
            if(!areScreensLoaded)
            {
                return;
            }
            if(isControllerInput)
            {
                RestoreLastButtonOrDefualt();
            }
            else
            {
                if(EventSystem.current.currentSelectedGameObject != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }
        private void RestoreLastButtonOrDefualt()
        {
            if(EventSystem.current.currentSelectedGameObject == null)
            {
                if(lastButtonSelected != null)
                {
                    if(!lastButtonSelected.activeInHierarchy)
                    {
                        lastButtonSelected = null;
                    }else
                    {
                        EventSystem.current.SetSelectedGameObject(lastButtonSelected);
                    }
                }
                else
                {
                    foreach(MenuScreen menuScreen in screensInScene)
                    {
                        if(menuScreen != null
                            && menuScreen.gameObject.activeInHierarchy
                            && menuScreen.defaultInteractable != null
                            && menuScreen.defaultInteractable.gameObject.activeInHierarchy
                            && menuScreen.defaultInteractable.IsInteractable())
                        {
                            EventSystem.current.SetSelectedGameObject(menuScreen.defaultInteractable.gameObject);
                            break;
                        }
                    }
                }
            }
            else
            {
                lastButtonSelected = EventSystem.current.currentSelectedGameObject;
            }
        }
        private void LoadScreensInScene()
        {
            screensInScene = FindObjectsByType<MenuScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            areScreensLoaded = true;
        }
        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            if (device is Gamepad)
            {
                isControllerInput = true;
            }
            else if (device is Mouse)
            {
                isControllerInput = false;
            }
            else if (device is Keyboard)
            {
                isControllerInput = false;
            }
        }
    }
}