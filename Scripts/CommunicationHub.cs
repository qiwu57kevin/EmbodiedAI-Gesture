using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

// A class used to communicate between agent and recordings
public class CommunicationHub: MonoBehaviour
{
    [Header("Animation Playback")]
    // Path to read recordings.
    [Tooltip("The save path for object prefabs relative to the Assets folder")]
    public string objSavePath = "/Resources/NavObj/";
    [Tooltip("The save path for referencing gesture animation clips relative to the Assets folder")]
    public string animSavePath = "/Resources/Recordings/";
    private string recordingAbsPath;
    private string animAbsPath;
    private string stopAnimAbsPath;
    [Tooltip("Script that controlls the replay of animation clips")]
    public RepGestureAnim replayController;
    private AnimatorController[] animCtrls;
    // [Tooltip("How many object do you want in each episode")][Range(1,5)]
    // public int numObjects = 3;
    [Tooltip("Player ID list. Default is 1 to 10")]
    public int[] playerIDs = Enumerable.Range(1,10).ToArray();
    public PlayerHeightOffset playerInfoCenter;
    private Dictionary<int,float> playerID2Height = new Dictionary<int, float>();

    [Header("Environment Setup")]
    // Trainign setup
    public EnvSetup envSetup;
    public AgentController agentController;
    [Tooltip("Room root transform. It should includes different room setups as its child transforms")]
    public Transform roomRootT;
    private bool isTraining;

    [Header("Evaluation Setting")]
    [Tooltip("If use new gesture recordings for training")]
    public bool newGestures = false;
    [Tooltip("If use new Objects for training")]
    public bool newObjects = false;
    [Tooltip("If use new Rooms for training")]
    public bool newRooms = false;

    // Devie and target setup
    private string[] deviceList; // A list of device used (Kinect, Leap left&right)
    private string[] objCatList; // object catgory list
    private NavObj.ObjCategory objCategory;
    [HideInInspector] public NavObj.ObjType objType;
    [HideInInspector] public int objInstanceNum;

    // List to save all object prefabs in our directory
    private List<NavObj> objList = new List<NavObj>();
    // List to save the number of environment objects with 3 different categories
    private int[] objNums = new int[3];
    // List to save all selected navigable objects
    private NavObj[] selectedNavObjs;
    private AnimationClip[] selectedClip; // played animationclip
    private AnimationClip[] selectedRefClip; // played referencing clip
    private Transform[] _objList;
    // Object location indices for target and environment objects
    private int[] objLocIdx;

    // List to save all animation clips
    private List<AnimationClip> animList = new List<AnimationClip>();

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

        // deviceList = new string[]{"Kinect", "LeapLeft", "LeapRight"};
        // animCtrls = new AnimatorController[3]{replayController.kinectController,replayController.leapLeftController,replayController.leapRightController};
        deviceList = new string[]{"Kinect", "LeapRight"}; // only replay on right hand
        selectedClip = new AnimationClip[deviceList.Length];
        animCtrls = new AnimatorController[2]{replayController.kinectController, replayController.leapRightController}; // only replay on right hand
         
        objCatList = Enum.GetNames(typeof(NavObj.ObjCategory)).ToArray();

