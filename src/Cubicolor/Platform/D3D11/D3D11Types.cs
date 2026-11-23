using System;
using System.Runtime.InteropServices;
using System.Text;
using Cubicolor.Platform.Windows;

namespace Cubicolor.Platform.D3D11;

// The Direct3D 11 and DXGI calls the renderer makes, through the vtables (Com): the device, its immediate
// context and the swap chain, each method at its slot in d3d11.h / dxgi1_2.h, and the descriptions they take.

internal enum Format : uint
{
    R32G32B32A32Float = 2, R32G32B32Float = 6, R32G32Float = 16, D32Float = 40, R16UInt = 57, R8UNorm = 61,
    B8G8R8A8UNorm = 87, B8G8R8A8UNormSrgb = 91,
}

[StructLayout(LayoutKind.Sequential)]
internal struct BufferDesc
{
    public uint ByteWidth, Usage, BindFlags, CpuAccessFlags, MiscFlags, StructureByteStride;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct SubresourceData
{
    public void* Memory;
    public uint Pitch, SlicePitch;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Texture2DDesc
{
    public uint Width, Height, MipLevels, ArraySize;
    public Format Format;
    public uint SampleCount, SampleQuality, Usage, BindFlags, CpuAccessFlags, MiscFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct InputElement
{
    public byte* SemanticName;
    public uint SemanticIndex;
    public Format Format;
    public uint InputSlot, AlignedByteOffset, InputSlotClass, InstanceDataStepRate;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RasterizerDesc
{
    public int FillMode, CullMode, FrontCounterClockwise, DepthBias;
    public float DepthBiasClamp, SlopeScaledDepthBias;
    public int DepthClipEnable, ScissorEnable, MultisampleEnable, AntialiasedLineEnable;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TargetBlendDesc
{
    public int BlendEnable, SrcBlend, DestBlend, BlendOp, SrcBlendAlpha, DestBlendAlpha, BlendOpAlpha;
    public byte WriteMask;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BlendDesc
{
    public int AlphaToCoverageEnable, IndependentBlendEnable;
    public TargetBlendDesc Target0;
    // The other seven targets' descriptions, unused (IndependentBlendEnable off).
    private unsafe fixed byte _others[7 * 32];
}

[StructLayout(LayoutKind.Sequential)]
internal struct DepthStencilDesc
{
    public int DepthEnable, DepthWriteMask, DepthFunc, StencilEnable;
    public byte StencilReadMask, StencilWriteMask;
    public int FrontFail, FrontDepthFail, FrontPass, FrontFunc, BackFail, BackDepthFail, BackPass, BackFunc;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct SamplerDesc
{
    public int Filter, AddressU, AddressV, AddressW;
    public float MipLodBias;
    public uint MaxAnisotropy;
    public int ComparisonFunc;
    public fixed float BorderColor[4];
    public float MinLod, MaxLod;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Viewport
{
    public float X, Y, Width, Height, MinDepth, MaxDepth;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Mapped
{
    public void* Data;
    public uint RowPitch, DepthPitch;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Box
{
    public uint Left, Top, Front, Right, Bottom, Back;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SwapChainDesc1
{
    public uint Width, Height;
    public Format Format;
    public int Stereo;
    public uint SampleCount, SampleQuality, BufferUsage, BufferCount, Scaling, SwapEffect, AlphaMode, Flags;
}

internal static class D3D
{
    public const uint UsageDefault = 0, UsageImmutable = 1, UsageDynamic = 2, UsageStaging = 3;
    public const uint BindVertex = 0x1, BindIndex = 0x2, BindConstant = 0x4, BindShaderResource = 0x8, BindRenderTarget = 0x20,
        BindDepthStencil = 0x40;
    public const uint CpuWrite = 0x10000, CpuRead = 0x20000;
    public const int MapRead = 1, MapWriteDiscard = 4;
    public const int BlendZero = 1, BlendOne = 2, BlendSrcAlpha = 5, BlendInvSrcAlpha = 6, BlendOpAdd = 1;
    public const int CompareNever = 1, CompareLessEqual = 4, CompareAlways = 8;
}

// ID3D11Device.
internal readonly unsafe struct Device(IntPtr handle)
{
    public IntPtr Handle { get; } = handle;

    public IntPtr CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initial = default)
    {
        IntPtr buffer;
        fixed (byte* data = initial)
        {
            var sub = new SubresourceData { Memory = data };
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, BufferDesc*, SubresourceData*, IntPtr*, int>) Com.Slot(Handle, 3))(
                Handle, &desc, initial.IsEmpty ? null : &sub, &buffer), "CreateBuffer");
        }
        return buffer;
    }

    public IntPtr CreateTexture2D(Texture2DDesc desc)
    {
        IntPtr texture;
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, Texture2DDesc*, SubresourceData*, IntPtr*, int>) Com.Slot(Handle, 5))(Handle, &desc, null, &texture),
            "CreateTexture2D");
        return texture;
    }

    public IntPtr CreateShaderResourceView(IntPtr resource) => CreateView(7, resource, "CreateShaderResourceView");
    public IntPtr CreateRenderTargetView(IntPtr resource) => CreateView(9, resource, "CreateRenderTargetView");
    public IntPtr CreateDepthStencilView(IntPtr resource) => CreateView(10, resource, "CreateDepthStencilView");

    // The three Create*View methods share a shape: the resource, a description (none: the resource's own), the view.
    private IntPtr CreateView(int slot, IntPtr resource, string what)
    {
        IntPtr view;
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void*, IntPtr*, int>) Com.Slot(Handle, slot))(Handle, resource, null, &view), what);
        return view;
    }

    public IntPtr CreateInputLayout(ReadOnlySpan<(string Semantic, Format Format, uint Offset)> elements, ReadOnlySpan<byte> vertexShader)
    {
        var descs = stackalloc InputElement[elements.Length];
        var names = new byte[elements.Length][];
        for (int i = 0; i < elements.Length; i++) names[i] = Encoding.ASCII.GetBytes(elements[i].Semantic + "\0");
        IntPtr layout;
        // The names must stay put until the call returns.
        var pins = new GCHandle[elements.Length];
        try
        {
            for (int i = 0; i < elements.Length; i++)
            {
                pins[i] = GCHandle.Alloc(names[i], GCHandleType.Pinned);
                descs[i] = new InputElement
                {
                    SemanticName = (byte*) pins[i].AddrOfPinnedObject(),
                    Format = elements[i].Format,
                    AlignedByteOffset = elements[i].Offset,
                };
            }
            fixed (byte* code = vertexShader)
                Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, InputElement*, uint, byte*, nuint, IntPtr*, int>) Com.Slot(Handle, 11))(
                    Handle, descs, (uint) elements.Length, code, (nuint) vertexShader.Length, &layout), "CreateInputLayout");
        }
        finally
        {
            foreach (GCHandle pin in pins)
                if (pin.IsAllocated) pin.Free();
        }
        return layout;
    }

    public IntPtr CreateVertexShader(ReadOnlySpan<byte> code) => CreateShader(12, code, "CreateVertexShader");
    public IntPtr CreatePixelShader(ReadOnlySpan<byte> code) => CreateShader(15, code, "CreatePixelShader");

    private IntPtr CreateShader(int slot, ReadOnlySpan<byte> code, string what)
    {
        IntPtr shader;
        fixed (byte* bytes = code)
            Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, byte*, nuint, IntPtr, IntPtr*, int>) Com.Slot(Handle, slot))(
                Handle, bytes, (nuint) code.Length, IntPtr.Zero, &shader), what);
        return shader;
    }

    public IntPtr CreateBlendState(BlendDesc desc) => CreateState(20, &desc, "CreateBlendState");
    public IntPtr CreateDepthStencilState(DepthStencilDesc desc) => CreateState(21, &desc, "CreateDepthStencilState");
    public IntPtr CreateRasterizerState(RasterizerDesc desc) => CreateState(22, &desc, "CreateRasterizerState");
    public IntPtr CreateSamplerState(SamplerDesc desc) => CreateState(23, &desc, "CreateSamplerState");

    // A query: TIMESTAMP is 2, TIMESTAMP_DISJOINT 3.
    public IntPtr CreateQuery(uint kind)
    {
        uint* desc = stackalloc uint[2] { kind, 0 };
        return CreateState(24, desc, "CreateQuery");
    }

    private IntPtr CreateState(int slot, void* desc, string what)
    {
        IntPtr state;
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, void*, IntPtr*, int>) Com.Slot(Handle, slot))(Handle, desc, &state), what);
        return state;
    }

    // The reason the device was lost (DXGI_ERROR_DEVICE_REMOVED and its kin), for the error message.
    public int RemovedReason => ((delegate* unmanaged[Stdcall]<IntPtr, int>) Com.Slot(Handle, 39))(Handle);
}

