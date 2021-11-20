/*****************************************************************************************
 * @file   GaussianBlend.cginc
 * @brief  Library of functions relating to sampling, blending, and inverting gaussian textures
 * @author Error-mdl
 * @date   2021
 *****************************************************************************************/

 // Missing macros for defining texture2D's and their samplers and for sampling a texture2d with the gradient function
#ifndef UNITY_PASS_TEX2D
	#define UNITY_PASS_TEX2D(tex) tex, sampler##tex
#endif

#ifndef UNITY_SAMPLE_TEX2D_GRAD
	#define UNITY_SAMPLE_TEX2D_GRAD(tex, coord, DDX, DDY) tex.SampleGrad (sampler##tex, coord, DDX, DDY)
#endif

#define SQRT_3     1.732050808
#define TWO_SQRT_3 3.464101615
#define TWO_TAN_30 1.154700538
#define TAN_30     0.577350269

/**
 * A structure containing the three basis vectors of the colorspace, the inverse of the lengths of the basis vectors,
 * and the center of the colorspace
 */
struct colorspace
{
	float4 axis0; ////< First basis vector of the colorspace in the xyz components, inverse length of the axis in w
	float4 axis1; ////< Second basis vector of the colorspace in the xyz components, inverse length of the axis in w
	float4 axis2; ////< Third basis vector of the colorspace in the xyz components, inverse length of the axis in w
	float4 center; ///< Center of the colorspace in the xyz components, w is unused
};

//-------------------------------------------------------------------------------------------------------------------------------------------------------------
//-LUT Functions-----------------------------------------------------------------------------------------------------------------------------------------------
//-------------------------------------------------------------------------------------------------------------------------------------------------------------


/** @brief Takes the R color from gaussian textures and samples the lookup table to find the true color
 *  
 *  @param lut      The look up table (LUT)
 *  @param lutDim   float2 containing the width, height of the LUT
 *  @param coords   float2 containing the R and A colors from the gaussian texture, which represents coordinates in the LUT
 *  @param mip      mip level used when sampling the gaussian texture
 *
 *  @returns The non-gaussian color of the texture in the colorspace associated with the LUT. Note that for single channel
 *           textures, there is only one variable so gaussian textures generated from these should not have decorrelated
 *           colorspaces
 */

float4 LookUpTableR(Texture2DArray lut, float2 lutDim, const float coords, const float mip)
{
    uint coords1 = floor(coords * (lutDim.x * lutDim.y - 1.0));
    uint LUTWidth = (uint)lutDim.x;
    uint LUTModW = LUTWidth - 1; // x % y is equivalent to x & (y - 1) if y is power of 2
    uint LUTDivW = firstbithigh(LUTWidth); // LUTWidth is a power of 2, so firstbithigh gives log2(LUTWidth)
    float4 sample1 = float4(
        lut.Load(int4(coords1 & LUTModW, coords1 >> LUTDivW, mip, 0)).r,
        0,
        0,
        1
        );
    return sample1;
}


/** @brief Takes the R and G color from gaussian textures and samples the lookup table 2 times to find the true color
 *  
 *  @param lut      The look up table (LUT)
 *  @param lutDim   float2 containing the width, height of the LUT
 *  @param coords   float2 containing the R and G colors from the gaussian texture, which represents coordinates in the LUT
 *  @param mip      mip level used when sampling the gaussian texture
 *
 *  @returns The non-gaussian color of the texture in the (possibly non-rgb) colorspace associated with the LUT  
 */

float4 LookUpTableRG(Texture2DArray lut, float2 lutDim, const float2 coords, const float mip)
{
    uint2 coords1 = floor(coords * (lutDim.x * lutDim.y - 1.0));
    uint LUTWidth = (uint)lutDim.x;
    uint LUTModW = LUTWidth - 1; // x % y is equivalent to x & (y - 1) if y is power of 2
    uint LUTDivW = firstbithigh(LUTWidth); // LUTWidth is a power of 2, so firstbithigh gives log2(LUTWidth)
    float4 sample1 = float4(
        lut.Load(int4(coords1.x & LUTModW, coords1.x >> LUTDivW, mip, 0)).r,
        lut.Load(int4(coords1.y & LUTModW, coords1.y >> LUTDivW, mip, 0)).g,
        0,
        1
        );
    return sample1;
}


