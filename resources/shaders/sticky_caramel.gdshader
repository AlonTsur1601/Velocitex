shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec4 base_color : source_color = vec4(0.34, 0.11, 0.035, 1.0);
uniform vec4 light_caramel : source_color = vec4(0.72, 0.31, 0.075, 1.0);
uniform vec4 deep_caramel : source_color = vec4(0.12, 0.025, 0.012, 1.0);
uniform float motion_scale : hint_range(0.0, 1.0, 0.05) = 1.0;
uniform float detail_strength : hint_range(0.0, 1.0, 0.05) = 0.72;

const float TWO_PI = 6.28318530718;

float periodic_detail(vec2 uv, vec2 frequency, vec2 phase) {
	float a = sin((uv.x * frequency.x * TWO_PI) + phase.x);
	float b = sin((uv.y * frequency.y * TWO_PI) + phase.y);
	float c = sin(((uv.x * frequency.y) + (uv.y * frequency.x)) * TWO_PI + phase.x - phase.y);
	return (a + b + c) / 6.0 + 0.5;
}

void fragment() {
	float time = TIME * motion_scale;
	vec2 uv = UV;
	vec2 warp = vec2(
		sin((uv.y * 11.0 * TWO_PI) + (time * 0.31)),
		sin((uv.x * 13.0 * TWO_PI) - (time * 0.27))
	) * 0.0045;
	vec2 viscous_uv = uv + warp;

	float fine_grain = periodic_detail(viscous_uv, vec2(47.0, 53.0), vec2(0.7, 2.1));
	float pores = periodic_detail(viscous_uv, vec2(71.0, 67.0), vec2(2.8, 0.4));
	float bubble_field = periodic_detail(viscous_uv, vec2(29.0, 31.0), vec2(1.4, 2.6));
	float bubbles = smoothstep(0.83, 0.96, bubble_field + ((pores - 0.5) * 0.12));

	float strand_wave = 0.5 + (0.5 * sin(
		((viscous_uv.x * 37.0) + (viscous_uv.y * 4.0)) * TWO_PI +
		(sin(viscous_uv.y * 19.0 * TWO_PI) * 0.42) -
		(time * 0.34)
	));
	float strands = pow(strand_wave, 13.0);
	float pits = smoothstep(0.18, 0.04, pores);

	vec3 caramel = mix(deep_caramel.rgb, base_color.rgb, 0.82 + ((fine_grain - 0.5) * 0.16));
	caramel = mix(caramel, light_caramel.rgb, strands * 0.26 * detail_strength);
	caramel = mix(caramel, deep_caramel.rgb, pits * 0.22 * detail_strength);
	caramel = mix(caramel, light_caramel.rgb, bubbles * 0.14 * detail_strength);

	ALBEDO = caramel;
	ROUGHNESS = clamp(0.54 + (pores * 0.13) - (strands * 0.16) + (bubbles * 0.08), 0.34, 0.76);
	SPECULAR = 0.32;
	METALLIC = 0.0;
}
