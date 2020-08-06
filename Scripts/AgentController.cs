using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine.Windows.Speech;
using Random = UnityEngine.Random;

public class AgentController : Agent
{
    # region AgentController fields
    [Header("Agent Parameters")]
    [Range(0,30)]
    public float turnAmount = 15f;
    [Range(0,1)]
    public float forwardAmount = 0.2f;
    [Range(0,2)]
    public float navigationThreshold = 1.5f;
    [Tooltip("Randomly set agent position when it resets.")]
    public bool randomPosition = true;
    [Tooltip("Mimic human movement.")]
    public bool humanoidMovement = false;
    [Tooltip("If require STOP as an additional action")]
    public bool requireStop = false;

    [Header("Voice Recognition Setting")]
    public List<string> targetKeywords;
    public List<string> actionKeywords;
    private DictationRecognizer recognizer; // keyword recognizer
    [Tooltip("Set up confidence for the voice recognizer. Higher confidence indicates higher accuracy.")] 
    public ConfidenceLevel confidence = ConfidenceLevel.Medium;
    public string sentence = ""; // sentence recognized in each step

    // how many modals we have (we have audio + gesture)
    public enum Source
    {
        audio, gesture, both, none
    }
    [Header("Modality Setting")]
    public Source source = Source.both;

    [Header("Logger Setting")]
    public bool logSuccessRate = false;
    public bool drawAgentPath = false;
    public bool saveAgentPath = false;

    [Header("Agent vision sensors")]
    public Camera rgbCam;
    public Camera depthCam;
    

    // Agent position and dynamics
    Rigidbody rBody;
    Vector3 startingPosition;
    Quaternion startingRotation;
    Animator m_Animator; // a reference to the animator component

    // Agent state
    float distanceToTarget = 1000;
    bool takeStatus = false;
    bool TurnOnStatus = false;
    bool contact = false;
    int contactCount = 0;

    // CommunicationHub
    CommunicationHub CommunicationHub;

    // Kinect Avatar
    Transform kinectAvatar;
    Animator kinect_Animatior;
    HumanPoseHandler kinectPoseHandler;
    HumanPose kinectPose;

    // Task and target selected from Academy
    EnvSetup academyAgent;
    EnvSetup.targets targetSelected;
    int targetChildNum;
    EnvSetup.tasks taskSelected; 

    // Training/Inference check
    bool isTraining;
    bool isInference;
    
    // Target parent gameobject and selected target transform
    Transform targetParent;
    Transform target;
 
    // Action state
    bool actionGoTo = false;
    bool actionTake = false;
    bool actionDrop = false;
    bool actionTurnOn = false;
    bool actionTurnOff = false;
    bool wasPickUp = false;
    bool hitWall= false;

    // Rotation and forward actions performed in one episode
    int rotateActions = 0;
    int forwardActions = 0;

    // Agent performance measurement
    bool done = false; // if the episode is completed or not
    int successEpisode = 0; // successful episode count
    float success_rates = 0; // overall success rates;
    float success_rates_sms = 0; // overall SMS success rates

    // System random generator
    System.Random rnd;

    #endregion

    void Start()
    {
        rBody = GetComponent<Rigidbody>();
        startingPosition = this.transform.position;
        startingRotation = this.transform.rotation;

        // Find Academy
        academyAgent = GameObject.Find("EnvSetup").GetComponent<EnvSetup>();
        if (academyAgent!=null)
        {
            isTraining = academyAgent.TrainingCheck();
            isInference = !isTraining;
            targetKeywords = Enum.GetNames(typeof(EnvSetup.targets)).ToList();
        }
        else
        {
            Debug.LogError("Agent Controller: Academy not found");
            return;
        }

        // Find CommunicationHub
        CommunicationHub = GameObject.Find("EnvSetup").GetComponent<CommunicationHub>();
        if (CommunicationHub==null)
        {
            Debug.LogError("Agent Controller: CommunicationHub not found");
            return;
        }
        
        // Find target list
        targetParent = GameObject.Find("Targets").transform;
        if (targetParent==null)
        {
            Debug.LogError("Agent Controller: Target list not found");
            return;
        }

        // Find Kinect avatar
        kinectAvatar = GameObject.Find("KinectAvatar").transform;
        if (kinectAvatar==null)
        {
            Debug.LogError("Agent Controller: Kinect avatar not found");
        }
        else
        {
            kinect_Animatior = kinectAvatar.GetComponent<Animator>();
            kinectPoseHandler = new HumanPoseHandler(kinect_Animatior.avatar, kinectAvatar);
            kinectPose = new HumanPose();
        }

        if(drawAgentPath)
        {
            PathDraw2D.EnablePathDraw();
        }

        // Initiate dictation recognizer
        recognizer = new DictationRecognizer(confidence);
        recognizer.Start();

        // Get the animator and third person character component
        m_Animator = GetComponent<Animator>();

        // System random initializer
        rnd = new System.Random();
    }

