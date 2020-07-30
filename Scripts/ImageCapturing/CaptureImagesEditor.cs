using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(CaptureImages))]
public class CaptureImagesEditor: Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CaptureImages myScript = (CaptureImages)target;

        if(GUILayout.Button("Capture Target Images"))
        {
            myScript.GenerateTargetImages();
        }
    }
}
