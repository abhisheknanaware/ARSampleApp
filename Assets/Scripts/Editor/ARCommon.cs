using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

/// <summary>Shared editor helpers for both AR projects: XR/ARCore setup, the AR rig, simulated room, materials and UI.</summary>
public static class ARCommon
{
    public static Sprite UiSprite;
    public static Material TextOutline;

    // ---------------- Project setup ----------------

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    public static T SaveAsset<T>(T asset, string path) where T : UnityEngine.Object
    {
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    public static void ConfigureProject(string productName, string bundleId)
    {
        PlayerSettings.productName = productName;
        PlayerSettings.companyName = "College Project";
        NamedBuildTarget android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, bundleId);
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

        // ARCore on Android; no loader on desktop so the editor falls back to the simulated room.
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
        {
            EnsureFolder("Assets/XR");
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        foreach (BuildTargetGroup g in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
        {
            if (!perTarget.HasSettingsForBuildTarget(g)) perTarget.CreateDefaultSettingsForBuildTarget(g);
            if (!perTarget.HasManagerSettingsForBuildTarget(g)) perTarget.CreateDefaultManagerSettingsForBuildTarget(g);
            perTarget.SettingsForBuildTarget(g).InitManagerOnStart = true;
        }
        XRPackageMetadataStore.AssignLoader(perTarget.SettingsForBuildTarget(BuildTargetGroup.Android).AssignedSettings,
            "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android);

        UnityEditor.XR.ARCore.ARCoreSettings arcore = UnityEditor.XR.ARCore.ARCoreSettings.GetOrCreateSettings();
        arcore.requirement = UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional;
        arcore.depth = UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional;
        EditorUtility.SetDirty(arcore);

        AddARBackgroundFeature();
    }

    /// <summary>URP needs the AR Background Renderer Feature to draw the phone camera image.</summary>
    static void AddARBackgroundFeature()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            UniversalRendererData data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data.rendererFeatures.Any(f => f is ARBackgroundRendererFeature)) continue;
            ARBackgroundRendererFeature feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            feature.name = "ARBackgroundRendererFeature";
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            SerializedObject so = new SerializedObject(data);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
        }
    }

    /// <summary>PC build: a phone-shaped, resizable window that runs in the simulated room.</summary>
    public static void BuildWindows(string scene, string relative, string tag)
    {
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 540;
        PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        Build(scene, BuildTarget.StandaloneWindows64, relative, tag);
    }

