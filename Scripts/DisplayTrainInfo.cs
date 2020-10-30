using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;
using UnityEngine.UI;

// Display training information, including object selected and animationclip played
public class DisplayTrainInfo : MonoBehaviour
{
    public Animator kinectAnimator;
    public Animator leapAnimator;
    public Text displayArea;
    public EnvSetup envSetup;
    public AgentController agentCtrl;

    private AnimatorController kinectCtrl;
    private AnimatorController leapCtrl;
    private AnimationClip kinectClip;
    private AnimationClip leapClip;

    void LateUpdate()
    {
        if(envSetup.isTraining && agentCtrl.useGesture)
        {
            kinectCtrl = kinectAnimator.runtimeAnimatorController as AnimatorController;
            leapCtrl = leapAnimator.runtimeAnimatorController as AnimatorController;

            NavObj.ObjType currentType = GameObject.FindObjectOfType<AgentController>().CurrentObjType;
            string objInfo = $"The object type is {currentType.ToString()}";

            kinectClip = kinectCtrl.layers[0].stateMachine.defaultState.motion as AnimationClip;
            leapClip = leapCtrl.layers[0].stateMachine.defaultState.motion as AnimationClip;
            string kinectInfo = $"The Kinect clip is {kinectClip.name}";
            string leapInfo = $"The Leap clip is {leapClip.name}";

            displayArea.text = string.Join("\n",new string[]{objInfo,kinectInfo,leapInfo});
        }
    }
}