        // Load all prefabs at specified path
        recordingAbsPath = Application.dataPath + objSavePath + (!isTraining&&newObjects? "Test/":"Train/");
        animAbsPath = Application.dataPath + animSavePath + (!isTraining&&newGestures? "Test/":"Train/");
        stopAnimAbsPath = Application.dataPath + animSavePath + "stop_gestures/";
        LoadNavObjPrefabs(objList, recordingAbsPath);
        LoadAnimationClips(animList, animAbsPath); LoadAnimationClips(animList, stopAnimAbsPath);
    }

    void Start()
    {       
        foreach(int id in playerIDs)
        {
            playerID2Height.Add(id, playerInfoCenter.playerHeightList.Where(player => int.Parse(player.name)==id).ToArray()[0].height);
        }
    }

    // Setup replay for device using the replay controller, and return the instantiated gameobjects
    public Transform[] SetupTarget(NavObj.ObjCategory m_objCat, int m_objLocIdx)
    {        
        // Instantiate all objects in the list
        return InitObj(SelectNavObj(m_objCat), m_objCat, m_objLocIdx);        
    }

    public void SetupReplay(NavObj.ObjCategory m_objCat, int m_objLocIdx)
    {
        // Play selected animation clips
        int playerID = playerIDs[!isTraining&&newGestures? Random.Range(6,10):Random.Range(0,6)]; // select a random player ID
        // Scale Kinect avatar according to playerID
        float scale = playerID2Height[playerID]/1.75f;
        if(envSetup.autoSetTarget||agentController.CompletedEpisodes==0) GameObject.Find("KinectAvatar").transform.localScale = new Vector3(scale, scale, scale);

        for(int i=0;i<deviceList.Length;i++)
        {
            if(envSetup.autoSetTarget||agentController.CompletedEpisodes==0)
            {
                AnimationClip m_Clip = SelectAnimClip(deviceList[i], playerID, m_objCat, m_objLocIdx);
                selectedClip[i] = m_Clip;
                replayController.PlayAnimClipInCtrl(animCtrls[i],m_Clip);
            }
            else
            { 
                replayController.PlayAnimClipInCtrl(animCtrls[i],selectedClip[i]);
            }
        }
    }

    // Resume referencing gesture replay
    public void ResumeReplay()
    {
        for(int i=0;i<deviceList.Length;i++)
        {
            replayController.PlayAnimClipInCtrl(animCtrls[i],selectedClip[i]);
        }
    }

    public void SetupStopReplay()
    {     
        for(int i=0;i<deviceList.Length;i++)
        {
            replayController.PlayAnimClipInCtrl(animCtrls[i],SelectStopAnimClip(deviceList[i]));
        }
    }

    // Select room for current episdoe
    public void SetupRoom()
    {
        // int roomNum = roomRootT.childCount;
        int roomNumSelected = (!isTraining)&&newRooms? Random.Range(7,11):Random.Range(0,7);
        for(int i=0;i<11;i++)
        {
            roomRootT.GetChild(i).gameObject.SetActive(i==roomNumSelected? true:false);
        }
    }

    // Load all object prefabs from an absolute path
    public void LoadNavObjPrefabs(List<NavObj> m_objList, string path)
    {
        int currObjInstanceNum = 0; // The instance number for current loaded object
        foreach(string objCat in Enum.GetNames(typeof(NavObj.ObjCategory)))
        {
            foreach(string filePath in Directory.GetFiles(path+objCat+"/","*.prefab"))
            {
                string shortPath = filePath.Replace(Application.dataPath, "Assets");
                Object objLoaded = AssetDatabase.LoadAssetAtPath(shortPath, typeof(GameObject));
                // Add the loaded object prefab to the list
                NavObj navObj = new NavObj(objLoaded, Enum.GetNames(typeof(NavObj.ObjCategory)).ToList().IndexOf(objCat));
                navObj.objInstanceNum = currObjInstanceNum++;
                m_objList.Add(navObj);
            }
        }       

        // Debug.Log($"The number of object instances is {currObjInstanceNum}");
    }

    // Load all animation clips from an absolute path
    private void LoadAnimationClips(List<AnimationClip> m_animList, string path)
    {
        foreach(string filePath in Directory.GetFiles(path,"*.anim",SearchOption.AllDirectories))
        {
            string shortPath = filePath.Replace(Application.dataPath, "Assets");
            AnimationClip animLoaded = AssetDatabase.LoadAssetAtPath(shortPath, typeof(AnimationClip)) as AnimationClip;
            animList.Add(animLoaded);
        }
    }

    // Select an objet prefab with provided object category. Return a list of objects with the first one as the target object
    // private Object[] SelectNavObj(NavObj.ObjCategory m_objCat, int numObj)
    // {
    //     if(envSetup.autoSetTarget||agentController.CompletedEpisodes==0)
    //     {
    //         // Select NavObj with category m_objCat
    //         NavObj[] _list = objList.Where(obj => obj.objCat==m_objCat).ToArray();

    //         // Randomly select one object type
    //         objType = _list[Random.Range(0,_list.Length)].objType;
    //         NavObj[] qualifiedList = _list.Where(obj => obj.objType==objType).ToArray();

    //         NavObj[] selectedList = new NavObj[numObj];
    //         for(int i=0;i<numObj;i++)
    //         { 
    //             selectedList[i] = qualifiedList[Random.Range(0,qualifiedList.Length)];
    //         }
    //         _selectedNavObjs = selectedList.Select(obj => obj.objInstance).ToArray();
    //     }
    //     return _selectedNavObjs;
    // }

    // Select an objet prefab with provided object category. Return a list of objects which will be randomly spawned in the scene. There can be at most 3 objects with OnFloor category,
    // including the target object.
    private NavObj[] SelectNavObj(NavObj.ObjCategory m_objCat)
    {
        // Only call this random selection function when autosettarget is true
        if(envSetup.autoSetTarget||agentController.CompletedEpisodes==0)
        {
            // Randomly select 1-2 OnFloor Objects, 1-9 OnFurnitureTop Objects, 1-9 OnWall Objects (except for the target objects)
            // int OFObjNum = Random.Range(1,2);
            // int OFTObjNum = Random.Range(1,9);
            // int OWObjNum = Random.Range(1,9);
            objNums[0] = Random.Range(1,2);
            objNums[1] = Random.Range(1,9);
            objNums[2] = Random.Range(1,9);

            // Create new object array which with be returned and contains all the objects
            selectedNavObjs = new NavObj[objNums.Sum()+1];

            // Generate arrays with different categories
            NavObj[] OF_list = objList.Where(obj => obj.objCat==NavObj.ObjCategory.OnFloor).ToArray();
            NavObj[] OFT_list = objList.Where(obj => obj.objCat==NavObj.ObjCategory.OnFurnitureTop).ToArray();
            NavObj[] OW_list = objList.Where(obj => obj.objCat==NavObj.ObjCategory.OnWall).ToArray();

            // Select the target objects
            switch(m_objCat)
            {
                case NavObj.ObjCategory.OnFloor:
                    selectedNavObjs[0] = OF_list[Random.Range(0,OF_list.Length)];
                    break;
                case NavObj.ObjCategory.OnFurnitureTop:
                    selectedNavObjs[0] = OFT_list[Random.Range(0,OFT_list.Length)];
                    break;
                case NavObj.ObjCategory.OnWall:
                    selectedNavObjs[0] = OW_list[Random.Range(0,OW_list.Length)];
                    break;
            }

            // Set values for global object type and instance number variables
            objType = selectedNavObjs[0].objType;
            objInstanceNum = selectedNavObjs[0].objInstanceNum;

            for(int i=1;i<objNums.Sum()+1;i++)
            {
                if(i<objNums[0]+1) // Select OnFloor Objects
                {
                    selectedNavObjs[i] = OF_list[Random.Range(0,OF_list.Length)];
                }
                else if(i<objNums[0]+objNums[1]+1)
                {
                    selectedNavObjs[i] = OFT_list[Random.Range(0,OFT_list.Length)];
                }
                else
                {
                    selectedNavObjs[i] = OW_list[Random.Range(0,OW_list.Length)];
                }
            }
            // return selectedNavObjs;
        }
        return selectedNavObjs;
    }

    // Select Animation Clips with provided object category and location
    private AnimationClip SelectAnimClip(string m_device, int playerID, NavObj.ObjCategory m_objCat, int m_objLocIdx)
    {
        AnimationClip[] qualifiedList = animList.Where(anim => anim.name.Split('_')[0]==m_device&&
                                        anim.name.Split('_')[1]==playerID.ToString()&&
                                        anim.name.Split('_')[2]==m_objCat.ToString()&&
                                        anim.name.Split('_')[3]==m_objLocIdx.ToString()).ToArray();
        AnimationClip selectedClip = qualifiedList[Random.Range(0,qualifiedList.Length)];
        return selectedClip;
    }

    // Play animation clips with stop signs
    private AnimationClip SelectStopAnimClip(string m_device)
    {
        AnimationClip[] qualifiedList = animList.Where(anim => anim.name.Split('_')[1].StartsWith("stop")&&
                                        anim.name.Split('_')[0]==m_device).ToArray();
        AnimationClip selectedClip = qualifiedList[Random.Range(0,qualifiedList.Length)];
        return selectedClip;
    }

    // Initialize selected objects in the given location. Return a list of instantiated Gameobjects
    private Transform[] InitObj(NavObj[] m_objList, NavObj.ObjCategory m_ObjCat, int m_objLocIdx)
    {
        // Instantiate other objects in the object list if needed
        if(m_objList.Length!=1&&(envSetup.autoSetTarget||agentController.CompletedEpisodes==0))
        {
            // Create a new transform array
            _objList = new Transform[m_objList.Length];
            // Create a new object location indices
            objLocIdx = new int[_objList.Length];

            // Instantiate the target object
            _objList[0] = InstantiateObj(m_objList[0].objInstance, m_ObjCat, m_objLocIdx).transform;
            objLocIdx[0] = m_objLocIdx;
            // _fakeTargetsLocIdx = Enumerable.Range(0,10).Where(num => num!=m_objLocIdx).OrderBy(num => Guid.NewGuid()).Take(m_objList.Length-1).ToArray();

            // Debug.Log(objLocIdx.Length);

            // Environment object location indices
            switch(m_ObjCat)
            {
                case NavObj.ObjCategory.OnFloor: 
                    Array.Copy(Enumerable.Range(0,10).Where(num => num!=objLocIdx[0]).OrderBy(num => Guid.NewGuid()).Take(objNums[0]).ToArray(),0,objLocIdx,1,objNums[0]);
                    Array.Copy(Enumerable.Range(0,10).OrderBy(num => Guid.NewGuid()).Take(objNums[1]).ToArray(),0,objLocIdx,1+objNums[0],objNums[1]);
                    Array.Copy(Enumerable.Range(0,10).OrderBy(num => Guid.NewGuid()).Take(objNums[2]).ToArray(),0,objLocIdx,1+objNums[0]+objNums[1],objNums[2]);
                    break;
                case NavObj.ObjCategory.OnFurnitureTop:
                    Array.Copy(Enumerable.Range(0,10).OrderBy(num => Guid.NewGuid()).Take(objNums[0]).ToArray(),0,objLocIdx,1,objNums[0]);
                    Array.Copy(Enumerable.Range(0,10).Where(num => num!=objLocIdx[0]).OrderBy(num => Guid.NewGuid()).Take(objNums[1]).ToArray(),0,objLocIdx,1+objNums[0],objNums[1]);
                    Array.Copy(Enumerable.Range(0,10).OrderBy(num => Guid.NewGuid()).Take(objNums[2]).ToArray(),0,objLocIdx,1+objNums[0]+objNums[1],objNums[2]);
                    break;
                case NavObj.ObjCategory.OnWall:
                    Array.Copy(Enumerable.Range(0,10).OrderBy(num => Guid.NewGuid()).Take(objNums[0]).ToArray(),0,objLocIdx,1,objNums[0]);
                    Array.Copy(Enumerable.Range(0,10).OrderBy(num => Guid.NewGuid()).Take(objNums[1]).ToArray(),0,objLocIdx,1+objNums[0],objNums[1]);
                    Array.Copy(Enumerable.Range(0,10).Where(num => num!=objLocIdx[0]).OrderBy(num => Guid.NewGuid()).Take(objNums[2]).ToArray(),0,objLocIdx,1+objNums[0]+objNums[1],objNums[2]);
                    break; 
            }

            for(int i=1;i<objNums.Sum()+1;i++)
            {
                _objList[i] = InstantiateObj(m_objList[i].objInstance, m_objList[i].objCat, objLocIdx[i]).transform;
            }
        }
        // for(int i=1;i<_objList.Length;i++)
        // {
        // _objList[i] = InstantiateObj(m_objList[i], m_ObjCat, _fakeTargetsLocIdx[i-1]).transform;
        // }

        return _objList;
    }

    // Instantiate a gameject at given location
    private GameObject InstantiateObj(Object objOri, NavObj.ObjCategory m_objCat, int m_objLocIdx)
    {
        Transform locReceptable = GameObject.Find($"PosReceptacles/{m_objCat.ToString()}").transform.GetChild(m_objLocIdx);
        GameObject objInst = GameObject.Instantiate(objOri,locReceptable) as GameObject;
        objInst.transform.localPosition = Vector3.zero; // Reset object location at the center
        
        // Change orientations of objects with category on wall
        if(m_objCat==NavObj.ObjCategory.OnWall)
        {
            if(locReceptable.position.x==4f) objInst.transform.localRotation = Quaternion.Euler(0,-90f,0);
            if(locReceptable.position.x==-4f) objInst.transform.localRotation = Quaternion.Euler(0,90f,0);
            if(locReceptable.position.z==2.5f) objInst.transform.localRotation = Quaternion.Euler(0,180f,0);
            if(locReceptable.position.z==-2.5f) objInst.transform.localRotation = Quaternion.Euler(0,0,0);
        }
        else
        {
            // objInst.transform.localRotation = ((GameObject)objOri).transform.localRotation;
            objInst.transform.localRotation = Quaternion.Euler(0,Random.Range(-180f,180f),0);
        }
        return objInst;
    }
}
