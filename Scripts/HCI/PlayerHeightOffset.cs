using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHeightOffset : MonoBehaviour
{
    [Range(1f, 2f)][Tooltip("Player height in meters. Default 1.75m")]
    public float playerHeight = 1.75f;
    static readonly float defaultHeight = 1.75f;
    public string playerName;
    [Tooltip("A map between a player's name and height")]
    public List<PlayerInfo> playerHeightList = new List<PlayerInfo>();

    [Serializable]
    public struct PlayerInfo
    {
        public string name;
        public float height;

        public PlayerInfo(string _name, float _height)
        {
            name = _name;
            height = _height;
        }
    }

    [Tooltip("Kinect Avatar transform")]
    public Transform kinectAvatar;
    [Tooltip("OVRCameraRig transform")]
    public Transform OVRCameraRig;

    public void OffsetPlayerHeight()
    {
        AdjustKinect(playerHeight, kinectAvatar); AdjustLeap(playerHeight, OVRCameraRig);
    }

    public void SavePlayerInformation()
    {
        playerHeightList.Add(new PlayerInfo(playerName, playerHeight));
    }

    private void AdjustKinect(float height, Transform Kinect)
    {
        float scale = playerHeight/defaultHeight;
        Kinect.localScale = new Vector3(scale,scale,scale);
    }

    private void AdjustLeap(float height, Transform OVR)
    {
        OVR.position = GameObject.Find("Head").transform.position;
    }
}
