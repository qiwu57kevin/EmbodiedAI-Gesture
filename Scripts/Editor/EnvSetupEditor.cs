using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EnvSetup))]
public class EnvSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var myScript = target as EnvSetup;

        if(!myScript.isTraining)
        {
            myScript.replayInInference = EditorGUILayout.Toggle("Replay In Inference", false);
        }
    }
}
