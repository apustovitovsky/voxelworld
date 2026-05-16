using UnityEngine;
using UnityEngine.InputSystem;

public class FlyCamera : MonoBehaviour, InputSystem_Actions.IPlayerActions
{

    /*
    Writen by Windexglow 11-13-10.  Use it, edit it, steal it I don't care.
    Converted to C# 27-02-13 - no credit wanted.
    Simple flycam I made, since I couldn't find any others made public.
    Made simple to use (drag and drop, done) for regular keyboard layout
    wasd : basic movement
    shift : Makes camera accelerate
    space : Moves camera on X and Z axis only.  So camera doesn't gain any height*/


    float mainSpeed = 100.0f; //regular speed
    float shiftAdd = 250.0f; //multiplied by how long shift is held.  Basically running
    float maxShift = 1000.0f; //Maximum speed when holdin gshift
    float camSens = 250.0f; //Scaled for pointer delta action values
    private float totalRun = 1.0f;

    private InputSystem_Actions actions;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool sprintHeld;
    private bool planarMoveHeld;

    void Awake()
    {
        actions = new InputSystem_Actions();
        actions.Player.SetCallbacks(this);
    }

    void OnEnable()
    {
        actions.Player.Enable();
    }

    void OnDisable()
    {
        actions.Player.Disable();
    }

    void OnDestroy()
    {
        actions.Dispose();
    }

    void Update()
    {
        Vector3 eulerAngles = transform.eulerAngles;
        eulerAngles.x += lookInput.y * camSens;
        eulerAngles.y += lookInput.x * camSens;
        eulerAngles.z = 0f;
        transform.eulerAngles = eulerAngles;
        //Mouse  camera angle done.

        //Keyboard commands
        Vector3 p = GetBaseInput();
        if (p.sqrMagnitude > 0)
        { // only move while a direction key is pressed
            if (sprintHeld)
            {
                totalRun += Time.deltaTime;
                p = p * totalRun * shiftAdd;
                p.x = Mathf.Clamp(p.x, -maxShift, maxShift);
                p.y = Mathf.Clamp(p.y, -maxShift, maxShift);
                p.z = Mathf.Clamp(p.z, -maxShift, maxShift);
            }
            else
            {
                totalRun = Mathf.Clamp(totalRun * 0.5f, 1f, 1000f);
                p = p * mainSpeed;
            }

            p = p * Time.deltaTime;
            Vector3 newPosition = transform.position;
            if (planarMoveHeld)
            { //If player wants to move on X and Z axis only
                transform.Translate(p);
                newPosition.x = transform.position.x;
                newPosition.z = transform.position.z;
                transform.position = newPosition;
            }
            else
            {
                transform.Translate(p);
            }
        }
    }

    private Vector3 GetBaseInput()
    { //returns the basic values, if it's 0 than it's not active.
        return new Vector3(moveInput.x, 0f, moveInput.y);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = context.ReadValueAsButton();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        planarMoveHeld = context.ReadValueAsButton();
    }

    public void OnAttack(InputAction.CallbackContext context) { }
    public void OnInteract(InputAction.CallbackContext context) { }
    public void OnCrouch(InputAction.CallbackContext context) { }
    public void OnPrevious(InputAction.CallbackContext context) { }
    public void OnNext(InputAction.CallbackContext context) { }
    public void OnFire(InputAction.CallbackContext context) { }
    public void OnAim(InputAction.CallbackContext context) { }
    public void OnReload(InputAction.CallbackContext context) { }
    public void OnNextWeapon(InputAction.CallbackContext context) { }
}
