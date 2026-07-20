shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec4 base_color : source_color = vec4(0.035, 0.14, 0.17, 1.0);
uniform vec4 groove_color : source_color = vec4(0.015, 0.035, 0.04, 1.0);
uniform vec4 pulse_color : source_color = vec4(0.16, 0.72, 0.78, 1.0);
uniform float motion_scale : hint_range(0.0, 1.0, 0.05) = 1.0;
uniform float surface_u_span : hint_range(0.125, 8.0, 0.001) = 1.0;

const float TWO_PI = 6.28318530718;

float band(float value, float width) {
	return 1.0 - smoothstep(width, width + 0.018, abs(value));
}

void fragment() {
	vec2 fine_uv = UV * vec2(91.0, 83.0);
	float grain = (sin(fine_uv.x * TWO_PI) * sin(fine_uv.y * TWO_PI)) * 0.5 + 0.5;
	float lane_grooves = band(sin(UV.x * 14.0 * TWO_PI), 0.34);

	float moving_v = UV.y * 5.0 + (TIME * 0.72 * motion_scale);
	// surface_u_span is the actual top-face UV width supplied by RoomGeometry.
	// Normalizing by it keeps one complete column centered on every belt width.
	// The point faces decreasing UV.y, which is local forward (-Z) on box tops.
	vec2 cell = vec2((UV.x / max(surface_u_span, 0.001)) - 0.5, fract(moving_v) - 0.5);
	// A filled arrow keeps its point inside the repeated cell. The old outlined
	// chevron was cut by a hard lower bound, leaving every blue tip visibly flat.
	float head_width = clamp((cell.y + 0.43) * 0.92, 0.0, 0.34);
	float arrow_head = (1.0 - smoothstep(head_width - 0.018, head_width + 0.018, abs(cell.x)))
		* smoothstep(-0.45, -0.40, cell.y)
		* (1.0 - smoothstep(-0.07, -0.025, cell.y));
	float arrow_shaft = (1.0 - smoothstep(0.075, 0.098, abs(cell.x)))
		* smoothstep(-0.075, -0.035, cell.y)
		* (1.0 - smoothstep(0.31, 0.35, cell.y));
	float chevron = max(arrow_head, arrow_shaft);
	float travelling_band = pow(0.5 + (0.5 * sin(moving_v * TWO_PI)), 10.0);

	vec3 color = mix(groove_color.rgb, base_color.rgb, 0.78 + (grain * 0.12));
	color = mix(color, groove_color.rgb, lane_grooves * 0.28);
	color = mix(color, pulse_color.rgb, max(chevron * 0.72, travelling_band * 0.16));

	ALBEDO = color;
	ROUGHNESS = clamp(0.66 - (chevron * 0.16) + (grain * 0.08), 0.42, 0.78);
	METALLIC = 0.24;
	SPECULAR = 0.3;
	EMISSION = pulse_color.rgb * chevron * 0.09;
}
