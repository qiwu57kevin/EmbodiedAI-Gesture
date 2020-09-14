using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnvSetup : MonoBehaviour
{
    public enum tasks
    {
        GoTo, PickUp, Bring, TurnOn, TurnOff
    };
    private int numObjCats = Enum.GetValues(typeof(NavObj.ObjCategory)).Length;
    private int numTasks = Enum.GetValues(typeof(tasks)).Length;
    
    [Header("Task/Target Setup")]
    [Tooltip("Object category selected.")]
    public NavObj.ObjCategory objCatSelected;
    [Tooltip("Select location index where the object will appear.")][Range(0,9)]
    public int objLocationIdxSelected = 0;
    public tasks taskSelected;
    [Tooltip("Auto set task and target. Deselect to fix task and target choice.")]
    public bool autoSetTarget = false;
    [Tooltip("Auto set room. Deselect to fix room choice.")]
    public bool autoSetRoom = false;

    [Header("Training/Inference Setup")]
    // academy parameters
    [Tooltip("Select if the agent is in training. Deselect for inference or heuristic mode.")]
    public bool isTraining;
    [Tooltip("If shuffle replay list and select from it.")]
    public bool shuffleReplay;
    [Tooltip("Index for selection of the replayed file.")]
    public int testIdx;
    [Tooltip("Select to enable replaying from recordings.")]
    [HideInInspector]public bool replayInInference = false;

    public bool TrainingCheck()
    {
        return isTraining;
    }

    public void settingTaskTarget()
    {   
        // Auto set object category and location
        if(autoSetTarget)
        {
            taskSelected = tasks.GoTo; // only GoTo for object navigation purpose
            switch(taskSelected)
            {
                case tasks.GoTo:
                    objCatSelected = (NavObj.ObjCategory)Random.Range(0,numObjCats);
                    objLocationIdxSelected = Random.Range(0,10);
                    break;
                case tasks.PickUp:
                    break;
                case tasks.Bring:
                    break;
                case tasks.TurnOn:
                    break;
                case tasks.TurnOff:
                    break;
            }
        }               
    }
}
