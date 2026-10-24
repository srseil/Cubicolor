using System;
using System.Runtime.InteropServices;
using static Cubicolor.Platform.Mac.ObjC;
using Cubicolor.Platform.Mac;

namespace Cubicolor.Platform.Metal;

// The Metal objects the renderer uses, as thin typed wrappers over their Objective-C handles, with each
// selector resolved once. Only what is called is here; add a method when a renderer needs one.
//
// Enum values are from Metal's headers (MTLPixelFormat.h, MTLTexture.h, MTLRenderPass.h, MTLResource.h,
// MTLCommandBuffer.h), from memory: the Command Line Tools ship no Metal headers to check against. Each one
// in use is exercised by a run that fails visibly when it is wrong (the backend comparison test).

internal enum PixelFormat : ulong { R8Unorm = 10, R32Float = 55, RGBA8Unorm = 70, BGRA8Unorm = 80, BGRA8UnormSrgb = 81, RGBA16Float = 115, RGBA32Float = 125, Depth32Float = 252 }

[Flags] internal enum TextureUsage : ulong { ShaderRead = 1, ShaderWrite = 2, RenderTarget = 4 }

internal enum StorageMode : ulong { Shared = 0, Private = 2, Memoryless = 3 }

internal enum LoadAction : ulong { DontCare = 0, Load = 1, Clear = 2 }

internal enum StoreAction : ulong { DontCare = 0, Store = 1 }

internal enum PrimitiveType : ulong { Triangle = 3 }

internal enum VertexFormat : ulong { Float = 28, Float2 = 29, Float3 = 30, Float4 = 31 }

internal enum CompareFunction : ulong { Never = 0, Less = 1, Equal = 2, LessEqual = 3, Greater = 4, NotEqual = 5, GreaterEqual = 6, Always = 7 }

internal enum SamplerFilter : ulong { Nearest = 0, Linear = 1 }

internal enum MipFilter : ulong { NotMipmapped = 0, Nearest = 1, Linear = 2 }

internal enum AddressMode : ulong { ClampToEdge = 0, Repeat = 2 }

internal enum CommandBufferStatus : ulong { NotEnqueued = 0, Enqueued = 1, Committed = 2, Scheduled = 3, Completed = 4, Error = 5 }

[StructLayout(LayoutKind.Sequential)]
internal struct MTLSize { public nuint Width, Height, Depth; public MTLSize(nuint w, nuint h, nuint d) { Width = w; Height = h; Depth = d; } }

[StructLayout(LayoutKind.Sequential)]
internal struct MTLOrigin { public nuint X, Y, Z; }

[StructLayout(LayoutKind.Sequential)]
internal struct MTLClearColor { public double R, G, B, A; public MTLClearColor(double r, double g, double b, double a) { R = r; G = g; B = b; A = a; } }

[StructLayout(LayoutKind.Sequential)]
internal struct CGSize { public double Width, Height; }

