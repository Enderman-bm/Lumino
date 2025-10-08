// Vulkan片段着色器 - 用于音符渲染
#version 450

layout(location = 0) in vec4 fragColor;
layout(location = 1) in vec2 fragTexCoord;

layout(location = 0) out vec4 outColor;

layout(push_constant) uniform PushConstants {
    mat4 projection;
    vec4 color;
    float radius;
} pushConstants;

void main() {
    // 圆角矩形渲染
    vec2 center = vec2(0.5, 0.5);
    vec2 pos = fragTexCoord - center;
    float dist = length(pos);

    // 简单的圆角计算
    float alpha = 1.0;
    if (pushConstants.radius > 0.0) {
        float maxRadius = min(0.5, pushConstants.radius);
        if (dist > (0.5 - maxRadius)) {
            float circleDist = dist - (0.5 - maxRadius);
            alpha = 1.0 - smoothstep(0.0, maxRadius, circleDist);
        }
    }

    outColor = fragColor * vec4(1.0, 1.0, 1.0, alpha);
}