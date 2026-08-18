using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace SdfRenderer.Editor
{
    public sealed class SDFEditorPerformanceWindow : EditorWindow
    {
        private static readonly SDFBenchmarkAnimation[] Modes =
        {
            SDFBenchmarkAnimation.None,
            SDFBenchmarkAnimation.Positions | SDFBenchmarkAnimation.Rotations | SDFBenchmarkAnimation.Scales,
            SDFBenchmarkAnimation.Operations | SDFBenchmarkAnimation.Modifiers,
            SDFBenchmarkAnimation.Everything,
            SDFBenchmarkAnimation.Everything,
            SDFBenchmarkAnimation.Everything
        };

        private static readonly string[] Labels =
        {
            "Static + Scene orbit", "Animated transforms", "Animated operations + modifiers",
            "Everything", "Everything + continuous selection", "Everything + inspector-style editing"
        };

        [SerializeField] private SDFBenchmarkController m_Controller;
        [SerializeField] private int m_WarmupRepaints = 30;
        [SerializeField] private int m_SampleRepaints = 120;
        private readonly FrameTiming[] m_Timings = new FrameTiming[1];
        private readonly List<SDFShape> m_Shapes = new List<SDFShape>(1024);
        private StringBuilder m_Report;
        private SceneView m_TargetView;
        private Quaternion m_OriginalRotation;
        private UnityEngine.Object[] m_OriginalSelection;
        private SDFBenchmarkAnimation m_OriginalAnimation;
        private bool m_OriginalPreview;
        private SDFShape m_EditedShape;
        private float m_OriginalRadius;
        private ProfilerRecorder m_GcRecorder;
        private bool m_Running;
        private bool m_Sampling;
        private int m_ModeIndex;
        private int m_PhaseRepaints;
        private int m_TotalRepaints;
        private int m_SelectionIndex;
        private double m_StartTime;
        private double m_CpuTotal;
        private double m_GpuTotal;
        private int m_CpuSamples;
        private int m_GpuSamples;
        private long m_GcTotal;
        private long m_UploadTotal;
        private long m_ShapeTotal;
        private long m_ModelTotal;
        private long m_OperationTotal;
        private long m_ModifierTotal;
        private long m_BoundsTotal;

        [MenuItem("Tools/SDF/Editor Performance Benchmark")]
        private static void Open() => GetWindow<SDFEditorPerformanceWindow>("SDF Editor Benchmark");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Measures the complete native-resolution Scene view. Open a Game view or a second Scene view before starting to include those simultaneous-view cases.",
                MessageType.Info);
            m_Controller = (SDFBenchmarkController)EditorGUILayout.ObjectField(
                "Benchmark Controller", m_Controller, typeof(SDFBenchmarkController), true);
            m_WarmupRepaints = Mathf.Max(1, EditorGUILayout.IntField("Warmup Repaints", m_WarmupRepaints));
            m_SampleRepaints = Mathf.Max(1, EditorGUILayout.IntField("Sample Repaints", m_SampleRepaints));
            using (new EditorGUI.DisabledScope(m_Running))
            {
                if (GUILayout.Button("Run Complete Scene-view Sweep"))
                    StartSweep();
            }
            using (new EditorGUI.DisabledScope(!m_Running))
            {
                if (GUILayout.Button("Stop and Restore Editor State"))
                    StopSweep(false);
            }
            if (m_Running)
                EditorGUILayout.LabelField("Running", Labels[m_ModeIndex]);
        }

        private void StartSweep()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Run the Scene-view benchmark outside Play Mode.");
                return;
            }
            if (m_Controller == null)
                m_Controller = FindFirstObjectByType<SDFBenchmarkController>();
            m_TargetView = SceneView.lastActiveSceneView;
            if (m_Controller == null || m_TargetView == null)
            {
                Debug.LogWarning("The Editor benchmark requires an active SDFBenchmarkController and Scene view.");
                return;
            }

            m_OriginalAnimation = m_Controller.Animation;
            m_OriginalPreview = m_Controller.PreviewInEditMode;
            m_OriginalRotation = m_TargetView.rotation;
            m_OriginalSelection = Selection.objects;
            SDFSceneRegistry.GetRegisteredShapes(m_Shapes);
            m_EditedShape = m_Shapes.Count > 0 ? m_Shapes[0] : null;
            m_OriginalRadius = m_EditedShape != null ? m_EditedShape.Radius : 0f;
            m_Report = new StringBuilder(2048);
            m_Report.AppendLine($"SDF Editor benchmark: sceneViews={SceneView.sceneViews.Count}, " +
                $"gameViewOpen={HasOpenGameView()}, shapes={m_Shapes.Count}");
            m_Report.AppendLine("Case,Editor FPS,CPU ms,GPU ms,GC bytes/frame,Upload KiB/frame,Shapes/frame,Models/frame,Operations/frame,Modifiers/frame,Bounds/frame,Scene repaints/sec");
            m_GcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            m_Running = true;
            m_ModeIndex = 0;
            BeginMode();
            EditorApplication.update += BenchmarkUpdate;
            SceneView.duringSceneGui += CountSceneRepaint;
        }

        private void BeginMode()
        {
            m_Controller.PreviewInEditMode = true;
            m_Controller.Animation = Modes[m_ModeIndex];
            m_Sampling = false;
            m_PhaseRepaints = 0;
            m_TotalRepaints = 0;
            ClearTotals();
        }

        private void BenchmarkUpdate()
        {
            if (!m_Running || m_Controller == null || m_TargetView == null)
            {
                StopSweep(false);
                return;
            }
            if (m_ModeIndex == 0)
                m_TargetView.rotation = Quaternion.AngleAxis(0.35f, Vector3.up) * m_TargetView.rotation;
            else if (m_ModeIndex == 4 && m_Shapes.Count > 0 && (m_PhaseRepaints & 3) == 0)
                Selection.activeGameObject = m_Shapes[m_SelectionIndex++ % m_Shapes.Count].gameObject;
            else if (m_ModeIndex == 5 && m_EditedShape != null)
                m_EditedShape.Radius = m_OriginalRadius + ((m_PhaseRepaints & 1) == 0 ? 0.001f : 0f);

            if (m_Sampling)
                FrameTimingManager.CaptureFrameTimings();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        private void CountSceneRepaint(SceneView view)
        {
            if (!m_Running || Event.current == null || Event.current.type != EventType.Repaint)
                return;
            ++m_TotalRepaints;
            if (view != m_TargetView)
                return;
            ++m_PhaseRepaints;
            if (!m_Sampling)
            {
                if (m_PhaseRepaints < m_WarmupRepaints)
                    return;
                m_Sampling = true;
                m_PhaseRepaints = 0;
                m_TotalRepaints = 0;
                m_StartTime = EditorApplication.timeSinceStartup;
                ClearTotals();
                return;
            }

            uint timingCount = FrameTimingManager.GetLatestTimings(1, m_Timings);
            if (timingCount > 0 && m_Timings[0].cpuFrameTime > 0.0)
            {
                m_CpuTotal += m_Timings[0].cpuFrameTime;
                ++m_CpuSamples;
            }
            if (timingCount > 0 && m_Timings[0].gpuFrameTime > 0.0)
            {
                m_GpuTotal += m_Timings[0].gpuFrameTime;
                ++m_GpuSamples;
            }
            if (m_GcRecorder.Valid)
                m_GcTotal += m_GcRecorder.LastValue;
            SDFPerformanceSnapshot snapshot = SDFPerformanceMetrics.CurrentFrame;
            m_UploadTotal += snapshot.UploadBytes;
            m_ShapeTotal += snapshot.ShapesRefreshed;
            m_ModelTotal += snapshot.ModelsRefreshed;
            m_OperationTotal += snapshot.OperationsRefreshed;
            m_ModifierTotal += snapshot.ModifiersRefreshed;
            m_BoundsTotal += snapshot.BoundsRefreshed;
            if (m_PhaseRepaints >= m_SampleRepaints)
                FinishMode();
        }

        private void FinishMode()
        {
            double elapsed = Math.Max(0.000001, EditorApplication.timeSinceStartup - m_StartTime);
            double frames = m_SampleRepaints;
            m_Report.AppendLine($"{Labels[m_ModeIndex]},{frames / elapsed:F2}," +
                $"{(m_CpuSamples > 0 ? m_CpuTotal / m_CpuSamples : 0.0):F3}," +
                $"{(m_GpuSamples > 0 ? m_GpuTotal / m_GpuSamples : 0.0):F3}," +
                $"{m_GcTotal / frames:F2},{m_UploadTotal / frames / 1024.0:F2}," +
                $"{m_ShapeTotal / frames:F2},{m_ModelTotal / frames:F2},{m_OperationTotal / frames:F2}," +
                $"{m_ModifierTotal / frames:F2},{m_BoundsTotal / frames:F2},{m_TotalRepaints / elapsed:F2}");
            ++m_ModeIndex;
            if (m_ModeIndex >= Modes.Length)
                StopSweep(true);
            else
                BeginMode();
        }

        private void ClearTotals()
        {
            m_CpuTotal = m_GpuTotal = 0.0;
            m_CpuSamples = m_GpuSamples = 0;
            m_GcTotal = m_UploadTotal = m_ShapeTotal = m_ModelTotal = 0L;
            m_OperationTotal = m_ModifierTotal = m_BoundsTotal = 0L;
        }

        private void StopSweep(bool completed)
        {
            if (!m_Running)
                return;
            EditorApplication.update -= BenchmarkUpdate;
            SceneView.duringSceneGui -= CountSceneRepaint;
            m_GcRecorder.Dispose();
            if (m_Controller != null)
            {
                m_Controller.Animation = m_OriginalAnimation;
                m_Controller.PreviewInEditMode = m_OriginalPreview;
            }
            if (m_TargetView != null)
                m_TargetView.rotation = m_OriginalRotation;
            if (m_EditedShape != null)
                m_EditedShape.Radius = m_OriginalRadius;
            Selection.objects = m_OriginalSelection ?? Array.Empty<UnityEngine.Object>();
            m_Running = false;
            if (completed)
                Debug.Log(m_Report.ToString(), m_Controller);
            Repaint();
        }

        private void OnDisable() => StopSweep(false);

        private static bool HasOpenGameView()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            return gameViewType != null && Resources.FindObjectsOfTypeAll(gameViewType).Length > 0;
        }
    }
}
