using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class NormalShootHandle : ShootingHandle
{
    float _countCadenceTime;
    float _countOverHeat;
    private void Start()
    {
        CustomStart();
        _countCadenceTime = characterStatsController.NormalShootCadenceTime;
        _countOverHeat = 0;
    }
    protected override void SetBulletStats(BulletBehaivour bulletBehaivour)
    {
        base.SetBulletStats(bulletBehaivour);
    }

    public void NormalShooting()
    {
        Vector3 velocity = (mainCamera.transform.forward + cameraCenterOffSet) * characterStatsController.NormalShootSpeed;
        ShootBullet(velocity, characterStatsController.NormalShootMaxRange);
    }
}
