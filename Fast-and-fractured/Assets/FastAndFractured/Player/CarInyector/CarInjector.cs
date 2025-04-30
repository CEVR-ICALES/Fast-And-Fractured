using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using UnityEngine.Animations;
using FastAndFractured;
using UnityEngine.UI;
using TMPro;

public class CarInjector : MonoBehaviour
{
    [SerializeField] GameObject prefab;
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
        var injectedCar = Instantiate(prefab, this.transform.position, Quaternion.identity, transform);
        var controllers = GetComponentsInChildren<Controller>();
        var positionConstraints = transform.GetComponentsInChildren<IConstraint>();

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
            injectedCar.GetComponent<CarMovementController>().IsAi = true;
        }
        else{
            if (GameObject.Find("SpeedOverlay"))
            {
                injectedCar.GetComponent<CarMovementController>().speedOverlay = GameObject.Find("SpeedOverlay").GetComponentInChildren<TextMeshProUGUI>();
            }
        } 

        return injectedCar;
    }
}
