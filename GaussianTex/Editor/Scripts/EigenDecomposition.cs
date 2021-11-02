/** 
 * Author: Error-mdl
 * Created: 2021-10-25
 * 
 * An implementation of an iterative eigen solver for symmetric real 3x3 matricies
 * taken from "A Robust Eigensolver for 3x3 Symmetric Matrices" by David Eberly
 * https://www.geometrictools.com/Documentation/RobustEigenSymmetric3x3.pdf
 * 
 * Made for computing the Eigenvectors of the covarience matrix of the RGB channels
 * of an image, in order to generate gaussian-transformed textures that compress well
 * with DXT compression as described in "Procedural Stochastic Textures by Tiling and
 * Blending" by Thomas Deliot and Eric Heitz
 * 
 * (c) 2021 Error-mdl
 * This code is licensed under the BSD-3 Clause license, see LICENSE.md for more details
 */

using System.Collections;

using System.Collections.Generic;
using UnityEngine;
using MatrixPlus;
/// <summary>
/// Calculates the Eigen-values and -vectors of real, symmetric 3x3 matricies.
/// Based on the iterative method laid out in <see href="https://www.geometrictools.com/Documentation/RobustEigenSymmetric3x3.pdf"></see>
/// </summary>
public class EigenDecomposition
{

  /// <summary>
  /// Calculates a vector (sin X, cos X) such that the dot product of a given vector (-v, u) with (sin X, cos X) is 0.
  /// See equation 3 in <see href="https://www.geometrictools.com/Documentation/RobustEigenSymmetric3x3.pdf"></see>
  /// </summary>
  /// <param name="u">First component of the input vector</param>
  /// <param name="v">Second component of the input vector</param>
  /// <param name="sin0">Variable in which to output the calculated value of sin X</param>
  /// <param name="cos0">Variable in which to output the calculated value of cos X</param>
  private static void ParallelSinCos(float u, float v, ref float cos0, ref float sin0)
  {
    float inputLen = Mathf.Sqrt((u * u) + (v * v));
    if (inputLen > 0)
    {
      cos0 = u / inputLen;
      sin0 = v / inputLen;
      if (cos0 > 0)
      {
        cos0 = -cos0;
        sin0 = -sin0;
      }
    }
    else
    {
      sin0 = 0;
      cos0 = -1;
    }
  }

/// <summary>
/// Calculates the exponent portion of a float, similar to C's frexp. THIS DOESN"T SEEM TO QUITE WORK RIGHT
/// Mostly taken from a stackoverflow post: <see href="https://stackoverflow.com/questions/389993/extracting-mantissa-and-exponent-from-double-in-c-sharp"></see>
/// </summary>
/// <param name="input">Float from which the exponent is extracted from</param>
/// <returns>Exponent of input float</returns>
  private static int float_exp(float input)
  {
    if (System.Single.IsNaN(input) || System.Single.IsInfinity(input))
    {
      return 0;
    }
    
    byte[] inputBytes = System.BitConverter.GetBytes(input);
    int inputInt = System.BitConverter.ToInt32(inputBytes, 0);
    int exponent = (inputInt >> 23) & 0x000000ff;
    int mantissa = inputInt & 0x007fffff;
    
    if (exponent == 0)
    {
      exponent = 1;
    }
    else
    {
      mantissa = mantissa | (1 << 23);
    }
    exponent -= 127 + 23;

    if (mantissa == 0)
    {
      return exponent;
    }
    
    while ((mantissa & 1) == 0)
    {
      mantissa >>= 1;
      exponent++;
    }
    
    return exponent;
  }

  /// <summary>
  /// Checks if a given element in a matrix is approximately 0 compared with the two closest diagonal elements of the matrix.
  /// Copied from listing 1 in <see href="https://www.geometrictools.com/Documentation/RobustEigenSymmetric3x3.pdf"></see>
  /// </summary>
  /// <param name="diagonal0">First closest diagonal element</param>
  /// <param name="diagonal1">Second closest diagonal element</param>
  /// <param name="value">Non-diagonal element of a matrix</param>
  /// <returns></returns>
  private static bool isRelativeZero(float diagonal0, float diagonal1, float value)
  {
    float magnitude = Mathf.Abs(diagonal0) + Mathf.Abs(diagonal1);
    return (magnitude + value) == magnitude;
  }

