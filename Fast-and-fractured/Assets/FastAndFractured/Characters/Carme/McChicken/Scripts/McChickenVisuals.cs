using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class McChickenVisuals : MonoBehaviour
{
    [Header("Particles")]
    [SerializeField] private ParticleSystem _spawnFeathersVfx;

    [Header("Models")]
    [SerializeField] private GameObject chickenModel;
    [SerializeField] private GameObject eggModel;

    [Header("Legs")]
    [SerializeField] private McChickenLegsMovement[] legs;

    //this scripts will also have animations logic
    public void OnEggLaunched()
    {
        chickenModel.SetActive(false);
        eggModel.SetActive(true);
    }

    public void OnLand()
    {
        eggModel.SetActive(false);
        chickenModel.SetActive(true);
        _spawnFeathersVfx.Play();
    }

    public void OnChickenOnFloor()
    {
        NotifyLegsOfGroundState(true);
    }

    public void OnChickenOffFloor()
    {
        NotifyLegsOfGroundState(false);

    }

    private void NotifyLegsOfGroundState(bool isGround)
    {
        foreach (McChickenLegsMovement leg in legs)
        {
            leg.SetIsWalking(isGround);
        }
    }
}
