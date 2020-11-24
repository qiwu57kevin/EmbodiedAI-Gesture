using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Leap;
using Leap.Unity;

public class TrackingDisplay : MonoBehaviour
{
    [Header("Kinect Color Image Display")]
    public bool displayKinectImage = false;
    public RawImage KinectImage;
    public KinectManager kinectManager;

    void Start()
    {
        if(!displayKinectImage)
        {
            KinectImage.gameObject.SetActive(false);
            GameObject.Find("KinectImageLabel").SetActive(false);
        }
        else
        {
            KinectImage.gameObject.SetActive(true);
            GameObject.Find("KinectImageLabel").SetActive(true);
        }
    }

    void Update()
    {
        if(displayKinectImage)
        {
            KinectImage.texture = kinectManager.GetUsersClrTex();
			KinectImage.rectTransform.localScale = kinectManager.GetColorImageScale();
        }
    }
}
