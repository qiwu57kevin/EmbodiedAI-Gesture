using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
public class RecRepGestureANIM : MonoBehaviour
{
    // Record and replay gestures in the form of .anim files

    // Uniy screen UI info
    Text recInfo;
    Text replayInfo;
    InputField replayFile;
    InputField recorderName;
    Button recStartEnd;
    Button recSave;
    Button replayStartEnd;
    Button replayLoad;
    Dropdown targetDropdown;
    Dropdown targetNumDropdown;
    Toggle LeftRight;
    Toggle TrainTest;

    // Recoder of Leap and Kinect gameobjects
    GameObjectRecorder kinectRecorder;
    GameObjectRecorder leapLeftRecorder;
    GameObjectRecorder leapRightRecorder;

    // AnimationClip to save
    AnimationClip kinectClip;
    AnimationClip leapLeftClip;
    AnimationClip leapRightClip;

    // Recording status
    bool isRecording = false;
    bool isTraining = false;
    EnvSetup academyAgent;

    public GameObject kinectAvatar;
    public GameObject leapHandLeft;
    public GameObject leapHandRight;

    public GameObject kinectRootObj;
    public GameObject leapLeftRootObj;
    public GameObject leapRightRootObj;

    void Start()
    {
        academyAgent = GameObject.Find("EnvSetup").GetComponent<EnvSetup>();
        isTraining = academyAgent.TrainingCheck();

        kinectClip = new AnimationClip();
        leapLeftClip = new AnimationClip();
        leapRightClip = new AnimationClip();

        kinectRecorder = new GameObjectRecorder(kinectAvatar);
        leapLeftRecorder = new GameObjectRecorder(leapHandLeft);
        leapRightRecorder = new GameObjectRecorder(leapHandRight);

        // Initiate dropdown options
        if(!isTraining)
        {
            recInfo = GameObject.Find("recInfo").GetComponent<Text>();
            replayInfo = GameObject.Find("replayInfo").GetComponent<Text>();
            replayFile = GameObject.Find("replayFile").GetComponent<InputField>();
            recorderName = GameObject.Find("recorder").GetComponent<InputField>();
            recStartEnd = GameObject.Find("recStartEnd").GetComponent<Button>();
            recSave = GameObject.Find("recSave").GetComponent<Button>();
            replayStartEnd = GameObject.Find("replayStartEnd").GetComponent<Button>();
            replayLoad = GameObject.Find("replayLoad").GetComponent<Button>();
            targetDropdown = GameObject.Find("target").GetComponent<Dropdown>();
            targetNumDropdown = GameObject.Find("targetNum").GetComponent<Dropdown>();
            LeftRight = GameObject.Find("handGroup/left").GetComponent<Toggle>();
            TrainTest = GameObject.Find("train").GetComponent<Toggle>();

            targetDropdown.ClearOptions();
            targetNumDropdown.ClearOptions();
            targetDropdown.AddOptions(Enum.GetNames(typeof(EnvSetup.targets)).ToList());
            targetNumDropdown.AddOptions(Enumerable.Range(0,10).Select(num => num.ToString()).ToList());

            ResetRecorder();
        }
    }

    void LateUpdate()
    {
        if(isRecording)
        {
            TakeRecordings();
        }
    }

    public void StartEndRecording()
    {
        isRecording = !isRecording;
        if(!isTraining)
        {
            if(!isRecording)
            {
                SaveRecordingsToClip();

                recSave.interactable = true;
                recInfo.text = "Record Ended!";
                recStartEnd.GetComponentInChildren<Text>().text = "Start Recording";
                StartCoroutine(RecInfoCoroutine());

                ResetRecorder();
            }
            else
            {
                recSave.interactable = false;
                replayStartEnd.interactable = false;
                recInfo.text = "Recording!";
                recStartEnd.GetComponentInChildren<Text>().text = "End Recording";
            }
        }
    }

    public void TakeRecordings()
    {
        kinectRecorder.TakeSnapshot(Time.deltaTime);
        leapLeftRecorder.TakeSnapshot(Time.deltaTime);
        leapRightRecorder.TakeSnapshot(Time.deltaTime);
    }

    public void SaveRecordings()
    {
        string recorder = recorderName.text;
        string target = targetDropdown.captionText.text;
        int targetNum = targetNumDropdown.value;
        bool handedness = LeftRight.isOn;
        bool forTraining = TrainTest.isOn;
        SaveClipToPath(kinectClip, "Assets/Recordings/Kinect/", "Kinect", recorder, target, targetNum, handedness, forTraining);
        SaveClipToPath(leapLeftClip, "Assets/Recordings/Leap/Left/", "LeapLeft", recorder, target, targetNum, handedness, forTraining);
        SaveClipToPath(leapRightClip, "Assets/Recordings/Leap/Right/", "LeapRight", recorder, target, targetNum, handedness, forTraining);
    }

    public void SaveRecordingsToClip()
    {
        kinectRecorder.SaveToClip(kinectClip);
        leapLeftRecorder.SaveToClip(leapLeftClip);
        leapRightRecorder.SaveToClip(leapRightClip);
    }

    public void SaveClipToPath(AnimationClip clip, string path, string device, string recorder, string target, int targetNum, bool handedness, bool forTraining)
    {
        DateTime now = DateTime.Now;
        Directory.CreateDirectory(Directory.GetCurrentDirectory()+"/"+path+recorder);
        string filePath = path + recorder + "/" + String.Join("_",new string[]{device, recorder, target,
            targetNum.ToString(), handedness? "left":"right", now.ToString("MM-dd-yyyy_HH-mm-ss"), forTraining? "train":"test"}) + ".anim";
        AssetDatabase.CreateAsset(clip, filePath);
    }

    public void ResetRecorder()
    {
        kinectRecorder.ResetRecording();
        leapLeftRecorder.ResetRecording();
        leapRightRecorder.ResetRecording();

        kinectRecorder.BindComponentsOfType<Transform>(kinectRootObj,true);
        leapLeftRecorder.BindComponentsOfType<Transform>(leapLeftRootObj,true);
        leapRightRecorder.BindComponentsOfType<Transform>(leapRightRootObj,true);
    }
      
    IEnumerator RecInfoCoroutine()
    {
        yield return new WaitForSeconds(1);
        recInfo.text = "";
    }

    IEnumerator ReplayInfoCoroutine()
    {
        yield return new WaitForSeconds(1);
        replayInfo.text = "";
    }  
}
