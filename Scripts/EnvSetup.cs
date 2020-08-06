using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnvSetup : MonoBehaviour
{
    // Enumerators for all targets and tasks
    public enum targets
    {
        Cup, Laptop, Book, Switch, Monitor, Cellphone, Clock, Printer, Chair, Rack, Pen, Sofa
    };
    public enum tasks
    {
        GoTo, PickUp, Bring, TurnOn, TurnOff
    };
    private int numTargets = Enum.GetValues(typeof(targets)).Length;
    private int numTasks = Enum.GetValues(typeof(tasks)).Length;
    private static readonly targets[] targetPickUp = {targets.Cup, targets.Laptop, targets.Book, targets.Cellphone, targets.Pen};
    private static readonly targets[] targetBring = {targets.Cup, targets.Laptop, targets.Book, targets.Monitor, targets.Cellphone, targets.Printer, targets.Chair, targets.Pen};
    private static readonly targets[] targetTurnOnOff = {targets.Switch, targets.Monitor};
    
    [Tooltip("Select if the agent is in training. Deselect for inference or heuristic mode.")]
    public bool isTraining;
    [Header("Task/Target Setup")]
    public targets targetSelected;
    [Range(0,10)]
    public int targetChildNum = 1; // which child of the target is selected; initialized to be 1 (the first child)
    public tasks taskSelected;
    [Tooltip("Auto set task and target. Deselect to fix task and target choice.")]
    public bool autoSet = false;

    [Header("Training/Inference Setup")]
    // academy parameters
    [Tooltip("If shuffle replay list and select from it.")]
    public bool shuffleReplay;
    [Tooltip("Select to enable replaying from recordings.")]
    public bool replayInInference;
    public int testIdx;

    public bool TrainingCheck()
    {
        return isTraining;
    }

    public void settingTaskTarget()
    {   
        // Auto set task and target
        if(autoSet)
        {
            // taskSelected = (tasks)Random.Range(0, numTasks);
            taskSelected = tasks.GoTo; // only Goto for simplicity of training
            switch(taskSelected)
            {
                case tasks.GoTo:
                    // targetSelected = (targets)Random.Range(0,numTargets);
                    targets[] currentTargetList = {targets.Cup, targets.Chair, targets.Rack, targets.Sofa};
                    targetSelected = currentTargetList[Random.Range(0,4)];
                    break;
                case tasks.PickUp:
                    targetSelected = targetPickUp[Random.Range(0,targetPickUp.Length)];
                    break;
                case tasks.Bring:
                    targetSelected = targetBring[Random.Range(0,targetBring.Length)];
                    break;
                case tasks.TurnOn:
                case tasks.TurnOff:
                    targetSelected = targetTurnOnOff[Random.Range(0,targetTurnOnOff.Length)];
                    break;
            }

            switch(targetSelected)
            {
                case targets.Laptop:
                case targets.Book:
                case targets.Monitor:
                case targets.Cellphone:
                case targets.Clock:
                case targets.Printer:
                case targets.Pen:
                    targetChildNum = 0;
                    break;
                case targets.Cup:
                    targetChildNum = Random.Range(0,5);
                    break;
                case targets.Switch:
                    targetChildNum = Random.Range(0,2);
                    break;
                case targets.Chair:
                    targetChildNum = Random.Range(0,8);
                    break;
                case targets.Rack:
                    targetChildNum = Random.Range(0,6);
                    break;
                case targets.Sofa:
                    targetChildNum = Random.Range(0,6);
                    break;
            }
        }
                
        if (taskSelected == tasks.PickUp && targetSelected != targets.Cup && targetSelected != targets.Book && targetSelected != targets.Laptop)
        {
            Debug.LogError("Academy: \"PickUp\" can only be used with \"Cup\" or \"Book\" or \"Laptop\"");
            return;
        }
        else if (taskSelected == tasks.Bring && targetSelected != targets.Cup && targetSelected != targets.Book && targetSelected != targets.Laptop)
        {
            Debug.LogError("Academy: \"Bring\" can only be used with \"Cup\" or \"Book\" or \"Laptop\"");
            return;
        }
        else if (taskSelected == tasks.TurnOn && targetSelected != targets.Laptop && targetSelected != targets.Monitor && targetSelected != targets.Switch)
        {
            Debug.LogError("Academy: \"Turn On\" can only be used with \"Laptop\" or \"Monitor\" or \"Switch\"");
            return;
        }
        else if (taskSelected == tasks.TurnOff && targetSelected != targets.Laptop && targetSelected != targets.Monitor && targetSelected != targets.Switch)
        {
            Debug.LogError("Academy: \"Turn TurnOff\" can only be used with \"Laptop\" or \"Monitor\" or \"Switch\"");
            return;
        }                         
    }
}