internal readonly unsafe partial struct MetalDevice
{
    public readonly IntPtr Handle;
    private MetalDevice(IntPtr handle) => Handle = handle;

    [LibraryImport("/System/Library/Frameworks/Metal.framework/Metal")]
    private static partial IntPtr MTLCreateSystemDefaultDevice();

    private static readonly IntPtr SelName = Sel("name"), SelNewQueue = Sel("newCommandQueue"),
        SelNewLibrary = Sel("newLibraryWithSource:options:error:"),
        SelNewPipeline = Sel("newRenderPipelineStateWithDescriptor:error:"),
        SelNewTexture = Sel("newTextureWithDescriptor:"), SelNewBuffer = Sel("newBufferWithLength:options:"),
        SelTextureDescriptor = Sel("texture2DDescriptorWithPixelFormat:width:height:mipmapped:"),
        SelSetUsage = Sel("setUsage:"), SelSetStorage = Sel("setStorageMode:"), SelSetLabel = Sel("setLabel:"),
        SelSetMipCount = Sel("setMipmapLevelCount:"), SelNewBufferWithBytes = Sel("newBufferWithBytes:length:options:"),
        SelNewSampler = Sel("newSamplerStateWithDescriptor:"), SelNewDepthState = Sel("newDepthStencilStateWithDescriptor:"),
        SelMinFilter = Sel("setMinFilter:"), SelMagFilter = Sel("setMagFilter:"), SelMipFilter = Sel("setMipFilter:"),
        SelAddressS = Sel("setSAddressMode:"), SelAddressT = Sel("setTAddressMode:"), SelAddressR = Sel("setRAddressMode:"),
        SelLodMin = Sel("setLodMinClamp:"), SelLodMax = Sel("setLodMaxClamp:"), SelAnisotropy = Sel("setMaxAnisotropy:"),
        SelSamplerCompare = Sel("setCompareFunction:"), SelDepthCompare = Sel("setDepthCompareFunction:"),
        SelDepthWrite = Sel("setDepthWriteEnabled:"), SelNew = Sel("new"), SelAutorelease = Sel("autorelease");
    private static readonly IntPtr TextureDescriptorClass = Class("MTLTextureDescriptor"),
        SamplerDescriptorClass = Class("MTLSamplerDescriptor"), DepthStencilDescriptorClass = Class("MTLDepthStencilDescriptor");

    private static readonly IntPtr SelSetTextureType = Sel("setTextureType:"), SelSetSampleCount = Sel("setSampleCount:"),
        SelSupportsSampleCount = Sel("supportsTextureSampleCount:");

    // A multisampled 2D render target of `samples` samples, owned, private to the GPU.
    public MetalTexture NewMultisampleTexture(string label, PixelFormat format, uint width, uint height, int samples)
    {
        IntPtr descriptor = Send<IntPtr, PixelFormat, nuint, nuint, byte>(TextureDescriptorClass, SelTextureDescriptor, format, width, height, 0);
        Call(descriptor, SelSetTextureType, (nuint) 4);   // MTLTextureType2DMultisample
        Call(descriptor, SelSetSampleCount, (nuint) samples);
        Call(descriptor, SelSetUsage, TextureUsage.RenderTarget);
        Call(descriptor, SelSetStorage, StorageMode.Private);
        IntPtr texture = Send<IntPtr, IntPtr>(Handle, SelNewTexture, descriptor);
        if (texture == IntPtr.Zero) throw new InvalidOperationException($"Couldn't create {label}");
        Call(texture, SelSetLabel, NSString(label));
        return new MetalTexture(texture, width, height);
    }

    // Whether render targets can have this many samples (what SDL_GPU's Metal backend asks too).
    public bool SupportsSampleCount(int samples) => Send<byte, nuint>(Handle, SelSupportsSampleCount, (nuint) samples) != 0;

    // A buffer holding a copy of `data`, in memory the CPU and GPU share; owned. Meshes are uploaded this way:
    // on Apple Silicon the memory is one, so there is no separate copy to the GPU to make.
    public MetalBuffer NewBuffer(ReadOnlySpan<byte> data, string label)
    {
        IntPtr buffer;
        fixed (byte* p = data)
            buffer = Send<IntPtr, IntPtr, nuint, nuint>(Handle, SelNewBufferWithBytes, (IntPtr) p, (nuint) data.Length, 0 /* shared */);
        if (buffer == IntPtr.Zero) throw new InvalidOperationException($"Couldn't create {label} of {data.Length} bytes");
        Call(buffer, SelSetLabel, NSString(label));
        return new MetalBuffer(buffer, (nuint) data.Length);
    }

    // A sampler state, owned: the LOD clamped to [0, maxLod], up to `anisotropy` samples along a surface seen
    // at a slant (1, the default, is plain trilinear), and, for a comparison sampler (sample_compare),
    // `compare` (the reference against the texel); a plain sampler ignores it.
    public IntPtr NewSampler(SamplerFilter filter, MipFilter mip, AddressMode address, float maxLod, int anisotropy = 1,
                             CompareFunction compare = CompareFunction.Always)
    {
        IntPtr d = Send<IntPtr>(Send<IntPtr>(SamplerDescriptorClass, SelNew), SelAutorelease);
        Call(d, SelMinFilter, filter);
        Call(d, SelMagFilter, filter);
        Call(d, SelMipFilter, mip);
        Call(d, SelAddressS, address);
        Call(d, SelAddressT, address);
        Call(d, SelAddressR, address);
        Call(d, SelLodMin, 0f);
        Call(d, SelLodMax, maxLod);
        Call(d, SelAnisotropy, (nuint) anisotropy);
        Call(d, SelSamplerCompare, compare);
        IntPtr sampler = Send<IntPtr, IntPtr>(Handle, SelNewSampler, d);
        if (sampler == IntPtr.Zero) throw new InvalidOperationException("Couldn't create a sampler");
        return sampler;
    }

    // A depth test and write state, owned. As SDL_GPU sets it: no test compares Always and never writes.
    public IntPtr NewDepthState(CompareFunction compare, bool write)
    {
        IntPtr d = Send<IntPtr>(Send<IntPtr>(DepthStencilDescriptorClass, SelNew), SelAutorelease);
        Call(d, SelDepthCompare, compare);
        Call(d, SelDepthWrite, (byte) (write ? 1 : 0));
        return Send<IntPtr, IntPtr>(Handle, SelNewDepthState, d);
    }

    // The system's GPU (one on Apple Silicon); owned for the process.
    public static MetalDevice CreateDefault()
    {
        IntPtr device = MTLCreateSystemDefaultDevice();
        if (device == IntPtr.Zero) throw new InvalidOperationException("no Metal device");
        return new MetalDevice(device);
    }

    public string Name => Str(Send<IntPtr>(Handle, SelName)) ?? "?";

    public IntPtr NewCommandQueue() => Send<IntPtr>(Handle, SelNewQueue);

    // Compile MSL at runtime (Metal caches the result on disk). Owned; throws with Metal's message on failure.
    public MetalLibrary NewLibrary(string msl, string what)
    {
        IntPtr error = IntPtr.Zero;
        IntPtr library = Send<IntPtr, IntPtr, IntPtr, IntPtr>(Handle, SelNewLibrary, NSString(msl), IntPtr.Zero, (IntPtr) (&error));
        if (library == IntPtr.Zero) throw new InvalidOperationException($"Couldn't compile the MSL for {what}: {ErrorText(error)}");
        return new MetalLibrary(library);
    }

    private static readonly IntPtr SelNewComputePipeline = Sel("newComputePipelineStateWithFunction:error:");

    public IntPtr NewComputePipeline(IntPtr function, string what)
    {
        IntPtr error = IntPtr.Zero;
        IntPtr pipeline = Send<IntPtr, IntPtr, IntPtr>(Handle, SelNewComputePipeline, function, (IntPtr) (&error));
        if (pipeline == IntPtr.Zero) throw new InvalidOperationException($"Couldn't create the {what} compute pipeline: {ErrorText(error)}");
        return pipeline;
    }

    public IntPtr NewRenderPipeline(IntPtr descriptor, string what)
    {
        IntPtr error = IntPtr.Zero;
        IntPtr pipeline = Send<IntPtr, IntPtr, IntPtr>(Handle, SelNewPipeline, descriptor, (IntPtr) (&error));
        if (pipeline == IntPtr.Zero) throw new InvalidOperationException($"Couldn't create the {what} pipeline: {ErrorText(error)}");
        return pipeline;
    }

    // A 2D texture with `levels` mip levels, owned.
    public MetalTexture NewTexture(string label, PixelFormat format, uint width, uint height, TextureUsage usage, StorageMode storage, int levels = 1)
    {
        IntPtr descriptor = Send<IntPtr, PixelFormat, nuint, nuint, byte>(TextureDescriptorClass, SelTextureDescriptor, format, width, height, 0);
        Call(descriptor, SelSetUsage, usage);
        Call(descriptor, SelSetStorage, storage);
        Call(descriptor, SelSetMipCount, (nuint) levels);
        IntPtr texture = Send<IntPtr, IntPtr>(Handle, SelNewTexture, descriptor);
        if (texture == IntPtr.Zero) throw new InvalidOperationException($"Couldn't create {label}");
        Call(texture, SelSetLabel, NSString(label));
        return new MetalTexture(texture, width, height);
    }

    // A buffer in memory the CPU and GPU share, owned.
    public MetalBuffer NewSharedBuffer(nuint length)
    {
        IntPtr buffer = Send<IntPtr, nuint, nuint>(Handle, SelNewBuffer, length, 0 /* MTLResourceStorageModeShared */);
        if (buffer == IntPtr.Zero) throw new InvalidOperationException($"Couldn't create a buffer of {length} bytes");
        return new MetalBuffer(buffer, length);
    }
}

