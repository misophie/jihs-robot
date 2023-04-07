using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

// At the beginning, we employ a more offensive approach: 
// collect the closest balls and return the balls to the base. 
// If the number of balls that are in our base are over half the total amount of balls 
// (i.e. once we hit 5 balls in the base), 
// the strategy of our robot would then switch to a more defensive play. 
// Now the robot has a goal of defending its base by shooting lasers.
// If we have less than half the total amounts of balls in our base, we stick to the offensive approach.
// We also want the robot to drop off any targets its holding when there's 30 seconds or less.
// We also teach it to not hold onto targets for too long.

public class JIHS : CogsAgent
{
    // ------------------BASIC MONOBEHAVIOR FUNCTIONS-------------------
    
    // Initialize values
    protected override void Start()
    {
        base.Start();
        AssignBasicRewards();
    }

    // For actual actions in the environment (e.g. movement, shoot laser)
    // that is done continuously
    protected override void FixedUpdate() {
        base.FixedUpdate();
        
        LaserControl();
        // Movement based on DirToGo and RotateDir
        moveAgent(dirToGo, rotateDir);

        // If in protect mode, incentivize hitting the enemy! pew pew
        if (protect() && enemy.GetComponent<CogsAgent>().IsFrozen()) AddReward(rewardDict["protect-laser"]);

        // If carrying targets, reward it very slightly negatively... bring it back to base!
        AddReward(rewardDict["held-targets"] * carriedTargets.Count);

    }


    
    // --------------------AGENT FUNCTIONS-------------------------

    // Get relevant information from the environment to effectively learn behavior
    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent velocity in x and z axis 
        var localVelocity = transform.InverseTransformDirection(rBody.velocity);
        sensor.AddObservation(localVelocity.x);
        sensor.AddObservation(localVelocity.z);

        // Time remaning
        sensor.AddObservation(timer.GetComponent<Timer>().GetTimeRemaning());  

        // Agent's current rotation
        var localRotation = transform.rotation;
        sensor.AddObservation(transform.rotation.y);

        // Agent and home base's position
        sensor.AddObservation(this.transform.localPosition);
        sensor.AddObservation(baseLocation.localPosition);

        // for each target in the environment, add: its position, whether it is being carried,
        // and whether it is in a base
        foreach (GameObject target in targets){
            sensor.AddObservation(target.transform.localPosition);
            sensor.AddObservation(target.GetComponent<Target>().GetCarried());
            sensor.AddObservation(target.GetComponent<Target>().GetInBase());
        }
        
        // Whether the agent is frozen
        sensor.AddObservation(IsFrozen());
    }

    // For manual override of controls. This function will use keyboard presses to simulate output from your NN 
    public override void Heuristic(in ActionBuffers actionsOut)
{
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0; //Simulated NN output 0
        discreteActionsOut[1] = 0; //....................1
        discreteActionsOut[2] = 0; //....................2
        discreteActionsOut[3] = 0; //....................3

        //TODO-2: Uncomment this next line when implementing GoBackToBase();
        discreteActionsOut[4] = 0;
       
        if (Input.GetKey(KeyCode.UpArrow))
        {
            discreteActionsOut[0] = 1;
        }       
        if (Input.GetKey(KeyCode.DownArrow))
        {
            discreteActionsOut[0] = 2;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActionsOut[1] = 1;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            //TODO-1: Using the above as examples, set the action out for the left arrow press
            discreteActionsOut[1]= 2;
        }
        

        //Shoot
        if (Input.GetKey(KeyCode.Space)){
            discreteActionsOut[2] = 1;
        }

        //GoToNearestTarget
        if (Input.GetKey(KeyCode.A)){
            discreteActionsOut[3] = 1;
        }


        //TODO-2: implement a keypress (your choice of key) for the output for GoBackToBase();
          if (Input.GetKey(KeyCode.B)) {
            discreteActionsOut[4] = 1;
          }
    }

        // What to do when an action is received (i.e. when the Brain gives the agent information about possible actions)
        public override void OnActionReceived(ActionBuffers actions){

        int forwardAxis = (int)actions.DiscreteActions[0]; //NN output 0

        //TODO-1: Set these variables to their appopriate item from the act list
        int rotateAxis = (int)actions.DiscreteActions[1]; 
        int shootAxis = (int)actions.DiscreteActions[2]; 
        int goToTargetAxis = (int)actions.DiscreteActions[3];
        
        //TODO-2: Uncomment this next line and set it to the appropriate item from the act list
        int goToBaseAxis = (int) actions.DiscreteActions[4];

        //TODO-2: Make sure to remember to add goToBaseAxis when working on that part!
        
        MovePlayer(forwardAxis, rotateAxis, shootAxis, goToTargetAxis, goToBaseAxis); 

    }


