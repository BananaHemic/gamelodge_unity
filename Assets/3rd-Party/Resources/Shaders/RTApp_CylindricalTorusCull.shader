// NOTE: Assumes the torus has a core radius and tube radii (hrz and vert) of 1 in MODEL SPACE.
// NOTE: Assumes the vertces contain the core radius direction vector stored in TEXCOORD1. 
Shader "RTUnityApp/CylindricalTorusCull"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_IsLit ("Is lit", int) = 1								
		_LightDir ("Light direction", Vector) = (1, 1, 1, 0)	
		_LightIntensity ("Light intensity", float) = 1.5	
		_TorusCenter("Torus center", Vector) = (0,0,0,0)
		_TorusCoreRadius("Torus core radius", float) = 1.0
		_TorusHrzRadius("Torus hrz radius", float) = 1.0
		_TorusVertRadius("Torus vert radius", float) = 1.0
		_CullAlphaScale("Cull alpha scale", float) = 0.1
		_CamLook("Cam Look", Vector) = (0,0,1,0)
		_OrthoCam("Ortho camera", int) = 0
		_ZTest("ZTest", int) = 4
		_ZWrite("ZWrite", int) = 1
		_CullMode ("Cull mode", int) = 2	
	}

	SubShader
	{
		Tags{ "Queue" = "Transparent" }
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZTest[_ZTest]
			ZWrite[_ZWrite]
			Cull [_CullMode]

			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma vertex vert
			#pragma fragment frag

			float4 _Color;
			int _IsLit;
			float4 _LightDir;
			float _LightIntensity;
			float4 _TorusCenter;
			float _TorusCoreRadius;
			float _TorusHrzRadius;
			float _TorusVertRadius;
			float _CullAlphaScale;
			float3 _CamLook;
			int _OrthoCam;

			struct vInput
			{
				float4 vertexPos : POSITION;
				float3 vertexNormal : NORMAL;
				float2 coreRadiusDir : TEXCOORD1;
			};

			struct vOutput
			{
				float4 clipPos : SV_POSITION;
				float3 worldPosition : TEXCOORD0;
				float3 worldNormal : TEXCOORD1;
			};

			vOutput vert(vInput input)
			{
				vOutput o;

				float3 radiusDir = normalize(float3(input.coreRadiusDir.x, 0.0f, input.coreRadiusDir.y));
				float3 vPos3 = float3(input.vertexPos.x, input.vertexPos.y, input.vertexPos.z);
				float3 sliceCenter = radiusDir;		// Assume core radius of 1 in model space
				float3 vPosDir = (vPos3 - sliceCenter);

				float3 up = float3(0.0f, 1.0f, 0.0f);
				float signUp = sign(dot(up, vPosDir));
				float signRadius = sign(dot(radiusDir, vPosDir));
		
				sliceCenter = radiusDir * _TorusCoreRadius;
				float4 finalVert = float4(sliceCenter + up * signUp * _TorusVertRadius + radiusDir * signRadius * _TorusHrzRadius, input.vertexPos.w);

				o.clipPos = UnityObjectToClipPos(finalVert);
				o.worldPosition = mul(unity_ObjectToWorld, finalVert);
				o.worldNormal = mul(float4(input.vertexNormal, 0.0f), unity_WorldToObject);

				return o;
			}

			float4 frag(vOutput o) : COLOR
			{
				float4 color = _Color;

				float3 worldNormal = normalize(o.worldPosition - _TorusCenter);
				float3 dirPersp = normalize(o.worldPosition - _WorldSpaceCameraPos);
				float3 dirOrtho = _CamLook;
				float3 dir = normalize(lerp(dirPersp, dirOrtho, _OrthoCam));
				if (dot(dir, worldNormal) >= 0.0f) color = float4(_Color.rgb, _Color.a * _CullAlphaScale);

				if(_IsLit == 0) return color;
				else
				{
					o.worldNormal = normalize(o.worldNormal);

					float minInfluence = 0.35f;
					float lightInfluence = saturate(dot(-_LightDir.xyz, o.worldNormal));
					lightInfluence = saturate(lightInfluence + minInfluence * (1.0f - lightInfluence));

					float rgbScaleValue = lightInfluence * _LightIntensity;
					return color * float4(rgbScaleValue, rgbScaleValue, rgbScaleValue, 1.0f);
				}
			}
			ENDCG
		}
	}
}
