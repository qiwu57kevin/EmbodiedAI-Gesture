using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
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
    [Tooltip("Require STOP action.")]
    public bool requireStop = true;
    [Tooltip("Maximum number of STOP actions an agent can issue within one episode.")][Range(10,1000)]
    public int maxStopActions = 10;
    [Tooltip("Mimic human movement.")]
    public bool humanoidMovement = false;

    // how many modals we have (we have category + gesture)
    [Header("Sensor Setting")]
    public bool useCategory = false; // category info
    public bool useGesture = false; // gesture
    public bool useHand = false; // if hand will be used

    [Header("Log Setting")]
    public bool logCustomMetrics = false;
    public bool drawAgentPath = false;
    public bool saveAgentPath = false;
    public string saveDirectory = "/Logs/path2D/";

    [Header("Agent vision sensors")]
    public Camera rgbCam;
    public Camera depthCam;
    

    // Agent states
    Rigidbody m_rBody;
    CapsuleCollider m_collider;
    Vector3 startingPosition;
    Vector3 startingFacingNorm;
    Quaternion startingRotation;
    Animator m_Animator;
    float distanceToTarget = 1000f;
    bool contact = false; // if the agent is in contact with an object
    bool stop = false; // if the agent has issued a stop action

    // Animaton capture
    [Header("Observation Capture")]
    public TakeAnimSnapshot animTaker;

    // External components
    CommunicationHub commHub;
    EnvSetup envSetup;

    // Navigation targets
    List<string> targetKeywords; // a list of target types
    NavObj.ObjCategory catSelected;
    NavObj.ObjType typeSelected;
    int instanceNumSelected;
    int locIdxSelected;
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
    // Renderers for target object
    Renderer[] targetObjRenderers;

    // Agent performance measurement
    int rotateActions = 0; // Rotations performed in one episode
    int stopActions = 0; // Stop actions called in one episode
    int STEP_COUNT = 0; // Number of training steps
    int SUCCESS_EPISODE_COUNT = 0; // Number of successful episodes
    bool EPISODE_DONE = false; // if the episode is completed or not
    bool EPISODE_SUCCESS = false; // if the episode is a success or not
    StatsRecorder statsRecorder;

    // System random generator
    System.Random rnd;

    #endregion

    void Start()
    {
        m_rBody = GetComponentInChildren<Rigidbody>();
        m_collider = GetComponentInChildren<CapsuleCollider>();
        startingPosition = this.transform.position;
        startingFacingNorm = this.transform.forward;
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
        }

        // Find CommunicationHub
        commHub = GameObject.Find("EnvSetup").GetComponent<CommunicationHub>();
        if (commHub==null)
        {
            Debug.LogError("Agent Controller: CommunicationHub not found");
        }

        if(drawAgentPath)
        {
            PathDraw2D.EnablePathDraw();
        }

        // Get the animator and third person character component
        m_Animator = GetComponent<Animator>();

        // System random initializer
        rnd = new System.Random();

        // StatsRecorder which logs custom metrics into tensorboard
        statsRecorder = Academy.Instance.StatsRecorder;
    }

    public void Update()
    {
        // If humanoid movements are not allowed, make sure rigidbody velocity is zero
        if(!humanoidMovement)
        {
            m_rBody.velocity = Vector3.zero;
            m_rBody.angularVelocity = Vector3.zero;
        }

        // Reset agent if it accidently moves out of the room bounds
        if(Mathf.Abs(transform.position.x)>4f||Mathf.Abs(transform.position.z)>2.5f)
        {
            transform.position = startingPosition;
        }
    }

    public override void OnEpisodeBegin()
    {
        // Reset agent velocity and angular
        m_rBody.velocity = Vector3.zero;
        m_rBody.angularVelocity = Vector3.zero;

        // Quit application after 1000 episodes for evaluation
        // if(isInference&&CompletedEpisodes>100)
        // {
        //     EditorApplication.isPlaying=false;
        // }

        if(CompletedEpisodes>0)
        {
            // Destroy objects in previous episode
            foreach(GameObject obj in objList.Select(objT => objT.gameObject))
            {
                Destroy(obj);
            }

            // Save path if required (only in inference)
            if(drawAgentPath)
            {
                //Reset agent trail renderer
                PathDraw2D.ResetPathDraw();

                if(saveAgentPath)
                {
                    DateTime now = DateTime.Now;
                    string savePath = Application.dataPath + saveDirectory;
                    Directory.CreateDirectory(savePath);
                    PathDraw2D.SavePath2PNG(GameObject.Find("FloorPlanCamera").GetComponent<Camera>(), savePath + now.ToString("MM-dd-yyyy_HH-mm-ss") + $"{catSelected}{locIdxSelected}{typeSelected}_episdoe-{CompletedEpisodes}.png");
                }
            }

            if(logCustomMetrics)
            {
                if(EPISODE_SUCCESS)
                {
                    LogSuccessRate(1f, SuccessRateSMS(STEP_COUNT, targetObj, startingPosition, startingFacingNorm));
                }
                else
                {
                    LogSuccessRate(0f, 0f);
                }

                LogDTS(targetObj);
                LogStopActionMetrics();
            }
        }

        // Reset the whole environment
        ResetEnv();
      
        // Reset Agent in a random position
        if (randomPosition)
        {
            ChooseAgentPosition(7.5f, 4.5f, out Vector3 pos, out float rot);
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
        startingFacingNorm = transform.forward;
        startingRotation = transform.rotation;

        // Reset agent counter
        rotateActions = 0;
        stopActions = 0;

        // Reset performance measurements
        STEP_COUNT = 0;
        EPISODE_DONE = false;
        EPISODE_SUCCESS = false;

        // Reset agent states
        distanceToTarget = 1000f;
        contact = false;
        stop = false;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // if(useCategory)
        // {
        //     sensor.AddOneHotObservation((int)typeSelected, targetKeywords.Count);
        // }
        // else
        // {
        //     sensor.AddObservation(new float[targetKeywords.Count]);
        // }

        // Use object instance number as the object label
        if(useCategory)
        {
            sensor.AddOneHotObservation(instanceNumSelected, 33);
        }
        else
        {
            sensor.AddObservation(new float[33]);
        }

        // Add Kinect observations
        if(useGesture)
        {
            animTaker.TakeKinectSnapshot(sensor); // 148
            if(useHand) animTaker.TakeLeapSnapshot(sensor); // 150
            else sensor.AddObservation(new float[150]);
        }
        else 
        {
            sensor.AddObservation(new float[298]);
        }
    }
    
    public override void OnActionReceived(float[] vectorAction)
    {
        int action = (int)vectorAction[0];

        if(humanoidMovement) // Humanoid movements are enabled
        {
            m_Animator.SetFloat("Forward", 0);
            m_Animator.SetFloat("Turn", 0);

            switch(action)
            {
                case 0: // Move forward
                    m_Animator.SetFloat("Forward", 0.2f);
                    transform.Translate(transform.forward * forwardAmount, Space.World);
                    break;
                case 1: // Turn left
                    transform.Rotate(0, turnAmount, 0);
                    break;
                case 2: // Turn right
                    transform.Rotate(0, -turnAmount, 0);
                    break;
            }
        }
        else
        {
            switch(action)
            {
                case 0: // Move forward
                    transform.Translate(transform.forward * forwardAmount, Space.World);
                    break;
                case 1: // Turn right
                    transform.Rotate(0, turnAmount, 0);
                    break;
                case 2: // Turn left
                    // transform.Rotate(0, -turnAmount * Time.deltaTime, 0);
                    transform.Rotate(0, -turnAmount, 0);
                    break;
                case 3: // STOP
                    if(requireStop)
                    {
                        stop = true;
                    }
                    break;
            }
        }

        // Get current distance to the target
        distanceToTarget = DistanceToTarget(transform.position, targetObj);
        // Calculate rewards and check if the episode is completed
        EPISODE_DONE = CalculateRewards();
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
            // Debug.Log("Forward");
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            actionsOut[0] = 2;
            // Debug.Log("Right");
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            actionsOut[0] = 3;
            // Debug.Log("Left");
        }
    }

    #region Customized Functions

    // Calculate Rewards based on task, and task stage
    private bool CalculateRewards()
    {
        bool done = false;

        // Uniform time penalty
        // AddReward(-1f/MaxStep);
        AddReward(-0.001f);

        // Collision penalty
        if (contact)
        {
            // AddReward(-5f/MaxStep);
            AddReward(-0.005f);
        }


        if(requireStop)
        {
            if(stop)
            {
                stopActions++;
                if(distanceToTarget<navigationThreshold && isVisibleFromCam(rgbCam, targetObj))
                {
                    AddReward(1f);
                    done = true;
                    EPISODE_SUCCESS = true;
                }
                // else if(stopActions>maxStopActions)
                // {
                //     AddReward(-0.1f);
                //     done = true;
                // }
                else
                {
                    // AddReward(-2f/MaxStep);
                    AddReward(-0.1f);
                    // done = true;
                }
                stop = false;
            }
        }
        else
        {
            if(distanceToTarget<navigationThreshold && isVisibleFromCam(rgbCam, targetObj))
            {
                SetReward(1f);
                done = true;
                EPISODE_SUCCESS = true;
            }
        }
        return done;
    }

    // Reset the environment, including rooms and targets
    private void ResetEnv()
    {
        // Setup Room
        if(envSetup.autoSetRoom) commHub.SetupRoom();

        // Set the next task ang target
        envSetup.settingTaskTarget();
        catSelected = envSetup.objCatSelected;
        locIdxSelected = envSetup.objLocationIdxSelected;

        objList = commHub.SetupTarget(catSelected, locIdxSelected);
        targetObj = objList[0];
        if((isTraining||envSetup.replayInInference)&&(useGesture)) 
        {
            commHub.SetupReplay(catSelected, locIdxSelected);
        }
        // typeSelected = commHub.objType;
        instanceNumSelected = commHub.objInstanceNum;
        targetObjRenderers = targetObj.GetComponentsInChildren<Renderer>();
    }

    // The Euclidean distance of two object in x-z plane.
    private float Distance2D(Vector3 a, Vector3 b)
    {
        return Mathf.Sqrt( Mathf.Pow(a.x-b.x,2)+Mathf.Pow(a.z-b.z,2) );
    }

    // Calculate the distance to the object
    private float DistanceToTarget(Vector3 pos, Transform target)
    {
        // Vector3 closestPoint = targetObjBounds.ClosestPoint(pos);
        // return Distance2D(pos, closestPoint)-0.3f; // offset by object radius
        return Distance2D(pos, target.position) - 0.3f;
    }

    // Randomly choose agent positions based on the room width and length
    private void ChooseAgentPosition(float length, float width, out Vector3 pos, out float rot)
    {
        bool posChoosing = false;
        float xPos = 0f;
        float zPos = 0f;
        float yAngle = 0f;
        yAngle = UnityEngine.Random.Range(-180f, 180f); 
        // Attempt for 10000 times
        for(int i=0; i<10000; i++)
        {
            xPos = UnityEngine.Random.Range(-length/2, length/2);
            zPos = UnityEngine.Random.Range(-width/2, width/2);
            posChoosing = CheckPosition(new Vector3(xPos, 0f, zPos));
            if(posChoosing) break;
        }
        if(posChoosing)
        {
            pos = new Vector3(xPos,0f,zPos);
            rot = yAngle;
        }
        else
        {
            Debug.Log("No possible position");
            pos = Vector3.zero;
            rot = 0f;
        }
    }

    // Check if the position on the floor is a legitimate position, return true if the position is properly chosen
    private bool CheckPosition(Vector3 positionOnFloor)
    {
        Vector3 pt1 = positionOnFloor + new Vector3(0,m_collider.radius,0); // The center of the sphere at the start of the capsule
        Vector3 pt2 = positionOnFloor - new Vector3(0,m_collider.radius,0) + new Vector3(0,m_collider.height,0); // The center of the sphere at the end of the capsule
        Collider[] colliders = Physics.OverlapCapsule(pt1, pt2, m_collider.radius).Where(col => col.CompareTag("Environment")||col.CompareTag("Structure")).ToArray();
        return colliders.Length == 0 && (DistanceToTarget(positionOnFloor, targetObj) >= (navigationThreshold+1f)); // make sure it is at least 1m away from the target object
    }
    
    // Check if an Renderer is rendered by a specific camera
    public bool isVisibleFromCam(Camera camera, Transform target)
    {
        // Get planes making up the camera frustrum
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        if(target.childCount==0)
        {
            return GeometryUtility.TestPlanesAABB(planes, target.GetComponent<Renderer>().bounds);
        }
        else // if this is a parent Gameobject, check its children
        {
            foreach(Renderer childRenderer in targetObjRenderers)
            {
                if(GeometryUtility.TestPlanesAABB(planes, childRenderer.bounds))
                {
                    return GeometryUtility.TestPlanesAABB(planes, childRenderer.bounds);
                }
            }
            return false;
        }
    }

    // Calculate the angle between current facing direction and the target
    private float FacingAngle(Transform target, Vector3 currentPos, Vector3 facingNorm)
    {
        Vector3 jointVec = new Vector3(target.position.x-currentPos.x, 0, target.position.z-currentPos.z);
        float angle = Vector3.Angle(facingNorm, jointVec);
        return angle;
    }

    // Calcuate minimum number of rotations agent needs to make to reach the object
    private int MinRotMovements(Transform target, Vector3 startPos, Vector3 facingNorm)
    {
        float angle = FacingAngle(target, startPos, facingNorm);
        return Mathf.CeilToInt(angle/turnAmount);
    }

    // Calculate success rate weighted by minimum steps (SMS)
    private float SuccessRateSMS(int totSteps, Transform target, Vector3 startPos, Vector3 facingNorm)
    {
        // Minimum number of steps agent needs to take
        int minSteps = MinRotMovements(target, startPos, facingNorm) + Mathf.CeilToInt((Distance2D(startPos, target.position)-navigationThreshold)/forwardAmount);
        return (float)minSteps/Mathf.Max(minSteps, totSteps);
    }

    private void LogSuccessRate(float sr, float sms)
    {
        statsRecorder.Add("Metrics/Success Rate", sr);
        statsRecorder.Add("Metrics/Success Rate Weighted by Min Steps", sms);

        // if(requireStop)
        // {
        //     statsRecorder.Add("Metrics/SR on First Stop", stopActions<2? sr:0f);
        //     statsRecorder.Add("Metrics/SMS on First Stop", stopActions<2? sms:0f);
        // }
    }

    // Log metrics related with stop actions
    private void LogStopActionMetrics()
    {
        statsRecorder.Add("Success/Number of Stops", stopActions);
        // statsRecorder.Add("Metrics/SR on First Stop", numStops<2? sr:0f);
        // statsRecorder.Add("Metrics/SMS on First Stop", numStops<2? sr:0f);
        // statsRecorder.Add("Metrics/DTS on First Stop", numStops<2? sr:0f);

        // Log SR less than 3 stops

        // Log SR less than 5 stops

        // Log SR less than 10 stops

        // Log SR less than 15 stops

        // Log SR less than 20 stops
    }

    // Calculate distance to success (Chaplot et al., 2020) (DTS), which is the distance of the agent from the success threshold boundary when the episode ends
    // and log DTS to Tensorboard
    private void LogDTS(Transform target)
    {
        float dts = Mathf.Max(0f, DistanceToTarget(transform.position, target) - navigationThreshold);
        statsRecorder.Add("Metrics/Distance to Success", dts);

        // if(requireStop)
        // {
        //     statsRecorder.Add("Metrics/DTS on First Stop", stopActions<2? dts:0f);
        // }
    }

    ///=========================================================================
    ///Unity events

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

    }

    ///Unity events
    ///=========================================================================
    #endregion
}