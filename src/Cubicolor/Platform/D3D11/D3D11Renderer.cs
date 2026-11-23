using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Cubicolor.Platform.Windows;
using Cubicolor.Render;
using Cubicolor.Utilities;

namespace Cubicolor.Platform.D3D11;

// The renderer on Windows. One pass a frame into a multisampled sRGB target: the underlay, the scene's opaque
// meshes, then its translucent ones farthest first, then the overlay; resolved into a single-sampled sRGB
// texture, which is copied into the window's swap chain or, with no window (headless), read back by ReadPixels.
// (A flip-model swap chain can't have an sRGB format, so the resolve, which must average in linear light, goes
// to a texture of its own; the copy then moves the encoded bytes as they are.)
//
// The shaders ship as HLSL source and are compiled when the renderer starts, by the compiler every Windows
// since 8.1 has (d3dcompiler_47.dll), as Metal compiles its own on macOS.
internal sealed unsafe partial class D3D11Renderer : IRenderer
{
    private const int Samples = 4;
    private const Format ColorFormat = Format.B8G8R8A8UNormSrgb;

    private readonly IntPtr _device, _context, _swapChain;
    private readonly Device _d;
    private readonly Context _c;
    private readonly Func<(int Width, int Height)> _size;
    private readonly bool _vsync, _tearing;
    private readonly string _adapter;

    private readonly IntPtr _sceneVertex, _scenePixel, _sceneLayout, _overlayVertex, _overlayPixel, _overlayLayout;
    private readonly IntPtr _opaqueBlend, _alphaBlend, _noColor, _depthWrite, _depthTest, _depthAlways, _rasterizer, _sampler;
    private readonly IntPtr _frameConstants, _drawConstants, _viewportConstants;
    private readonly IntPtr _atlasTexture, _atlasView;
    private readonly D3D11Atlas _atlas;
    private readonly List<(IntPtr Vertices, IntPtr Indices, int IndexCount)> _meshes = [];
    private readonly IntPtr _infoQueue;

    private IntPtr _multisample, _multisampleView, _depth, _depthView, _resolved;
    private int _targetWidth, _targetHeight, _swapWidth, _swapHeight;
    private IntPtr _overlayBuffer;
    private int _overlayCapacity;
    private OverlayVertex[] _overlayVertices = [];
    private IntPtr _disjoint, _timeStart, _timeEnd;
    private bool _drawn;

    public string Name => $"Direct3D 11 ({_adapter})";
    public int Width { get; private set; }
    public int Height { get; private set; }
    public IOverlayAtlas Atlas => _atlas;
    public RenderStats Stats { get; private set; }
    private int _drawCalls, _triangles;