// ----------------------ONTRIGGER AND ONCOLLISION FUNCTIONS------------------------
    // Called when object collides with or trigger (similar to collide but without physics) other objects
    protected override void OnTriggerEnter(Collider collision)
    {

        
        if (collision.gameObject.CompareTag("HomeBase") && collision.gameObject.GetComponent<HomeBase>().team == GetTeam())
        {
            // Add rewards here
            if (carriedTargets.Count > 0) {
                // If there is less than 30 seconds left, get the targets back in base!
                // if (timer.GetComponent<Timer>().GetTimeRemaining() < 30) AddReward(rewardDict["30s-left"] * carriedTargets.Count);
                if (timer.GetComponent<Timer>().GetTimeRemaning() < 30) AddReward(rewardDict["30s-left"] * carriedTargets.Count);
                else AddReward(rewardDict["target-in-base"] * carriedTargets.Count);
            }
            else {
                if (protect()) AddReward(rewardDict["protect-in-base"]);
                else AddReward(rewardDict["offense-in-base"]);
            }

        }
        base.OnTriggerEnter(collision);
    }

    protected override void OnCollisionEnter(Collision collision) 
    {
        

        //target is not in my base and is not being carried and I am not frozen
        if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() != GetTeam() && collision.gameObject.GetComponent<Target>().GetCarried() == 0 && !IsFrozen())
        {
            //Add rewards here
            // If in protect phase, give regular incentive
            // If not in protect phase, give higher incentive to collect targets
            if (protect()) AddReward(rewardDict["targets-not-in-base"]);
            else AddReward(rewardDict["offense-collecting-targets"]);
        }

        if (collision.gameObject.CompareTag("Wall"))
        {
            //Add rewards here
            AddReward(rewardDict["touching-wall"]);
        }
        base.OnCollisionEnter(collision);
    }



    //  --------------------------HELPERS---------------------------- 

    // True if robot should protect (more than half of the targets are in base) AND all balls are in a base, false otherwise
    private bool protect() {
        bool targetNotInBase = false;
        foreach (GameObject target in targets) {
            if (target.gameObject.GetComponent<Target>().GetInBase() != 0) targetNotInBase = true;
        }
        return myBase.GetComponent<HomeBase>().GetCaptured() > targets.Length/2 && !targetNotInBase;
    }

     private void AssignBasicRewards() {
        rewardDict = new Dictionary<string, float>();

        rewardDict.Add("frozen", -1f);
        rewardDict.Add("shooting-laser", -0.1f);
        rewardDict.Add("hit-enemy", 1f);
        rewardDict.Add("dropped-one-target", -1f);
        rewardDict.Add("dropped-targets", -2f);
        rewardDict.Add("touching-wall", -0.5f);
        rewardDict.Add("dropped-no-targets", 0.1f);
        rewardDict.Add("targets-not-in-base", 1f);
        rewardDict.Add("target-in-base", 2.5f);
        
        // Rewards for protect and attack, 
        // protect: incentivize staying at base and hitting opponents with a laser
        // attack/offense: discourage staying at base and give higher rewards for collecting targets
        rewardDict.Add("protect-in-base", 0.1f);
        rewardDict.Add("protect-laser", 0.5f);
        rewardDict.Add("offense-in-base", -0.2f);
        rewardDict.Add("offense-collecting-targets", 1.5f);

        // high reward for getting targets back in the last 30s
        rewardDict.Add("30s-left", 5f);

        // negative reward for just holding targets
        rewardDict.Add("held-targets", -0.2f);
    }
    
    private void MovePlayer(int forwardAxis, int rotateAxis, int shootAxis, int goToTargetAxis, int goToBaseAxis)
    //TODO-2: Add goToBase as an argument to this function ^
    {
        dirToGo = Vector3.zero;
        rotateDir = Vector3.zero;

        Vector3 forward = transform.forward;
        Vector3 backward = -transform.forward;
        Vector3 right = transform.up;
        Vector3 left = -transform.up;

        //fowardAxis: 
            // 0 -> do nothing
            // 1 -> go forward
            // 2 -> go backward
        if (forwardAxis == 0){
            //do nothing. This case is not necessary to include, it's only here to explicitly show what happens in case 0
        }
        else if (forwardAxis == 1){
            dirToGo = forward;
        }
        else if (forwardAxis == 2){
            //TODO-1: Tell your agent to go backward!
            dirToGo = backward;
        }

        //rotateAxis: 
            // 0 -> do nothing
            // 1 -> go right
            // 2 -> go left
        if (rotateAxis == 0){
            //do nothing
        }
        
        //TODO-1 : Implement the other cases for rotateDir
         else if (rotateAxis == 1) {
            rotateDir = right;
         }
         else if (rotateAxis == 2) {
            rotateDir = left;
         }

        //shoot
        if (shootAxis == 1){
            SetLaser(true);
        }
        else {
            SetLaser(false);
        }

        //go to the nearest target
        if (goToTargetAxis == 1){
            GoToNearestTarget();
        }

        //TODO-2: Implement the case for goToBaseAxis
        if (goToBaseAxis == 1) {
            GoToBase();
        }
        
    }

    // Go to home base
    private void GoToBase(){
        TurnAndGo(GetYAngle(myBase));
    }

    // Go to the nearest target
    private void GoToNearestTarget(){
        GameObject target = GetNearestTarget();
        if (target != null){
            float rotation = GetYAngle(target);
            TurnAndGo(rotation);
        }        
    }

    // Rotate and go in specified direction
    private void TurnAndGo(float rotation){

        if(rotation < -5f){
            rotateDir = transform.up;
        }
        else if (rotation > 5f){
            rotateDir = -transform.up;
        }
        else {
            dirToGo = transform.forward;
        }
    }

    // return reference to nearest target
    protected GameObject GetNearestTarget(){
        float distance = 200;
        GameObject nearestTarget = null;
        foreach (var target in targets)
        {
            float currentDistance = Vector3.Distance(target.transform.localPosition, transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != team){
                distance = currentDistance;
                nearestTarget = target;
            }
        }
        return nearestTarget;
    }

    private float GetYAngle(GameObject target) {
        
       Vector3 targetDir = target.transform.position - transform.position;
       Vector3 forward = transform.forward;

      float angle = Vector3.SignedAngle(targetDir, forward, Vector3.up);
      return angle; 
        
    }
}