internal readonly struct MetalLibrary
{
    public readonly IntPtr Handle;
    public MetalLibrary(IntPtr handle) => Handle = handle;

    private static readonly IntPtr SelNewFunction = Sel("newFunctionWithName:");

    // An owned function, by its name in the source (VSMain, PSMain, or a kernel's own).
    public IntPtr Function(string name)
    {
        IntPtr function = Send<IntPtr, IntPtr>(Handle, SelNewFunction, NSString(name));
        if (function == IntPtr.Zero) throw new InvalidOperationException($"no function {name} in the library");
        return function;
    }
}

// A render pipeline's description, filled in and turned into a pipeline state by MetalDevice.
internal readonly struct RenderPipelineDescriptor
{
    public readonly IntPtr Handle;   // autoreleased
    private RenderPipelineDescriptor(IntPtr handle) => Handle = handle;

    private static readonly IntPtr Cls = Class("MTLRenderPipelineDescriptor");
    private static readonly IntPtr SelNew = Sel("new"), SelAutorelease = Sel("autorelease"),
        SelVertex = Sel("setVertexFunction:"), SelFragment = Sel("setFragmentFunction:"), SelLabel = Sel("setLabel:"),
        SelColorAttachments = Sel("colorAttachments"), SelAt = Sel("objectAtIndexedSubscript:"),
        SelPixelFormat = Sel("setPixelFormat:"), SelDepthFormat = Sel("setDepthAttachmentPixelFormat:"),
        SelSampleCount = Sel("setRasterSampleCount:");

    public static RenderPipelineDescriptor Create(string label, IntPtr vertex, IntPtr fragment)
    {
        IntPtr d = Send<IntPtr>(Send<IntPtr>(Cls, SelNew), SelAutorelease);
        Call(d, SelLabel, NSString(label));
        Call(d, SelVertex, vertex);
        Call(d, SelFragment, fragment);
        return new RenderPipelineDescriptor(d);
    }

    public void ColorFormat(int index, PixelFormat format)
        => Call(Send<IntPtr, nuint>(Send<IntPtr>(Handle, SelColorAttachments), SelAt, (nuint) index), SelPixelFormat, format);

    private static readonly IntPtr SelBlending = Sel("setBlendingEnabled:"), SelRgbOp = Sel("setRgbBlendOperation:"),
        SelAlphaOp = Sel("setAlphaBlendOperation:"), SelSourceRgb = Sel("setSourceRGBBlendFactor:"),
        SelDestinationRgb = Sel("setDestinationRGBBlendFactor:"), SelSourceAlpha = Sel("setSourceAlphaBlendFactor:"),
        SelDestinationAlpha = Sel("setDestinationAlphaBlendFactor:");

    // Straight alpha over what is there, the colour's own alpha kept as coverage (MetalPipelineSpec.AlphaBlend),
    // as MTLBlendFactor values (Zero 0, One 1, SourceAlpha 4,
    // OneMinusSourceAlpha 5) with MTLBlendOperationAdd (0).
    public void AlphaBlend(int index)
    {
        IntPtr attachment = Send<IntPtr, nuint>(Send<IntPtr>(Handle, SelColorAttachments), SelAt, (nuint) index);
        Call(attachment, SelBlending, (byte) 1);
        Call(attachment, SelRgbOp, (nuint) 0);
        Call(attachment, SelAlphaOp, (nuint) 0);
        Call(attachment, SelSourceRgb, (nuint) 4);
        Call(attachment, SelDestinationRgb, (nuint) 5);
        Call(attachment, SelSourceAlpha, (nuint) 1);
        Call(attachment, SelDestinationAlpha, (nuint) 5);
    }

    private static readonly IntPtr SelWriteMask = Sel("setWriteMask:");

    // Which colour channels the pipeline writes (MTLColorWriteMask: 0 none, 0xf all).
    public void WriteMask(int index, ulong mask) =>
        Call(Send<IntPtr, nuint>(Send<IntPtr>(Handle, SelColorAttachments), SelAt, (nuint) index), SelWriteMask, (nuint) mask);

    public void DepthFormat(PixelFormat format) => Call(Handle, SelDepthFormat, format);
    public void SampleCount(int samples) => Call(Handle, SelSampleCount, (nuint) samples);

    private static readonly IntPtr VertexDescriptorClass = Class("MTLVertexDescriptor");
    private static readonly IntPtr SelVertexDescriptor = Sel("vertexDescriptor"), SelSetVertexDescriptor = Sel("setVertexDescriptor:"),
        SelAttributes = Sel("attributes"), SelLayouts = Sel("layouts"), SelFormat = Sel("setFormat:"), SelOffset = Sel("setOffset:"),
        SelBufferIndex = Sel("setBufferIndex:"), SelStride = Sel("setStride:"), SelStepFunction = Sel("setStepFunction:"),
        SelStepRate = Sel("setStepRate:");

    // One interleaved vertex buffer: attribute i at location i, read from [[buffer(bufferIndex)]].
    public void VertexLayout((VertexFormat Format, uint Offset)[] attributes, uint stride, int bufferIndex)
    {
        IntPtr vertex = Send<IntPtr>(VertexDescriptorClass, SelVertexDescriptor);
        IntPtr list = Send<IntPtr>(vertex, SelAttributes);
        for (int i = 0; i < attributes.Length; i++)
        {
            IntPtr attribute = Send<IntPtr, nuint>(list, SelAt, (nuint) i);
            Call(attribute, SelFormat, attributes[i].Format);
            Call(attribute, SelOffset, (nuint) attributes[i].Offset);
            Call(attribute, SelBufferIndex, (nuint) bufferIndex);
        }
        IntPtr layout = Send<IntPtr, nuint>(Send<IntPtr>(vertex, SelLayouts), SelAt, (nuint) bufferIndex);
        Call(layout, SelStride, (nuint) stride);
        Call(layout, SelStepFunction, (nuint) 1);   // MTLVertexStepFunctionPerVertex
        Call(layout, SelStepRate, (nuint) 1);
        Call(Handle, SelSetVertexDescriptor, vertex);
    }
}

