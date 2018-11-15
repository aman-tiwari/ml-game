// Standard shader with triplanar mapping
// https://github.com/keijiro/StandardTriplanar

Shader "Standard Triplanar (3 Textures)"
{
	Properties
	{
		_Color("", Color) = (1, 1, 1, 1)


		_Offset("", Vector) = (0, 0, 0, 0)
		_Scale("", Vector) = (0, 0, 0, 0)
		_ForwardTex("", 2D) = "white" {}
		_ForwardFov("", Vector) = (0, 0, 0, 0)
		_ForwardPos("", Vector) = (0, 0, 0, 0)

		_RightTex("", 2D) = "white" {}
		_RightFov("", Vector) = (0, 0, 0, 0)
		_RightPos("", Vector) = (0, 0, 0, 0)

		_UpTex("", 2D) = "white" {}
		_UpFov("", Vector) = (0, 0, 0, 0)
		_UpPos("", Vector) = (0, 0, 0, 0)

		_Extents("", Vector) = (0, 0, 0, 0)
		_Glossiness("", Range(0, 1)) = 0.5
		[Gamma] _Metallic("", Range(0, 1)) = 0

		_BumpScale("", Float) = 1
		_BumpMap("", 2D) = "bump" {}

		_OcclusionStrength("", Range(0, 1)) = 1
		_OcclusionMap("", 2D) = "white" {}

		_MapScale("", Float) = 1
	}
		SubShader
		{
			Tags { "RenderType" = "Opaque" }

			CGPROGRAM

			#pragma surface surf Standard vertex:vert fullforwardshadows addshadow

			#pragma shader_feature _NORMALMAP
			#pragma shader_feature _OCCLUSIONMAP

			#pragma target 3.0

			half4x4 _ParentT;

			half4 _Scale;
			half4 _Offset;
			half4 _Extents;
			half4 _Color;
			sampler2D _ForwardTex;
			half4 _ForwardFov;
			half4 _ForwardPos;

			sampler2D _RightTex;
			half4 _RightFov;
			half4 _RightPos;

			sampler2D _UpTex;
			half4 _UpFov;
			half4 _UpPos;

			half _Glossiness;
			half _Metallic;

			half _BumpScale;
			sampler2D _BumpMap;

			half _OcclusionStrength;
			sampler2D _OcclusionMap;

			half _MapScale;

			struct Input
			{
				float3 localCoord;
				float3 localNormal;
			};

#define PI (3.14159)
#define TO_RAD(X) ((X) * 0.0174533)

			void vert(inout appdata_full v, out Input data)
			{
				UNITY_INITIALIZE_OUTPUT(Input, data);
				data.localCoord =  v.vertex.xyz;
				//v.vertex.xyz;
				data.localNormal = mul((float3x3)_ParentT, v.normal.xyz);
			}

			void surf(Input IN, inout SurfaceOutputStandard o)
			{

				// Blending factor of triplanar mapping
				float3 bf = normalize(abs(IN.localNormal));
				bf *= bf; // sharpen blending edges
				//bf *= bf;
				bf *= bf;
				// enforce x + y + z == 1
				bf /= dot(bf, (float3)1);

				float3 xyz = (IN.localCoord + _Offset.xyz) * _Scale;

				// Triplanar mapping
				// forward, right, up
				float distF = length(xyz - _ForwardPos.xyz);
				float distR = length(xyz - _RightPos.xyz);
				float distU = length(xyz - _UpPos.xyz);

				float2 planeF = 2.0 * distF / (tan(TO_RAD(90 - _ForwardFov.xy * 0.5)));
				float2 planeR = 2.0 * distR / (tan(TO_RAD(90 - _RightFov.xy * 0.5)));
				float2 planeU = 2.0 * distU / (tan(TO_RAD(90 - _UpFov.xy * 0.5)));

				float2 tx = (xyz.zy / planeF) + 0.5;
				float2 ty = (xyz.zx / planeR) + 0.5;
				float2 tz = (xyz.xy / planeU) + 0.5;
				
				// Base color
				half2 v = half2(1.0, 0.0);
				/* half4 cx = v.xyyx * bf.x;
				half4 cy = v.yxyx * bf.y;
				half4 cz = v.yyxx * bf.z; */
				half4 cx = tex2D(_ForwardTex, tx) * bf.x;
				half4 cy = tex2D(_RightTex, ty) * bf.y;
				half4 cz = tex2D(_UpTex, tz) * bf.z;
				half4 color = (cx + cy + cz) * _Color;
				o.Albedo = color.rgb;
				o.Alpha = color.a;

			#ifdef _NORMALMAP
				// Normal map
				half4 nx = tex2D(_BumpMap, tx) * bf.x;
				half4 ny = tex2D(_BumpMap, ty) * bf.y;
				half4 nz = tex2D(_BumpMap, tz) * bf.z;
				o.Normal = UnpackScaleNormal(nx + ny + nz, _BumpScale);
			#endif

			#ifdef _OCCLUSIONMAP
				// Occlusion map
				half ox = tex2D(_OcclusionMap, tx).g * bf.x;
				half oy = tex2D(_OcclusionMap, ty).g * bf.y;
				half oz = tex2D(_OcclusionMap, tz).g * bf.z;
				o.Occlusion = lerp((half4)1, ox + oy + oz, _OcclusionStrength);
			#endif

				// Misc parameters
				o.Metallic = _Metallic;
				o.Smoothness = _Glossiness;
			}
			ENDCG
		}
			FallBack "Diffuse"
			CustomEditor "StandardTriplanarInspector"
}