using System.Collections.Generic;
using System.IO;
using System;
using System.Text;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Barracuda;

// Display training information, including object selected and animationclip played
public class AnimationClip2CSV : EditorWindow
{
    private List<AnimationClip> animList = new List<AnimationClip>();

    float maxPos = 0f;
    float maxRot = 0f;
    float minScale = 1f;

    private Vector3 m_position;
    private NNModel kinectModel;
    private NNModel leapModel;
    private AnimationClip exampleKinectClip;
    private AnimationClip exampleLeapClip;

    [MenuItem("Window/AnimationClip2CSV")]
    static void Init()
    {
        GetWindow(typeof(AnimationClip2CSV));
    }

    public void OnGUI()
    {
        m_position = EditorGUILayout.Vector3Field("Position the agent should point to:", m_position);
        kinectModel = EditorGUILayout.ObjectField("Kinect NN model:", kinectModel, typeof(NNModel), false) as NNModel;
        leapModel = EditorGUILayout.ObjectField("Leap NN model:", leapModel, typeof(NNModel), false) as NNModel;
        exampleKinectClip = EditorGUILayout.ObjectField("Example Kinect Animation Clip", exampleKinectClip, typeof(AnimationClip), false) as AnimationClip;
        exampleLeapClip = EditorGUILayout.ObjectField("Example Leap Animation Clip", exampleLeapClip, typeof(AnimationClip), false) as AnimationClip;

        if(GUILayout.Button("Load Animation Clips"))
        {
            string filePath = Application.dataPath + "/Resources/Recordings/";
            animList.Clear();
            LoadAnimationClips(animList, filePath);
            Debug.Log(animList.Count);
        }

        if(GUILayout.Button("Save Clips Data to CSV Files"))
        {
            // Retrieve current time in MM-dd-yyyy_HH:mm:ss
            DateTime now = DateTime.Now;

            string path = Application.dataPath + "/Resources/clips2csv_" + now.ToString("MM-dd-yyyy_HH-mm-ss");
            TextWriter sw_Kinect = new StreamWriter(path + "_Kinect.csv");
            TextWriter sw_Leap = new StreamWriter(path + "_Leap.csv");
            StringBuilder line_Kinect = new StringBuilder(); // each line in the csv file
            StringBuilder line_Leap = new StringBuilder();
            foreach(AnimationClip clip in animList)
            {
                line_Kinect.Clear();
                line_Leap.Clear();

                if(clip != null)
                {
                    string device = clip.name.Split('_')[0];
                    if(device == "Kinect") RecordAnimationClip2CSV(clip, line_Kinect, sw_Kinect);
                    else if(device == "LeapRight") RecordAnimationClip2CSV(clip, line_Leap, sw_Leap);
                }
            }
            sw_Kinect.Close();
            sw_Leap.Close();
            // Debug.Log(maxPos);
            // Debug.Log(maxRot);
            // Debug.Log(minScale);
        }

        if(GUILayout.Button("Recorded transform path and property name"))
        {
            // Retrieve current time in MM-dd-yyyy_HH:mm:ss
            DateTime now = DateTime.Now;
            string path = Application.dataPath + "/Resources/propertynames_" + now.ToString("MM-dd-yyyy_HH-mm-ss") + ".csv";
            TextWriter sw = new StreamWriter(path);

            var bindings = AnimationUtility.GetCurveBindings(exampleKinectClip);
            foreach(var binding in bindings)
            {
                sw.Write(binding.path + "/" + binding.propertyName + ",");
            }
            sw.Close();
        }

        if(GUILayout.Button("Inference Kinect Body from Position"))
        {
            var model = ModelLoader.Load(kinectModel, false);
            var worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, model);

            // Pass the input to the model
            Tensor input = new Tensor(1,3, new float[3]{m_position.x,m_position.y,m_position.z}); // create a tensor 
            // Debug.Log(input[0]);
            var output = worker.Execute(input).PeekOutput();

            // From the example Kinect animation clip, find all transforms that the output should match with
            // and assign each output to either position, rotation, or scale of the transform
            var bindings = AnimationUtility.GetCurveBindings(exampleKinectClip);
            Transform currT = null; // Current transform for this binding
            int i = 0;
            while(i<bindings.Length)
            {
                currT = GameObject.Find($"KinectAvatar/{bindings[i].path}").transform;
                switch(bindings[i].propertyName)
                {
                    case "m_LocalPosition.x": 
                        if(bindings[i].path=="" || bindings[i].path=="Reference/Hips")
                            currT.localPosition = new Vector3(2*output[i],currT.localPosition.y,currT.localPosition.z); 
                        i++; break;
                    case "m_LocalPosition.y": 
                        if(bindings[i].path=="" || bindings[i].path=="Reference/Hips")
                            currT.localPosition = new Vector3(currT.localPosition.x,2*output[i],currT.localPosition.z); 
                        i++; break;
                    case "m_LocalPosition.z": 
                        if(bindings[i].path=="" || bindings[i].path=="Reference/Hips")
                            currT.localPosition = new Vector3(currT.localPosition.x,currT.localPosition.y,2*output[i]); 
                        i++; break;
                    case "m_LocalScale.x": 
                        if(bindings[i].path=="")
                            currT.localScale = new Vector3(output[i],currT.localScale.y,currT.localScale.z); 
                        i++; break;
                    case "m_LocalScale.y": 
                        if(bindings[i].path=="")
                            currT.localScale = new Vector3(currT.localScale.x,output[i],currT.localScale.z); 
                        i++; break;
                    case "m_LocalScale.z":
                        if(bindings[i].path=="") 
                            currT.localScale = new Vector3(currT.localScale.x,currT.localScale.y,output[i]); 
                        i++; break;
                    case "m_LocalRotation.x": 
                        // currT.localRotation = new Quaternion(output[i],currT.localRotation.y,currT.localRotation.z,currT.localRotation.w);
                        if(trackedJoints.Contains(bindings[i].path.Split('/').Last()))
                        {
                            Quaternion newquaternion = new Quaternion(output[i],output[i+1],output[i+2],output[i+3]);
                            currT.localRotation = newquaternion;
                        }
                        i = i+4;
                        break;
                    // case "m_LocalRotation.y": currT.localRotation = new Quaternion(currT.localRotation.x,output[i],currT.localRotation.z,currT.localRotation.w); break;
                    // case "m_LocalRotation.z": currT.localRotation = new Quaternion(currT.localRotation.x,currT.localRotation.y,output[i],currT.localRotation.w); break;
                    // case "m_LocalRotation.w": currT.localRotation = new Quaternion(currT.localRotation.x,currT.localRotation.y,currT.localRotation.z,output[i]); break;
                }

                // Debug.Log($"{i+1}: "+output[i]);
                if(i<bindings.Length && bindings[i].path.Split('/').Last() == "Hips" && bindings[i].propertyName.StartsWith("m_LocalRotation"))
                {
                    Debug.Log(bindings[i].propertyName + ": " + output[i] + "," + output[i+1] + "," + output[i+2] + "," + output[i+3]);
                }
            }

            Debug.Log(GameObject.Find("Hips").transform.localRotation);
        }

