// The board, the cube and the exit's stack: lit rounded boxes with their surface patterns. Colours are linear;
// the render target is sRGB, so blending happens in linear light. The same shader as Scene.metal, line for line.

static const float PI = 3.14159265;

cbuffer FrameConstants : register(b0)
{
    float4x4 viewProjection;
    float4 lightDirection;   // xyz: towards the light
    float4 ambient;
    float4 diffuse;
    float4 viewDirection;    // xyz: the way the camera looks
    float4 specular;         // x: strength, y: shininess
};

cbuffer DrawConstants : register(b1)
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
    float3 position : POSITION;
    float3 normal : NORMAL;
};

struct Fragment
{
    float4 position : SV_Position;
    float3 normal : NORMAL;
    float3 local : TEXCOORD0;
    float3 localNormal : TEXCOORD1;
};

Fragment scene_vertex(VertexIn input)
{
    Fragment output;
    // Precise: the depth pass and the colour pass of a translucent mesh must reach exactly the same depth.
    precise float4 position = mul(viewProjection, mul(model, float4(input.position, 1.0)));
    output.position = position;
    output.normal = mul(model, float4(input.normal, 0.0)).xyz;
    output.local = input.position;
    output.localNormal = input.normal;
    return output;
}

// Signed distance to a rectangle of half-size `half_size` with corners rounded by `radius`.
float rounded_box(float2 p, float2 half_size, float radius)
{
    float2 q = abs(p) - half_size + radius;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

// How much of a pixel lies inside a shape, from its signed distance: a one-pixel smooth edge.
float coverage(float distance)
{
    float width = max(fwidth(distance), 1e-5);
    return saturate(0.5 - distance / width);
}

// The paint's front: how far along the surface a point is from the face the paint starts at (see Scene.metal).
float paint_reach(float3 position, float3 normal, float3 up)
{
    float3 a = abs(up.y) > 0.5 ? float3(1, 0, 0) : float3(0, 1, 0);
    float3 b = cross(up, a);
    float3 p = float3(dot(position, a), dot(position, up), dot(position, b));
    float3 n = float3(dot(normal, a), dot(normal, up), dot(normal, b));
    if (n.y < -0.7) return -0.05;

    float girth;
    float height;
    bool far = n.y > 0.7;
    if (far)
    {
        float2 a4 = pow(abs(p.xz), 4.0);
        height = 1.0 + max(0.0, 0.5 - pow(a4.x + a4.y, 0.25));
        girth = atan2(p.z, p.x) / (2.0 * PI) * 4.0;
    }
    else
    {
        height = p.y + 0.5;
        if (abs(n.x) > abs(n.z)) girth = (n.x > 0 ? 0.0 : 2.0) + (n.x > 0 ? p.z : -p.z) + 0.5;
        else girth = (n.z > 0 ? 1.0 : 3.0) + (n.z > 0 ? -p.x : p.x) + 0.5;
    }
    float slow = 0.06 * sin(girth * 2.0 * PI * 2.0 + 0.7);
    float quick = 0.04 * sin(girth * 2.0 * PI * 5.0 + 2.1) * (1.0 - smoothstep(0.95, 1.1, height));
    float left = far ? saturate((1.5 - height) * 2.0) : 1.0;
    float tongues = (slow + quick) * smoothstep(0.0, 0.3, height) * left * left;
    return height + tongues;
}

float4 scene_fragment(Fragment input) : SV_Target
{
    float3 base = color.rgb;
    int surface = (int) pattern.x;
    // Every coverage is worked out before anything branches per pixel: fwidth needs its neighbours to have run
    // the same code.
    float2 p = input.local.xz;
    float plus = coverage(min(rounded_box(p, float2(0.27, 0.095), 0.095), rounded_box(p, float2(0.095, 0.27), 0.095)));
    float square = coverage(rounded_box(p, float2(0.36, 0.36), 0.07));
    float reach = paint_reach(input.local, input.localNormal, paintUp.xyz);
    float spread = coverage(reach - pattern.y * 1.65);
    float remaining = 1.0 - pattern.z;
    float kept = coverage(paint_reach(input.local, input.localNormal, drainUp.xyz) - remaining * 1.65);
    float top = input.localNormal.y > 0.7 ? 1.0 : 0.0;

    float painted = (pattern.y > 0.0 ? spread : 0.0) * (remaining > 0.0 ? kept : 0.0);
    float amount = surface == 1 ? plus * top : surface == 2 ? square * top : surface == 3 ? painted : 0.0;
    base = lerp(base, accent.rgb, amount * accent.a);

    float3 n = normalize(input.normal);
    float3 l = normalize(lightDirection.xyz);
    float3 v = -normalize(viewDirection.xyz);
    float lambert = max(dot(n, l), 0.0);
    float3 h = normalize(l + v);
    float highlight = pow(max(dot(n, h), 0.0), specular.y) * specular.x;
    float3 lit = base * (ambient.rgb + diffuse.rgb * lambert) + highlight;
    return float4(lit, color.a);
}
