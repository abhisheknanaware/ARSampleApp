using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// AR Showroom sample app: pick furniture from a catalog and place it in your room, move / rotate / delete it,
/// measure real distances with an AR tape measure, and save screenshots.
/// </summary>
public class ShowroomApp : MonoBehaviour
{
    public enum Mode { Place, Measure }

    [Header("AR")]
    public SurfaceRaycaster surfaces;
    public Transform reticle;

    [Header("Catalog")]
    public Placeable[] catalog;
    public Image[] catalogButtons;

    [Header("Measure")]
    public LineRenderer measureLinePrefab;
    public Transform measureMarkerPrefab;
    public TextMeshPro measureLabelPrefab;

    [Header("UI")]
    public Canvas canvas;
    public TMP_Text hintText;
    public TMP_Text toastText;
    public TMP_Text modeButtonLabel;
    public GameObject onboardingPanel;
    public GameObject unsupportedPanel;
    public GameObject selectionBar;
    public TMP_Text selectionLabel;

    static readonly Color Normal = new Color(1f, 1f, 1f, 0.92f);
    static readonly Color Highlight = new Color(1f, 0.78f, 0.25f, 1f);

    readonly List<Placeable> placed = new List<Placeable>();
    readonly List<GameObject> measureObjects = new List<GameObject>();
    Placeable selected;
    Placeable dragging;
    int catalogIndex;
    Mode mode = Mode.Place;
    bool hasPointA;
    Vector3 pointA;
    LineRenderer liveLine;
    TextMeshPro liveLabel;
    Coroutine toastRoutine;
    bool anySurfaceSeen;

    public IReadOnlyList<Placeable> Placed => placed;
    public Placeable Selected => selected;
    public Mode CurrentMode => mode;
    public float LastDistance { get; private set; } = -1f;
    public string LastScreenshotPath { get; private set; }

    void OnEnable()
    {
        SimulationMode.Unsupported += ShowUnsupported;
    }

    void OnDisable()
    {
        SimulationMode.Unsupported -= ShowUnsupported;
    }

    void Start()
    {
        if (unsupportedPanel) unsupportedPanel.SetActive(false);
        if (toastText) toastText.gameObject.SetActive(false);
        SelectCatalog(0);
        SetMode(Mode.Place);
        UpdateSelectionBar();
    }

    void Update()
    {
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        bool surface = surfaces.TryHit(center, out Pose pose, out _);
        if (surface) anySurfaceSeen = true;
        if (onboardingPanel) onboardingPanel.SetActive(!anySurfaceSeen && !(unsupportedPanel && unsupportedPanel.activeSelf));
        if (reticle)
        {
            reticle.gameObject.SetActive(surface);
            if (surface) reticle.SetPositionAndRotation(pose.position + pose.up * 0.012f, pose.rotation);
        }
        if (hasPointA && liveLine && surface) UpdateMeasurement(liveLine, liveLabel, pointA, pose.position);

        GestureInput g = GestureInput.Instance;
        if (g)
        {
            if (g.Tapped) HandleTap(g.TapPosition);
            if (mode == Mode.Place)
            {
                if (g.DragBegan) BeginDrag(g.DragStart);
                if (g.Dragging) DragTo(g.DragPosition);
                if (g.DragEnded) dragging = null;
                if (selected && Mathf.Abs(g.TwistDegrees) > 0.01f) selected.Rotate(g.TwistDegrees);
            }
        }
        UpdateHint(surface);
    }

    // ---------- Placing furniture ----------

    public void HandleTap(Vector2 screenPoint)
    {
        if (mode == Mode.Measure)
        {
            MeasureTap(screenPoint);
            return;
        }
        Placeable hit = surfaces.PickObject<Placeable>(screenPoint);
        if (hit)
        {
            Select(hit == selected ? null : hit);
            return;
        }
        if (surfaces.TryHit(screenPoint, out Pose pose, out bool vertical))
        {
            if (vertical)
            {
                Toast("Furniture goes on the floor - aim at a horizontal surface");
                return;
            }
            Place(pose);
        }
        else Select(null);
    }

