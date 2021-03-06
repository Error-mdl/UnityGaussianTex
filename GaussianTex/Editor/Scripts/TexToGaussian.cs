using System.IO;
using UnityEngine;
using UnityEditor;
using MatrixPlus;

namespace GaussianTexture
{
  public enum FileType { png, jpg, tga, exr };


  public class TexToGaussian : ScriptableObject
  {

    public ComputeShader TexturePreprocessor;
    public ComputeShader BitonicSort;
    public ComputeShader CumulativeDistribution;
    public RenderTexture TestOutput;
    public RenderTexture TestLUT;

    
    private static int texInP = Shader.PropertyToID("_TexIn");
    private static int texIn2P = Shader.PropertyToID("_TexIn2");
    private static int texOutP = Shader.PropertyToID("_TexOut");
    private static int redP = Shader.PropertyToID("_Red");
    private static int greenP = Shader.PropertyToID("_Green");
    private static int blueP = Shader.PropertyToID("_Blue");
    private static int alphaP = Shader.PropertyToID("_Alpha");
    private static int indexRP = Shader.PropertyToID("_IndexR");
    private static int indexGP = Shader.PropertyToID("_IndexG");
    private static int indexBP = Shader.PropertyToID("_IndexB");
    private static int indexAP = Shader.PropertyToID("_IndexA");
    private static int bufferAvgP = Shader.PropertyToID("_BufferAvg");
    private static int mipP = Shader.PropertyToID("_Mip");
    private static int texWidthP = Shader.PropertyToID("_TexWidth");
    private static int texHeightP = Shader.PropertyToID("_TexHeight");
    private static int kernelWidthP = Shader.PropertyToID("_KernelWidth");
    private static int kernelHeightP = Shader.PropertyToID("_KernelHeight");
    private static int eigenVector1P = Shader.PropertyToID("_EigenVector1");
    private static int eigenVector2P = Shader.PropertyToID("_EigenVector2");
    private static int eigenVector3P = Shader.PropertyToID("_EigenVector3");
    private static int minColorP = Shader.PropertyToID("_MinColor");
    private static int maxColorP = Shader.PropertyToID("_MaxColor");
    private static int swizzleMaskP = Shader.PropertyToID("_SwizzleMask");

    private static int colorP = Shader.PropertyToID("_Color");
    private static int indexP = Shader.PropertyToID("_Index");
    private static int heightPowerP = Shader.PropertyToID("_HeightPower");

    private static int lutP = Shader.PropertyToID("_LUT");
    private static int colorMaskP = Shader.PropertyToID("_ColorMask");
    private static int invAxisLengthsP = Shader.PropertyToID("_InvAxisLengths");
    private static int lutWidthP = Shader.PropertyToID("_LUTWidth");
    private static int lutHeightP = Shader.PropertyToID("_LUTHeight");
    private static int mipLevelP = Shader.PropertyToID("_MipLevel");
    private static int mipStdP = Shader.PropertyToID("_MipStd");


    #region PRIVATE_METHODS

    /// <summary>
    /// Copies the contents of a texture to a random write enabled rendertexture
    /// </summary>
    /// <param name="TexIn">The texture to copy</param>
    /// <param name="TexOut">The rendertexture to copy the texture to. Must have random write enabled</param>
    private void copyImage(Texture TexIn, RenderTexture TexOut)
    {
      int copyImageKern = TexturePreprocessor.FindKernel("CopyTexture");
      TexturePreprocessor.SetInt(texWidthP, TexIn.width);
      TexturePreprocessor.SetInt(texHeightP, TexIn.height);
      TexturePreprocessor.SetTexture(copyImageKern, texInP, TexIn);
      TexturePreprocessor.SetTexture(copyImageKern, texOutP, TexOut);
      TexturePreprocessor.Dispatch(copyImageKern, Mathf.Max(1, TexIn.width / 32), Mathf.Max(1, TexIn.height / 32), 1);
    }

    /// <summary>
    /// Performs an operation in the image preprocessor compute shader over an entire image. The three kernels that work with
    /// this function are AverageImage, MinImage, and MaxImage
    /// </summary>
    /// <param name="operationKernel">ID of the kernel to execute, should be either AverageImage, MinImage, or MaxImage</param>
    /// <param name="tempRT1">A temporary rendertexture used during computation which should contain the image data to be processed</param>
    /// <param name="tempRT2">A temporary rendertexture used during computation, should be the same size and format as tempRT1</param>
    /// <param name="AvgOut">A RWBuffer with 4 elements of size 4, will contain the average of each color channel in the image once finished</param>
    /// <param name="avgProp">The shader property ID of _BufferAvg</param>
    /// <param name="texInProp">The shader property ID of _TexIn</param>
    /// <param name="texOutProp">The shader property ID of _TexOut</param>
    /// <param name="texWidthProp">The shader propertyID of _TexWidth</param>
    /// <param name="texHeightProp">The shader propertyID of _TexHeight</param>
    void ImageOperation(int operationKernel, RenderTexture tempRT1, RenderTexture tempRT2, ComputeBuffer AvgOut, int texWidth, int texHeight)
    {
      //int averageKernel = TexturePreprocessor.FindKernel("AverageImage");
      int kernelWidth = 4;
      int smallestDim = Mathf.Min(texWidth, texHeight);

      TexturePreprocessor.SetInt(kernelWidthP, kernelWidth);
      TexturePreprocessor.SetInt(kernelHeightP, kernelWidth);
      TexturePreprocessor.SetBuffer(operationKernel, bufferAvgP, AvgOut);

      bool oddStep = false;

      if (texWidth * texHeight == 1)
      {
        TexturePreprocessor.SetTexture(operationKernel, texInP, tempRT2);
        TexturePreprocessor.SetTexture(operationKernel, texOutP, tempRT1);
      
        TexturePreprocessor.SetInt(kernelWidthP, texWidth);
        TexturePreprocessor.SetInt(kernelHeightP, texHeight);
        TexturePreprocessor.SetInt(texWidthP, texWidth);
        TexturePreprocessor.SetInt(texHeightP, texHeight);
        TexturePreprocessor.Dispatch(operationKernel, 1, 1, 1);
      }

      for (int i = smallestDim; i >= kernelWidth; i /= kernelWidth)
      {
        if (oddStep == true)
        {
          TexturePreprocessor.SetTexture(operationKernel, texInP, tempRT2);
          TexturePreprocessor.SetTexture(operationKernel, texOutP, tempRT1);
        }
        else
        {
          TexturePreprocessor.SetTexture(operationKernel, texInP, tempRT1);
          TexturePreprocessor.SetTexture(operationKernel, texOutP, tempRT2);
        }

        TexturePreprocessor.SetInt(texWidthP, texWidth);
        TexturePreprocessor.SetInt(texHeightP, texHeight);
        TexturePreprocessor.Dispatch(operationKernel, Mathf.Max(texWidth * texHeight / 1024, 1), 1, 1);

        oddStep = !oddStep;
        texWidth /= kernelWidth;
        texHeight /= kernelWidth;
      }

      if (texWidth * texHeight > 1)
      {
        if (oddStep == true)
        {
          TexturePreprocessor.SetTexture(operationKernel, texInP, tempRT2);
          TexturePreprocessor.SetTexture(operationKernel, texOutP, tempRT1);
        }
        else
        {
          TexturePreprocessor.SetTexture(operationKernel, texInP, tempRT1);
          TexturePreprocessor.SetTexture(operationKernel, texOutP, tempRT2);
        }
        TexturePreprocessor.SetInt(kernelWidthP, texWidth);
        TexturePreprocessor.SetInt(kernelHeightP, texHeight);
        TexturePreprocessor.SetInt(texWidthP, texWidth);
        TexturePreprocessor.SetInt(texHeightP, texHeight);
        TexturePreprocessor.Dispatch(operationKernel, 1, 1, 1);
      }
    }

