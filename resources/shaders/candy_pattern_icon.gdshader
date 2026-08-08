shader_type canvas_item;

uniform vec4 primary_color : source_color;
uniform vec4 secondary_color : source_color;
uniform int pattern_mode = 0;

float segment_distance(vec2 p, vec2 a, vec2 b) { vec2 s=b-a; return length(p-(a+s*clamp(dot(p-a,s)/dot(s,s),0.0,1.0))); }
float hash31(vec3 p){return fract(sin(dot(p,vec3(127.1,311.7,74.7)))*43758.5453);}
float noise31(vec3 p){vec3 c=floor(p);vec3 l=fract(p);l=l*l*(3.0-2.0*l);float x00=mix(hash31(c),hash31(c+vec3(1,0,0)),l.x);float x10=mix(hash31(c+vec3(0,1,0)),hash31(c+vec3(1,1,0)),l.x);float x01=mix(hash31(c+vec3(0,0,1)),hash31(c+vec3(1,0,1)),l.x);float x11=mix(hash31(c+vec3(0,1,1)),hash31(c+vec3(1,1,1)),l.x);return mix(mix(x00,x10,l.y),mix(x01,x11,l.y),l.z);}

void fragment() {
    vec2 circle_uv=(UV-vec2(0.5))/0.72;
    vec2 projected=circle_uv*2.0;
    float sphere_z=sqrt(max(0.0,1.0-dot(projected,projected)));
    vec3 sphere=normalize(vec3(projected.x,-projected.y,sphere_z));
    float longitude=atan(sphere.z,sphere.x);
    float latitude=asin(clamp(sphere.y,-1.0,1.0));
    vec2 uv=vec2(fract(longitude/6.28318530718),latitude/3.14159265359+0.5);
    float mask=0.0;
    if(pattern_mode==1) mask=step(0.0,sphere.x);
    else if(pattern_mode==2) mask=smoothstep(-0.18,0.18,sin(longitude*3.0+latitude*8.0));
    else if(pattern_mode==3) mask=smoothstep(-0.12,0.12,sin(latitude*20.0));
    else if(pattern_mode==4){vec2 cell=fract(uv*vec2(12.0,7.0))-0.5;mask=1.0-smoothstep(0.16,0.23,length(cell));}
    else if(pattern_mode==5){vec2 cell=fract(uv*vec2(9.0,5.0))-0.5;float a=atan(cell.y,cell.x);float r=mix(0.105,0.245,0.5+0.5*cos(a*5.0));mask=1.0-smoothstep(r-0.018,r+0.018,length(cell));}
    else if(pattern_mode==6){vec2 cell=fract(uv*vec2(7.0,4.0))-0.5;float u=segment_distance(cell,vec2(0.10,-0.43),vec2(-0.07,-0.04));float l=segment_distance(cell,vec2(0.05,-0.03),vec2(-0.12,0.43));float b=segment_distance(cell,vec2(-0.07,-0.04),vec2(0.05,-0.03));mask=1.0-smoothstep(0.045,0.075,min(u,min(l,b)));}
    else if(pattern_mode==7){float edge=0.245+sin(longitude*3.0)*0.022+sin(longitude*7.0+0.7)*0.010;float cap=1.0-smoothstep(edge-0.018,edge+0.018,uv.y);float phase=fract((longitude/6.28318530718)*5.0+0.5)-0.5;float i=floor((longitude/6.28318530718)*5.0+0.5);float len=0.09+0.075*(0.5+0.5*sin(i*2.41));float w=0.10+0.025*(0.5+0.5*sin(i*1.73));float body=1.0-smoothstep(w*0.65,w,abs(phase));float vertical=smoothstep(edge-0.01,edge+0.025,uv.y)*(1.0-smoothstep(edge+len-0.025,edge+len+0.018,uv.y));float tip=1.0-smoothstep(w*0.75,w*1.12,length(vec2(phase,uv.y-(edge+len-0.012))));mask=max(cap,max(body*vertical,tip));}
    else if(pattern_mode==8) mask=smoothstep(-0.14,0.14,sin((uv.x+sin(uv.y*12.5663)*0.08)*37.6991));
    else if(pattern_mode==9){float wave=sin(longitude*3.0+sin(latitude*4.0)*0.52);float rope=1.0-smoothstep(0.16,0.36,abs(wave));float ridge=0.5+0.5*sin(latitude*19.0-longitude*5.0);mask=clamp(rope*0.68+rope*ridge*0.32,0.0,1.0);}
    else if(pattern_mode==10){float target=acos(clamp(dot(sphere,vec3(0.0,0.0,1.0)),-1.0,1.0));mask=smoothstep(-0.1,0.1,sin(target*15.0));}
    else if(pattern_mode==11){vec2 c=vec2(longitude/3.14159265359,latitude/1.57079632679);c.x=(fract((c.x+1.0)*3.0)-0.5)*2.0;float spine=segment_distance(c,vec2(-0.18,-0.96),vec2(0.08,-0.46));spine=min(spine,segment_distance(c,vec2(0.08,-0.46),vec2(-0.14,0.04)));spine=min(spine,segment_distance(c,vec2(-0.14,0.04),vec2(0.12,0.48)));spine=min(spine,segment_distance(c,vec2(0.12,0.48),vec2(-0.04,0.96)));float branches=segment_distance(c,vec2(0.04,-0.42),vec2(0.52,-0.70));branches=min(branches,segment_distance(c,vec2(-0.12,0.02),vec2(-0.62,0.34)));branches=min(branches,segment_distance(c,vec2(0.10,0.46),vec2(0.56,0.70)));mask=1.0-smoothstep(0.035,0.075,min(spine*0.82,branches));}
    else if(pattern_mode==12) mask=(0.5+0.5*sin(dot(sphere,vec3(7.0,9.0,11.0))))*0.38;
    else if(pattern_mode==13){vec2 grid=abs(fract(uv*vec2(10.0,6.0))-0.5);float lines=1.0-smoothstep(0.035,0.065,min(grid.x,grid.y));float checks=step(0.0,sin(longitude*10.0)*sin(latitude*6.0));mask=max(lines,checks*0.28);}
    // Keep the exact front-facing pattern projection used by the current
    // icons, but render it as clean, flat two-color artwork: no candy grain,
    // pores, lighting, rim shading, or three-dimensional highlight.
    vec3 flat_color=mix(primary_color.rgb,secondary_color.rgb,step(0.5,clamp(mask,0.0,1.0)));
    COLOR=vec4(flat_color,1.0);
    COLOR.a*=1.0-step(0.5,length(circle_uv));
}
