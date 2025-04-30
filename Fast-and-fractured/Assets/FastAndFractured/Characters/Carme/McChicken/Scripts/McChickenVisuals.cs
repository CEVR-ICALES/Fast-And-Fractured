using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class McChickenVisuals : MonoBehaviour
{
    [SerializeField] private ParticleSystem _spawnFeathersVfx;

    //this scripts will also have animations logic

    public void OnLand()
    {
        _spawnFeathersVfx.Play();
    }
}
