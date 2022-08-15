using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AsperitasRaymarchRenderFeature : ScriptableRendererFeature {

    class AsperitasPass : ScriptableRenderPass {

        RenderTargetIdentifier cameraColorTarget; // the target to render to
        Material material;

        public AsperitasPass() {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public void SetTarget(RenderTargetIdentifier colorHandle) {
            cameraColorTarget = colorHandle;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
            ConfigureTarget(new RenderTargetIdentifier(cameraColorTarget, 0, CubemapFace.Unknown, -1));
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {

            var camera = renderingData.cameraData.camera;

            if (camera.cameraType != CameraType.Game)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(name: "AsperitasPass");

            cmd.SetRenderTarget(new RenderTargetIdentifier(cameraColorTarget, 0, CubemapFace.Unknown, -1));
            //The RenderingUtils.fullscreenMesh argument specifies that the mesh to draw is a quad.
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, material);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

        }
    }


    [Header("Height Multiplier")]
    public float heightMultiplier = 10;

    [Header("Raymarch Settings")]
    public int stepCount = 30;
    [Range(1, 100)]
    public float stepSize = 10;
    public float cloudThickness = 1000;
    [Range(0, 1)]
    public float distanceDamping = 0.8f;

    [Header("Cloud Settings")]
    public Texture2D blueNoise; // used to randomly off set the ray origin to reduce layered artifact

    [Header("Base Noise")]
    public Vector3 baseNoiseOffset;
    public float baseNoiseScale = 1;

    [Header("Detail Noise")]
    public Vector3 detailNoiseOffset;
    public float detailNoiseScale = 1;

    [Header("Density Modifiers")]
    public float densityMultiplier = 1;
    [Range(0, 5)]
    public float globalCoverageMultiplier;

    [Header("Lighting")]
    public Light sunLight;
    [Range(0, 1)]
    public float darknessThreshold = 0;
    public float lightAbsorptionThroughCloud = 1;
    public float lightAbsorptionTowardSun = 1;
    [Range(0, 1)]
    public float forwardScattering = .83f;
    [Range(0, 1)]
    public float backScattering = .3f;
    [Range(0, 1)]
    public float baseBrightness = 0.5f;
    [Range(0, 1)]
    public float phaseFactor = .15f;

    public Material material;


    AsperitasPass asperitasPass;
    public override void Create() {
        asperitasPass = new AsperitasPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        renderer.EnqueuePass(asperitasPass);

    }
}
