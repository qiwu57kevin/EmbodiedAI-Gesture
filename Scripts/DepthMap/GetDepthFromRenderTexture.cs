﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GetDepthFromRenderTexture : MonoBehaviour
{
	public int x_position;
	public int y_position;
 //  Just for reference
 //  Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
 
 //  // ofc you probably don't have a class that is called CameraController :P
 // Camera activeCamera = CameraController.getActiveCamera();
 
	// // Initialize and render
	// RenderTexture rt = new RenderTexture(width, height, 24);
	// activeCamera.targetTexture = rt;
	// activeCamera.Render();
	// RenderTexture.active = rt;
	 
	// // Read pixels
	// tex.ReadPixels(rectReadPicture, 0, 0);
	 
	// // Clean up
	// activeCamera.targetTexture = null;
	// RenderTexture.active = null; // added to avoid errors 
	// DestroyImmediate(rt);

	private void Update()
	{
		//Create a new texture with the width and height of the screen
        Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        //Read the pixels in the Rect starting at 0,0 and ending at the screen's width and height
        texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false);
        texture.Apply();
        //Fetch the array of pixels in the screen
        Color[] pix = texture.GetPixels(x_position,y_position,1,1);
        Debug.Log(pix[0].ToString());
	}
}