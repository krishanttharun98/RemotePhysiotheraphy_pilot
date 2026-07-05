Shader "Custom/HemisphereHeatmapOverlay"
{
    Properties
    {
        _MaxDarkness ("Max Darkness Alpha", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            float _MaxDarkness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.color.a *= _MaxDarkness;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (i.color.a < 0.001) discard;
                return i.color;
            }
            ENDCG
        }
    }
}
