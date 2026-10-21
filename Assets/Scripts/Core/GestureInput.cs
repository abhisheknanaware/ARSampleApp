using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// One place to read gestures each frame, from touch on a phone or the mouse/keyboard in the editor:
/// tap, one-finger drag, two-finger pinch (scale factor) and twist (degrees).
/// Desktop equivalents: left click = tap, left drag = drag, mouse wheel = pinch, Q / E = twist.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GestureInput : MonoBehaviour
{
    public static GestureInput Instance { get; private set; }

    public float tapMaxMove = 18f;
    public float tapMaxTime = 0.35f;

    public bool Tapped { get; private set; }
    public Vector2 TapPosition { get; private set; }
    public bool DragBegan { get; private set; }
    public bool Dragging { get; private set; }
    public bool DragEnded { get; private set; }
    public Vector2 DragStart { get; private set; }
    public Vector2 DragPosition { get; private set; }
    public float PinchScale { get; private set; } = 1f;
    public float TwistDegrees { get; private set; }

    /// <summary>True when the current gesture started over a UI element (buttons, panels).</summary>
    public bool StartedOverUI { get; private set; }

    Vector2 pressStart;
    float pressTime;
    bool pressed;
    float prevPinchDistance;
    float prevTwistAngle;
    bool twoFinger;

    void Awake() => Instance = this;

    void OnEnable() => EnhancedTouchSupport.Enable();

    void OnDisable()
    {
        if (EnhancedTouchSupport.enabled) EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        Tapped = DragBegan = DragEnded = false;
        PinchScale = 1f;
        TwistDegrees = 0f;

        var touches = Touch.activeTouches;
        if (touches.Count >= 2) TwoFinger(touches[0], touches[1]);
        else
        {
            twoFinger = false;
            if (touches.Count == 1) OnePointer(touches[0].screenPosition, touches[0].phase != UnityEngine.InputSystem.TouchPhase.Ended && touches[0].phase != UnityEngine.InputSystem.TouchPhase.Canceled, touches[0].touchId);
            else if (Mouse.current != null) OnePointer(Mouse.current.position.ReadValue(), Mouse.current.leftButton.isPressed, -1);
        }

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) PinchScale *= 1f + Mathf.Sign(scroll) * 0.08f;
        }
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.isPressed) TwistDegrees += 90f * Time.deltaTime;
            if (Keyboard.current.eKey.isPressed) TwistDegrees -= 90f * Time.deltaTime;
        }
    }

    void OnePointer(Vector2 position, bool down, int pointerId)
    {
        if (down && !pressed)
        {
            pressed = true;
            pressStart = position;
            pressTime = Time.unscaledTime;
            StartedOverUI = OverUI(pointerId);
        }
        else if (down && pressed)
        {
            if (!Dragging && (position - pressStart).magnitude > tapMaxMove && !StartedOverUI)
            {
                Dragging = true;
                DragBegan = true;
                DragStart = pressStart;
            }
            if (Dragging) DragPosition = position;
        }
        else if (!down && pressed)
        {
            pressed = false;
            if (Dragging)
            {
                Dragging = false;
                DragEnded = true;
            }
            else if (!StartedOverUI && Time.unscaledTime - pressTime <= tapMaxTime && (position - pressStart).magnitude <= tapMaxMove)
            {
                Tapped = true;
                TapPosition = position;
            }
        }
    }

    void TwoFinger(Touch a, Touch b)
    {
        if (Dragging)
        {
            Dragging = false;
            DragEnded = true;
        }
        pressed = false;
        Vector2 delta = b.screenPosition - a.screenPosition;
        float distance = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        if (twoFinger && prevPinchDistance > 1f)
        {
            PinchScale = distance / prevPinchDistance;
            TwistDegrees = -Mathf.DeltaAngle(prevTwistAngle, angle);
        }
        twoFinger = true;
        prevPinchDistance = distance;
        prevTwistAngle = angle;
    }

    static bool OverUI(int pointerId)
    {
        EventSystem es = EventSystem.current;
        if (!es) return false;
        return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
    }
}
