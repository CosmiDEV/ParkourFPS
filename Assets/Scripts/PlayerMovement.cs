using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimiento en primera persona estilo arcade.
/// Incluye: correr con aceleración, salto con "coyote time" y "jump buffer",
/// salto variable (más alto si mantienes el botón), doble salto,
/// deslizamiento en paredes y salto en paredes.
/// Requiere un CharacterController en el mismo objeto.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Correr")]
    [Tooltip("Velocidad máxima en el suelo (metros por segundo).")]
    public float maxSpeed = 12f;
    [Tooltip("Qué tan rápido aceleras en el suelo.")]
    public float groundAcceleration = 70f;
    [Tooltip("Qué tan rápido frenas en el suelo al soltar las teclas.")]
    public float groundDeceleration = 60f;
    [Tooltip("Control en el aire: cuánto puedes empujarte hacia donde apuntas.")]
    public float airAcceleration = 25f;
    [Tooltip("Frenado en el aire cuando no presionas nada (bajo = conservas el impulso).")]
    public float airDeceleration = 3f;

    [Header("Salto")]
    [Tooltip("Altura del salto en metros.")]
    public float jumpHeight = 2.2f;
    [Tooltip("Gravedad (valor positivo).")]
    public float gravity = 25f;
    [Tooltip("Multiplicador de gravedad al caer (más alto = caes más rápido).")]
    public float fallMultiplier = 1.6f;
    [Tooltip("Multiplicador si sueltas el botón mientras subes (salto corto).")]
    public float lowJumpMultiplier = 2f;
    public float maxFallSpeed = 40f;
    [Tooltip("Tiempo extra para saltar justo después de dejar una plataforma.")]
    public float coyoteTime = 0.12f;
    [Tooltip("Si presionas saltar un poco antes de tocar el suelo, el salto se guarda.")]
    public float jumpBufferTime = 0.12f;

    [Header("Doble salto")]
    public int maxAirJumps = 1;
    [Range(0.5f, 1.2f)]
    [Tooltip("Fuerza del salto en el aire respecto al salto normal.")]
    public float airJumpMultiplier = 0.9f;

    [Header("Paredes")]
    [Tooltip("Qué capas cuentan como pared. Por defecto, todas.")]
    public LayerMask wallMask = ~0;
    [Tooltip("Distancia extra (sobre el radio del jugador) para detectar una pared.")]
    public float wallCheckDistance = 0.5f;
    [Tooltip("Velocidad hacia arriba del salto en pared.")]
    public float wallJumpUpSpeed = 9.5f;
    [Tooltip("Velocidad con la que te empuja lejos de la pared.")]
    public float wallJumpPushSpeed = 9f;
    [Range(0f, 1f)]
    [Tooltip("Cuánta velocidad lateral (a lo largo de la pared) conservas al saltar.")]
    public float wallJumpKeepSpeed = 0.7f;
    [Tooltip("Tiempo mínimo entre dos saltos en pared.")]
    public float wallJumpCooldown = 0.2f;
    [Tooltip("Velocidad máxima de caída mientras estás pegado a una pared.")]
    public float wallSlideSpeed = 3f;
    [Tooltip("Si está activo, el salto en pared te devuelve el doble salto.")]
    public bool wallJumpResetsAirJumps = true;
    [Tooltip("Margen (m) bajo los pies para confirmar que pisas suelo real. Sube si se siente que 'no detecta' el suelo en bordes.")]
    public float groundProbeDistance = 0.2f;
    [Tooltip("Tiempo mínimo en el aire antes de poder pegarte a una pared (evita falsos positivos por parpadeos del suelo).")]
    public float wallMinAirTime = 0.12f;
    [Tooltip("Altura mínima sobre el suelo (bajo tus pies) para poder pegarte a una pared.")]
    public float wallMinHeight = 0.6f;

    // --- Estado interno ---
    CharacterController controller;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    bool grounded;
    float coyoteCounter;
    float jumpBufferCounter;
    float wallJumpTimer;
    float airTime;
    int airJumpsUsed;

    Vector2 moveInput;
    bool jumpPressed;
    bool jumpHeld;

    bool onWall;
    Vector3 wallNormal;
    float wallSide; // +1 pared a la derecha, -1 a la izquierda, 0 delante/detrás

    // --- Datos para otros scripts (cámara, HUD...) ---
    public float HorizontalSpeed => horizontalVelocity.magnitude;
    public bool IsGrounded => grounded;
    public bool IsOnWallAirborne => onWall && !grounded;
    public float WallSide => wallSide;
    public Vector2 MoveInput => moveInput;
    public int AirJumpsLeft => Mathf.Max(0, maxAirJumps - airJumpsUsed);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // isGrounded también da "true" si la cápsula solo roza el borde de una plataforma en el aire.
        // Por eso lo confirmamos con rayos hacia abajo y descartamos mientras subes.
        grounded = controller.isGrounded && verticalVelocity <= 0.5f && HasGroundBelow();
        ReadInput();

        // 1) Estado del suelo
        airTime = grounded ? 0f : airTime + dt;
        if (grounded)
        {
            coyoteCounter = coyoteTime;
            airJumpsUsed = 0;
            if (verticalVelocity < 0f) verticalVelocity = -2f; // mantenerse pegado al suelo
        }
        else
        {
            coyoteCounter -= dt;
        }

        // 2) Paredes
        DetectWall();

        // 3) Saltos
        HandleJumps(dt);

        // 4) Movimiento horizontal
        HandleHorizontal(dt);

        // 5) Gravedad
        HandleGravity(dt);

        // 6) Mover
        Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
        CollisionFlags flags = controller.Move(motion * dt);

        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f; // golpe contra el techo
    }

    // ------------------------------------------------------------------
    // Confirma que hay suelo real bajo los pies (5 rayos: centro + 4 alrededor).
    // Un roce con el borde de una plataforma no cuenta como suelo.
    bool HasGroundBelow()
    {
        Vector3 c = transform.position + controller.center;
        float dist = controller.height * 0.5f + controller.skinWidth + groundProbeDistance;
        float r = controller.radius * 0.6f;

        for (int i = 0; i < 5; i++)
        {
            Vector3 offset = Vector3.zero;
            if (i == 1) offset = transform.right * r;
            else if (i == 2) offset = -transform.right * r;
            else if (i == 3) offset = transform.forward * r;
            else if (i == 4) offset = -transform.forward * r;

            if (Physics.Raycast(c + offset, Vector3.down, out RaycastHit hit, dist,
                                ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.5f)
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    void ReadInput()
    {
        moveInput = Vector2.zero;
        jumpPressed = false;
        jumpHeld = false;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.wKey.isPressed) moveInput.y += 1f;
        if (kb.sKey.isPressed) moveInput.y -= 1f;
        if (kb.dKey.isPressed) moveInput.x += 1f;
        if (kb.aKey.isPressed) moveInput.x -= 1f;
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);

        jumpPressed = kb.spaceKey.wasPressedThisFrame;
        jumpHeld = kb.spaceKey.isPressed;
    }

    // ------------------------------------------------------------------
    void DetectWall()
    {
        onWall = false;
        wallSide = 0f;
        if (grounded) return;

        // controller.isGrounded parpadea a veces (1 frame en "false" aunque pises el suelo).
        // Para no activar el modo pared por error, exigimos llevar un rato en el aire
        // y estar a cierta altura sobre el suelo.
        if (airTime < wallMinAirTime) return;
        float probe = controller.height * 0.5f + wallMinHeight;
        if (Physics.Raycast(transform.position, Vector3.down, probe, wallMask, QueryTriggerInteraction.Ignore))
            return;

        float dist = controller.radius + wallCheckDistance;
        float best = float.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = GetDirection(i);
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, dist,
                                wallMask, QueryTriggerInteraction.Ignore))
            {
                // Solo superficies casi verticales cuentan como pared
                if (Mathf.Abs(hit.normal.y) > 0.3f) continue;

                if (hit.distance < best)
                {
                    best = hit.distance;
                    wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                    onWall = true;
                }
            }
        }

        if (onWall)
        {
            float d = Vector3.Dot(-wallNormal, transform.right);
            wallSide = Mathf.Abs(d) > 0.3f ? Mathf.Sign(d) : 0f;
        }
    }

    Vector3 GetDirection(int i)
    {
        switch (i)
        {
            case 0: return transform.right;
            case 1: return -transform.right;
            case 2: return transform.forward;
            default: return -transform.forward;
        }
    }

    // ------------------------------------------------------------------
    void HandleJumps(float dt)
    {
        if (jumpPressed) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= dt;

        wallJumpTimer -= dt;

        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            // Salto normal (suelo o recién dejado el borde)
            verticalVelocity = JumpVelocity();
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }
        else if (jumpBufferCounter > 0f && onWall && wallJumpTimer <= 0f)
        {
            // Salto en pared
            Vector3 along = Vector3.ProjectOnPlane(horizontalVelocity, wallNormal);
            horizontalVelocity = along * wallJumpKeepSpeed + wallNormal * wallJumpPushSpeed;
            verticalVelocity = wallJumpUpSpeed;
            wallJumpTimer = wallJumpCooldown;
            jumpBufferCounter = 0f;
            onWall = false;
            if (wallJumpResetsAirJumps) airJumpsUsed = 0;
        }
        else if (jumpPressed && !grounded && airJumpsUsed < maxAirJumps)
        {
            // Doble salto (solo con una pulsación nueva)
            verticalVelocity = JumpVelocity() * airJumpMultiplier;
            airJumpsUsed++;
            jumpBufferCounter = 0f;
        }
    }

    float JumpVelocity()
    {
        return Mathf.Sqrt(2f * gravity * jumpHeight);
    }

    // ------------------------------------------------------------------
    void HandleHorizontal(float dt)
    {
        Vector3 wish = transform.right * moveInput.x + transform.forward * moveInput.y;
        bool hasInput = wish.sqrMagnitude > 0.001f;

        if (grounded)
        {
            Vector3 target = wish * maxSpeed;
            float accel = hasInput ? groundAcceleration : groundDeceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, accel * dt);
        }
        else
        {
            if (hasInput)
            {
                // Control aéreo: empujas hacia donde apuntas sin perder el impulso que ya llevas
                Vector3 wishDir = wish.normalized;
                float current = Vector3.Dot(horizontalVelocity, wishDir);
                float add = Mathf.Min(airAcceleration * dt, Mathf.Max(0f, maxSpeed - current));
                horizontalVelocity += wishDir * add;
            }
            else
            {
                horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, airDeceleration * dt);
            }
        }
    }

    // ------------------------------------------------------------------
    void HandleGravity(float dt)
    {
        float g = gravity;
        if (verticalVelocity < 0f) g *= fallMultiplier;
        else if (verticalVelocity > 0f && !jumpHeld) g *= lowJumpMultiplier;

        verticalVelocity -= g * dt;
        verticalVelocity = Mathf.Max(verticalVelocity, -maxFallSpeed);

        // Deslizamiento lento por la pared
        if (onWall && !grounded && verticalVelocity < -wallSlideSpeed)
            verticalVelocity = -wallSlideSpeed;
    }

    // ------------------------------------------------------------------
    // Al chocar de lado con una pared, quitamos la velocidad que empuja contra ella
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (Mathf.Abs(hit.normal.y) < 0.3f)
        {
            Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            float into = Vector3.Dot(horizontalVelocity, n);
            if (into < 0f) horizontalVelocity -= n * into;
        }
    }

    /// <summary>Detiene todo el movimiento (lo usa el reaparecer).</summary>
    public void ResetState()
    {
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        airJumpsUsed = 0;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        wallJumpTimer = 0f;
        airTime = 0f;
    }
}
