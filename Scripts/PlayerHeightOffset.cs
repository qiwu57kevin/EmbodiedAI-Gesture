using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHeightOffset : MonoBehaviour
{
    [Range(1f, 2f)][Tooltip("Player height in meters. Default 1.75m")]
    public float playerHeight = 1.75f;
    static readonly float defaultHeight = 1.75f;

    public void OffsetPlayerHeight()
    {

    }

    private void AdjustKinect()
    {

    }

    private void AdjustLeap()
    {

    }
}
