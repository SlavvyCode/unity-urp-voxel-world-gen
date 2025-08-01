using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class CameraController : MonoBehaviour
{
    [Header("Main Camera Controls")]
    [SerializeField] private Vector2 pitchMinMax = new Vector2(-80, 80);
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float walkSpeed = 10f;
    [SerializeField] private float sprintSpeed = 30f;
    
    [Header("Debug Camera")]
    [SerializeField] private Camera debugCamera;
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F3;
    [SerializeField] private float debugMoveSpeed = 15f;
    [SerializeField] private GUIStyle debugTextStyle;

    private Camera mainCamera;
    private CameraControls actions;
    private float currentSpeed;
    private Vector2 rotation;
    private Vector3 wasdInput;
    private float vertAxisMovement;
    private bool debugMode = false;
    private string debugStatusText = "";
    [SerializeField] private TextMeshProUGUI debugStatusTextUI;

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
        actions = new CameraControls();
        currentSpeed = walkSpeed;
        
        if (!debugCamera)
        {
            debugCamera = GetComponentInChildren<Camera>(true);
            if (debugCamera) 
            {
                debugCamera.gameObject.SetActive(false);
                debugCamera.depth = mainCamera.depth + 1; // Render on top
            }
        }

        // Setup debug text style if null
        if (debugTextStyle == null)
        {
            debugTextStyle = new GUIStyle();
            debugTextStyle.fontSize = 24;
            debugTextStyle.normal.textColor = Color.red;
        }
    }

    private void Start()
    {
        
        HideCursor();
        
        //disable debug
        if (debugCamera)
            debugCamera.gameObject.SetActive(false);
        debugMode = false;
        
        
        actions.Default.Sprint.performed += ctx => currentSpeed = debugMode ? debugMoveSpeed : sprintSpeed;
        actions.Default.Sprint.canceled += ctx => currentSpeed = debugMode ? debugMoveSpeed : walkSpeed;
        actions.Default.MouseLook.performed += ctx => LookCamera(ctx);
        
        actions.Default.Move.performed += ctx => wasdInput = ctx.ReadValue<Vector2>();
        actions.Default.Move.canceled += ctx => wasdInput = Vector2.zero;
        
        actions.Default.MoveUpDown.performed += ctx => vertAxisMovement = ctx.ReadValue<float>();
        actions.Default.MoveUpDown.canceled += ctx => vertAxisMovement = 0f;
    }

    private void Update()
    {
        HandleDebugToggle();
    }

    private void OnGUI()
    {
        if (debugMode)
        {
            GUI.Label(new Rect(10, 10, 300, 50), "DEBUG CAMERA ACTIVE", debugTextStyle);
        }
    }

    private void FixedUpdate()
    {
        if (debugMode)
        {
            HandleDebugMovement();
        }
        else
        {
            HandlePlayerMovement();
        }
    }

    private void HandleDebugToggle()
    {
        if (Input.GetKeyDown(debugToggleKey))
        {
            debugMode = !debugMode;
            
            debugStatusText = debugMode ? "DEBUG CAMERA ACTIVE" : "";
            
            debugCamera.gameObject.SetActive(debugMode);
            mainCamera.enabled = !debugMode;
            
            if (debugMode)
            {
                // Sync position but maintain separate rotation
                debugCamera.transform.position = transform.position;
                debugCamera.transform.rotation = transform.rotation;
            }
            else
            {
                HideCursor();
            }
        }
    }

    private static void HideCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandlePlayerMovement()
    {
        if (wasdInput != Vector3.zero)
        {
            Vector3 moveDirection = new Vector3(wasdInput.x, 0, wasdInput.y);
            moveDirection = transform.TransformDirection(moveDirection).normalized * currentSpeed * Time.fixedDeltaTime;
            transform.position += moveDirection;
        }
        
        if (Mathf.Abs(vertAxisMovement) > 0.1f)
        {
            Vector3 verticalMove = Vector3.up * vertAxisMovement * currentSpeed * Time.fixedDeltaTime;
            transform.position += verticalMove;
        }
    }

    private void HandleDebugMovement()
    {
        Vector2 lookDelta = Mouse.current.delta.ReadValue();
        rotation.x += lookDelta.x * lookSensitivity;
        rotation.y = Mathf.Clamp(
            rotation.y - lookDelta.y * lookSensitivity,
            pitchMinMax.x,
            pitchMinMax.y
        );
        debugCamera.transform.localRotation = Quaternion.Euler(rotation.y, rotation.x, 0);

        // Movement relative to debug camera's orientation
        Vector3 move = debugCamera.transform.TransformDirection(
            new Vector3(wasdInput.x, vertAxisMovement, wasdInput.y)
        ) * currentSpeed * Time.fixedDeltaTime;
        
        debugCamera.transform.position += move;
    }

    private void LookCamera(InputAction.CallbackContext context)
    {
        Vector2  lookDelta = context.ReadValue<Vector2>();
      
        rotation.x += lookDelta.x * lookSensitivity;
        rotation.y = Mathf.Clamp(
            rotation.y - lookDelta.y * lookSensitivity,
            pitchMinMax.x,
            pitchMinMax.y
        );
        transform.localRotation = Quaternion.Euler(rotation.y, rotation.x, 0);
        debugCamera.transform.localRotation = Quaternion.Euler(rotation.y, rotation.x, 0);
    }

    private void OnEnable() => actions.Enable();
    private void OnDisable() => actions.Disable();
}