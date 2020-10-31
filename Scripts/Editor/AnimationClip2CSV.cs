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
                    if(device == "Kinect") RecordAnimationClip2CSV(clip, line_Kinect, sw_Kinect, true);
                    else if(device == "LeapRight") RecordAnimationClip2CSV(clip, line_Leap, sw_Leap, false);
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
            string path = Application.dataPath + "/Resources/propertynames_" + now.ToString("MM-dd-yyyy_HH-mm-ss");
            TextWriter sw_Kinect = new StreamWriter(path + "_Kinect.csv");
            TextWriter sw_Leap = new StreamWriter(path + "_Leap.csv");

            RecordPathPropertyNames(exampleKinectClip, sw_Kinect, true);
            RecordPathPropertyNames(exampleLeapClip, sw_Leap, false);
            
            sw_Kinect.Close();
            sw_Leap.Close();
        }

        if(GUILayout.Button("Inference Kinect Body from Position"))
        {
            var model = ModelLoader.Load(kinectModel, false);
            var worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, model);

            // Pass the input to the model
            Tensor input = new Tensor(1,10, new float[10]{m_position.x,m_position.y,m_position.z, 0f, 0f, 1.25f, 0f, 0.5f, 0f, 1.8f}); // create a tensor 
            // Debug.Log(input[0]);
            var output = worker.Execute(input).PeekOutput();

            // From the example Kinect animation clip, find all transforms that the output should match with
            // and assign each output to either position, rotation, or scale of the transform
            InferenceFromModel(true, output);
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

    private void RecordPathPropertyNames(AnimationClip clip, TextWriter sw, bool isKinect)
    {
        var bindings = AnimationUtility.GetCurveBindings(clip);
        int numBindings = bindings.Length;
        int i = 0;
        Debug.Log(bindings[0].propertyName);
        while(i < numBindings)
        {
            if(isKinect)
            {
                switch(bindings[i].propertyName.Split('.')[0])
                {
                    case "m_LocalPosition":
                        if(bindings[i].path == "Reference/Hips") // Only add position and location for center and hip
                            sw.Write(bindings[i].path + "/" + bindings[i].propertyName + ",");
                        i += 1;
                        break;
                    case "m_LocalRotation":
                        if(trackedKinectJoints.Contains(bindings[i].path.Split('/').Last()))
                        {
                            sw.Write(bindings[i].path + "/" + bindings[i].propertyName.Split('.')[0] + ".x" + ",");
                            sw.Write(bindings[i].path + "/" + bindings[i].propertyName.Split('.')[0] + ".y" + ",");
                            sw.Write(bindings[i].path + "/" + bindings[i].propertyName.Split('.')[0] + ".z" + ",");
                        }
                        i += 4;
                        break;
                    case "m_LocalScale":
                        i += 3;
                        break;
                }
            }
            else
            {
                switch(bindings[i].propertyName.Split('.')[0])
                {
                    case "m_LocalPosition":
                        if(!ignoredLeapJoints.Contains(bindings[i].path.Split('/').Last())) // Only add position and location for center and hip
                            sw.Write(bindings[i].path + "/" + bindings[i].propertyName + ",");
                        i += 1;
                        break;
                    case "m_LocalRotation":
                        if(!ignoredLeapJoints.Contains(bindings[i].path.Split('/').Last()) && !ignoredLeapRotations.Contains(bindings[i].path.Split('/').Last()))
                        {
                            sw.Write(bindings[i].path + "/" + bindings[i].propertyName.Split('.')[0] + ".x" + ",");
                            sw.Write(bindings[i].path + "/" + bindings[i].propertyName.Split('.')[0] + ".y" + ",");
                            sw.Write(bindings[i].path + "/" + bindings[i].propertyName.Split('.')[0] + ".z" + ",");
                        }
                        i += 4;
                        break;
                    case "m_LocalScale":
                        i += 3;
                        break;
                }
            }
        }
    }

    private void RecordAnimationClip2CSV(AnimationClip clip, StringBuilder line, TextWriter sw, bool isKinect)
    {
        string playerID = clip.name.Split('_')[1];
        string objCat = clip.name.Split('_')[2];
        int objLocIdx = int.Parse(clip.name.Split('_')[3]);
        // get the receptacle location according to the clip name
        Transform locReceptable = GameObject.Find($"PosReceptacles/{objCat.ToString()}").transform.GetChild(objLocIdx);

        PlayerHeightOffset playerHeights = GameObject.Find("Human Interaction Modules").GetComponent<PlayerHeightOffset>();

        // Input for the model
        line.Append($"{locReceptable.position.x},{locReceptable.position.y},{locReceptable.position.z},"); // Location of the target
        line.Append("0,0,1.25,"); // Initial location of the human emulator
        line.Append("0,0.5,0,"); // Initial rotation of the human emulator
        line.Append(playerHeights.GetPlayerHeight(playerID) + ",");

        // Bindings for current animation clip
        var bindings = AnimationUtility.GetCurveBindings(clip);
        int numBindings = bindings.Length;

        int i = 0;
        while(i<numBindings)
        {
            // line.Append(binding.propertyName);
            if(isKinect)
            {
                switch(bindings[i].propertyName.Split('.')[0])
                {
                    case "m_LocalPosition":
                        // if(Mathf.Abs(curve.keys[0].value)>maxPos) maxPos = curve.keys[0].value;
                        if(bindings[i].path == "Reference/Hips") // Only add position and location for center and hip
                        {
                            line.Append(AnimationUtility.GetEditorCurve(clip, bindings[i]).keys[0].value); line.Append(",");
                            line.Append(AnimationUtility.GetEditorCurve(clip, bindings[i+1]).keys[0].value); line.Append(",");
                            line.Append(AnimationUtility.GetEditorCurve(clip, bindings[i+2]).keys[0].value); line.Append(",");
                        }
                        i += 3;
                        break;
                    case "m_LocalRotation": // All rotations are scaled by 360 to fit in the range of (-1,1)
                        // if(Mathf.Abs(curve.keys[0].value)>maxRot) maxRot = curve.keys[0].value;
                        if(trackedKinectJoints.Contains(bindings[i].path.Split('/').Last()))
                        {
                            Quaternion currQ = new Quaternion(AnimationUtility.GetEditorCurve(clip, bindings[i]).keys[0].value, AnimationUtility.GetEditorCurve(clip, bindings[i+1]).keys[0].value, AnimationUtility.GetEditorCurve(clip, bindings[i+2]).keys[0].value, AnimationUtility.GetEditorCurve(clip, bindings[i+3]).keys[0].value);
                            Vector3 currAngle = currQ.eulerAngles;
                            line.Append(currAngle.x/360); line.Append(",");
                            line.Append(currAngle.y/360); line.Append(",");
                            line.Append(currAngle.z/360); line.Append(",");
                        }
                        i += 4;
                        break;
                    case "m_LocalScale":
                        // if(Mathf.Abs(curve.keys[0].value)<minScale) minScale = curve.keys[0].value;
                        // if(bindings[i].path == "") // Add the scale (i.e. Avatar height)
                        // {
                        //     line.Append(AnimationUtility.GetEditorCurve(clip, bindings[i]).keys[0].value);
                        // }
                        i += 3;
                        break;
                }
            }
            else
            {
                switch(bindings[i].propertyName.Split('.')[0])
                {
                    case "m_LocalPosition":
                        if(!ignoredLeapJoints.Contains(bindings[i].path.Split('/').Last())) // Those joints don't change with time
                        {
                            line.Append(AnimationUtility.GetEditorCurve(clip, bindings[i]).keys[0].value); line.Append(",");
                            line.Append(AnimationUtility.GetEditorCurve(clip, bindings[i+1]).keys[0].value); line.Append(",");
                            line.Append(AnimationUtility.GetEditorCurve(clip, bindings[i+2]).keys[0].value); line.Append(",");
                        }
                        i += 3;
                        break;
                    case "m_LocalRotation": // All rotations are scaled by 360 to fit in the range of (-1,1)
                        if(!ignoredLeapJoints.Contains(bindings[i].path.Split('/').Last()) && !ignoredLeapRotations.Contains(bindings[i].path.Split('/').Last()))
                        {
                            Quaternion currQ = new Quaternion(AnimationUtility.GetEditorCurve(clip, bindings[i]).keys[0].value, AnimationUtility.GetEditorCurve(clip, bindings[i+1]).keys[0].value, AnimationUtility.GetEditorCurve(clip, bindings[i+2]).keys[0].value, AnimationUtility.GetEditorCurve(clip, bindings[i+3]).keys[0].value);
                            Vector3 currAngle = currQ.eulerAngles;
                            line.Append(currAngle.x/360); line.Append(",");
                            line.Append(currAngle.y/360); line.Append(",");
                            line.Append(currAngle.z/360); line.Append(",");
                        }
                        i += 4;
                        break;
                    case "m_LocalScale":
                        // if(Mathf.Abs(curve.keys[0].value)<minScale) minScale = curve.keys[0].value;
                        // if(bindings[i].path == "") // Add the scale (i.e. Avatar height)
                        // {
                        //     line.Append(AnimationUtility.GetEditorCurve(clip, bindings[i]).keys[0].value);
                        // }
                        i += 3;
                        break;
                }
            }
        }

        sw.WriteLine(line.ToString());
    }

    private void InferenceFromModel(bool isKinect, Tensor output)
    {
        var bindings = AnimationUtility.GetCurveBindings(exampleKinectClip);
        Transform currT = null; // Current transform for this binding
        int i = 0;
        int j = 0; // output index
        while(i<bindings.Length)
        {
            currT = GameObject.Find($"KinectAvatar/{bindings[i].path}").transform;
            switch(bindings[i].propertyName)
            {
                case "m_LocalPosition.x": 
                    if(bindings[i].path == "Reference/Hips")
                    {
                        currT.localPosition = new Vector3(output[j],output[j+1],output[j+2]);
                        j += 3;
                    }
                    i += 3; break;
                case "m_LocalScale.x": 
                    i += 3; break;
                case "m_LocalRotation.x": 
                    // currT.localRotation = new Quaternion(output[i],currT.localRotation.y,currT.localRotation.z,currT.localRotation.w);
                    if(trackedKinectJoints.Contains(bindings[i].path.Split('/').Last()))
                    {
                        currT.eulerAngles = new Vector3(output[j]*360,output[j+1]*360,output[j+2]*360);
                        j += 3;
                    }
                    i = i+4;
                    break;
                // case "m_LocalRotation.y": currT.localRotation = new Quaternion(currT.localRotation.x,output[i],currT.localRotation.z,currT.localRotation.w); break;
                // case "m_LocalRotation.z": currT.localRotation = new Quaternion(currT.localRotation.x,currT.localRotation.y,output[i],currT.localRotation.w); break;
                // case "m_LocalRotation.w": currT.localRotation = new Quaternion(currT.localRotation.x,currT.localRotation.y,currT.localRotation.z,output[i]); break;
            }

            // Debug.Log($"{i+1}: "+output[i]);
            // if(i<bindings.Length && bindings[i].path.Split('/').Last() == "Hips" && bindings[i].propertyName.StartsWith("m_LocalRotation"))
            // {
            //     Debug.Log(bindings[i].propertyName + ": " + output[i] + "," + output[i+1] + "," + output[i+2] + "," + output[i+3]);
            // }
        }

        // Debug.Log(GameObject.Find("Hips").transform.localRotation);
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

    public static string[] trackedKinectJoints = new string[]{
        "Hips",
        "LeftUpLeg","RightUpLeg",
        "LeftLeg","RightLeg",
        "LeftFoot","RightFoot",
        "Spine",
        "LeftShoulder", "RightShoulder",
        "LeftArm", "RightArm",
        "LeftForeArm", "RightForeArm",
        "LeftHand", "RightHand",
        "Neck"
    };

    public static string[] ignoredLeapJoints = new string[]{
        "R_index_end",
        "R_middle_end",
        "R_pinky_end",
        "R_ring_end",
        "R_thumb_end"
    };

    public static string[] ignoredLeapRotations = new string[]{
        "R_index_meta",
        "R_middle_meta",
        "R_pinky_meta",
        "R_ring_meta"
    };
}