    public void Update()
    {
        if(isInference)
        {  
            recognizer.DictationResult += (text, confidence) =>
            {
                if (text != null)
                    sentence = text;
            };
        }
    }

    public override void OnEpisodeBegin()
    {
        // Set agent velocity and angular velocity as 0
        rBody.velocity = Vector3.zero;
        rBody.angularVelocity = Vector3.zero;

        if(CompletedEpisodes>0)
        {
            target.gameObject.SetActive(false);

            if(source==Source.gesture||source==Source.both)
            {
                // Load new data from Kinect or Leap (prevents repetitive loading of same data file)
                if(isTraining||academyAgent.replayInInference)
                    CommunicationHub.RequestData();
            }

            // Save path if required (only in inference)
            if(saveAgentPath&&drawAgentPath)
            {
                DateTime now = DateTime.Now;
                string savePath = Directory.GetCurrentDirectory() + "/Assets/Log/2Dpath/" + now.ToString("MM-dd-yyyy_HH-mm-ss") + $"{targetSelected}{targetChildNum}_episdoe-{CompletedEpisodes}.png";
                PathDraw2D.SavePath2PNG(GameObject.Find("FloorPlanCamera").GetComponent<Camera>(), savePath);
            }

            if(logSuccessRate)
            {
                if(StepCount<MaxStep)
                {
                    success_rates += 1.0f;
                    success_rates_sms += SuccessRateSMS(StepCount, target);
                }
                LogSuccessRate(success_rates/CompletedEpisodes, success_rates_sms/CompletedEpisodes);
            }
        }

        // Set the next task ang taget by academy
        academyAgent.settingTaskTarget();

        if (academyAgent.shuffleReplay) {academyAgent.testIdx = Random.Range(0,100);}

        targetSelected = academyAgent.targetSelected;
        taskSelected = academyAgent.taskSelected;
        targetChildNum = academyAgent.targetChildNum;
        target = targetParent.Find(targetSelected.ToString()).GetChild(targetChildNum);
        target.gameObject.SetActive(true);

        // Reset Agent in a random position
        if (randomPosition)
        {
            ChooseAgentPosition(5f, 8f, out Vector3 pos, out float rot);
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, rot, 0f);
        }
        else
        {
            transform.position = startingPosition;
            transform.rotation = startingRotation;
        }

        // record starting position and rotation of the new episode
        startingPosition = transform.position;
        startingRotation = transform.rotation;

        // reset rotation and forward movement counter
        rotateActions = 0;
        forwardActions = 0;

        // reset actions
        actionGoTo = false;
        actionTake = false;
        actionDrop = false;
        actionTurnOn = false;
        actionTurnOff = false;

        //Reset agent trail renderer
        PathDraw2D.ResetPathDraw();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if(GetComponent<CameraSensorComponent>()!=null) // use agent poses from Unity if no camera/rendertexture repsent
        {
            // Agent position. Assume no vertical movement.
            sensor.AddObservation(transform.localPosition.x);
            sensor.AddObservation(transform.localPosition.z);

            // Agent velocity. Assume no vertical movement.
            sensor.AddObservation(rBody.velocity.x);
            sensor.AddObservation(rBody.velocity.z);

            // Agent rotation. Assume only rotation in y-axis.
            sensor.AddObservation(transform.localEulerAngles.y);
        }
        else
        {
            sensor.AddObservation(new float[5]);
        }

        if(source!=Source.gesture&&source!=Source.none)
        {
            if(isTraining)
            {
                sensor.AddOneHotObservation((int)targetSelected, targetKeywords.Count);
            }
            else
            {
                if(targetKeywords.Exists(keyword => sentence.Contains(keyword)))
                {
                    sensor.AddOneHotObservation(targetKeywords.FindIndex(keyword => sentence.Contains(keyword)), targetKeywords.Count);
                }
                else
                {
                    sensor.AddObservation(new float[targetKeywords.Count]);
                }
            }
        }
        else
        {
            sensor.AddObservation(new float[targetKeywords.Count]);
        }

