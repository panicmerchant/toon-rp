﻿using UnityEngine;
using UnityEngine.Rendering;

namespace ToonRP.Runtime.PostProcessing
{
    public class ToonPostProcessing
    {
        private const string BufferName = "Post-Processing";

        private static readonly int PostProcessingBufferId = Shader.PropertyToID("_ToonRP_PostProcessing");

        private readonly CommandBuffer _cmd = new() { name = BufferName };
        private ToonBloom _bloom;
        private Camera _camera;
        private RenderTextureFormat _colorFormat;
        private ScriptableRenderContext _context;
        private int _rtHeight;
        private int _rtWidth;
        private ToonPostProcessingSettings _settings;

        public bool IsActive => _settings.Enabled && _camera.cameraType <= CameraType.SceneView;

        public void Setup(in ScriptableRenderContext context, in ToonPostProcessingSettings settings,
            RenderTextureFormat colorFormat, Camera camera, int rtWidth, int rtHeight)
        {
            _colorFormat = colorFormat;
            _context = context;
            _settings = settings;
            _camera = camera;
            _rtWidth = rtWidth;
            _rtHeight = rtHeight;

            SetupBloom();
        }

        private void SetupBloom()
        {
            if (!_settings.Bloom.Enabled)
            {
                return;
            }

            _bloom ??= new ToonBloom();
            _bloom.Setup(_settings.Bloom, _colorFormat, _rtWidth, _rtHeight);
        }

        public void Render(int width, int height, RenderTextureFormat format, int sourceId,
            RenderTargetIdentifier destination)
        {
            if (!IsActive)
            {
                return;
            }

            RenderTargetIdentifier currentBuffer = sourceId;

            _cmd.GetTemporaryRT(PostProcessingBufferId, width, height, 0, FilterMode.Point, format,
                RenderTextureReadWrite.Linear
            );

            if (_settings.Bloom.Enabled)
            {
                _bloom.Render(_cmd, sourceId, PostProcessingBufferId);
                currentBuffer = PostProcessingBufferId;
            }

            if (currentBuffer != destination)
            {
                const string sampleName = "Blit Post-Processing Result";
                _cmd.BeginSample(sampleName);
                _cmd.Blit(currentBuffer, destination);
                _cmd.EndSample(sampleName);
            }

            _cmd.ReleaseTemporaryRT(PostProcessingBufferId);

            _context.ExecuteCommandBuffer(_cmd);
            _cmd.Clear();
        }
    }
}