  /// <summary>
  /// Gets the Eigenvectors and values of a given symmetric 3x3 matrix.
  /// </summary>
  /// <param name="mat">Input 3x3 matrix, assumed to be symmetric.</param>
  /// <param name="eigValues">Vector3 into which the eigenvalues are output</param>
  /// <returns>A 3x3 matrix composed of the 3 eigenvectors as its columns</returns>
  public static Matrix3x3f Get3x3SymmetricEigen(Matrix3x3f mat, ref Vector3 eigValues)
  {
    float c0 = 0, s0 = 0;
    ParallelSinCos(mat.m12, -mat.m02, ref c0, ref s0);
    //Debug.Log(string.Format("b12 first cos-sin product:({0}, {1}) * ({2}, {3}) = {4}",c0, s0, mat.m02, mat.m12, c0 * mat.m02 + s0 * mat.m12));
    float ca00sa01 = c0 * mat.m00 + s0 * mat.m01;
    float ca01sa11 = c0 * mat.m01 + s0 * mat.m11;
    float sa00ca01 = s0 * mat.m00 - c0 * mat.m01;
    float sa01ca11 = s0 * mat.m01 - c0 * mat.m11;
    float b00 = c0 * ca00sa01 + s0 * ca01sa11;
    float b11 = s0 * sa00ca01 - c0 * sa01ca11;
    float b22 = mat.m22;
    float b01 = s0 * ca00sa01 - c0 * ca01sa11;
    float b12 = s0 * mat.m02 - c0 * mat.m12;
    //float b02 = 0f;
    //Debug.Log(string.Format("b00: {0}\tb11: {1}\tb22: {2}\nb01: {3}\tb12: {4}", b00, b11, b22, b01, b12));
    Matrix3x3f B = new Matrix3x3f(
      b00, b01, 0f,
      b01, b11, b12,
      0f,  b12, b22
      );

    Matrix3x3f Q = new Matrix3x3f(
      c0,  s0,  0,
      s0, -c0,  0,
      0,   0,   1
      );

    int alpha = 24 + 125; //24 digits in float32 mantissa, minus the minimum exponent of -125

    if (Mathf.Abs(b12) < Mathf.Abs(b01))
    {
      int power = 127;// since float_exp(b12) doesn't seem to be outputting the correct values, just assume the max exponent;
      //Debug.Log("Power: " + power.ToString());
      int maxIterations = 2 * (power + alpha + 1);
      for (int i = 0; i < maxIterations; i++)
      {
        // Calculate sin(theta), cos(theta) such that (sin(2theta), cos(2theta)) dot (b01, (b11 - b00)/2) = 0
        // See eq.6 https://www.geometrictools.com/Documentation/RobustEigenSymmetric3x3.pdf

        float c1_2theta = 0.0f, s1_2theta = 0.0f;
        ParallelSinCos(0.5f * (B.m11 - B.m00), -B.m01, ref c1_2theta, ref s1_2theta);
        float s1 = Mathf.Sqrt(0.5f * (1f - c1_2theta));
        float c1 = s1_2theta / (2 * s1);

        // Calculate Gt * B * G, but rather than actually multiply the matricies only calculate the upper triangular elements
        // and use pre-simplified formulae for each element

        float p00 = c1 * (c1 * B.m00 + s1 * B.m01) + s1 * (c1 * B.m01 + s1 * B.m11);
        float p11 = B.m22;
        float p22 = s1 * (s1 * B.m00 - c1 * B.m01) - c1 * (s1 * B.m01 - c1 * B.m11);
        float p01 = s1 * B.m12;
        float p12 = c1 * B.m12;

        B = new Matrix3x3f(
          p00, p01, 0f,
          p01, p11, p12,
          0f, p12, p22
          );
        //Debug.Log("Iteration " + i.ToString());
        //Debug.Log(string.Format("b00: {0}\tb11: {1}\tb22: {2}\nb01: {3}\tb12: {4}", B.m00, B.m11, B.m22, B.m01, B.m12));
        /* Calculate Q = Q * G
         */
        Q = new Matrix3x3f(
          Q.m00 * c1 + Q.m01 * s1,  Q.m02,  Q.m00 * (-s1) + Q.m01 * c1,
          Q.m10 * c1 + Q.m11 * s1,  Q.m12,  Q.m10 * (-s1) + Q.m11 * c1,
          Q.m20 * c1 + Q.m21 * s1,  Q.m22,  Q.m20 * (-s1) + Q.m21 * c1
          );

        /*
        string qout = "";
        for (int t = 0; t < 3; t++)
        {
          qout += string.Format("{0}\t{1}\t{2}\n", Q.mValues[t][0], Q.mValues[t][1], Q.mValues[t][2]);
        }
        Debug.Log("Q:");
        Debug.Log(qout);
        */

        if (isRelativeZero(p11, p22, p12))
        {
          //Debug.Log(string.Format("Successfully Zero'd out p12 after {0} iterations", i));
          float c2_2theta = 0.0f, s2_2theta = 0.0f;
          ParallelSinCos((p00 - p11) * 0.5f, p01, ref c2_2theta, ref s2_2theta);
          float s2 = Mathf.Sqrt(0.5f * (1f - c2_2theta));
          float c2 = s2_2theta / (2 * s2);
          eigValues.x = c2 * (c2 * p00 + s2 * p01) + s2 * (c2 * p01 + s2 * p11);
          eigValues.y = s2 * (s2 * p00 - c2 * p01) - c2 * (s2 * p01 - c2 * p11);
          eigValues.z = p22;

          Matrix3x3f H2 = new Matrix3x3f(
            c2,  s2, 0f,
            s2, -c2, 0f,
            0f,  0f, 1f
            );
          Q = new Matrix3x3f(
            Q.m00 * c2 + Q.m01 * s2, Q.m00 * s2 - Q.m01 * c2, Q.m02,
            Q.m10 * c2 + Q.m11 * s2, Q.m10 * s2 - Q.m11 * c2, Q.m12,
            Q.m20 * c2 + Q.m21 * s2, Q.m20 * s2 - Q.m21 * c2, Q.m22
            );
          break;
        }
      }
      //B = 
    }
    else
    {
      int power = 127;// since float_exp(b01) doesn't seem to be outputting the correct values, assume the max exponent;
      //Debug.Log("Power: " + power.ToString());
      int maxIterations = 2 * (power + alpha + 1);
      for (int i = 0; i < maxIterations; i++)
      {
        // Calculate sin(theta), cos(theta) such that (sin(2theta), cos(2theta)) dot (b12, (b22 - b11)/2) = 0
        // See eq.20 https://www.geometrictools.com/Documentation/RobustEigenSymmetric3x3.pdf

        float c1_2theta = 0.0f, s1_2theta = 0.0f;
        ParallelSinCos(0.5f * (B.m22 - B.m11), - B.m12, ref c1_2theta, ref s1_2theta);
        float s1 = Mathf.Sqrt(0.5f * (1f - c1_2theta));
        float c1 = s1_2theta / (2 * s1);

        // Calculate Gt * B * G, but rather than actually multiply the matricies only calculate the upper triangular elements
        // and use pre-simplified formulae for each element

        float p00 = c1 * (c1 * B.m11 + s1 * B.m12) + s1 * (c1 * B.m12 + s1 * B.m22);
        float p11 = B.m00;
        float p22 = s1 * (s1 * B.m11 - c1 * B.m12) - c1 * (s1 * B.m12 - c1 * B.m22);
        float p01 = c1 * B.m01;
        float p12 = -s1 * B.m01;

        B = new Matrix3x3f(
          p00, p01, 0f,
          p01, p11, p12,
          0f, p12, p22
          );

        //Debug.Log("Iteration " + i.ToString());
        //Debug.Log(string.Format("b00: {0}\tb11: {1}\tb22: {2}\nb01: {3}\tb12: {4}", B.m00, B.m11, B.m22, B.m01, B.m12));

        /* Calculate Q = Q * G
         */
        Q = new Matrix3x3f(
          Q.m01 * c1 + Q.m02 * s1, Q.m00, Q.m01 * (-s1) + Q.m02 * c1,
          Q.m11 * c1 + Q.m12 * s1, Q.m10, Q.m11 * (-s1) + Q.m12 * c1,
          Q.m21 * c1 + Q.m22 * s1, Q.m20, Q.m21 * (-s1) + Q.m22 * c1
          );

        /*
        string qout = "";
        for (int t = 0; t < 3; t++)
        {
          qout += string.Format("{0}\t{1}\t{2}\n",Q.mValues[t][0], Q.mValues[t][1], Q.mValues[t][2]);
        }
        Debug.Log("Q:");
        Debug.Log(qout);
        */

        if (isRelativeZero(B.m00, B.m11, B.m01))
        {
          //Debug.Log(string.Format("Successfully Zero'd out p01 after {0} iterations", i));
          float c2_2theta = 0.0f, s2_2theta = 0.0f;
          ParallelSinCos((B.m11 - B.m22) * 0.5f, B.m12, ref c2_2theta, ref s2_2theta);
          float s2 = Mathf.Sqrt(0.5f * (1f - c2_2theta));
          float c2 = s2_2theta / (2 * s2);
          eigValues.x = B.m00;
          eigValues.y = c2 * (c2 * B.m11 + s2 * B.m12) + s2 * (c2 * B.m12 + s2 * B.m22);
          eigValues.z = s2 * (s2 * B.m11 - c2 * B.m12) - c2 * (s2 * B.m12 - c2 * B.m22);
         
          Matrix3x3f H2 = new Matrix3x3f(
            1,  0,  0f,
            0,  c2, s2,
            0f, s2, -c2
            );
          Q = new Matrix3x3f(
            Q.m00, Q.m01 * c2 + Q.m02 * s2, Q.m01 * s2 - Q.m02 * c2,
            Q.m10, Q.m11 * c2 + Q.m12 * s2, Q.m11 * s2 - Q.m12 * c2,
            Q.m20, Q.m21 * c2 + Q.m22 * s2, Q.m21 * s2 - Q.m22 * c2
            );
          break;
        }
      }
    }
    return Q;
  }
}