        // Add Kinect observations
        if(source!=Source.audio&&source!=Source.none) {addKinectObs(sensor);}
        else {sensor.AddObservation(new float[102]);}
    }
    
    public override void OnActionReceived(float[] vectorAction)
    {
        int action = (int)vectorAction[0];

        if(humanoidMovement)
        {
            m_Animator.SetFloat("Forward", 0);
            m_Animator.SetFloat("Turn", 0);

            switch(action)
            {
                case 1:
                    m_Animator.SetFloat("Forward", 0.2f);
                    transform.Translate(transform.forward * forwardAmount, Space.World);
                    // Debug.Log(m_Animator.deltaPosition.magnitude);
                    break;
                case 2:
                    transform.Rotate(0, turnAmount, 0);
                    break;
                case 3:
                    transform.Rotate(0, -turnAmount, 0);
                    break;
                case 4: // Action: GoTo
                    if(requireStop)
                    {
                        actionGoTo = true;
                    }
                    break;
            }
        }
        else
        {
            switch(action)
            {
                case 1: // Move forward
                    transform.Translate(transform.forward * forwardAmount, Space.World);
                    // rBody.AddForce(transform.forward * forwardAmount, ForceMode.VelocityChange);
                    break;
                case 2: // Turn right
                    // transform.Rotate(0, turnAmount * Time.deltaTime, 0);
                    transform.Rotate(0, turnAmount, 0);
                    break;
                case 3: // Turn left
                    // transform.Rotate(0, -turnAmount * Time.deltaTime, 0);
                    transform.Rotate(0, -turnAmount, 0);
                    break;
                case 4: // Action: GoTo
                    if(requireStop)
                    {
                        actionGoTo = true;
                    }
                    break;
            }
        }

        // Get current distance to the target
        distanceToTarget = Distance2D(transform.position, target.position);

        done = CalculateRewards(taskSelected);

        if(done)
        {
            AddReward(-0.005f * (rotateActions - 180/turnAmount)); // excessive rotation penalty
            EndEpisode();
        }
    }

    // // Mask stop action during training to ensure more stable training
    // public override void CollectDiscreteActionMasks(DiscreteActionMasker actionMasker)
    // {
    //     if(isTraining)
    //     {
    //         if(distanceToTarget>=navigationThreshold)
    //         {
    //             actionMasker.SetMask(0, new int[1]{4});
    //         }
    //     }
    // }

    public override void Heuristic(float[] actionsOut)
    {
        actionsOut[0] = 0;
        if (Input.GetKey(KeyCode.UpArrow))
        {
            actionsOut[0] = 1;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            actionsOut[0] = 2;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            actionsOut[0] = 3;
        }
        if (requireStop&&Input.GetKey(KeyCode.Space))
        {
            actionsOut[0] = 4;
        }
    }

    #region Customized Functions
    /// <summary>
    /// The Euclidean distance of two object in x-z plane.
    ///</summary>
    float Distance2D(Vector3 a, Vector3 b)
    {
        return Mathf.Sqrt( Mathf.Pow(a.x-b.x,2)+Mathf.Pow(a.z-b.z,2) );
    }

    // Calculate Rewards based on task, and task stage
    bool CalculateRewards(EnvSetup.tasks task)
    {
        bool done = false;

        // Uniform time penalty
        AddReward(-1f/MaxStep);

        // Adaptive time penalty
        // AddReward(-5f/(MaxStep*Distance2D(startingPosition, target.position)));

        // Collision penalty
        if (contact)
        {
            AddReward(-5f/MaxStep);
        }

        // Add reward based on stage
        switch(task)
        {
            case EnvSetup.tasks.GoTo:
                // if (actionGoTo == true)
                if(requireStop? actionGoTo:true)
                {    
                    // Conditions for a successfull episode: sufficiently close to target, execute stop action, target in camera view
                    if(distanceToTarget<navigationThreshold && isVisibleFrom(rgbCam, target))
                    {
                        SetReward(1f);
                        done = true;
                    }
                    else if(requireStop? actionGoTo:false)
                    {
                        SetReward(-0.1f);
                        done = true;
                    }
                }
                else
                {
                    if(distanceToTarget<navigationThreshold)
                    {
                        AddReward(1f/MaxStep); // Incentives for moving closer to the target
                    }
                }
                
                // AddReward(navigationThreshold/(MaxStep*distanceToTarget));

                break;
            case EnvSetup.tasks.PickUp:
                if (actionTake == true)
                {
                    SetReward(1f);
                }
                break;
            case EnvSetup.tasks.Bring:
                if (actionTake == true && actionDrop == true)
                {
                    SetReward(1f);
                }
                break;
            case EnvSetup.tasks.TurnOn:
                if (actionTurnOn == true)
                {
                    SetReward(1f);
                }
                break;
            case EnvSetup.tasks.TurnOff:
                if (actionTurnOff == true)
                {
                    SetReward(1f);
                }
                break;
        }

        // Determine if episode is done
        if(actionGoTo||actionDrop||actionTurnOn||actionTurnOff||(actionTake&&task!=EnvSetup.tasks.Bring)) {done=true;}

        return done;
    }

    // Add encoded Kinect gesture as Observations
    private void addKinectObs(VectorSensor sensor)
    {
        if(isTraining || academyAgent.replayInInference)
        {
            CommunicationHub.RigOverwriter("Kinect"); // add kinect data as input
        }

        kinectPoseHandler.GetHumanPose(ref kinectPose);

        // Add position and rotation as the agent observations
        sensor.AddObservation(kinectPose.bodyPosition);
        sensor.AddObservation(kinectPose.bodyRotation);
        // Add the array of muscle values as the agent observations
        sensor.AddObservation(kinectPose.muscles);
    }

    // check the obstacles in front of the agent before it hits them
    private bool CheckObstacle()
    {
        CapsuleCollider collider = GetComponent<CapsuleCollider>(); 
        Vector3 pt1 = transform.TransformPoint(collider.center);
        pt1.y -= collider.height/2;
        Vector3 pt2 = transform.TransformPoint(collider.center);
        pt2.y += collider.height/2;
        // Collider[] colliders = Physics.OverlapCapsule(pt1, pt2, collider.radius * 2);
        // return colliders.Where(col => col.CompareTag("Environment") || col.CompareTag("Structure")).ToArray().Length != 0;
        return Physics.CapsuleCast(pt1, pt2, collider.radius, transform.forward, 0.3f);
    }

    // Randomly choose agent positions based on the room width and length
    private void ChooseAgentPosition(float width, float length, out Vector3 pos, out float rot)
    {
        bool posChoosing = true;
        float xPos = 0f;
        float zPos = 0f;
        float yAngle = 0f;
        while (posChoosing)
        {
            xPos = UnityEngine.Random.Range(-length/2, length/2);
            zPos = UnityEngine.Random.Range(-width/2, width/2);
            yAngle = UnityEngine.Random.Range(0f, 360f);
            posChoosing = !CheckPosition(new Vector3(xPos, 0f, zPos));
        }
        pos = new Vector3(xPos,0f,zPos);
        rot = yAngle;
    }

    // Check if the position on the floor is a legitimate position, return true if the position is properly chosen
    private bool CheckPosition(Vector3 positionOnFloor)
    {
        CapsuleCollider collider = GetComponent<CapsuleCollider>(); 
        Vector3 pt1 = positionOnFloor + new Vector3(0,collider.radius,0); // The center of the sphere at the start of the capsule
        Vector3 pt2 = positionOnFloor + new Vector3(0,collider.radius,0) + new Vector3(0,collider.height,0); // The center of the sphere at the end of the capsule
        Collider[] colliders = Physics.OverlapCapsule(pt1, pt2, collider.radius);
        return colliders.Length == 0 && (Distance2D(positionOnFloor, target.position) >= (navigationThreshold+1));
    }
    
    // Check if an Renderer is rendered by a specific camera
    public bool isVisibleFrom(Camera camera, Transform target)
    {
        // Get planes making up the camera frustrum
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        if(target.childCount==0)
        {
            return GeometryUtility.TestPlanesAABB(planes, target.GetComponent<Renderer>().bounds);
        }
        else // if this is a parent Gameobject, check its children
        {
            foreach(Renderer childRenderer in target.GetComponentsInChildren<Renderer>())
            {
                if(GeometryUtility.TestPlanesAABB(planes, childRenderer.bounds))
                {
                    return GeometryUtility.TestPlanesAABB(planes, childRenderer.bounds);
                }
            }
            return false;
        }
    }

    // Calcuate minimum number of rotations agent needs to make to reach the object
    private int MinRotMovements(Transform target)
    {
        // Vector joining agent and target
        Vector3 jointVec = new Vector3(target.position.x-transform.position.x, 0, target.position.z-transform.position.z);
        // Minimun angle agent needs to make
        float angle = Vector3.Angle(transform.forward, jointVec);
        return Mathf.CeilToInt(angle/turnAmount);
    }

    // Calculate success rate weighted by minimum steps (SMS)
    private float SuccessRateSMS(int totSteps, Transform target)
    {
        // Minimum number of steps agent needs to take
        int minSteps = MinRotMovements(target) + Mathf.CeilToInt(Distance2D(transform.position, target.position)/forwardAmount);
        return minSteps/Mathf.Max(minSteps, totSteps);
    }

    private void LogSuccessRate(float sr, float sms)
    {
        var statsRecorder = Academy.Instance.StatsRecorder;
        statsRecorder.Add("Average Success Rate", sr);
        statsRecorder.Add("Average SMS", sms);
    }

    ///=========================================================================
    ///Unity events

    // Trigger
    void OnTriggerEnter(Collider collider)
    {
        if(collider.tag == "Environment" || collider.tag == "Structure")
        {
            contact = true;
        }
    }

    void OnTriggerExit(Collider collider)
    {
        contact = false;
        contactCount = 0;
    }

    // Collision
    void OnCollisionEnter(Collision collision)
    {
        if(collision.collider.tag == "Environment" || collision.collider.tag == "Structure")
        {
            contact = true;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        contact = false;
        contactCount = 0;
    }

    ///Unity events
    ///=========================================================================
    #endregion
}