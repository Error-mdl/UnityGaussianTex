/** 
 * Author: Error-mdl
 * Created: 2021-10-26
 * 
 * An extremely incomplete class for 3x3 matricies made for doing eigen decomposition of 3x3 covarience matricies.
 * Only a few simple methods like matrix by matrix multiplication are implemented.
 * 
 * (c) 2021 Error.mdl
 * This code is licensed under the BSD-3 Clause license, see LICENSE.md for more details
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;



/// <summary>
/// A mostly incomplete set of classes implementing matricies of other sizes than unity's Matrix4x4
/// </summary>
namespace MatrixPlus
{
  /// <summary>
  /// Partially implemented 3x3 32 bit floating point matrix class, has a few basic constructors and operations
  /// </summary>
  public class Matrix3x3f
  {
    public float[][] mValues;
    public float m00
    {
      get { return mValues[0][0]; }
      set { mValues[0][0] = value; }
    }
    public float m01
    {
      get { return mValues[0][1]; }
      set { mValues[0][1] = value; }
    }
    public float m02
    {
      get { return mValues[0][2]; }
      set { mValues[0][2] = value; }
    }
    public float m10
    {
      get { return mValues[1][0]; }
      set { mValues[1][0] = value; }
    }
    public float m11
    {
      get { return mValues[1][1]; }
      set { mValues[1][1] = value; }
    }
    public float m12
    {
      get { return mValues[1][2]; }
      set { mValues[1][2] = value; }
    }
    public float m20
    {
      get { return mValues[2][0]; }
      set { mValues[2][0] = value; }
    }
    public float m21
    {
      get { return mValues[2][1]; }
      set { mValues[2][1] = value; }
    }
    public float m22
    {
      get { return mValues[2][2]; }
      set { mValues[2][2] = value; }
    }

    public static Matrix3x3f operator +(Matrix3x3f a, Matrix3x3f b) => 
      new Matrix3x3f(new float[3, 3] {
        { a.m00 + b.m00, a.m01 + b.m01, a.m02 + b.m02 },
        { a.m10 + b.m10, a.m11 + b.m11, a.m12 + b.m12 },
        { a.m20 + b.m20, a.m21 + b.m21, a.m22 + b.m22 },
        });

    public static Matrix3x3f operator -(Matrix3x3f a, Matrix3x3f b) =>
      new Matrix3x3f(new float[3, 3] {
        { a.m00 - b.m00, a.m01 - b.m01, a.m02 - b.m02 },
        { a.m10 - b.m10, a.m11 - b.m11, a.m12 - b.m12 },
        { a.m20 - b.m20, a.m21 - b.m21, a.m22 - b.m22 },
        });

    public static Matrix3x3f operator *(float a, Matrix3x3f b) =>
      new Matrix3x3f(new float[3, 3] {
        { a * b.m00, a * b.m01, a * b.m02 },
        { a * b.m10, a * b.m11, a * b.m12 },
        { a * b.m20, a * b.m21, a * b.m22 },
        });
    public static Matrix3x3f operator *(Matrix3x3f b, float a) =>
      new Matrix3x3f(new float[3, 3] {
        { a * b.m00, a * b.m01, a * b.m02 },
        { a * b.m10, a * b.m11, a * b.m12 },
        { a * b.m20, a * b.m21, a * b.m22 },
        });


    public Matrix3x3f()
    {
      mValues = new float[3][] {new float[3]{ 0.0f, 0.0f, 0.0f },
                                 new float[3]{ 0.0f, 0.0f, 0.0f },
                                 new float[3]{ 0.0f, 0.0f, 0.0f }};
    }

    public Matrix3x3f(float i00, float i01, float i02, float i10, float i11, float i12, float i20, float i21, float i22)
    {
      mValues = new float[3][] {new float[3]{ i00, i01, i02 },
                                new float[3]{ i10, i11, i12 },
                                new float[3]{ i20, i21, i22 }};
    }

    public Matrix3x3f(float[,] arrayInput)
    {
      if (arrayInput == null)
      {
        throw new ArgumentException(string.Format("Input array null", nameof(arrayInput)));
      }

      if (arrayInput.GetLength(0) != 3 || arrayInput.GetLength(1) != 3)
      {
        throw new ArgumentException(string.Format("Cannot initialize Matrix3x3f from array with dimensions {0}x{1}, input must be 3x3!",
          arrayInput.GetLength(0), arrayInput.GetLength(1)), nameof(arrayInput));
      }

      mValues = new float[3][] {new float[3]{ arrayInput[0, 0], arrayInput[0, 1], arrayInput[0, 2] },
                                 new float[3]{ arrayInput[1, 0], arrayInput[1, 1], arrayInput[1, 2] },
                                 new float[3]{ arrayInput[2, 0], arrayInput[2, 1], arrayInput[2, 2] }};
      /*
      for (int r = 0; r < 3; r++)
      {
        for (int c = 0; c < 3; c++)
        {
          mValues[r][c] = arrayInput[r, c];
        }
      }
      */
    }

    /// <summary>
    /// Fills the matrix with 3 Vector3's, assigning the vectors as columns of the matrix
    /// </summary>
    /// <param name="column0">Vector3 that will become the first column of the matrix</param>
    /// <param name="column1">Vector3 that will become the second column of the matrix</param>
    /// <param name="column2">Vector3 that will become the third column of the matrix</param>
    public void VectorsToColumns(Vector3 column0, Vector3 column1, Vector3 column2)
    {
      mValues[0][0] = column0.x;
      mValues[1][0] = column0.y;
      mValues[2][0] = column0.z;
      mValues[0][1] = column1.x;
      mValues[1][1] = column1.y;
      mValues[2][1] = column1.z;
      mValues[0][2] = column2.x;
      mValues[1][2] = column2.y;
      mValues[2][2] = column2.z;
    }

    /// <summary>
    /// Fills the matrix with 3 Vector3's, assigning the vectors as rows of the matrix
    /// </summary>
    /// <param name="row0">Vector3 that will become the first row of the matrix</param>
    /// <param name="row1">Vector3 that will become the second row of the matrix</param>
    /// <param name="row2">Vector3 that will become the third row of the matrix</param>
    public void VectorsToRows(Vector3 row0, Vector3 row1, Vector3 row2)
    {
      mValues[0][0] = row0.x;
      mValues[0][1] = row0.y;
      mValues[0][2] = row0.z;
      mValues[1][0] = row1.x;
      mValues[1][1] = row1.y;
      mValues[1][2] = row1.z;
      mValues[2][0] = row2.x;
      mValues[2][1] = row2.y;
      mValues[2][2] = row2.z;
    }

    /// <summary>
    /// Gets a column of the matrix, and returns it as a Vector3
    /// </summary>
    /// <param name="index">Index of the column to retrieve, starting at 0</param>
    /// <returns></returns>
    public Vector3 GetColumnVector(int index)
    {
      if (index >= 3 || index < 0)
      {
        throw new IndexOutOfRangeException(String.Format("Tried to retrieve column at index {0} of 3x3 matrix, index must be between 0 and 2", index));
      }
      return new Vector3((float)mValues[0][index], (float)mValues[1][index], (float)mValues[2][index]);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public Vector3 GetRowVector(int index)
    {
      if (index >= 3 || index < 0)
      {
        throw new IndexOutOfRangeException(String.Format("Tried to retrieve row at index {0} of 3x3 matrix, index must be between 0 and 2", index));
      }
      return new Vector3((float)mValues[index][0], (float)mValues[index][1], (float)mValues[index][2]);
    }

    public static Matrix3x3f Mul(Matrix3x3f mat1, Matrix3x3f mat2)
    {
      Matrix3x3f output = new Matrix3x3f();
      for (int r = 0; r < 3; r++)
      {
        for (int c = 0; c < 3; c++)
        {
          float element = 0.0f;

          for (int i = 0; i < 3; i++)
          {
            element += mat1.mValues[r][i] * mat2.mValues[i][c];
          }

          output.mValues[r][c] = element;
        }
      }
      return output;
    }

    public Matrix3x3f Transpose()
    {
      Matrix3x3f output = new Matrix3x3f(m00, m10, m20,
                                         m01, m11, m21,
                                         m02, m12, m22);
      return output;
    }
  }
}
