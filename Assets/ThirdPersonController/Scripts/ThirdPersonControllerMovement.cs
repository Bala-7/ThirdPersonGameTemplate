using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Playables;
using UnityEngine.Serialization;

public class ThirdPersonControllerMovement : MonoBehaviour
{
    public static ThirdPersonControllerMovement s;
    
    private Rigidbody _rb;
    
    #region Camera
    private Camera _cam;
    private ThirdPersonCameraMovement _cm;
    private Vector3 camFwd;
    #endregion

    #region Movement
    private bool movementEnabled = true;
    [FormerlySerializedAs("walk_speed")] [Range(1.0f, 10.0f)]
    public float walkSpeed;
    [FormerlySerializedAs("run_speed_multiplier")] [Range(1.0f, 2.0f)]
    private float runSpeedMultiplier = 1.0f;
    [FormerlySerializedAs("crouch_walk_speed_multiplier")] [Range(0.0f, 1.0f)]
    private float crouchWalkSpeedMultiplier;
    [FormerlySerializedAs("crouch_run_speed_multiplier")] [Range(0.0f, 1.0f)]
    private float crouchRunSpeedMultiplier;

    [Range(1.0f, 10.0f)]
    public float backwards_walk_speed;
    [Range(1.0f, 10.0f)]
    public float strafe_speed;

    [FormerlySerializedAs("rotation_speed")] [Range(0.1f, 1.5f)]
    public float rotationSpeed;

    [FormerlySerializedAs("jump_force")] [Range(2.0f, 10.0f)]
    private float jumpForce;

    private Vector3 move = Vector3.zero;
    #endregion

    #region Animations
    private MyTPCharacter tpc;
    private float animFreeLookBlend = 0f;
    private float animLockViewBlendX = 0f;
    private float animLockViewBlendY = 0f;
    #endregion

    #region Input
    private float hInput;
    private float vInput;

    private bool run = false;
    #endregion


    #region States
    private enum PLAYER_STATES { FREE = 0 };
    private PLAYER_STATES _state;
    #endregion


    private Vector3 initialPosition;
    private Quaternion initialRotation;
    
    private void Awake()
    {
        s = this;
        //InitializeModelInfo();
        _cm = GetComponent<ThirdPersonCameraMovement>();
        _cam = _cm.GetCamera();
        _rb = GetComponent<Rigidbody>();
        

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if(!_cm.lookAt)
            _cm.lookAt = transform;
        //InitializeAnimator();
    }

    public void InitializeModelInfo() {
        tpc = FindObjectOfType<MyTPCharacter>();
        
        InitializeAnimator();
    }

