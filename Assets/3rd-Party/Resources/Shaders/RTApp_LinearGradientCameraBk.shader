Shader "RTUnityApp/LinearGradientCameraBk" 
{
	Properties
	{
		_FirstColor ("First color", Color) = (1, 1, 1, 1)			
		_SecondColor ("Second color", Color) = (1, 1, 1, 1)			
		_FarPlaneHeight ("Far plane height", float) = 1				
		_GradientOffset ("Gradient offset", float) = 0				
	}

	Subshader
	{
		Tags {"Queue" = "Transparent" "IgnoreProjector" = "True"}
		Pass
		{
			ZWrite Off
			ZTest LEqual

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4 _FirstColor;
			float4 _SecondColor;
			float _FarPlaneHeight;
			float _GradientOffset;

			struct vInput
			{
				float4 vertexPos : POSITION;		
			};

			struct vOutput
			{
				float4 clipPos : SV_POSITION;		
				float3 viewPos : TEXCOORD0;			
			};

			vOutput vert(vInput input)
			{
				vOutput o;
				o.clipPos = UnityObjectToClipPos(input.vertexPos);
				o.viewPos = mul(UNITY_MATRIX_MV, input.vertexPos);

				return o;
			}

			float4 frag(vOutput o) : COLOR
			{
				// Calculate the pixel's Y position in view space relative to the top of the far plane.
				// The smaller the pixel's view space Y pos is, the closer 'pixelYPos' will get to the
				// value of the bottom of the far plane (full second color).
				float pixelYPos = _FarPlaneHeight * 0.5f - o.viewPos.y;
				float weight = saturate(pixelYPos / _FarPlaneHeight + _GradientOffset);

				// Interpolate the pixel color and return it
				float4 pixelColor = lerp(_FirstColor, _SecondColor, weight);
				return float4(pixelColor.rgb, 1.0f);
			}
			ENDCG
		}
	}
}