        if(GUILayout.Button("Inference Leap Hand from Position"))
        {
            var model = ModelLoader.Load(leapModel, false);
            var worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, model);

            // Pass the input to the model
            Tensor input = new Tensor(1,3, new float[3]{m_position.x,m_position.y,m_position.z}); // create a tensor 
            // Debug.Log(input[0]);
            var output = worker.Execute(input).PeekOutput();

            // From the example Leap animation clip, find all transforms that the output should match with
            // and assign each output to either position, rotation, or scale of the transform
            var bindings = AnimationUtility.GetCurveBindings(exampleLeapClip);
            Transform currT = null; // Current transform for this binding
            int i = 0;
            while(i<bindings.Length)
            {
                currT = GameObject.Find($"RiggedHandRight/{bindings[i].path}").transform;
                switch(bindings[i].propertyName)
                {
                    case "m_LocalPosition.x":
                        // Vector3 newvector3 = new Vector3(2*output[i],2*output[i+1],2*output[i+2]);
                        // currT.localPosition = newvector3;
                        i = i+3;
                        break;
                    case "m_LocalRotation.x": 
                        // if(i<bindings.Length && bindings[i].path == "R_Wrist/R_Palm" && bindings[i].propertyName.StartsWith("m_LocalRotation"))
                        // {
                        //     Debug.Log(i);
                        //     Debug.Log(bindings[i].path + bindings[i].propertyName + ": " + output[i] + "," + output[i+1] + "," + output[i+2] + "," + output[i+3]);
                        // }
                        Quaternion newquarternion = new Quaternion(output[i],output[i+1],output[i+2],output[i+3]);
                        currT.localRotation = newquarternion;
                        // Debug.Log(GameObject.Find("R_Palm").transform.localRotation);
                        i = i+4;
                        break;
                    case "m_LocalScale.x":
                        i = i+3;
                        break;
                }
            }

