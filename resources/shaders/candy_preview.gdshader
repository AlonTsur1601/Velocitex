shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec4 primary_color : source_color = vec4(0.82, 0.12, 0.18, 1.0);
uniform vec4 secondary_color : source_color = vec4(0.91, 0.85, 0.72, 1.0);
uniform int pattern_mode = 0;

float hash21(vec2 point) {
	return fract(sin(dot(point, vec2(127.1, 311.7))) * 43758.5453);
}

float noise21(vec2 point) {
	vec2 cell = floor(point);
	vec2 local = fract(point);
	local = local * local * (3.0 - (2.0 * local));
	float bottom = mix(hash21(cell), hash21(cell + vec2(1.0, 0.0)), local.x);
	float top = mix(hash21(cell + vec2(0.0, 1.0)), hash21(cell + vec2(1.0, 1.0)), local.x);
	return mix(bottom, top, local.y);
}

float hash31(vec3 point) {
	return fract(sin(dot(point, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

float noise31(vec3 point) {
	vec3 cell = floor(point);
	vec3 local = fract(point);
	local = local * local * (3.0 - (2.0 * local));
	float x00 = mix(hash31(cell), hash31(cell + vec3(1.0, 0.0, 0.0)), local.x);
	float x10 = mix(hash31(cell + vec3(0.0, 1.0, 0.0)), hash31(cell + vec3(1.0, 1.0, 0.0)), local.x);
	float x01 = mix(hash31(cell + vec3(0.0, 0.0, 1.0)), hash31(cell + vec3(1.0, 0.0, 1.0)), local.x);
	float x11 = mix(hash31(cell + vec3(0.0, 1.0, 1.0)), hash31(cell + vec3(1.0, 1.0, 1.0)), local.x);
	return mix(mix(x00, x10, local.y), mix(x01, x11, local.y), local.z);
}

float segment_distance(vec2 point, vec2 start, vec2 end) {
	vec2 segment = end - start;
	float amount = clamp(dot(point - start, segment) / dot(segment, segment), 0.0, 1.0);
	return length(point - (start + (segment * amount)));
}

vec3 sphere_direction(vec2 uv) {
	float longitude = uv.x * 6.28318530718;
	float latitude = (uv.y - 0.5) * 3.14159265359;
	float latitude_radius = cos(latitude);
	return vec3(
		latitude_radius * cos(longitude),
		sin(latitude),
		latitude_radius * sin(longitude)
	);
}

void fragment() {
	vec2 uv = UV;
	vec3 sphere = sphere_direction(uv);
	float longitude = atan(sphere.z, sphere.x);
	float latitude = asin(clamp(sphere.y, -1.0, 1.0));
	float mask = 0.0;
	if (pattern_mode == 1) {
		mask = step(0.0, sphere.x);
	} else if (pattern_mode == 2) {
		mask = smoothstep(-0.18, 0.18, sin((longitude * 3.0) + (latitude * 8.0)));
	} else if (pattern_mode == 3) {
		mask = smoothstep(-0.12, 0.12, sin(latitude * 20.0));
	} else if (pattern_mode == 4) {
		vec2 cell = fract(uv * vec2(12.0, 7.0)) - 0.5;
		mask = 1.0 - smoothstep(0.16, 0.23, length(cell));
	} else if (pattern_mode == 5) {
		vec2 cell = fract(uv * vec2(9.0, 5.0)) - 0.5;
		float angle = atan(cell.y, cell.x);
		float star_radius = mix(0.105, 0.245, 0.5 + (0.5 * cos(angle * 5.0)));
		mask = 1.0 - smoothstep(star_radius - 0.018, star_radius + 0.018, length(cell));
	} else if (pattern_mode == 6) {
		vec2 cell = fract(uv * vec2(7.0, 4.0)) - 0.5;
		float upper_bolt = segment_distance(cell, vec2(0.10, -0.43), vec2(-0.07, -0.04));
		float lower_bolt = segment_distance(cell, vec2(0.05, -0.03), vec2(-0.12, 0.43));
		float bridge = segment_distance(cell, vec2(-0.07, -0.04), vec2(0.05, -0.03));
		float bolt = min(upper_bolt, min(lower_bolt, bridge));
		mask = 1.0 - smoothstep(0.045, 0.075, bolt);
	} else if (pattern_mode == 7) {
		float edge = 0.245 + (sin(longitude * 3.0) * 0.022) + (sin(longitude * 7.0 + 0.7) * 0.010);
		float cap = 1.0 - smoothstep(edge - 0.018, edge + 0.018, uv.y);
		float drip_phase = fract((longitude / 6.28318530718) * 5.0 + 0.5) - 0.5;
		float drip_index = floor((longitude / 6.28318530718) * 5.0 + 0.5);
		float drip_length = 0.09 + (0.075 * (0.5 + (0.5 * sin(drip_index * 2.41))));
		float drip_width = 0.10 + (0.025 * (0.5 + (0.5 * sin(drip_index * 1.73))));
		float drip_body = 1.0 - smoothstep(drip_width * 0.65, drip_width, abs(drip_phase));
		float drip_vertical = smoothstep(edge - 0.01, edge + 0.025, uv.y) * (1.0 - smoothstep(edge + drip_length - 0.025, edge + drip_length + 0.018, uv.y));
		float rounded_tip = 1.0 - smoothstep(drip_width * 0.75, drip_width * 1.12, length(vec2(drip_phase, uv.y - (edge + drip_length - 0.012))));
		mask = max(cap, max(drip_body * drip_vertical, rounded_tip));
	} else if (pattern_mode == 8) {
		mask = smoothstep(-0.14, 0.14, sin((uv.x + sin(uv.y * 12.5663) * 0.08) * 37.6991));
	} else if (pattern_mode == 9) {
		float rope_wave = sin((longitude * 3.0) + (sin(latitude * 4.0) * 0.52));
		float rope = 1.0 - smoothstep(0.16, 0.36, abs(rope_wave));
		float twisted_ridge = 0.5 + (0.5 * sin((latitude * 19.0) - (longitude * 5.0)));
		mask = clamp((rope * 0.68) + (rope * twisted_ridge * 0.32), 0.0, 1.0);
	} else if (pattern_mode == 10) {
		float target = acos(clamp(dot(sphere, vec3(0.0, 0.0, 1.0)), -1.0, 1.0));
		mask = smoothstep(-0.1, 0.1, sin(target * 15.0));
	} else if (pattern_mode == 11) {
		vec2 crack_uv = vec2(longitude / 3.14159265359, latitude / 1.57079632679);
		crack_uv.x = (fract((crack_uv.x + 1.0) * 3.0) - 0.5) * 2.0;
		float spine = segment_distance(crack_uv, vec2(-0.18, -0.96), vec2(0.08, -0.46));
		spine = min(spine, segment_distance(crack_uv, vec2(0.08, -0.46), vec2(-0.14, 0.04)));
		spine = min(spine, segment_distance(crack_uv, vec2(-0.14, 0.04), vec2(0.12, 0.48)));
		spine = min(spine, segment_distance(crack_uv, vec2(0.12, 0.48), vec2(-0.04, 0.96)));
		float branches = segment_distance(crack_uv, vec2(0.04, -0.42), vec2(0.52, -0.70));
		branches = min(branches, segment_distance(crack_uv, vec2(-0.12, 0.02), vec2(-0.62, 0.34)));
		branches = min(branches, segment_distance(crack_uv, vec2(0.10, 0.46), vec2(0.56, 0.70)));
		float crack_line = min(spine * 0.82, branches);
		mask = 1.0 - smoothstep(0.035, 0.075, crack_line);
	} else if (pattern_mode == 12) {
		float pearl = 0.5 + (0.5 * sin(dot(sphere, vec3(7.0, 9.0, 11.0))));
		mask = pearl * 0.38;
	}

	float grain = noise31((sphere * 82.0) + vec3(11.0, 29.0, 7.0));
	float coarse_grain = noise31((sphere * 13.0) + vec3(17.0, 9.0, 23.0));
	float macro_grain = noise31((sphere * 3.2) + vec3(31.0, 5.0, 19.0));
	float crust_noise = noise31((sphere * 8.5) + vec3(3.0, 27.0, 14.0));
	float sugar_crust = smoothstep(0.61, 0.82, crust_noise + ((macro_grain - 0.5) * 0.22));
	float crystal = smoothstep(0.86, 0.97, noise31((sphere * 39.0) + vec3(19.0, 2.0, 31.0)));
	float pore = smoothstep(0.88, 0.975, noise31((sphere * 32.0) + vec3(7.0, 13.0, 23.0)));
	float glaze_wave = 0.5 + (0.5 * sin(dot(sphere, vec3(9.0, 13.0, 17.0)) + ((coarse_grain - 0.5) * 1.8)));
	vec3 candy = mix(primary_color.rgb, secondary_color.rgb, clamp(mask, 0.0, 1.0));
	vec3 textured_candy = candy * (0.78 + (macro_grain * 0.34) + ((coarse_grain - 0.5) * 0.14) + ((grain - 0.5) * 0.055) + (glaze_wave * 0.025));
	vec3 crust_color = mix(candy, secondary_color.rgb, 0.32) * 1.08;
	textured_candy = mix(textured_candy, crust_color, sugar_crust * 0.28);
	textured_candy += vec3(crystal * 0.1);
	textured_candy *= 1.0 - (pore * 0.12);
	ALBEDO = textured_candy;
	METALLIC = pattern_mode == 12 ? 0.16 : 0.02;
	ROUGHNESS = pattern_mode == 12
		? 0.34 + (grain * 0.08)
		: 0.41 + (grain * 0.08) + (pore * 0.11) + (sugar_crust * 0.08) - (crystal * 0.035);
	SPECULAR = 0.24;
}
