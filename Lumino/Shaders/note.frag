// Vulkan片段着色器 - 用于音符渲染
#version 450

layout(location = 0) in vec4 fragColor;
layout(location = 1) in vec2 fragTexCoord;

layout(location = 0) out vec4 outColor;

layout(push_constant) uniform PushConstants {
    mat4 projection;
    vec4 color;
    vec2 size;
    float radius;
    float padding;
} pushConstants;

void main() {
    // 圆角矩形渲染
    vec2 size = pushConstants.size;
    float radius = pushConstants.radius;
    
    // 将UV坐标(0..1)转换为像素坐标，中心为(0,0)
    vec2 p = (fragTexCoord - 0.5) * size;
    
    // 计算半尺寸（减去半径）
    // 确保半径不超过尺寸的一半
    float r = min(radius, min(size.x, size.y) * 0.5);
    vec2 b = size * 0.5 - vec2(r);
    
    // SDF计算 (Signed Distance Field)
    vec2 d = abs(p) - b;
    float dist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
    
    // 抗锯齿处理 (边缘平滑)
    // dist < 0 在形状内部，dist > 0 在形状外部
    // 使用smoothstep在边缘附近产生平滑过渡
    float alpha = 1.0 - smoothstep(-0.5, 0.5, dist);

    outColor = pushConstants.color * vec4(1.0, 1.0, 1.0, alpha);
}