    /// <summary>
    /// Flips a Vector3 so that the component with the greatest magnitude is positive
    /// </summary>
    /// <param name="vecIn"></param>
    /// <returns>The original vector if the greatest magnitude component is postive, otherwise the negative of the vector</returns>
    Vector3 MaxPositive(Vector3 vecIn)
    {
      if (Mathf.Abs(vecIn.x) >= Mathf.Abs(vecIn.y) && Mathf.Abs(vecIn.x) >= Mathf.Abs(vecIn.z))
      {
        return Mathf.Sign(vecIn.x) * vecIn;
      }
      else if (Mathf.Abs(vecIn.y) >= Mathf.Abs(vecIn.z))
      {
        return Mathf.Sign(vecIn.y) * vecIn;
      }
      else
      {
        return Mathf.Sign(vecIn.z) * vecIn;
      }
    }

    /// <summary>
    /// Sorts the input eigen vectors, lengths, min, and max values by the lengths such that the largest value is the second.
    /// This is so that the longest axis is stored in the green channel, which has more bits in DXT compression. Outputs
    /// an array of indicies to swizzle the colors in the image by.
    /// </summary>
    /// <param name="eigen0">Eigen Vector 0</param>
    /// <param name="eigen1">Eigen Vector 1</param>
    /// <param name="eigen2">Eigen Vector 2</param>
    /// <param name="len0">Length of the bounding box in the direction of eigen vector 0</param>
    /// <param name="len1">Length of the bounding box in the direction of eigen vector 1</param>
    /// <param name="len2">Length of the bounding box in the direction of eigen vector 2</param>
    /// <param name="minValues">Minimum color channel values</param>
    /// <param name="maxValues">Maximum color channel values</param>
    /// <returns>A int[4] which maps the index of the original color channel to the sorted index</returns>
    int[] SortAxis(ref Vector3 eigen0, ref Vector3 eigen1, ref Vector3 eigen2, ref float len0, ref float len1, ref float len2, ref float[] minValues, ref float[] maxValues)
    {
      int[] swizzleID = new int[4] { 0, 1, 2, 3 };
      if (len2 > len0)
      {
        Vector3 tempE = eigen0;
        float tempMin = minValues[0];
        float tempMax = maxValues[0];
        float tempLen = len0;
        int tempSwizzle = swizzleID[0];

        eigen0 = eigen2;
        minValues[0] = minValues[2];
        maxValues[0] = maxValues[2];
        len0 = len2;
        swizzleID[0] = swizzleID[2];

        eigen2 = tempE;
        minValues[2] = tempMin;
        maxValues[2] = tempMax;
        len2 = tempLen;
        swizzleID[2] = tempSwizzle;
      }

      if (len0 > len1)
      {
        Vector3 tempE = eigen1;
        float tempMin = minValues[1];
        float tempMax = maxValues[1];
        float tempLen = len1;
        int tempSwizzle = swizzleID[1];

        eigen1 = eigen0;
        minValues[1] = minValues[0];
        maxValues[1] = maxValues[0];
        len1 = len0;
        swizzleID[1] = swizzleID[0];

        eigen0 = tempE;
        minValues[0] = tempMin;
        maxValues[0] = tempMax;
        len0 = tempLen;
        swizzleID[0] = tempSwizzle;
      }

      return swizzleID;
    }