// A render pass: where it draws, and what happens to each target at its start and end.
internal readonly struct RenderPassDescriptor
{
    public readonly IntPtr Handle;   // autoreleased
    private RenderPassDescriptor(IntPtr handle) => Handle = handle;

    private static readonly IntPtr Cls = Class("MTLRenderPassDescriptor");
    private static readonly IntPtr SelCreate = Sel("renderPassDescriptor"), SelColorAttachments = Sel("colorAttachments"),
        SelAt = Sel("objectAtIndexedSubscript:"), SelDepth = Sel("depthAttachment"), SelTexture = Sel("setTexture:"),
        SelLoad = Sel("setLoadAction:"), SelStore = Sel("setStoreAction:"), SelClearColor = Sel("setClearColor:"),
        SelClearDepth = Sel("setClearDepth:");

    public static RenderPassDescriptor Create() => new(Send<IntPtr>(Cls, SelCreate));

    public void Color(int index, MetalTexture texture, LoadAction load, StoreAction store, MTLClearColor clear = default)
    {
        IntPtr attachment = Send<IntPtr, nuint>(Send<IntPtr>(Handle, SelColorAttachments), SelAt, (nuint) index);
        Call(attachment, SelTexture, texture.Handle);
        Call(attachment, SelLoad, load);
        Call(attachment, SelStore, store);
        Call(attachment, SelClearColor, clear);
    }

    private static readonly IntPtr SelResolveTexture = Sel("setResolveTexture:");

    // A multisampled colour target resolved into `resolve` at the end of the pass (MTLStoreActionMultisampleResolve).
    public void ColorResolved(int index, MetalTexture texture, MetalTexture resolve, LoadAction load, MTLClearColor clear)
    {
        IntPtr attachment = Send<IntPtr, nuint>(Send<IntPtr>(Handle, SelColorAttachments), SelAt, (nuint) index);
        Call(attachment, SelTexture, texture.Handle);
        Call(attachment, SelResolveTexture, resolve.Handle);
        Call(attachment, SelLoad, load);
        Call(attachment, SelStore, (nuint) 2);
        Call(attachment, SelClearColor, clear);
    }

    private static readonly IntPtr SelSampleAttachments = Sel("sampleBufferAttachments"), SelSetSampleBuffer = Sel("setSampleBuffer:"),
        SelStartVertex = Sel("setStartOfVertexSampleIndex:"), SelEndVertex = Sel("setEndOfVertexSampleIndex:"),
        SelStartFragment = Sel("setStartOfFragmentSampleIndex:"), SelEndFragment = Sel("setEndOfFragmentSampleIndex:");

    // GPU timestamps at the start and end of this pass's vertex and fragment stages, into `sampleBuffer` from
    // `first` on (four of them; see MetalGpuTimer).
    public void Sample(IntPtr sampleBuffer, int first)
    {
        IntPtr attachment = Send<IntPtr, nuint>(Send<IntPtr>(Handle, SelSampleAttachments), SelAt, 0);
        Call(attachment, SelSetSampleBuffer, sampleBuffer);
        Call(attachment, SelStartVertex, (nuint) first);
        Call(attachment, SelEndVertex, (nuint) (first + 1));
        Call(attachment, SelStartFragment, (nuint) (first + 2));
        Call(attachment, SelEndFragment, (nuint) (first + 3));
    }

    public void Depth(MetalTexture texture, LoadAction load, StoreAction store, double clear)
    {
        IntPtr attachment = Send<IntPtr>(Handle, SelDepth);
        Call(attachment, SelTexture, texture.Handle);
        Call(attachment, SelLoad, load);
        Call(attachment, SelStore, store);
        Call(attachment, SelClearDepth, clear);
    }
}

