using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
using Utilities;

public class DeadWallsBehaivour : MonoBehaviour, IKillCharacters
{
    [SerializeField]
    private float _timeTillKill = 1.5f;
    [SerializeField]
    private int _killPriority = 2;
    [SerializeField]
    private float frontAngle = 180;
    private Dictionary<StatsController,ITimer> _charactersDontExitTheCollider;

    public int killPriority => _killPriority;

    public float killTime => _timeTillKill;

    private void Start()
    {
        _charactersDontExitTheCollider = new Dictionary<StatsController,ITimer>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out StatsController controller))
        {
            StartKillNotify(controller);
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.TryGetComponent(out StatsController controller))
        {
            if (IsObjectInFront(other.transform))
            {
                CharacterEscapedDead(controller);
            }
        }
    }

    // Verifica si el objeto está dentro del cono de visión y distancia
    public bool IsObjectInFront(Transform target)
    {
        // Dirección hacia el objetivo
        Vector3 directionToTarget = target.position - (transform.position + transform.lossyScale);
        float distance = directionToTarget.magnitude;

        // Normalizar la dirección para el producto punto
        directionToTarget.Normalize();

        // Calcular el producto punto (coseno del ángulo)
        float dotProduct = Vector3.Dot(transform.forward, directionToTarget);

        // Convertir a ángulo en grados
        float angleToTarget = Mathf.Acos(dotProduct) * Mathf.Rad2Deg;

        // Si el ángulo es menor que la mitad del campo de visión, está dentro del cono
        return angleToTarget < (frontAngle / 2f);
    }

    public void StartKillNotify(StatsController statsController)
    {
        statsController.GetKilledNotify(this, false);
    }
    public void CharacterEscapedDead(StatsController statsController)
    {
        statsController.GetKilledNotify(this, true);
    }
}
