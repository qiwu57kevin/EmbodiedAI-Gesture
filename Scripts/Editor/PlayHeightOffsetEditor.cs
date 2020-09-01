using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(PlayerHeightOffset))]
public class PlayerHeightOffsetEditor: Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayerHeightOffset myScript = (PlayerHeightOffset)target;

        if(GUILayout.Button("Offset Player Height"))
        {
            myScript.OffsetPlayerHeight();
        }
        if(GUILayout.Button("Save Player Infomation"))
        {
            myScript.SavePlayerInformation();
        }
    }
}
