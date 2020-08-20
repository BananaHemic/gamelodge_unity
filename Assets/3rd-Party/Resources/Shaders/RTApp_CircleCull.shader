Shader "RTUnityApp/CircleCull"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_CircleCenter("Circle Center", Vector) = (0,0,0,0)
		_CullAlphaScale("Cull Alpha Scale", float) = 0.1
		_CamLook("Cam Look", Vector) = (0,0,1,0)	
		_OrthoCam("Ortho camera", int) = 0
		_ZTest("ZTest", int) = 4
		_ZWrite("ZWrite", int) = 1
	}

	SubShader
	{
		Tags{ "Queue" = "Transparent" }
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			ZTest[_ZTest]
			ZWrite[_ZWrite]

			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma vertex vert
			#pragma fragment frag

			float4 _Color;
			float4 _CircleCenter;
			float _CullAlphaScale;
			float3 _CamLook;
			int _OrthoCam;

			struct vInput
			{
				float4 vertexPos : POSITION;
			};

			struct vOutput
			{
				float4 clipPos : SV_POSITION;
				float3 worldPosition : TEXCOORD0;
			};

			vOutput vert(vInput input)
			{
				vOutput o;
				o.clipPos = UnityObjectToClipPos(input.vertexPos);
				o.worldPosition = mul(unity_ObjectToWorld, input.vertexPos);

				return o;
			}

			float4 frag(vOutput input) : COLOR
			{
				float3 worldNormal = normalize(input.worldPosition - _CircleCenter);
				float3 dirPersp = normalize(input.worldPosition - _WorldSpaceCameraPos);
				float3 dirOrtho = _CamLook;
				float3 dir = normalize(lerp(dirPersp, dirOrtho, _OrthoCam));
				if (dot(dir, worldNormal) >= 0.0f) return float4(_Color.rgb, _Color.a * _CullAlphaScale);

				return _Color;
			}
			ENDCG
		}
	}
}
