using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class AIBehavior : MonoBehaviour
{
    [Header("Movement / Patrol")]
    public Transform[] waypoints;         // List of waypoints to visit in order
    public float waypointThreshold = 0.5f; // How close before switching to next waypoint
    public bool loop = true;             // Loop back to start when finished

    public float damping = 0.1f;         // smoothing for animation parameters

    private NavMeshAgent agent;
    private Animator anim;

    private int currentWaypointIndex = 0;

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
        // Start by going to the first waypoint, if any
        if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
        {
            agent.SetDestination(waypoints[0].position);
        }
        else
        {
            Debug.LogWarning($"{name}: No waypoints assigned to AIBehavior.");
        }
    }

    void Update()
    {
        HandlePatrol();
        DriveAnimator();
    }

    private void HandlePatrol()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        Transform currentWaypoint = waypoints[currentWaypointIndex];
        if (currentWaypoint == null)
            return;

        // Keep updating destination (helps if waypoint moves)
        agent.SetDestination(currentWaypoint.position);

        // Check if we are close enough to the waypoint
        float distance = Vector3.Distance(transform.position, currentWaypoint.position);
        if (distance <= waypointThreshold)
        {
            // Go to next waypoint
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Length)
            {
                if (loop)
                {
                    currentWaypointIndex = 0; // loop back
                }
                else
                {
                    // Stop moving at last waypoint
                    currentWaypointIndex = waypoints.Length - 1;
                    agent.ResetPath();
                }
            }
        }
    }

    private void DriveAnimator()
    {
        // Always grounded for this simple AI
        anim.SetBool(onGroundHash, true);

        // Convert world velocity to local (relative to character forward)
        Vector3 worldVel = agent.velocity;
        Vector3 localVel = transform.InverseTransformDirection(worldVel);

        // Normalize by agent speed to keep values roughly -1..1
        float forward = (agent.speed > 0.01f) ? localVel.z / agent.speed : 0f;
        float right = (agent.speed > 0.01f) ? localVel.x / agent.speed : 0f;

        // When agent stops, these go to 0 and the blend tree goes to idle
        anim.SetFloat(forwardHash, forward, damping, Time.deltaTime);
        anim.SetFloat(rightHash, right, damping, Time.deltaTime);

        // --- Turning parameter for Turn Tree ---
        float turn = 0f;

        Transform currentWaypoint = null;
        if (waypoints != null && waypoints.Length > 0)
            currentWaypoint = waypoints[currentWaypointIndex];

        if (currentWaypoint != null)
        {
            Vector3 toTarget = currentWaypoint.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.001f)
            {
                float angle = Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up);
                // Map roughly -90..90 degrees to -1..1
                turn = Mathf.Clamp(angle / 90f, -1f, 1f);
            }
        }

        // When standing still but turn != 0, the controller’s Turn Tree
        // will kick in and play the turning-in-place animations.
        anim.SetFloat(turnHash, turn, damping, Time.deltaTime);
    }

    // Optional: draw gizmos so you can see the path in the Scene view
    void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.DrawSphere(waypoints[i].position, 0.15f);

            int nextIndex = (i + 1) % waypoints.Length;
            if (!loop && i == waypoints.Length - 1) break;
            if (waypoints[nextIndex] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[nextIndex].position);
        }
    }
}
