using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace FastAndFractured
{
    public class MapSelection : MonoBehaviour
    {
        [SerializeField] private Image selectedMapImage;
        [SerializeField] private Sprite[] spriteList;
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject returnButton;
        private List<Button> buttons = new List<Button>();
        private int selectedIndex = -1;

        private bool isAnimating = false;
        private int animationSteps = 0;
        private int animationTargetSteps = 0;
        private float animationTimer = 0f;
        private int animationCurrentIndex = 0;
        private EventSystem eventSystem;
        private InputSystemUIInputModule inputModule;
        private const float ANIMATION_INTERVAL = 0.5f;
        private const int MIN_ANIMATION_STEPS = 10;
        private const int MAX_ANIMATION_STEPS = 15;

        void Start()
        {
            eventSystem = EventSystem.current;
            inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            panel.SetActive(false);
            buttons.AddRange(GetComponentsInChildren<Button>());
            for (int i = 0; i < buttons.Count; i++)
            {
                int idx = i;
                EventTrigger trigger = buttons[i].gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                    trigger = buttons[i].gameObject.AddComponent<EventTrigger>();

                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener((eventData) => { UpdateSelectedMapImage(idx); });
                trigger.triggers.Add(entry);
            }
            UpdateSelectedMapImage(0);
        }

        void Update()
        {
            if (isAnimating)
            {
                animationTimer += Time.deltaTime;
                if (animationTimer >= ANIMATION_INTERVAL)
                {
                    animationTimer = 0f;
                    if (selectedIndex >= 0)
                        DeselectButton(selectedIndex);

                    animationCurrentIndex = (animationCurrentIndex + 1) % buttons.Count;
                    SelectButton(animationCurrentIndex);
                    selectedIndex = animationCurrentIndex;

                    animationSteps++;

                    if (animationSteps >= animationTargetSteps)
                    {
                        isAnimating = false;

                        if (selectedIndex >= 0)
                            DeselectButton(selectedIndex);

                        SelectButton(selectedIndex);
                        buttons[selectedIndex].onClick.Invoke();
                        inputModule.enabled = true;
                    }
                }
            }
        }

        public void RandomMap()
        {
            if (buttons.Count == 0) return;
            panel.SetActive(true);
            inputModule.enabled = false;
            returnButton.SetActive(false);
            animationTargetSteps = Random.Range(MIN_ANIMATION_STEPS, MAX_ANIMATION_STEPS + 1);
            animationSteps = 0;
            animationTimer = 0f;
            animationCurrentIndex = selectedIndex >= 0 ? selectedIndex : 0;
            isAnimating = true;
        }

        private void SelectButton(int index)
        {
            buttons[index].Select();
            UpdateSelectedMapImage(index);
        }
        private void DeselectButton(int index)
        {
            if (EventSystem.current.currentSelectedGameObject == buttons[index].gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
        private void UpdateSelectedMapImage(int index)
        {
            if (selectedMapImage != null && buttons.Count > index && index >= 0)
            {
                Sprite sprite = spriteList[index];
                if (sprite != null)
                    selectedMapImage.sprite = sprite;
            }
        }
    }
}