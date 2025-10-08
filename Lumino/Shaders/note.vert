// Vulkan顶点着色器 - 用于音符渲染
#version 450

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec2 inTexCoord;
layout(location = 2) in vec4 inColor;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec2 fragTexCoord;

layout(push_constant) uniform PushConstants {
    mat4 projection;
    vec4 color;
    float radius;
} pushConstants;

void main() {
    gl_Position = pushConstants.projection * vec4(inPosition, 0.0, 1.0);
    fragColor = inColor * pushConstants.color;
    fragTexCoord = inTexCoord;
}