internal readonly struct MetalCommandBuffer
{
    public readonly IntPtr Handle;   // autoreleased, unless retained
    public MetalCommandBuffer(IntPtr handle) => Handle = handle;

    private static readonly IntPtr SelCreate = Sel("commandBuffer"), SelRender = Sel("renderCommandEncoderWithDescriptor:"),
        SelBlit = Sel("blitCommandEncoder"), SelPresent = Sel("presentDrawable:"), SelCommit = Sel("commit"),
        SelWait = Sel("waitUntilCompleted"), SelStatus = Sel("status"), SelError = Sel("error"), SelLabel = Sel("setLabel:");

    public static MetalCommandBuffer From(IntPtr queue, string label)
    {
        var buffer = new MetalCommandBuffer(Send<IntPtr>(queue, SelCreate));
        Call(buffer.Handle, SelLabel, NSString(label));
        return buffer;
    }

    public MetalRenderEncoder RenderPass(RenderPassDescriptor pass) => new(Send<IntPtr, IntPtr>(Handle, SelRender, pass.Handle));

    private static readonly IntPtr SelCompute = Sel("computeCommandEncoder"), SelComputeWithDescriptor = Sel("computeCommandEncoderWithDescriptor:"),
        SelSampleAttachments = Sel("sampleBufferAttachments"), SelAt = Sel("objectAtIndexedSubscript:"), SelSetSampleBuffer = Sel("setSampleBuffer:"),
        SelStartEncoder = Sel("setStartOfEncoderSampleIndex:"), SelEndEncoder = Sel("setEndOfEncoderSampleIndex:"),
        SelComputePassDescriptor = Sel("computePassDescriptor");
    private static readonly IntPtr ComputePassDescriptorClass = Class("MTLComputePassDescriptor");

    public MetalComputeEncoder Compute() => new(Send<IntPtr>(Handle, SelCompute));

    // A compute pass with GPU timestamps at its start and end, into `sampleBuffer` at `first` and `first + 1`.
    public MetalComputeEncoder Compute(IntPtr sampleBuffer, int first)
    {
        IntPtr descriptor = Send<IntPtr>(ComputePassDescriptorClass, SelComputePassDescriptor);
        IntPtr attachment = Send<IntPtr, nuint>(Send<IntPtr>(descriptor, SelSampleAttachments), SelAt, 0);
        Call(attachment, SelSetSampleBuffer, sampleBuffer);
        Call(attachment, SelStartEncoder, (nuint) first);
        Call(attachment, SelEndEncoder, (nuint) (first + 1));
        return new MetalComputeEncoder(Send<IntPtr, IntPtr>(Handle, SelComputeWithDescriptor, descriptor));
    }

    private static readonly IntPtr SelGpuStart = Sel("GPUStartTime"), SelGpuEnd = Sel("GPUEndTime");

    // When the GPU started and finished this command buffer, in seconds; meaningful once it has completed,
    // and only as a span when nothing else was queued with it (docs/metal-renderer.md §3).
    public double GpuStartTime => Send<double>(Handle, SelGpuStart);
    public double GpuEndTime => Send<double>(Handle, SelGpuEnd);
    public MetalBlitEncoder Blit() => new(Send<IntPtr>(Handle, SelBlit));
    public void Present(IntPtr drawable) => Call(Handle, SelPresent, drawable);
    public void Commit() => Call(Handle, SelCommit);
    public void WaitUntilCompleted() => Call(Handle, SelWait);
    public CommandBufferStatus Status => Send<CommandBufferStatus>(Handle, SelStatus);
    public string? ErrorMessage => Send<IntPtr>(Handle, SelError) is var e && e != IntPtr.Zero ? ErrorText(e) : null;
}

