using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine.Windows.Speech;
using Random = UnityEngine.Random;

public class Controller_Agent : Agent
{
    private const int V = 1;
    [Header("Agent Parameters")]
    public float turnSpeed = 15f; 
    public float walkSpeed = 0.1f;
    public float meterToTarget = 1.5f;
    [Tooltip("Randomly set agent position when it resets.")]
    public bool randomPosition = true;
    [Tooltip("Mimic human movement.")]
    public bool humanoidMovement = false;
    [Tooltip("If require STOP as an additional action")]
    public bool requireStop = false;

    [Header("Voice Recognition Setting")]
    public List<string> target_keywords;
    public List<string> action_keywords;
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
    public bool logAgentSetup = false;
    public string runID = "";

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

    // DataIO
    DataIO dataIO;

    // Kinect Avatar
    Transform kinectAvatar;
    Animator kinect_Animatior;
    HumanPoseHandler kinectPoseHandler;
    HumanPose kinectPose;

    // Task and target selected from Academy
    Academy_Agent academyAgent;
    Academy_Agent.targets targetSelected;
    int targetChildNum;
    Academy_Agent.tasks taskSelected; 

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
    // int rotationActions = 0;
    int forwardActions = 0;

    // Agent performance measurement
    bool done = false; // if the episode is completed or not
    int successEpisode = 0; // successful episode count
    float success_rates = 0; // overall success rates;
    string success_rates_log = ""; // write success rate each episode to a logger
    string agent_setup_log = ""; // get agent setup parameters

    // System random generator
    System.Random rnd;

    void Start()
    {
        rBody = GetComponent<Rigidbody>();
        startingPosition = this.transform.position;
        startingRotation = this.transform.rotation;

        // Find Academy
        academyAgent = GameObject.Find("AgentAcademy").GetComponent<Academy_Agent>();
        if (academyAgent!=null)
        {
            isTraining = academyAgent.TrainingCheck();
            isInference = !isTraining;
            target_keywords = Enum.GetNames(typeof(Academy_Agent.targets)).ToList();
        }
        else
        {
            Debug.LogError("Agent Controller: Academy not found");
            return;
        }

        // Find DataIO
        dataIO = GameObject.Find("AgentAcademy").GetComponent<DataIO>();
        if (dataIO==null)
        {
            Debug.LogError("Agent Controller: DataIO not found");
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
        if (isInference)
        {  
            recognizer.DictationResult += (text, confidence) =>
            {
                if (text != null)
                    sentence = text;
            };
        }

        // meterToTarget = academyAgent.resetParameters["meter_to_target"];
    }

     public override void OnEpisodeBegin()
    {
        // Set agent velocity and angular velocity as 0
        rBody.velocity = Vector3.zero;
        rBody.angularVelocity = Vector3.zero;
        
        // Set the next task ang taget by academy
        academyAgent.targetSelected = academyAgent.initTarget;
        academyAgent.taskSelected = academyAgent.initTask;
        academyAgent.settingTaskTarget();

        if (academyAgent.randomSeed) {academyAgent.testIdx = Random.Range(0,100);}

        targetSelected = academyAgent.targetSelected;
        taskSelected = academyAgent.taskSelected;
        targetChildNum = academyAgent.targetChildNum;

        if(CompletedEpisodes>0)
        {
            target.gameObject.SetActive(false);

            if(source==Source.gesture||source==Source.both)
            {
                // Load new data from Kinect or Leap (prevents repetitive loading of same data file)
                if(isTraining||academyAgent.recordedReplay)
                    dataIO.RequestData();
            }
        }
        target = targetParent.Find(targetSelected.ToString()).GetChild(targetChildNum);
        target.gameObject.SetActive(true);

        if (StepCount==MaxStep)
        {
            // Calculate success rate
            if (logSuccessRate)
            {   
                int minStepRequired =  (int)(180f/turnSpeed) + (int)(Mathf.Abs(Distance2D(startingPosition, target.position) - meterToTarget)/walkSpeed);
                float sr = minStepRequired / Mathf.Max(minStepRequired, StepCount);

                success_rates += sr;
                success_rates_log += (success_rates/CompletedEpisodes).ToString() + ",";
            }
            successEpisode++;
        }

        // randomize source in training
        // if (isTraining)
        // {
            // Array sources = Enum.GetValues(typeof(Source));
            // var rnd = new System.Random();
            // source = (Source) sources.GetValue(rnd.Next(sources.Length));
            // source = (Source) sources.GetValue(1);
        // }

        // Reset Agent in a random position
        if (randomPosition)
        {
            bool posChoosing = true;
            float xPos = 0f;
            float zPos = 0f;
            float yAngle = 0f;
            while (posChoosing)
            {
                xPos = UnityEngine.Random.Range(-4f, 4f);
                zPos = UnityEngine.Random.Range(-2.5f, 2.5f);
                yAngle = UnityEngine.Random.Range(0f, 360f);
                posChoosing = !CheckPosition(new Vector3(xPos, 0f, zPos));
            }
            transform.position = new Vector3(xPos, 0f, zPos);
            transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
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
        // rotationActions = 0;
        forwardActions = 0;

        // reset actions
        actionGoTo = false;
        actionTake = false;
        actionDrop = false;
        actionTurnOn = false;
        actionTurnOff = false;
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
                sensor.AddOneHotObservation((int)targetSelected, target_keywords.Count);
            }
            else
            {
                if(target_keywords.Exists(keyword => sentence.Contains(keyword)))
                {
                    sensor.AddOneHotObservation(target_keywords.FindIndex(keyword => sentence.Contains(keyword)), target_keywords.Count);
                }
                else
                {
                    sensor.AddObservation(new float[target_keywords.Count]);
                }
            }
        }
        else
        {
            sensor.AddObservation(new float[target_keywords.Count]);
        }

        // Add Kinect observations
        if(source!=Source.audio&&source!=Source.none) {addKinectObs(sensor);}
        else {sensor.AddObservation(new float[102]);}

        // Set action mask TODO: need to change action mask script
        // if(isTraining)
        // {
        //     distanceToTarget = Distance2D(transform.position, target.position);
        //     if(distanceToTarget>meterToTarget && (actionGoTo||actionTake||actionTurnOn||actionTurnOff))
        //     {   
        //         SetActionMask(new int[]{3,4,6,7});
        //     }
        //     if(Distance2D(transform.position, kinectAvatar.position)>1f && actionDrop==true)
        //     {
        //         SetActionMask(5);
        //     }
        // }
    }

