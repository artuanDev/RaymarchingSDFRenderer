using System;
using System.Collections.Generic;
using UnityEngine;

namespace SdfRenderer
{
    [Flags]
    public enum SDFDirtyFlags
    {
        None = 0,
        Topology = 1 << 0,
        Shapes = 1 << 1,
        Modifiers = 1 << 2,
        Materials = 1 << 3,
        Bounds = 1 << 4,
        Settings = 1 << 5,
        All = ~0
    }

    public static class SDFSceneRegistry
    {
        private static readonly HashSet<SDFModel> Models = new HashSet<SDFModel>();
        private static readonly HashSet<SDFShape> Shapes = new HashSet<SDFShape>();
        private const int ChangeHistorySize = 256;
        private static readonly uint[] ChangeVersions = new uint[ChangeHistorySize];
        private static readonly SDFDirtyFlags[] ChangeFlags = new SDFDirtyFlags[ChangeHistorySize];
        private static uint s_Version = 1;
        private static SDFDirtyFlags s_DirtyFlags = SDFDirtyFlags.All;
        private static int s_BatchDepth;
        private static bool s_BatchChanged;
        private static SDFDirtyFlags s_BatchFlags;

        static SDFSceneRegistry() => RecordVersion(1, SDFDirtyFlags.All);

        public static uint Version => s_Version;
        public static SDFDirtyFlags DirtyFlags => s_DirtyFlags;
        internal static IEnumerable<SDFModel> RegisteredModels => Models;
        internal static IEnumerable<SDFShape> RegisteredShapes => Shapes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Models.Clear();
            Shapes.Clear();
            s_Version = 1;
            s_DirtyFlags = SDFDirtyFlags.All;
            s_BatchDepth = 0;
            s_BatchChanged = false;
            s_BatchFlags = SDFDirtyFlags.None;
            Array.Clear(ChangeVersions, 0, ChangeVersions.Length);
            Array.Clear(ChangeFlags, 0, ChangeFlags.Length);
            RecordVersion(1, SDFDirtyFlags.All);
        }

        internal static void Register(SDFModel model)
        {
            if (model != null && Models.Add(model))
                MarkDirty(SDFDirtyFlags.Topology);
        }

        internal static void Unregister(SDFModel model)
        {
            if (model != null && Models.Remove(model))
                MarkDirty(SDFDirtyFlags.Topology);
        }

        internal static void Register(SDFShape shape)
        {
            if (shape != null && Shapes.Add(shape))
                MarkDirty(SDFDirtyFlags.Topology);
        }

        internal static void Unregister(SDFShape shape)
        {
            if (shape != null && Shapes.Remove(shape))
                MarkDirty(SDFDirtyFlags.Topology);
        }

        public static void MarkDirty(SDFDirtyFlags flags = SDFDirtyFlags.All)
        {
            s_DirtyFlags |= flags;
            if (s_BatchDepth > 0)
            {
                s_BatchChanged = true;
                s_BatchFlags |= flags;
                return;
            }
            AdvanceVersion(flags);
        }

        internal static void ClearDirtyFlags() => s_DirtyFlags = SDFDirtyFlags.None;

        public static SDFDirtyFlags GetDirtyFlagsSince(uint version)
        {
            if (version == s_Version) return SDFDirtyFlags.None;
            uint difference = unchecked(s_Version - version);
            if (version == 0 || difference >= ChangeHistorySize)
                return SDFDirtyFlags.All;
            SDFDirtyFlags result = SDFDirtyFlags.None;
            for (uint current = version + 1; current != s_Version + 1; ++current)
            {
                int slot = (int)(current % ChangeHistorySize);
                if (ChangeVersions[slot] != current)
                    return SDFDirtyFlags.All;
                result |= ChangeFlags[slot];
            }
            return result;
        }

        public static ChangeBatch BatchChanges()
        {
            ++s_BatchDepth;
            return new ChangeBatch(true);
        }

        public static void GetRegisteredShapes(List<SDFShape> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            foreach (SDFShape shape in Shapes)
                if (shape != null) results.Add(shape);
        }

        internal static void CheckForTransformChanges()
        {
            bool changed = false;
            foreach (SDFShape shape in Shapes)
            {
                if (shape == null || !shape.transform.hasChanged)
                    continue;
                shape.transform.hasChanged = false;
                changed = true;
            }
            if (changed)
                MarkDirty(SDFDirtyFlags.Shapes | SDFDirtyFlags.Bounds);
        }

        private static void EndBatch()
        {
            if (s_BatchDepth <= 0)
                return;
            --s_BatchDepth;
            if (s_BatchDepth == 0 && s_BatchChanged)
            {
                s_BatchChanged = false;
                SDFDirtyFlags flags = s_BatchFlags;
                s_BatchFlags = SDFDirtyFlags.None;
                AdvanceVersion(flags);
            }
        }

        private static void AdvanceVersion(SDFDirtyFlags flags)
        {
            s_DirtyFlags = flags;
            unchecked { ++s_Version; }
            RecordVersion(s_Version, flags);
        }

        private static void RecordVersion(uint version, SDFDirtyFlags flags)
        {
            int slot = (int)(version % ChangeHistorySize);
            ChangeVersions[slot] = version;
            ChangeFlags[slot] = flags;
        }

        public readonly struct ChangeBatch : IDisposable
        {
            private readonly bool m_Active;
            internal ChangeBatch(bool active) => m_Active = active;
            public void Dispose() { if (m_Active) EndBatch(); }
        }
    }
}
