using UnityEngine;

namespace SdfRenderer
{
    internal static class SDFShaderIds
    {
        internal static readonly int Models = Shader.PropertyToID("_SDFModels");
        internal static readonly int Shapes = Shader.PropertyToID("_SDFShapes");
        internal static readonly int Modifiers = Shader.PropertyToID("_SDFModifiers");
        internal static readonly int Materials = Shader.PropertyToID("_SDFMaterials");
        internal static readonly int ViewProjection = Shader.PropertyToID("_SDFViewProjection");
        internal static readonly int CameraPosition = Shader.PropertyToID("_SDFCameraPosition");
        internal static readonly int CameraForward = Shader.PropertyToID("_SDFCameraForward");
        internal static readonly int CameraNear = Shader.PropertyToID("_SDFCameraNear");
        internal static readonly int Orthographic = Shader.PropertyToID("_SDFOrthographic");
        internal static readonly int SceneView = Shader.PropertyToID("_SDFSceneView");
        internal static readonly int PixelWorldScale = Shader.PropertyToID("_SDFPixelWorldScale");
        internal static readonly int MaxSteps = Shader.PropertyToID("_SDFMaxSteps");
        internal static readonly int MaxDistance = Shader.PropertyToID("_SDFMaxDistance");
        internal static readonly int StepSafety = Shader.PropertyToID("_SDFStepSafety");
        internal static readonly int SurfaceEpsilon = Shader.PropertyToID("_SDFSurfaceEpsilon");
        internal static readonly int NormalEpsilon = Shader.PropertyToID("_SDFNormalEpsilon");
        internal static readonly int PixelTolerance = Shader.PropertyToID("_SDFPixelTolerance");
        internal static readonly int PassMode = Shader.PropertyToID("_SDFPassMode");
        internal static readonly int PreviewMode = Shader.PropertyToID("_SDFPreviewMode");
        internal static readonly int ModelCount = Shader.PropertyToID("_SDFModelCount");
        internal static readonly int ShadowCascadeCount = Shader.PropertyToID("_SDFShadowCascadeCount");
        internal static readonly int ReceiveUrpShadows = Shader.PropertyToID("_SDFReceiveUrpShadows");
        internal static readonly int SelfShadows = Shader.PropertyToID("_SDFSelfShadows");
        internal static readonly int ShadowMaxSteps = Shader.PropertyToID("_SDFShadowMaxSteps");
        internal static readonly int ShadowMaxDistance = Shader.PropertyToID("_SDFShadowMaxDistance");
        internal static readonly int ShadowStepSafety = Shader.PropertyToID("_SDFShadowStepSafety");
        internal static readonly int ShadowBias = Shader.PropertyToID("_SDFShadowBias");
        internal static readonly int ShadowStrength = Shader.PropertyToID("_SDFShadowStrength");
        internal static readonly int UseUrpScreenSpaceAo = Shader.PropertyToID("_SDFUseUrpScreenSpaceAO");
        internal static readonly int AmbientOcclusionEnabled = Shader.PropertyToID("_SDFAmbientOcclusionEnabled");
        internal static readonly int AmbientOcclusionStrength = Shader.PropertyToID("_SDFAmbientOcclusionStrength");
        internal static readonly int AmbientColor = Shader.PropertyToID("_SDFAmbientColor");
        internal static readonly int LightDirection = Shader.PropertyToID("_SDFLightDirection");
        internal static readonly int LightColor = Shader.PropertyToID("_SDFLightColor");
        internal static readonly int[] Textures =
        {
            Shader.PropertyToID("_SDFTexture0"), Shader.PropertyToID("_SDFTexture1"),
            Shader.PropertyToID("_SDFTexture2"), Shader.PropertyToID("_SDFTexture3"),
            Shader.PropertyToID("_SDFTexture4"), Shader.PropertyToID("_SDFTexture5"),
            Shader.PropertyToID("_SDFTexture6"), Shader.PropertyToID("_SDFTexture7"),
            Shader.PropertyToID("_SDFTexture8"), Shader.PropertyToID("_SDFTexture9"),
            Shader.PropertyToID("_SDFTexture10"), Shader.PropertyToID("_SDFTexture11"),
            Shader.PropertyToID("_SDFTexture12"), Shader.PropertyToID("_SDFTexture13"),
            Shader.PropertyToID("_SDFTexture14"), Shader.PropertyToID("_SDFTexture15")
        };
    }
}
