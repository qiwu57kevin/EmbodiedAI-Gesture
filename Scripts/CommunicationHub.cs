using System;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;

// A class used to communicate between agent and recordings
public class CommunicationHub: MonoBehaviour
{
    // Path to read recordings.
    public string recordingSavePath = "Recordings/Archived/";
    private string recordingAbsPath;

    // Agent and academy
    public EnvSetup envSetup;
    public GameObject agentObj;
    private AgentController agent;
    bool isTraining;

    // Devie,and target setup
    private string[] _DeviceList;
    private string[] _TargetList;
    private EnvSetup.targets target;
    private int targetChildNum;

    // Kinect avatar model
    public GameObject kinectAvatar;  
    RecordReplayGesture animationRecorder;

    // Create dictionary to store all files
    Dictionary<Tuple<string, string, int>, List<string>> recording;
    // If Kinect or Leap data has been loaded for current episode
    bool kinectLoadData = false;
    bool leapLoadData = false;

    void Awake()
    {
        if (envSetup!=null)
        {
            isTraining = envSetup.isTraining;
        }
        else
        {
            Debug.LogError("Can't find EnvSetup");
            return;
        }

        _DeviceList = new string[]{"Kinect", "Leap"};
        _TargetList = Enum.GetNames(typeof(EnvSetup.targets)).ToArray();
        InitializeRecording();

        // Get recording and logger path
        recordingAbsPath = Directory.GetCurrentDirectory() + "/Assets/" + recordingSavePath;

        // Load all files in the recording archive
        LoadFileNames(recordingAbsPath);
    }

    void Start()
    {       
        // Load setup
        if (agentObj==null)
        {
            Debug.LogError("Can't find RobotAgent");
            return;
        }
        else
        {
            agent = agentObj.GetComponent<AgentController>();
        }

        if (kinectAvatar==null)
        {
            Debug.LogError("Can't find KinectAvatar");
        }
        else
        {
            animationRecorder = kinectAvatar.GetComponent<RecordReplayGesture>();
        }

        // Set application target framerate
        Application.targetFrameRate = 60;
    }

    // look up EnvSetup for Test index and Target
    public bool RigOverwriter(string Rig)
    {
        target = envSetup.targetSelected;
        targetChildNum = envSetup.targetChildNum;

        // Check if the device is corrent
        if (!string.Equals(Rig, "Kinect") && !string.Equals(Rig, "Leap"))
        {
            Debug.LogError("DataIO: Wrong rig overwriter name");
            return false;
        }
        
        List<string> selectedList = recording[new Tuple<string, string, int>(Rig, target.ToString(), targetChildNum)];

        int listLength = selectedList.Count;
        if (listLength == 0)
        {
            Debug.LogError($"DataIO: database does not contain: {Rig}-{target.ToString()}-{targetChildNum.ToString()}");
            return false;
        }
        

        if (string.Equals(Rig, "Kinect"))
        {
            if (!kinectLoadData)
            {
                animationRecorder.LoadAnimation(selectedList[Random.Range(0,listLength)]);
                animationRecorder.StartEndReplay();
                kinectLoadData = true;
            }
        }         

        return true;
    }


    // Initialize recording
    void InitializeRecording()
    {
        recording = new Dictionary<Tuple<string, string, int>, List<string>>();
        foreach(string device in _DeviceList)
        {
            foreach(string target in _TargetList) 
            {
                for(int i=0; i<=10; i++)
                {
                    recording.Add(new Tuple<string, string, int>(device, target, i), new List<string>());
                }
            }
        }
        
    }
    
    // Load filenames based on training or testing phase
    void LoadFileNames(string fileloc)
    {
        foreach (string filename in Directory.GetFiles(fileloc).Where(file => isTraining? file.Contains("train")&&!file.EndsWith(".meta"):file.Contains("train")&&!file.EndsWith(".meta")))
        {
            string[] absFileName = filename.Split('/');
            string[] pt = absFileName[absFileName.Length-1].Split('_');
            string device = pt[0];
            string target = pt[2];
            int targetChildNum = int.Parse(pt[3]);
            if (Array.Exists(_DeviceList, element => element == device) && Array.Exists(_TargetList, element => element == target))
                recording[new Tuple<string, string, int>(device, target, targetChildNum)].Add(absFileName[absFileName.Length-1]); 
            else
                Debug.LogWarning($"DataIO: {filename} is not valid to load");
        }
    }
    
    // reset data loader, called when agent reset
    public void RequestData()
    {
        kinectLoadData = false;
        leapLoadData = false;
        animationRecorder.StartEndReplay();
    }
}