    private void addKinectObs(VectorSensor sensor)
    {
        if(isTraining || academyAgent.recordedReplay)
        {
            dataIO.RigOverwriter("Kinect"); // add kinect data as input
        }

        kinectPoseHandler.GetHumanPose(ref kinectPose);

        // Add position and rotation as the agent observations
        sensor.AddObservation(kinectPose.bodyPosition);
        sensor.AddObservation(kinectPose.bodyRotation);
        // Add the array of muscle values as the agent observations
        sensor.AddObservation(kinectPose.muscles);
    }

    
    public override void OnActionReceived(float[] vectorAction)
    {
        // Discrete
        // int forward = (int)vectorAction[0];
        // int turn = (int)vectorAction[1];
        int action = (int)vectorAction[0];

        // Continuous
        // float forward = Mathf.Clamp(vectorAction[0], 0.5f, 1f);
        // float turn = vectorAction[1]>0? Mathf.Clamp(vectorAction[1], 1f, 2f):Mathf.Clamp(vectorAction[1], -2f, -1f);

        // Discrete
        if(humanoidMovement)
        {
            m_Animator.SetFloat("Forward", 0);
            m_Animator.SetFloat("Turn", 0);

            // switch(forward)
            // {
            //     case 0:
            //         break;
            //     case 1:
            //         m_Animator.SetFloat("Forward", 1f);
            //         // Debug.Log(m_Animator.deltaPosition.magnitude);
            //         break;
            // }
            // switch(turn)
            // {
            //     case 0:
            //         break;
            //     case 1:
            //         m_Animator.SetFloat("Turn", 1f);
            //         transform.Rotate(0, 10f, 0);
            //         // Debug.Log(m_Animator.deltaRotation.eulerAngles.y);
            //         break;
            //     case 2:
            //         m_Animator.SetFloat("Turn", -1f);
            //         transform.Rotate(0, -10f, 0);
            //         break;
            // }

            switch(action)
            {
                case 1:
                    m_Animator.SetFloat("Forward", 1f);
                    // Debug.Log(m_Animator.deltaPosition.magnitude);
                    break;
                case 2:
                    transform.Rotate(0, turnSpeed, 0);
                    break;
                case 3:
                    transform.Rotate(0, -turnSpeed, 0);
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
            // switch(forward)
            // {
            //     case 0:
            //         break;
            //     case 1:
            //         transform.Translate(transform.forward * walkSpeed, Space.World);
            //         break;
            // }
            // switch(turn)
            // {
            //     case 0:
            //         break;
            //     case 1:
            //         transform.Rotate(0, turnSpeed, 0);

            //         break;
            //     case 2:
            //         transform.Rotate(0, -turnSpeed, 0);
            //         break;
            // }

            switch(action)
            {
                case 1: // Move forward
                    transform.Translate(transform.forward * walkSpeed, Space.World);
                    // rBody.AddForce(transform.forward * walkSpeed, ForceMode.VelocityChange);
                    break;
                // case 2: // Move backward
                    // transform.Translate(-transform.forward * walkSpeed, Space.World);
                //     rBody.AddForce(-transform.forward * walkSpeed, ForceMode.VelocityChange);
                //     break;
                case 2: // Turn right
                    // transform.Rotate(0, turnSpeed * Time.deltaTime, 0);
                    transform.Rotate(0, turnSpeed, 0);
                    break;
                case 3: // Turn left
                    // transform.Rotate(0, -turnSpeed * Time.deltaTime, 0);
                    transform.Rotate(0, -turnSpeed, 0);
                    break;
                case 4: // Action: GoTo
                    if(requireStop)
                    {
                        actionGoTo = true;
                    }
                    break;
            }
        }
        

        // // Continuous
        // m_Animator.SetFloat("Forward", forward);
        // m_Animator.SetFloat("Turn", turn);

        // switch(action)
        // {
        //     case 0: // turn CCW
        //         // Debug.Log("Action" + action + ": Turn CCW");
                
        //         // m_Animator.SetFloat("Turn", turnSpeed);

        //         transform.Rotate(0, turnSpeed, 0);

        //         // rotationActions++;

        //         break;

        //     case 1: // turn CW
        //         // Debug.Log("Action" + action + ": Turn CW");

        //         // m_Animator.SetFloat("Turn", -turnSpeed);

        //         transform.Rotate(0, -turnSpeed, 0);

        //         // rotationActions++;

        //         break;

        //     case 2: // forward
        //         // Debug.Log("Action" + action + ": Move forward");
                
        //         // m_Animator.SetFloat("Forward", walkSpeed);

        //         transform.Translate(transform.forward * walkSpeed, Space.World);

        //         forwardActions++;

        //         break;

        //     case 3: // go to
        //         // Debug.Log("Action" + action + ": Go to object");
        //         actionGoTo = true;
        //         break;

        //     case 4: // take
        //         // Debug.Log("Action" + action + ": Take object");
        //         actionTake = true;
        //         break;

        //     case 5: // drop
        //         // Debug.Log("Action" + action + ": Drop object");
        //         actionDrop = true;
        //         break;

        //     case 6: // turn on
        //         // Debug.Log("Action" + action + ": Turn on object");
        //         actionTurnOn = true;
        //         break;

        //     case 7: // turn off
        //         // Debug.Log("Action" + action + ": Turn off object");
        //         actionTurnOff = true;
        //         break;
        // }

        // Get current distance to the target
        distanceToTarget = Distance2D(transform.position, target.position);

        done = CalculateRewards(taskSelected);

        if(done)
        {
            // AddReward(-0.001f * (rotationActions - 180/turnSpeed)); // excessive rotation penalty
            EndEpisode();
        }
    }

    // Calculate Rewards based on task, and task stage
    bool CalculateRewards(Academy_Agent.tasks task)
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
            case Academy_Agent.tasks.GoTo:
                // if (actionGoTo == true)
                if(requireStop? actionGoTo:true)
                {    
                    if(distanceToTarget<meterToTarget)
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
                    if(distanceToTarget<meterToTarget)
                    {
                        AddReward(1f/MaxStep); // Incentives for moving closer to the target
                    }
                }
                
                // AddReward(meterToTarget/(MaxStep*distanceToTarget));

                break;
            case Academy_Agent.tasks.PickUp:
                if (actionTake == true)
                {
                    SetReward(1f);
                }
                break;
            case Academy_Agent.tasks.Bring:
                if (actionTake == true && actionDrop == true)
                {
                    SetReward(1f);
                }
                break;
            case Academy_Agent.tasks.TurnOn:
                if (actionTurnOn == true)
                {
                    SetReward(1f);
                }
                break;
            case Academy_Agent.tasks.TurnOff:
                if (actionTurnOff == true)
                {
                    SetReward(1f);
                }
                break;
        }