            // Debug.Log(i);
            // Debug.Log(GameObject.Find("R_Palm").transform.localRotation);
        }
    }

    private void RecordAnimationClip2CSV(AnimationClip clip, StringBuilder line, TextWriter sw)
    {
        string objCat = clip.name.Split('_')[2];
        int objLocIdx = int.Parse(clip.name.Split('_')[3]);
        // get the receptacle location according to the clip name
        Transform locReceptable = GameObject.Find($"PosReceptacles/{objCat.ToString()}").transform.GetChild(objLocIdx);

        // Input for the model
        // Position of the object is the input for the model
        line.Append($"{locReceptable.position.x},{locReceptable.position.y},{locReceptable.position.z},");

        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            // line.Append(binding.propertyName);
            switch(binding.propertyName.Split('.')[0])
            {
                case "m_LocalPosition":
                    // if(Mathf.Abs(curve.keys[0].value)>maxPos) maxPos = curve.keys[0].value;
                    // if(binding.path == "" || binding.path.Contains("Hips"))
                    // {
                        line.Append(curve.keys[0].value/2f);
                        line.Append(",");
                    // }
                    break;
                case "m_LocalRotation":
                    // if(Mathf.Abs(curve.keys[0].value)>maxRot) maxRot = curve.keys[0].value;
                    // if(trackedJoints.Contains(binding.path.Split('/').Last()))
                    // {
                        line.Append(curve.keys[0].value);
                        line.Append(",");
                    // }
                    break;
                case "m_LocalScale":
                    // if(Mathf.Abs(curve.keys[0].value)<minScale) minScale = curve.keys[0].value;
                    // if(binding.path == "")
                    // {
                        line.Append(curve.keys[0].value);
                        line.Append(",");
                    // }
                    break;
            }
        }

        sw.WriteLine(line.ToString());
    }

    // Load all animation clips from an absolute path
    private void LoadAnimationClips(List<AnimationClip> m_animList, string path)
    {
        foreach(string filePath in Directory.GetFiles(path,"*.anim",SearchOption.AllDirectories))
        {
            string shortPath = filePath.Replace(Application.dataPath, "Assets");
            AnimationClip animLoaded = AssetDatabase.LoadAssetAtPath(shortPath, typeof(AnimationClip)) as AnimationClip;
            m_animList.Add(animLoaded);
        }

    }

    public static string[] trackedJoints = new string[]{
        "Hips",
        "LeftUpLeg","RightUpLeg",
        "LeftLeg","RightLeg",
        "LeftFoot","RightFoot",
        "Spine",
        "LeftShoulder", "RightShoulder",
        "LeftArm", "RightArm",
        "LeftForeArm", "RightForeArm",
        "LeftHand", "RightHand",
        "LeftHandIndex1", "RightHandIndex1",
        "LeftHandIndex2", "RightHandIndex2",
        "LeftHandIndex3", "RightHandIndex3",
        "LeftHandMiddle1", "RightHandMiddle1",
        "LeftHandMiddle2", "RightHandMiddle2",
        "LeftHandMiddle3", "RightHandMiddle3",
        "LeftHandPinky1", "RightHandPinky1",
        "LeftHandPinky2", "RightHandPinky2",
        "LeftHandPinky3", "RightHandPinky3",
        "LeftHandRing1", "RightHandRing1",
        "LeftHandRing2", "RightHandRing2",
        "LeftHandRing3", "RightHandRing3",
        "LeftHandThumb1", "RightHandThumb1",
        "LeftHandThumb2", "RightHandThumb2",
        "LeftHandThumb3", "RightHandThumb3",
        "Neck"
    };
}