/** @brief Takes the R and A color from gaussian textures and samples the lookup table 2 times to find the true color,
 *         useful for unity metallic-smoothness maps.
 *  
 *  @param lut      The look up table (LUT)
 *  @param lutDim   float2 containing the width, height of the LUT
 *  @param coords   float2 containing the R and A colors from the gaussian texture, which represents coordinates in the LUT
 *  @param mip      mip level used when sampling the gaussian texture
 *
 *  @returns The non-gaussian color of the texture in the (possibly non-rgb) colorspace associated with the LUT. Note that
 *           for RA textures, red and alpha are practically independent so gaussian textures generated from these images
 *           should not have decorrelated colorspaces and thus the return value should already be in the proper space 
 */

float4 LookUpTableRA(Texture2DArray lut, float2 lutDim, const float2 coords, const float mip)
{
    uint2 coords1 = floor(coords * (lutDim.x * lutDim.y - 1.0));
    uint LUTWidth = (uint)lutDim.x;
    uint LUTModW = LUTWidth - 1; // x % y is equivalent to x & (y - 1) if y is power of 2
    uint LUTDivW = firstbithigh(LUTWidth); // LUTWidth is a power of 2, so firstbithigh gives log2(LUTWidth)
    float4 sample1 = float4(
        lut.Load(int4(coords1.x & LUTModW, coords1.x >> LUTDivW, mip, 0)).r,
        0,
        0,
        lut.Load(int4(coords1.y & LUTModW, coords1.y >> LUTDivW, mip, 0)).a
        );
    return sample1;
}

/** @brief Takes the RGB (no alpha) color from gaussian textures and samples the lookup table 3 times to find the true color
 *  
 *  @param lut      The look up table (LUT)
 *  @param lutDim   float2 containing the width, height of the LUT
 *  @param coords   float3 containing the RGB colors from the gaussian texture, which represents coordinates in the LUT
 *  @param mip      mip level used when sampling the gaussian texture
 *
 *  @returns The non-gaussian color of the texture in the (possibly non-rgb) colorspace associated with the LUT  
 */

float4 LookUpTableRGB(Texture2DArray lut, float2 lutDim, const float3 coords, const float mip)
{
    uint3 coords1 = floor(coords * (lutDim.x * lutDim.y - 1.0));
    uint LUTWidth = (uint)lutDim.x;
    uint LUTModW = LUTWidth - 1; // x % y is equivalent to x & (y - 1) if y is power of 2
    uint LUTDivW = firstbithigh(LUTWidth); // LUTWidth is a power of 2, so firstbithigh gives log2(LUTWidth)
    float3 sample1 = float3(
        lut.Load(int4(coords1.x & LUTModW, coords1.x >> LUTDivW, mip, 0)).r,
        lut.Load(int4(coords1.y & LUTModW, coords1.y >> LUTDivW, mip, 0)).g,
        lut.Load(int4(coords1.z & LUTModW, coords1.z >> LUTDivW, mip, 0)).b
        );
    return float4(sample1, 1);
}

/** @brief Takes the RGBA color from gaussian textures and samples the lookup table 4 times to find the true color
 *  
 *  @param lut      The look up table (LUT)
 *  @param lutDim   float2 containing the width, height of the LUT
 *  @param coords   float4 containing the RGBA colors from the gaussian texture, which represents coordinates in the LUT
 *  @param mip      mip level used when sampling the gaussian texture
 *
 *  @returns The non-gaussian color of the texture in the (possibly non-rgb) colorspace associated with the LUT  
 */

float4 LookUpTableRGBA(Texture2DArray lut, float2 lutDim, const float4 coords, const float mip)
{
    uint4 coords1 = floor(coords * (lutDim.x * lutDim.y - 1.0));
    uint LUTWidth = (uint)lutDim.x;
    uint LUTModW = LUTWidth - 1; // x % y is equivalent to x & (y - 1) if y is power of 2
    uint LUTDivW = firstbithigh(LUTWidth); // LUTWidth is a power of 2, so firstbithigh gives log2(LUTWidth)
    float4 sample1 = float4(
        lut.Load(int4(coords1.x & LUTModW, coords1.x >> LUTDivW, mip, 0)).r,
        lut.Load(int4(coords1.y & LUTModW, coords1.y >> LUTDivW, mip, 0)).g,
        lut.Load(int4(coords1.z & LUTModW, coords1.z >> LUTDivW, mip, 0)).b,
        lut.Load(int4(coords1.w & LUTModW, coords1.w >> LUTDivW, mip, 0)).a
        );
    return sample1;
}


