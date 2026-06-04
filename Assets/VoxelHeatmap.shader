Shader "Custom/VoxelHeatmap"
{
    Properties
    {
        _VoxelScale ("Voxel Scale", Float) = 0.05
        _MaxAlpha ("Maximum Alpha Ceiling", Range(0.1, 1.0)) = 1.0
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
            StructuredBuffer<float2> _VoxelInteractions; 

            float _VoxelScale;
            int _IsVisualizationActive; 
            float _MaxAlpha;
            float4x4 _LocalToWorldMatrix;

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                
                float3 localCenterPos = _VoxelPositions[instanceID];
                float2 handInteractions = _VoxelInteractions[instanceID]; 

                float3 localVertex = v.vertex.xyz * _VoxelScale;
                float3 shiftedLocalPos = localVertex + localCenterPos;
                
                float4 worldPos = mul(_LocalToWorldMatrix, float4(shiftedLocalPos, 1.0));
                o.vertex = UnityObjectToClipPos(worldPos);

                // --- PROCEDURAL REPLICA HEADSET CYLINDER GENERATION ---
                // Checks if the voxel sits inside a 10cm radius column around the center, from chest to head height
                float lateralDistFromCenter = length(localCenterPos.xz);
                bool isHeadsetCylinder = (lateralDistFromCenter < 0.10) && (localCenterPos.y > -0.4 && localCenterPos.y < 0.2);

                if (_IsVisualizationActive == 3 && isHeadsetCylinder)
                {
                    o.color = float4(0.8, 0.8, 0.8, 0.7); // Render a distinctive solid grey head-post cylinder
                }
                // --- STATE 2: MANUAL PLACEMENT PREVIEW MODE ---
                else if (_IsVisualizationActive == 2)
                {
                    if (instanceID % 450 == 0) o.color = float4(1.0, 1.0, 1.0, 0.25); 
                    else o.color = float4(0, 0, 0, 0); 
                }
                // --- STATE 3: ACTIVE MIRROR MODE ---
                else if (_IsVisualizationActive == 3)
                {
                    if (instanceID % 450 == 0 && handInteractions.x == 0 && handInteractions.y == 0)
                    {
                        o.color = float4(1.0, 1.0, 1.0, 0.15); 
                    }
                    else if (handInteractions.x > 0.5 || handInteractions.y > 0.5)
                    {
                        if (handInteractions.x > 0.5 && handInteractions.y > 0.5) o.color = float4(1.0, 1.0, 1.0, _MaxAlpha);
                        else if (handInteractions.x > 0.5) o.color = float4(0.0, 1.0, 0.0, _MaxAlpha);
                        else if (handInteractions.y > 0.5) o.color = float4(1.0, 0.0, 0.0, _MaxAlpha);
                        else o.color = float4(0, 0, 0, 0);
                    }
                    else
                    {
                        o.color = float4(0, 0, 0, 0); 
                    }
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