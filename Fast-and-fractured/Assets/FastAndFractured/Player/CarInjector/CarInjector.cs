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
    const string VISUALS_NAME = "Visuals";
    public virtual GameObject Install(GameObject prefabToInstall,bool skipInstantiation=false)
    {
        if (prefabToInstall != null)
        {
            prefab = prefabToInstall;
        }
        GameObject injectedCar;
        CarMovementController carMovementController;
        //TODO optimize this if posible
        if (skipInstantiation)
        {
            carMovementController = GetComponentInChildren<CarMovementController>();
            injectedCar = GetComponentInChildren<CarMovementController>().gameObject;
        }
        else
        {
            injectedCar = Instantiate(prefab, spawnPoint.transform.position, spawnPoint.transform.rotation, transform);
            carMovementController = injectedCar.GetComponent<CarMovementController>();
        }
        var controllers = GetComponentsInChildren<Controller>();
        var positionConstraints = transform.GetComponentsInChildren<IConstraint>(true);
        var constraintSource = new ConstraintSource();
        constraintSource.sourceTransform = injectedCar.transform.Find(VISUALS_NAME);
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
            
           var carSpeedOverlay = injectedCar.AddComponent<CarSpeedOverlay>();
            //todo fix
        //    carSpeedOverlay.speedOverlayText = HUDManager.Instance.GetUIElement(UIDynamicElementType.SPEED_INDICATOR).GetComponent<TextMeshProUGUI>();
            carMovementController.InputProvider = injectedCar.GetComponentInParent<PlayerInputProvider>();

        }

        return injectedCar;
    }
}
