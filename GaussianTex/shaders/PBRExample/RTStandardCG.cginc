
uniform float4 _Color;
uniform Texture2D _MainTex;
uniform sampler sampler_MainTex;
uniform Texture2DArray<float4> _MainTex_LUT;
uniform float4 _MainTex_LUT_TexelSize;
uniform float4 _MainTex_ST;
uniform Texture2D _MetallicGlossMap;
uniform Texture2DArray<float4> _MetGloss_LUT;
uniform float4 _MetGloss_LUT_TexelSize;
uniform Texture2D _NormalMap;
uniform Texture2DArray<float4> _NormalMap_LUT;
uniform float4 _NormalMap_LUT_TexelSize;
uniform float4 _NormalMap_ST;
uniform float _TilingScale;
uniform float _Smoothness;
uniform float _Metallic;
uniform float _NormalStrength;

uniform float4 _CsCenter;
uniform float4 _CX;
uniform float4 _CY;
uniform float4 _CZ;

uniform float4 _NormCsCenter;
uniform float4 _NCX;
uniform float4 _NCY;
uniform float4 _NCZ;


#include "RTStandardCommon.cginc"
#include "../GaussianBlend.cginc"

v2f vert(vertexIn v)
{
	v2f o;

	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_OUTPUT(v2f, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	StdCommonVert(v, o);

	return o;
}

float4 frag(v2f i) : SV_TARGET
{

	//Compute random tiling offsets and weights
    float3 weights = float3(0,0,0);
    float2 uvVertex0 = 0, uvVertex1 = 0, uvVertex2 = 0;
    RandomOffsetTiling(i.uv * _TilingScale, weights, uvVertex0, uvVertex1, uvVertex2);

    //Compute the screenspace derivatives on the original unaltered uvs for mip-mapping 
    float2 constDx = ddx(i.uv);
    float2 constDy = ddy(i.uv);
	float mip = CalcMipLevel(_MainTex, sampler_MainTex, i.uv);

	colorspace cs;
	cs.axis0 = _CX;
    cs.axis1 = _CY;
    cs.axis2 = _CZ;
    cs.center = _CsCenter;

	float3 mainGauss1 = _MainTex.SampleGrad(sampler_MainTex, i.uv + uvVertex0, constDx, constDy).rgb;
	float3 mainGauss2 = _MainTex.SampleGrad(sampler_MainTex, i.uv + uvVertex1, constDx, constDy).rgb;
	float3 mainGauss3 = _MainTex.SampleGrad(sampler_MainTex, i.uv + uvVertex2, constDx, constDy).rgb;

    float3 mainGaussTotal = Blend3GaussianRGB(mainGauss1, mainGauss2, mainGauss3, weights, cs);

    float4 mainColor = LookUpTableRGB(_MainTex_LUT, _MainTex_LUT_TexelSize.zw, mainGaussTotal, mip);
    mainColor.rgb = ConvertColorspaceToRGB(mainColor.rgb, cs);

	mainColor *= _Color;

	//float4 metallicGlossMap = tex2D(_MetallicGlossMap, i.uv);
	float2 mGlossGauss1 = _MetallicGlossMap.SampleGrad(sampler_MainTex, i.uv + uvVertex0, constDx, constDy).ra;
	float2 mGlossGauss2 = _MetallicGlossMap.SampleGrad(sampler_MainTex, i.uv + uvVertex1, constDx, constDy).ra;
	float2 mGlossGauss3 = _MetallicGlossMap.SampleGrad(sampler_MainTex, i.uv + uvVertex2, constDx, constDy).ra;

	float2 mGlossTotal = Blend3GaussianRA(mGlossGauss1, mGlossGauss2, mGlossGauss3, weights);
	float4 mGlossColor = LookUpTableRA(_MetGloss_LUT, _MetGloss_LUT_TexelSize.zw, mGlossTotal, mip);


	float4 normGauss1 = _NormalMap.SampleGrad(sampler_MainTex, i.uv + uvVertex0, constDx, constDy);
	float4 normGauss2 = _NormalMap.SampleGrad(sampler_MainTex, i.uv + uvVertex1, constDx, constDy);
	float4 normGauss3 = _NormalMap.SampleGrad(sampler_MainTex, i.uv + uvVertex2, constDx, constDy);
	
	
	float4 normTotal = Blend3GaussianRGBANoCs(normGauss1, normGauss2, normGauss3, weights);
	float4 normColor = LookUpTableRGBA(_NormalMap_LUT, _NormalMap_LUT_TexelSize.zw, normTotal, mip);
	

	//float4 normColor = normGauss1 * weights.x + normGauss2 * weights.y + normGauss2 * weights.z / (weights.x + weights.y + weights.z);
	//float3 tNormal = normalize(2*normColor - 1 );
	float3 tNormal = saturate(UnpackNormal(normColor)) + float3(0,0,0.0001);
	tNormal.xy *= _NormalStrength;
	tNormal = normalize(tNormal);

	float3x3 TangentToWorld = float3x3(i.tangent.x, i.bitangent.x, i.normal.x,
									   i.tangent.y, i.bitangent.y, i.normal.y,
									   i.tangent.z, i.bitangent.z, i.normal.z);
	
	float3 normal = normalize(mul(TangentToWorld, tNormal));

	//clip(texCol.a - _Cutoff);

	
	float smoothness = mGlossColor.a * _Smoothness;

	float metallic = mGlossColor.r * _Metallic;
	
	float4 color = StdCommonFrag(i, mainColor, normal, smoothness, metallic);
	return color;
}

