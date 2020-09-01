using System.IO;
using UnityEngine;

public class PathDraw2D : MonoBehaviour
{
    public static void EnablePathDraw()
    {
        GameObject.Find("AgentTrail").GetComponent<TrailRenderer>().emitting = true;
    }

    public static void ResetPathDraw()
    {
        GameObject.Find("AgentTrail").GetComponent<TrailRenderer>().Clear();
    }

    // Save agent path to a png file
    public static void SavePath2PNG(Camera obsCam, string savePath)
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture rt = new RenderTexture(1280, 720, 24);
        obsCam.targetTexture = rt;
        RenderTexture.active = obsCam.targetTexture;
 
        obsCam.Render();
 
        Texture2D Image = new Texture2D(1280, 720);
        Image.ReadPixels(new Rect(0, 0, obsCam.targetTexture.width, obsCam.targetTexture.height), 0, 0);
        Image.Apply();
        RenderTexture.active = currentRT;
 
        var Bytes = ImageConversion.EncodeToPNG(Image);
        Destroy(Image);
 
        File.WriteAllBytes(savePath, Bytes);
    }
}