//-------------------------------------------------------------------------------------------------------------------------------------------------------------
//-Blend Functions---------------------------------------------------------------------------------------------------------------------------------------------
//-------------------------------------------------------------------------------------------------------------------------------------------------------------

/** @brief Takes the R color from two samples of a gaussian texture and blends them with variance preserving blending
 *  
 *  @param gaussian1    First gaussian sample R
 *  @param gaussian2    Second gaussian sample R
 *  @param weights      float2 containing the 0-1 weights of the two gaussian samples
 *  @returns The non-gaussian color of the texture, note that single channel images should not be generated with decorrleated colorspaces
 */

float Blend2GaussianR(float2 gaussian1, float2 gaussian2,
	float2 weights)
{
		float gaussian = ((weights.x * gaussian1 + weights.y * gaussian2 - 0.5)
            / sqrt(weights.x * weights.x + weights.y * weights.y)) + 0.5;
        return saturate(gaussian);
}


/** @brief Takes the R color from three samples of a gaussian texture and blends them with variance preserving blending
 *  
 *  @param gaussian1    First gaussian sample R
 *  @param gaussian2    Second gaussian sample R
 *  @param gaussian3    Third gaussian sample R
 *  @param weights      float3 containing the 0-1 weights of each of the three gaussian samples
 *  @returns The non-gaussian color of the texture, note that single channel images should not be generated with decorrleated colorspaces
 */

float Blend3GaussianR(float gaussian1, float2 gaussian2, float gaussian3,
	float3 weights)
{
		float gaussian = ((weights.x * gaussian1 + weights.y * gaussian2 + weights.z * gaussian3 - 0.5)
            / sqrt(weights.x * weights.x + weights.y * weights.y + weights.z * weights.z)) + 0.5;
        return saturate(gaussian);
}

/** @brief Takes the RG colors from two samples of a gaussian texture and blends them with variance preserving blending, assumes RGB colorspace
 *  
 *  @param gaussian1    First gaussian sample RG
 *  @param gaussian2    Second gaussian sample RG
 *  @param weights      float2 containing the 0-1 weights of the two gaussian samples
 *  @returns The non-gaussian color of the texture, assuming the texture does not have a decorrelated colorspace
 */