internal readonly unsafe struct MetalRenderEncoder
{
    public readonly IntPtr Handle;   // autoreleased
    public MetalRenderEncoder(IntPtr handle) => Handle = handle;

    private static readonly IntPtr SelPipeline = Sel("setRenderPipelineState:"), SelVertexBytes = Sel("setVertexBytes:length:atIndex:"),
        SelFragmentBytes = Sel("setFragmentBytes:length:atIndex:"), SelDraw = Sel("drawPrimitives:vertexStart:vertexCount:"),
        SelEnd = Sel("endEncoding"), SelPushGroup = Sel("pushDebugGroup:"), SelPopGroup = Sel("popDebugGroup");

    public void SetPipeline(IntPtr pipeline) => Call(Handle, SelPipeline, pipeline);

    // Uniforms by value, at the [[buffer(n)]] index the shader reads them from (vertex buffers start above
    // them, at MetalPipelines.FirstVertexBuffer).
    public void SetVertexBytes<T>(in T data, int index) where T : unmanaged => Uniform.Set(Handle, SelVertexBytes, data, index);
    public void SetFragmentBytes<T>(in T data, int index) where T : unmanaged => Uniform.Set(Handle, SelFragmentBytes, data, index);

    public void Draw(PrimitiveType type, int start, int count) => Call(Handle, SelDraw, type, (nuint) start, (nuint) count);

    private static readonly IntPtr SelVertexBuffer = Sel("setVertexBuffer:offset:atIndex:"),
        SelVertexTexture = Sel("setVertexTexture:atIndex:"), SelVertexSampler = Sel("setVertexSamplerState:atIndex:"),
        SelFragmentTexture = Sel("setFragmentTexture:atIndex:"), SelFragmentSampler = Sel("setFragmentSamplerState:atIndex:"),
        SelDepthState = Sel("setDepthStencilState:"),
        SelDrawIndexed = Sel("drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:");

    public void SetPipeline(MetalPipeline pipeline)
    {
        SetPipeline(pipeline.State);
        if (pipeline.DepthState != IntPtr.Zero) Call(Handle, SelDepthState, pipeline.DepthState);
    }

    public void SetVertexBuffer(MetalBuffer buffer, int index) => Call(Handle, SelVertexBuffer, buffer.Handle, (nuint) 0, (nuint) index);
    public void SetVertexTexture(IntPtr texture, int index) => Call(Handle, SelVertexTexture, texture, (nuint) index);   // read with Load, no sampler
    public void SetVertexTexture(IntPtr texture, IntPtr sampler, int index)
    {
        Call(Handle, SelVertexTexture, texture, (nuint) index);
        Call(Handle, SelVertexSampler, sampler, (nuint) index);
    }

    public void SetFragmentTexture(IntPtr texture, IntPtr sampler, int index)
    {
        Call(Handle, SelFragmentTexture, texture, (nuint) index);
        Call(Handle, SelFragmentSampler, sampler, (nuint) index);
    }

    // Triangles from 32-bit indices.
    public void DrawIndexed(int indexCount, MetalBuffer indices)
        => Call(Handle, SelDrawIndexed, PrimitiveType.Triangle, (nuint) indexCount, (nuint) 1 /* MTLIndexTypeUInt32 */, indices.Handle, (nuint) 0);

    // Triangles from 16-bit indices, starting `firstIndex` into the buffer.
    public void DrawIndexed16(int indexCount, MetalBuffer indices, int firstIndex)
        => Call(Handle, SelDrawIndexed, PrimitiveType.Triangle, (nuint) indexCount, (nuint) 0 /* MTLIndexTypeUInt16 */, indices.Handle, (nuint) (firstIndex * 2));

    [StructLayout(LayoutKind.Sequential)]
    private struct MTLScissorRect { public nuint X, Y, Width, Height; }

    private static readonly IntPtr SelScissor = Sel("setScissorRect:");

    public void SetScissor(int x, int y, int width, int height)
        => Call(Handle, SelScissor, new MTLScissorRect { X = (nuint) x, Y = (nuint) y, Width = (nuint) width, Height = (nuint) height });
    public void PushGroup(string name) => Call(Handle, SelPushGroup, NSString(name));
    public void PopGroup() => Call(Handle, SelPopGroup);
    public void End() => Call(Handle, SelEnd);
}

