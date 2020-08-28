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
    // Path to read recordings.
    [Tooltip("The save path for object prefabs relative to the Assets folder")]
    public string objSavePath = "/Resources/NavObj/";
    [Tooltip("The save path for gesture animation clips relative to the Assets folder")]
    public string animSavePath = "/Resources/Recordings/";
    private string recordingAbsPath;
    private string animAbsPath;
    [Tooltip("Script that controlls the replay of animation clips")]
    public RepGestureAnim replayController;
    [Tooltip("How many object do you want in each episode")][Range(2,5)]
    public int numObjects = 3;
    [Tooltip("Player ID list. Default is 1 to 10")]
    public int[] playerIDs = Enumerable.Range(1,10).ToArray();

    // Trainign setup
    public EnvSetup envSetup;
    private bool isTraining;

    // Devie and target setup
    private string[] deviceList; // A list of device used (Kinect, Leap left&right)
    private string[] objCatList; // pbject catgory list
    private NavObj.ObjCategory objCategory;
    private int objLocationIdx;
    [HideInInspector] public NavObj.ObjType objType;

    // List to save all object prefabs
    private List<NavObj> objList = new List<NavObj>();

    // Lists to save all animation clips
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

        deviceList = new string[]{"Kinect", "LeapLeft", "LeapRight"};
        objCatList = Enum.GetNames(typeof(NavObj.ObjCategory)).ToArray();

        // Load all prefabs at specified path
        recordingAbsPath = Application.dataPath + objSavePath;
        animAbsPath = Application.dataPath + animSavePath;
        LoadNavObjPrefabs(objList, recordingAbsPath);
        LoadAnimationClips(animList, animAbsPath);
    }

    void Start()
    {       

    }

    // Setup replay for device using the replay controller, and return the instantiated gameobjects
    public Transform[] SetupReplay(NavObj.ObjCategory m_objCat, int m_objLocIdx)
    {
        objCategory = envSetup.objCatSelected;
        objLocationIdx = envSetup.objLocationIdxSelected;
        
        List<NavObj> selectedList = objList.Where(navObj => navObj.objCat==m_objCat).ToList();

        int listLength = selectedList.Count;
        if(listLength == 0)
        {
            Debug.LogError($"There is no recording for object-{m_objCat.ToString()}");
            return null;
        }

        // Play selected animation clips
        AnimatorController[] animCtrls = new AnimatorController[3]{replayController.kinectController,replayController.leapLeftController,replayController.leapRightController}; 
        int playerID = playerIDs[Random.Range(0,playerIDs.Length)]; // select a random player ID
        for(int i=0;i<deviceList.Length;i++)
        {
            replayController.PlayAnimClipInCtrl(animCtrls[i],SelectAnimClip(deviceList[i], playerID, m_objCat, m_objLocIdx));
        }

        // Instantiate all objects in the list
        return InitObj(SelectNavObj(objCategory,numObjects), m_objCat, m_objLocIdx);        
    }

    // Load all object prefabs from an absolute path
    private void LoadNavObjPrefabs(List<NavObj> m_objList, string path)
    {
        foreach(string objCat in Enum.GetNames(typeof(NavObj.ObjCategory)))
        {
            foreach(string filePath in Directory.GetFiles(path+objCat+"/","*.prefab"))
            {
                string shortPath = filePath.Replace(Application.dataPath, "Assets");
                Object objLoaded = AssetDatabase.LoadAssetAtPath(shortPath, typeof(GameObject));
                // Add the loaded object prefab to the list
                NavObj navObj = new NavObj(objLoaded, Enum.GetNames(typeof(NavObj.ObjCategory)).ToList().IndexOf(objCat));
                m_objList.Add(navObj);
            }
        }
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

    // Select an objet prefab with provided object category. Also select other 2 objects with the same category. Return a List
    // a list of objects with the first one as the target object
    private Object[] SelectNavObj(NavObj.ObjCategory m_objCat, int numObj)
    {
        NavObj[] qualifiedList = objList.Where(obj => obj.objCat==m_objCat).ToArray();
        objType = qualifiedList[0].objType;

        Object[] selectedList = new Object[numObj];
        for(int i=0;i<numObj;i++)
        {
            selectedList[i] = qualifiedList[Random.Range(0,qualifiedList.Length)].objInstance;
        }
        return selectedList;
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

    // Initialize selected objects in the given location. Return a list of instantiated Gameobjects
    private Transform[] InitObj(Object[] m_objList, NavObj.ObjCategory m_ObjCat, int m_objLocIdx)
    {
        Transform[] _objList = new Transform[m_objList.Length];

        // Instantiate the target object
        _objList[0] = InstantiateObj(m_objList[0], m_ObjCat, m_objLocIdx).transform;

        // Instantiate other objects in the object list
        int[] newIdxList = Enumerable.Range(0,10).Where(num => num!=m_objLocIdx).OrderBy(num => Guid.NewGuid()).Take(m_objList.Length-1).ToArray();
        for(int i=1;i<_objList.Length;i++)
        {
           _objList[i] = InstantiateObj(m_objList[i], m_ObjCat, newIdxList[i-1]).transform;
        }

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
            objInst.transform.localRotation = ((GameObject)objOri).transform.localRotation;
        }
        return objInst;
    }
}