    public static void Build(string scene, BuildTarget target, string relative, string tag)
    {
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scene }, locationPathName = output, target = target, options = BuildOptions.None
        });
        BuildSummary s = report.summary;
        Debug.Log($"[{tag}] {target} build {s.result}: {s.totalSize / (1024f * 1024f):F1} MB, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime}");
        if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
    }

    // ---------------- Materials ----------------

    public static Texture2D SaveTexture(string name, int size, Func<int, int, Color> pixel, bool repeat)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = pixel(x, y);
        t.SetPixels(px);
        t.Apply();
        string path = $"Assets/Materials/{name}.png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static Material Lit(string name, Color color, float smooth = 0.3f, float metal = 0f, Texture2D tex = null, Vector2? tiling = null, Color? emission = null)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        if (tex)
        {
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        return SaveAsset(m, $"Assets/Materials/{name}.mat");
    }

    public static Material Transparent(string name, Color color, Texture2D tex = null, Vector2? tiling = null, string shader = "Universal Render Pipeline/Unlit")
    {
        Material m = new Material(Shader.Find(shader)) { name = name };
        m.SetColor("_BaseColor", color);
        if (tex)
        {
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        }
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return SaveAsset(m, $"Assets/Materials/{name}.mat");
    }

    public static void PrepareCommonAssets()
    {
        foreach (string d in new[] { "Assets/Scenes", "Assets/Materials", "Assets/Meshes", "Assets/Prefabs" }) EnsureFolder(d);
        UiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Material outline = new Material(TMP_Settings.defaultFontAsset.material) { name = "TMP_Outline" };
        outline.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        outline.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.75f));
        outline.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
        outline.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        TextOutline = SaveAsset(outline, "Assets/Materials/TMP_Outline.mat");
    }

    // ---------------- Meshes ----------------

    public static Mesh FlatRing(string name, float inner, float outer, int segments = 48)
    {
        var v = new List<Vector3>();
        var tri = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(d * inner);
            v.Add(d * outer);
        }
        for (int i = 0; i < segments; i++)
        {
            int k = i * 2;
            tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
        }
        Mesh m = new Mesh { name = name };
        m.SetVertices(v);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return SaveAsset(m, $"Assets/Meshes/{name}.asset");
    }

    // ---------------- Scene helpers ----------------

    public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat,
        bool collider = true, Vector3? euler = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = euler ?? Vector3.zero;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    public static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    public static Transform Group(string name, Transform parent = null)
    {
        Transform t = new GameObject(name).transform;
        if (parent) t.SetParent(parent, false);
        return t;
    }

    public static GameObject SavePrefab(GameObject go, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------------- AR rig ----------------

    public struct Rig
    {
        public Camera camera;
        public ARSession session;
        public ARPlaneManager planes;
        public ARRaycastManager raycasts;
        public SurfaceRaycaster surfaces;
    }

    public static GameObject PlanePrefab(Material fill, Material edge)
    {
        GameObject go = new GameObject("AR Plane");
        go.AddComponent<ARPlane>();
        go.AddComponent<MeshFilter>();
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = fill;
        r.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<MeshCollider>();
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = edge;
        line.widthMultiplier = 0.006f;
        line.useWorldSpace = false;
        line.loop = true;
        line.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<ARPlaneMeshVisualizer>();
        return SavePrefab(go, "Assets/Prefabs/AR Plane.prefab");
    }

    public static GameObject PointCloudPrefab(Material particle)
    {
        GameObject go = new GameObject("AR Point Cloud");
        go.AddComponent<ARPointCloud>();
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startSize = 0.012f;
        main.startColor = new Color(1f, 0.85f, 0.3f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 2000;
        var em = ps.emission;
        em.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particle;
        go.AddComponent<ARPointCloudParticleVisualizer>();
        return SavePrefab(go, "Assets/Prefabs/AR Point Cloud.prefab");
    }

    public static Rig BuildRig(GameObject planePrefab, GameObject pointCloudPrefab, PlaneDetectionMode detection)
    {
        Rig rig = new Rig();
        GameObject sessionGo = new GameObject("AR Session");
        rig.session = sessionGo.AddComponent<ARSession>();
        sessionGo.AddComponent<ARInputManager>();

        GameObject originGo = new GameObject("XR Origin (AR)");
        XROrigin origin = originGo.AddComponent<XROrigin>();
        Transform offset = Group("Camera Offset", originGo.transform);
        GameObject camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(offset, false);
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 40f;
        cam.fieldOfView = 60f;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<ARCameraManager>();
        camGo.AddComponent<ARCameraBackground>();
        TrackedPoseDriver pose = camGo.AddComponent<TrackedPoseDriver>();
        pose.positionInput = new InputActionProperty(new InputAction("Position", binding: "<HandheldARInputDevice>/devicePosition"));
        pose.rotationInput = new InputActionProperty(new InputAction("Rotation", binding: "<HandheldARInputDevice>/deviceRotation"));
        origin.Camera = cam;
        origin.CameraFloorOffsetObject = offset.gameObject;
        origin.Origin = originGo;

        rig.planes = originGo.AddComponent<ARPlaneManager>();
        rig.planes.planePrefab = planePrefab;
        rig.planes.requestedDetectionMode = detection;
        rig.raycasts = originGo.AddComponent<ARRaycastManager>();
        originGo.AddComponent<ARAnchorManager>();
        if (pointCloudPrefab) originGo.AddComponent<ARPointCloudManager>().pointCloudPrefab = pointCloudPrefab;

        rig.camera = cam;
        rig.surfaces = originGo.AddComponent<SurfaceRaycaster>();
        rig.surfaces.raycastManager = rig.raycasts;
        rig.surfaces.planeManager = rig.planes;
        rig.surfaces.arCamera = cam;

        new GameObject("Gesture Input").AddComponent<GestureInput>();
        return rig;
    }

    public static Transform Reticle(Material ringMat, Material dotMat)
    {
        Transform r = Group("Reticle");
        MeshObj("Ring", r, FlatRing("ReticleRing", 0.07f, 0.085f), ringMat, Vector3.zero);
        MeshObj("Dot", r, FlatRing("ReticleDot", 0f, 0.012f, 16), dotMat, Vector3.zero);
        r.gameObject.SetActive(false);
        return r;
    }

    /// <summary>A small furnished room used when no AR device is available (editor / PC / demo mode).</summary>
    public static GameObject SimulatedRoom(Material overlay, bool withWallSurface)
    {
        Texture2D wood = SaveTexture("SimFloorWood", 256, (x, y) =>
        {
            float plank = Mathf.Floor(x / 32f);
            float seed = Mathf.Repeat(Mathf.Sin(plank * 12.9898f) * 43758.5453f, 1f) * 0.08f;
            float grain = Mathf.PerlinNoise(x * 0.05f + plank * 5f, y * 0.4f) * 0.08f;
            bool seam = x % 32 == 0;
            Color c = new Color(0.62f - seed - grain, 0.45f - seed - grain, 0.3f - seed * 0.5f - grain);
            return seam ? c * 0.75f : c;
        }, true);
        Material floorMat = Lit("SimFloor", Color.white, 0.4f, 0f, wood, new Vector2(4f, 4f));
        Material wallMat = Lit("SimWall", new Color(0.86f, 0.84f, 0.8f), 0.1f);
        Material tableMat = Lit("SimTable", new Color(0.35f, 0.24f, 0.17f), 0.5f);
        Material rugMat = Lit("SimRug", new Color(0.45f, 0.2f, 0.25f), 0.05f);
        Material plantMat = Lit("SimPlant", new Color(0.2f, 0.5f, 0.25f), 0.2f);

        GameObject room = new GameObject("Simulated Room (PC only)");
        GameObject floor = Prim(PrimitiveType.Cube, "Floor", room.transform, new Vector3(0f, -0.05f, 1f), new Vector3(6f, 0.1f, 6f), floorMat);
        floor.AddComponent<SimulatedSurface>();
        GameObject fo = Prim(PrimitiveType.Quad, "PlaneOverlay", floor.transform, new Vector3(0f, 0.51f, 0f), new Vector3(0.9f, 0.9f, 1f), overlay, false, new Vector3(90f, 0f, 0f));
        fo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Prim(PrimitiveType.Cube, "Rug", room.transform, new Vector3(-0.3f, 0.003f, 1.1f), new Vector3(1.8f, 0.006f, 1.2f), rugMat, false);

        GameObject back = Prim(PrimitiveType.Cube, "BackWall", room.transform, new Vector3(0f, 1.4f, 4.05f), new Vector3(6f, 2.8f, 0.1f), wallMat);
        if (withWallSurface)
        {
            back.AddComponent<SimulatedSurface>();
            GameObject wo = Prim(PrimitiveType.Quad, "PlaneOverlay", back.transform, new Vector3(0f, 0f, -0.51f), new Vector3(0.9f, 0.9f, 1f), overlay, false);
            wo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        Prim(PrimitiveType.Cube, "LeftWall", room.transform, new Vector3(-3.05f, 1.4f, 1f), new Vector3(0.1f, 2.8f, 6.2f), wallMat);
        Prim(PrimitiveType.Cube, "RightWall", room.transform, new Vector3(3.05f, 1.4f, 1f), new Vector3(0.1f, 2.8f, 6.2f), wallMat);

        Transform table = Group("Table", room.transform);
        table.localPosition = new Vector3(1.4f, 0f, 2.2f);
        GameObject top = Prim(PrimitiveType.Cube, "Top", table, new Vector3(0f, 0.74f, 0f), new Vector3(1.2f, 0.04f, 0.7f), tableMat);
        top.AddComponent<SimulatedSurface>();
        foreach (Vector2 l in new[] { new Vector2(-0.55f, -0.3f), new Vector2(0.55f, -0.3f), new Vector2(-0.55f, 0.3f), new Vector2(0.55f, 0.3f) })
            Prim(PrimitiveType.Cube, "Leg", table, new Vector3(l.x, 0.36f, l.y), new Vector3(0.05f, 0.72f, 0.05f), tableMat, false);

        Transform plant = Group("Plant", room.transform);
        plant.localPosition = new Vector3(-2.4f, 0f, 3.5f);
        Prim(PrimitiveType.Cylinder, "Pot", plant, new Vector3(0f, 0.18f, 0f), new Vector3(0.35f, 0.18f, 0.35f), tableMat, false);
        Prim(PrimitiveType.Sphere, "Leaves", plant, new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.9f, 0.7f), plantMat, false);

        Light l2 = new GameObject("Room Light").AddComponent<Light>();
        l2.transform.SetParent(room.transform, false);
        l2.transform.localPosition = new Vector3(0f, 2.5f, 1f);
        l2.type = LightType.Point;
        l2.range = 8f;
        l2.intensity = 1.2f;
        l2.color = new Color(1f, 0.95f, 0.85f);
        room.SetActive(false);
        return room;
    }

    public static void Lighting()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.75f, 0.78f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.6f, 0.6f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.32f, 0.3f);
        Light sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        sun.intensity = 1.1f;
        sun.shadows = LightShadows.Soft;
    }

    // ---------------- UI ----------------

    public static Canvas Canvas(string name = "UI")
    {
        GameObject go = new GameObject(name);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler s = go.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1080f, 1920f);
        s.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
        return c;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Panel(RectTransform rt, Color color)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = UiSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        return img;
    }

    public static TMP_Text Text(Transform parent, string name, string value, float size, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        RectTransform rt = Rect(name, parent, anchor, pivot, pos, box);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = value;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.fontSharedMaterial = TextOutline;
        return t;
    }

    public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color color, UnityAction action, out TMP_Text text)
    {
        RectTransform rt = Rect(label + " Button", parent, anchor, new Vector2(0.5f, 0.5f), pos, size);
        Image img = Panel(rt, color);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        b.colors = cb;
        UnityEventTools.AddPersistentListener(b.onClick, action);
        text = Text(rt, "Label", label, Mathf.Min(40f, size.y * 0.38f), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16f, 8f));
        text.fontStyle = FontStyles.Bold;
        return b;
    }

    public static Button IntButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color color, UnityAction<int> action, int argument, out TMP_Text text)
    {
        RectTransform rt = Rect(label + " Button", parent, anchor, new Vector2(0.5f, 0.5f), pos, size);
        Image img = Panel(rt, color);
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        UnityEventTools.AddIntPersistentListener(b.onClick, action, argument);
        text = Text(rt, "Label", label, Mathf.Min(36f, size.y * 0.34f), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(12f, 8f));
        text.fontStyle = FontStyles.Bold;
        return b;
    }

    public static void Capture(Camera cam, string path, int w = 1080, int h = 1920)
    {
        RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.aspect = w / (float)h;
        for (int i = 0; i < 4; i++) cam.Render();
        RenderTexture.active = rt;
        Texture2D img = new Texture2D(w, h, TextureFormat.RGB24, false);
        img.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        img.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, img.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
    }
}