float2 Blend2GaussianRG(float2 gaussian1, float2 gaussian2,
	float2 weights)
{
		float2 gaussian =  ((weights.x * gaussian1 + weights.y * gaussian2 - float2(0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y)) + float2(0.5, 0.5);
        return saturate(gaussian);
}


/** @brief Takes the RG colors from three samples of a gaussian texture and blends them with variance preserving blending
 *  
 *  @param gaussian1    First gaussian sample RG
 *  @param gaussian2    Second gaussian sample RG
 *  @param gaussian3    Third gaussian sample RG
 *  @param weights      float3 containing the 0-1 weights of each of the three gaussian samples
 *  @returns The non-gaussian color of the texture, assuming the texture does not have a decorrelated colorspace
 */

float2 Blend3GaussianRG(float2 gaussian1, float2 gaussian2, float2 gaussian3,
	float3 weights)
{
		float2 gaussian = ((weights.x * gaussian1 + weights.y * gaussian2 + weights.z * gaussian3 - float2(0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y + weights.z * weights.z)) + float2(0.5, 0.5);
        return saturate(gaussian);
}

/** @brief Takes the RG or RA colors from two samples of a gaussian texture and blends them with variance preserving blending
 *  
 *  @param gaussian1    First gaussian sample RA
 *  @param gaussian2    Second gaussian sample RA
 *  @param weights      float2 containing the 0-1 weights of the two gaussian samples
 *  @returns The non-gaussian color of the texture in the (possibly non-rgb) colorspace associated with the LUT. Note that
 *           for RA textures, red and alpha are practically independent so gaussian textures generated from these images
 *           should not have decorrelated colorspaces and thus cs.axis0.w should be 1
 */

float2 Blend2GaussianRA(float2 gaussian1, float2 gaussian2,
	float2 weights)
{
		float2 gaussian = ((weights.x * gaussian1 + weights.y * gaussian2 - float2(0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y)) + float2(0.5, 0.5);
        return saturate(gaussian);
}


/** @brief Takes the RA colors from three samples of a gaussian texture and blends them with variance preserving blending
 *  
 *  @param gaussian1    First gaussian sample RA
 *  @param gaussian2    Second gaussian sample RA
 *  @param gaussian3    Third gaussian sample RA
 *  @param weights      float3 containing the 0-1 weights of each of the three gaussian samples
 *  @returns The non-gaussian color of the texture in normal RGBA colorspace. Note that
 *           for RA textures, red and alpha are practically independent so gaussian textures generated from these images
 *           should not have decorrelated colorspaces
 */

float2 Blend3GaussianRA(float2 gaussian1, float2 gaussian2, float2 gaussian3,
	float3 weights)
{
		float2 gaussian = ((weights.x * gaussian1 + weights.y * gaussian2 + weights.z * gaussian3 - float2(0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y + weights.z * weights.z)) + float2(0.5, 0.5);
        return saturate(gaussian);
}

/** @brief Takes the RGB colors from two samples of a gaussian texture and blends them with variance preserving blending
 *  
 *  @param gaussian1    First gaussian sample RGB
 *  @param gaussian2    Second gaussian sample RGB
 *  @param weights      float2 containing the 0-1 weights of the two gaussian samples
 *  @param cs           colorspace struct containing the inverse lengths of the basis vectors as the w component of the vectors
 *  @returns The non-gaussian color of the texture in the (possibly non-rgb) colorspace associated with the LUT  
 */

float3 Blend2GaussianRGB(float3 gaussian1, float3 gaussian2,
	float2 weights, colorspace cs)
{
		float3 gaussian = float3(cs.axis0.w, cs.axis1.w, cs.axis2.w) * ((weights.x * gaussian1 + weights.y * gaussian2 - float3(0.5,0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y)) + float3(0.5, 0.5, 0.5);
        return saturate(gaussian);
}


/** @brief Takes the RGB colors from three samples of a gaussian texture and blends them with variance preserving blending
 *  
 *  @param gaussian1    First gaussian sample RGB
 *  @param gaussian2    Second gaussian sample RGB
 *  @param gaussian3    Third gaussian sample RGB
 *  @param weights      float3 containing the 0-1 weights of each of the three gaussian samples
 *  @param cs           colorspace struct containing the inverse lengths of the basis vectors as the w component of the vectors
 *  @returns The non-gaussian color of the texture in the (possibly non-rgb) colorspace associated with the LUT  
 */

float3 Blend3GaussianRGB(float3 gaussian1, float3 gaussian2, float3 gaussian3,
	float3 weights, colorspace cs)
{
		float3 gaussian = float3(cs.axis0.w, cs.axis1.w, cs.axis2.w) * ((weights.x * gaussian1 + weights.y * gaussian2 + weights.z * gaussian3 - float3(0.5,0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y + weights.z * weights.z)) + float3(0.5, 0.5, 0.5);
        return saturate(gaussian);
}

/** @brief Takes the RGB colors from two samples of a gaussian texture and blends them with variance preserving blending, assumes normal RGB colorspace
 *  
 *  @param gaussian1    First gaussian sample RGB
 *  @param gaussian2    Second gaussian sample RGB
 *  @param weights      float2 containing the 0-1 weights of the two gaussian samples
 *  @returns The non-gaussian color of the texture, assuming the texture does not have a decorrelated colorspace
 */

float3 Blend2GaussianRGBNoCs(float3 gaussian1, float3 gaussian2,
	float2 weights)
{
		float3 gaussian = ((weights.x * gaussian1 + weights.y * gaussian2 - float3(0.5,0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y)) + float3(0.5, 0.5, 0.5);
        return saturate(gaussian);
}


/** @brief Takes the RGB colors from three samples of a gaussian texture and blends them with variance preserving blending, assumes normal RGB colorspace
 *  
 *  @param gaussian1    First gaussian sample RGB
 *  @param gaussian2    Second gaussian sample RGB
 *  @param gaussian3    Third gaussian sample RGB
 *  @param weights      float3 containing the 0-1 weights of each of the three gaussian samples
 *  @returns The non-gaussian color of the texture, assuming the texture does not have a decorrelated colorspace
 */

float3 Blend3GaussianRGBNoCs(float3 gaussian1, float3 gaussian2, float3 gaussian3,
	float3 weights, colorspace cs)
{
		float3 gaussian = ((weights.x * gaussian1 + weights.y * gaussian2 + weights.z * gaussian3 - float3(0.5,0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y + weights.z * weights.z)) + float3(0.5, 0.5, 0.5);
        return saturate(gaussian);
}


/** @brief Takes the RGBA colors from two samples of a gaussian texture and blends them with variance preserving blending
 *  
 *  @param gaussian1    First gaussian sample RGBA
 *  @param gaussian2    Second gaussian sample RGBA
 *  @param weights      float2 containing the 0-1 weights of the two gaussian samples
 *  @param cs           colorspace struct containing the inverse lengths of the basis vectors as the w component of the vectors
 *  @returns The non-gaussian color of the texture in the (possibly non-rgb) colorspace associated with the LUT  
 */

float4 Blend2GaussianRGBA(float4 gaussian1, float4 gaussian2,
	float2 weights, colorspace cs)
{
		float4 gaussian = float4(cs.axis0.w, cs.axis1.w, cs.axis2.w, 1) * ((weights.x * gaussian1 + weights.y * gaussian2 - float4(0.5,0.5,0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y)) + float4(0.5, 0.5, 0.5, 0.5);
        return saturate(gaussian);
}


/** @brief Takes the RGBA colors from three samples of a gaussian texture and blends them with variance preserving blending
 *  
 *  @param gaussian1    First gaussian sample RGBA
 *  @param gaussian2    Second gaussian sample RGBA
 *  @param gaussian3    Third gaussian sample RGBA
 *  @param weights      float3 containing the 0-1 weights of each of the three gaussian samples
 *  @param cs           colorspace struct containing the inverse lengths of the basis vectors as the w component of the vectors
 *  @returns The non-gaussian color of the texture in the (possibly non-rgb) colorspace associated with the LUT  
 */

float4 Blend3GaussianRGBA(float4 gaussian1, float4 gaussian2, float4 gaussian3,
	float3 weights, colorspace cs)
{
		float4 gaussian = float4(cs.axis0.w, cs.axis1.w, cs.axis2.w, 1) * ((weights.x * gaussian1 + weights.y * gaussian2 + weights.z * gaussian3 - float4(0.5,0.5,0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y + weights.z * weights.z)) + float4(0.5, 0.5, 0.5, 0.5);
        return saturate(gaussian);
}

/** @brief Takes the RGBA colors from two samples of a gaussian texture and blends them with variance preserving blending, assumes normal RGB colorspace
 *  
 *  @param gaussian1    First gaussian sample RGBA
 *  @param gaussian2    Second gaussian sample RGBA
 *  @param weights      float2 containing the 0-1 weights of the two gaussian samples
 *  @returns The non-gaussian color of the texture, assuming the texture does not have a decorrelated colorspace
 */

float4 Blend2GaussianRGBANoCs(float4 gaussian1, float4 gaussian2,
	float2 weights)
{
		float4 gaussian = ((weights.x * gaussian1 + weights.y * gaussian2 - float4(0.5,0.5,0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y)) + float4(0.5, 0.5, 0.5, 0.5);
        return saturate(gaussian);
}


/** @brief Takes the RGBA colors from three samples of a gaussian texture and blends them with variance preserving blending, assumes normal RGB colorspace
 *  
 *  @param gaussian1    First gaussian sample RGBA
 *  @param gaussian2    Second gaussian sample RGBA
 *  @param gaussian3    Third gaussian sample RGBA
 *  @param weights      float3 containing the 0-1 weights of each of the three gaussian samples
 *  @returns The non-gaussian color of the texture, assuming the texture does not have a decorrelated colorspace
 */

float4 Blend3GaussianRGBANoCs(float4 gaussian1, float4 gaussian2, float4 gaussian3,
	float3 weights)
{
		float4 gaussian = ((weights.x * gaussian1 + weights.y * gaussian2 + weights.z * gaussian3 - float4(0.5,0.5,0.5,0.5))
            / sqrt(weights.x * weights.x + weights.y * weights.y + weights.z * weights.z)) + float4(0.5, 0.5, 0.5, 0.5);
        return saturate(gaussian);
}


//-------------------------------------------------------------------------------------------------------------------------------------------------------------
//-Misc Functions----------------------------------------------------------------------------------------------------------------------------------------------
//-------------------------------------------------------------------------------------------------------------------------------------------------------------

/** @brief Takes the color output from the LUT in the decorrolated colorspace and converts it back to RGB
 *  
 *  @param lutColor The color from the LUT
 *  @param cs       Colorspace to convert from
 *
 *  @returns the final correct RGB color
 */

float3 ConvertColorspaceToRGB(float3 lutColor, const colorspace cs)
{
    return (lutColor.r * cs.axis0.xyz + lutColor.g * cs.axis1.xyz + lutColor.b * cs.axis2.xyz) + cs.center.xyz;
}

/** @brief Finds the mip level of a given texture and sampler with given uvs
 *  
 *  @param tex          The texture to determine the mip level of
 *  @param tex_sampler  The sampler corresponding to tex
 *  @param uv           uv coordinates that tex was sampled with
 *
 *  @returns the mip level of tex
 */

inline float CalcMipLevel(Texture2D tex, sampler sampler_tex, float2 uv)
{
    return tex.CalculateLevelOfDetail(sampler_tex, uv);
}


//-------------------------------------------------------------------------------------------------------------------------------------------------------------
//-Random Tiling Functions-------------------------------------------------------------------------------------------------------------------------------------
//-------------------------------------------------------------------------------------------------------------------------------------------------------------



float2 hash2(float2 input)
{
	return frac(sin(mul(float2x2(137.231, 512.37, 199.137, 373.351), input)) * 23597.3733);
}

/*
   (1,0)
     Y   Y'_______
     |   /\       /
     |30/  \     /
     | /    \   /
     |/      \ /
     0-----X--X'
   (0,0) (1,0)

Triangle Grid For Random Offset Tiling
X and Y are the original basis vectors in the UV space (1,0) and (0,1) respectively. Do
a space transformation with basis vectors X' and Y' such that X' is X scaled up by
2/sqrt(3) and Y' is Y sheared along the X axis so that the angle Y0Y' is 30 degrees.
The line connecting X' and Y' splits the new space's unit cell into two triangles which
map back to two equilateral triangles in the original space

Y' = ( tan(30), 1) = (1/sqrt(3), 1)
X' = ( 2 * tan(30), 0) = (2/sqrt(3), 0);
*/

/** @brief  For a given UV coordinate, generates 3 random offsets associated with the closest 3 points of a triangular grid
 *          and calculates the normalized weights of each three offsets based on the distance to each point from the input uv
 *
 *  @param uv           Input uv to calculate the offsets and weights for
 *  @param triWeights   float3 into which the weights of each offset UV will be output
 *  @param uvVertex0    float3 into which the offset associated with the first point in the grid will be Output
 *  @param uvVertex1    float3 into which the offset associated with the second point in the grid will be Output
 *  @param uvVertex2    float3 into which the offset associated with the third point in the grid will be Output
 */

void RandomOffsetTiling(float2 uv, inout float3 triWeights,
	inout float2 uvVertex0, inout float2 uvVertex1, inout float2 uvVertex2)
{

	float2x2 ShearedUVSpace = float2x2(-TAN_30, 1, TWO_TAN_30, 0); //WHY MUST TAN 30 BE NEGATIVE? WHY? IT SHOULDN"T BE BUT IT DOESN'T WORK OTHERWISE. I CAN'T FUCKING MATH.

	float2 shearedUVs = mul(ShearedUVSpace, uv);
	float2 intSUVs = floor(shearedUVs);
	float2 fracSUVs = frac(shearedUVs);
	float Ternary3rdComponent = 1.0 - fracSUVs.x - fracSUVs.y;
	float2 vertex0Offset = Ternary3rdComponent > 0 ? float2(0, 0) : float2(1, 1);
	float2 hashVertex0 = intSUVs + vertex0Offset;
	float2 hashVertex1 = intSUVs + float2(0, 1);
	float2 hashVertex2 = intSUVs + float2(1, 0);
	hashVertex0 = hash2(hashVertex0);
	hashVertex1 = hash2(hashVertex1);
	hashVertex2 = hash2(hashVertex2);
	/*
	float sin0, cos0, sin1, cos1, sin2, cos2;
	sincos(0.5 * (hashVertex0.x + hashVertex0.y) - 0.25, sin0, cos0);
	sincos(0.5 * (hashVertex1.x + hashVertex1.y) - 0.25, sin1, cos1);
	sincos(0.5 * (hashVertex2.x + hashVertex2.y) - 0.25, sin2, cos2);
    */
	uvVertex0 += hashVertex0;
	uvVertex1 += hashVertex1;
	uvVertex2 += hashVertex2;
	
	/*
	uvVertex0 = uv * lerp(0.8, 1.2, hashVertex0.x)  + hashVertex0;
	uvVertex1 = uv * lerp(0.8, 1.2, hashVertex1.x) + hashVertex1;
	uvVertex2 = uv * lerp(0.8, 1.2, hashVertex2.x) + hashVertex2;
	*/
	if (Ternary3rdComponent > 0)
	{
		triWeights = float3(Ternary3rdComponent, fracSUVs.y, fracSUVs.x);
	}
	else
	{
		triWeights = float3(-Ternary3rdComponent, 1.0 - fracSUVs.x, 1.0 - fracSUVs.y);
	}
}
