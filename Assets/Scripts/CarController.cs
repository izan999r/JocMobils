using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct SpawnPoint
{
    public Vector3 position;
    public Vector3 rotation;
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class CarController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float maxSpeedForward = 20f;
    [SerializeField] private float maxSpeedReverse = 8f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float brakeDeceleration = 20f;
    [SerializeField] private float naturalDeceleration = 6f;

    [Header("Giro")]
    [SerializeField] private float maxTurnSpeed = 90f;
    [SerializeField] private float turnReductionStartSpeed = 8f;
    [Range(0.05f, 1f)]
    [SerializeField] private float minTurnFactor = 0.25f;
    [SerializeField] private float steerSmoothing = 500f;
    [SerializeField] private float minSpeedToTurn = 0.3f;

    [Header("Física")]
    [SerializeField] private float extraGravity = 25f;
    [Range(0f, 1f)]
    [SerializeField] private float lateralGrip = 0.85f;
    [SerializeField] private float centerOfMassOffsetY = -0.5f;
    [SerializeField] private float maxAngularVel = 2f;

    [Header("Reaparición")]
    [Tooltip("Lista de puntos de reaparición con posición y rotación")]
    [SerializeField] private SpawnPoint[] spawnPoints;

    [Tooltip("Altura extra sobre el punto de reaparición para no empotrar con el suelo")]
    [SerializeField] private float spawnHeightOffset = 1f;

    [Header("Detección de suelo")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundRayExtra = 0.15f;

    private Rigidbody rb;
    private BoxCollider col;

    private Vector2 moveInput;
    private bool isAccelerating;
    private bool isBraking;

    private bool isGrounded;
    private float currentSpeed;
    private float currentSteer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<BoxCollider>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.centerOfMass = new Vector3(0f, centerOfMassOffsetY, 0f);
        rb.maxAngularVelocity = maxAngularVel;
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        ApplyExtraGravity();

        if (!isGrounded) return;

        UpdateSpeed();
        UpdateSteering();
        ApplyVelocity();
    }

    public void OnMove(InputAction.CallbackContext ctx)
        => moveInput = ctx.ReadValue<Vector2>();

    public void OnAccelerate(InputAction.CallbackContext ctx)
        => isAccelerating = ctx.performed;

    public void OnBrake(InputAction.CallbackContext ctx)
        => isBraking = ctx.performed;

    public void OnReappear(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        Reappear();
    }

    private void Reappear()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[Car] No hay spawn points definidos.");
            return;
        }

        SpawnPoint nearest = spawnPoints[0];
        float minDistance = Vector3.Distance(transform.position, spawnPoints[0].position);

        for (int i = 1; i < spawnPoints.Length; i++)
        {
            float d = Vector3.Distance(transform.position, spawnPoints[i].position);
            if (d < minDistance)
            {
                minDistance = d;
                nearest = spawnPoints[i];
            }
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        currentSpeed = 0f;
        currentSteer = 0f;

        rb.position = nearest.position + Vector3.up * spawnHeightOffset;
        rb.rotation = Quaternion.Euler(nearest.rotation);
    }

    private void UpdateSpeed()
    {
        float target;
        float rate;

        if (isAccelerating && !isBraking)
        {
            target = maxSpeedForward;
            rate = acceleration;
        }
        else if (isBraking)
        {
            if (currentSpeed > 0.05f)
            {
                target = 0f;
                rate = brakeDeceleration;
            }
            else
            {
                target = -maxSpeedReverse;
                rate = acceleration * 0.6f;
            }
        }
        else
        {
            target = 0f;
            rate = naturalDeceleration;
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.fixedDeltaTime);
    }

    private void UpdateSteering()
    {
        float speed = Mathf.Abs(currentSpeed);

        if (speed < minSpeedToTurn)
        {
            currentSteer = Mathf.MoveTowards(currentSteer, 0f, steerSmoothing * Time.fixedDeltaTime);
            return;
        }

        float t = Mathf.InverseLerp(turnReductionStartSpeed, maxSpeedForward, speed);
        float turnFactor = Mathf.Lerp(1f, minTurnFactor, t);
        float targetSteer = moveInput.x * maxTurnSpeed * turnFactor;

        if (currentSpeed < 0f) targetSteer = -targetSteer;

        currentSteer = Mathf.MoveTowards(currentSteer, targetSteer, steerSmoothing * Time.fixedDeltaTime);
    }

    private void ApplyVelocity()
    {
        Vector3 vel = rb.linearVelocity;

        Vector3 forwardVel = transform.forward * currentSpeed;
        Vector3 lateralVel = Vector3.Dot(vel, transform.right) * transform.right * (1f - lateralGrip);
        Vector3 verticalVel = Vector3.Dot(vel, transform.up) * transform.up;

        rb.linearVelocity = forwardVel + lateralVel + verticalVel;

        if (Mathf.Abs(currentSteer) > 0.01f)
        {
            Quaternion deltaRot = Quaternion.AngleAxis(currentSteer * Time.fixedDeltaTime, transform.up);
            rb.MoveRotation(rb.rotation * deltaRot);
        }
    }

    private void ApplyExtraGravity()
    {
        rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
    }

    private void CheckGrounded()
    {
        Bounds b = col.bounds;
        float rayLen = b.extents.y + groundRayExtra;
        Vector3 c = b.center;
        float ex = b.extents.x * 0.85f;
        float ez = b.extents.z * 0.85f;

        isGrounded =
            Cast(c, rayLen) ||
            Cast(c + new Vector3(ex, 0f, ez), rayLen) ||
            Cast(c + new Vector3(-ex, 0f, ez), rayLen) ||
            Cast(c + new Vector3(ex, 0f, -ez), rayLen) ||
            Cast(c + new Vector3(-ex, 0f, -ez), rayLen);
    }

    private bool Cast(Vector3 origin, float length)
        => Physics.Raycast(origin, Vector3.down, length, groundLayer, QueryTriggerInteraction.Ignore);

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (col == null) col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Bounds b = col.bounds;
            float rayLen = b.extents.y + groundRayExtra;
            Vector3 c = b.center;
            float ex = b.extents.x * 0.85f;
            float ez = b.extents.z * 0.85f;

            Gizmos.color = isGrounded ? Color.green : Color.red;
            DrawRay(c, rayLen);
            DrawRay(c + new Vector3(ex, 0f, ez), rayLen);
            DrawRay(c + new Vector3(-ex, 0f, ez), rayLen);
            DrawRay(c + new Vector3(ex, 0f, -ez), rayLen);
            DrawRay(c + new Vector3(-ex, 0f, -ez), rayLen);
        }

        if (spawnPoints == null) return;
        Gizmos.color = Color.cyan;
        foreach (SpawnPoint sp in spawnPoints)
        {
            Vector3 p = sp.position + Vector3.up * spawnHeightOffset;
            Gizmos.DrawSphere(p, 0.3f);
            Gizmos.DrawLine(sp.position, p);
            Gizmos.DrawRay(p, Quaternion.Euler(sp.rotation) * Vector3.forward * 1.5f);
        }
    }

    private void DrawRay(Vector3 o, float l)
    {
        Gizmos.DrawLine(o, o + Vector3.down * l);
        Gizmos.DrawWireSphere(o + Vector3.down * l, 0.05f);
    }
#endif
}
