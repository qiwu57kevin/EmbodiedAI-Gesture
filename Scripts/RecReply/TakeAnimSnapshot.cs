using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

// Take a snapshot of the current frame from the animation
public class TakeAnimSnapshot : MonoBehaviour
{
    public GameObject kinectGameObject;
    public Transform kinectHipT;

    public Transform leapLeftRootT;
    public Transform leapRightRootT;

    private List<Transform> observedKinectTs = new List<Transform>();
    private List<Transform> observedLeapTs = new List<Transform>();

    void Start()
    {
        foreach(string joint in trackedJoints)
        {
            observedKinectTs.Add(GameObject.Find(joint).transform);
        }
        observedLeapTs = leapRightRootT.GetComponentsInChildren<Transform>().ToList();
    }

    public void TakeKinectSnapshot(VectorSensor sensor)
    {
        sensor.AddObservation(kinectGameObject.transform.localScale.x); // Kinect avatar scale, related to player information
        sensor.AddObservation(PosToFloats(kinectGameObject.transform.position)); //
        sensor.AddObservation(PosToFloats(kinectHipT.transform.position));
        
        // Track rotations for each joint (positions will not be tracked since they won't change with time)
        foreach(Transform joint in observedKinectTs)
        {
            sensor.AddObservation(joint.rotation.eulerAngles/360f);
        }
        // in total 148
    }

    public void TakeLeapSnapshot(VectorSensor sensor)
    {
        foreach(Transform leapT in observedLeapTs)
        {
            // leapObs.AddRange(Vector3ToFloats(leapT.localScale));
            sensor.AddObservation(PosToFloats(leapT.position));
            sensor.AddObservation(leapT.rotation.eulerAngles/360f);
        }
        // in total 150
    }

    private List<float> PosToFloats(Vector3 vec)
    {
        List<float> floatList = new List<float>();
        // Normalize position w.r.t room size (x*z*y = L*W*H = 8*5*2.5)
        floatList.Add(vec.x/2f);
        floatList.Add(vec.y/2f);
        floatList.Add(vec.z/2f);
        return floatList;
    }

    // Track joints for the humanoid Kinect rig
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
