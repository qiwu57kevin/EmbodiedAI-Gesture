using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    public List<float> TakeKinectSnapshot()
    {
        List<float> kinectObs = new List<float>();
        kinectObs.Add(kinectGameObject.transform.localScale.x); // Kinect avatar scale, related to player information
        kinectObs.AddRange(Vector3ToFloats(kinectGameObject.transform.position)); //
        kinectObs.AddRange(Vector3ToFloats(kinectHipT.transform.position));
        
        // Track rotations for each joint (positions will not be tracked)
        foreach(Transform joint in observedKinectTs)
        {
            kinectObs.AddRange(Vector3ToFloats(joint.rotation.eulerAngles/180f));
        }
        return kinectObs;
        // in total 148
    }

    public List<float> TakeLeapSnapshot()
    {
        List<float> leapObs = new List<float>();
        foreach(Transform leapT in observedLeapTs)
        {
            leapObs.AddRange(Vector3ToFloats(leapT.localScale));
            leapObs.AddRange(Vector3ToFloats(leapT.position));
            leapObs.AddRange(Vector3ToFloats(leapT.rotation.eulerAngles/180f));
        }
        return leapObs;
        // in total 225
    }

    private List<float> Vector3ToFloats(Vector3 vec)
    {
        List<float> floatList = new List<float>();
        // Normalize position w.r.t room size (x*z*y = L*W*H = 8*5*2.5)
        floatList.Add(vec[0]/8f);
        floatList.Add(vec[1]/2.5f);
        floatList.Add(vec[2]/5f);
        return floatList;
    }

    // Track joints for the humanoid Kinect rig
    private string[] trackedJoints = new string[]{
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
