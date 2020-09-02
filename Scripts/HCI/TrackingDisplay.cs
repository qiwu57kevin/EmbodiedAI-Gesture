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
    [Header("Leap IR Image Display")]
    public bool displayLeapImage = false;
    public RawImage LeapImage;
    // Texture 2D to save Leap image data
    public RenderTexture leapRT;
    private Texture2D leapTex2D;

    [Space]
    public KinectManager kinectManager;
    public LeapServiceProvider leapServiceProvider;
    private Controller leapController;

    void Awake()
    {   
        if(displayLeapImage)
            leapController = leapServiceProvider.GetLeapController();
    }

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
        // Disable Leap image display due to crash on Unity
        // if(!displayLeapImage)
        // {
        //     LeapImage.gameObject.SetActive(false);
        //     GameObject.Find("LeapImageLabel").SetActive(false);
        // }
        // else
        // {
        //     LeapImage.gameObject.SetActive(true);
        //     GameObject.Find("LeapImageLabel").SetActive(true);
        //     if(leapController!=null)
        //     {
        //         leapController.ImageReady += onImageReady;
        //     }
        //     else
        //     {
        //         Debug.LogWarning("Warning - Leap Controller is null.");
        //     }
        // }
    }

    void Update()
    {
        if(displayKinectImage)
        {
            KinectImage.texture = kinectManager.GetUsersClrTex();
			KinectImage.rectTransform.localScale = kinectManager.GetColorImageScale();
        }
    }

    // private void onImageReady(object sender, Leap.ImageEventArgs args)
    // {
    //     if(leapTex2D==null)
    //         leapTex2D = new Texture2D(args.image.Width, args.image.Height, TextureFormat.R8, false);
    //     leapTex2D.LoadRawTextureData(args.image.Data(Leap.Image.CameraType.LEFT));
    //     leapTex2D.Apply();
    //     Graphics.Blit(leapTex2D, leapRT);
    // }
}
