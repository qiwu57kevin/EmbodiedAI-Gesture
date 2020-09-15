using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Leap.Unity;

// EnableDisableKinect is used to turn on or off the Kinect
public class EnableDisableDevices : MonoBehaviour
{
    public KinectManager kinectManager;
    public LeapServiceProvider leapServiceProvider;
    public bool enableKinect = false;
    public bool enableLeap = false;


    void Start()
    {
        if(!enableKinect)
        {
        	kinectManager.enabled = false;
        }
        else
        {
            kinectManager.enabled = true;
        }
        // KinectManager.Instance.refreshAvatarControllers();

        if(!enableLeap)
        {
            leapServiceProvider.enabled = false;
        }
        else
        {
            leapServiceProvider.enabled = true;
        }
    }
}