    private void InitializeAnimator()
    {
        tpc.AssignAnimator();
        if (_cm.type == ThirdPersonCameraMovement.CAMERA_TYPE.FREE_LOOK)
        {
            tpc.GetFullBodyAnimator().Play("FreeLookBlendTree");
        }
        else
        {
            tpc.GetFullBodyAnimator().Play("LockViewBlendTree");
        }
        

    }

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _cm.SetCameraToOrigin();
    }

    // Update is called once per frame
    void Update()
    {
        PlayerStateMachine();
    }

    private void FixedUpdate()
    {

    }

    private void MovePlayer() {
        // Gets the input
        hInput = Input.GetAxis("Horizontal");
        vInput = Input.GetAxis("Vertical");
        //crouch = (Input.GetButtonDown("Crouch")) ? !crouch : crouch;
        //run = Input.GetButton("Run");
        
        // Calculate camera relative directions to move:
        camFwd = Vector3.Scale(_cam.transform.forward, new Vector3(1, 1, 1)).normalized;
        Vector3 camFlatFwd = Vector3.Scale(_cam.transform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 flatRight = new Vector3(_cam.transform.right.x, 0, _cam.transform.right.z);

        Vector3 m_CharForward = Vector3.Scale(camFlatFwd, new Vector3(1, 0, 1)).normalized;
        Vector3 m_CharRight = Vector3.Scale(flatRight, new Vector3(1, 0, 1)).normalized;


        // Draws a ray to show the direction the player is aiming at
        //Debug.DrawLine(transform.position, transform.position + camFwd * 5f, Color.red);

        // Move the player (movement will be slightly different depending on the camera type)
        float w_speed = 0;
        if (_cm.type == ThirdPersonCameraMovement.CAMERA_TYPE.FREE_LOOK)
        {
            w_speed = GetPlayerMovementSpeed();
            move = (vInput * m_CharForward + hInput * m_CharRight).normalized * w_speed;
            _cam.transform.position += move * Time.deltaTime;
            
            // Rotate body
            tpc.transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(tpc.transform.forward, move, rotationSpeed, 0.0f));
        }
        else if (_cm.type == ThirdPersonCameraMovement.CAMERA_TYPE.LOCKED)
        {
            w_speed = (vInput > 0) ? walkSpeed : backwards_walk_speed;
            move = (vInput * m_CharForward + hInput * m_CharRight).normalized * ((hInput != 0) ? strafe_speed : w_speed);

            Vector3 camEuler = _cam.transform.eulerAngles;
            tpc.transform.eulerAngles = Vector3.Scale(_cam.transform.eulerAngles, Vector3.up);

        }

        transform.position += move * Time.deltaTime;    // Move the actual player
        
    }

    private void AnimatePlayer() {

        if (_cm.type == ThirdPersonCameraMovement.CAMERA_TYPE.FREE_LOOK) {
            //SelectCorrectAnimator();
            UpdateFreeLookBlend();
            tpc.GetFullBodyAnimator().SetFloat("Blend", animFreeLookBlend);
        }
        else {
            UpdateLockViewBlend();
            tpc.GetFullBodyAnimator().SetFloat("HInput", animLockViewBlendX);
            tpc.GetFullBodyAnimator().SetFloat("VInput", animLockViewBlendY);
        }
    }

    private void SelectCorrectAnimator() {
        Animator anim = tpc.GetFullBodyAnimator();
        if(run && !anim.GetCurrentAnimatorStateInfo(0).IsName("FreeLookBlendTreeJog")) tpc.GetFullBodyAnimator().Play("FreeLookBlendTreeJog");
        else if(!run && !anim.GetCurrentAnimatorStateInfo(0).IsName("FreeLookBlendTree")) tpc.GetFullBodyAnimator().Play("FreeLookBlendTree");
    }

    private void UpdateFreeLookBlend() {
        float movement = Mathf.Clamp01(move.magnitude);
        float t = 2.5f;

        int blendDirection = GetBlendDirection(animFreeLookBlend, movement);
        animFreeLookBlend = Mathf.Clamp01(animFreeLookBlend + t * blendDirection * Time.deltaTime);

    }

    private void UpdateLockViewBlend() {
        float t = 2f;
        int blendDirectionX = GetBlendDirection(animLockViewBlendX, Mathf.Clamp(hInput, -1, 1));
        int blendDirectionY = GetBlendDirection(animLockViewBlendY, Mathf.Clamp(vInput, -1, 1));
        
        animLockViewBlendX = Mathf.Clamp(animLockViewBlendX + t * blendDirectionX * Time.deltaTime, -1, 1);
        animLockViewBlendY = Mathf.Clamp(animLockViewBlendY + t * blendDirectionY * Time.deltaTime, -1, 1);
    }

    private int GetBlendDirection(float animValue, float inputValue) {
        if (animValue > inputValue) return -1;
        else if (animValue == inputValue) return 0;
        else return 1;
    }

    void PlayerStateMachine() {
        switch (_state) {
            case PLAYER_STATES.FREE:
                if (movementEnabled)
                    MovePlayer();
                AnimatePlayer();
                break;
            
            default: break;
        }
        CheckStateChange();
    }

    private void CheckStateChange() {
        switch (_state)
        {
            case PLAYER_STATES.FREE: break;
            default: break;
        }
    }

    private float GetPlayerMovementSpeed() {
        float result = walkSpeed;

        if (run) result *= runSpeedMultiplier;

        return result;
    }


    public void EnableMovement() { movementEnabled = true; }
    
    public void DisableMovement() { movementEnabled = false; }


    public void Restart()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;
    }

}
