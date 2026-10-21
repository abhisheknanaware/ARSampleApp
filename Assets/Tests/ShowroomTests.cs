using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ShowroomTests
{
    ShowroomApp app;
    Camera cam;

    [UnitySetUp]
    public IEnumerator Load()
    {
        LogAssert.ignoreFailingMessages = true;
        yield return SceneManager.LoadSceneAsync("ARShowroom");
        yield return null;
        yield return null;
        app = Object.FindAnyObjectByType<ShowroomApp>();
        cam = app.surfaces.arCamera;
    }

    Vector2 Screen(Vector3 world) => cam.WorldToScreenPoint(world);

    [UnityTest]
    public IEnumerator CatalogHasFiveItemsAndAppStartsInSimulation()
    {
        CollectionAssert.AreEqual(new[] { "Chair", "Table", "Sofa", "Lamp", "Plant" }, app.catalog.Select(c => c.displayName).ToArray());
        Assert.IsTrue(SimulationMode.Active);
        yield return new WaitForSeconds(0.2f);
        Assert.IsFalse(app.unsupportedPanel.activeSelf);
        Assert.IsFalse(app.onboardingPanel.activeSelf, "Onboarding hides once a surface is found");
        Assert.IsFalse(app.selectionBar.activeSelf);
    }

    [UnityTest]
    public IEnumerator PlacesChairOnFloorFacingTheViewer()
    {
        Vector3 target = new Vector3(0f, 0f, 1.2f);
        app.HandleTap(Screen(target));
        yield return null;
        Assert.AreEqual(1, app.Placed.Count);
        Placeable chair = app.Placed[0];
        Assert.AreEqual("Chair", chair.displayName);
        Assert.Less(Vector3.Distance(chair.transform.position, target), 0.05f);
        Vector3 toCam = cam.transform.position - chair.transform.position;
        toCam.y = 0f;
        Assert.Greater(Vector3.Dot(chair.transform.forward, toCam.normalized), 0.95f);
        Assert.IsTrue(app.selectionBar.activeSelf);
        Assert.AreEqual("Chair", app.selectionLabel.text);
    }

    [UnityTest]
    public IEnumerator CatalogSelectionAndWallRejection()
    {
        app.SelectCatalog(2);
        app.HandleTap(Screen(new Vector3(0f, 1.0f, 3.99f)));
        yield return null;
        Assert.AreEqual(0, app.Placed.Count, "Furniture is not placed on walls");
        app.HandleTap(Screen(new Vector3(0f, 0f, 1.5f)));
        Assert.AreEqual("Sofa", app.Placed[0].displayName);
    }

    [UnityTest]
    public IEnumerator RotateAndDeleteButtons()
    {
        app.HandleTap(Screen(new Vector3(0f, 0f, 1.2f)));
        yield return null;
        Placeable p = app.Selected;
        float yaw = p.transform.eulerAngles.y;
        app.RotateSelected();
        Assert.AreEqual(45f, Mathf.DeltaAngle(yaw, p.transform.eulerAngles.y), 0.5f);
        app.DeleteSelected();
        Assert.AreEqual(0, app.Placed.Count);
        Assert.IsFalse(app.selectionBar.activeSelf);
    }

    [UnityTest]
    public IEnumerator DragMovesFurniture()
    {
        app.SelectCatalog(4);
        app.HandleTap(Screen(new Vector3(-0.4f, 0f, 1.0f)));
        yield return new WaitForSeconds(0.35f);
        Placeable plant = app.Placed[0];
        GestureInputSim.BeginDrag(app, Screen(plant.transform.position + Vector3.up * 0.3f));
        Vector3 target = new Vector3(0.5f, 0f, 1.6f);
        app.DragTo(Screen(target));
        Assert.Less(Vector3.Distance(plant.transform.position, target), 0.05f);
    }

    [UnityTest]
    public IEnumerator TapeMeasureReadsOneMetre()
    {
        app.ToggleMeasure();
        Assert.AreEqual(ShowroomApp.Mode.Measure, app.CurrentMode);
        app.HandleTap(Screen(new Vector3(-0.5f, 0f, 1.2f)));
        yield return null;
        app.HandleTap(Screen(new Vector3(0.5f, 0f, 1.2f)));
        Assert.AreEqual(1f, app.LastDistance, 0.02f);
        Assert.AreEqual(0, app.Placed.Count, "Measuring does not place furniture");
        Assert.AreEqual("1.00 m", ShowroomApp.FormatDistance(1f));
        Assert.AreEqual("35 cm", ShowroomApp.FormatDistance(0.35f));
        app.ClearMeasurements();
        Assert.AreEqual(-1f, app.LastDistance);
    }

    [UnityTest]
    public IEnumerator ScreenshotIsSavedAsPng()
    {
        app.TakeScreenshot();
        for (float t = 0f; t < 3f && app.LastScreenshotPath == null; t += Time.deltaTime) yield return null;
        Assert.IsNotNull(app.LastScreenshotPath, "Screenshot should be written");
        Assert.IsTrue(File.Exists(app.LastScreenshotPath));
        Assert.Greater(new FileInfo(app.LastScreenshotPath).Length, 1000);
        File.Delete(app.LastScreenshotPath);
    }
}

/// <summary>Drives the app's private drag start the same way a one-finger drag does.</summary>
static class GestureInputSim
{
    public static void BeginDrag(ShowroomApp app, Vector2 screenPoint)
    {
        typeof(ShowroomApp).GetMethod("BeginDrag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(app, new object[] { screenPoint });
    }
}