        // Determine if episode is done
        if(actionGoTo||actionDrop||actionTurnOn||actionTurnOff||(actionTake&&task!=Academy_Agent.tasks.Bring)) {done=true;}

        return done;
    }

    public override void Heuristic(float[] actionsOut)
    {
        actionsOut[0] = 0;
        if (Input.GetKey(KeyCode.UpArrow))
        {
            actionsOut[0] = 1;
        }
        // if (Input.GetKey(KeyCode.DownArrow))
        // {
        //     actionsOut[0] = 2;
        // }
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

    /// <summary>
    /// The Euclidean distance of two object in x-z plane.
    ///</summary>
    float Distance2D(Vector3 a, Vector3 b)
    {
        return Mathf.Sqrt( Mathf.Pow(a.x-b.x,2)+Mathf.Pow(a.z-b.z,2) );
    }

    // check the obstacles in front of the agent before it hits them
    bool CheckObstacle()
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

    // Check if the position on the floor is a legitimate position, return true if the position is properly chosen
    bool CheckPosition(Vector3 positionOnFloor)
    {
        CapsuleCollider collider = GetComponent<CapsuleCollider>(); 
        Vector3 pt1 = positionOnFloor + new Vector3(0,collider.radius,0); // The center of the sphere at the start of the capsule
        Vector3 pt2 = positionOnFloor + new Vector3(0,collider.radius,0) + new Vector3(0,collider.height,0); // The center of the sphere at the end of the capsule
        Collider[] colliders = Physics.OverlapCapsule(pt1, pt2, collider.radius);
        return colliders.Length == 0 && (Distance2D(positionOnFloor, target.position) >= (meterToTarget+1));
    }


    // // visualize the outline of the agent capsule collider
    // void OnDrawGizmos()
    // {
    //     CapsuleCollider collider = GetComponent<CapsuleCollider>(); 
    //     Vector3 pt1 = transform.TransformPoint(collider.center);
    //     pt1.y -= collider.height/2;
    //     Vector3 pt2 = transform.TransformPoint(collider.center);
    //     pt2.y += collider.height/2;

    //     DebugExtension.DebugCapsule(pt1, pt2, Color.red, collider.radius);
    // }

    // Manhattan distance to the target
    private float ManhattanDistance(Transform target)
    {
        return Mathf.Abs(this.transform.position.x - target.position.x) + Mathf.Abs(this.transform.position.z - target.position.z);
    }

    // check if the sentence contains certain keywords
    private int sentence2Vector(string sentence)
    {
        float[] keyword_vector = new float[action_keywords.Count + target_keywords.Count - 3]; // indication vector telling if keywords are contained

        // break down sentence into an array of words
        string[] word_array = sentence.Split(new Char[]{' '});

        // if word_array contains one of the keyword from action_keywords
        IEnumerable<string> action_checklist = word_array.Where(word => action_keywords.Contains(word));
        if(action_checklist.Count() != 0)
        {
            // foreach(string item in action_checklist)
            // {
            //     keyword_vector[action_keywords.IndexOf(item)] = 1f;
            // }
        }

        // if word_array contains one of the target keywords
        IEnumerable<string> target_checklist = word_array.Where(word => target_keywords.Contains(word));
        if(target_checklist.Count() != 0)
        {
            foreach(string item in target_checklist)
            {
                // keyword_vector[target_keywords.IndexOf(item) - 2 + action_keywords.Count] = 1f;
                return target_keywords.IndexOf(item) - 2;
            }
        }

        return 0;

        // return keyword_vector;
    }

    ///=========================================================================
    ///utility functions

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

     void OnApplicationQuit()
    {
        if (logSuccessRate)
        {
            DateTime now = DateTime.Now;
            string filename = $"{runID}_SuccessRateLog_" + now.ToString("MM-dd-yyyy_HH-mm-ss");
            TextWriter tw = new StreamWriter(Directory.GetCurrentDirectory() + "/Assets/log/successrate/" + filename + ".csv" );
            tw.Write(success_rates_log);
            tw.Close();

            Debug.Log("Training time: " + Time.time + " seconds");
            Debug.Log("Total episodes: " + CompletedEpisodes);
            Debug.Log("Success rate w/ distance: " + (success_rates/CompletedEpisodes*100).ToString("F2") + "%");
            Debug.Log($"Success episodes percentage: {(successEpisode*100/CompletedEpisodes)}%");
        }

        if (logAgentSetup)
        {
            DateTime now = DateTime.Now;
            string filename = $"{runID}_AgentSetupLog_" + now.ToString("MM-dd-yyyy_HH-mm-ss");
            TextWriter tw = new StreamWriter(Directory.GetCurrentDirectory() + "/Assets/log/agentsetup/" + filename + ".csv" );
            tw.WriteLine($"Agent maximum step: {MaxStep}");
            tw.WriteLine($"Agent turn amount per step: {turnSpeed}");
            tw.WriteLine($"Agent forward amount per step: {walkSpeed}");
            tw.WriteLine($"Success episodes percentage: {(successEpisode*100/CompletedEpisodes)}%");
            tw.Close();
        }
    }

    ///utility functions
    ///=========================================================================
}