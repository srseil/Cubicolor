// The 2D overlay: menus and text, as triangles in pixels sampling a single-channel coverage atlas.
#include <metal_stdlib>
using namespace metal;

struct Viewport
{
    float2 size;   // in pixels
};

struct VertexIn
{
    float2 position [[attribute(0)]];
    float2 texCoord [[attribute(1)]];
    float4 color [[attribute(2)]];
};

struct Fragment
{
    float4 position [[position]];
    float2 texCoord;
    float4 color;
};

vertex Fragment overlay_vertex(VertexIn in [[stage_in]], constant Viewport& viewport [[buffer(0)]])
{
    Fragment out;
    float2 ndc = in.position / viewport.size * float2(2.0, -2.0) + float2(-1.0, 1.0);
    out.position = float4(ndc, 0.0, 1.0);
    out.texCoord = in.texCoord;
    out.color = in.color;
    return out;
}

fragment float4 overlay_fragment(Fragment in [[stage_in]], texture2d<float> atlas [[texture(0)]], sampler linear [[sampler(0)]])
{
    float coverage = atlas.sample(linear, in.texCoord).r;
    return float4(in.color.rgb, in.color.a * coverage);
}
