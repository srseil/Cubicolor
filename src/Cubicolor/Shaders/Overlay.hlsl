// The 2D overlay: menus and text, as triangles in pixels sampling a single-channel coverage atlas. The same
// shader as Overlay.metal.

cbuffer ViewportConstants : register(b0)
{
    float2 size;   // in pixels
};

Texture2D<float> atlas : register(t0);
SamplerState linearSampler : register(s0);

struct VertexIn
{
    float2 position : POSITION;
    float2 texCoord : TEXCOORD0;
    float4 color : COLOR;
};

struct Fragment
{
    float4 position : SV_Position;
    float2 texCoord : TEXCOORD0;
    float4 color : COLOR;
};

Fragment overlay_vertex(VertexIn input)
{
    Fragment output;
    float2 ndc = input.position / size * float2(2.0, -2.0) + float2(-1.0, 1.0);
    output.position = float4(ndc, 0.0, 1.0);
    output.texCoord = input.texCoord;
    output.color = input.color;
    return output;
}

float4 overlay_fragment(Fragment input) : SV_Target
{
    float coverage = atlas.Sample(linearSampler, input.texCoord);
    return float4(input.color.rgb, input.color.a * coverage);
}
