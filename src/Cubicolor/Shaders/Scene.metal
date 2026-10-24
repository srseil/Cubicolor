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
    float4 pattern;          // x: Surface, y: spread, z: drained
    float4 paintUp;          // xyz: away from the face the paint starts at, in the mesh's own space
    float4 drainUp;          // xyz: away from the face the paint drains into
};

struct VertexIn
{
    float3 position [[attribute(0)]];
    float3 normal [[attribute(1)]];
};

struct Fragment
{
    // Invariant: the depth pass and the colour pass of a translucent mesh must reach exactly the same depth.
    float4 position [[position, invariant]];
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

// The paint's front: how far along the surface a point is from the face the paint starts at (the cube's
// bottom when it began: `up` points away from it, in the cube's own space), going up the side the point is on
// and then in over the opposite face towards its middle: 0 at the bottom, 1 at the top edge, 1.5 at the
// top's centre. The tongues are a smooth wave along the cube's girth, so the paint rises in fingers; they
// grow in from nothing at the bottom, so the paint starts evenly all round.
static float paint_reach(float3 position, float3 normal, float3 up)
{
    float3 a = abs(up.y) > 0.5 ? float3(1, 0, 0) : float3(0, 1, 0);
    float3 b = cross(up, a);
    float3 p = float3(dot(position, a), dot(position, up), dot(position, b));
    float3 n = float3(dot(normal, a), dot(normal, up), dot(normal, b));
    if (n.y < -0.7) return -0.05;

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
    return height + tongues * smoothstep(0.0, 0.3, height);
}

fragment float4 scene_fragment(Fragment in [[stage_in]], constant Frame& frame [[buffer(0)]], constant Draw& draw [[buffer(1)]])
{
    float3 base = draw.color.rgb;
    int surface = int(draw.pattern.x);
    // Every coverage is worked out before anything branches per pixel: fwidth needs its neighbours to have run
    // the same code, and on the top face's edge they would not have (which drew a dashed line there).
    float2 p = in.local.xz;
    float plus = coverage(min(rounded_box(p, float2(0.27, 0.095), 0.095), rounded_box(p, float2(0.095, 0.27), 0.095)));
    float square = coverage(rounded_box(p, float2(0.36, 0.36), 0.07));
    float reach = paint_reach(in.local, in.localNormal, draw.paintUp.xyz);
    float spread = coverage(reach - draw.pattern.y * 1.65);
    // What is left as it drains: everything below a front sinking towards the face the cube stands on.
    float remaining = 1.0 - draw.pattern.z;
    float kept = coverage(paint_reach(in.local, in.localNormal, draw.drainUp.xyz) - remaining * 1.65);
    float top = in.localNormal.y > 0.7 ? 1.0 : 0.0;

    // The key's plus and the exit's square on the top face; the paint spreading over the cube, and what is left
    // of it while it drains (none at all at either end, not even on the face it starts from or drains into).
    float painted = (draw.pattern.y > 0.0 ? spread : 0.0) * (remaining > 0.0 ? kept : 0.0);
    float amount = surface == 1 ? plus * top : surface == 2 ? square * top : surface == 3 ? painted : 0.0;
    base = mix(base, draw.accent.rgb, amount * draw.accent.a);

    float3 n = normalize(in.normal);
    float3 l = normalize(frame.lightDirection.xyz);
    float3 v = -normalize(frame.viewDirection.xyz);
    float diffuse = max(dot(n, l), 0.0);
    float3 h = normalize(l + v);
    float highlight = pow(max(dot(n, h), 0.0), frame.specular.y) * frame.specular.x;
    float3 lit = base * (frame.ambient.rgb + frame.diffuse.rgb * diffuse) + highlight;
    return float4(lit, draw.color.a);
}
