using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;

public static class ShowroomBuilder
{
    const string ScenePath = "Assets/Scenes/ARShowroom.unity";

    [MenuItem("AR Showroom/1. Build Project Content")]
    public static void BuildAll()
    {
        try
        {
            if (TMP_Settings.defaultFontAsset == null) throw new Exception("TMP Essentials missing");
            ARCommon.PrepareCommonAssets();
            ARCommon.ConfigureProject("AR Showroom", "com.college.arshowroom");
            BuildScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[ARShowroom] Project content built successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("[ARShowroom] Build content failed: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    [MenuItem("AR Showroom/2. Build Android APK")]
    public static void BuildAndroid() => ARCommon.Build(ScenePath, BuildTarget.Android, "Builds/Android/ARShowroom.apk", "ARShowroom");

    [MenuItem("AR Showroom/2b. Build Windows (PC)")]
    public static void BuildWindows() => ARCommon.BuildWindows(ScenePath, "Builds/Windows/ARShowroom.exe", "ARShowroom");

    static Placeable Furniture(string name, float ringRadius, Material selMat, Mesh ring, Action<Transform> build)
    {
        GameObject root = new GameObject(name);
        build(root.transform);
        GameObject sel = ARCommon.MeshObj("SelectionRing", root.transform, ring, selMat, new Vector3(0f, 0.004f, 0f));
        sel.transform.localScale = Vector3.one * (ringRadius / 0.5f);
        Placeable p = root.AddComponent<Placeable>();
        p.displayName = name;
        p.selectionRing = sel;
        sel.SetActive(false);
        p.minScale = 1f;
        p.maxScale = 1f;
        return ARCommon.SavePrefab(root, $"Assets/Prefabs/{name}.prefab").GetComponent<Placeable>();
    }

    static void BuildScene()
    {
        Texture2D dots = ARCommon.SaveTexture("PlaneDots", 64, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x % 16, y % 16), new Vector2(7.5f, 7.5f));
            return new Color(1f, 1f, 1f, d < 2.2f ? 0.85f : 0.1f);
        }, true);
        Material planeFill = ARCommon.Transparent("PlaneFill", new Color(1f, 1f, 1f, 0.45f), dots, new Vector2(8f, 8f));
        Material planeEdge = ARCommon.Transparent("PlaneEdge", new Color(1f, 1f, 1f, 0.8f));
        Material ringMat = ARCommon.Transparent("Ring", new Color(1f, 1f, 1f, 0.95f));
        Material selMat = ARCommon.Transparent("SelectionRing", new Color(1f, 0.78f, 0.25f, 0.95f));
        Mesh selRing = ARCommon.FlatRing("SelectionRingMesh", 0.46f, 0.5f);

        Material oak = ARCommon.Lit("Oak", new Color(0.72f, 0.52f, 0.32f), 0.45f);
        Material walnut = ARCommon.Lit("Walnut", new Color(0.36f, 0.23f, 0.15f), 0.5f);
        Material fabricBlue = ARCommon.Lit("FabricBlue", new Color(0.22f, 0.36f, 0.62f), 0.1f);
        Material fabricGrey = ARCommon.Lit("FabricGrey", new Color(0.55f, 0.56f, 0.6f), 0.08f);
        Material metal = ARCommon.Lit("BlackMetal", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.8f);
        Material shade = ARCommon.Lit("LampShade", new Color(1f, 0.94f, 0.82f), 0.2f, 0f, null, null, new Color(1f, 0.85f, 0.55f) * 1.4f);
        Material ceramic = ARCommon.Lit("Ceramic", new Color(0.92f, 0.9f, 0.86f), 0.7f);
        Material leaf = ARCommon.Lit("Leaf", new Color(0.18f, 0.5f, 0.22f), 0.3f);

        Placeable chair = Furniture("Chair", 0.38f, selMat, selRing, t =>
        {
            ARCommon.Prim(PrimitiveType.Cube, "Seat", t, new Vector3(0f, 0.45f, 0f), new Vector3(0.46f, 0.05f, 0.46f), oak);
            ARCommon.Prim(PrimitiveType.Cube, "Cushion", t, new Vector3(0f, 0.485f, 0f), new Vector3(0.42f, 0.03f, 0.42f), fabricBlue);
            ARCommon.Prim(PrimitiveType.Cube, "Back", t, new Vector3(0f, 0.7f, -0.21f), new Vector3(0.46f, 0.45f, 0.04f), oak);
            foreach (Vector2 l in new[] { new Vector2(-0.2f, -0.2f), new Vector2(0.2f, -0.2f), new Vector2(-0.2f, 0.2f), new Vector2(0.2f, 0.2f) })
                ARCommon.Prim(PrimitiveType.Cube, "Leg", t, new Vector3(l.x, 0.215f, l.y), new Vector3(0.04f, 0.43f, 0.04f), oak);
        });
        Placeable table = Furniture("Table", 0.75f, selMat, selRing, t =>
        {
            ARCommon.Prim(PrimitiveType.Cube, "Top", t, new Vector3(0f, 0.74f, 0f), new Vector3(1.2f, 0.04f, 0.7f), walnut);
            foreach (Vector2 l in new[] { new Vector2(-0.54f, -0.29f), new Vector2(0.54f, -0.29f), new Vector2(-0.54f, 0.29f), new Vector2(0.54f, 0.29f) })
                ARCommon.Prim(PrimitiveType.Cube, "Leg", t, new Vector3(l.x, 0.36f, l.y), new Vector3(0.05f, 0.72f, 0.05f), walnut);
        });
        Placeable sofa = Furniture("Sofa", 1.0f, selMat, selRing, t =>
        {
            ARCommon.Prim(PrimitiveType.Cube, "Base", t, new Vector3(0f, 0.22f, 0f), new Vector3(1.8f, 0.3f, 0.85f), fabricGrey);
            ARCommon.Prim(PrimitiveType.Cube, "Back", t, new Vector3(0f, 0.6f, -0.33f), new Vector3(1.8f, 0.5f, 0.2f), fabricGrey);
            foreach (float x in new[] { -0.82f, 0.82f })
                ARCommon.Prim(PrimitiveType.Cube, "Arm", t, new Vector3(x, 0.47f, 0f), new Vector3(0.16f, 0.22f, 0.85f), fabricGrey);
            foreach (float x in new[] { -0.5f, 0f, 0.5f })
                ARCommon.Prim(PrimitiveType.Cube, "Cushion", t, new Vector3(x, 0.42f, 0.06f), new Vector3(0.48f, 0.1f, 0.62f), fabricBlue);
            foreach (Vector2 l in new[] { new Vector2(-0.82f, -0.35f), new Vector2(0.82f, -0.35f), new Vector2(-0.82f, 0.35f), new Vector2(0.82f, 0.35f) })
                ARCommon.Prim(PrimitiveType.Cylinder, "Foot", t, new Vector3(l.x, 0.035f, l.y), new Vector3(0.05f, 0.035f, 0.05f), walnut);
        });
        Placeable lamp = Furniture("Lamp", 0.25f, selMat, selRing, t =>
        {
            ARCommon.Prim(PrimitiveType.Cylinder, "Base", t, new Vector3(0f, 0.015f, 0f), new Vector3(0.3f, 0.015f, 0.3f), metal);
            ARCommon.Prim(PrimitiveType.Cylinder, "Pole", t, new Vector3(0f, 0.75f, 0f), new Vector3(0.025f, 0.75f, 0.025f), metal);
            ARCommon.Prim(PrimitiveType.Cylinder, "Shade", t, new Vector3(0f, 1.48f, 0f), new Vector3(0.38f, 0.14f, 0.38f), shade);
            Light l = new GameObject("Bulb").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            l.type = LightType.Point;
            l.range = 2.5f;
            l.intensity = 1.2f;
            l.color = new Color(1f, 0.85f, 0.6f);
        });
        Placeable plant = Furniture("Plant", 0.28f, selMat, selRing, t =>
        {
            ARCommon.Prim(PrimitiveType.Cylinder, "Pot", t, new Vector3(0f, 0.17f, 0f), new Vector3(0.32f, 0.17f, 0.32f), ceramic);
            ARCommon.Prim(PrimitiveType.Cylinder, "Soil", t, new Vector3(0f, 0.335f, 0f), new Vector3(0.29f, 0.01f, 0.29f), walnut, false);
            Vector3[] leaves = { new Vector3(0f, 0.75f, 0f), new Vector3(0.12f, 0.6f, 0.05f), new Vector3(-0.11f, 0.62f, -0.06f), new Vector3(0.03f, 0.58f, -0.13f), new Vector3(-0.04f, 0.9f, 0.05f) };
            foreach (Vector3 p in leaves) ARCommon.Prim(PrimitiveType.Sphere, "Leaves", t, p, new Vector3(0.3f, 0.4f, 0.3f), leaf);
        });

        Material tape = ARCommon.Transparent("TapeLine", new Color(1f, 0.85f, 0.2f, 1f));
        GameObject lineGo = new GameObject("Measure Line");
        LineRenderer line = lineGo.AddComponent<LineRenderer>();
        line.sharedMaterial = tape;
        line.widthMultiplier = 0.008f;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.shadowCastingMode = ShadowCastingMode.Off;
        LineRenderer linePrefab = ARCommon.SavePrefab(lineGo, "Assets/Prefabs/Measure Line.prefab").GetComponent<LineRenderer>();
        GameObject marker = ARCommon.Prim(PrimitiveType.Sphere, "Measure Point", null, Vector3.zero, Vector3.one * 0.022f, ARCommon.Lit("MarkerWhite", Color.white, 0.6f, 0f, null, null, Color.white * 0.4f), false);
        Transform markerPrefab = ARCommon.SavePrefab(marker, "Assets/Prefabs/Measure Point.prefab").transform;
        GameObject labelGo = new GameObject("Measure Label");
        TextMeshPro label = labelGo.AddComponent<TextMeshPro>();
        label.fontSize = 0.7f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.fontSharedMaterial = ARCommon.TextOutline;
        label.rectTransform.sizeDelta = new Vector2(0.5f, 0.12f);
        TextMeshPro labelPrefab = ARCommon.SavePrefab(labelGo, "Assets/Prefabs/Measure Label.prefab").GetComponent<TextMeshPro>();

        GameObject planePrefab = ARCommon.PlanePrefab(planeFill, planeEdge);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ARCommon.Lighting();
        ARCommon.Rig rig = ARCommon.BuildRig(planePrefab, null, PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical);
        Transform reticle = ARCommon.Reticle(ringMat, ringMat);
        GameObject room = ARCommon.SimulatedRoom(planeFill, true);

        SimulationMode sim = new GameObject("Simulation Mode").AddComponent<SimulationMode>();
        sim.simulatedRoom = room;
        sim.arCamera = rig.camera;
        sim.arSession = rig.session;
        sim.activateWhenUnsupported = false;

        ShowroomApp app = new GameObject("AR Showroom App").AddComponent<ShowroomApp>();
        app.surfaces = rig.surfaces;
        app.reticle = reticle;
        app.catalog = new[] { chair, table, sofa, lamp, plant };
        app.measureLinePrefab = linePrefab;
        app.measureMarkerPrefab = markerPrefab;
        app.measureLabelPrefab = labelPrefab;
        BuildUI(app);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static void BuildUI(ShowroomApp app)
    {
        Canvas canvas = ARCommon.Canvas();
        app.canvas = canvas;
        Transform c = canvas.transform;
        Color dark = new Color(0.06f, 0.06f, 0.09f, 0.75f);
        Color gold = new Color(1f, 0.78f, 0.25f);

        RectTransform top = ARCommon.Rect("Top Bar", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1040f, 170f));
        ARCommon.Panel(top, dark).raycastTarget = false;
        ARCommon.Text(top, "Title", "AR SHOWROOM", 48, gold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(1000f, 64f)).fontStyle = FontStyles.Bold;
        app.hintText = ARCommon.Text(top, "Hint", "", 32, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(1000f, 70f));

        RectTransform onboard = ARCommon.Rect("Onboarding", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(860f, 300f));
        ARCommon.Panel(onboard, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = false;
        ARCommon.Text(onboard, "Text", "Point your phone at the floor\nand move it slowly to scan the room", 40, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 260f));
        app.onboardingPanel = onboard.gameObject;

        app.toastText = ARCommon.Text(c, "Toast", "", 36, gold, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 620f), new Vector2(1000f, 70f));

