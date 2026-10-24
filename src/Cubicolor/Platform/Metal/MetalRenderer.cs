using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Cubicolor.Platform.Mac;
using Cubicolor.Render;
using Cubicolor.Utilities;
using static Cubicolor.Platform.Mac.ObjC;

namespace Cubicolor.Platform.Metal;

// The renderer on macOS. One render pass a frame into a multisampled target: the scene's opaque meshes,
// then its translucent ones farthest first, then the overlay; resolved into the window's drawable, or, with no
// window (headless), into a texture of its own that ReadPixels copies out.
internal sealed unsafe class MetalRenderer : IRenderer
{
    private const int Samples = 4;
    private const PixelFormat ColorFormat = PixelFormat.BGRA8UnormSrgb;
    private const int VertexBufferIndex = 2;

    private readonly MetalDevice _device;
    private readonly IntPtr _queue;
    private readonly MetalLayer? _layer;
    private readonly Func<(int Width, int Height)> _size;

    private readonly MetalPipeline _opaque, _translucent, _depthOnly, _overlay;
    private IntPtr _lastCommands;   // the last frame's command buffer, retained, for waiting on the GPU
    private readonly IntPtr _sampler;
    private readonly MetalAtlas _atlas;
    private readonly List<(MetalBuffer Vertices, MetalBuffer Indices, int IndexCount)> _meshes = [];

    private MetalTexture _multisample, _depth, _offscreen;
    private IntPtr _lastFrame;   // the texture the last frame resolved into (retained while it is the offscreen one)

    public string Name => $"Metal ({_device.Name})";
    public int Width { get; private set; }
    public int Height { get; private set; }
    public IOverlayAtlas Atlas => _atlas;

    // With a layer, frames go to the window; without one, to an offscreen texture of `size`.
    public MetalRenderer(IntPtr layer, Func<(int Width, int Height)> size, bool vsync)
    {
        _size = size;
        _device = MetalDevice.CreateDefault();
        _queue = _device.NewCommandQueue();
        using var pool = AutoreleasePool.Push();
        if (layer != IntPtr.Zero)
        {
            _layer = new MetalLayer(layer);
            _layer.Value.Configure(_device, ColorFormat);
            _layer.Value.SetDisplaySync(vsync);
        }

        MetalLibrary scene = _device.NewLibrary(Shader("Scene.metal"), "Scene.metal");
        MetalLibrary overlay = _device.NewLibrary(Shader("Overlay.metal"), "Overlay.metal");
        _opaque = ScenePipeline(scene, "scene, opaque", ScenePass.Opaque);
        _translucent = ScenePipeline(scene, "scene, translucent", ScenePass.Translucent);
        _depthOnly = ScenePipeline(scene, "scene, depth only", ScenePass.DepthOnly);

        var descriptor = RenderPipelineDescriptor.Create("overlay", overlay.Function("overlay_vertex"), overlay.Function("overlay_fragment"));
        descriptor.ColorFormat(0, ColorFormat);
        descriptor.AlphaBlend(0);
        descriptor.DepthFormat(PixelFormat.Depth32Float);
        descriptor.SampleCount(Samples);
        descriptor.VertexLayout([(VertexFormat.Float2, 0), (VertexFormat.Float2, 8), (VertexFormat.Float4, 16)], OverlayVertex.Stride, VertexBufferIndex);
        _overlay = new MetalPipeline(_device.NewRenderPipeline(descriptor.Handle, "overlay"), _device.NewDepthState(CompareFunction.Always, false));

        _sampler = _device.NewSampler(SamplerFilter.Linear, MipFilter.NotMipmapped, AddressMode.ClampToEdge, 0);
        _atlas = new MetalAtlas(_device.NewTexture("overlay atlas", PixelFormat.R8Unorm, OverlayAtlas.Size, OverlayAtlas.Size,
            TextureUsage.ShaderRead, StorageMode.Shared), this);
        Log.Info($"Renderer: {Name}");
    }

    private enum ScenePass { Opaque, Translucent, DepthOnly }

    // A translucent mesh fades as one object: its depth first (DepthOnly, no colour), then its colour only
    // where it is the nearest surface (Translucent, depth equal), so each pixel blends it exactly once and
    // its own sides never show through one another.
    private MetalPipeline ScenePipeline(MetalLibrary library, string label, ScenePass pass)
    {
        var descriptor = RenderPipelineDescriptor.Create(label, library.Function("scene_vertex"), library.Function("scene_fragment"));
        descriptor.ColorFormat(0, ColorFormat);
        if (pass == ScenePass.Translucent) descriptor.AlphaBlend(0);
        if (pass == ScenePass.DepthOnly) descriptor.WriteMask(0, 0);
        descriptor.DepthFormat(PixelFormat.Depth32Float);
        descriptor.SampleCount(Samples);
        descriptor.VertexLayout([(VertexFormat.Float3, 0), (VertexFormat.Float3, 12)], MeshVertex.Stride, VertexBufferIndex);
        IntPtr depth = _device.NewDepthState(CompareFunction.LessEqual, pass != ScenePass.Translucent);
        return new MetalPipeline(_device.NewRenderPipeline(descriptor.Handle, label), depth);
    }

