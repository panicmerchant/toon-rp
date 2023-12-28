﻿using System;
using UnityEngine;

namespace DELTation.ToonRP.Extensions.BuiltIn
{
    [Serializable]
    public struct ToonInvertedHullOutlineSettings
    {
        public enum NormalsSource
        {
            Normals,
            UV2,
            Tangents,
        }

        public enum VertexColorThicknessSource
        {
            None,
            R,
            G,
            B,
            A,
        }

        public Pass[] Passes;

        [Serializable]
        public struct Pass
        {
            public string Name;
            public LayerMask LayerMask;
            public StencilLayer StencilLayer;
            [ColorUsage(false, true)]
            public Color Color;
            [Min(0f)]
            public float Thickness;
            public bool FixedScreenSpaceThickness;
            [Min(0f)]
            public float NoiseAmplitude;
            [Min(0f)]
            public float NoiseFrequency;
            public Material OverrideMaterial;
            public NormalsSource NormalsSource;
            public VertexColorThicknessSource VertexColorThicknessSource;
            public float DepthBias;
            [Min(0f)]
            public float MaxDistance;
            [Range(0.001f, 1f)]
            public float DistanceFade;
            public PrePassMode PrePassIgnoreMask;
            public ToonCameraOverrideSettings CameraOverrides;

            public bool IsNoiseEnabled => NoiseAmplitude > 0.0f && NoiseFrequency > 0.0f;

            public bool IsDistanceFadeEnabled => MaxDistance > 0.0f;
        }
    }
}