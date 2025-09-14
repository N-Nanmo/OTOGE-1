Shader "Custom/URP/LaneHighlight"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.15,0.15,0.15,1)
        _HighlightColor ("Highlight Color", Color) = (1,1,1,1)
        _EmissionStrength ("Emission Strength", Float) = 2
        _LaneCount ("Lane Count", Int) = 14
        _AngleOffset ("Angle Offset (0-1)", Range(0,1)) = 0      // ���[�����E��]���� (���=1)
        _EdgeFeather ("Edge Feather (0-0.5)", Range(0,0.2)) = 0.0 // ���[�����E�\�t�g��
        _InvertDirection ("Invert Direction (0/1)", Float) = 1
        _LaneShift ("Lane Shift (lanes)", Float) = 0.5 // 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Unlit" }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #ifndef PI
            #define PI 3.14159265359
            #endif
            #define TWO_PI 6.28318530718

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _HighlightColor;
                float _EmissionStrength;
                int   _LaneCount;
                float _AngleOffset;   // 0..1
                float _EdgeFeather;   // 0..0.5 fraction of lane width
                float _InvertDirection; // 0 or 1
                float _LaneShift;       // lane units (ex -0.5)
                float _LaneHighlight[32];
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 posOS      : TEXCOORD0; // object space
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.posOS = v.positionOS.xyz;
                return o;
            }

            // �p�x(�I�u�W�F�N�g���) -> 0..1
            float Angle01(float2 p)
            {
                float ang = atan2(p.x, p.y); // y=Z, x=X  => -PI..PI (Z��O���)
                // ���K�� 0..1
                ang = (ang + PI) / TWO_PI; // 0..1
                // �I�t�Z�b�g�K�p
                ang = frac(ang + _AngleOffset);
                return ang;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Object space XZ ���ʂ��g�p (�X�P�[���ψ�O��)
                float2 p = float2(i.posOS.x, i.posOS.z);
                float ang01 = Angle01(p);
                if(_InvertDirection>0.5) ang01=1.0-ang01; // �������]
                // ���[����ւ̃V�t�g (���[�����Ŋ����Đ��K��)
                float shift = _LaneShift / max(1.0,_LaneCount);
                ang01 = frac(ang01 + shift);
                float laneFloatAll = ang01 * _LaneCount; // 0..LaneCount
                float laneIndexF = floor(laneFloatAll);
                int lane = (int)clamp(laneIndexF,0,_LaneCount-1);

                // �G�b�W�t�F�U�[�v�Z (�I�v�V����)
                float localU = frac(laneFloatAll); // �����[���� 0..1
                float feather=_EdgeFeather;
                float edgeMask=1.0;
                if(feather>0.0001)
                {
                    float left=smoothstep(0.0,feather,localU);               // �[0��feather
                    float right=smoothstep(0.0,feather,1.0-localU);        // �[1��1-feather ���]
                    edgeMask=min(left,right); // ���[�ŗ��Ƃ�
                }                
                float h=_LaneHighlight[lane]*edgeMask;
                float3 col = _BaseColor.rgb + _HighlightColor.rgb * h * _EmissionStrength;
                return half4(col,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
