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
        //    var parentConstraints = transform.GetComponentsInChildren<ParentConstraint>();
        foreach (var constraint in positionConstraints)
        {
            var constraintSource = new ConstraintSource();
            constraintSource.sourceTransform = injectedCar.transform;
            constraintSource.weight = MAX_WEIGHT;
            constraint.SetSources(new List<ConstraintSource>() { constraintSource });
        }


        foreach (var controller in controllers)
        {

            foreach (Behaviour mono in transform.GetComponentsInChildren<Behaviour>())
            {
                controller.AddBehaviour(mono);
            }
        }
        if (GetComponent<EnemyAIBrain>() == null)
        {
            Debug.Log("sd");
            injectedCar.GetComponent<CarMovementController>().speedOverlay = GameObject.Find("SpeedOverlay").GetComponentInChildren<TextMeshProUGUI>();
        }

        return injectedCar;
    }
}
