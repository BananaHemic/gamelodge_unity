Shader "RTUnityApp/GizmoSolidHandle" 
{
	Properties
	{
		_Color ("Color", Color) = (1, 1, 1, 1)					
		_IsLit ("Is lit", int) = 1								
		_LightDir ("Light direction", Vector) = (1, 1, 1, 0)	
		_LightIntensity ("Light intensity", float) = 1.5		
		_CullMode ("Cull mode", int) = 2						
		_ZTest ("ZTest", int) = 4								
		_ZWrite("ZWrite", int) = 1								
	}

	Subshader
	{
		Tags { "Queue" = "Transparent" }
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha		
			ZTest [_ZTest]
			ZWrite [_ZWrite]
			Cull [_CullMode]

			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma fragment frag
			#pragma vertex vert

			float4 _Color;
			int _IsLit;
			float4 _LightDir;
			float _LightIntensity;

			struct vInput
			{
				float4 vertexPos : POSITION;
				float3 vertexNormal : NORMAL;
			};

			struct vOutput
			{
				float4 clipPos : SV_POSITION;
				float3 worldNormal : TEXCOORD1;
			};

			vOutput vert(vInput input)
			{
				vOutput o;
				o.clipPos = UnityObjectToClipPos(input.vertexPos);
				o.worldNormal = mul(float4(input.vertexNormal, 0.0f), unity_WorldToObject);

				return o;
			}

			float4 frag(vOutput o) : COLOR
			{
				if(_IsLit == 0) return _Color;
				else
				{
					o.worldNormal = normalize(o.worldNormal);

					float minInfluence = 0.35f;
					float lightInfluence = saturate(dot(-_LightDir.xyz, o.worldNormal));
					lightInfluence = saturate(lightInfluence + minInfluence * (1.0f - lightInfluence));

					float rgbScaleValue = lightInfluence * _LightIntensity;
					return _Color * float4(rgbScaleValue, rgbScaleValue, rgbScaleValue, 1.0f);
				}
			}
			ENDCG
		}
	}
}
