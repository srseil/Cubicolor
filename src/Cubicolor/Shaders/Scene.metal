// The board, the cube and the exit's stack: lit rounded boxes with their surface patterns. Colours are linear;
// the render target is sRGB, so blending happens in linear light.
#include <metal_stdlib>
using namespace metal;

struct Frame
{
    float4x4 viewProjection;
    float4 lightDirection;   // xyz: towards the light
    float4 ambient;
    float4 diffuse;
    float4 viewDirection;    // xyz: the way the camera looks
    float4 specular;         // x: strength, y: shininess
};

struct Draw
{
    float4x4 model;
    float4 color;            // rgb, and the opacity of the whole mesh
    float4 accent;
    float4 pattern;          // x: Surface, y: progress
};

struct VertexIn
{
    float3 position [[attribute(0)]];
    float3 normal [[attribute(1)]];
};

struct Fragment
{
    float4 position [[position]];
    float3 normal;
    float3 local;
    float3 localNormal;
};

vertex Fragment scene_vertex(VertexIn in [[stage_in]], constant Frame& frame [[buffer(0)]], constant Draw& draw [[buffer(1)]])
{
    Fragment out;
    float4 world = draw.model * float4(in.position, 1.0);
    out.position = frame.viewProjection * world;
    out.normal = (draw.model * float4(in.normal, 0.0)).xyz;
    out.local = in.position;
    out.localNormal = in.normal;
    return out;
}

// Signed distance to a rectangle of half-size `half` with corners rounded by `radius`.
static float rounded_box(float2 p, float2 half_size, float radius)
{
    float2 q = abs(p) - half_size + radius;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

// How much of a pixel lies inside a shape, from its signed distance: a one-pixel smooth edge.
static float coverage(float distance)
{
    float width = max(fwidth(distance), 1e-5);
    return clamp(0.5 - distance / width, 0.0, 1.0);
}

// The paint's front: how far along the surface a point is from the cube's bottom edge, going up the side
// it is on and then in over the top towards the middle (0 at the bottom, 1 at the top edge, 1.5 at the
// top's centre). The fingers are a smooth wave along the cube's girth, so the paint rises in tongues.
static float paint_reach(float3 p, float3 n)
{
    float girth;
    float height;
    if (n.y > 0.7)
    {
        height = 1.0 + (0.5 - max(abs(p.x), abs(p.z)));
        girth = atan2(p.z, p.x) / (2.0 * M_PI_F) * 4.0;
    }
    else
    {
        height = p.y + 0.5;
        // Around the cube: each side face is one unit of girth.
        if (abs(n.x) > abs(n.z)) girth = (n.x > 0 ? 0.0 : 2.0) + (n.x > 0 ? p.z : -p.z) + 0.5;
        else girth = (n.z > 0 ? 1.0 : 3.0) + (n.z > 0 ? -p.x : p.x) + 0.5;
    }
    float tongues = 0.06 * sin(girth * 2.0 * M_PI_F * 2.0 + 0.7) + 0.04 * sin(girth * 2.0 * M_PI_F * 5.0 + 2.1);
    return height + tongues;
}

fragment float4 scene_fragment(Fragment in [[stage_in]], constant Frame& frame [[buffer(0)]], constant Draw& draw [[buffer(1)]])
{
    float3 base = draw.color.rgb;
    int surface = int(draw.pattern.x);
    bool top = in.localNormal.y > 0.7;
    if (surface == 1 && top)
    {
        // The key's plus: two rounded bars across the top face.
        float2 p = in.local.xz;
        float bar = min(rounded_box(p, float2(0.21, 0.075), 0.075), rounded_box(p, float2(0.075, 0.21), 0.075));
        base = mix(base, draw.accent.rgb, coverage(bar) * draw.accent.a);
    }
    else if (surface == 2 && top)
    {
        float square = rounded_box(in.local.xz, float2(0.36, 0.36), 0.07);
        base = mix(base, draw.accent.rgb, coverage(square) * draw.accent.a);
    }
    else if (surface == 3)
    {
        // Paint reaches 1.6 (past the top's centre, fingers and all) as progress reaches 1.
        float front = draw.pattern.y * 1.65;
        float reach = paint_reach(in.local, in.localNormal);
        base = mix(base, draw.accent.rgb, coverage(reach - front) * draw.accent.a);
    }

    float3 n = normalize(in.normal);
    float3 l = normalize(frame.lightDirection.xyz);
    float3 v = -normalize(frame.viewDirection.xyz);
    float diffuse = max(dot(n, l), 0.0);
    float3 h = normalize(l + v);
    float highlight = pow(max(dot(n, h), 0.0), frame.specular.y) * frame.specular.x;
    float3 lit = base * (frame.ambient.rgb + frame.diffuse.rgb * diffuse) + highlight;
    return float4(lit, draw.color.a);
}
