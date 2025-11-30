// Vulkan顶点着色器 - 用于音符渲染
#version 450

layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec2 fragTexCoord;

layout(push_constant) uniform PushConstants {
    mat4 projection;
    vec4 color;
    vec2 size;
    float radius;
    float padding;
} pushConstants;

void main() {
    // 使用逆时针(CCW)顺序定义顶点，以配合Vulkan的默认剔除模式
    vec2 positions[6] = vec2[](
        vec2(0.0, 0.0), // Top-Left
        vec2(0.0, 1.0), // Bottom-Left
        vec2(1.0, 1.0), // Bottom-Right
        vec2(1.0, 1.0), // Bottom-Right
        vec2(1.0, 0.0), // Top-Right
        vec2(0.0, 0.0)  // Top-Left
    );
    
    vec2 texCoords[6] = vec2[](
        vec2(0.0, 0.0), // Top-Left
        vec2(0.0, 1.0), // Bottom-Left
        vec2(1.0, 1.0), // Bottom-Right
        vec2(1.0, 1.0), // Bottom-Right
        vec2(1.0, 0.0), // Top-Right
        vec2(0.0, 0.0)  // Top-Left
    );

    vec2 pos = positions[gl_VertexIndex];
    
    gl_Position = pushConstants.projection * vec4(pos, 0.0, 1.0);
    fragColor = pushConstants.color;
    fragTexCoord = texCoords[gl_VertexIndex];
}