        RectTransform sel = ARCommon.Rect("Selection Bar", c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 470f), new Vector2(1040f, 120f));
        ARCommon.Panel(sel, dark);
        app.selectionLabel = ARCommon.Text(sel, "Selected", "Chair", 38, gold, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(420f, 80f), TextAlignmentOptions.Left);
        ARCommon.Button(sel, "Rotate 45", new Vector2(0.5f, 0.5f), new Vector2(150f, 0f), new Vector2(260f, 96f), new Color(0.25f, 0.5f, 0.9f), app.RotateSelected, out _);
        ARCommon.Button(sel, "Delete", new Vector2(0.5f, 0.5f), new Vector2(380f, 0f), new Vector2(200f, 96f), new Color(0.8f, 0.25f, 0.28f), app.DeleteSelected, out _);
        app.selectionBar = sel.gameObject;

        RectTransform bottom = ARCommon.Rect("Catalog Panel", c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1040f, 420f));
        ARCommon.Panel(bottom, dark);
        ARCommon.Text(bottom, "Catalog Title", "CATALOG", 30, new Color(1f, 1f, 1f, 0.7f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(1000f, 44f));
        string[] items = { "Chair", "Table", "Sofa", "Lamp", "Plant" };
        app.catalogButtons = new Image[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            Button b = ARCommon.IntButton(bottom, items[i], new Vector2(0.5f, 0f), new Vector2(-404f + i * 202f, 270f), new Vector2(188f, 140f), Color.white, app.SelectCatalog, i, out TMP_Text t);
            t.color = new Color(0.1f, 0.1f, 0.14f);
            t.fontMaterial = TMP_Settings.defaultFontAsset.material;
            app.catalogButtons[i] = b.GetComponent<Image>();
        }
        ARCommon.Button(bottom, "Measure", new Vector2(0.5f, 0f), new Vector2(-378f, 90f), new Vector2(236f, 110f), new Color(0.95f, 0.6f, 0.15f), app.ToggleMeasure, out TMP_Text modeLabel);
        app.modeButtonLabel = modeLabel;
        ARCommon.Button(bottom, "Photo", new Vector2(0.5f, 0f), new Vector2(-126f, 90f), new Vector2(236f, 110f), new Color(0.25f, 0.65f, 0.45f), app.TakeScreenshot, out _);
        ARCommon.Button(bottom, "Clear Tape", new Vector2(0.5f, 0f), new Vector2(126f, 90f), new Vector2(236f, 110f), new Color(0.45f, 0.45f, 0.52f), app.ClearMeasurements, out _);
        ARCommon.Button(bottom, "Clear Room", new Vector2(0.5f, 0f), new Vector2(378f, 90f), new Vector2(236f, 110f), new Color(0.6f, 0.25f, 0.3f), app.ClearAll, out _);

        RectTransform unsupported = ARCommon.Rect("Unsupported Panel", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1920f));
        ARCommon.Panel(unsupported, new Color(0.05f, 0.05f, 0.08f, 0.96f));
        ARCommon.Text(unsupported, "Title", "AR not supported", 64, gold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1000f, 90f)).fontStyle = FontStyles.Bold;
        ARCommon.Text(unsupported, "Body", "This phone does not support Google ARCore\n(Google Play Services for AR).\n\nYou can still try the app in a virtual room.", 38, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(940f, 260f));
        ARCommon.Button(unsupported, "Try Demo Mode", new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(520f, 130f), new Color(0.25f, 0.65f, 0.45f), app.StartDemoMode, out _);
        app.unsupportedPanel = unsupported.gameObject;
    }

    [MenuItem("AR Showroom/3. Save Preview Screenshot")]
    public static void Screenshot()
    {
        EditorSceneManager.OpenScene(ScenePath);
        SimulationMode sim = UnityEngine.Object.FindAnyObjectByType<SimulationMode>(FindObjectsInactive.Include);
        ShowroomApp app = UnityEngine.Object.FindAnyObjectByType<ShowroomApp>();
        sim.simulatedRoom.SetActive(true);
        Camera cam = sim.arCamera;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.transform.SetPositionAndRotation(sim.startPosition, Quaternion.Euler(sim.startAngles.x, sim.startAngles.y, 0f));
        (int item, Vector3 pos, float yaw)[] layout =
        {
            (2, new Vector3(-0.3f, 0f, 2.6f), 180f), (1, new Vector3(-0.35f, 0f, 1.3f), 0f), (0, new Vector3(0.55f, 0f, 1.2f), -110f),
            (3, new Vector3(-1.5f, 0f, 2.7f), 0f), (4, new Vector3(0.9f, 0f, 0.4f), 0f)
        };
        foreach (var (item, pos, yaw) in layout)
        {
            Placeable p = (Placeable)PrefabUtility.InstantiatePrefab(app.catalog[item]);
            p.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            if (item == 0) p.selectionRing.SetActive(true);
        }
        LineRenderer line = (LineRenderer)PrefabUtility.InstantiatePrefab(app.measureLinePrefab);
        Vector3 a = new Vector3(-1.1f, 0.006f, 0.3f), b = new Vector3(-1.1f, 0.006f, 1.8f);
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        TextMeshPro label = (TextMeshPro)PrefabUtility.InstantiatePrefab(app.measureLabelPrefab);
        label.text = ShowroomApp.FormatDistance(Vector3.Distance(a, b));
        label.transform.position = (a + b) * 0.5f + Vector3.up * 0.06f;
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
        ARCommon.Capture(cam, Path.Combine(dir, "showroom_simulated_room.png"));
        EditorSceneManager.OpenScene(ScenePath);
        Debug.Log("[ARShowroom] Screenshot saved to " + dir);
    }
}
