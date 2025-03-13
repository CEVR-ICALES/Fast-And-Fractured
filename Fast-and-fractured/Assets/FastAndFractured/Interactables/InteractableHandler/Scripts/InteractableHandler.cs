using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Utilities;

public class InteractableHandler : AbstractSingleton<InteractableHandler>
{
    [SerializeField] GameObject[] interactablesToToggle;
    [SerializeField] private int numberOfItemsActiveAtSameTime = 8;
    [SerializeField] float itemCooldownAfterPick = 2f;
    
    List<GameObject> _shuffledInteractables = new();
    List<GameObject> _itemsOnCooldown = new();
    protected override void Awake()
    {
        base.Awake();
        foreach (var item in interactablesToToggle)
        {
            var interectable = item.GetComponentInParent<GenericInteractable>();
            if (interectable)
            {
                interectable.disableGameObjectOnInteract = true;
                interectable.onInteract.AddListener(RemoveInteractableFromPool);
            }
        }
        MakeInitialPool();

    }


    void MakeInitialPool()
    {
        _shuffledInteractables = interactablesToToggle.OrderBy(_ => UnityEngine.Random.Range(0, interactablesToToggle.Length)).ToList();
        UpdateVisibleInteractablesList();
    }
    void UpdateVisibleInteractablesList(int index = 0)
    {
        for (int i = index; i < _shuffledInteractables.Count && i < numberOfItemsActiveAtSameTime; i++)
        {
            GameObject item = _shuffledInteractables[i];
            item.SetActive(true);
        }
        for (int i = numberOfItemsActiveAtSameTime; i < _shuffledInteractables.Count; i++)
        {
            GameObject item = _shuffledInteractables[i];
            item.SetActive(false);

        }
    }
    private void RemoveInteractableFromPool(GameObject interactionFrom, GameObject intearactionTo)
    {
        _itemsOnCooldown.Add(intearactionTo);
        TimerManager.Instance.StartTimer(itemCooldownAfterPick, () => ConvertItemOnCooldownToPool(intearactionTo), null, new Guid().ToString());
        _shuffledInteractables.Remove(intearactionTo);
        intearactionTo.gameObject.SetActive(false);
        UpdateVisibleInteractablesList();
    }

    private void ConvertItemOnCooldownToPool(GameObject arg0)
    {
        _itemsOnCooldown.Remove(arg0);
        _shuffledInteractables.Add(arg0);
    }
}

