Shader "Error.mdl/Blending Demo"
{
    Properties
    {
        _MainTex ("Gaussian Texture", 2D) = "white" {}
        _MainTex2 ("Original Texture", 2D) = "black" {}
        _LUTTex("Lookup Table Texture", 2DArray) = "white" {}
        _CsCenter("Colorspace Center", Vector) = (0,0,0,0)
        _CX("Colorspace X", Vector) = (1, 0, 0, 1)
        _CY("Colorspace Y", Vector) = (0, 1, 0, 1)
        _CZ("Colorspace Z", Vector) = (0, 0, 1, 1)
        [Enum(Gaussian Blend, 2, Linear Blend, 1, None, 0)] _GOn ("Blend Mode", int) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma target 5.0

            #include "UnityCG.cginc"
            #include "../GaussianBlend.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            UNITY_DECLARE_TEX2D(_MainTex);
            sampler2D _MainTex2;
          
            Texture2DArray<float4> _LUTTex;
            float4 _LUTTex_TexelSize;
            float4 _MainTex_ST;
            int _GOn;
            float4 _CsCenter;
            float4 _CX;
            float4 _CY;
            float4 _CZ;
            float _Mip;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }


            fixed4 frag(v2f i) : SV_Target
            {

                float4 output = float4(0,0,0,0);

                //Compute random tiling offsets and weights
                float3 weights = float3(0,0,0);
                float2 uvVertex0 = 0, uvVertex1 = 0, uvVertex2 = 0;
                RandomOffsetTiling(i.uv * SQRT_3, weights, uvVertex0, uvVertex1, uvVertex2);

                //Compute the screenspace derivatives on the original unaltered uvs for mip-mapping 
                float2 constDx = ddx(i.uv);
                float2 constDy = ddy(i.uv);

                // Variance preserving blending on the gaussian texture inputs and 
                if (_GOn == 2) 
                {
                    //Sample the gaussian texture 3 times, one for each offset, using the macro defined in GaussianBlend.cginc that wraps tex.SampleGrad
                    float4 gaussian1 = UNITY_SAMPLE_TEX2D_GRAD(_MainTex, i.uv + uvVertex0, constDx, constDy);
                    float4 gaussian2 = UNITY_SAMPLE_TEX2D_GRAD(_MainTex, i.uv + uvVertex1, constDx, constDy);
                    float4 gaussian3 = UNITY_SAMPLE_TEX2D_GRAD(_MainTex, i.uv + uvVertex2, constDx, constDy);

                    //Fill out the colorspace structure with the colorspace information defined in the material to make it easier to pass the information to functions
                    colorspace cs;
                    cs.axis0 = _CX;
                    cs.axis1 = _CY;
                    cs.axis2 = _CZ;
                    cs.center = _CsCenter;

                    //Call
                    float3 gaussianTotal = Blend3GaussianRGB(gaussian1, gaussian2, gaussian3, weights, cs);

                    float mip = CalcMipLevel(UNITY_PASS_TEX2D(_MainTex), i.uv);
                    float4 LUT = LookUpTableRGB(_LUTTex, _LUTTex_TexelSize.zw, gaussianTotal.rgb, mip);
                    LUT.rgb = ConvertColorspaceToRGB(LUT.rgb, cs);

                    output = LUT;
                }
                else if (_GOn == 1)
                {
                    float4 reference1 = tex2Dgrad(_MainTex2, i.uv + uvVertex0, constDx, constDy);
                    float4 reference2 = tex2Dgrad(_MainTex2, i.uv + uvVertex1, constDx, constDy);
                    float4 reference3 = tex2Dgrad(_MainTex2, i.uv + uvVertex2, constDx, constDy);
                    output = reference1 * weights.x + reference2*weights.y + reference3*weights.z;
                }
                else
                {
                    output = tex2Dgrad(_MainTex2, i.uv, constDx, constDy);
                }
                return output;
                // return float4(uvVertex0 * weights.x + uvVertex1 * weights.y + uvVertex2 * weights.z, 0 , 1);
            }
            ENDCG
        }
    }
}
