using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 최적화 전후 비교용 측정 도구.
// Play 모드에서 PerfCapture.Run("라벨")을 호출하면 고정 시점마다
// 렌더 통계 평균, 게임 화면, Stats 창 화면, 가시성 맵(위에서 내려다본 그림)을 Docs/Perf/<라벨>/에 저장한다.
public static class PerfCapture
{
    private struct ViewPoint
    {
        public string Name;
        public string Desc;
        public Vector3 Pos;
        public Vector3 Euler;
    }

    private static readonly ViewPoint[] Views =
    {
        new ViewPoint { Name = "01_spawn", Desc = "스폰 지점, 북쪽", Pos = new Vector3(10f, 1.7f, -2f), Euler = new Vector3(5f, 0f, 0f) },
        new ViewPoint { Name = "02_hangar_front", Desc = "격납고 바로 앞, 동쪽 (가림 많음)", Pos = new Vector3(12f, 1.7f, -13f), Euler = new Vector3(0f, 90f, 0f) },
        new ViewPoint { Name = "03_open_center", Desc = "맵 중앙 트인 곳, 북동쪽", Pos = new Vector3(40f, 1.7f, -35f), Euler = new Vector3(0f, 45f, 0f) },
        new ViewPoint { Name = "04_overview", Desc = "맵 모서리 고공 전경", Pos = new Vector3(-55f, 25f, -85f), Euler = new Vector3(20f, 45f, 0f) },
        new ViewPoint { Name = "05_player_close", Desc = "플레이어 3.5m 정면", Pos = new Vector3(10f, 2.0f, 3.5f), Euler = new Vector3(10f, 180f, 0f) },
        new ViewPoint { Name = "06_player_far", Desc = "플레이어 35m 뒤", Pos = new Vector3(10f, 3f, -35f), Euler = new Vector3(3f, 0f, 0f) },
        new ViewPoint { Name = "07_quarter", Desc = "쿼터뷰 (45,45) 12m", Pos = new Vector3(4f, 9.49f, -6f), Euler = new Vector3(45f, 45f, 0f) },
    };

    private const int WarmupFrames = 30;
    private const int SampleFrames = 120;

    private enum Phase { Warmup, Sample, Shot, StatsGrab, VisPrep, VisRead, Next }

    private class Result
    {
        public string View;
        public string Desc;
        public float DrawCalls, Batches, SetPass, Triangles, Vertices, CullMs, MainThreadMs, CpuFrameMs, GpuFrameMs;
        public int StatsDrawCalls, StatsBatches, StatsSetPass, StatsTris, StatsVerts, StatsVisibleSkinned;
        public int VisibleRenderers, InFrustum, Occluded, OutOfFrustum, LodGroupsTotal;
        public int[] LodHistogram = new int[5];
    }

    private static string _label;
    private static string _outDir;
    private static int _viewIndex;
    private static Phase _phase;
    private static int _counter;
    private static int _lastFrame;
    private static Result _current;
    private static readonly List<Result> Results = new List<Result>();
    private static readonly List<ProfilerRecorder> Recorders = new List<ProfilerRecorder>();
    private static Camera _cam;
    private static float _savedShadowDistance;
    private static double _sumDraw, _sumBatch, _sumSetPass, _sumTris, _sumVerts, _sumCull, _sumMain, _sumCpu, _sumGpu;
    private static int _gpuSamples;

    public static bool IsRunning { get; private set; }
    public static string LastStatus { get; private set; } = "idle";

