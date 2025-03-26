using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetTargetFPS : MonoBehaviour
{
    public int targetFPS;

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = targetFPS;
    }    
}
