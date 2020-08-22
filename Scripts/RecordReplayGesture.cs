using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO; // needed for reading and writing .csv
using System.Text; // for csv 


public class RecordReplayGesture: MonoBehaviour
{
    [Header("Kinect Setting")]
    [Tooltip("Animator from Kinect Avatar")]
    public Transform kinectAvatar;

    [Header("Leap Setting")]
    public Transform leftLeapHand;
    public Transform rightLeapHand;

    [Header("Global Setting")]
    [Tooltip("Maximum number frames it can record.")]
    public int maxFrame = 10000; // max number of frames until unity stops recording automatically
    [Tooltip("Whether to loop the replayed animation. When set to true, the saved animation will be replayed repeatedly.")]
    public bool loopReplay = true; // whether to the loop the replayed animation

    HumanPose currentPose = new HumanPose(); // keeps track of currentPose while animated
    HumanPose poseToSet; // reassemble poses from .csv data
    HumanPoseHandler poseHandler; // to record and retarget animation

    float[] currentMuscles; // an array containig current Kinect avatar muscle values
    float[,] animationHumanPoses; // stack all poses in one array
    int counterRec = 0; // count number of frames
    [HideInInspector]
    public int counterPlay = 0; // count animation playback frames
    int counterLoad = 0; // count number of frames of loaded animation

    // Note down position and rotation at start
    Vector3 positionAtStart;
    Vector3 currentPosition;
    Vector3 posePositionAtStart;
    Quaternion rotationAtStart;
    Quaternion currentRotation;
    Quaternion poseRotationAtStart;

    // Used to set poses
    int muscleCount; // count the number of muscles of the Kinect avatar
    int leapCount; // count number of data tracked by Leap hands
    Transform[] leftHandTransforms; // note down all left hand transforms tracked by Leap
    Transform[] rightHandTransforms; // note down all right hand transforms tracked by Leap
    bool recordPoses = false;
    bool reapplyPoses = false; // the recorded animation

    EnvSetup academyAgent;

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

    bool isTraining;

