# UnityGaussianTex
Editor utility that transforms a texture to have gaussian histogram and generates lookup table (LUT) that maps the transformed image back to the original image, for the purpose
of detail preserving blending as described by [Deliot and Heitz (2019)](https://eheitzresearch.wordpress.com/738-2/). The authors of this paper created their own unity plugin available [here](https://github.com/UnityLabs/procedural-stochastic-texturing). The tool presented here was created based soley on the paper, prior to me knowing of the existence of Deliot and Heitz's own unity implementation. Hopefully, this tool still has some value as it is not tied to a particular render pipeline or version of unity, and is implemented very differently using compute shaders for most of the calculations, making it dramatically faster even in the tool's currently unoptimized state.

# Purpose
In shaders, it is sometimes useful to blend multiple samples of the same texture with different UV coordinates. For example this is done in triplanar mapping, where a texture is sampled three times using flattened world-space coordinates as UVs and the three samples are blended based on normal direction. This is also done in random tiling, where the UVs are divided into a triangular grid. Each grid point has an associated random UV offset, and each pixel blends between three texture samples taken with the offsets of the closest grid points. Doing a simple linear blend produces poor results in many cases, having reduced contrast with details from each texture being washed out and ghostly.

Linear Blending | Variance Preserving Blending
:---------------:|:---------------------------:
![Linear Blending](https://github.com/Error-mdl/UnityGaussianTex/blob/284daf7ef6b060c122e9e534ef47012c883bcfbf/Moss_LinearBlend.jpg) | ![Variance Preserving Blending](https://github.com/Error-mdl/UnityGaussianTex/blob/284daf7ef6b060c122e9e534ef47012c883bcfbf/Moss_VariancePreservingBlend.jpg)

A different solution to blending the texture samples together was first proposed by [Heitz and Neyret (2018)](https://eheitzresearch.wordpress.com/722-2/) and later refined by  Deliot and Heitz (2019). They observed that linear blending does not preserve the statistcal properties of the image, which is expressed by the histogram of the image. However, in the case where the input has a gaussian histogram there exists a different blending formula called a variance-preserving blend which retains the input's histogram. Their solution is to transform the input image's histogram to be gaussian using the inverse cumulative distribution function and to create a lookup table which maps the values in the gaussian-transformed texture back to the colors in the original image. Shaders using these textures first blend samples of the gaussian texture using the variance-preserving formula and then obtain the true color from the lookup table. This produces dramatically better results at the cost of only three (or four if alpha is needed) extra texture samples from the lookup table and a few other operations.

Normal Tiling | Random Tiling with Variance Preserving Blending
:---------------:|:---------------------------:
![Normal Tiling](https://github.com/Error-mdl/UnityGaussianTex/blob/284daf7ef6b060c122e9e534ef47012c883bcfbf/NormalTiling.jpg) | ![Variance Preserving Blending](https://github.com/Error-mdl/UnityGaussianTex/blob/284daf7ef6b060c122e9e534ef47012c883bcfbf/RandomTiling.jpg)
# Details
This script was based almost entirely off of Deliot and Heitz (2019), with a few minor adjustments from the Burley (2019) implementation.

The first step performed by the script is optionally computing a new color-space for the image where each axis is independent, and converting the image to this new colorspace. When blending a gaussian texture derived from normal RGB images, it is possible to generate colors that never existed in the original input as the RGB colors may be correlated. To generate a colorspace where each axis is independent, the tool calculates the covariance matrix of the input's RGB channels and finds its eigenvectors. For simplicity, alpha is not included and assumed to be independent. The eigenvectors are calculated using the iterative formula for 3x3 symmetric, real matricies described by [Eberly (2021)](https://www.geometrictools.com/Documentation/RobustEigenSymmetric3x3.pdf). The new color space is constructed using the eigenvectors as the basis vectors. The space is then shifted so that it is centered on the minimum value of each axis in the image, and the basis vectors are scaled to the bounds of the images values so that all values are in the 0 to 1 range. This step of creating a de-correlated space is optional as not all images will obviously show false colors when blended, and my implementation seems to slightly reduce color accuracy (possibly due to compression issues or loss of precision during the creation of the gaussian texture?).

The next step is to sort each color channel of the image. This is accomplished by a compute shader first splitting each color channel of the input into four compute buffers and generating four identical compute buffers that map the array index to flattened pixel coordinates. These four buffers will be sorted with each color channel, and are used to keep track of the original coordinates of each pixel. Then, each channel is sorted by a compute shader using the bitonic merge sort algorithm. This shader was written following [this tutorial by tgfrerer](https://poniesandlight.co.uk/reflect/bitonic_merge_sort/).

Once the texture is sorted, the actual generation of the gaussian transformed texture and associated lookup table can begin. For each element of each sorted color channel, a compute shader calculates the inverse cumulative distribution function (invCDF) of the element's index divided by the total number of elements and stores the color in the output gaussian image at the coordinates stored in the associated index buffer. Computing the invCDF is not trivial, as it involves the inverse error function. This function is properly calculated by an infinite series that converges incredibly slowly for values close to 1. Methods which very accurately approximate the inverse error function in fewer steps usually involve complex tricks ill-suited to gpu computation. Rather than actually calculate the inverse error function proper, I have opted to use the approximation described Vedder (1987) which uses a function composed a few sinh and tanh functions. This approximation is massively simpler to compute and is close enough for our purposes to the true value on the 0-1 range. Calculating the LUT is similar, and involves calculating the cumulative distribution function of a value U ranging from 0 to 1, finding the colors stored in the each of the four color channels at the index equal to value of the CDF of U times the total number of elements, and storing it in the output lookup table at a flattened coordinate equal to U. Additionally, as suggested by [Burely (2019)](https://www.jcgt.org/published/0008/04/02/paper-lowres.pdf) the CDF and invCDF functions were scaled to ensure no clipping occurs for input values close to 0 or 1.

In addition to the main LUT, different versions of the LUT must be generated for each mip level greater than the lowest mip using a different method. First the average variance of all 2^mip level blocks of pixels in the gaussian image is calculated. Then for each pixel of the LUT, the invCDF is calculated for (2x the number of elements in the LUT) values in the 0 to 1 range using the square root of the previously computed variance as the standard deviation and the element's index as the mean. These values are then averaged to get the final value at that element.

Additionally, a small improvement over the Deliot and Heitz (2019) implementation was made here. Rather than store each LUT as a horizontal strip of pixels in a (LUT elements) x (mip levels) image, this script generates a texture array where each element of the array is a single LUT wrapped into a sqrt(LUT elements) x sqrt(LUT elements) image. For an average 256 element LUT, this creates a texture array of 16x16 images. This incurs a cost of a few math operations to turn the 1d coordinate from the gaussian texture into a 2d coordinate. However, sampling from this tiny square LUT should have much better cache hit rate than sampling from a 256 pixel long strip of a larger image.

# Using the Editor Utility

Included with this tool is a GUI for generating the gaussian texture and lookup table, and for assigning the colorspace data to a material from a colorspace asset. The GUI can be found under "Window/Convert Texture to Gaussian".

![Editor Window](https://github.com/Error-mdl/UnityGaussianTex/blob/284daf7ef6b060c122e9e534ef47012c883bcfbf/Window.png)

Controls:
1. Convert Texture To Gaussian
	1. Texture To Convert: Input field for the texture to be converted
	1. Save as: Filetype to save the gaussian transformed texture as
	1. Decorrolate Colorspace: Convert image's RGB color space to one with independent basis vectors before perfoming the gaussian transformation. This prevents colors that do not exist in the original image from being created by the blending process, but also seems to increase banding in some cases. Not recommended for red-alpha images like metallic-smoothness maps or grayscale images
	1. Compression Correction: This is *supposed* to reduce issues with DXT compression by scaling the colors in the input texture by the inverse lengths of the colorspace vectors, but either I'm doing something wrong or this just plain causes compression issues in a lot of cases. 
	1. Lookup Table Dimensions: Width and height of each slice of the LUT texture2Darray, set as powers of 2 by the sliders below. 16x16 slices with 256 elements is ideal for most textures. Smaller dimensions decrease the number of possible colors but should improve the per-pixel cost of sampling the LUT, while larger dimensions get better color accuracy at the cost of slower sampling of the LUT.
	1. Compute Shaders: dropdown underneath which the three compute shaders are assigned. These should be automatically assigned assuming the "shader" folder containing the compute shaders is in the same directory as the "scripts" folder containing TexToGaussian.cs
	1. Create Gaussian Texture and Lookup Table: Pressing this button will create three assets in the same directory as the input texture with the same name as the input texture plus an additional identifier at the end. These are texture name + "_gauss.png" which is the gaussian texture, texture name + "_lut.asset" which is the lut saved as unity's texture2Darray asset, and texture name + "_colorspace.asset" which is a scriptable object containing the image's decorrolated colorspace basis vectors and center.
1. Copy Colorspace Settings to Material
	1. Material to copy the colorspace settings to
	1. Colorspace scriptable object to copy the settings from
	1. Material Property Names: names of the material properties that store the colorspace basis vectors and center

# Notes About Utilizing Gaussian Blending in Shaders

Included with this tool is GaussianBlend.cginc, which defines many functions for blending multiple samples of gaussian textures, obtaining the color from the lookup texture, and converting the color from the decorrolated colorspace to RGB as well as functions for doing random tiling. An example of random tiling is included in shaders/Demo/BlendDemo.shader. See that shader for a basic implementation of doing random tiling on an unlit diffuse texture. Additionally, a more complex example of random tiling in a PBR shader is included in shaders/PBRExample/RTStandardOpaque

All shaders using GaussianBlend.cginc need to add `#pragma target 5.0` to the header as the included functions use shader model 5 specific functions. Additionally, rather than defining the main texture as `sampler2D _MainTex;` use either the unity macro `UNITY_DECLARE_TEX2D(_MainTex)` or declare the texture and sampler separately like:
```
Texture2D _MainTex;
sampler sampler_MainTex;
```
This is so you will have access to the sampler for calculating the mip level with CalcMipLevel. Also declare the LUT as just a Texture2DArray like `Texture2DArray<fixed4> _LUTTex;` as we do not need a sampler state for this texture.

When blending the main albedo texture use the `Blend3GaussianRGB` or `Blend3GaussianRGBA` to get the variance-preserved blend of three samples of the gaussian texture, use CalcMipLevel with the uvs of one of the samples and the main texture sampler to get the mip level, then use `LookUpTableRGB` or `LookUpTableRGBA` to get the true color in the decorrolated colorspace, and then use ConvertColorspaceToRGB to get the final color.

For supporting textures like the metallic-smoothness map where the color channels contain non-color, completely independent information it is advisable to generate the textures with "decorrolate colorspace" unchecked. It is not necessary and requires the shader have an additional 4 float4's to contain that texture's colorspace. The functions in the cginc for blending red-alpha (used for unity's metallic smoothness), red-green, and red (single channel or grayscale images) do not correct for decorrelated colorspaces. If you use a packed map that utilizes other channels you should use `Blend3GaussianRGBNoCs` or `Blend3GaussianRGBANoCs` which don't use a colorspace. In theses cases just directly use the color returned by the blend function.

# References

Deliot, Thomas, and Eric Heitz. "Procedural stochastic textures by tiling and blending." GPU Zen 2 (2019). [](https://eheitzresearch.wordpress.com/738-2/)

Burley, Brent, and Walt Disney Animation Studios. "On Histogram-Preserving Blending for Randomized Texture Tiling." Journal of Computer Graphics Techniques (JCGT) 8.4 (2019): 8. [](https://www.jcgt.org/published/0008/04/02/paper-lowres.pdf)

Implementing Bitonic Merge Sort in Vulkan Compute. [](https://poniesandlight.co.uk/reflect/bitonic_merge_sort/)
 
Eberly, David. "A Robust Eigensolver for 3x3 Symmetric Matrices." Geometric Tools (2021). [](https://www.geometrictools.com/Documentation/RobustEigenSymmetric3x3.pdf)

Vedder, John D. "Simple approximations for the error function and its inverse." American Journal of Physics 55.8 (1987): 762-763. [](https://aapt.scitation.org/doi/abs/10.1119/1.15018?journalCode=ajp)