// A uniform block as a shader's constant buffer has it: rounded up to whole 16-byte registers. A C# struct
// whose last register is not full is shorter than that (AmbientOcclusion's: 172 bytes against the
// shader's 176), and Metal's validation rejects a set*Bytes that short. SDL_GPU never shows it, because it
// copies each push into a larger buffer of its own. The padding is zeros.
internal static unsafe class Uniform
{
    // Pass `data` to a set*Bytes:length:atIndex: call on `encoder`, padded with zeros to whole registers.
    public static void Set<T>(IntPtr encoder, IntPtr selector, in T data, int index) where T : unmanaged
    {
        int size = sizeof(T), padded = (size + 15) & ~15;
        fixed (T* p = &data)
        {
            if (padded == size)
            {
                Call(encoder, selector, (IntPtr) p, (nuint) size, (nuint) index);
                return;
            }
            byte* copy = stackalloc byte[padded];
            new Span<byte>(copy, padded).Clear();
            Buffer.MemoryCopy(p, copy, padded, size);
            Call(encoder, selector, (IntPtr) copy, (nuint) padded, (nuint) index);
        }
    }
}

internal readonly unsafe struct MetalComputeEncoder
{
    public readonly IntPtr Handle;   // autoreleased
    public MetalComputeEncoder(IntPtr handle) => Handle = handle;

    private static readonly IntPtr SelPipeline = Sel("setComputePipelineState:"), SelTexture = Sel("setTexture:atIndex:"),
        SelSampler = Sel("setSamplerState:atIndex:"), SelBytes = Sel("setBytes:length:atIndex:"),
        SelDispatch = Sel("dispatchThreadgroups:threadsPerThreadgroup:"), SelEnd = Sel("endEncoding"),
        SelPushGroup = Sel("pushDebugGroup:"), SelPopGroup = Sel("popDebugGroup");

    public void SetPipeline(IntPtr pipeline) => Call(Handle, SelPipeline, pipeline);
    public void SetTexture(IntPtr texture, int index) => Call(Handle, SelTexture, texture, (nuint) index);
    public void SetSampler(IntPtr sampler, int index) => Call(Handle, SelSampler, sampler, (nuint) index);

    public void SetBytes<T>(in T data, int index) where T : unmanaged => Uniform.Set(Handle, SelBytes, data, index);

    public void Dispatch(MTLSize groups, MTLSize threadsPerGroup) => Call(Handle, SelDispatch, groups, threadsPerGroup);
    public void PushGroup(string name) => Call(Handle, SelPushGroup, NSString(name));
    public void PopGroup() => Call(Handle, SelPopGroup);
    public void End() => Call(Handle, SelEnd);
}

