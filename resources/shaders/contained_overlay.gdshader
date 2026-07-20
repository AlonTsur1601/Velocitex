shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled;

uniform sampler2D overlay_texture : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform vec4 overlay_tint : source_color = vec4(1.0);
uniform float overlay_opacity : hint_range(0.0, 1.0, 0.01) = 0.72;
uniform vec2 uv_scale = vec2(1.0);
uniform float transparent_padding : hint_range(0.05, 0.25, 0.01) = 0.14;

void fragment() {
	vec2 contained_uv = mix(
		vec2(-transparent_padding),
		vec2(1.0 + transparent_padding),
		UV
	);
	vec2 inside_low = step(vec2(0.0), contained_uv);
	vec2 inside_high = step(contained_uv, vec2(1.0));
	float inside_texture = inside_low.x * inside_low.y * inside_high.x * inside_high.y;
	vec4 sampled = texture(overlay_texture, clamp(contained_uv, vec2(0.0), vec2(1.0)) * uv_scale);

	ALBEDO = sampled.rgb * overlay_tint.rgb;
	ALPHA = sampled.a * overlay_tint.a * overlay_opacity * inside_texture;
	ROUGHNESS = 0.92;
	METALLIC = 0.0;
}
