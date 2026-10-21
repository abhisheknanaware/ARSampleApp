using System.Collections;
using UnityEngine;

/// <summary>
/// A virtual object placed in the real world. Shows a ring when selected, pops in when placed,
/// and limits gesture scaling to a sensible range.
/// </summary>
public class Placeable : MonoBehaviour
{
    public string displayName = "Object";
    public GameObject selectionRing;
    public float minScale = 0.3f;
    public float maxScale = 3f;

    Vector3 baseScale;
    float scaleFactor = 1f;

    public bool Selected { get; private set; }
    public float ScaleFactor => scaleFactor;

    void Awake()
    {
        baseScale = transform.localScale;
        if (selectionRing) selectionRing.SetActive(false);
    }

    void OnEnable() => StartCoroutine(PopIn());

    IEnumerator PopIn()
    {
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            float k = t / 0.25f;
            transform.localScale = baseScale * scaleFactor * (k + Mathf.Sin(k * Mathf.PI) * 0.2f);
            yield return null;
        }
        transform.localScale = baseScale * scaleFactor;
    }

    public void SetSelected(bool selected)
    {
        Selected = selected;
        if (selectionRing) selectionRing.SetActive(selected);
    }

    public void Scale(float factor)
    {
        scaleFactor = Mathf.Clamp(scaleFactor * factor, minScale, maxScale);
        transform.localScale = baseScale * scaleFactor;
    }

    public void Rotate(float degrees) => transform.Rotate(0f, degrees, 0f, Space.World);

    public void MoveTo(Pose pose, bool keepRotation = true)
    {
        transform.position = pose.position;
        if (!keepRotation) transform.rotation = pose.rotation;
    }
}
