using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NormalBulletBehaivour : BulletBehaivour
{
    private Vector3 initPosition;

    private void Start()
    {
    }
    protected override void FixedUpdate()
    {
        if ((transform.position - initPosition).magnitude >= range)
        {
            OnBulletEndTrayectory();
        }
    }

    public override void InitBulletTrayectory()
    {
        base.InitBulletTrayectory();
        initPosition = transform.position;
        rb.velocity = velocity;
    }
}