    // With a window, frames go to a swap chain on it; without one, to an offscreen texture of `size`.
    public D3D11Renderer(IntPtr window, Func<(int Width, int Height)> size, bool vsync)
    {
        _size = size;
        _vsync = vsync;
        (_device, _context) = CreateDevice(Environment.GetEnvironmentVariable("D3D11_DEBUG") == "1");
        _d = new Device(_device);
        _c = new Context(_context);
        _infoQueue = Com.Query(_device, new Guid("6543DBB6-1B48-42F5-AB82-E97EC74326F6"));   // ID3D11InfoQueue, with the debug layer only

        // The adapter (for the log) and the factory that made it (for the swap chain).
        IntPtr dxgiDevice = Com.Query(_device, new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C")), adapter = IntPtr.Zero, factory = IntPtr.Zero;
        try
        {
            // IDXGIDevice::GetAdapter (7), IDXGIAdapter::GetDesc (8), IDXGIObject::GetParent (6)
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>) Com.Slot(dxgiDevice, 7))(dxgiDevice, &adapter), "GetAdapter");
            byte* desc = stackalloc byte[512];
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, byte*, int>) Com.Slot(adapter, 8))(adapter, desc), "GetDesc");
            _adapter = new string((char*) desc);   // DXGI_ADAPTER_DESC begins with the name, 128 UTF-16 units
            // IDXGIDevice1::SetMaximumFrameLatency (12): one frame queued, so input shows on the next refresh.
            IntPtr dxgiDevice1 = Com.Query(_device, new Guid("77DB970F-6276-48BA-BA28-070143B4392C"));
            if (dxgiDevice1 != IntPtr.Zero)
            {
                ((delegate* unmanaged[Stdcall]<IntPtr, uint, int>) Com.Slot(dxgiDevice1, 12))(dxgiDevice1, 1);
                Com.Release(ref dxgiDevice1);
            }
            if (window != IntPtr.Zero)
            {
                Guid factoryIid = new("50C83A1C-E072-4C48-87B0-3630FA36A6D0");   // IDXGIFactory2
                Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>) Com.Slot(adapter, 6))(adapter, &factoryIid, &factory), "GetParent");
                _tearing = !vsync && TearingSupported(factory);
                _swapChain = CreateSwapChain(factory, window);
            }
        }
        finally
        {
            Com.Release(ref factory);
            Com.Release(ref adapter);
            Com.Release(ref dxgiDevice);
        }

        byte[] sceneVs = Compile("Scene.hlsl", "scene_vertex", "vs_4_0"), scenePs = Compile("Scene.hlsl", "scene_fragment", "ps_4_0");
        byte[] overlayVs = Compile("Overlay.hlsl", "overlay_vertex", "vs_4_0"), overlayPs = Compile("Overlay.hlsl", "overlay_fragment", "ps_4_0");
        _sceneVertex = _d.CreateVertexShader(sceneVs);
        _scenePixel = _d.CreatePixelShader(scenePs);
        _sceneLayout = _d.CreateInputLayout([("POSITION", Format.R32G32B32Float, 0), ("NORMAL", Format.R32G32B32Float, 12)], sceneVs);
        _overlayVertex = _d.CreateVertexShader(overlayVs);
        _overlayPixel = _d.CreatePixelShader(overlayPs);
        _overlayLayout = _d.CreateInputLayout(
            [("POSITION", Format.R32G32Float, 0), ("TEXCOORD", Format.R32G32Float, 8), ("COLOR", Format.R32G32B32A32Float, 16)], overlayVs);

        // Straight alpha over what is there, the colour's own alpha kept as coverage, as on Metal.
        _opaqueBlend = _d.CreateBlendState(Blend(false, 0xF));
        _alphaBlend = _d.CreateBlendState(Blend(true, 0xF));
        _noColor = _d.CreateBlendState(Blend(false, 0));
        _depthWrite = _d.CreateDepthStencilState(new DepthStencilDesc { DepthEnable = 1, DepthWriteMask = 1, DepthFunc = D3D.CompareLessEqual });
        _depthTest = _d.CreateDepthStencilState(new DepthStencilDesc { DepthEnable = 1, DepthWriteMask = 0, DepthFunc = D3D.CompareLessEqual });
        _depthAlways = _d.CreateDepthStencilState(new DepthStencilDesc { DepthEnable = 0, DepthWriteMask = 0, DepthFunc = D3D.CompareAlways });
        // Both sides drawn, as Metal does by default: nothing in the scene relies on culling.
        _rasterizer = _d.CreateRasterizerState(new RasterizerDesc { FillMode = 3, CullMode = 1, DepthClipEnable = 1, MultisampleEnable = 1 });
        _sampler = _d.CreateSamplerState(new SamplerDesc
        {
            Filter = 0x15 /* MIN_MAG_MIP_LINEAR */, AddressU = 3, AddressV = 3, AddressW = 3 /* CLAMP */,
            ComparisonFunc = D3D.CompareNever, MaxLod = float.MaxValue,
        });

        _frameConstants = ConstantBuffer(sizeof(FrameUniforms));
        _drawConstants = ConstantBuffer(sizeof(DrawUniforms));
        _viewportConstants = ConstantBuffer(16);

        _atlasTexture = _d.CreateTexture2D(new Texture2DDesc
        {
            Width = OverlayAtlas.Size, Height = OverlayAtlas.Size, MipLevels = 1, ArraySize = 1, Format = Format.R8UNorm, SampleCount = 1,
            Usage = D3D.UsageDefault, BindFlags = D3D.BindShaderResource,
        });
        _atlasView = _d.CreateShaderResourceView(_atlasTexture);
        _atlas = new D3D11Atlas(this);
        if (window == IntPtr.Zero)
        {
            _disjoint = _d.CreateQuery(3);
            _timeStart = _d.CreateQuery(2);
            _timeEnd = _d.CreateQuery(2);
        }
        Log.Info($"Renderer: {Name}");
    }

    private static (IntPtr Device, IntPtr Context) CreateDevice(bool debug)
    {
        // Feature level 10.0 is enough: shader model 4, and 4× multisampling, which every 10.1 GPU has and nearly
        // every 10.0 one. Hardware first, then WARP (Windows' own software rasterizer: a VM, a missing driver).
        int* levels = stackalloc int[] { 0xB000, 0xA100, 0xA000 };
        foreach (int driver in new[] { 1 /* HARDWARE */, 5 /* WARP */ })
        {
            foreach (bool withDebug in debug ? new[] { true, false } : [false])
            {
                IntPtr device, context;
                int level;
                uint flags = 0x20 /* BGRA_SUPPORT */ | (withDebug ? 0x2u /* DEBUG */ : 0);
                if (D3D11CreateDevice(IntPtr.Zero, driver, IntPtr.Zero, flags, levels, 3, 7, &device, &level, &context) >= 0)
                {
                    if (debug && !withDebug) Log.Warn("D3D11_DEBUG: no debug layer (Settings > Optional features > Graphics Tools)");
                    if (driver == 5) Log.Warn("Renderer: no Direct3D 11 hardware, drawing with WARP");
                    return (device, context);
                }
            }
        }
        throw new InvalidOperationException("Direct3D 11 is not available");
    }

    private static bool TearingSupported(IntPtr factory)
    {
        // IDXGIFactory5::CheckFeatureSupport (28), DXGI_FEATURE_PRESENT_ALLOW_TEARING: presenting without waiting
        // for the display, which a flip-model swap chain needs for vsync off.
        IntPtr factory5 = Com.Query(factory, new Guid("7632E1F5-EE65-4DCA-87FD-84CD75F8838D"));
        if (factory5 == IntPtr.Zero) return false;
        int allowed = 0;
        int hr = ((delegate* unmanaged[Stdcall]<IntPtr, int, int*, uint, int>) Com.Slot(factory5, 28))(factory5, 0, &allowed, 4);
        Com.Release(ref factory5);
        return hr >= 0 && allowed != 0;
    }

    private IntPtr CreateSwapChain(IntPtr factory, IntPtr window)
    {
        var desc = new SwapChainDesc1
        {
            Format = Format.B8G8R8A8UNorm, SampleCount = 1, BufferUsage = 0x20 /* RENDER_TARGET_OUTPUT */, BufferCount = 2,
            SwapEffect = 4 /* FLIP_DISCARD */, Flags = _tearing ? 0x800u /* ALLOW_TEARING */ : 0,
        };
        // IDXGIFactory2::CreateSwapChainForHwnd (15); IDXGIFactory::MakeWindowAssociation (8) with
        // DXGI_MWA_NO_ALT_ENTER, so Alt-Enter doesn't switch to exclusive fullscreen behind the game's back.
        IntPtr swapChain;
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, SwapChainDesc1*, void*, IntPtr, IntPtr*, int>) Com.Slot(factory, 15))(
            factory, _device, window, &desc, null, IntPtr.Zero, &swapChain), "CreateSwapChainForHwnd");
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int>) Com.Slot(factory, 8))(factory, window, 2);
        return swapChain;
    }

    private static BlendDesc Blend(bool alpha, byte writeMask) => new()
    {
        Target0 = new TargetBlendDesc
        {
            BlendEnable = alpha ? 1 : 0,
            SrcBlend = D3D.BlendSrcAlpha, DestBlend = D3D.BlendInvSrcAlpha, BlendOp = D3D.BlendOpAdd,
            SrcBlendAlpha = D3D.BlendOne, DestBlendAlpha = D3D.BlendInvSrcAlpha, BlendOpAlpha = D3D.BlendOpAdd,
            WriteMask = writeMask,
        },
    };

    private IntPtr ConstantBuffer(int size) => _d.CreateBuffer(new BufferDesc
    {
        ByteWidth = (uint) size, Usage = D3D.UsageDynamic, BindFlags = D3D.BindConstant, CpuAccessFlags = D3D.CpuWrite,
    });

    private static byte[] Compile(string name, string entry, string target)
    {
        using Stream stream = typeof(D3D11Renderer).Assembly.GetManifestResourceStream("Shaders/" + name)
                              ?? throw new InvalidOperationException($"The shader {name} is not embedded");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        byte[] source = memory.ToArray();
        byte[] entryName = Encoding.ASCII.GetBytes(entry + "\0"), targetName = Encoding.ASCII.GetBytes(target + "\0"),
            sourceName = Encoding.ASCII.GetBytes(name + "\0");
        IntPtr code, errors;
        int hr;
        fixed (byte* src = source, e = entryName, t = targetName, n = sourceName)
            hr = D3DCompile(src, (nuint) source.Length, n, IntPtr.Zero, IntPtr.Zero, e, t, 1u << 15 /* OPTIMIZATION_LEVEL3 */, 0, &code, &errors);
        string? message = errors != IntPtr.Zero ? Marshal.PtrToStringAnsi((IntPtr) BlobPointer(errors), (int) BlobSize(errors)) : null;
        Com.Release(ref errors);
        if (hr < 0) throw new InvalidOperationException($"{name} ({entry}) did not compile: {message}");
        byte[] bytecode = new ReadOnlySpan<byte>(BlobPointer(code), (int) BlobSize(code)).ToArray();
        Com.Release(ref code);
        return bytecode;
    }

    // ID3DBlob::GetBufferPointer (3), GetBufferSize (4)
    private static void* BlobPointer(IntPtr blob) => ((delegate* unmanaged[Stdcall]<IntPtr, void*>) Com.Slot(blob, 3))(blob);
    private static nuint BlobSize(IntPtr blob) => ((delegate* unmanaged[Stdcall]<IntPtr, nuint>) Com.Slot(blob, 4))(blob);

    public MeshHandle Upload(MeshData mesh)
    {
        IntPtr vertices = _d.CreateBuffer(new BufferDesc
        {
            ByteWidth = (uint) (mesh.Vertices.Length * MeshVertex.Stride), Usage = D3D.UsageImmutable, BindFlags = D3D.BindVertex,
        }, MemoryMarshal.AsBytes(mesh.Vertices.AsSpan()));
        IntPtr indices = _d.CreateBuffer(new BufferDesc
        {
            // Rounded up to whole 4 bytes, which a buffer's size must be.
            ByteWidth = (uint) ((mesh.Indices.Length * 2 + 3) & ~3), Usage = D3D.UsageImmutable, BindFlags = D3D.BindIndex,
        }, Padded(MemoryMarshal.AsBytes(mesh.Indices.AsSpan())));
        _meshes.Add((vertices, indices, mesh.Indices.Length));
        return new MeshHandle(_meshes.Count - 1);
    }

    private static ReadOnlySpan<byte> Padded(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % 4 == 0) return bytes;
        var padded = new byte[(bytes.Length + 3) & ~3];
        bytes.CopyTo(padded);
        return padded;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameUniforms
    {
        public Matrix4x4 ViewProjection;
        public Vector4 LightDirection, Ambient, Diffuse, ViewDirection, Specular;
    }

    // D3D11_QUERY_DATA_TIMESTAMP_DISJOINT.
    [StructLayout(LayoutKind.Sequential)]
    private struct Disjoint
    {
        public ulong Frequency;
        public int IsDisjoint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DrawUniforms
    {
        public Matrix4x4 Model;
        public Vector4 Color, Accent, Pattern, PaintUp, DrainUp;
    }

    public bool Draw(Scene scene, ReadOnlySpan<OverlayVertex> underlay, ReadOnlySpan<OverlayVertex> overlay)
    {
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        _drawCalls = _triangles = 0;
        (int width, int height) = _size();
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        EnsureTargets(width, height);
        bool timed = _disjoint != IntPtr.Zero;
        if (timed)
        {
            _c.Begin(_disjoint);
            _c.End(_timeStart);
        }

        Vector4 background = scene.Background;
        // A transparent background (for images to lay over others) clears to premultiplied colour.
        _c.ClearRenderTarget(_multisampleView, background.X * background.W, background.Y * background.W, background.Z * background.W, background.W);
        _c.ClearDepth(_depthView, 1f);
        _c.OMSetRenderTarget(_multisampleView, _depthView);
        _c.RSSetViewport(new Viewport { Width = width, Height = height, MaxDepth = 1 });
        _c.RSSetState(_rasterizer);
        _c.IASetTriangleList();

        // The underlay and the overlay share one buffer: the underlay's triangles first.
        int total = underlay.Length + overlay.Length;
        if (total > 0)
        {
            if (_overlayVertices.Length < total) _overlayVertices = new OverlayVertex[Math.Max(total, _overlayVertices.Length * 2)];
            underlay.CopyTo(_overlayVertices);
            overlay.CopyTo(_overlayVertices.AsSpan(underlay.Length));
            UploadOverlay(total);
            _c.Write(_viewportConstants, new Vector4(width, height, 0, 0));
        }
        if (underlay.Length > 0) DrawOverlay(0, underlay.Length);

        Lighting light = scene.Lighting;
        _c.Write(_frameConstants, new FrameUniforms
        {
            ViewProjection = scene.View * scene.Projection,
            LightDirection = new Vector4(Vector3.Normalize(light.Direction), 0),
            Ambient = new Vector4(light.Ambient, 0),
            Diffuse = new Vector4(light.Diffuse, 0),
            ViewDirection = new Vector4(scene.ViewDirection, 0),
            Specular = new Vector4(light.Specular, light.Shininess, 0, 0),
        });
        _c.VSSetShader(_sceneVertex);
        _c.PSSetShader(_scenePixel);
        _c.IASetInputLayout(_sceneLayout);
        _c.VSSetConstantBuffer(0, _frameConstants);
        _c.PSSetConstantBuffer(0, _frameConstants);
        _c.VSSetConstantBuffer(1, _drawConstants);
        _c.PSSetConstantBuffer(1, _drawConstants);

        _c.OMSetBlendState(_opaqueBlend);
        _c.OMSetDepthStencilState(_depthWrite);
        foreach (DrawItem item in scene.Items)
            if (item.Color.W >= 1f) DrawMesh(item);
        var translucent = new List<(float Depth, DrawItem Item)>();
        foreach (DrawItem item in scene.Items)
            if (item.Color.W < 1f && item.Color.W > 0f)
                translucent.Add((Vector3.Transform(item.Model.Translation, scene.View).Z, item));
        if (translucent.Count > 0)
        {
            // A translucent mesh fades as one object: its depth first (no colour), then its colour only where it
            // is the nearest surface, so each pixel blends it exactly once. View space looks down -z: the
            // farthest has the smallest z.
            translucent.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            foreach (var (_, item) in translucent)
            {
                _c.OMSetBlendState(_noColor);
                _c.OMSetDepthStencilState(_depthWrite);
                DrawMesh(item);
                _c.OMSetBlendState(_alphaBlend);
                _c.OMSetDepthStencilState(_depthTest);
                DrawMesh(item);
            }
        }

        if (overlay.Length > 0) DrawOverlay(underlay.Length, overlay.Length);

        _c.ResolveSubresource(_resolved, _multisample, ColorFormat);
        if (timed)
        {
            _c.End(_timeEnd);
            _c.End(_disjoint);
        }
        if (_swapChain != IntPtr.Zero)
        {
            var swapChain = new SwapChain(_swapChain);
            IntPtr buffer = swapChain.Buffer();
            _c.CopyResource(buffer, _resolved);
            Com.Release(ref buffer);
            int hr = swapChain.Present(_vsync ? 1u : 0, _tearing ? 0x200u /* ALLOW_TEARING */ : 0);
            if (hr < 0) throw new InvalidOperationException($"Present failed (0x{hr:X8}, device: 0x{_d.RemovedReason:X8})");
        }
        double cpuMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Stats = new RenderStats(cpuMs, Stats.GpuMs, _drawCalls + (underlay.Length > 0 ? 1 : 0) + (overlay.Length > 0 ? 1 : 0),
            _triangles + total / 3, total);
        if (timed)
        {
            // Headless: wait for the GPU, which also times it.
            var disjoint = _c.GetData<Disjoint>(_disjoint);
            ulong start = _c.GetData<ulong>(_timeStart), end = _c.GetData<ulong>(_timeEnd);
            if (disjoint.IsDisjoint == 0 && disjoint.Frequency > 0) Stats = Stats with { GpuMs = (end - start) * 1000.0 / disjoint.Frequency };
        }
        ReportDebugMessages();
        _drawn = true;
        Width = width;
        Height = height;
        return true;
    }

    private void UploadOverlay(int count)
    {
        if (count > _overlayCapacity)
        {
            Com.Release(ref _overlayBuffer);
            _overlayCapacity = Math.Max(count, _overlayCapacity * 2);
            _overlayBuffer = _d.CreateBuffer(new BufferDesc
            {
                ByteWidth = (uint) (_overlayCapacity * OverlayVertex.Stride), Usage = D3D.UsageDynamic, BindFlags = D3D.BindVertex,
                CpuAccessFlags = D3D.CpuWrite,
            });
        }
        Mapped mapped = _c.Map(_overlayBuffer, D3D.MapWriteDiscard);
        MemoryMarshal.AsBytes(_overlayVertices.AsSpan(0, count)).CopyTo(new Span<byte>(mapped.Data, count * OverlayVertex.Stride));
        _c.Unmap(_overlayBuffer);
    }

    private void DrawOverlay(int first, int count)
    {
        _c.VSSetShader(_overlayVertex);
        _c.PSSetShader(_overlayPixel);
        _c.IASetInputLayout(_overlayLayout);
        _c.IASetVertexBuffer(_overlayBuffer, OverlayVertex.Stride);
        _c.VSSetConstantBuffer(0, _viewportConstants);
        _c.PSSetShaderResource(0, _atlasView);
        _c.PSSetSampler(0, _sampler);
        _c.OMSetBlendState(_alphaBlend);
        _c.OMSetDepthStencilState(_depthAlways);
        _c.Draw(count, first);
    }

    private void DrawMesh(DrawItem item)
    {
        var (vertices, indices, count) = _meshes[item.Mesh.Id];
        _c.Write(_drawConstants, new DrawUniforms
        {
            Model = item.Model,
            Color = item.Color,
            Accent = item.Accent,
            Pattern = new Vector4((float) item.Surface, item.Progress, item.Drained, 0),
            PaintUp = new Vector4(item.PaintUp == Vector3.Zero ? Vector3.UnitY : item.PaintUp, 0),
            DrainUp = new Vector4(item.DrainUp == Vector3.Zero ? Vector3.UnitY : item.DrainUp, 0),
        });
        _c.IASetVertexBuffer(vertices, MeshVertex.Stride);
        _c.IASetIndexBuffer(indices, Format.R16UInt);
        _c.DrawIndexed(count);
        _drawCalls++;
        _triangles += count / 3;
    }

    private void EnsureTargets(int width, int height)
    {
        if (_swapChain != IntPtr.Zero && (width != _swapWidth || height != _swapHeight))
        {
            new SwapChain(_swapChain).Resize((uint) width, (uint) height, _tearing ? 0x800u : 0);
            (_swapWidth, _swapHeight) = (width, height);
        }
        if (_multisample != IntPtr.Zero && width == _targetWidth && height == _targetHeight) return;
        ReleaseTargets();
        _multisample = _d.CreateTexture2D(Target(width, height, ColorFormat, Samples, D3D.BindRenderTarget));
        _multisampleView = _d.CreateRenderTargetView(_multisample);
        _depth = _d.CreateTexture2D(Target(width, height, Format.D32Float, Samples, D3D.BindDepthStencil));
        _depthView = _d.CreateDepthStencilView(_depth);
        _resolved = _d.CreateTexture2D(Target(width, height, ColorFormat, 1, 0));
        (_targetWidth, _targetHeight) = (width, height);
    }

    private static Texture2DDesc Target(int width, int height, Format format, int samples, uint bind) => new()
    {
        Width = (uint) width, Height = (uint) height, MipLevels = 1, ArraySize = 1, Format = format, SampleCount = (uint) samples,
        Usage = D3D.UsageDefault, BindFlags = bind,
    };

    private void ReleaseTargets()
    {
        Com.Release(ref _multisampleView);
        Com.Release(ref _multisample);
        Com.Release(ref _depthView);
        Com.Release(ref _depth);
        Com.Release(ref _resolved);
        _drawn = false;
    }

    public byte[] ReadPixels()
    {
        if (!_drawn) throw new InvalidOperationException("No frame has been drawn yet");
        IntPtr staging = _d.CreateTexture2D(new Texture2DDesc
        {
            Width = (uint) Width, Height = (uint) Height, MipLevels = 1, ArraySize = 1, Format = ColorFormat, SampleCount = 1,
            Usage = D3D.UsageStaging, CpuAccessFlags = D3D.CpuRead,
        });
        try
        {
            _c.CopyResource(staging, _resolved);
            Mapped mapped = _c.Map(staging, D3D.MapRead);
            var pixels = new byte[Width * Height * 4];
            for (int y = 0; y < Height; y++)
                new ReadOnlySpan<byte>((byte*) mapped.Data + y * mapped.RowPitch, Width * 4).CopyTo(pixels.AsSpan(y * Width * 4));
            _c.Unmap(staging);
            // BGRA to RGBA.
            for (int i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            return pixels;
        }
        finally
        {
            Com.Release(ref staging);
        }
    }

    // With D3D11_DEBUG=1 and the debug layer installed: what it found, into the log (as MTL_DEBUG_LAYER does).
    private void ReportDebugMessages()
    {
        if (_infoQueue == IntPtr.Zero) return;
        // ID3D11InfoQueue::GetNumStoredMessages (8), GetMessage (5), ClearStoredMessages (4). A D3D11_MESSAGE is
        // category, severity, id (ints), then the description and its length.
        ulong count = ((delegate* unmanaged[Stdcall]<IntPtr, ulong>) Com.Slot(_infoQueue, 8))(_infoQueue);
        for (ulong i = 0; i < count; i++)
        {
            nuint length = 0;
            ((delegate* unmanaged[Stdcall]<IntPtr, ulong, byte*, nuint*, int>) Com.Slot(_infoQueue, 5))(_infoQueue, i, null, &length);
            var buffer = new byte[(int) length];
            int severity;
            string text;
            fixed (byte* message = buffer)
            {
                if (((delegate* unmanaged[Stdcall]<IntPtr, ulong, byte*, nuint*, int>) Com.Slot(_infoQueue, 5))(_infoQueue, i, message, &length) < 0) continue;
                severity = *(int*) (message + 4);   // corruption 0, error 1, warning 2, info 3, message 4
                text = Marshal.PtrToStringAnsi(*(IntPtr*) (message + 16)) ?? "";
            }
            if (severity <= 1) Log.Error($"D3D11: {text}");
            else if (severity == 2) Log.Warn($"D3D11: {text}");
        }
        ((delegate* unmanaged[Stdcall]<IntPtr, void>) Com.Slot(_infoQueue, 4))(_infoQueue);
    }

    public void Dispose()
    {
        if (_context != IntPtr.Zero) _c.ClearState();
        ReleaseTargets();
        foreach (var (vertices, indices, _) in _meshes)
        {
            IntPtr v = vertices, i = indices;
            Com.Release(ref v);
            Com.Release(ref i);
        }
        _meshes.Clear();
        IntPtr[] owned =
        [
            _sceneVertex, _scenePixel, _sceneLayout, _overlayVertex, _overlayPixel, _overlayLayout, _opaqueBlend, _alphaBlend, _noColor,
            _depthWrite, _depthTest, _depthAlways, _rasterizer, _sampler, _frameConstants, _drawConstants, _viewportConstants,
            _atlasView, _atlasTexture, _overlayBuffer, _disjoint, _timeStart, _timeEnd, _infoQueue, _swapChain, _context, _device,
        ];
        for (int i = 0; i < owned.Length; i++) Com.Release(ref owned[i]);
        _overlayBuffer = _disjoint = _timeStart = _timeEnd = IntPtr.Zero;
    }

    // The atlas is a default-usage texture: an upload goes through the context, which keeps it in order with the
    // frames that read the atlas, so nothing waits here.
    private sealed class D3D11Atlas(D3D11Renderer renderer) : IOverlayAtlas
    {
        public int Size => OverlayAtlas.Size;

        public void Upload(int x, int y, int width, int height, ReadOnlySpan<byte> coverage) =>
            renderer._c.UpdateSubresource(renderer._atlasTexture,
                new Box { Left = (uint) x, Top = (uint) y, Front = 0, Right = (uint) (x + width), Bottom = (uint) (y + height), Back = 1 },
                coverage, (uint) width);
    }

    [LibraryImport("d3d11.dll")]
    private static partial int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, int* featureLevels, uint levelCount,
                                                 uint sdkVersion, IntPtr* device, int* level, IntPtr* context);

    [LibraryImport("d3dcompiler_47.dll")]
    private static partial int D3DCompile(byte* source, nuint size, byte* sourceName, IntPtr defines, IntPtr include, byte* entry, byte* target,
                                          uint flags1, uint flags2, IntPtr* code, IntPtr* errors);
}
