using System;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class DataIO: MonoBehaviour
{
    string dataAbsolutePath;

    // Agent and academy
    Academy_Agent academyAgent;
    Controller_Agent agent;
    GameObject agentObj;
    bool isTraining;

    // Devie,and target setup
    string[] _DeviceList;
    string[] _TargetList;
    Academy_Agent.targets target;
    int targetChildNum;

    // Kinect avatar model
    GameObject kinectAvatar;  
    RecordGesture animationRecorder;

    // Create dictionary to store all files
    Dictionary<Tuple<string, string, int>, List<string>> recording;
    bool kinectLoadData = false;
    bool leapLoadData;

    string customLogger = ""; //Retrieve training data from agent

    void Awake()
    {
        academyAgent = GameObject.Find("AgentAcademy").GetComponent<Academy_Agent>();
        if (academyAgent!=null)
        {
            isTraining = academyAgent.isTraining;
        }
        else
        {
            Debug.LogError("DataIO: Can't find AgentAcademy");
            return;
        }

        _DeviceList = new string[]{"Kinect", "Leap"};
        _TargetList = Enum.GetNames(typeof(Academy_Agent.targets)).ToArray();
        InitializeRecording();

        // Get recording and logger path
        dataAbsolutePath = Directory.GetCurrentDirectory() + "/Assets/Recordings/Archived/";

        // Load all files in the recording archive
        LoadFileNames(dataAbsolutePath);
    }

    void Start()
    {       
        // load setup

        agentObj = GameObject.Find("RobotAgent");
        if (agentObj==null)
        {
            Debug.LogError("DataIO: Can't find RobotAgent");
            return;
        }
        else
        {
            agent = agentObj.GetComponent<Controller_Agent>();
        }

        kinectAvatar = GameObject.Find("KinectAvatar");
        if (kinectAvatar==null)
        {
            Debug.LogError("DataIO: Can't find KinectAvatar");
        }
        else
        {
            animationRecorder = kinectAvatar.GetComponent<RecordGesture>();
        }

        // Set application target framerate
        Application.targetFrameRate = 60;
    }

    // look up agentAcademy for Test index and Target
    public bool RigOverwriter(string Rig)
    {
        target = academyAgent.targetSelected;
        targetChildNum = academyAgent.targetChildNum;

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