    public Placeable Place(Pose pose)
    {
        Placeable prefab = catalog[catalogIndex];
        Vector3 toCamera = surfaces.arCamera.transform.position - pose.position;
        toCamera.y = 0f;
        Quaternion facing = toCamera.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toCamera.normalized) : Quaternion.identity;
        Placeable p = Instantiate(prefab, pose.position, facing);
        p.name = prefab.displayName;
        if (!SimulationMode.Active) p.gameObject.AddComponent<ARAnchor>();
        placed.Add(p);
        Select(p);
        Toast($"{prefab.displayName} placed");
        return p;
    }

    public void Select(Placeable p)
    {
        if (selected) selected.SetSelected(false);
        selected = p;
        if (selected) selected.SetSelected(true);
        UpdateSelectionBar();
    }

    void BeginDrag(Vector2 screenPoint)
    {
        Placeable hit = surfaces.PickObject<Placeable>(screenPoint);
        if (!hit) return;
        Select(hit);
        dragging = hit;
    }

    public void DragTo(Vector2 screenPoint)
    {
        if (dragging && surfaces.TryHit(screenPoint, out Pose pose, out bool vertical) && !vertical) dragging.MoveTo(pose);
    }

    public void SelectCatalog(int index)
    {
        catalogIndex = Mathf.Clamp(index, 0, catalog.Length - 1);
        for (int i = 0; i < catalogButtons.Length; i++)
            if (catalogButtons[i]) catalogButtons[i].color = i == catalogIndex ? Highlight : Normal;
        if (mode == Mode.Measure) SetMode(Mode.Place);
    }

    public void RotateSelected()
    {
        if (selected) selected.Rotate(45f);
    }

    public void DeleteSelected()
    {
        if (!selected) return;
        placed.Remove(selected);
        Destroy(selected.gameObject);
        selected = null;
        UpdateSelectionBar();
    }

    void UpdateSelectionBar()
    {
        if (selectionBar) selectionBar.SetActive(selected);
        if (selectionLabel && selected) selectionLabel.text = selected.displayName;
    }

    // ---------- Measuring ----------

    public void ToggleMeasure() => SetMode(mode == Mode.Measure ? Mode.Place : Mode.Measure);

    public void SetMode(Mode newMode)
    {
        mode = newMode;
        CancelLiveMeasurement();
        if (newMode == Mode.Measure) Select(null);
        if (modeButtonLabel) modeButtonLabel.text = mode == Mode.Measure ? "Done" : "Measure";
    }

    public void MeasureTap(Vector2 screenPoint)
    {
        if (!surfaces.TryHit(screenPoint, out Pose pose, out _)) return;
        if (!hasPointA)
        {
            hasPointA = true;
            pointA = pose.position;
            measureObjects.Add(Instantiate(measureMarkerPrefab, pointA, Quaternion.identity).gameObject);
            liveLine = Instantiate(measureLinePrefab);
            liveLabel = Instantiate(measureLabelPrefab);
            measureObjects.Add(liveLine.gameObject);
            measureObjects.Add(liveLabel.gameObject);
            UpdateMeasurement(liveLine, liveLabel, pointA, pointA);
            return;
        }
        Vector3 pointB = pose.position;
        measureObjects.Add(Instantiate(measureMarkerPrefab, pointB, Quaternion.identity).gameObject);
        LastDistance = UpdateMeasurement(liveLine, liveLabel, pointA, pointB);
        Toast($"Measured {FormatDistance(LastDistance)}");
        hasPointA = false;
        liveLine = null;
        liveLabel = null;
    }

    float UpdateMeasurement(LineRenderer line, TextMeshPro label, Vector3 a, Vector3 b)
    {
        float distance = Vector3.Distance(a, b);
        line.SetPosition(0, a + Vector3.up * 0.005f);
        line.SetPosition(1, b + Vector3.up * 0.005f);
        label.text = FormatDistance(distance);
        label.transform.position = (a + b) * 0.5f + Vector3.up * 0.06f;
        Vector3 look = label.transform.position - surfaces.arCamera.transform.position;
        if (look.sqrMagnitude > 1e-4f) label.transform.rotation = Quaternion.LookRotation(look);
        return distance;
    }

    public static string FormatDistance(float meters) => meters >= 1f ? $"{meters:0.00} m" : $"{meters * 100f:0} cm";

    void CancelLiveMeasurement()
    {
        if (!hasPointA) return;
        hasPointA = false;
        if (liveLine) Destroy(liveLine.gameObject);
        if (liveLabel) Destroy(liveLabel.gameObject);
    }

    public void ClearMeasurements()
    {
        CancelLiveMeasurement();
        foreach (GameObject go in measureObjects) if (go) Destroy(go);
        measureObjects.Clear();
        LastDistance = -1f;
    }

    public void ClearAll()
    {
        ClearMeasurements();
        foreach (Placeable p in placed) if (p) Destroy(p.gameObject);
        placed.Clear();
        Select(null);
        Toast("Room cleared");
    }

    // ---------- Screenshot / messages ----------

    public void TakeScreenshot() => StartCoroutine(Capture());

    /// <summary>Renders the AR camera (camera image + furniture, without the UI) to a PNG in the app's storage.</summary>
    IEnumerator Capture()
    {
        bool ringWasOn = selected && selected.selectionRing && selected.selectionRing.activeSelf;
        if (ringWasOn) selected.selectionRing.SetActive(false);
        yield return null;
        Camera cam = surfaces.arCamera;
        int w = Mathf.Max(Screen.width, 2), h = Mathf.Max(Screen.height, 2);
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = previous;
        RenderTexture.active = rt;
        Texture2D shot = new Texture2D(w, h, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        shot.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        if (ringWasOn) selected.selectionRing.SetActive(true);
        string file = Path.Combine(Application.persistentDataPath, $"ARShowroom_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        File.WriteAllBytes(file, shot.EncodeToPNG());
        Destroy(shot);
        LastScreenshotPath = file;
        Toast("Screenshot saved");
    }

    public void StartDemoMode()
    {
        if (unsupportedPanel) unsupportedPanel.SetActive(false);
        SimulationMode sim = FindAnyObjectByType<SimulationMode>();
        if (sim) sim.Activate();
    }

    void ShowUnsupported()
    {
        if (unsupportedPanel) unsupportedPanel.SetActive(true);
    }

    void UpdateHint(bool surface)
    {
        if (!hintText) return;
        if (mode == Mode.Measure)
            hintText.text = hasPointA ? "Tap the end point" : "Tap the start point to measure";
        else if (!surface && placed.Count == 0)
            hintText.text = SimulationMode.Active ? "Look at the floor (right mouse: look, WASD: move)" : "Point at the floor and move slowly";
        else if (selected)
            hintText.text = SimulationMode.Active ? "Drag to move  |  Q / E to rotate" : "Drag to move  |  Twist with two fingers to rotate";
        else
            hintText.text = SimulationMode.Active
                ? $"Click the floor to place the {catalog[catalogIndex].displayName}\n<size=80%>Right mouse: look   W A S D: move</size>"
                : $"Tap the floor to place the {catalog[catalogIndex].displayName}";
    }

    public void Toast(string message)
    {
        if (!toastText) return;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(message));
    }

    IEnumerator ToastRoutine(string message)
    {
        toastText.text = message;
        toastText.gameObject.SetActive(true);
        yield return new WaitForSeconds(1.8f);
        toastText.gameObject.SetActive(false);
    }
}
