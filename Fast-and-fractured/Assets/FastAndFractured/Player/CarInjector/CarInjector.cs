using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using UnityEngine.Animations;
using FastAndFractured;
using UnityEngine.UI;
using TMPro;
using Enums;

public class CarInjector : MonoBehaviour
{
    [SerializeField] GameObject prefab;
    [SerializeField] GameObject spawnPoint;
    [SerializeField] bool autoInject = false;


    private void Start()
    {
        if (autoInject)
        {
            Install(prefab);
        }
    }

    const float MAX_WEIGHT = 1;
    public virtual GameObject Install(GameObject prefabToInstall)
    {
        if (prefabToInstall != null)
        {
            prefab = prefabToInstall;
        }
        //TODO optimize this if posible
        var injectedCar = Instantiate(prefab, spawnPoint.transform.position, spawnPoint.transform.rotation, transform);
        var controllers = GetComponentsInChildren<Controller>();
        var positionConstraints = transform.GetComponentsInChildren<IConstraint>();
        var carMovementController = injectedCar.GetComponent<CarMovementController>();
        var constraintSource = new ConstraintSource();
        constraintSource.sourceTransform = injectedCar.transform;
        constraintSource.weight = MAX_WEIGHT;

        foreach (var constraint in positionConstraints)
        {
            constraint.SetSources(new List<ConstraintSource>() { constraintSource });
        }


        foreach (var controller in controllers)
        {

            foreach (Behaviour mono in transform.GetComponentsInChildren<Behaviour>())
            {
                controller.AddBehaviour(mono);
            }
        }
        if(TryGetComponent<EnemyAIBrain>(out EnemyAIBrain enemyAIBrain))
        {
            enemyAIBrain.InstallAIParameters(injectedCar.GetComponent<StatsController>().CharacterData.AIParameters);
            carMovementController.InputProvider = injectedCar.GetComponentInParent<AiInputProvider>();
        }
        else
        {
            
            injectedCar.AddComponent<CarSpeedOverlay>();
            injectedCar.GetComponent<CarSpeedOverlay>().speedOverlayText = HUDManager.Instance.GetUIElement(UIDynamicElementType.SPEED_INDICATOR).GetComponent<TextMeshProUGUI>();
            carMovementController.InputProvider = injectedCar.GetComponentInParent<PlayerInputProvider>();

        }

        return injectedCar;
    }

    public virtual GameObject Install(GameObject prefabToInstall, string nameCode, CharacterDataProvider characterDataProvider)
    {
        if (prefabToInstall != null)
        {
            prefab = prefabToInstall;
        }
        //TODO optimize this if posible
        var injectedCar = Instantiate(prefab, spawnPoint.transform.position, spawnPoint.transform.rotation, transform);

        //skin provider
        characterDataProvider.SetCharacterSkin(nameCode, injectedCar);

        var controllers = GetComponentsInChildren<Controller>();
        var positionConstraints = transform.GetComponentsInChildren<IConstraint>();
        var carMovementController = injectedCar.GetComponent<CarMovementController>();
        var constraintSource = new ConstraintSource();
        constraintSource.sourceTransform = injectedCar.transform;
        constraintSource.weight = MAX_WEIGHT;

        foreach (var constraint in positionConstraints)
        {
            constraint.SetSources(new List<ConstraintSource>() { constraintSource });
        }


        foreach (var controller in controllers)
        {

            foreach (Behaviour mono in transform.GetComponentsInChildren<Behaviour>())
            {
                controller.AddBehaviour(mono);
            }
        }
        if (TryGetComponent<EnemyAIBrain>(out EnemyAIBrain enemyAIBrain))
        {
            enemyAIBrain.InstallAIParameters(injectedCar.GetComponent<StatsController>().CharacterData.AIParameters);
            carMovementController.InputProvider = injectedCar.GetComponentInParent<AiInputProvider>();
        }
        else
        {

            injectedCar.AddComponent<CarSpeedOverlay>();
            injectedCar.GetComponent<CarSpeedOverlay>().speedOverlayText = HUDManager.Instance.GetUIElement(UIDynamicElementType.SPEED_INDICATOR).GetComponent<TextMeshProUGUI>();
            carMovementController.InputProvider = injectedCar.GetComponentInParent<PlayerInputProvider>();

        }

        return injectedCar;
    }
}
