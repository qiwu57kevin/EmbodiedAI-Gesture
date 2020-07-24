using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// EnableDisableKinect is used to turn on or off the Kinect
public class EnableDisableKinect : MonoBehaviour
{
    public GameObject kinectController;
    public bool enableKinect = true;

    void Awake()
    {
        if(!enableKinect)
        {
        	kinectController.SetActive(false);
        }
    }
}
