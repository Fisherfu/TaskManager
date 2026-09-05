using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class NPCController : MonoBehaviour
{
    private enum State
    {
        Idle,
        Patrol,
        Talking
    }

    [Header("Patrol")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float waitTimeAtPoint = 1.5f;
    [SerializeField] private float arrivalThreshold = 0.1f;

    private State currentState;
    private State stateBeforeTalking;
    private int waypointIndex;
    private float waitTimer;
    private Rigidbody2D rb;
    private Animator animator;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        currentState = HasWaypoints ? State.Patrol : State.Idle;
    }

    private bool HasWaypoints => waypoints != null && waypoints.Length > 0;

    public void SetTalking(bool isTalking)
    {
        if (isTalking)
        {
            stateBeforeTalking = currentState;
            currentState = State.Talking;
        }
        else
        {
            currentState = stateBeforeTalking;
        }

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
        }
    }

    private void FixedUpdate()
    {
        if (currentState != State.Patrol || !HasWaypoints)
        {
            return;
        }

        Transform target = waypoints[waypointIndex];
        Vector2 toTarget = (Vector2)target.position - rb.position;

        if (toTarget.magnitude <= arrivalThreshold)
        {
            waitTimer += Time.fixedDeltaTime;
            if (animator != null)
            {
                animator.SetBool("IsMoving", false);
            }

            if (waitTimer >= waitTimeAtPoint)
            {
                waitTimer = 0f;
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
            }
            return;
        }

        Vector2 step = toTarget.normalized * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + step);

        if (animator != null)
        {
            animator.SetFloat("MoveX", toTarget.normalized.x);
            animator.SetFloat("MoveY", toTarget.normalized.y);
            animator.SetBool("IsMoving", true);
        }
    }
}
