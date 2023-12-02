﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.ToonRP.Shadows.Blobs
{
    public abstract class DynamicBlobShadowsMesh
    {
        protected static readonly VertexAttributeDescriptor[] VertexAttributeDescriptorsDefault =
        {
            new(VertexAttribute.Position, VertexAttributeFormat.Float16, 2),
            new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
        };

        protected static readonly VertexAttributeDescriptor[] VertexAttributeDescriptorsParams =
        {
            new(VertexAttribute.Position, VertexAttributeFormat.Float16, 2),
            new(VertexAttribute.Color, VertexAttributeFormat.Float16, 4),
            new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
        };

        protected static readonly List<ushort> TempIndices = new();

        protected static readonly ProfilerMarker FillBuffersMarker =
            new("BlobShadows.FillBuffers");
        protected static readonly ProfilerMarker FillBuffersBuildBatchesMarker =
            new("BlobShadows.FillBuffers.BuildBatches");
        protected static readonly ProfilerMarker FillBuffersBuildMeshMarker =
            new("BlobShadows.FillBuffers.BuildMesh");
        protected static readonly ProfilerMarker UploadBuffersMarker =
            new("BlobShadows.UploadBuffers");

        protected static readonly Vector2[] QuadVertices =
        {
            new(-1, -1),
            new(1, -1),
            new(1, 1),
            new(-1, 1),
        };

        public abstract ToonBlobShadowType ShadowType { get; }

        public List<int> UsedRenderers { get; } = new();
        public List<BatchData> Batches { get; } = new();

        [CanBeNull]
        public abstract Mesh Construct(List<ToonBlobShadowsRendererData> renderers, Bounds2D bounds);

        public struct Vertex
        {
            // ReSharper disable once NotAccessedField.Local
            public Vector2Half Position;
            // ReSharper disable once NotAccessedField.Local
            public Vector2Half UV;
        }

        public struct VertexParams
        {
            // ReSharper disable once NotAccessedField.Local
            public Vector2Half Position;
            // ReSharper disable once NotAccessedField.Local
            public Vector4Half Params;
            // ReSharper disable once NotAccessedField.Local
            public Vector2Half UV;
        }

        public struct Vector2Half
        {
            // ReSharper disable once NotAccessedField.Local
            public ushort X;
            // ReSharper disable once NotAccessedField.Local
            public ushort Y;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector2Half FromVector2(Vector2 vector) =>
                new()
                {
                    X = Mathf.FloatToHalf(vector.x),
                    Y = Mathf.FloatToHalf(vector.y),
                };
        }

        public struct Vector4Half
        {
            // ReSharper disable once NotAccessedField.Local
            public ushort X;
            // ReSharper disable once NotAccessedField.Local
            public ushort Y;
            // ReSharper disable once NotAccessedField.Local
            public ushort Z;
            // ReSharper disable once NotAccessedField.Local
            public ushort W;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector4Half FromVector4(Vector4 vector) =>
                new()
                {
                    X = Mathf.FloatToHalf(vector.x),
                    Y = Mathf.FloatToHalf(vector.y),
                    Z = Mathf.FloatToHalf(vector.z),
                    W = Mathf.FloatToHalf(vector.w),
                };
        }

        public struct BatchData
        {
            public Texture2D BakedShadowTexture;
            public List<int> Renderers;
            public int VertexCount;
            public int IndexCount;
        }
    }

    public abstract class DynamicBlobShadowsMesh<TVertex> : DynamicBlobShadowsMesh where TVertex : struct
    {
        private const MeshUpdateFlags MeshUpdateFlags = UnityEngine.Rendering.MeshUpdateFlags.DontResetBoneBounds |
                                                        UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds |
                                                        UnityEngine.Rendering.MeshUpdateFlags.DontNotifyMeshUsers;
        private const IndexFormat IndexFormat = UnityEngine.Rendering.IndexFormat.UInt16;


        private static readonly List<TVertex> TempVertices = new();

        private Bounds2D _bounds;
        private Vector2 _inverseWorldSize;
        private int _lastIndexCount = -1;
        private int _lastVertexCount = -1;
        private Mesh _mesh;

        protected abstract VertexAttributeDescriptor[] VertexAttributeDescriptors { get; }

        private void EnsureMeshIsCreated()
        {
            if (_mesh != null)
            {
                return;
            }

            _mesh = new Mesh
            {
                name = $"Dynamic Blob Shadows Mesh ({ShadowType.ToString()})",
            };
            _mesh.MarkDynamic();
        }

        public sealed override Mesh Construct(List<ToonBlobShadowsRendererData> renderers, Bounds2D bounds)
        {
            _bounds = bounds;

            Prepare();
            FillBuffers(renderers);

            if (UsedRenderers.Count > 0)
            {
                EnsureMeshIsCreated();
                UploadBuffers();
                Cleanup();
                return _mesh;
            }

            Cleanup();
            return null;
        }

        private void Prepare()
        {
            UsedRenderers.Clear();
            Batches.Clear();

            TempVertices.Clear();
            TempIndices.Clear();

            _inverseWorldSize = _bounds.Size;
            _inverseWorldSize.x = 1.0f / _inverseWorldSize.x;
            _inverseWorldSize.y = 1.0f / _inverseWorldSize.y;
        }

        private void FillBuffers(List<ToonBlobShadowsRendererData> renderers)
        {
            using ProfilerMarker.AutoScope profilerScope = FillBuffersMarker.Auto();

            ToonBlobShadowType shadowType = ShadowType;

            using (FillBuffersBuildBatchesMarker.Auto())
            {
                for (int index = 0; index < renderers.Count; index++)
                {
                    ToonBlobShadowsRendererData renderer = renderers[index];
                    if (renderer.ShadowType != shadowType)
                    {
                        continue;
                    }

                    UsedRenderers.Add(index);

                    int thisBatchIndex = -1;

                    for (int batchIndex = 0; batchIndex < Batches.Count; batchIndex++)
                    {
                        BatchData batchData = Batches[batchIndex];
                        if (ReferenceEquals(batchData.BakedShadowTexture, renderer.BakedShadowTexture))
                        {
                            thisBatchIndex = batchIndex;
                            break;
                        }
                    }

                    if (thisBatchIndex < 0)
                    {
                        thisBatchIndex = Batches.Count;
                        Batches.Add(new BatchData
                            {
                                BakedShadowTexture = renderer.BakedShadowTexture,
                                Renderers = ListPool<int>.Get(),
                            }
                        );
                    }

                    Batches[thisBatchIndex].Renderers.Add(index);
                }
            }

            using (FillBuffersBuildMeshMarker.Auto())
            {
                for (int index = 0; index < Batches.Count; index++)
                {
                    BatchData batchData = Batches[index];

                    foreach (int rendererIndex in batchData.Renderers)
                    {
                        ToonBlobShadowsRendererData renderer = renderers[rendererIndex];

                        // vertices
                        var translation = new float2(renderer.Position.x, renderer.Position.y);
                        translation = WorldToHClip(translation);

                        int baseVertexIndex = batchData.VertexCount;

                        AddVertex(QuadVertices[0], translation, renderer.HalfSize, renderer.Params);
                        AddVertex(QuadVertices[1], translation, renderer.HalfSize, renderer.Params);
                        AddVertex(QuadVertices[2], translation, renderer.HalfSize, renderer.Params);
                        AddVertex(QuadVertices[3], translation, renderer.HalfSize, renderer.Params);
                        batchData.VertexCount += 4;

                        // indices
                        AddIndex(baseVertexIndex + 0);
                        AddIndex(baseVertexIndex + 1);
                        AddIndex(baseVertexIndex + 2);

                        AddIndex(baseVertexIndex + 2);
                        AddIndex(baseVertexIndex + 3);
                        AddIndex(baseVertexIndex + 0);
                        batchData.IndexCount += 6;
                    }

                    Batches[index] = batchData;
                }
            }
        }

        private void UploadBuffers()
        {
            using ProfilerMarker.AutoScope profilerScope = UploadBuffersMarker.Auto();

            int vertexCount = TempVertices.Count;
            if (vertexCount != _lastVertexCount)
            {
                _mesh.SetVertexBufferParams(vertexCount, VertexAttributeDescriptors);
            }

            _mesh.SetVertexBufferData(TempVertices, 0, 0, vertexCount, 0, MeshUpdateFlags);
            _lastVertexCount = vertexCount;

            int indexCount = TempIndices.Count;
            if (indexCount != _lastIndexCount)
            {
                _mesh.SetIndexBufferParams(indexCount, IndexFormat);
            }

            _mesh.SetIndexBufferData(TempIndices, 0, 0, indexCount, MeshUpdateFlags);
            _lastIndexCount = indexCount;

            using (ListPool<SubMeshDescriptor>.Get(out List<SubMeshDescriptor> subMeshDescriptors))
            {
                int baseVertex = 0;
                int indexStart = 0;

                foreach (BatchData batchData in Batches)
                {
                    subMeshDescriptors.Add(new SubMeshDescriptor
                        {
                            bounds = default,
                            topology = MeshTopology.Triangles,
                            baseVertex = baseVertex,
                            firstVertex = 0,
                            indexCount = batchData.IndexCount,
                            indexStart = indexStart,
                            vertexCount = batchData.VertexCount,
                        }
                    );

                    baseVertex += batchData.VertexCount;
                    indexStart += batchData.IndexCount;
                }

                _mesh.SetSubMeshes(subMeshDescriptors, MeshUpdateFlags);
            }
        }

        private void Cleanup()
        {
            foreach (BatchData batchData in Batches)
            {
                ListPool<int>.Release(batchData.Renderers);
                batchData.Renderers.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddVertex(Vector2 originalVertex, Vector2 translation, float halfSize, Vector4 @params)
        {
            ComputePositionAndUv(originalVertex, translation, halfSize, out Vector2 position, out Vector2 uv);
            TempVertices.Add(BuildVertex(position, uv, @params));
        }

        protected abstract TVertex BuildVertex(Vector2 position, Vector2 uv, Vector4 @params);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ComputePositionAndUv(Vector2 originalVertex, Vector2 translation, float halfSize,
            out Vector2 position, out Vector2 uv
        )
        {
            float size = halfSize * 2.0f;
            Vector2 scale = _inverseWorldSize * size;

            position = originalVertex * scale + translation;
            uv = originalVertex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector2 WorldToHClip(Vector2 position)
        {
            Vector2 boundsMin = _bounds.Min;
            Vector2 boundsMax = _bounds.Max;
            float x = InverseLerpUnclamped(boundsMin.x, boundsMax.x, position.x);
            x = (x - 0.5f) * 2.0f;
            float y = InverseLerpUnclamped(boundsMin.y, boundsMax.y, position.y);
            y = (y - 0.5f) * 2.0f;

            if (SystemInfo.graphicsUVStartsAtTop)
            {
                y *= -1.0f;
            }

            return new Vector2(x, y);
        }

        private static float InverseLerpUnclamped(float a, float b, float value) =>
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            a != b ? (value - a) / (b - a) : 0.0f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddIndex(int index)
        {
            TempIndices.Add((ushort) index);
        }
    }
}