internal readonly struct MetalBlitEncoder
{
    public readonly IntPtr Handle;   // autoreleased
    public MetalBlitEncoder(IntPtr handle) => Handle = handle;

    private static readonly IntPtr SelCopyTexture = Sel("copyFromTexture:toTexture:"),
        SelCopyToBuffer = Sel("copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toBuffer:destinationOffset:destinationBytesPerRow:destinationBytesPerImage:"),
        SelEnd = Sel("endEncoding");

    // A whole texture into another of the same size and format.
    public void Copy(MetalTexture from, IntPtr toTexture) => Call(Handle, SelCopyTexture, from.Handle, toTexture);

    private static readonly IntPtr SelMipmaps = Sel("generateMipmapsForTexture:");

    // Every mip level below the first, filtered down from it (as SDL_GPU's GenerateMipmapsForGPUTexture).
    public void GenerateMipmaps(MetalTexture texture) => Call(Handle, SelMipmaps, texture.Handle);

    // A whole texture's first level into a buffer at `offset`, rows tightly packed.
    public void Copy(MetalTexture from, MetalBuffer to, int offset = 0, int bytesPerTexel = 4)
    {
        nuint row = from.Width * (nuint) bytesPerTexel;
        Call(Handle, SelCopyToBuffer, from.Handle, (nuint) 0, (nuint) 0, default(MTLOrigin), new MTLSize(from.Width, from.Height, 1),
            to.Handle, (nuint) offset, row, row * from.Height);
    }

    public void End() => Call(Handle, SelEnd);
}

internal readonly unsafe struct MetalTexture
{
    public readonly IntPtr Handle;   // owned unless it came from a drawable
    public readonly uint Width, Height;
    public MetalTexture(IntPtr handle, uint width, uint height) { Handle = handle; Width = width; Height = height; }

    public bool IsNull => Handle == IntPtr.Zero;

    private static readonly IntPtr SelReplace = Sel("replaceRegion:mipmapLevel:withBytes:bytesPerRow:");

    [StructLayout(LayoutKind.Sequential)]
    private struct MTLRegion { public MTLOrigin Origin; public MTLSize Size; }

    // Write a rectangle of the first mip level of a Shared texture from the CPU: `width`×`height` texels of
    // `bytesPerTexel`, rows tightly packed.
    public void ReplaceRegion(int x, int y, int width, int height, ReadOnlySpan<byte> texels, int bytesPerTexel)
    {
        var region = new MTLRegion { Origin = new MTLOrigin { X = (nuint) x, Y = (nuint) y }, Size = new MTLSize((nuint) width, (nuint) height, 1) };
        fixed (byte* p = texels) Call(Handle, SelReplace, region, (nuint) 0, (IntPtr) p, (nuint) (width * bytesPerTexel));
    }
}

// A render pipeline state and the depth state that goes with it (none for a pass without depth); owned.
internal readonly record struct MetalPipeline(IntPtr State, IntPtr DepthState)
{
    public void Release()
    {
        IntPtr state = State, depth = DepthState;
        ObjC.Release(ref state);
        ObjC.Release(ref depth);
    }
}

internal readonly unsafe struct MetalBuffer
{
    public readonly IntPtr Handle;   // owned
    public readonly nuint Length;
    public MetalBuffer(IntPtr handle, nuint length) { Handle = handle; Length = length; }

    private static readonly IntPtr SelContents = Sel("contents");

    public ReadOnlySpan<byte> Contents => new((void*) Send<IntPtr>(Handle, SelContents), (int) Length);
}

// The window's CAMetalLayer (made by Window), and the drawables it hands out to present.
internal readonly struct MetalLayer
{
    public readonly IntPtr Handle;
    public MetalLayer(IntPtr handle) => Handle = handle;

    private static readonly IntPtr SelDevice = Sel("setDevice:"), SelPixelFormat = Sel("setPixelFormat:"),
        SelDrawableSize = Sel("setDrawableSize:"), SelFramebufferOnly = Sel("setFramebufferOnly:"),
        SelNextDrawable = Sel("nextDrawable"), SelDisplaySync = Sel("setDisplaySyncEnabled:"), SelTexture = Sel("texture");

    public void Configure(MetalDevice device, PixelFormat format)
    {
        Call(Handle, SelDevice, device.Handle);
        Call(Handle, SelPixelFormat, format);
        Call(Handle, SelFramebufferOnly, (byte) 0);   // the frame is blitted in, which a framebuffer-only texture refuses
    }

    public void SetDrawableSize(uint width, uint height) => Call(Handle, SelDrawableSize, new CGSize { Width = width, Height = height });

    // Vsync on or off (CAMetalLayer.displaySyncEnabled, macOS only).
    public void SetDisplaySync(bool enabled) => Call(Handle, SelDisplaySync, (byte) (enabled ? 1 : 0));

    // The next image to present, autoreleased; zero when none comes within a second.
    public IntPtr NextDrawable() => Send<IntPtr>(Handle, SelNextDrawable);

    public static IntPtr TextureOf(IntPtr drawable) => Send<IntPtr>(drawable, SelTexture);
}
