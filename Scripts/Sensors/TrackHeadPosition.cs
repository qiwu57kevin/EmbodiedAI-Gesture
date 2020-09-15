using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackHeadPosition : MonoBehaviour
{
    // Transform of player head
    public Transform headTransform;

    // [Header("OVRCamera Setting")]
    // public float xOffset = 0f;
    // public float yOffset = 0f;
    // public float zOffset = 0f;

    void LateUpdate()
    {
       // Let the OVR camera follow Kinect avatar head movement
    //    this.transform.position = headTransform.position;
        GameObject.Find("CenterEyeAnchor").transform.position = headTransform.position;
    }
}
