using UnityEngine;
using UnityEngine.UI;
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
        [SerializeField] private int min_animation_steps = 10;
        [SerializeField] private int max_animation_steps = 15;
        private readonly List<Button> buttons = new();
        private int selectedIndex = 0;

        private bool isAnimating;
        private int animationSteps;
        private int animationTargetSteps;
        private float animationTimer;
        private int animationCurrentIndex;

        private EventSystem eventSystem;
        private InputSystemUIInputModule inputModule;
        private const float ANIMATION_INTERVAL = 0.5f;
        
        void Start()
        {
            eventSystem = EventSystem.current;
            inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            panel.SetActive(false);

            buttons.AddRange(GetComponentsInChildren<Button>());

            for (int i = 0; i < buttons.Count; i++)
            {
                int idx = i;
                var trigger = buttons[i].gameObject.GetComponent<EventTrigger>() ?? buttons[i].gameObject.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener((_) => UpdateSelectedMapImage(idx));
                trigger.triggers.Add(entry);
            }

            UpdateSelectedMapImage(selectedIndex);
        }

        void Update()
        {
            if (!isAnimating) return;

            animationTimer += Time.deltaTime;
            if (animationTimer < ANIMATION_INTERVAL) return;

            animationTimer = 0f;
            DeselectButton(selectedIndex);

            animationCurrentIndex = (animationCurrentIndex + 1) % buttons.Count;
            SelectButton(animationCurrentIndex);
            selectedIndex = animationCurrentIndex;
            animationSteps++;

            if (animationSteps < animationTargetSteps) return;

            isAnimating = false;
            DeselectButton(selectedIndex);
            SelectButton(selectedIndex);
            buttons[selectedIndex].onClick.Invoke();
            inputModule.enabled = true;
        }

        public void RandomMap()
        {
            if (buttons.Count == 0) return;

            panel.SetActive(true);
            inputModule.enabled = false;
            returnButton.SetActive(false);

            animationTargetSteps = Random.Range(min_animation_steps, max_animation_steps + 1);
            animationSteps = 0;
            animationTimer = 0f;
            animationCurrentIndex = selectedIndex;
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
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void UpdateSelectedMapImage(int index)
        {
            if (selectedMapImage && index >= 0 && index < spriteList.Length)
            {
                var sprite = spriteList[index];
                if (sprite) selectedMapImage.sprite = sprite;
            }
        }
    }
}