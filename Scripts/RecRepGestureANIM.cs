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

    // Recorder of Leap hands
    GameObjectRecorder leapLeftRecorder;
    GameObjectRecorder leapRightRecorder;

    // AnimationCurves for Kinect recordings
    Dictionary<int, AnimationCurve> kinectMuscleCurves = new Dictionary<int, AnimationCurve>(); // Kinect avatar muscle values
    Dictionary<int, AnimationCurve> kinectRootCurves = new Dictionary<int, AnimationCurve>(); // Kinct avatar root motions
    // HumanPose and HumanPoseHandler for Kinect humanoid rig
    HumanPose humanPose = new HumanPose();
    HumanPoseHandler humanPoseHandler;

    // AnimationClip to save
    AnimationClip kinectClip;
    AnimationClip leapLeftClip;
    AnimationClip leapRightClip;

    // Recording status
    bool isRecording = false;
    bool isTraining = false;
    float time = 0f; // time elapsed

    EnvSetup academyAgent;

    public GameObject kinectAvatar;
    public GameObject leapHandLeft;
    public GameObject leapHandRight;

    public GameObject leapLeftRootObj;
    public GameObject leapRightRootObj;

    void Start()
    {
        academyAgent = GameObject.Find("EnvSetup").GetComponent<EnvSetup>();
        isTraining = academyAgent.TrainingCheck();

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

        // Initialized Kinect setup
        humanPoseHandler = new HumanPoseHandler(kinectAvatar.GetComponent<Animator>().avatar, kinectAvatar.transform);

        foreach(HumanBodyBones boneType in Kinect2MecanimBones)
        {
            for(int i=0;i<3;i++)
            {
                int muscle = HumanTrait.MuscleFromBone((int)boneType, i);
                if(!kinectMuscleCurves.ContainsKey(muscle) && muscle!=-1)
                    kinectMuscleCurves.Add(muscle, null);
            }
        }

        for(int i=0;i<10;i++)
        {
            kinectRootCurves.Add(i, null);
        }
    }

    void LateUpdate()
    {
        if(isRecording)
        {
            time += Time.deltaTime;
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

                time = 0f;
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

    private void SaveClipToPath(AnimationClip clip, string path, string device, string recorder, string target, int targetNum, bool handedness, bool forTraining)
    {
        DateTime now = DateTime.Now;
        Directory.CreateDirectory(Directory.GetCurrentDirectory()+"/"+path+recorder);
        string filePath = path + recorder + "/" + String.Join("_",new string[]{device, recorder, target,
            targetNum.ToString(), handedness? "left":"right", now.ToString("MM-dd-yyyy_HH-mm-ss"), forTraining? "train":"test"}) + ".anim";
        AssetDatabase.CreateAsset(clip, filePath);
    }

    private void TakeRecordings()
    {
        // Leap
        leapLeftRecorder.TakeSnapshot(Time.deltaTime);
        leapRightRecorder.TakeSnapshot(Time.deltaTime);

        // Kinect
        humanPoseHandler.GetHumanPose(ref humanPose);
        foreach(var item in kinectMuscleCurves)
        {
            item.Value.AddKey(time, humanPose.muscles[item.Key]);
        }

        for(int i=0;i<3;i++)
        {
            kinectRootCurves[i].AddKey(time, humanPose.bodyPosition[i]);
        }
        for(int i=0;i<4;i++)
        {
            kinectRootCurves[i+3].AddKey(time, humanPose.bodyRotation[i]);
        }
        for(int i=0;i<3;i++)
        {
            kinectRootCurves[i+7].AddKey(time, kinectAvatar.transform.localPosition[i]);
        }
    }

    private void SaveRecordingsToClip()
    {
        // Leap
        leapLeftRecorder.SaveToClip(leapLeftClip);
        leapRightRecorder.SaveToClip(leapRightClip);

        // Kinect
        foreach(var item in kinectMuscleCurves)
        {
            kinectClip.SetCurve("", typeof(Animator), HumanTrait.MuscleName[item.Key], item.Value);
        }

        kinectClip.SetCurve("", typeof(Animator),"RootT.x", kinectRootCurves[0]);
        kinectClip.SetCurve("", typeof(Animator),"RootT.y", kinectRootCurves[1]);
        kinectClip.SetCurve("", typeof(Animator),"RootT.z", kinectRootCurves[2]);
        kinectClip.SetCurve("", typeof(Animator),"RootQ.x", kinectRootCurves[3]);
        kinectClip.SetCurve("", typeof(Animator),"RootQ.y", kinectRootCurves[4]);
        kinectClip.SetCurve("", typeof(Animator),"RootQ.z", kinectRootCurves[5]);
        kinectClip.SetCurve("", typeof(Animator),"RootQ.w", kinectRootCurves[6]);
        kinectClip.SetCurve("", typeof(Transform),"localPosition.x", kinectRootCurves[7]);
        kinectClip.SetCurve("", typeof(Transform),"localPosition.y", kinectRootCurves[8]);
        kinectClip.SetCurve("", typeof(Transform),"localPosition.z", kinectRootCurves[9]);
    }

    private void ResetRecorder()
    {
        // Reset all clips
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

        // Leap specific
        leapLeftRecorder.ResetRecording();
        leapRightRecorder.ResetRecording();
        leapLeftRecorder.BindComponentsOfType<Transform>(leapLeftRootObj,true);
        leapRightRecorder.BindComponentsOfType<Transform>(leapRightRootObj,true);

        // Kinect specific
        foreach(int boneIdx in kinectMuscleCurves.Keys.ToList())
        {
            kinectMuscleCurves[boneIdx] = new AnimationCurve();
        }
        foreach(int rootIdx in kinectRootCurves.Keys.ToList())
        {
            kinectRootCurves[rootIdx] = new AnimationCurve();
        }
    }

    // Mecanim bones used by the humanoid rig
    public static readonly HumanBodyBones[] Kinect2MecanimBones = new HumanBodyBones[]{

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
