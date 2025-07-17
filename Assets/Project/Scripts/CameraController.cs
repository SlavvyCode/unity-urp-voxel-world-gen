using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    
    PlayerInput playerInput;
    CameraControls Actions;
    
    float sprintSpeed = 10f;
    float walkSpeed = 5f;
    float currentSpeed = 5f;
    [SerializeField] private Vector2 pitchMinMax = new Vector2(-80, 80);
    
    private Vector2 rotation = Vector2.zero;
    [SerializeField] private float lookSensitivity = 0.1f;
    
    private Vector3 wasdInput = Vector3.zero;
    private float vertAxisMovement = 0f;


    private void Awake()
    {

        playerInput = GetComponent<PlayerInput>();

        Actions = new CameraControls();    }

    // Start is called before the first frame update
    void Start()
    {
        
        // Bind the camera movement actions
        // Actions.Default.Move.performed += ctx => Move(ctx);
        Actions.Default.Sprint.performed += ctx => currentSpeed = sprintSpeed;
        Actions.Default.Sprint.canceled += ctx => currentSpeed = walkSpeed;
        Actions.Default.MouseLook.performed += ctx => LookCamera(ctx);
        
        
        Actions.Default.Move.performed += ctx => {
            wasdInput = ctx.ReadValue<Vector2>();
        };     
        Actions.Default.Move.canceled += ctx => wasdInput = Vector2.zero;
        
        Actions.Default.MoveUpDown.performed += ctx => {
            //axis for moving up and down - vector1
            float input = ctx.ReadValue<float>();
            vertAxisMovement = input;
        };
        Actions.Default.MoveUpDown.canceled += ctx => vertAxisMovement = 0f;
        
    }

    private void OnEnable()
    {

        Actions.Enable();
        Actions.Default.Enable();     
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Actions.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    //
    // private void Move(InputAction.CallbackContext context)
    // {
    //     Vector3 moveDirection = context.ReadValue<Vector3>();
    //     if (moveDirection.magnitude > 1f)
    //     {
    //         moveDirection.Normalize();
    //     }
    //
    //     Vector3 movement = transform.TransformDirection(moveDirection) * currentSpeed * Time.deltaTime;
    //     transform.position += movement;
    // }

    // Physics-compatible movement
    private void FixedUpdate()
    {
        
        if (wasdInput != Vector3.zero)
        {
            // Rotate the vector to match the camera's orientation
            Vector3 moveDirection = new Vector3(wasdInput.x, 0, wasdInput.y);
            moveDirection = transform.TransformDirection(moveDirection).normalized * currentSpeed * Time.fixedDeltaTime;
            
            transform.position += moveDirection;
            
        }
        if (Mathf.Abs(vertAxisMovement) > 0.1f)
        {
            Vector3 verticalMove = transform.up * vertAxisMovement * currentSpeed * Time.fixedDeltaTime;
            transform.position += verticalMove;
        }
    }
    
    private void LookCamera(InputAction.CallbackContext context)
    {
        Vector2 lookDelta = context.ReadValue<Vector2>();
        
        // Horizontal rotation (around world Y axis)
        rotation.x += lookDelta.x * lookSensitivity;
        
        // Vertical rotation (around local X axis)
        rotation.y = Mathf.Clamp(
            rotation.y - lookDelta.y * lookSensitivity,
            pitchMinMax.x,
            pitchMinMax.y
        );
        
        transform.localRotation = Quaternion.Euler(rotation.y, rotation.x, 0);
    }
}
