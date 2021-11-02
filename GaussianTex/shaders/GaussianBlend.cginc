#ifndef UNITY_PASS_TEX2D
	#define UNITY_PASS_TEX2D(tex) tex, sampler##tex
#endif


struct colorspace
{
	float4 axis1;
	float4 axis2;
	float4 axis3;
	float4 center;
};

float3 Blend3GaussianRGB(float3 gaussian1, float3 gaussian2, float3 gaussian3,
	float3 weights, colorspace cs, float mip, int trilinear)
{
		
}