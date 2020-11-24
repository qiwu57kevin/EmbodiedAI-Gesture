using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.Barracuda;
public class KinectAgent : Agent
{
    public Transform embodiedAgent;
    public AgentController agentController;
    public Transform teacherKinectT;
    public Transform teacherKinectHipT;
    public Transform studentKinectT;
    public Transform studentKinectHipT;

    public bool replayFromNNModel;
    public NNModel kinectModel;
    private Model model;
    private IWorker worker;
    private Tensor input;
    private Tensor output;

    private List<Transform> observedteacherKinectTs = new List<Transform>();
    private List<Transform> observedstudentKinectTs = new List<Transform>();
    
    void Start()
    {
        foreach(string joint in trackedJoints)
        {
            // observedteacherKinectTs.Add(GameObject.Find(joint).transform);
            observedteacherKinectTs.Add(TransformDeepChildExtension.FindDeepChild(teacherKinectT,joint));
            observedstudentKinectTs.Add(TransformDeepChildExtension.FindDeepChild(studentKinectT,joint));
        }

        // foreach(Transform T in observedstudentKinectTs)
        // {
        //     Debug.Log(T.parent.parent.name);
        // }

        model = ModelLoader.Load(kinectModel, false);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, model);
    }

    void Update()
    {
        if(replayFromNNModel)
        {
            studentKinectT.localScale = teacherKinectT.localScale;
            input = new Tensor(1,10, new float[10]{agentController.targetObj.position.x,agentController.targetObj.position.y,agentController.targetObj.position.z,teacherKinectT.localPosition.x,teacherKinectT.localPosition.y,teacherKinectT.localPosition.z,teacherKinectT.localRotation.eulerAngles.x/360f,teacherKinectT.localRotation.eulerAngles.y/360f,teacherKinectT.localRotation.eulerAngles.z/360f,teacherKinectT.localScale.x*1.8f});
            output = worker.Execute(input).PeekOutput();

            int i = 0;
            
            // Track rotations for each joint (positions will not be tracked since they won't change with time)
            foreach(Transform joint in observedstudentKinectTs)
            {
                joint.localRotation = new Quaternion(output[i++],output[i++],output[i++],output[i++]);
            }

            studentKinectHipT.localPosition = new Vector3(output[i++],output[i++],output[i++]);

            input.Dispose();
        }
        // in total 17*4+3=71
        // AddReward(0.002f);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // sensor.AddObservation(embodiedAgent.localPosition); // Agent position
        // sensor.AddObservation(embodiedAgent.localRotation); // Agent location
        sensor.AddObservation(agentController.targetObj.position); // target location
        sensor.AddObservation(teacherKinectT.localPosition); // Kinect location
        sensor.AddObservation(teacherKinectT.localRotation.eulerAngles/360f); // Kinect rotation
        sensor.AddObservation(teacherKinectT.localScale.x*1.8f); // Kinect height/scale
        // in total 10
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        int i = 0;
        // teacherKinectT.localScale = new Vector3(vectorAction[i],vectorAction[i],vectorAction[i]); i++; // Kinect avatar scale, related to player information
        // teacherKinectT.localPosition = new Vector3(vectorAction[i++],vectorAction[i++],vectorAction[i++]); //
        studentKinectHipT.localPosition = new Vector3(2*vectorAction[i++],2*vectorAction[i++],2*vectorAction[i++]);
        
        // Track rotations for each joint (positions will not be tracked since they won't change with time)
        foreach(Transform joint in observedstudentKinectTs)
        {
            joint.localRotation = new Quaternion(vectorAction[i++],vectorAction[i++],vectorAction[i++],vectorAction[i++]);
        }
        // in total 17*4+3=71
        // AddReward(0.002f);
    }

    public override void Heuristic(float[] actionsOut)
    {
        int i = 0;
        actionsOut[i++] = teacherKinectHipT.localPosition.x/2;
        actionsOut[i++] = teacherKinectHipT.localPosition.y/2;
        actionsOut[i++] = teacherKinectHipT.localPosition.z/2;

        foreach(Transform joint in observedteacherKinectTs)
        {
            actionsOut[i++] = joint.localRotation.x;
            actionsOut[i++] = joint.localRotation.y;
            actionsOut[i++] = joint.localRotation.z;
            actionsOut[i++] = joint.localRotation.w;
        }
    }

    // Track joints for the humanoid Kinect rig
    public static string[] trackedJoints = new string[]{
        "Hips",
        "LeftUpLeg","LeftLeg","LeftFoot",
        "RightUpLeg","RightLeg","RightFoot",
        "Spine",
        "LeftShoulder","LeftArm","LeftForeArm","LeftHand",
        "Neck",
        "RightShoulder","RightArm","RightForeArm","RightHand"
    };
}

public static class TransformDeepChildExtension
 {
     //Depth-first search
    public static Transform FindDeepChild(this Transform aParent, string aName)
    {
        foreach(Transform child in aParent)
        {
            if(child.name == aName)
                return child;
            var result = FindDeepChild(child,aName);
            if (result != null)
                return result;
        }
        return null;
    }
 }
