using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class ExplosionForce : MonoBehaviour
{
    private float _pushForce;
    public SphereCollider ExplosionCollider {set=> _explosionCollider = value; }
    private SphereCollider _explosionCollider;
    [SerializeField] private Transform _explosionVFX;

    public void DesactivateExplostionHitbox()
    {
        gameObject.SetActive(false);
    }
    public void ActivateExplosionHitbox(float radius,float pushForce,Vector3 center)
    {
        if (_explosionCollider != null)
        {
            gameObject.SetActive(true);
            _pushForce = pushForce;
            _explosionCollider.center = center;
            _explosionCollider.radius = radius;
            _explosionVFX.localScale = Vector3.one * radius;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<StatsController>(out var statsController))
        {
            float oCarWeight = statsController.Weight;
            float oCarEnduranceFactor = statsController.Endurance;
            float force = _pushForce * (1 - oCarEnduranceFactor) * (/*oCarWeight*/ other.GetComponent<Rigidbody>().mass / 20);
            Vector3 direction = CalculateDirectionByRegion(other.transform);
                float distanceToCenter = Vector3.Distance(other.transform.position, transform.position + _explosionCollider.center);
                other.GetComponent<Rigidbody>().AddForce(direction * force * (1 - (distanceToCenter / _explosionCollider.radius)));
        }
    }

    private Vector3 CalculateDirectionByRegion(Transform target)
    {
        var directionToObject = (target.position - (transform.position * _explosionCollider.radius)).normalized;
        // Determinar la región del objeto
        Vector3 forwardDirection = transform.forward;
        float angle = Vector3.SignedAngle(forwardDirection, directionToObject, Vector3.up);
        // Aplicar fuerza adicional según la región
        Vector3 regionDirection = Vector3.zero;
        if (angle >= -45f && angle < 45f) // Forward
        {
            return transform.forward;
        }
        else if (angle >= 45f && angle < 135f) // Right
        {
            return transform.right;
        }
        else if (angle >= 135f || angle < -135f) // Back
        {
           return -transform.forward;
        }
        else if (angle >= -135f && angle < -45f) // Left
        {
           return -transform.right;
        }
        else 
            return transform.forward;
    }
}
