using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;

public class RepGestureAnim : MonoBehaviour
{
    // Play prerecorded gesture animation in the scene

    // Replayed Kinect avatar and Leap hands
    public GameObject kinectAvatar;
    public GameObject leapHandLeft;
    public GameObject leapHandRight;

    // Animator controller to replay animation clips
    public AnimatorController kinectController;
    public AnimatorController leapLeftController;
    public AnimatorController leapRightController;

    public EnvSetup envSetup;

    private bool isTraining;
    private bool isReplaying;

    void Start()
    {
        isTraining = GameObject.FindObjectOfType<EnvSetup>().TrainingCheck();
        isReplaying = GameObject.FindObjectOfType<EnvSetup>().replayInInference;
        
        if(isTraining || isReplaying)
        {
            // Remove existing Kinect or Leap avatar component (to avoid behavior overwriting)
            kinectAvatar.GetComponent<Animator>().avatar = null;
            leapHandLeft.GetComponent<Animator>().avatar = null;
            leapHandRight.GetComponent<Animator>().avatar = null;

            // Add AnimatorController to objects at runtime
            kinectAvatar.GetComponent<Animator>().runtimeAnimatorController = kinectController as RuntimeAnimatorController;
            leapHandLeft.GetComponent<Animator>().runtimeAnimatorController = leapLeftController as RuntimeAnimatorController; 
            leapHandRight.GetComponent<Animator>().runtimeAnimatorController = leapRightController as RuntimeAnimatorController;

            // Clear states saved in state machines
            ClearAnimCtrl(kinectController);
            ClearAnimCtrl(leapLeftController);
            ClearAnimCtrl(leapRightController);
        }
    }

    // Set an animation clip as the default clip and play it in the animator controller
    public void PlayAnimClipInCtrl(AnimatorController controller, AnimationClip clip)
    {
        ClearAnimCtrl(controller);
        AnimatorState animState =  controller.layers[0].stateMachine.AddState(clip.name);
        animState.motion = clip;
        controller.layers[0].stateMachine.defaultState = animState;
    }

    // Clear the animator controller to make sure there is no states
    private void ClearAnimCtrl(AnimatorController controller)
    {
        ChildAnimatorState[] animStates = controller.layers[0].stateMachine.states;
        foreach(ChildAnimatorState animState in animStates)
        {
            controller.layers[0].stateMachine.RemoveState(animState.state);
        }
    }

    private void OnApplicationQuit() 
    {
        // Clear states saved in state machines
        ClearAnimCtrl(kinectController);
        ClearAnimCtrl(leapLeftController);
        ClearAnimCtrl(leapRightController);
    }
}
