using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Dungonator.Client.Shaders.FogOfWar
{
    public class FogOfWarRendererFeature : ScriptableRendererFeature
    {
        private static readonly int CameraBounds = Shader.PropertyToID("_CameraBounds");
        private static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");

        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            public Material fogMaterial;
        }

        public Settings settings = new Settings();
        private FogOfWarPass _fogPass;

        public override void Create()
        {
            _fogPass = new FogOfWarPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.fogMaterial == null) return;
            renderer.EnqueuePass(_fogPass);
        }

        class FogOfWarPass : ScriptableRenderPass
        {
            private Settings _settings;
        
            private class PassData
            {
                internal Material Material;
                internal Vector4 CameraBounds;
            }

            public FogOfWarPass(Settings settings)
            {
                this._settings = settings;
                renderPassEvent = settings.renderPassEvent;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings.fogMaterial == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                // Calculate camera bounds for orthographic camera
                var camera = cameraData.camera;
                float height = camera.orthographicSize * 2f;
                float width = height * camera.aspect;
                Vector3 camPos = camera.transform.position;
                Vector4 cameraBounds = new Vector4(
                    camPos.x - width * 0.5f,
                    camPos.y - height * 0.5f,
                    width,
                    height
                );

                var source = resourceData.activeColorTexture;
            
                if (!source.IsValid())
                    return;

                TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    cameraData.cameraTargetDescriptor,
                    "FogOfWarTemp",
                    true
                );

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fog Of War Pass", out var passData))
                {
                    passData.Material = _settings.fogMaterial;
                    passData.CameraBounds = cameraBounds;

                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.Material.SetVector(CameraBounds, data.CameraBounds);
                        data.Material.SetTexture(BlitTexture, source);
                        Blitter.BlitTexture(context.cmd, source, new Vector4(1, 1, 0, 0), data.Material, 0);
                    });
                }

                // Copy back to source
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fog Of War Copy", out var passData))
                {
                    passData.Material = null;

                    builder.UseTexture(destination, AccessFlags.Read);
                    builder.SetRenderAttachment(source, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, destination, new Vector4(1, 1, 0, 0), 0f, false);
                    });
                }
            }
        }
    }
}