// ID3D11DeviceContext, the immediate one.
internal readonly unsafe struct Context(IntPtr handle)
{
    public IntPtr Handle { get; } = handle;

    public void VSSetConstantBuffer(uint slot, IntPtr buffer) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>) Com.Slot(Handle, 7))(Handle, slot, 1, &buffer);

    public void PSSetShaderResource(uint slot, IntPtr view) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>) Com.Slot(Handle, 8))(Handle, slot, 1, &view);

    public void PSSetShader(IntPtr shader) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, uint, void>) Com.Slot(Handle, 9))(Handle, shader, null, 0);

    public void PSSetSampler(uint slot, IntPtr sampler) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>) Com.Slot(Handle, 10))(Handle, slot, 1, &sampler);

    public void VSSetShader(IntPtr shader) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, uint, void>) Com.Slot(Handle, 11))(Handle, shader, null, 0);

    public void DrawIndexed(int count) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int, void>) Com.Slot(Handle, 12))(Handle, (uint) count, 0, 0);

    public void Draw(int count, int first) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, void>) Com.Slot(Handle, 13))(Handle, (uint) count, (uint) first);

    public Mapped Map(IntPtr resource, int type)
    {
        Mapped mapped;
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, uint, Mapped*, int>) Com.Slot(Handle, 14))(Handle, resource, 0, type, 0, &mapped),
            "Map");
        return mapped;
    }

    public void Unmap(IntPtr resource) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, void>) Com.Slot(Handle, 15))(Handle, resource, 0);

    // Writes `value` over the whole of a dynamic buffer.
    public void Write<T>(IntPtr buffer, in T value) where T : unmanaged
    {
        Mapped mapped = Map(buffer, D3D.MapWriteDiscard);
        *(T*) mapped.Data = value;
        Unmap(buffer);
    }

    public void PSSetConstantBuffer(uint slot, IntPtr buffer) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, void>) Com.Slot(Handle, 16))(Handle, slot, 1, &buffer);

    public void IASetInputLayout(IntPtr layout) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>) Com.Slot(Handle, 17))(Handle, layout);

    public void IASetVertexBuffer(IntPtr buffer, uint stride)
    {
        uint offset = 0;
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr*, uint*, uint*, void>) Com.Slot(Handle, 18))(Handle, 0, 1, &buffer, &stride, &offset);
    }

    public void IASetIndexBuffer(IntPtr buffer, Format format) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Format, uint, void>) Com.Slot(Handle, 19))(Handle, buffer, format, 0);

    public void IASetTriangleList() =>
        ((delegate* unmanaged[Stdcall]<IntPtr, int, void>) Com.Slot(Handle, 24))(Handle, 4 /* D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST */);

    public void Begin(IntPtr query) => ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>) Com.Slot(Handle, 27))(Handle, query);
    public void End(IntPtr query) => ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>) Com.Slot(Handle, 28))(Handle, query);

    // A query's result, waiting for the GPU to have it.
    public T GetData<T>(IntPtr query) where T : unmanaged
    {
        T value;
        while (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void*, uint, uint, int>) Com.Slot(Handle, 29))(Handle, query, &value, (uint) sizeof(T), 0) == 1)
            System.Threading.Thread.Yield();
        return value;
    }

    public void OMSetRenderTarget(IntPtr target, IntPtr depth) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, IntPtr, void>) Com.Slot(Handle, 33))(Handle, 1, &target, depth);

    public void OMSetBlendState(IntPtr state) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, float*, uint, void>) Com.Slot(Handle, 35))(Handle, state, null, 0xFFFFFFFF);

    public void OMSetDepthStencilState(IntPtr state) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, void>) Com.Slot(Handle, 36))(Handle, state, 0);

    public void RSSetState(IntPtr state) => ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, void>) Com.Slot(Handle, 43))(Handle, state);

    public void RSSetViewport(Viewport viewport) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, Viewport*, void>) Com.Slot(Handle, 44))(Handle, 1, &viewport);

    public void CopyResource(IntPtr destination, IntPtr source) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, void>) Com.Slot(Handle, 47))(Handle, destination, source);

    public void UpdateSubresource(IntPtr resource, Box box, ReadOnlySpan<byte> data, uint rowPitch)
    {
        fixed (byte* bytes = data)
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, Box*, byte*, uint, uint, void>) Com.Slot(Handle, 48))(Handle, resource, 0, &box, bytes, rowPitch, 0);
    }

    public void ClearRenderTarget(IntPtr view, float r, float g, float b, float a)
    {
        float* color = stackalloc float[4] { r, g, b, a };
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, float*, void>) Com.Slot(Handle, 50))(Handle, view, color);
    }

    public void ClearDepth(IntPtr view, float depth) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, float, byte, void>) Com.Slot(Handle, 53))(Handle, view, 1 /* D3D11_CLEAR_DEPTH */, depth, 0);

    public void ResolveSubresource(IntPtr destination, IntPtr source, Format format) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, IntPtr, uint, Format, void>) Com.Slot(Handle, 57))(Handle, destination, 0, source, 0, format);

    public void ClearState() => ((delegate* unmanaged[Stdcall]<IntPtr, void>) Com.Slot(Handle, 110))(Handle);
}

// IDXGISwapChain1.
internal readonly unsafe struct SwapChain(IntPtr handle)
{
    public IntPtr Handle { get; } = handle;

    public int Present(uint interval, uint flags) =>
        ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>) Com.Slot(Handle, 8))(Handle, interval, flags);

    public IntPtr Buffer()
    {
        Guid iid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");   // ID3D11Texture2D
        IntPtr texture;
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, Guid*, IntPtr*, int>) Com.Slot(Handle, 9))(Handle, 0, &iid, &texture), "GetBuffer");
        return texture;
    }

    public void Resize(uint width, uint height, uint flags) =>
        Com.Check(((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, Format, uint, int>) Com.Slot(Handle, 13))(Handle, 0, width, height, 0, flags),
            "ResizeBuffers");
}
