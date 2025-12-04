using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class AIBehavior : MonoBehaviour
{
    [System.Serializable]
    public class WaypointData
    {
        public Transform point;          // Where to go
        public bool waitHere = false;    // Should AI wait at this waypoint?
        public float waitDuration = 2f;  // How long to wait (seconds)
    }

    [Header("Patrol")]
    public WaypointData[] waypoints;
    public float waypointThreshold = 0.5f;
    public bool loop = true; // not used for cycling, but used for facing next waypoint

    [Header("Patrol Cycle")]
    public float patrolInterval = 5f;   // seconds to wait before each patrol round

    private bool patrolActive = false;  // are we currently walking through waypoints?
    private float patrolTimer = 0f;     // counts up to patrolInterval
    private Vector3 startPosition;
    private Quaternion startRotation;

    [Header("Movement Speeds")]
    public float walkSpeed = 2f;
    public float runSpeed = 4f;

    [Header("Waiting / Turning")]
    public float waitTurnSpeed = 2f;   // how fast AI rotates while waiting

    [Header("Line of Sight / Player Tracking")]
    public Transform playerHead;          // Usually Main Camera on the player
    public float eyeHeight = 1.7f;        // Height from ground where AI "eyes" are
    public float fieldOfViewAngle = 90f;  // Total FOV cone angle
    public float losCheckInterval = 1f;   // Seconds between LOS checks
    public LayerMask hideableLayerMask;   // Layer(s) that can hide the player

    [Header("Game Over")]
    public GameObject gameOverScreen;     // Assign a UI panel / canvas object here
    public float gameOverDistance = 1.2f; // How close AI must get to trigger game over
    public bool freezeTimeOnGameOver = true;

    [Header("Animation")]
    public float damping = 0.1f;

    private NavMeshAgent agent;
    private Animator anim;

    private int currentWaypointIndex = 0;
    private bool isWaiting = false;
    private float waitTimer = 0f;

    private bool hasSpottedPlayer = false;

    private bool patrolFinished = false;
    private bool isChasing = false;
    private float losTimer = 0f;

    private bool gameOverTriggered = false;

    private int forwardHash = Animator.StringToHash("Forward");
    private int rightHash = Animator.StringToHash("Right");
    private int turnHash = Animator.StringToHash("Turn");
    private int onGroundHash = Animator.StringToHash("OnGround");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
    }

    void Start()
    {
        agent.speed = walkSpeed;

        // Remember starting transform so we can teleport back after each patrol
        startPosition = transform.position;
        startRotation = transform.rotation;

        if (playerHead == null && Camera.main != null)
        {
            playerHead = Camera.main.transform;
            Debug.Log($"{name}: playerHead not assigned, using Camera.main ({playerHead.name}).");
        }

        // FIRST PATROL SHOULD WAIT: start idle, not patrolling
        patrolActive = false;
        patrolTimer = 0f;
        patrolFinished = false;
        isWaiting = false;
        currentWaypointIndex = 0;

        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning($"{name}: No waypoints assigned to AIBehavior.");
        }

        if (gameOverScreen != null)
        {
            gameOverScreen.SetActive(false);
        }
    }

    void Update()
    {
        if (gameOverTriggered)
            return;

        // Check line of sight ONLY while patrolling AND only until player is first spotted
        losTimer += Time.deltaTime;
        if (patrolActive && !hasSpottedPlayer && losTimer >= losCheckInterval)
        {
            losTimer = 0f;
            CheckLineOfSight();
        }

        // Once the player has been spotted, always chase
        if ((isChasing || hasSpottedPlayer) && playerHead != null)
        {
            // Lock into chase mode
            isChasing = true;

            Vector3 targetPos = playerHead.position;
            agent.speed = runSpeed;
            agent.SetDestination(targetPos);

            // --- GAME OVER DISTANCE CHECK (using planar distance) ---
            Vector3 aiPosFlat = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 playerPosFlat = new Vector3(playerHead.position.x, 0f, playerHead.position.z);
            float planarDistToPlayer = Vector3.Distance(aiPosFlat, playerPosFlat);

            Debug.Log($"{name}: CHASING (permanent). Distance (XZ) = {planarDistToPlayer:F2}, gameOverDistance = {gameOverDistance}");

            if (!agent.pathPending && planarDistToPlayer <= gameOverDistance)
            {
                TriggerGameOver();
            }
        }
        else
        {
            // Normal patrol only before the player is ever spotted,
            // and only according to the patrol cycle
            HandlePatrolCycle();
        }

        DriveAnimator();
    }

    // ---------------- PATROL CYCLE ----------------

    private void HandlePatrolCycle()
    {
        // If patrol is not active, we're in the waiting phase between rounds
        if (!patrolActive)
        {
            agent.isStopped = true;
            agent.ResetPath();

            patrolTimer += Time.deltaTime;

            if (patrolTimer >= patrolInterval)
            {
                // Time to start a new patrol round
                patrolActive = true;
                patrolTimer = 0f;
                patrolFinished = false;
                isWaiting = false;
                currentWaypointIndex = 0;

                if (waypoints != null && waypoints.Length > 0 && waypoints[0].point != null)
                {
                    agent.isStopped = false;
                    agent.SetDestination(waypoints[0].point.position);
                    Debug.Log($"{name}: Starting patrol cycle, going to waypoint 0 ({waypoints[0].point.name}).");
                }
                else
                {
                    Debug.LogWarning($"{name}: No waypoints set, cannot start patrol cycle.");
                    patrolActive = false;
                }
            }
        }
        else
        {
            // Normal patrol logic while the cycle is active
            HandlePatrol();
        }
    }

    // ---------------- PATROL LOGIC ----------------

    private void HandlePatrol()
    {
        if (!patrolActive)
            return;

        if (waypoints == null || waypoints.Length == 0 || patrolFinished)
            return;

        WaypointData currentWaypoint = waypoints[currentWaypointIndex];

        if (isWaiting)
        {
            // Stop moving while waiting
            agent.ResetPath();

            // Rotate toward player first, then toward next waypoint
            RotateWhileWaiting(currentWaypoint);

            waitTimer += Time.deltaTime;
            if (waitTimer >= currentWaypoint.waitDuration)
            {
                isWaiting = false;
                Debug.Log($"{name}: Finished waiting at waypoint {currentWaypointIndex} ({currentWaypoint.point.name}).");
                AdvanceToNextWaypoint();
            }
            return;
        }

        if (currentWaypoint.point == null)
            return;

        agent.speed = walkSpeed;

        // Only set destination if we don't already have a path
        // or our destination changed
        if (!agent.hasPath || agent.destination != currentWaypoint.point.position)
        {
            agent.SetDestination(currentWaypoint.point.position);
        }

        // Wait for path calculation first
        if (agent.pathPending)
            return;

        // Use remainingDistance instead of raw Vector3 distance
        float remaining = agent.remainingDistance;

        // Optional: tiny guard so it's not NaN/Infinity
        if (remaining <= Mathf.Epsilon)
            return;

        // If we're close enough (taking stoppingDistance into account),
        // treat this as "we reached the waypoint"
        if (remaining <= waypointThreshold + agent.stoppingDistance)
        {
            if (currentWaypoint.waitHere && currentWaypoint.waitDuration > 0f)
            {
                isWaiting = true;
                waitTimer = 0f;
                Debug.Log($"{name}: Reached waypoint {currentWaypointIndex}, waiting {currentWaypoint.waitDuration}s.");
            }
            else
            {
                Debug.Log($"{name}: Reached waypoint {currentWaypointIndex}, going to next.");
                AdvanceToNextWaypoint();
            }
        }
    }

    private void AdvanceToNextWaypoint()
    {
        currentWaypointIndex++;

        if (currentWaypointIndex >= waypoints.Length)
        {
            // End of patrol round: teleport back and wait for next cycle
            EndPatrolCycle();
        }
        else
        {
            Debug.Log($"{name}: Moving to waypoint {currentWaypointIndex} ({waypoints[currentWaypointIndex].point.name}).");
        }
    }

    private void EndPatrolCycle()
    {
        Debug.Log($"{name}: Patrol cycle complete. Teleporting back to start and waiting.");

        patrolActive = false;
        patrolFinished = true;
        isWaiting = false;
        agent.isStopped = true;
        agent.ResetPath();

        // Teleport back to original position on the NavMesh
        if (agent.isOnNavMesh)
        {
            agent.Warp(startPosition);
        }
        else
        {
            transform.position = startPosition;
        }

        transform.rotation = startRotation;

        // Start counting again for the next patrol round
        patrolTimer = 0f;
        currentWaypointIndex = 0;
    }

    // ---------------- LINE OF SIGHT / CHASE ----------------

    private void CheckLineOfSight()
    {
        if (playerHead == null)
            return;

        // If we've already seen the player once, no need to keep checking
        if (hasSpottedPlayer)
            return;

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = playerHead.position - eyePos;
        float distanceToPlayer = toPlayer.magnitude;

        if (distanceToPlayer < 0.01f)
            return;

        Vector3 dirToPlayer = toPlayer / distanceToPlayer;

        // Check if player is in forward cone
        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle > fieldOfViewAngle * 0.5f)
        {
            // Player outside of FOV - do nothing, we only care about starting chase
            return;
        }

        // Raycast towards the player
        Ray ray = new Ray(eyePos, dirToPlayer);
        RaycastHit[] hits = Physics.RaycastAll(ray, distanceToPlayer);

        bool hideableBlocking = false;

        foreach (var hit in hits)
        {
            if ((hideableLayerMask.value & (1 << hit.collider.gameObject.layer)) != 0)
            {
                hideableBlocking = true;
                break;
            }
        }

        // If no Hideable object is blocking, we "see" the player
        if (!hideableBlocking)
        {
            hasSpottedPlayer = true;
            isChasing = true;
            isWaiting = false; // break out of any wait
            Debug.Log($"{name}: Player spotted! ENTERING PERMANENT CHASE MODE (no more hiding).");
        }
    }

    // ---------------- GAME OVER ----------------

    private void TriggerGameOver()
    {
        gameOverTriggered = true;
        agent.isStopped = true;

        Debug.Log($"{name}: GAME OVER - AI reached the player.");

        if (gameOverScreen != null)
        {
            gameOverScreen.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"{name}: Game over triggered but no gameOverScreen assigned.");
        }

        if (freezeTimeOnGameOver)
        {
            Time.timeScale = 0f;
        }
    }

    // ---------------- ANIMATION ----------------

    private void DriveAnimator()
    {
        anim.SetBool(onGroundHash, true);

        if (isChasing || hasSpottedPlayer)
        {
            // FORCE RUN ANIMATION
            anim.SetFloat(forwardHash, 2f);     // full speed forward (tune as needed for your blend tree)
            anim.SetFloat(rightHash, 0f);       // no strafing
            anim.SetFloat(turnHash, 0f);        // ignore turn animations
            return;
        }

        // ------- NORMAL PATROL ANIMATION -------
        Vector3 worldVel = agent.velocity;
        Vector3 localVel = transform.InverseTransformDirection(worldVel);

        float forward = (agent.speed > 0.01f) ? localVel.z / agent.speed : 0f;
        float right = (agent.speed > 0.01f) ? localVel.x / agent.speed : 0f;

        anim.SetFloat(forwardHash, forward, damping, Time.deltaTime);
        anim.SetFloat(rightHash, right, damping, Time.deltaTime);

        float turn = 0f;
        anim.SetFloat(turnHash, turn, damping, Time.deltaTime);
    }

    private void RotateWhileWaiting(WaypointData currentWaypoint)
    {
        // Safety
        if (currentWaypoint == null)
            return;

        // If no wait duration, nothing to do
        if (currentWaypoint.waitDuration <= 0f)
            return;

        // 0..1 progress through the wait
        float t = Mathf.Clamp01(waitTimer / currentWaypoint.waitDuration);

        // FIRST HALF of wait: face the player (if we know their head)
        if (t < 0.5f && playerHead != null)
        {
            Vector3 toPlayer = playerHead.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    waitTurnSpeed * Time.deltaTime
                );
            }
        }
        else
        {
            // SECOND HALF of wait: face the direction of the NEXT waypoint

            int nextIndex = currentWaypointIndex;

            // Pick next waypoint index based on loop/non-loop (for facing only)
            if (waypoints.Length > 1)
            {
                int candidate = currentWaypointIndex + 1;
                if (candidate >= waypoints.Length)
                {
                    nextIndex = loop ? 0 : currentWaypointIndex; // if not looping, keep facing current
                }
                else
                {
                    nextIndex = candidate;
                }
            }

            Transform nextPoint = waypoints[nextIndex].point;
            if (nextPoint == null)
                return;

            Vector3 toNext = nextPoint.position - transform.position;
            toNext.y = 0f;

            if (toNext.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toNext.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    waitTurnSpeed * Time.deltaTime
                );
            }
        }
    }
}
