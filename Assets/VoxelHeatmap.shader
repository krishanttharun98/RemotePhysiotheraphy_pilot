Shader "Custom/VoxelHeatmap"
{
    Properties
    {
        _VoxelScale ("Voxel Scale", Float) = 0.05
        _MaxAlpha ("Maximum Alpha Ceiling", Range(0.1, 1.0)) = 0.9
        _MaxHeat ("Heat value mapped to full hot", Float) = 6.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            StructuredBuffer<float3> _VoxelPositions;
            StructuredBuffer<float2> _VoxelInteractions; // x = accumulated heat, y = reserved

            float _VoxelScale;
            int _IsVisualizationActive;
            float _MaxAlpha;
            float _MaxHeat;
            float4x4 _LocalToWorldMatrix;

            // Cold -> hot colour ramp: blue -> cyan -> green -> yellow -> red.
            float3 HeatColor(float t)
            {
                t = saturate(t);
                float3 c;
                if (t < 0.25)       c = lerp(float3(0,0,1), float3(0,1,1), t / 0.25);
                else if (t < 0.5)   c = lerp(float3(0,1,1), float3(0,1,0), (t - 0.25) / 0.25);
                else if (t < 0.75)  c = lerp(float3(0,1,0), float3(1,1,0), (t - 0.5) / 0.25);
                else                c = lerp(float3(1,1,0), float3(1,0,0), (t - 0.75) / 0.25);
                return c;
            }

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;

                float3 localCenterPos = _VoxelPositions[instanceID];
                float heat = _VoxelInteractions[instanceID].x;

                float3 localVertex = v.vertex.xyz * _VoxelScale;
                float3 shiftedLocalPos = localVertex + localCenterPos;

                float4 worldPos = mul(_LocalToWorldMatrix, float4(shiftedLocalPos, 1.0));
                o.vertex = UnityObjectToClipPos(worldPos);

                if (_IsVisualizationActive == 3 && heat > 0.0)
                {
                    float maxHeat = max(_MaxHeat, 0.0001);
                    float t = heat / maxHeat;
                    float3 col = HeatColor(t);
                    // Fade in alpha with low heat so first touches are faint.
                    float a = lerp(0.25, _MaxAlpha, saturate(t));
                    o.color = float4(col, a);
                }
                else
                {
                    o.color = float4(0, 0, 0, 0);
                }

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (i.color.a == 0) discard;
                return i.color;
            }
            ENDCG
        }
    }
}
