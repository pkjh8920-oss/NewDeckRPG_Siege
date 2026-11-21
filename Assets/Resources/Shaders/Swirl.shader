// 소용돌이 화면 효과 (타이틀 → 게임으로 들어갈 때)
// 찍어 둔 화면 그림(_MainTex)을 가운데를 중심으로 비틀어 돌림 + 어두운 고리 + 점점 어두워짐
//   _Angle  : 비트는 정도 (라디안. 클수록 많이 돔)
//   _Radius : 비틀리는 범위 (화면 세로 기준 0~1)
//   _Ring   : 어두운 고리 진하기 (0~1)
//   _Dark   : 화면 전체 어두워지기 (0~1)
//   _Aspect : 화면 가로/세로 (원이 찌그러지지 않게)
// Resources 폴더에 있어서 빌드에도 자동으로 포함됨 (Shader.Find 로 찾음)
Shader "Hidden/DeckRPG/Swirl"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Angle ("Angle", Float) = 0
        _Radius ("Radius", Float) = 0.9
        _Ring ("Ring", Float) = 0
        _Dark ("Dark", Float) = 0
        _Aspect ("Aspect", Float) = 1.7777
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            sampler2D _MainTex;
            float _Angle, _Radius, _Ring, _Dark, _Aspect;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // 가운데에 가까울수록 많이 돌림 (바깥은 거의 안 돎)
            float2 Swirl (float2 uv, float extra)
            {
                float2 p = uv - 0.5;
                p.x *= _Aspect;
                float r = length(p);
                float t = saturate(1.0 - r / _Radius);
                float a = (_Angle + extra) * t * t;
                float s = sin(a);
                float c = cos(a);
                p = float2(p.x * c - p.y * s, p.x * s + p.y * c);
                p.x /= _Aspect;
                return p + 0.5;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 돌아가는 방향으로 4번 겹쳐 찍어서 흐릿하게 (유리처럼 번지는 느낌)
                fixed4 col = tex2D(_MainTex, Swirl(i.uv, 0));
                col += tex2D(_MainTex, Swirl(i.uv, 0.05 * _Angle));
                col += tex2D(_MainTex, Swirl(i.uv, -0.05 * _Angle));
                col += tex2D(_MainTex, Swirl(i.uv, 0.10 * _Angle));
                col *= 0.25;

                // 어두운 고리
                float2 p = i.uv - 0.5;
                p.x *= _Aspect;
                float r = length(p);
                float ring = exp(-pow((r - 0.38) / 0.12, 2));
                col.rgb *= 1.0 - _Ring * ring * 0.8;

                // 전체가 보랏빛 어둠으로
                col.rgb = lerp(col.rgb, float3(0.03, 0.01, 0.06), _Dark);
                col.a = i.color.a;
                return col;
            }
            ENDCG
        }
    }
}