    public static void Run(string label)
    {
        if (!EditorApplication.isPlaying)
        {
            LastStatus = "error: play 모드가 아닙니다";
            return;
        }

        _label = label;
        _outDir = Path.Combine("Docs", "Perf", label);
        Directory.CreateDirectory(_outDir);
        Results.Clear();
        _viewIndex = 0;

        PrepareScene();

        Recorders.Clear();
        Recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"));
        Recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"));
        Recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"));
        Recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"));
        Recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count"));
        Recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Render, "CullScriptable"));
        Recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread"));

        IsRunning = true;
        LastStatus = "running";
        BeginView();
        EditorApplication.update += Tick;
    }

    private static void PrepareScene()
    {
        // 측정 조건을 매번 같게 만든다: 시간 정지, 좀비 스폰 중지, UI/미니맵 끄기, 카메라 스크립트 끄기.
        Time.timeScale = 0f;

        foreach (var b in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            string typeName = b.GetType().Name;
            if (typeName == "ZombieSpawner" || typeName == "CameraFollow" || typeName == "CameraRotate" || typeName == "MinimapCamera")
            {
                b.enabled = false;
            }
        }

        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (c != Camera.main) c.enabled = false;
        }

        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            canvas.enabled = false;
        }

        _cam = Camera.main;

        // Scene 뷰가 같이 렌더되면 통계가 섞이므로 Game 뷰를 최대화하고 Stats 오버레이를 켠다.
        var gameView = GetGameView();
        if (gameView != null)
        {
            gameView.maximized = true;
            var statsField = gameView.GetType().GetField("m_Stats", BindingFlags.Instance | BindingFlags.NonPublic);
            statsField?.SetValue(gameView, true);
            gameView.Repaint();
        }
    }

    private static EditorWindow GetGameView()
    {
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        return EditorWindow.GetWindow(type, false, null, false);
    }

    private static void BeginView()
    {
        var v = Views[_viewIndex];
        _cam.transform.SetPositionAndRotation(v.Pos, Quaternion.Euler(v.Euler));
        _current = new Result { View = v.Name, Desc = v.Desc };
        _phase = Phase.Warmup;
        _counter = 0;
        _sumDraw = _sumBatch = _sumSetPass = _sumTris = _sumVerts = _sumCull = _sumMain = _sumCpu = _sumGpu = 0;
        _gpuSamples = 0;
        _lastFrame = Time.frameCount;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish("error: 측정 중 play 모드가 종료되었습니다");
            return;
        }

        // 프레임이 넘어갔을 때만 진행한다.
        if (Time.frameCount == _lastFrame) return;
        _lastFrame = Time.frameCount;

        try
        {
            Step();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Finish("error: " + e.Message);
        }
    }

    private static void Step()
    {
        var v = Views[_viewIndex];
        _cam.transform.SetPositionAndRotation(v.Pos, Quaternion.Euler(v.Euler));

        switch (_phase)
        {
            case Phase.Warmup:
                if (++_counter >= WarmupFrames)
                {
                    _phase = Phase.Sample;
                    _counter = 0;
                }
                break;

            case Phase.Sample:
                _sumDraw += Recorders[0].LastValue;
                _sumBatch += Recorders[1].LastValue;
                _sumSetPass += Recorders[2].LastValue;
                _sumTris += Recorders[3].LastValue;
                _sumVerts += Recorders[4].LastValue;
                _sumCull += Recorders[5].LastValue * 1e-6;
                _sumMain += Recorders[6].LastValue * 1e-6;

                FrameTimingManager.CaptureFrameTimings();
                var timings = new FrameTiming[1];
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    _sumCpu += timings[0].cpuFrameTime;
                    // 드물게 비정상적으로 큰 값이 들어오므로 1초가 넘는 GPU 시간은 버린다.
                    if (timings[0].gpuFrameTime > 0 && timings[0].gpuFrameTime < 1000)
                    {
                        _sumGpu += timings[0].gpuFrameTime;
                        _gpuSamples++;
                    }
                }

                if (++_counter >= SampleFrames)
                {
                    _current.DrawCalls = (float)(_sumDraw / SampleFrames);
                    _current.Batches = (float)(_sumBatch / SampleFrames);
                    _current.SetPass = (float)(_sumSetPass / SampleFrames);
                    _current.Triangles = (float)(_sumTris / SampleFrames);
                    _current.Vertices = (float)(_sumVerts / SampleFrames);
                    _current.CullMs = (float)(_sumCull / SampleFrames);
                    _current.MainThreadMs = (float)(_sumMain / SampleFrames);
                    _current.CpuFrameMs = (float)(_sumCpu / SampleFrames);
                    _current.GpuFrameMs = _gpuSamples > 0 ? (float)(_sumGpu / _gpuSamples) : 0f;

                    // Game 뷰 Stats 창이 보여주는 값(UnityStats)도 같이 남긴다.
                    _current.StatsDrawCalls = UnityStats.drawCalls;
                    _current.StatsBatches = UnityStats.batches;
                    _current.StatsSetPass = UnityStats.setPassCalls;
                    _current.StatsTris = UnityStats.triangles;
                    _current.StatsVerts = UnityStats.vertices;
                    _current.StatsVisibleSkinned = UnityStats.visibleSkinnedMeshes;

                    ScreenCapture.CaptureScreenshot(Path.Combine(_outDir, v.Name + "_game.png"));
                    _phase = Phase.Shot;
                    _counter = 0;
                }
                break;

            case Phase.Shot:
                if (++_counter >= 3)
                {
                    _phase = Phase.StatsGrab;
                    _counter = 0;
                }
                break;

            case Phase.StatsGrab:
                GrabGameViewWindow(Path.Combine(_outDir, v.Name + "_stats.png"));
                // 그림자 패스도 isVisible을 켜므로 가시성 판정 동안만 그림자를 끈다.
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urp != null)
                {
                    _savedShadowDistance = urp.shadowDistance;
                    urp.shadowDistance = 0f;
                }
                _phase = Phase.VisPrep;
                _counter = 0;
                break;

            case Phase.VisPrep:
                if (++_counter >= 3)
                {
                    _phase = Phase.VisRead;
                }
                break;

            case Phase.VisRead:
                BuildVisibilityMap(Path.Combine(_outDir, v.Name + "_visibility.png"));
                var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urpAsset != null) urpAsset.shadowDistance = _savedShadowDistance;
                Results.Add(_current);
                _phase = Phase.Next;
                break;

            case Phase.Next:
                _viewIndex++;
                if (_viewIndex >= Views.Length)
                {
                    WriteReport();
                    Finish("done: " + _outDir);
                    return;
                }
                BeginView();
                break;
        }
    }

    private static void Finish(string status)
    {
        EditorApplication.update -= Tick;
        foreach (var r in Recorders) r.Dispose();
        Recorders.Clear();
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null && _phase == Phase.VisRead) urp.shadowDistance = _savedShadowDistance;
        var gameView = GetGameView();
        if (gameView != null) gameView.maximized = false;
        IsRunning = false;
        LastStatus = status;
        Debug.Log("[PerfCapture] " + status);
    }

    // 에디터 Game 창을 화면 픽셀 그대로 읽어 Stats 오버레이까지 함께 저장한다.
    private static void GrabGameViewWindow(string path)
    {
        var gameView = GetGameView();
        if (gameView == null) return;
        float ppp = EditorGUIUtility.pixelsPerPoint;
        Rect r = gameView.position;
        int w = Mathf.RoundToInt(r.width * ppp);
        int h = Mathf.RoundToInt(r.height * ppp);
        var pixels = InternalEditorUtility.ReadScreenPixel(new Vector2(r.x * ppp, r.y * ppp), w, h);
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    // 메인 카메라 기준 렌더러 상태를 위에서 내려다본 그림으로 그린다.
    // 초록: 렌더됨(LOD0), 노랑/주황/보라: LOD1/2/3로 렌더됨, 빨강: 시야 안인데 렌더 안 됨(오클루전 또는 LOD 컬링), 회색: 시야 밖.
    private static void BuildVisibilityMap(string path)
    {
        var planes = GeometryUtility.CalculateFrustumPlanes(_cam);
        var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r => (r is MeshRenderer || r is SkinnedMeshRenderer) && r.enabled && r.gameObject.activeInHierarchy)
            .ToList();

        var colorOf = new Dictionary<Renderer, Color>();
        var hidden = new HashSet<Renderer>();
        var lodColors = new[] { new Color(0.2f, 0.85f, 0.3f), new Color(0.95f, 0.85f, 0.2f), new Color(1f, 0.5f, 0.1f), new Color(0.7f, 0.3f, 0.9f), new Color(0.3f, 0.6f, 1f) };
        var red = new Color(0.9f, 0.15f, 0.15f);
        var gray = new Color(0.35f, 0.35f, 0.35f);

        int visible = 0, inFrustum = 0, occluded = 0, outFrustum = 0;
        var lodGroups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
        _current.LodGroupsTotal = lodGroups.Length;
        var inLodGroup = new HashSet<Renderer>();

        // LOD Group은 하나의 물체로 세고, 실제로 렌더된 LOD 단계로 색을 칠한다.
        foreach (var g in lodGroups)
        {
            var lods = g.GetLODs();
            var groupRenderers = lods.SelectMany(l => l.renderers).Where(r => r != null).Distinct().ToList();
            foreach (var r in groupRenderers) inLodGroup.Add(r);
            if (groupRenderers.Count == 0) continue;

            int shown = -1;
            for (int i = 0; i < lods.Length && shown < 0; i++)
            {
                if (lods[i].renderers.Any(r => r != null && r.isVisible)) shown = i;
            }

            var bounds = groupRenderers[0].bounds;
            foreach (var r in groupRenderers) bounds.Encapsulate(r.bounds);
            bool frustum = GeometryUtility.TestPlanesAABB(planes, bounds);

            Color c;
            if (shown >= 0)
            {
                visible++;
                inFrustum++;
                _current.LodHistogram[Mathf.Min(shown, 4)]++;
                c = lodColors[Mathf.Min(shown, 4)];
            }
            else if (frustum)
            {
                inFrustum++;
                occluded++;
                c = red;
            }
            else
            {
                outFrustum++;
                c = gray;
            }

            // 그림에는 LOD0 렌더러만 대표로 그린다.
            var drawSet = lods[0].renderers.Where(r => r != null).ToList();
            foreach (var r in groupRenderers)
            {
                if (drawSet.Contains(r)) colorOf[r] = c;
                else hidden.Add(r);
            }
        }

        foreach (var r in all)
        {
            if (inLodGroup.Contains(r)) continue;
            bool frustum = GeometryUtility.TestPlanesAABB(planes, r.bounds);
            if (r.isVisible)
            {
                visible++;
                inFrustum++;
                _current.LodHistogram[0]++;
                colorOf[r] = lodColors[0];
            }
            else if (frustum)
            {
                inFrustum++;
                occluded++;
                colorOf[r] = red;
            }
            else
            {
                outFrustum++;
                colorOf[r] = gray;
            }
        }

        _current.VisibleRenderers = visible;
        _current.InFrustum = inFrustum;
        _current.Occluded = occluded;
        _current.OutOfFrustum = outFrustum;

        // 바닥 타일은 위에서 보면 다른 물체를 덮으므로 같은 색을 어둡게 칠해 구분한다.
        foreach (var r in colorOf.Keys.ToList())
        {
            var s = r.bounds.size;
            if (s.y < 0.5f && s.x * s.z > 100f) colorOf[r] = colorOf[r] * 0.4f;
        }

        RenderTopDown(path, all, colorOf, hidden);
    }

    private static void RenderTopDown(string path, List<Renderer> all, Dictionary<Renderer, Color> colorOf, HashSet<Renderer> hidden)
    {
        var worldBounds = all[0].bounds;
        foreach (var r in all) worldBounds.Encapsulate(r.bounds);

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var matCache = new Dictionary<Color, Material>();
        var saved = new Dictionary<Renderer, Material[]>();
        var savedEnabled = new Dictionary<Renderer, bool>();

        foreach (var r in all)
        {
            saved[r] = r.sharedMaterials;
            savedEnabled[r] = r.enabled;
            if (hidden.Contains(r) || !colorOf.TryGetValue(r, out var c))
            {
                r.enabled = false;
                continue;
            }
            if (!matCache.TryGetValue(c, out var m))
            {
                m = new Material(shader);
                m.SetColor("_BaseColor", c);
                matCache[c] = m;
            }
            r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
            // LOD Group에 속한 대표 렌더러는 LOD 전환과 상관없이 그려지도록 잠시 LOD Group을 끈다.
        }

        var lodGroups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
        var lodEnabled = lodGroups.ToDictionary(g => g, g => g.enabled);
        foreach (var g in lodGroups) g.enabled = false;

        const int size = 1024;
        var go = new GameObject("PerfCapture_TopDown");
        var topCam = go.AddComponent<Camera>();
        topCam.orthographic = true;
        float half = Mathf.Max(worldBounds.extents.x, worldBounds.extents.z) * 1.05f;
        topCam.orthographicSize = half;
        topCam.transform.position = new Vector3(worldBounds.center.x, worldBounds.max.y + 50f, worldBounds.center.z);
        topCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        topCam.nearClipPlane = 1f;
        topCam.farClipPlane = 200f;
        topCam.clearFlags = CameraClearFlags.SolidColor;
        topCam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
        topCam.useOcclusionCulling = false;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.renderShadows = false;

        var rt = new RenderTexture(size, size, 24);
        topCam.targetTexture = rt;
        topCam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        RenderTexture.active = prev;

        // 메인 카메라 위치와 시야 방향을 그린다.
        Vector2 ToPixel(Vector3 w)
        {
            var vp = topCam.WorldToViewportPoint(w);
            return new Vector2(vp.x * size, vp.y * size);
        }

        var camPos = _cam.transform.position;
        var fwd = _cam.transform.forward;
        fwd.y = 0f;
        fwd.Normalize();
        float halfFov = _cam.fieldOfView * 0.5f * Mathf.Deg2Rad * _cam.aspect;
        var left = Quaternion.Euler(0f, -halfFov * Mathf.Rad2Deg, 0f) * fwd;
        var right = Quaternion.Euler(0f, halfFov * Mathf.Rad2Deg, 0f) * fwd;
        var p0 = ToPixel(camPos);
        DrawLine(tex, p0, ToPixel(camPos + left * 60f), Color.white);
        DrawLine(tex, p0, ToPixel(camPos + right * 60f), Color.white);
        DrawDisc(tex, p0, 7, Color.cyan);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());

        topCam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(go);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);

        foreach (var r in all)
        {
            r.sharedMaterials = saved[r];
            r.enabled = savedEnabled[r];
        }
        foreach (var kv in lodEnabled) kv.Key.enabled = kv.Value;
        foreach (var m in matCache.Values) UnityEngine.Object.DestroyImmediate(m);
    }

    private static void DrawLine(Texture2D tex, Vector2 a, Vector2 b, Color c)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b));
        for (int i = 0; i <= steps; i++)
        {
            var p = Vector2.Lerp(a, b, steps == 0 ? 0f : (float)i / steps);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = (int)p.x + dx, y = (int)p.y + dy;
                    if (x >= 0 && y >= 0 && x < tex.width && y < tex.height) tex.SetPixel(x, y, c);
                }
            }
        }
    }

    private static void DrawDisc(Texture2D tex, Vector2 center, int radius, Color c)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > radius * radius) continue;
                int x = (int)center.x + dx, y = (int)center.y + dy;
                if (x >= 0 && y >= 0 && x < tex.width && y < tex.height) tex.SetPixel(x, y, c);
            }
        }
    }

    private static void WriteReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("view,desc,drawCalls,batches,setPass,triangles,vertices,cullMs,mainThreadMs,cpuFrameMs,gpuFrameMs,statsDrawCalls,statsBatches,statsSetPass,statsTris,statsVerts,statsVisibleSkinned,visibleRenderers,inFrustum,occluded,outOfFrustum,lodGroups,lod0,lod1,lod2,lod3,lod4");
        foreach (var r in Results)
        {
            sb.AppendLine(string.Join(",", new object[]
            {
                r.View, "\"" + r.Desc + "\"", r.DrawCalls.ToString("F1"), r.Batches.ToString("F1"), r.SetPass.ToString("F1"),
                r.Triangles.ToString("F0"), r.Vertices.ToString("F0"), r.CullMs.ToString("F3"), r.MainThreadMs.ToString("F3"),
                r.CpuFrameMs.ToString("F3"), r.GpuFrameMs.ToString("F3"),
                r.StatsDrawCalls, r.StatsBatches, r.StatsSetPass, r.StatsTris, r.StatsVerts, r.StatsVisibleSkinned,
                r.VisibleRenderers, r.InFrustum, r.Occluded, r.OutOfFrustum, r.LodGroupsTotal,
                r.LodHistogram[0], r.LodHistogram[1], r.LodHistogram[2], r.LodHistogram[3], r.LodHistogram[4],
            }));
        }
        File.WriteAllText(Path.Combine(_outDir, "stats.csv"), sb.ToString(), new UTF8Encoding(true));

        var info = new StringBuilder();
        info.AppendLine("label=" + _label);
        info.AppendLine("time=" + DateTime.Now.ToString("s"));
        info.AppendLine("unity=" + Application.unityVersion);
        info.AppendLine("screen=" + Screen.width + "x" + Screen.height);
        info.AppendLine("gpu=" + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType);
        info.AppendLine("lodBias=" + QualitySettings.lodBias + " meshLodThreshold=" + QualitySettings.meshLodThreshold);
        info.AppendLine("occlusionDataBytes=" + StaticOcclusionCulling.umbraDataSize + " camUseOcclusion=" + _cam.useOcclusionCulling);
        File.WriteAllText(Path.Combine(_outDir, "env.txt"), info.ToString(), new UTF8Encoding(true));
    }
}