    void Start()
    {
        muscleCount = HumanTrait.MuscleCount;
        currentMuscles = new float[muscleCount];

        leftHandTransforms = leftLeapHand.GetComponentsInChildren<Transform>(true);
        rightHandTransforms = rightLeapHand.GetComponentsInChildren<Transform>(true);
        leapCount = 7*(leftHandTransforms.Length+rightHandTransforms.Length);

        animationHumanPoses = new float[maxFrame, muscleCount+7 + leapCount];

        poseHandler = new HumanPoseHandler(kinectAvatar.GetComponent<Animator>().avatar, kinectAvatar);

        academyAgent = GameObject.Find("EnvSetup").GetComponent<EnvSetup>();
        isTraining = academyAgent.TrainingCheck();

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
        }
    }

    // Update is called once per frame
    // even running this in LateUpdate does not capture IK
    void LateUpdate()
    {
        if (recordPoses) { RecordPoses(); } 
        if (reapplyPoses) { reapplyPosesAnimation(); }
    }
    
    // Save entire animation as a csv file
    public void SaveAnimation()
    {
        // Retrieve current time in MM-dd-yyyy_HH:mm:ss
        DateTime now = DateTime.Now;

        string path = Directory.GetCurrentDirectory();
        path = path + "/Assets/Recordings/" + String.Join("_",new string[]{recorderName.text, targetDropdown.captionText.text, 
            targetNumDropdown.value.ToString(), LeftRight.isOn? "left":"right", now.ToString("MM-dd-yyyy_HH-mm-ss"), TrainTest.isOn? "train":"test"}) + ".csv";
        TextWriter sw = new StreamWriter(path);
        string line;

        for (int frame = 0; frame < counterRec; frame++) // run through all frames 
        {
            line = "";
            for (int i = 0; i < muscleCount+7+leapCount; i++) // and all values composing one Pose
            {
                line = line + animationHumanPoses[frame, i].ToString() + ";";
            }
            sw.WriteLine(line);
        }
        sw.Close();

        if(!isTraining)
        {
            recInfo.text = "Animation Saved!";
            recSave.interactable = false;
            StartCoroutine(RecInfoCoroutine());
        }
    }

    // Refill animationHumanPoses with values from loaded csv files
    public void LoadAnimation(string loadedFile)
    {
        string path = Directory.GetCurrentDirectory();
        path = path + "/Assets/Recordings/" + (loadedFile.EndsWith(".csv")? loadedFile:(loadedFile+".csv"));

        if (File.Exists(path))
        {
            if(!isTraining)
            {
                replayStartEnd.interactable = true;
                replayInfo.text = "File Loaded!";
            }

            StreamReader sr = new StreamReader(path);
            int frame = 0;
            string[] line;
            while (!sr.EndOfStream)
            {
                line = sr.ReadLine().Split(';');
                for (int num = 0; num < line.Length - 1; num++)
                {
                    animationHumanPoses[frame, num] = float.Parse(line[num]);
                }
                frame++;
            }
            counterLoad = frame;
        }
        else
        {
            if(!isTraining)
            {
                replayInfo.text = "File cannot be found in the directory!";
                StartCoroutine(ReplayInfoCoroutine());
            }
        }
    }

    // Load animation from the input text field. Used for recording.
    public void LoadAnimationFromTextField()
    {
        string path = Directory.GetCurrentDirectory();
        path = path + "/Assets/Recordings/" + (replayFile.text.EndsWith(".csv")? replayFile.text:(replayFile.text+".csv"));

        if (File.Exists(path))
        {
            if(!isTraining)
            {
                replayStartEnd.interactable = true;
                replayInfo.text = "File Loaded!";
            }

            StreamReader sr = new StreamReader(path);
            int frame = 0;
            string[] line;
            while (!sr.EndOfStream)
            {
                line = sr.ReadLine().Split(';');
                for (int num = 0; num < line.Length - 1; num++)
                {
                    animationHumanPoses[frame, num] = float.Parse(line[num]);
                }
                frame++;
            }
            counterLoad = frame;
        }
        else
        {
            if(!isTraining)
            {
                replayInfo.text = "File cannot be found in the directory!";
                StartCoroutine(ReplayInfoCoroutine());
            }
        }
    }

    public void StartEndRecording()
    {
        recordPoses = !recordPoses;
        if(!isTraining)
        {
            if(!recordPoses)
            {
                recSave.interactable = true;
                recInfo.text = "Record Ended!";
                recStartEnd.GetComponentInChildren<Text>().text = "Start Recording";
                StartCoroutine(RecInfoCoroutine());
            }
            else
            {
                counterRec = 0;
                recSave.interactable = false;
                replayStartEnd.interactable = false;
                recInfo.text = "Recording!";
                recStartEnd.GetComponentInChildren<Text>().text = "End Recording";
            }
        }
    }

    public void StartEndReplay()
    {
        if(recordPoses)
        {
            if(!isTraining)
            {
                replayInfo.text = "You cannot replay while recording!";
                StartCoroutine(ReplayInfoCoroutine());
            }
        }
        else
        {
            reapplyPoses = !reapplyPoses;
            if(!isTraining)
            {
                if(!reapplyPoses)
                {
                    replayLoad.interactable = true;
                    replayInfo.text = "Replay Ended!";
                    replayStartEnd.GetComponentInChildren<Text>().text = "Start Replay";
                    StartCoroutine(ReplayInfoCoroutine());
                }
                else
                {
                    counterRec = 0;
                    counterPlay = 0;
                    replayLoad.interactable = false;
                    replayInfo.text = "Replaying!";
                    replayStartEnd.GetComponentInChildren<Text>().text = "End Replay";
                }
            }
        }
    }

    // Record poses up to maximum frames
    public void RecordPoses()
    {
        poseHandler.GetHumanPose(ref currentPose);

        if (counterRec == 0)
        {
            positionAtStart = kinectAvatar.position;
            rotationAtStart = kinectAvatar.rotation;
            posePositionAtStart = currentPose.bodyPosition;
            poseRotationAtStart = currentPose.bodyRotation;

            animationHumanPoses[counterRec, 0] = positionAtStart.x;
            animationHumanPoses[counterRec, 1] = positionAtStart.y;
            animationHumanPoses[counterRec, 2] = positionAtStart.z;
            animationHumanPoses[counterRec, 3] = rotationAtStart.x;
            animationHumanPoses[counterRec, 4] = rotationAtStart.y;
            animationHumanPoses[counterRec, 5] = rotationAtStart.z;
            animationHumanPoses[counterRec, 6] = rotationAtStart.w;

            counterRec++;
        }
        else if (counterRec < maxFrame)
        {
            currentPosition = currentPose.bodyPosition;
            currentRotation = currentPose.bodyRotation;
            animationHumanPoses[counterRec, 0] = currentPosition.x - posePositionAtStart.x;
            animationHumanPoses[counterRec, 1] = currentPosition.y;
            animationHumanPoses[counterRec, 2] = currentPosition.z - posePositionAtStart.z;
            animationHumanPoses[counterRec, 3] = currentRotation.x;
            animationHumanPoses[counterRec, 4] = currentRotation.y;
            animationHumanPoses[counterRec, 5] = currentRotation.z;
            animationHumanPoses[counterRec, 6] = currentRotation.w;

            // Record Kinect avatar muscle values
            for (int i = 0; i < muscleCount; i++) 
            {
                animationHumanPoses[counterRec, i+7] = currentPose.muscles[i];
            }

            // Record position and rotation of Leap hand transforms
            for(int j = 0; j <leftHandTransforms.Length; j++)
            {
                animationHumanPoses[counterRec, muscleCount+7+j*7+0] = leftHandTransforms[j].position.x;
                animationHumanPoses[counterRec, muscleCount+7+j*7+1] = leftHandTransforms[j].position.y;
                animationHumanPoses[counterRec, muscleCount+7+j*7+2] = leftHandTransforms[j].position.z;
                animationHumanPoses[counterRec, muscleCount+7+j*7+3] = leftHandTransforms[j].rotation.x;
                animationHumanPoses[counterRec, muscleCount+7+j*7+4] = leftHandTransforms[j].rotation.y;
                animationHumanPoses[counterRec, muscleCount+7+j*7+5] = leftHandTransforms[j].rotation.z;
                animationHumanPoses[counterRec, muscleCount+7+j*7+6] = leftHandTransforms[j].rotation.w;          
            } 
            for(int k = 0; k <rightHandTransforms.Length; k++)
            {
                animationHumanPoses[counterRec, muscleCount+7+leftHandTransforms.Length*7+k*7+0] = rightHandTransforms[k].position.x;
                animationHumanPoses[counterRec, muscleCount+7+leftHandTransforms.Length*7+k*7+1] = rightHandTransforms[k].position.y;
                animationHumanPoses[counterRec, muscleCount+7+leftHandTransforms.Length*7+k*7+2] = rightHandTransforms[k].position.z;
                animationHumanPoses[counterRec, muscleCount+7+leftHandTransforms.Length*7+k*7+3] = rightHandTransforms[k].rotation.x;
                animationHumanPoses[counterRec, muscleCount+7+leftHandTransforms.Length*7+k*7+4] = rightHandTransforms[k].rotation.y;
                animationHumanPoses[counterRec, muscleCount+7+leftHandTransforms.Length*7+k*7+5] = rightHandTransforms[k].rotation.z;
                animationHumanPoses[counterRec, muscleCount+7+leftHandTransforms.Length*7+k*7+6] = rightHandTransforms[k].rotation.w;          
            }           
            counterRec++;
        }
    }

    // Loop through array and apply poses one frame after another. 
    public void reapplyPosesAnimation()
    {
        poseToSet = new HumanPose();

        int currentFrame = counterPlay%counterLoad;

        if (currentFrame == 0)
        {
            // Set Kinect avatar body location and rotation at start
            kinectAvatar.position = new Vector3(animationHumanPoses[currentFrame, 0],animationHumanPoses[currentFrame, 1],animationHumanPoses[currentFrame, 2]);
            // transform.rotation = new Quaternion(animationHumanPoses[currentFrame, 3],animationHumanPoses[currentFrame, 4],animationHumanPoses[currentFrame, 5],animationHumanPoses[currentFrame, 6]);
            kinectAvatar.rotation = Quaternion.identity;

            counterPlay++;
        }
        else if (!loopReplay && counterPlay >= counterLoad)
        {
            transform.position = positionAtStart;
            transform.rotation = rotationAtStart;
        }
        else
        {
            // Set Kinect avatar poses
            poseToSet.bodyPosition = new Vector3(animationHumanPoses[currentFrame, 0],animationHumanPoses[currentFrame, 1],animationHumanPoses[currentFrame, 2]);
            poseToSet.bodyRotation = new Quaternion(animationHumanPoses[currentFrame, 3],animationHumanPoses[currentFrame, 4],animationHumanPoses[currentFrame, 5],animationHumanPoses[currentFrame, 6]);
            for (int i = 0; i < muscleCount; i++) { currentMuscles[i] = animationHumanPoses[currentFrame, i+7]; } // somehow cannot directly modify muscle values
            poseToSet.muscles = currentMuscles;
            poseHandler.SetHumanPose(ref poseToSet);

            // Set Leap hands
            for(int j = 0; j <leftHandTransforms.Length; j++)
            {
                leftHandTransforms[j].position = new Vector3(animationHumanPoses[currentFrame, muscleCount+7+j*7+0],
                                                animationHumanPoses[currentFrame, muscleCount+7+j*7+1],animationHumanPoses[currentFrame, muscleCount+7+j*7+2]);
                leftHandTransforms[j].rotation = new Quaternion(animationHumanPoses[currentFrame, muscleCount+7+j*7+3],animationHumanPoses[currentFrame, muscleCount+7+j*7+4],
                                                animationHumanPoses[currentFrame, muscleCount+7+j*7+5],animationHumanPoses[currentFrame, muscleCount+7+j*7+6]);    
            } 
            for(int k = 0; k <rightHandTransforms.Length; k++)
            {
                leftHandTransforms[k].position = new Vector3(animationHumanPoses[currentFrame, muscleCount+7+leftHandTransforms.Length*7+k*7+0],
                                                animationHumanPoses[currentFrame, muscleCount+7+leftHandTransforms.Length*7+k*7+1],animationHumanPoses[currentFrame, muscleCount+7+leftHandTransforms.Length*7+k*7+2]);
                leftHandTransforms[k].rotation = new Quaternion(animationHumanPoses[currentFrame, muscleCount+7+leftHandTransforms.Length*7+k*7+3],animationHumanPoses[currentFrame, muscleCount+7+leftHandTransforms.Length*7+k*7+4],
                                                animationHumanPoses[currentFrame, muscleCount+7+leftHandTransforms.Length*7+k*7+5],animationHumanPoses[currentFrame, muscleCount+7+leftHandTransforms.Length*7+k*7+6]);       
            }           

            counterPlay++;
        }       
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