    private static string Shader(string name)
    {
        using Stream stream = typeof(MetalRenderer).Assembly.GetManifestResourceStream("Shaders/" + name)
                              ?? throw new InvalidOperationException($"The shader {name} is not embedded");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public MeshHandle Upload(MeshData mesh)
    {
        using var pool = AutoreleasePool.Push();
        MetalBuffer vertices = _device.NewBuffer(MemoryMarshal.AsBytes(mesh.Vertices.AsSpan()), "mesh vertices");
        MetalBuffer indices = _device.NewBuffer(MemoryMarshal.AsBytes(mesh.Indices.AsSpan()), "mesh indices");
        _meshes.Add((vertices, indices, mesh.Indices.Length));
        return new MeshHandle(_meshes.Count - 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameUniforms
    {
        public Matrix4x4 ViewProjection;
        public Vector4 LightDirection, Ambient, Diffuse, ViewDirection, Specular;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DrawUniforms
    {
        public Matrix4x4 Model;
        public Vector4 Color, Accent, Pattern, PaintUp;
    }

    public bool Draw(Scene scene, ReadOnlySpan<OverlayVertex> overlay)
    {
        using var pool = AutoreleasePool.Push();
        (int width, int height) = _size();
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        EnsureTargets(width, height);

        IntPtr drawable = IntPtr.Zero;
        IntPtr target;
        if (_layer is { } layer)
        {
            layer.SetDrawableSize((uint) width, (uint) height);
            drawable = layer.NextDrawable();
            if (drawable == IntPtr.Zero) return false;
            target = MetalLayer.TextureOf(drawable);
        }
        else
        {
            target = _offscreen.Handle;
        }

        var commands = MetalCommandBuffer.From(_queue, "frame");
        var pass = RenderPassDescriptor.Create();
        Vector4 background = scene.Background;
        // A transparent background (for images to lay over others) clears to premultiplied colour.
        pass.ColorResolved(0, _multisample, new MetalTexture(target, (uint) width, (uint) height), LoadAction.Clear,
            new MTLClearColor(background.X * background.W, background.Y * background.W, background.Z * background.W, background.W));
        pass.Depth(_depth, LoadAction.Clear, StoreAction.DontCare, 1.0);
        MetalRenderEncoder encoder = commands.RenderPass(pass);

        Lighting light = scene.Lighting;
        var frame = new FrameUniforms
        {
            ViewProjection = scene.View * scene.Projection,
            LightDirection = new Vector4(Vector3.Normalize(light.Direction), 0),
            Ambient = new Vector4(light.Ambient, 0),
            Diffuse = new Vector4(light.Diffuse, 0),
            ViewDirection = new Vector4(scene.ViewDirection, 0),
            Specular = new Vector4(light.Specular, light.Shininess, 0, 0),
        };
        encoder.SetVertexBytes(frame, 0);
        encoder.SetFragmentBytes(frame, 0);

        encoder.SetPipeline(_opaque);
        foreach (DrawItem item in scene.Items)
            if (item.Color.W >= 1f) DrawMesh(encoder, item);
        var translucent = new List<(float Depth, DrawItem Item)>();
        foreach (DrawItem item in scene.Items)
            if (item.Color.W < 1f && item.Color.W > 0f)
                translucent.Add((Vector3.Transform(item.Model.Translation, scene.View).Z, item));
        if (translucent.Count > 0)
        {
            // View space looks down -z: the farthest has the smallest z.
            translucent.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            foreach (var (_, item) in translucent)
            {
                encoder.SetPipeline(_depthOnly);
                DrawMesh(encoder, item);
                encoder.SetPipeline(_translucent);
                DrawMesh(encoder, item);
            }
        }

        MetalBuffer overlayBuffer = default;
        if (overlay.Length > 0)
        {
            overlayBuffer = _device.NewBuffer(MemoryMarshal.AsBytes(overlay), "overlay vertices");
            encoder.SetPipeline(_overlay);
            encoder.SetVertexBytes(new Vector2(width, height), 0);
            encoder.SetVertexBuffer(overlayBuffer, VertexBufferIndex);
            encoder.SetFragmentTexture(_atlas.Texture.Handle, _sampler, 0);
            encoder.Draw(PrimitiveType.Triangle, 0, overlay.Length);
        }
        encoder.End();

        if (drawable != IntPtr.Zero) commands.Present(drawable);
        commands.Commit();
        Release(ref _lastCommands);
        _lastCommands = objc_retain(commands.Handle);
        // The command buffer keeps what it uses alive until it completes.
        if (overlayBuffer.Handle != IntPtr.Zero)
        {
            IntPtr buffer = overlayBuffer.Handle;
            Release(ref buffer);
        }
        if (_layer == null)
        {
            commands.WaitUntilCompleted();
            if (commands.Status == CommandBufferStatus.Error) throw new InvalidOperationException($"The frame failed on the GPU: {commands.ErrorMessage}");
        }
        _lastFrame = target;
        Width = width;
        Height = height;
        return true;
    }

    private void DrawMesh(MetalRenderEncoder encoder, DrawItem item)
    {
        var (vertices, indices, count) = _meshes[item.Mesh.Id];
        var uniforms = new DrawUniforms
        {
            Model = item.Model,
            Color = item.Color,
            Accent = item.Accent,
            Pattern = new Vector4((float) item.Surface, item.Progress, 0, 0),
            PaintUp = new Vector4(item.PaintUp == Vector3.Zero ? Vector3.UnitY : item.PaintUp, 0),
        };
        encoder.SetVertexBytes(uniforms, 1);
        encoder.SetFragmentBytes(uniforms, 1);
        encoder.SetVertexBuffer(vertices, VertexBufferIndex);
        encoder.DrawIndexed16(count, indices, 0);
    }

    private void EnsureTargets(int width, int height)
    {
        if (_multisample.Handle != IntPtr.Zero && _multisample.Width == width && _multisample.Height == height) return;
        ReleaseTargets();
        _multisample = _device.NewMultisampleTexture("scene colour", ColorFormat, (uint) width, (uint) height, Samples);
        _depth = _device.NewMultisampleTexture("scene depth", PixelFormat.Depth32Float, (uint) width, (uint) height, Samples);
        if (_layer == null)
            _offscreen = _device.NewTexture("offscreen frame", ColorFormat, (uint) width, (uint) height,
                TextureUsage.RenderTarget | TextureUsage.ShaderRead, StorageMode.Private);
    }

    private void ReleaseTargets()
    {
        foreach (MetalTexture texture in new[] { _multisample, _depth, _offscreen })
        {
            IntPtr handle = texture.Handle;
            if (handle != IntPtr.Zero) Release(ref handle);
        }
        _multisample = _depth = _offscreen = default;
    }

    public byte[] ReadPixels()
    {
        if (_lastFrame == IntPtr.Zero) throw new InvalidOperationException("No frame has been drawn yet");
        using var pool = AutoreleasePool.Push();
        MetalBuffer buffer = _device.NewSharedBuffer((nuint) (Width * Height * 4));
        var commands = MetalCommandBuffer.From(_queue, "read pixels");
        MetalBlitEncoder blit = commands.Blit();
        blit.Copy(new MetalTexture(_lastFrame, (uint) Width, (uint) Height), buffer);
        blit.End();
        commands.Commit();
        commands.WaitUntilCompleted();
        byte[] pixels = buffer.Contents.ToArray();
        IntPtr handle = buffer.Handle;
        Release(ref handle);
        // BGRA to RGBA.
        for (int i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        return pixels;
    }

    public void Dispose()
    {
        ReleaseTargets();
        foreach (var (vertices, indices, _) in _meshes)
        {
            IntPtr v = vertices.Handle, i = indices.Handle;
            Release(ref v);
            Release(ref i);
        }
        _meshes.Clear();
        _opaque.Release();
        _translucent.Release();
        _depthOnly.Release();
        Release(ref _lastCommands);
        _overlay.Release();
    }

    // Waits for the last frame the GPU was given to finish.
    private void WaitForGpu()
    {
        if (_lastCommands != IntPtr.Zero) new MetalCommandBuffer(_lastCommands).WaitUntilCompleted();
    }

    // The atlas lives in memory the CPU and GPU share, and a frame still drawing may be reading it: a new
    // glyph waits for that frame first, or it would flicker in half written. Glyphs are added rarely.
    private sealed class MetalAtlas(MetalTexture texture, MetalRenderer renderer) : IOverlayAtlas
    {
        public MetalTexture Texture { get; } = texture;
        public int Size => (int) Texture.Width;

        public void Upload(int x, int y, int width, int height, ReadOnlySpan<byte> coverage)
        {
            renderer.WaitForGpu();
            Texture.ReplaceRegion(x, y, width, height, coverage, 1);
        }
    }
}
