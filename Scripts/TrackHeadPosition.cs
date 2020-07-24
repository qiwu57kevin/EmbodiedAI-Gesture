using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackHeadPosition : MonoBehaviour
{
    KinectManager manager;
    Vector3 headPosition;

    [Header("OVRCamera Setting")]
    public float xOffset = 0f;
    public float yOffset = 0f;
    public float zOffset = 0f;

	void Start()
	{
		// Get Kinect Manager instance
        manager = KinectManager.Instance;
	}

    void FixedUpdate()
    {
        if(manager && manager.IsInitialized())
        {
            if(manager.IsUserDetected())
            {
                long userId = manager.GetPrimaryUserID();

                if(manager.IsJointTracked(userId, (int)KinectInterop.JointType.Head))
                {
                    headPosition = manager.GetJointPosition(userId, (int)KinectInterop.JointType.Head);
                    transform.position = new Vector3(headPosition.x + xOffset, headPosition.y + yOffset, headPosition.z + zOffset); // adjust camera's position with the offsets
                }
            }
        }
    }
}
