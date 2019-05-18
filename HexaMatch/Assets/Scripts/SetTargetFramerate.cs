using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetTargetFramerate : MonoBehaviour {

    public int targetFramerate = 60;

    private void Start()
    {
        Application.targetFrameRate = targetFramerate;
    }
}