    /// <summary>
    /// Transforms the RGB colors in the input Rendertexture to a space whose axes are aligned with the eigenvectors of the covariance matrix,
    /// whose center is the minimum of each channel in the new space, and which is scaled so that the range of each channel is 0 to 1.
    /// This makes it so that the values stored in the image are not correlated, so when transforming the image to the gaussian texture and
    /// then obtaining the real color via the lookup table we don't end up with colors that weren't present in the original image.
    /// </summary>
    /// <param name="TexIn">Input Rendertexture to transform, must have enableRandomWrite set</param>
    /// <param name="Axis0">First three components are one axis of the colorspace, and the fourth component is the length of the axis</param>
    /// <param name="Axis1">First three components are one axis of the colorspace, and the fourth component is the length of the axis</param>
    /// <param name="Axis2">First three components are one axis of the colorspace, and the fourth component is the length of the axis</param>
    /// <param name="ColorCenter">Center of the transformed colorspace in RGB space</param>
    void ComputeDecorrelatedColorSpace(RenderTexture TexIn, ref Vector4 Axis0, ref Vector4 Axis1, ref Vector4 Axis2, ref Vector4 ColorCenter)
    {


      /*
       * Create some small compute buffers to store the outputs of
       * several different kernels
       */
      ComputeBuffer AvgColor = new ComputeBuffer(4, 4);
      ComputeBuffer MinColor = new ComputeBuffer(4, 4);
      ComputeBuffer MaxColor = new ComputeBuffer(4, 4);
      ComputeBuffer CovDiagonalBuffer = new ComputeBuffer(4, 4);
      ComputeBuffer CovUpperBuffer = new ComputeBuffer(4, 4);

      /*
       * Create two temporary rendertextures in which to store modified versions of the texture when calculating
       * the average, min, and max of the image. This is stupidly inefficient and wastes tons of vram, but I'm too
       * lazy right now to come up with a smarter method that only uses one rendertexture.
       * 
       */
      RenderTexture tempRT1 = new RenderTexture(TexIn.width, TexIn.height, 1, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      tempRT1.enableRandomWrite = true;
      tempRT1.Create();
      RenderTexture tempRT2 = new RenderTexture(TexIn.width, TexIn.height, 1, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      tempRT2.enableRandomWrite = true;
      tempRT2.Create();

      int texWidth = tempRT1.width;
      int texHeight = tempRT1.height;

      // Copy the image into the first temp rendertexture. Don't use the input image directly as both temp images get modified by the compute shader.
      copyImage(TexIn, tempRT1);


      /*----------------------------------------------------------------------------------------------------------------------------
       * Calculate the covarience matrix of the RGB values of the image
       * ---------------------------------------------------------------------------------------------------------------------------
       */

      // Start by finding the average of each color channel in the image
      int averageKernel = TexturePreprocessor.FindKernel("AverageImage");
      ImageOperation(averageKernel, tempRT1, tempRT2, AvgColor, texWidth, texHeight);

      /* The super-diagonal elements of the covariance matrix follow the form:
       * 
       * (1/number of pixels) * sum((color1 - color1 average) * (color2 - color2 average))
       * 
       * So calculate the (red - red average) * (green - green average), (green - green average) * (blue - blue average),
       * and (red - red average) * (blue - blue average) at each pixel and store the values in the RGB channels of each pixel
       */
      int CovUpperKern = TexturePreprocessor.FindKernel("ImageCovUpper");
      TexturePreprocessor.SetInt(texWidthP, TexIn.width);
      TexturePreprocessor.SetInt(texHeightP, TexIn.width);
      TexturePreprocessor.SetTexture(CovUpperKern, texInP, TexIn);
      TexturePreprocessor.SetTexture(CovUpperKern, texOutP, tempRT1);
      TexturePreprocessor.SetBuffer(CovUpperKern, bufferAvgP, AvgColor);
      TexturePreprocessor.Dispatch(CovUpperKern, Mathf.Max(1, TexIn.width * TexIn.height / 1024), 1, 1);

      /*
       * Now average the values calculated by the previous step. This gives us almost the value we want, but rather than dividing by the
       * total number of pixels we should have divided by the total number of pixels - 1. This is easily solved by multiplying by
       * (total number of pixels) / (total number of pixels - 1)
       */
      ImageOperation(averageKernel, tempRT1, tempRT2, CovUpperBuffer, texWidth, texHeight);
      float[] covUpperArray = new float[4];

      CovUpperBuffer.GetData(covUpperArray);

      for (int el = 0; el < 3; el++)
      {
        covUpperArray[el] *= (float)((double)TexIn.width / (double)TexIn.width + 1.0);
      }

      /* Calculate the diagonal elements of the covariance matrix. These follow the form:
       * (1/number of pixels) * sum( (color - average color)^2 ) 
       */
      int CovDiagonalKern = TexturePreprocessor.FindKernel("ImageCovDiagonal");
      TexturePreprocessor.SetInt(texWidthP, TexIn.width);
      TexturePreprocessor.SetInt(texHeightP, TexIn.width);
      TexturePreprocessor.SetTexture(CovDiagonalKern, texInP, TexIn);
      TexturePreprocessor.SetTexture(CovDiagonalKern, texOutP, tempRT1);
      TexturePreprocessor.SetBuffer(CovDiagonalKern, bufferAvgP, AvgColor);
      TexturePreprocessor.Dispatch(CovDiagonalKern, Mathf.Max(1, TexIn.width * TexIn.height / 1024), 1, 1);
      ImageOperation(averageKernel, tempRT1, tempRT2, CovDiagonalBuffer, texWidth, texHeight);
      float[] covDiagArray = new float[4];
      CovDiagonalBuffer.GetData(covDiagArray);
      // We divided by N instead of N-1 when we averaged, so multiply by N/(N-1)
      for (int el = 0; el < 3; el++)
      {
        covDiagArray[el] *= (TexIn.width / TexIn.width + 1.0f);
      }

      // Assemble the covariance matrix from the calculated elements
      Matrix3x3f covarianceMatrix = new Matrix3x3f(
        covDiagArray[0], covUpperArray[0], covUpperArray[2],
        covUpperArray[0], covDiagArray[1], covUpperArray[1],
        covUpperArray[2], covUpperArray[1], covDiagArray[2]
        );

      /*-----------------------------------------------------------------------------------------------------------------------------
       * Calculate the eigen vectors of the covariance matrix.
       * These will become the new basis vectors of our new colorspace
       * ----------------------------------------------------------------------------------------------------------------------------
       */
      Vector3 eigenValues = new Vector3(0, 0, 0);
      Matrix3x3f eigenVectors = EigenDecomposition.Get3x3SymmetricEigen(covarianceMatrix, ref eigenValues);



      // Debug code to make sure the eigen decomposition method is actually generating real eigenvectors
      /* 
      Vector3 product1 = new Vector3(Colorspace.m00 * eigenVectors.m00 + Colorspace.m01 * eigenVectors.m10 + Colorspace.m02 * eigenVectors.m20,
                                     Colorspace.m10 * eigenVectors.m00 + Colorspace.m11 * eigenVectors.m10 + Colorspace.m12 * eigenVectors.m20,
                                     Colorspace.m20 * eigenVectors.m00 + Colorspace.m21 * eigenVectors.m10 + Colorspace.m22 * eigenVectors.m20);
      Vector3 product2 = eigenvalues.x * eigenVectors.GetColumnVector(0);
      Debug.Log(string.Format("{0}\t{1}\t{2}\n{3}\t{4}\t{5}\n{6}\t{7}\t{8}",
        eigenVectors.m00, eigenVectors.m01, eigenVectors.m02,
        eigenVectors.m10, eigenVectors.m11, eigenVectors.m12,
        eigenVectors.m20, eigenVectors.m21, eigenVectors.m22));
      Debug.Log(string.Format("Eigen Values: {0}, {1}, {2}", eigenvalues.x, eigenvalues.y, eigenvalues.z));
      Debug.Log(string.Format("A*E: {0},\t v*E: {1}", product1.ToString(), product2.ToString()));
      */




      /* Split the matrix into three separate vector3's containing the eigen vectors.
       * Normalize the vectors and flip them so the greatest magnitude component is positive 
       */
      Vector3 eigen0 = eigenVectors.GetColumnVector(0);
      Vector3 eigen1 = eigenVectors.GetColumnVector(1);
      Vector3 eigen2 = eigenVectors.GetColumnVector(2);
      eigen0 = Vector3.Normalize(eigen0);
      eigen1 = Vector3.Normalize(eigen1);
      eigen2 = Vector3.Normalize(eigen2);
      eigen0 = MaxPositive(eigen0);
      eigen1 = MaxPositive(eigen1);
      eigen2 = MaxPositive(eigen2);

      //Transform the colors of the input image to the new colorspace with the eigenvectors as the basis vectors

      int toEigenKern = TexturePreprocessor.FindKernel("TransformColorsToEigenSpace");

      TexturePreprocessor.SetFloats(eigenVector1P, new float[4] { eigen0.x, eigen0.y, eigen0.z, 0.0f });
      TexturePreprocessor.SetFloats(eigenVector2P, new float[4] { eigen1.x, eigen1.y, eigen1.z, 0.0f });
      TexturePreprocessor.SetFloats(eigenVector3P, new float[4] { eigen2.x, eigen2.y, eigen2.z, 0.0f });
      TexturePreprocessor.SetInt(texWidthP, TexIn.width);
      TexturePreprocessor.SetTexture(toEigenKern, texOutP, TexIn);
      TexturePreprocessor.Dispatch(toEigenKern, Mathf.Max(1, TexIn.width * TexIn.height / 1024), 1, 1);

      // Find the minimum values of the image in the new colorspace

      copyImage(TexIn, tempRT1);
      int minKern = TexturePreprocessor.FindKernel("MinImage");
      ImageOperation(minKern, tempRT1, tempRT2, MinColor, texWidth, texHeight);
      float[] minColors = new float[4];
      MinColor.GetData(minColors);

      // Find the maximum values of the image in the new colorspace

      copyImage(TexIn, tempRT1);
      int maxKern = TexturePreprocessor.FindKernel("MaxImage");
      ImageOperation(maxKern, tempRT1, tempRT2, MaxColor, texWidth, texHeight);
      float[] maxColors = new float[4];
      MaxColor.GetData(maxColors);

      // Compute the dimensions of the bounding box that encapsulates all values in the image

      float length0 = maxColors[0] - minColors[0];
      float length1 = maxColors[1] - minColors[1];
      float length2 = maxColors[2] - minColors[2];

      // Reorder the values in the color channels so that the channel with the greatest range of values is stored in the green channel
      
      int[] SwizzleMask = SortAxis(ref eigen0, ref eigen1, ref eigen2, ref length0, ref length1, ref length2, ref minColors, ref maxColors);
      int swizzleKern = TexturePreprocessor.FindKernel("SwizzleColors");
      TexturePreprocessor.SetInt(texWidthP, TexIn.width);
      TexturePreprocessor.SetTexture(swizzleKern, texOutP, TexIn);
      TexturePreprocessor.SetInts(swizzleMaskP, SwizzleMask);
      TexturePreprocessor.Dispatch(swizzleKern, Mathf.Max(1, TexIn.width * TexIn.height / 1024), 1, 1);
      Debug.Log(string.Format("Swizzle Mask: {0}, {1}, {2}, {3}", SwizzleMask[0], SwizzleMask[1], SwizzleMask[2], SwizzleMask[3]));
      
      /* Modify the new colorspace so it is centered on the minimum value of each channel and scale the basis vectors to be the same lengths as
       * the bounding box dimensions. This makes it so that the values in each channel range from 0 to 1
       */

      Vector3 center = minColors[0] * eigen0 + minColors[1] * eigen1 + minColors[2] * eigen2;

      int toBoundsKern = TexturePreprocessor.FindKernel("TransformColorsToBoundingBox");
      TexturePreprocessor.SetFloats(minColorP, minColors);
      TexturePreprocessor.SetFloats(maxColorP, maxColors);
      TexturePreprocessor.SetInt(texWidthP, TexIn.width);
      TexturePreprocessor.SetTexture(toBoundsKern, texOutP, TexIn);
      TexturePreprocessor.Dispatch(toBoundsKern, Mathf.Max(1, TexIn.width * TexIn.height / 1024), 1, 1);

      

      Axis0 = new Vector4(eigen0.x * length0, eigen0.y * length0, eigen0.z * length0, Mathf.Min(1.0f / length0, 10));
      Axis1 = new Vector4(eigen1.x * length1, eigen1.y * length1, eigen1.z * length1, Mathf.Min(1.0f / length1, 10));
      Axis2 = new Vector4(eigen2.x * length2, eigen2.y * length2, eigen2.z * length2, Mathf.Min(1.0f / length2, 10));
      ColorCenter = new Vector4(center.x, center.y, center.z, 0);

      /*
      Debug.Log(string.Format("Eigenvector 1: {0:0.#########E+00}, {1:0.#########E+00}, {2:0.#########E+00}", eigen0.x, eigen0.y, eigen0.z));
      Debug.Log(string.Format("Eigenvector 2: {0:0.#########E+00}, {1:0.#########E+00}, {2:0.#########E+00}", eigen1.x, eigen1.y, eigen1.z));
      Debug.Log(string.Format("Eigenvector 3: {0:0.#########E+00}, {1:0.#########E+00}, {2:0.#########E+00}", eigen2.x, eigen2.y, eigen2.z));
      Debug.Log(string.Format("Eigen Values: {0}, {1}, {2}", eigenValues.x, eigenValues.y, eigenValues.z));
      Debug.Log(string.Format("Axis Lengths: {0}, {1}, {2}", length0, length1, length2));
      Debug.Log(string.Format("Min Colors: {0}, {1}, {2}", minColors[0], minColors[1], minColors[2]));
      Debug.Log(string.Format("Max Colors: {0}, {1}, {2}", maxColors[0], maxColors[1], maxColors[2]));
      Debug.Log(string.Format("Max Colors: {0}, {1}, {2}", maxColors[0], maxColors[1], maxColors[2]));
      */
      tempRT1.Release();
      tempRT2.Release();
      AvgColor.Release();
      MinColor.Release();
      MaxColor.Release();
      CovDiagonalBuffer.Release();
      CovUpperBuffer.Release();
    }


    void SortBuffer(ComputeBuffer colorBuffer, ComputeBuffer indexBuffer, int texWidth, int texHeight)
    {
      int bitonicLocalFullKern = BitonicSort.FindKernel("LocalFullSortKernel");
      BitonicSort.SetBuffer(bitonicLocalFullKern, colorP, colorBuffer);
      BitonicSort.SetBuffer(bitonicLocalFullKern, indexP, indexBuffer);
      int bitonicLocalShearKern = BitonicSort.FindKernel("LocalShearKernel");
      BitonicSort.SetBuffer(bitonicLocalShearKern, colorP, colorBuffer);
      BitonicSort.SetBuffer(bitonicLocalShearKern, indexP, indexBuffer);
      int bitonicGlobalMirrorKern = BitonicSort.FindKernel("GlobalMirrorKernel");
      BitonicSort.SetBuffer(bitonicGlobalMirrorKern, colorP, colorBuffer);
      BitonicSort.SetBuffer(bitonicGlobalMirrorKern, indexP, indexBuffer);
      int bitonicGlobalShearKern = BitonicSort.FindKernel("GlobalShearKernel");
      BitonicSort.SetBuffer(bitonicGlobalShearKern, colorP, colorBuffer);
      BitonicSort.SetBuffer(bitonicGlobalShearKern, indexP, indexBuffer);

      BitonicSort.SetInt(texWidthP, texWidth);
      BitonicSort.SetInt(texHeightP, texHeight);

      int numElements = texWidth * texHeight;
      int numGroups = Mathf.Max(1, numElements / 1024);

      BitonicSort.Dispatch(bitonicLocalFullKern, numGroups, 1, 1);

      // we sorted groups of 1024 pixels in the last step which is the most you can do in the thread group's shared memory.

      int heightPower = 11; // 1024 = 2^10;
                            //uint height = 2048;
      for (uint height = 2048; height <= numElements; height *= 2)
      {
        BitonicSort.SetInt(heightPowerP, heightPower);
        BitonicSort.Dispatch(bitonicGlobalMirrorKern, numGroups, 1, 1);

        int height2Power = heightPower - 1;
        //uint height2 = height / 2;
        for (uint height2 = height / 2; height2 >= 2; height2 /= 2)
        {
          BitonicSort.SetInt(heightPowerP, height2Power);
          if (height2 <= 1024)
          {
            BitonicSort.Dispatch(bitonicLocalShearKern, numGroups, 1, 1);
            break;
          }
          else
          {
            BitonicSort.Dispatch(bitonicGlobalShearKern, numGroups, 1, 1);
          }
          height2Power -= 1;
        }

        heightPower += 1;
      }
    }

    
    private void averageBlock(RenderTexture temp1, RenderTexture temp2, ComputeBuffer averageBuffer,
      int kernel, int texWidth, int texHeight, int kernelWidth, int blockWidth, ref bool oddStep)
    {
      TexturePreprocessor.SetInt(kernelWidthP, kernelWidth);
      TexturePreprocessor.SetInt(kernelHeightP, kernelWidth);
      TexturePreprocessor.SetBuffer(kernel, bufferAvgP, averageBuffer);

      int i = blockWidth;
      int j = 0;
      
      while (i >= kernelWidth)
      {
        j++;
        if (oddStep == true)
        {
          TexturePreprocessor.SetTexture(kernel, texInP, temp2);
          TexturePreprocessor.SetTexture(kernel, texOutP, temp1);
        }
        else
        {
          TexturePreprocessor.SetTexture(kernel, texInP, temp1);
          TexturePreprocessor.SetTexture(kernel, texOutP, temp2);
        }

        TexturePreprocessor.SetInt(texWidthP, texWidth);
        TexturePreprocessor.SetInt(texHeightP, texHeight);
        TexturePreprocessor.Dispatch(kernel, Mathf.Max(texWidth * texHeight / 1024, 1), 1, 1);

        oddStep = !oddStep;
        texWidth /= kernelWidth;
        texHeight /= kernelWidth;
        i /= kernelWidth;
        if (i < kernelWidth && i != 1)
        {
          kernelWidth = i;
          TexturePreprocessor.SetInt(kernelWidthP, kernelWidth);
          TexturePreprocessor.SetInt(kernelHeightP, kernelWidth);
        }
      }
      //Debug.Log("Ran block average for: " + j.ToString());
    }

    /// <summary>
    /// Calculates the average variance of 2^mipLevel blocks of pixels of the input image
    /// </summary>
    /// <param name="TexIn">Input texture to calculate the variance of</param>
    /// <param name="temp1">Temporary rendertexture, should be the same dimensions as TexIn and have random write enabled</param>
    /// <param name="temp2">Temporary rendertexture, should be at least half the dimensions of TexIn and have random write enabled</param>
    /// <param name="temp3">Temporary rendertexture, should be at least half the dimensions of TexIn and have random write enabled</param>
    /// <param name="avgOut">Compute buffer of size 4,4 into which the variance array will be output from the compute shaders</param>
    /// <param name="mipLevel">Mip level to calculate the variance of</param>
    /// <param name="varKernRG">Integer ID of the kernel "PopulateRGVariance"</param>
    /// <param name="varKernBA">Integer ID of the kernel "PopulateBAVariance"</param>
    /// <param name="varKern">Integer ID of the kernel "CalculateVariance"</param>
    /// <param name="avgKern">Integer ID of the kernel "AverageImage"</param>
    /// <returns>Vector4 containing the average variance of the R, G, B, and A channels</returns>
    private Vector4 CalculateMipVariance(Texture TexIn, RenderTexture temp1, RenderTexture temp2, RenderTexture temp3,
      ComputeBuffer avgOut, int mipLevel, int varKernRG, int varKernBA, int varKern, int avgKern)
    {
      int texWidth = TexIn.width;
      int texHeight = TexIn.height;
      int mipWidth = texWidth >> mipLevel;
      int mipHeight = texHeight >> mipLevel;


      float[] rgVarArray = new float[4];
      float[] baVarArray = new float[4];
      float[] varArray = new float[4];

      TexturePreprocessor.SetInt(texWidthP, texWidth);
      TexturePreprocessor.SetInt(texHeightP, texHeight);
      TexturePreprocessor.SetTexture(varKernRG, texInP, TexIn);
      TexturePreprocessor.SetTexture(varKernRG, texOutP, temp1);
      TexturePreprocessor.Dispatch(varKernRG, Mathf.Max(1, texWidth / 32), Mathf.Max(1, texHeight / 32), 1);
      int blockWidth = 1 << mipLevel;

      int kernelWidth = mipLevel > 1 ? 4 : 2;
      bool oddStep = false;

      averageBlock(temp1, temp2, avgOut, avgKern, texWidth, texHeight, kernelWidth, blockWidth, ref oddStep);

      if (!oddStep)
      {
        copyImage(temp1, temp2);
      }

      if (mipWidth == 1 && mipHeight == 1)
      {
        avgOut.GetData(rgVarArray);
      }

      TexturePreprocessor.SetInt(texWidthP, texWidth);
      TexturePreprocessor.SetInt(texHeightP, texHeight);
      TexturePreprocessor.SetTexture(varKernBA, texInP, TexIn);
      TexturePreprocessor.SetTexture(varKernBA, texOutP, temp1);
      TexturePreprocessor.Dispatch(varKernBA, Mathf.Max(1, texWidth / 32), Mathf.Max(1, texHeight / 32), 1);


      kernelWidth = mipLevel > 1 ? 4 : 2;
      oddStep = false;

      averageBlock(temp1, temp3, avgOut, avgKern, texWidth, texHeight, kernelWidth, blockWidth, ref oddStep);

      if (!oddStep)
      {
        copyImage(temp1, temp3);
      }

      if (mipWidth == 1 && mipHeight == 1)
      {
        avgOut.GetData(baVarArray);
      }

      if (mipWidth == 1 && mipHeight == 1)
      {
        varArray[0] = Mathf.Max(0.0f, rgVarArray[1] - rgVarArray[0] * rgVarArray[0]);
        varArray[1] = Mathf.Max(0.0f, rgVarArray[3] - rgVarArray[2] * rgVarArray[2]);
        varArray[2] = Mathf.Max(0.0f, baVarArray[1] - baVarArray[0] * baVarArray[0]);
        varArray[3] = Mathf.Max(0.0f, baVarArray[3] - baVarArray[2] * baVarArray[2]);
      }
      else
      {
        TexturePreprocessor.SetInt(texWidthP, mipWidth);
        TexturePreprocessor.SetInt(texHeightP, mipHeight);
        TexturePreprocessor.SetTexture(varKern, texInP, temp2);
        TexturePreprocessor.SetTexture(varKern, texIn2P, temp3);
        TexturePreprocessor.SetTexture(varKern, texOutP, temp1);
        TexturePreprocessor.Dispatch(varKern, Mathf.Max(1, mipWidth / 32), Mathf.Max(1, mipHeight / 32), 1);
        ImageOperation(avgKern, temp1, temp2, avgOut, mipWidth, mipHeight);
        avgOut.GetData(varArray);
      }
      return new Vector4(varArray[0], varArray[1], varArray[2], varArray[3]);
    }


    
    private void CreateFilteredLUTLevel(RenderTexture LUT, RenderTexture temp1, RenderTexture temp2, int mip, int lutWidth, int lutHeight, Vector4 stdDev)
    {
      int populateKern = CumulativeDistribution.FindKernel("PopulateLUTMipFilter");
      int averageKern = CumulativeDistribution.FindKernel("AverageLUTMipFilter");

      int texHeight = lutWidth * lutHeight;
      int texWidth = lutWidth * lutHeight * 2;
      int kernelWidth = 16;

      CumulativeDistribution.SetFloats(mipStdP, new float[4] { stdDev.x, stdDev.y, stdDev.z, stdDev.w });

      CumulativeDistribution.SetInt(lutWidthP, lutWidth);
      CumulativeDistribution.SetInt(lutHeightP, lutHeight);
      CumulativeDistribution.SetInt(mipLevelP, mip);
      CumulativeDistribution.SetTexture(populateKern, texOutP, temp1);
      CumulativeDistribution.SetTexture(populateKern, lutP, LUT);
      CumulativeDistribution.Dispatch(populateKern, Mathf.Max(1, texWidth / 32), Mathf.Max(1, texHeight / 32), 1);

      CumulativeDistribution.SetInt(kernelWidthP, kernelWidth);
      CumulativeDistribution.SetTexture(averageKern, lutP, LUT);
      CumulativeDistribution.SetInt(texHeightP, texHeight);

      int currentWidth = texWidth;
      bool oddStep = false;
      for ( int i = texWidth; i >= kernelWidth; i /= kernelWidth)
      {
        CumulativeDistribution.SetInt(texWidthP, currentWidth);
       
        if (oddStep)
        {
          CumulativeDistribution.SetTexture(averageKern, texInP, temp2);
          CumulativeDistribution.SetTexture(averageKern, texOutP, temp1);
        }
        else
        {
          CumulativeDistribution.SetTexture(averageKern, texInP, temp1);
          CumulativeDistribution.SetTexture(averageKern, texOutP, temp2);
        }

        CumulativeDistribution.Dispatch(averageKern, Mathf.Max(1, currentWidth / 32), Mathf.Max(1, texHeight / 32), 1);
        oddStep = !oddStep;
        currentWidth /= kernelWidth;
      }

      if (currentWidth > 1)
      {
        CumulativeDistribution.SetInt(kernelWidthP, currentWidth);
        CumulativeDistribution.SetInt(texWidthP, currentWidth);

        if (oddStep)
        {
          CumulativeDistribution.SetTexture(averageKern, texInP, temp2);
          CumulativeDistribution.SetTexture(averageKern, texOutP, temp1);
        }
        else
        {
          CumulativeDistribution.SetTexture(averageKern, texInP, temp1);
          CumulativeDistribution.SetTexture(averageKern, texOutP, temp2);
        }

        CumulativeDistribution.Dispatch(averageKern, Mathf.Max(1, currentWidth / 32), Mathf.Max(1, texHeight / 32), 1);
      }
    }
    
    private void FilterLUT(RenderTexture TexIn, RenderTexture LUT)
    {
      int texWidth = TexIn.width;
      int texHeight = TexIn.height;
      int mipLevels = Mathf.RoundToInt(Mathf.Log(Mathf.Min(texWidth, texHeight), 2)) + 1;

      int varKernRG = TexturePreprocessor.FindKernel("PopulateRGVariance");
      int varKernBA = TexturePreprocessor.FindKernel("PopulateBAVariance");
      int varKern = TexturePreprocessor.FindKernel("CalculateVariance");
      int avgKern = TexturePreprocessor.FindKernel("AverageImage");

      RenderTexture temp1 = new RenderTexture(texWidth, texHeight, 1, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      temp1.enableRandomWrite = true;
      temp1.Create();
      RenderTexture temp2 = new RenderTexture(texWidth >> 1, texHeight >> 1, 1, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      temp2.enableRandomWrite = true;
      temp2.Create();
      RenderTexture temp3 = new RenderTexture(texWidth >> 1, texHeight >> 1, 1, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      temp3.enableRandomWrite = true;
      temp3.Create();

      ComputeBuffer avgOut = new ComputeBuffer(4, 4);

      int lutElements = LUT.width * LUT.height;
      RenderTexture tempLUT1 = new RenderTexture(lutElements << 1, lutElements, 1, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      tempLUT1.enableRandomWrite = true;
      tempLUT1.Create();
      RenderTexture tempLUT2 = new RenderTexture(lutElements, lutElements, 1, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      tempLUT2.enableRandomWrite = true;
      tempLUT2.Create();


      for (int i = 1; i <= mipLevels; i++)
      {
        Vector4 variance = CalculateMipVariance(TexIn, temp1, temp2, temp3, avgOut, i, varKernRG, varKernBA, varKern, avgKern);
        variance = new Vector4(Mathf.Sqrt(variance.x), Mathf.Sqrt(variance.y), Mathf.Sqrt(variance.z), Mathf.Sqrt(variance.w));
        CreateFilteredLUTLevel(LUT, tempLUT1, tempLUT2, i, LUT.width, LUT.height, variance);
      }


      /* 
       UnityEngine.Profiling.Profiler.BeginSample("CPU Variant");
       Vector4 variance2 = ComputeLODAverageSubpixelVariance(TexIn, 2);
       UnityEngine.Profiling.Profiler.EndSample();

       Debug.Log(string.Format("{0}, {1}, {2}, {3}", variance.x, variance.y, variance.z, variance.w));
       Debug.Log(string.Format("{0}, {1}, {2}, {3}", variance2.x, variance2.y, variance2.z, variance2.w));
       */

      avgOut.Release();
      temp1.Release();
      temp2.Release();
      temp3.Release();
      tempLUT1.Release();
      tempLUT2.Release();
    }
    

    private void CopyLUTSliceTo2D(RenderTexture LUT, RenderTexture Slice2D, int mipLevel)
    {
      int sliceKern = CumulativeDistribution.FindKernel("CopyLUTSliceTo2D");
      CumulativeDistribution.SetInt(mipLevelP, mipLevel);
      CumulativeDistribution.SetTexture(sliceKern, lutP, LUT);
      CumulativeDistribution.SetTexture(sliceKern, texOutP, Slice2D);
      CumulativeDistribution.Dispatch(sliceKern, 1, 1, 1);
    }

    private bool NoAlphaFormat(TextureFormat format)
    {
      return format == TextureFormat.DXT1 || format == TextureFormat.DXT1Crunched || format == TextureFormat.RGB24 ||
        format == TextureFormat.R8 || format == TextureFormat.R16 || format == TextureFormat.RFloat || format == TextureFormat.RG16 ||
        format == TextureFormat.RG32 || format == TextureFormat.RGB48 || format == TextureFormat.RGB565 || format == TextureFormat.RGB9e5Float ||
        format == TextureFormat.RGFloat || format == TextureFormat.RGHalf || format == TextureFormat.YUY2 ||
        format == TextureFormat.BC4 || format == TextureFormat.BC5 || 
        format == TextureFormat.EAC_R || format == TextureFormat.EAC_RG || format == TextureFormat.EAC_RG_SIGNED ||
        format == TextureFormat.EAC_R_SIGNED || format == TextureFormat.ETC2_RGB || format == TextureFormat.ETC_RGB4 ||
        format == TextureFormat.ETC_RGB4Crunched || format == TextureFormat.ASTC_RGB_10x10 || format == TextureFormat.ASTC_RGB_12x12 ||
        format == TextureFormat.ASTC_RGB_4x4 || format == TextureFormat.ASTC_RGB_5x5 || format == TextureFormat.ASTC_RGB_6x6 ||
        format == TextureFormat.ASTC_RGB_8x8;
    }

    #endregion

    #region PUBLIC_METHODS

    /// <summary>
    /// Copies colorspace settings from a given colorspace object to a specified material
    /// </summary>
    /// <param name="mat">Material to copy the settings to</param>
    /// <param name="colorSettings">Colorspace object to copy the settings from</param>
    /// <param name="axis0Prop">Property name in the material corresponding to the 1st axis of the colorspace</param>
    /// <param name="axis1Prop">Property name in the material corresponding to the 2nd axis of the colorspace</param>
    /// <param name="axis2Prop">Property name in the material corresponding to the 3nd axis of the colorspace</param>
    /// <param name="centerProp">Property name in the material corresponding to the center of the colorspace</param>
    public static void CopyColorspaceToMat(Material mat, ColorspaceObj colorSettings, string axis0Prop, string axis1Prop, string axis2Prop, string centerProp)
    {
      mat.SetVector(axis0Prop, colorSettings.Axis0);
      mat.SetVector(axis1Prop, colorSettings.Axis1);
      mat.SetVector(axis2Prop, colorSettings.Axis2);
      mat.SetVector(centerProp, colorSettings.Center);
    }

    /// <summary>
    /// For a given FileType enum, returns a string containing the corresponding file extension string
    /// </summary>
    /// <param name="fileT">FileType enum to get the extension of</param>
    /// <returns>A string containing the file type's file extension</returns>
    public static string FileTypeToString(FileType fileT)
    {
      switch (fileT)
      {
        case FileType.png:
          return ".png";
        case FileType.jpg:
          return ".jpg";
        case FileType.tga:
         return ".tga";
        case FileType.exr:
          return ".exr";
        default:
          return ".png";
      }
    }


    /// <summary>
    /// Finds the compute shader dependencies and assigns them by using the ability for ScriptableObjects to locate their source
    /// script file location. Assuming the user hasn't messed with the file structure, the Shaders folder containing all the compute
    /// shaders is located next to the scripts folder holding this script.
    /// </summary>
    /// <returns>0 if all shaders were located successfully, 1 otherwise</returns>
    public int AssignComputeShaders()
    {
      Object ThisScript = MonoScript.FromScriptableObject(this);
      string scriptPath = AssetDatabase.GetAssetPath(ThisScript);
      string directory = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)), "Shaders");
      TexturePreprocessor = AssetDatabase.LoadAssetAtPath<ComputeShader>(directory + "\\TexturePreprocessor.compute");
      BitonicSort = AssetDatabase.LoadAssetAtPath<ComputeShader>(directory + "\\BitonicMergeSort.compute");
      CumulativeDistribution = AssetDatabase.LoadAssetAtPath<ComputeShader>(directory + "\\SortedTextureToGaussian.compute");
      if (TexturePreprocessor == null || BitonicSort == null || CumulativeDistribution == null)
      {
        return 1;
      }
      else
      {
        return 0;
      }
    }

    /// <summary>
    /// Creates a gaussian-transformed texture and lookup table from an input image
    /// </summary>
    /// <param name="InputTex">Texture to transform into a gaussian texture and lookup table</param>
    /// <param name="LUTWidthPow2">Power of 2 of the width of the lookup table, clamped to 1 to 5</param>
    /// <param name="LUTHeightPow2">Power of 2 of the height of the lookup table, clamped to 1 to 5</param>
    /// <param name="CompressionCorrection">Whether or not to scale the colors by the colorspace axis lengths to try to get better DXT compression</param>
    /// <param name="DecorrelateColorspace">Whether or not to transform the textures colors to a space where the color channels aren't correlated. Prevents false colors from appearing in some images but may reduce compression quality</param>
    /// <param name="outputImageType">File type to save the gaussian texture as</param>
    /// <param name="outputImageDir">Output directory to save the gaussian image to. If not specified, saves the image next to the input image. Files are given the input's name plus an extension like "_gauss"</param>
    public void CreateGaussianTexture(Texture2D InputTex, int LUTWidthPow2, int LUTHeightPow2, bool CompressionCorrection = false, bool DecorrelateColorspace = true, FileType outputImageType = FileType.png, string outputImageDir = null)
    {

      int texWidth = InputTex.width;
      int texHeight = InputTex.height;

      int LUTWidth = 1 << Mathf.Clamp(LUTWidthPow2, 1, 5);
      int LUTHeight = 1 << Mathf.Clamp(LUTHeightPow2, 1, 5);
      int LUTDepth = Mathf.RoundToInt(Mathf.Log(Mathf.Min(texWidth, texHeight), 2)) + 1;
      Debug.Log(string.Format("Mip Levels: {0}", LUTDepth));
      /* First check if the input texture has power of 2 dimensions. The gpu bitonic merge sort
       * algorithm I'm using only works with power of 2 data sets, so we can't convert NPOT images
       */
      uint testWidth = (uint)InputTex.width;
      uint testHeight = (uint)InputTex.height;
      if ( (testWidth & (testWidth - 1u)) !=0  || (testHeight & (testHeight - 1u)) != 0 )
      {
        Debug.LogError(string.Format("Cannot convert textures with non-power of 2 dimensions, input texture is {0} by {1}", testWidth, testHeight));
        return;
      }

      /*
       * Figure out if our image has an alpha channel
       */
      bool NoAlpha = NoAlphaFormat(InputTex.format);

      /* Create a temporary Rendertexture to store our image in during the conversion process, with random writes enabled
       * so compute shaders can modify it. Also create a rendertexture to hold the lookup table
       */
      RenderTexture tempOutRT = new RenderTexture(texWidth, texHeight, 1, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      tempOutRT.enableRandomWrite = true;
      tempOutRT.Create();
      RenderTexture tempLUTRT = new RenderTexture(LUTWidth, LUTHeight, LUTDepth, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      tempLUTRT.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
      tempLUTRT.volumeDepth = LUTDepth;
      tempLUTRT.enableRandomWrite = true;
      tempLUTRT.Create();
      tempLUTRT.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
      tempLUTRT.volumeDepth = LUTDepth;
      RenderTexture tempLUT2D = new RenderTexture(LUTWidth, LUTHeight, 1, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
      tempLUT2D.enableRandomWrite = true;
      tempLUT2D.Create();


      //Graphics.Blit(InputTex, tempOutRT);
      copyImage(InputTex, tempOutRT);

      /* Transform the colors in the image to multiples of the Eigenvectors of the image's covarience matrix.
       * Essentially makes it so the values stored in the image aren't correlated with each other, so upon 
       * transforming back to real colors via the lookup table we don't get colors that weren't present in 
       * the original image. May not be necessary and cause worse color precision in some cases, so it can
       * be skipped by passing DecorrelateColorspace as false during the method call
       */

      Vector4 axis0 = new Vector4(1, 0, 0, 1);
      Vector4 axis1 = new Vector4(0, 1, 0, 1);
      Vector4 axis2 = new Vector4(0, 0, 1, 1);
      Vector4 colorCenter = new Vector4(0, 0, 0, 0);

      if (DecorrelateColorspace)
      {
        ComputeDecorrelatedColorSpace(tempOutRT, ref axis0, ref axis1, ref axis2, ref colorCenter);
      }
     
      if (!CompressionCorrection)
      {
        axis0.w = 1;
        axis1.w = 1;
        axis2.w = 1;
      }
      //Graphics.Blit(InputTex, tempOutRT);

      //OutputRT.enableRandomWrite = true;
      //OutputRT.width = texWidth;
      //OutputRT.height = texHeight;

      ComputeBuffer _r = new ComputeBuffer(texWidth * texHeight, 4);
      ComputeBuffer _g = new ComputeBuffer(texWidth * texHeight, 4);
      ComputeBuffer _b = new ComputeBuffer(texWidth * texHeight, 4);
      ComputeBuffer _a = new ComputeBuffer(texWidth * texHeight, 4);
      ComputeBuffer _rI = new ComputeBuffer(texWidth * texHeight, 4);
      ComputeBuffer _gI = new ComputeBuffer(texWidth * texHeight, 4);
      ComputeBuffer _bI = new ComputeBuffer(texWidth * texHeight, 4);
      ComputeBuffer _aI = new ComputeBuffer(texWidth * texHeight, 4);


      /* Split the image colors into four buffers so each can be sorted separately.
       * Also generate four extra buffers corresponding to each color channel that
       * contain the flattened pixel coordinates, which will be used to keep track
       * of the original coordinates of each pixel after sorting.
       * 
       * Technically, you could sort all 4 color channels simultaneously, but with the
       * method I'm using there would not be enough groupshared memory to store all the
       * colors and all four index buffers
       */
      int spKern = TexturePreprocessor.FindKernel("SplitTextureAndIndex");
      TexturePreprocessor.SetTexture(spKern, texInP, tempOutRT);
      TexturePreprocessor.SetBuffer(spKern, redP, _r);
      TexturePreprocessor.SetBuffer(spKern, greenP, _g);
      TexturePreprocessor.SetBuffer(spKern, blueP, _b);
      TexturePreprocessor.SetBuffer(spKern, alphaP, _a);
      TexturePreprocessor.SetBuffer(spKern, indexRP, _rI);
      TexturePreprocessor.SetBuffer(spKern, indexGP, _gI);
      TexturePreprocessor.SetBuffer(spKern, indexBP, _bI);
      TexturePreprocessor.SetBuffer(spKern, indexAP, _aI);
      TexturePreprocessor.SetInt(mipP, 0);
      TexturePreprocessor.SetInt(texWidthP, texWidth);
      TexturePreprocessor.SetInt(texHeightP, texHeight);
      TexturePreprocessor.Dispatch(spKern, texWidth * texHeight / 1024, 1, 1);

      //Debug.Log("Successfully Split Texture");

      /*
       * Sort the RGB channels using bitonic merge sorting. Thanks to @tgfrerer for the tutorial at
       * https://poniesandlight.co.uk/reflect/bitonic_merge_sort/
       */

      SortBuffer(_r, _rI, texWidth, texHeight);
      SortBuffer(_g, _gI, texWidth, texHeight);
      SortBuffer(_b, _bI, texWidth, texHeight);
      if (!NoAlpha)
      {
        SortBuffer(_a, _aI, texWidth, texHeight);
      }

      /*
       * Create the gaussian texture by calculating the inverse CDF of each pixel's index
       * in the sorted texture for each color channel, and then save the value to the pixel's
       * original position as saved in the index buffers
       * 
       */

      int numElements = texWidth * texHeight;
      int numGroupsInvCDF = Mathf.Max(1, numElements / 1024);

      int InvCDFKernel = CumulativeDistribution.FindKernel("CreateInvCDFTexture");

      CumulativeDistribution.SetInt(texWidthP, texWidth);
      CumulativeDistribution.SetInt(texHeightP, texHeight);

      CumulativeDistribution.SetTexture(InvCDFKernel, texOutP, tempOutRT);

      CumulativeDistribution.SetBuffer(InvCDFKernel, indexP, _rI);
      CumulativeDistribution.SetVector(colorMaskP, new Vector4(1, 0, 0, 0));
      CumulativeDistribution.Dispatch(InvCDFKernel, numGroupsInvCDF, 1, 1);


      CumulativeDistribution.SetBuffer(InvCDFKernel, indexP, _gI);
      CumulativeDistribution.SetVector(colorMaskP, new Vector4(0, 1, 0, 0));
      CumulativeDistribution.Dispatch(InvCDFKernel, numGroupsInvCDF, 1, 1);


      CumulativeDistribution.SetBuffer(InvCDFKernel, indexP, _bI);
      CumulativeDistribution.SetVector(colorMaskP, new Vector4(0, 0, 1, 0));
      CumulativeDistribution.Dispatch(InvCDFKernel, numGroupsInvCDF, 1, 1);

      if (!NoAlpha)
      {
        CumulativeDistribution.SetBuffer(InvCDFKernel, indexP, _aI);
        CumulativeDistribution.SetVector(colorMaskP, new Vector4(0, 0, 0, 1));
        CumulativeDistribution.Dispatch(InvCDFKernel, numGroupsInvCDF, 1, 1);
      }


      /*
       * Create the Lookup Table by calculating the CDF
       * 
       */

      int LUTKernel = CumulativeDistribution.FindKernel("CreateLookupTable");
      CumulativeDistribution.SetTexture(LUTKernel, lutP, tempLUTRT);
      CumulativeDistribution.SetBuffer(LUTKernel, redP, _r);
      CumulativeDistribution.SetBuffer(LUTKernel, greenP, _g);
      CumulativeDistribution.SetBuffer(LUTKernel, blueP, _b);
      CumulativeDistribution.SetBuffer(LUTKernel, alphaP, _a);
      CumulativeDistribution.SetInt(lutWidthP, LUTWidth);
      CumulativeDistribution.SetInt(lutHeightP, LUTHeight);
      CumulativeDistribution.Dispatch(LUTKernel, 1, 1, 1);

      FilterLUT(tempOutRT, tempLUTRT);
      //int blackKern = CumulativeDistribution.FindKernel("WriteBlackToLUT");
      //CumulativeDistribution.SetTexture(blackKern, lutP, tempLUTRT);
      //CumulativeDistribution.Dispatch(blackKern, 1, 1, 1);



      /*
       * Correct for DXT compression in the gaussian texture by scaling the colors about 0.5 by the axis length.
       * The LUT filtering process requires the unmodified gaussian texture, so do this after filtering the LUT.
       * 
       */

      if (CompressionCorrection)
      {
        float[] invAxisLengths = new float[4] { 1.0f / axis0.w, 1.0f / axis1.w, 1.0f / axis2.w, 1 };
        int compCorrKern = CumulativeDistribution.FindKernel("CompressionCorrection");
        CumulativeDistribution.SetInt(texWidthP, texWidth);
        CumulativeDistribution.SetInt(texHeightP, texHeight);
        CumulativeDistribution.SetInt(lutWidthP, LUTWidth);
        CumulativeDistribution.SetInt(lutHeightP, LUTHeight);
        CumulativeDistribution.SetFloats(invAxisLengthsP, invAxisLengths);
        CumulativeDistribution.SetTexture(compCorrKern, texOutP, tempOutRT);
        CumulativeDistribution.Dispatch(compCorrKern, Mathf.Max(1, InputTex.width / 32), Mathf.Max(1, InputTex.height / 32), 1);
      }


      //Graphics.Blit(tempOutRT, TestOutput);
      //Graphics.Blit(tempLUTRT, TestLUT);

      /* Copy the contents of the temporary rendertextures to Texture2D's
       * 
       * Right now I'm outputting 8-bit pngs no matter the input image's format. There isn't a good way
       * to tell the bit depth of each channel from unity's TextureFormat other than manually making
       * a list
       */

      TextureFormat OutputFormat = NoAlpha ? TextureFormat.RGB24 : TextureFormat.ARGB32;
      OutputFormat = outputImageType == FileType.exr ? TextureFormat.RGBAFloat : OutputFormat;
      Texture2D outTex = new Texture2D(InputTex.width, InputTex.height, OutputFormat, false, true);
      Texture2D LUTSlice = new Texture2D(tempLUTRT.width, tempLUTRT.height, OutputFormat, false, true);
      Texture2DArray LUTTex = new Texture2DArray(tempLUTRT.width, tempLUTRT.height, LUTDepth, OutputFormat, false, true);
      RenderTexture currRender = RenderTexture.active;
      Graphics.SetRenderTarget(tempOutRT, 0, CubemapFace.Unknown, 0);
      outTex.ReadPixels(new Rect(0, 0, InputTex.width, InputTex.height), 0, 0);
      outTex.Apply();
      //RenderTexture.active = tempLUTRT;

      Graphics.SetRenderTarget(tempLUT2D, 0, CubemapFace.Unknown, 0);
      CumulativeDistribution.SetInt(lutWidthP, LUTWidth);
      CumulativeDistribution.SetInt(lutHeightP, LUTHeight);
      for (int mip = 0; mip < LUTDepth; mip++)
      {
        CopyLUTSliceTo2D(tempLUTRT, tempLUT2D, mip);
        Graphics.SetRenderTarget(tempLUT2D, 0, CubemapFace.Unknown, 0);
        LUTSlice.ReadPixels(new Rect(0, 0, tempLUTRT.width, tempLUTRT.height), 0, 0);
        LUTSlice.Apply();
        //LUTTex.SetPixels(LUTSlice.GetPixels(), mip);
        Graphics.CopyTexture(LUTSlice, 0, LUTTex, mip);
      }
      //LUTTex.Apply();

      Graphics.SetRenderTarget(currRender, 0, CubemapFace.Unknown, 0);

      /*
       * Create a scriptable object containing the colorspace properties
       * 
       */

      ColorspaceObj colorSettings = ScriptableObject.CreateInstance<ColorspaceObj>();
      colorSettings.Axis0 = axis0;
      colorSettings.Axis1 = axis1;
      colorSettings.Axis2 = axis2;
      colorSettings.Center = colorCenter;

      /* 
       * Save the two images to the assets folder as pngs
       * 
       */

      byte[] outBytes;
      switch (outputImageType)
      {
        case FileType.png:
          outBytes = outTex.EncodeToPNG();
          break;
        case FileType.jpg:
          outBytes = outTex.EncodeToJPG(95);
          break;
        case FileType.tga:
          outBytes = outTex.EncodeToTGA();
          break;
        case FileType.exr:
          outBytes = outTex.EncodeToEXR();
          break;
        default:
          outBytes = outTex.EncodeToPNG();
          break;
      }
     
      string dataPath = Application.dataPath;
      dataPath = dataPath.Substring(0, dataPath.Length - 7); //Path to your assets folder, minus the assets folder itself
      string inputNameAndPath = AssetDatabase.GetAssetPath(InputTex);
      string inputName = Path.GetFileNameWithoutExtension(inputNameAndPath);
      string inputPath = Path.GetDirectoryName(inputNameAndPath);

      Debug.Log(dataPath);

      if (outputImageDir == null)
      {
        outputImageDir = inputPath;
      }

      string outputFileType = FileTypeToString(outputImageType);

      string outputImagePath = Path.Combine(outputImageDir, inputName + "_gauss" + outputFileType);
      string outputLUTPath = Path.Combine(outputImageDir, inputName + "_lut.asset");
      File.WriteAllBytes(Path.Combine(dataPath, outputImagePath), outBytes);

      if (File.Exists(Path.Combine(dataPath, outputLUTPath)))
      {
        Texture2DArray oldLUT = AssetDatabase.LoadAssetAtPath(outputLUTPath, typeof(Texture2DArray)) as Texture2DArray;
        EditorUtility.CopySerialized(LUTTex, oldLUT);
        DestroyImmediate(LUTTex);
      }
      else
      {
        AssetDatabase.CreateAsset(LUTTex, outputLUTPath);
      }

      /*
       * Save the scriptable object
       * 
       */
      string outputColorspacePath = Path.Combine(outputImageDir, inputName + "_colorspace.asset");
      if (File.Exists(Path.Combine(dataPath, outputColorspacePath)))
      {
        ColorspaceObj oldColors = AssetDatabase.LoadAssetAtPath(outputColorspacePath, typeof(ColorspaceObj)) as ColorspaceObj;
        EditorUtility.CopySerialized(colorSettings, oldColors);
        DestroyImmediate(colorSettings);
      }
      else
      {
        AssetDatabase.CreateAsset(colorSettings, outputColorspacePath);
      }

      /*
       * Import the images, copy the import settings of the input image, modify them so that sRGB is false,
       * and reimport the images with the new settings.
       *
       */

      if (AssetDatabase.LoadMainAssetAtPath(outputImagePath) == null)
      {
        AssetDatabase.ImportAsset(outputImagePath);
      }

      TextureImporter inputImport = AssetImporter.GetAtPath(inputNameAndPath) as TextureImporter;
      TextureImporter gaussImport = AssetImporter.GetAtPath(outputImagePath) as TextureImporter;
      //bool inputLinear = inputImport.sRGBTexture;
     // inputImport.sRGBTexture = false;

      if (gaussImport != null)
      {

        EditorUtility.CopySerialized(inputImport, gaussImport);
        // Our gaussian texture is not a normal map even if the input was a normal!
        // Unity does a conversion process on the image if it is marked as normal that would alter the information stored in our image. 
        if (gaussImport.textureType == TextureImporterType.NormalMap)
        {
          gaussImport.textureType = TextureImporterType.Default;
        }
        gaussImport.sRGBTexture = false;
        AssetDatabase.ImportAsset(outputImagePath);
      }
      else
      {
        Debug.LogError("Failed to set import settings on " + outputImagePath + ". YOU MUST UNCHECK sRGB IN THE TEXTURE'S IMPORT SETTINGS YOURSELF OR THE TEXTURE WILL NOT WORK!");
      }

     // inputImport.sRGBTexture = inputLinear;

      //outTex = AssetDatabase.LoadAssetAtPath("/textures/GaussianTest.png", typeof(Texture2D)) as Texture2D;
      //EditorUtility.SetDirty(outTex);

      _r.Release();
      _g.Release();
      _b.Release();
      _a.Release();
      _rI.Release();
      _gI.Release();
      _bI.Release();
      _aI.Release();
      tempOutRT.Release();
      tempLUTRT.Release();
    }

    #endregion
  }
}
