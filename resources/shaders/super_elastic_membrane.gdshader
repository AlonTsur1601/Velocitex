shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec4 rubber_color : source_color = vec4(0.14, 0.08, 0.24, 1.0);
uniform vec4 cell_color : source_color = vec4(0.48, 0.24, 0.7, 1.0);
uniform vec4 seam_color : source_color = vec4(0.055, 0.025, 0.085, 1.0);
uniform float motion_scale : hint_range(0.0, 1.0, 0.05) = 1.0;

const float TWO_PI = 6.28318530718;

float membrane_field(vec2 uv) {
	vec2 p = uv * vec2(9.0, 11.0);
	float a = cos(p.x * TWO_PI);
	float b = cos(((p.x * 0.5) + (p.y * 0.8660254)) * TWO_PI);
	float c = cos(((p.x * 0.5) - (p.y * 0.8660254)) * TWO_PI);
	return max(a, max(b, c));
}

void vertex() {
	float pulse = sin((UV.x * 2.0 + UV.y * 1.65) * TWO_PI + TIME * 2.6) * 0.035;
	VERTEX += NORMAL * pulse * max(NORMAL.y, 0.0) * motion_scale;
}

void fragment() {
	float cells = membrane_field(UV);
	float ribs = smoothstep(0.76, 0.94, cells);
	float inner = smoothstep(0.18, 0.62, cells) * (1.0 - ribs);
	float fine = sin((UV.x * 149.0 + UV.y * 131.0) * TWO_PI) * 0.5 + 0.5;
	float pores = smoothstep(0.91, 0.985, sin(UV.x * 211.0 * TWO_PI) * sin(UV.y * 197.0 * TWO_PI) * 0.5 + 0.5);
	float travelling = 0.5 + 0.5 * sin((UV.x + UV.y) * 3.0 * TWO_PI - TIME * 1.8 * motion_scale);

	vec3 color = mix(rubber_color.rgb, cell_color.rgb, inner * 0.34);
	color = mix(color, seam_color.rgb, ribs * 0.82);
	color *= 0.92 + fine * 0.11;
	color = mix(color, seam_color.rgb, pores * 0.18);

	ALBEDO = color;
	ROUGHNESS = clamp(0.72 - inner * 0.16 + fine * 0.07, 0.48, 0.82);
	METALLIC = 0.0;
	SPECULAR = 0.28;
	EMISSION = cell_color.rgb * inner * travelling * 0.035;
}
