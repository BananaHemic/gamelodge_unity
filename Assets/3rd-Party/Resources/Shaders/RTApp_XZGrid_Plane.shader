Shader "RTUnityApp/XZGrid_Plane"
{
	Properties
	{
		_LineColor("Line Color", Color) = (1,1,1,1)
		_CellSizeX("Cell Size X", float) = 1.0
		_CellSizeZ("Cell Size Z", float) = 1.0
		_CamFarPlaneDist ("Camera far plane", float) = 1000			
		_CamWorldPos ("Camera position", Vector) = (0,0,0,0)
		_GridOrigin ("Grid origin", Vector) = (0,0,0,0)		
		_GridRight ("Grid right", Vector) = (1,0,0,0)
		_GridLook ("grid look", Vector) = (0,0,1,0)
	}
	
	CGINCLUDE
	float CalculateCamAlphaScale(float3 viewPos, float camFarPlaneDist, float3 camWorldPos)
	{
		float farPlaneDist = camFarPlaneDist;
		farPlaneDist *= (0.15f * (1000.0f / camFarPlaneDist));
		farPlaneDist *= max(1.0f, abs(camWorldPos.y) / 10.0f);
		float distFromCamPos = abs(viewPos.z);
		return saturate(1.0f - distFromCamPos / farPlaneDist);
	}
	ENDCG

	Subshader
	{	
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off
			ZWrite Off

			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma fragment frag
			#pragma vertex vert
			#pragma target 2.5	

			float4 _LineColor;
			float _CellSizeX;
			float _CellSizeZ;
			float _CamFarPlaneDist;
			float3 _CamWorldPos;
			float3 _GridOrigin;
			float3 _GridRight;
			float3 _GridLook;
			float4x4 _TransformMatrix;

			struct vInput
			{
				float4 vertexPos : POSITION;
			};

			struct vOutput
			{
				float3 worldPos : TEXCOORD0;
				float3 viewPos : TEXCOORD1;
				float4 clipPos: SV_POSITION;
			};

			vOutput vert(vInput input)
			{
				vOutput output;

				output.clipPos = UnityObjectToClipPos(input.vertexPos);
				output.worldPos = mul(_TransformMatrix, input.vertexPos);
				output.viewPos = mul(UNITY_MATRIX_MV, input.vertexPos);

				return output;
			}

			float4 frag(vOutput input) : COLOR
			{
				float4 worldPos = float4(input.worldPos.x, input.worldPos.y, input.worldPos.z, 0.0f);	
				float3 modelPos = worldPos - _GridOrigin;
				modelPos.x = dot(modelPos, _GridRight);
				modelPos.z = dot(modelPos, _GridLook);

				float2 xzCoords = modelPos.xz * float2(1.0f / _CellSizeX, 1.0f / _CellSizeZ);
				 
				float2 grid = abs(frac(xzCoords - 0.5) - 0.5) / fwidth(xzCoords);	
				float a = min(grid.x, grid.y);

				float4 lineColor = _LineColor;
				return float4(lineColor.r, lineColor.g, lineColor.b, CalculateCamAlphaScale(input.viewPos, _CamFarPlaneDist, _CamWorldPos) * lineColor.a * (1.0 - min(a, 1.0)));
			}
			ENDCG
		}
	}
}