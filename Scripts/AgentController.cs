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
    [Header("Sensor Setting")]
    public Source source = Source.both;
    public bool useGPS = false;

    [Header("Log Setting")]
    public bool logCustomMetrics = false;
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
    CommunicationHub commHub;

    // Animaton capture
    [Header("Observation Capture")]
    public TakeAnimSnapshot animTaker;

    // Task and target selected from Academy
    EnvSetup envSetup;
    NavObj.ObjCategory catSelected;
    NavObj.ObjType typeSelected;
    int locIdxSelected;
    EnvSetup.tasks taskSelected; 

    public NavObj.ObjType CurrentObjType
    {
        get
        {
            return typeSelected;
        }
        set
        {
            typeSelected = value;
        }
    }

    // Training/Inference check
    bool isTraining;
    bool isInference;
    
    // Selected object list and the target object
    [Header("Objects Selected")]
    [SerializeField] Transform[] objList;
    [SerializeField] Transform targetObj;
 
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
    int STEP_COUNT = 0;
    bool EPISODE_DONE = false; // if the episode is completed or not
    bool EPISODE_SUCCESS = false; // if the episode is a success or not
    float tot_sr = 0; // total success rates in one episode
    float tot_sr_sms = 0; // total success rates(sms) in one episode
    StatsRecorder statsRecorder;

    // System random generator
    System.Random rnd;

    #endregion

    void Start()
    {
        rBody = GetComponentInChildren<Rigidbody>();
        startingPosition = this.transform.position;
        startingRotation = this.transform.rotation;

        // Find Academy
        envSetup = GameObject.Find("EnvSetup").GetComponent<EnvSetup>();
        if (envSetup!=null)
        {
            isTraining = envSetup.TrainingCheck();
            isInference = !isTraining;
            targetKeywords = Enum.GetNames(typeof(NavObj.ObjType)).ToList();
        }
        else
        {
            Debug.LogError("Agent Controller: Academy not found");
            return;
        }

        // Find CommunicationHub
        commHub = GameObject.Find("EnvSetup").GetComponent<CommunicationHub>();
        if (commHub==null)
        {
            Debug.LogError("Agent Controller: CommunicationHub not found");
            return;
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

        // StatsRecorder which logs custom metrics into tensorboard
        statsRecorder = Academy.Instance.StatsRecorder;
    }

    public void Update()
    {
        // if(isInference)
        // {  
        //     recognizer.DictationResult += (text, confidence) =>
        //     {
        //         if (text != null)
        //             sentence = text;
        //     };
        // }
    }

    public override void OnEpisodeBegin()
    {
        // Reset agent velocity and angular
        rBody.velocity = Vector3.zero;
        rBody.angularVelocity = Vector3.zero;

        if(CompletedEpisodes>0)
        {
            // Destroy objects in previous episode
            foreach(GameObject obj in objList.Select(objT => objT.gameObject))
            {
                Destroy(obj);
            }

            // Save path if required (only in inference)
            if(saveAgentPath&&drawAgentPath)
            {
                DateTime now = DateTime.Now;
                string savePath = Directory.GetCurrentDirectory() + "/Assets/Log/2Dpath/" + now.ToString("MM-dd-yyyy_HH-mm-ss") + $"{catSelected}{locIdxSelected}{typeSelected}_episdoe-{CompletedEpisodes}.png";
                PathDraw2D.SavePath2PNG(GameObject.Find("FloorPlanCamera").GetComponent<Camera>(), savePath);
            }

            if(logCustomMetrics)
            {
                if(EPISODE_SUCCESS)
                {
                    tot_sr += 1.0f;
                    tot_sr_sms += SuccessRateSMS(STEP_COUNT, targetObj, startingPosition);
                    LogSuccessRate(1f, SuccessRateSMS(STEP_COUNT, targetObj, startingPosition), tot_sr/CompletedEpisodes, tot_sr_sms/CompletedEpisodes);
                }
                else
                {
                    LogSuccessRate(0f, 0f, tot_sr, tot_sr_sms);
                }

                LogDTS(targetObj);
            }
        }

        // Set the next task ang target
        envSetup.settingTaskTarget();
        catSelected = envSetup.objCatSelected;
        locIdxSelected = envSetup.objLocationIdxSelected;
        taskSelected = envSetup.taskSelected;
        if(isTraining||envSetup.replayInInference) 
        {
            objList = commHub.SetupReplay(catSelected, locIdxSelected);
            targetObj = objList[0];
        }
        typeSelected = commHub.objType;

        
        // Reset Agent in a random position
        if (randomPosition)
        {
            ChooseAgentPosition(8f, 5f, out Vector3 pos, out float rot);
            transform.localPosition = pos;
            transform.localRotation = Quaternion.Euler(0f, rot, 0f);
        }
        else
        {
            transform.position = startingPosition;
            transform.rotation = startingRotation;
        }

        // record starting position and rotation of the new episode
        startingPosition = transform.position;
        startingRotation = transform.rotation;

        // Reset rotation and forward movement counter
        rotateActions = 0;
        forwardActions = 0;

        // Reset actions
        actionGoTo = false;
        actionTake = false;
        actionDrop = false;
        actionTurnOn = false;
        actionTurnOff = false;

        // Reset performance measurements
        STEP_COUNT = 0;
        EPISODE_DONE = false;
        EPISODE_SUCCESS = false;
        tot_sr = 0;
        tot_sr_sms = 0;

        //Reset agent trail renderer
        PathDraw2D.ResetPathDraw();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if(useGPS) // use agent poses from Unity if no camera/rendertexture repsent
        {
            // Agent position. Assume no vertical movement.
            sensor.AddObservation(transform.localPosition.x);
            sensor.AddObservation(transform.localPosition.z);

            // // Agent velocity. Assume no vertical movement.
            // sensor.AddObservation(rBody.velocity.x);
            // sensor.AddObservation(rBody.velocity.z);

            // Agent rotation. Assume only rotation in y-axis.
            sensor.AddObservation(transform.localEulerAngles.y);
        }
        else
        {
            sensor.AddObservation(new float[3]);
        }

        if(source!=Source.gesture&&source!=Source.none)
        {
            if(isTraining)
            {
                sensor.AddOneHotObservation((int)typeSelected, targetKeywords.Count);
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
        if(source!=Source.audio&&source!=Source.none)
        {
            sensor.AddObservation(animTaker.TakeKinectSnapshot());
            sensor.AddObservation(animTaker.TakeLeapSnapshot());
        }
        else 
        {
            sensor.AddObservation(new float[373]);
        }
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
        distanceToTarget = DistanceToTarget(transform, targetObj);

        EPISODE_DONE = CalculateRewards(taskSelected);

        if(EPISODE_DONE)
        {
            // Rotation penalty: avoid excessive rotation
            if(rotateActions>180f/turnAmount) {AddReward(-0.005f * (rotateActions - 180/turnAmount));} // excessive rotation penalty
            EndEpisode();
        }

        STEP_COUNT++;
    }

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

    // The Euclidean distance of two object in x-z plane.
    private float Distance2D(Vector3 a, Vector3 b)
    {
        return Mathf.Sqrt( Mathf.Pow(a.x-b.x,2)+Mathf.Pow(a.z-b.z,2) );
    }

    // Calculate the distance to the object
    private float DistanceToTarget(Transform currentObj, Transform target)
    {
        float dist = 1000.0f;
        foreach (Collider col in target.GetComponentsInChildren<Collider>())
        {
            Vector3 closestPoint = col.ClosestPointOnBounds(transform.position);
            dist = Math.Min(Distance2D(currentObj.position, closestPoint), dist);
        }
        return dist;
    }

    // Calculate Rewards based on task, and task stage
    private bool CalculateRewards(EnvSetup.tasks task)
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
                    if(distanceToTarget<navigationThreshold && isVisibleFrom(rgbCam, targetObj))
                    {
                        SetReward(1f);
                        done = true;
                        EPISODE_SUCCESS = true;
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
    private void ChooseAgentPosition(float length, float width, out Vector3 pos, out float rot)
    {
        bool posChoosing = false;
        float xPos = 0f;
        float zPos = 0f;
        float yAngle = 0f;
        // Attempt for 1000 times
        for(int i=0; i<1000; i++)
        {
            xPos = UnityEngine.Random.Range(-length/2, length/2);
            zPos = UnityEngine.Random.Range(-width/2, width/2);
            yAngle = UnityEngine.Random.Range(0f, 360f); 
            posChoosing = CheckPosition(new Vector3(xPos, 0.1f, zPos)); // small offset in y
            if(posChoosing) {break;}
        }
        if(posChoosing)
        {
            pos = new Vector3(xPos,0.1f,zPos);
            rot = yAngle;
        }
        else
        {
            pos = Vector3.zero;
            rot = 0f;
        }
    }

    // Check if the position on the floor is a legitimate position, return true if the position is properly chosen
    private bool CheckPosition(Vector3 positionOnFloor)
    {
        CapsuleCollider collider = GetComponentInChildren<CapsuleCollider>(); 
        Vector3 pt1 = positionOnFloor + new Vector3(0,collider.radius,0); // The center of the sphere at the start of the capsule
        Vector3 pt2 = positionOnFloor - new Vector3(0,collider.radius,0) + new Vector3(0,collider.height,0); // The center of the sphere at the end of the capsule
        Collider[] colliders = Physics.OverlapCapsule(pt1, pt2, collider.radius);
        return colliders.Length == 0 && (Distance2D(positionOnFloor, targetObj.position) >= (2*navigationThreshold));
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
    private int MinRotMovements(Transform target, Vector3 startPos)
    {
        // Vector joining agent and target
        Vector3 jointVec = new Vector3(target.position.x-startPos.x, 0, target.position.z-startPos.z);
        // Minimun angle agent needs to make
        float angle = Vector3.Angle(transform.forward, jointVec);
        return Mathf.CeilToInt(angle/turnAmount);
    }

    // Calculate success rate weighted by minimum steps (SMS)
    private float SuccessRateSMS(int totSteps, Transform target, Vector3 startPos)
    {
        // Minimum number of steps agent needs to take
        int minSteps = MinRotMovements(target, startPos) + Mathf.CeilToInt((Distance2D(startPos, target.position)-navigationThreshold)/forwardAmount);
        return (float)minSteps/Mathf.Max(minSteps, totSteps);
    }

    private void LogSuccessRate(float sr, float sms, float avg_sr, float avg_sms)
    {
        statsRecorder.Add("Metrics/Success Rate", sr);
        statsRecorder.Add("Metrics/Success Rate Weighted by Min Steps", sms);
        statsRecorder.Add("Metrics/Average Success Rate", avg_sr);
        statsRecorder.Add("Metrics/Average Success Rate Weighted by Min Steps", avg_sms);
    }

    // Calculate distance to success (Chaplot et al., 2020) (DTS), which is the distance of the agent from the success threshold boundary when the episode ends
    // and log DTS to Tensorboard
    private void LogDTS(Transform target)
    {
        float dts = Mathf.Max(0f, Distance2D(transform.position, target.position) - navigationThreshold);
        statsRecorder.Add("Metrics/Distance to Success", dts);
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