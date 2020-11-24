using System.IO;
using UnityEngine;

public class PathDraw2D : MonoBehaviour
{
    public AgentController agentCtrl;
    public TrailRenderer navTrail;
    public TrailRenderer helpTrail;

    void Update()
    {
        // if(navTrail.emitting)
        // if(agentCtrl.drawAgentPath)
        // {
        //     if(agentCtrl.isHelped)
        //     {
        //         navTrail.emitting = false;
        //         helpTrail.emitting = true;
        //         // navTrail.colorGradient = helpGradient;
        //         // navTrail.startColor = helpColor;
        //     }
        //     else
        //     {
        //         navTrail.emitting = true;
        //         helpTrail.emitting = false;
        //         // navTrail.colorGradient = navGradient;
        //         // navTrail.startColor = navColor;
        //     }
        // }
    }

    public void Activate()
    {
        // navTrail.emitting = true;
    }

    public void DeActivate()
    {
        // navTrail.emitting = false;
    }

    public void ResetPathDraw()
    {
        // navTrail.Clear();
        // helpTrail.Clear();
    }

    // Save agent path to a png file
    public static void SavePath2PNG(Camera obsCam, string savePath)
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture rt = new RenderTexture(1600, 1000, 24);
        obsCam.targetTexture = rt;
        RenderTexture.active = obsCam.targetTexture;
 
        obsCam.Render();
 
        Texture2D Image = new Texture2D(1600, 1000);
        Image.ReadPixels(new Rect(0, 0, obsCam.targetTexture.width, obsCam.targetTexture.height), 0, 0);
        Image.Apply();
        RenderTexture.active = currentRT;
        obsCam.targetTexture = null;
 
        var Bytes = ImageConversion.EncodeToPNG(Image);
        Destroy(Image);
 
        File.WriteAllBytes(savePath, Bytes);
    }
}
