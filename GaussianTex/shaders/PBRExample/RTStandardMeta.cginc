// Normal-map enabled Bakery-specific meta pass
#pragma multi_compile_instancing
#include"UnityStandardMeta.cginc"



// Include Bakery meta pass

#ifndef BAKERY_META
#define BAKERY_META

Texture2D bestFitNormalMap;

float _IsFlipped;

struct BakeryMetaInput
{
	float2 uv0 : TEXCOORD0;
	float2 uv1 : TEXCOORD1;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f_bakeryMeta
{
	float4 pos      : SV_POSITION;
	float4 uv       : TEXCOORD0;
	float3 normal   : TEXCOORD1;
	float3 tangent  : TEXCOORD2;
	float3 binormal : TEXCOORD3;
	#ifdef EDITOR_VISUALIZATION
    float2 vizUV        : TEXCOORD4;
    float4 lightCoord   : TEXCOORD5;
	#endif
};

v2f_bakeryMeta vert_bakerymt(BakeryMetaInput v)
{
	v2f_bakeryMeta o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_OUTPUT(v2f_bakeryMeta, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	o.pos = float4(((v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw) * 2 - 1) * float2(1,-1), 0.5, 1);
	o.uv = v.uv0.xyxy;
	o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal).xyz);
	o.tangent = normalize(mul((float3x3)unity_ObjectToWorld, v.tangent.xyz).xyz);
	o.binormal = cross(o.normal, o.tangent) * v.tangent.w * _IsFlipped;
	
	#ifdef EDITOR_VISUALIZATION
    o.vizUV = 0;
    o.lightCoord = 0;
    if (unity_VisualizationMode == EDITORVIZ_TEXTURE)
        o.vizUV = UnityMetaVizUV(unity_EditorViz_UVIndex, v.uv0.xy, v.uv1.xy, v.uv2.xy, unity_EditorViz_Texture_ST);
    else if (unity_VisualizationMode == EDITORVIZ_SHOWLIGHTMASK)
    {
        o.vizUV = v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
        o.lightCoord = mul(unity_EditorViz_WorldToLight, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)));
    }
	#endif
	
	return o;
}

float3 EncodeNormalBestFit(float3 n)
{
	float3 nU = abs(n);
	float maxNAbs = max(nU.z, max(nU.x, nU.y));
	float2 TC = nU.z < maxNAbs ? (nU.y < maxNAbs ? nU.yz : nU.xz) : nU.xy;
	//if (TC.x != TC.y)
	//{
	TC = TC.x < TC.y ? TC.yx : TC.xy;
	TC.y /= TC.x;

	n /= maxNAbs;
	float fittingScale = bestFitNormalMap.Load(int3(TC.x * 1023, TC.y * 1023, 0)).a;
	n *= fittingScale;
	//}
	return n * 0.5 + 0.5;
}

float3 TransformNormalMapToWorld(v2f_bakeryMeta i, float3 tangentNormalMap)
{
	float3x3 TBN = float3x3(normalize(i.tangent), normalize(i.binormal), normalize(i.normal));
	return mul(tangentNormalMap, TBN);
}

#define BakeryEncodeNormal EncodeNormalBestFit

#endif

float4 frag_customMeta (v2f_bakeryMeta i): SV_Target
{
    UnityMetaInput o;
    UNITY_INITIALIZE_OUTPUT(UnityMetaInput, o);

    // Output custom normal to use with Bakery's "Baked Normal Map" mode
    if (unity_MetaFragmentControl.z)
    {
        // Calculate custom normal
        float3 customNormalMap = UnpackNormal(tex2D(_BumpMap, pow(abs(i.uv), 1.5))); // example: UVs are procedurally distorted
        float3 customWorldNormal = TransformNormalMapToWorld(i, customNormalMap);

        // Output
        return float4(BakeryEncodeNormal(customWorldNormal),1);
    }
	
	FragmentCommonData data = UNITY_SETUP_BRDF_INPUT(i.uv);
    // Regular Unity meta pass
	#ifdef EDITOR_VISUALIZATION
		o.Albedo = data.diffColor;
		o.VizUV = i.vizUV;
		o.LightCoord = i.lightCoord;
	#else
		o.Albedo = UnityLightmappingAlbedo (data.diffColor, data.specColor, data.smoothness);
	#endif
	o.SpecularColor = data.specColor;
    return UnityMetaFragment(o);
}

