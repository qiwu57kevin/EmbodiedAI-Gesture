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
    InputField recorderName;
    Button recStartEnd;
    Button recSave;
    Dropdown targetDropdown;
    Dropdown targetNumDropdown;
    Toggle LeftRight;
    Toggle TrainTest;

    // Recorder of Leap and Kinect gameobjects
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

        kinectRecorder = new GameObjectRecorder(kinectAvatar);
        leapLeftRecorder = new GameObjectRecorder(leapHandLeft);
        leapRightRecorder = new GameObjectRecorder(leapHandRight);

        // Initiate dropdown options
        if(!isTraining)
        {
            recInfo = GameObject.Find("recInfo").GetComponent<Text>();
            recorderName = GameObject.Find("recorder").GetComponent<InputField>();
            recStartEnd = GameObject.Find("recStartEnd").GetComponent<Button>();
            recSave = GameObject.Find("recSave").GetComponent<Button>();
            targetDropdown = GameObject.Find("target").GetComponent<Dropdown>();
            targetNumDropdown = GameObject.Find("targetNum").GetComponent<Dropdown>();
            LeftRight = GameObject.Find("handGroup/left").GetComponent<Toggle>();
            TrainTest = GameObject.Find("train").GetComponent<Toggle>();

            // targetDropdown.ClearOptions();
            targetNumDropdown.ClearOptions();
            // targetDropdown.AddOptions(Enum.GetNames(typeof(EnvSetup.targets)).ToList());
            targetNumDropdown.AddOptions(Enumerable.Range(0,10).Select(num => num.ToString()).ToList());
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
            }
            else
            {
                ResetRecorder();
                recSave.interactable = false;
                recInfo.text = "Recording!";
                recStartEnd.GetComponentInChildren<Text>().text = "End Recording";
            }
        }
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

    private void TakeRecordings()
    {
        kinectRecorder.TakeSnapshot(Time.deltaTime);
        leapLeftRecorder.TakeSnapshot(Time.deltaTime);
        leapRightRecorder.TakeSnapshot(Time.deltaTime);
    }

    private void SaveRecordingsToClip()
    {
        kinectRecorder.SaveToClip(kinectClip);
        leapLeftRecorder.SaveToClip(leapLeftClip);
        leapRightRecorder.SaveToClip(leapRightClip);
    }

    private void SaveClipToPath(AnimationClip clip, string path, string device, string recorder, string target, int targetNum, bool handedness, bool forTraining)
    {
        DateTime now = DateTime.Now;
        Directory.CreateDirectory(Directory.GetCurrentDirectory()+"/"+path+recorder);
        string filePath = path + recorder + "/" + String.Join("_",new string[]{device, recorder, target,
            targetNum.ToString(), handedness? "left":"right", now.ToString("MM-dd-yyyy_HH-mm-ss"), forTraining? "train":"test"}) + ".anim";
        AssetDatabase.CreateAsset(clip, filePath);
    }

    private void ResetRecorder()
    {
        kinectClip = new AnimationClip();
        leapLeftClip = new AnimationClip();
        leapRightClip = new AnimationClip();

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(kinectClip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(kinectClip, settings);

        settings = AnimationUtility.GetAnimationClipSettings(leapLeftClip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(leapLeftClip, settings);

        settings = AnimationUtility.GetAnimationClipSettings(leapRightClip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(leapRightClip, settings);

        kinectRecorder.ResetRecording();
        leapLeftRecorder.ResetRecording();
        leapRightRecorder.ResetRecording();

        kinectRecorder.BindComponentsOfType<Transform>(kinectRootObj,true);
        leapLeftRecorder.BindComponentsOfType<Transform>(leapLeftRootObj,true);
        leapRightRecorder.BindComponentsOfType<Transform>(leapRightRootObj,true);
    }

    // Mecanim bones used by the humanoid rig
    protected static readonly HumanBodyBones[] usedMecanimBones = new HumanBodyBones[]{

        HumanBodyBones.Hips,
		HumanBodyBones.Spine,
//      HumanBodyBones.Chest,
		HumanBodyBones.Neck,
//		HumanBodyBones.Head,
	
		HumanBodyBones.LeftUpperArm,
		HumanBodyBones.LeftLowerArm,
		HumanBodyBones.LeftHand,
//		HumanBodyBones.LeftIndexProximal,
//		HumanBodyBones.LeftIndexIntermediate,
//		HumanBodyBones.LeftThumbProximal,
	
		 HumanBodyBones.RightUpperArm,
		 HumanBodyBones.RightLowerArm,
		 HumanBodyBones.RightHand,
//		 HumanBodyBones.RightIndexProximal,
//		 HumanBodyBones.RightIndexIntermediate,
//		 HumanBodyBones.RightThumbProximal,
	
		 HumanBodyBones.LeftUpperLeg,
		 HumanBodyBones.LeftLowerLeg,
		 HumanBodyBones.LeftFoot,
//		 HumanBodyBones.LeftToes},
	
		 HumanBodyBones.RightUpperLeg,
		 HumanBodyBones.RightLowerLeg,
		 HumanBodyBones.RightFoot,
//		 HumanBodyBones.RightToes,
	
		 HumanBodyBones.LeftShoulder,
		 HumanBodyBones.RightShoulder,
		 HumanBodyBones.LeftIndexProximal,
		 HumanBodyBones.RightIndexProximal,
		 HumanBodyBones.LeftThumbProximal,
		 HumanBodyBones.RightThumbProximal,
    };
      
    IEnumerator RecInfoCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        recInfo.text = "";
    }
}
