using System.Collections;
using System.Collections.Generic;
using Enums;
using UnityEngine;
using UnityEngine.Events;
using Utilities;

public class AimPushShootHitMarkCollision : MonoBehaviour, IPooledObject
{
    public UnityEvent<Vector3, Vector3> onCollide;
    [SerializeField]
    private float _speed = 100f;

    public Pooltype Pooltype { get => Pooltype.TRACE_COLISION; set => _pooltype = value; }
    private Pooltype _pooltype = Pooltype.TRACE_COLISION;
    public bool InitValues => _initValues;
    private bool _initValues = true;

    private void Start()
    {
    }
    public void SubscribeToParent(AimPushShootTrace aimPushShootTrace)
    {
        aimPushShootTrace.currentFinishedEvent.AddListener(OnEndDetection);
    }
    public void MoveToOtherPosition(Vector3 otherPosition,float frames)
    {
        transform.position = otherPosition;
    }

    public void ToogleCollider(bool enable)
    {
        GetComponent<Collider>().enabled = enable;
    }
    public void OnEndDetection()
    {
        ToogleCollider(false);
        ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
    }
    private void OnCollisionEnter(Collision collision)
    {
        ContactPoint contactPoint = collision.contacts[0];
        if (gameObject.activeSelf)
        {
            onCollide?.Invoke(contactPoint.point, contactPoint.normal);
        }
    }

    private void OnEnable()
    {
      
    }

    public void InitializeValues()
    {
        ToogleCollider(false);
    }
}
