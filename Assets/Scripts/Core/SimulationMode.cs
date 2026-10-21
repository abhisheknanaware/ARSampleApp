using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// AR needs an ARCore phone. When no AR provider is running (the editor or a PC) — or the phone does not
/// support ARCore and the user asks for a demo — this switches to a simulated room with a fly camera so
/// every interaction can still be tried and tested.
/// Fly camera: hold right mouse to look, W A S D to move, R / F up and down.
/// </summary>
public class SimulationMode : MonoBehaviour
{
    public static bool Active { get; private set; }
    public static event Action Activated;

    /// <summary>Raised on a phone whose ARCore support check fails.</summary>
    public static event Action Unsupported;

    public GameObject simulatedRoom;
    public Camera arCamera;
    public ARSession arSession;
    public bool activateWhenUnsupported = true;
    public Vector3 startPosition = new Vector3(0f, 1.45f, -1.6f);
    public Vector2 startAngles = new Vector2(28f, 0f);
    public float moveSpeed = 1.6f;
    public float lookSpeed = 0.15f;

    float yaw, pitch;
    bool activated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = false;
        Activated = null;
        Unsupported = null;
    }

    IEnumerator Start()
    {
        if (simulatedRoom) simulatedRoom.SetActive(false);
        if (LoaderUtility.GetActiveLoader() == null)
        {
            Activate();
            yield break;
        }
        yield return ARSession.CheckAvailability();
        if (ARSession.state == ARSessionState.Unsupported)
        {
            Unsupported?.Invoke();
            if (activateWhenUnsupported) Activate();
        }
    }

    public void Activate()
    {
        if (activated) return;
        activated = true;
        Active = true;
        if (arSession) arSession.enabled = false;
        if (simulatedRoom) simulatedRoom.SetActive(true);
        if (arCamera)
        {
            TrackedPoseDriver pose = arCamera.GetComponent<TrackedPoseDriver>();
            if (pose) pose.enabled = false;
            ARCameraBackground background = arCamera.GetComponent<ARCameraBackground>();
            if (background) background.enabled = false;
            arCamera.clearFlags = CameraClearFlags.Skybox;
            arCamera.transform.position = startPosition;
            pitch = startAngles.x;
            yaw = startAngles.y;
            arCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        Activated?.Invoke();
    }

    void OnDestroy()
    {
        if (activated) Active = false;
    }

    void Update()
    {
        if (!activated || !arCamera) return;
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue() * lookSpeed;
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
            arCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        if (kb == null) return;
        Vector3 move = Vector3.zero;
        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        if (kb.wKey.isPressed) move += flat * Vector3.forward;
        if (kb.sKey.isPressed) move -= flat * Vector3.forward;
        if (kb.dKey.isPressed) move += flat * Vector3.right;
        if (kb.aKey.isPressed) move -= flat * Vector3.right;
        if (kb.rKey.isPressed) move += Vector3.up;
        if (kb.fKey.isPressed) move -= Vector3.up;
        arCamera.transform.position += move * moveSpeed * Time.deltaTime;
    }
}
