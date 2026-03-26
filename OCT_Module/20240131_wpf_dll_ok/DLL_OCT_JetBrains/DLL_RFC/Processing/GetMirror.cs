// Type: DLL_RFC.Processing.GetMirror
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using MathNet.Filtering;
using MathNet.Filtering.Median;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DLL_RFC.Processing
{
  internal class GetMirror
  {
    private InputParameters para;
    private OnlineMedianFilter medianFilter;
    private float[][] fftResultCopy = new float[957][];
    private double[] mirrorProfileX;
    private double[] mirrorProfileY;
    private double[] mirrorProfileXMedian;
    private double[] mirrorProfileYMedian;
    private float[] mirrorProfileXMedianFloat;
    private float[] mirrorProfileYMedianFloat;
    private int count;
    private float maxValue;

    public GetMirror(InputParameters para)
    {
      this.para = para;
      this.medianFilter = new OnlineMedianFilter(3);
      for (int index = 0; index < this.fftResultCopy.Length; ++index)
        this.fftResultCopy[index] = new float[1500];
      this.mirrorProfileX = new double[this.fftResultCopy.Length];
      this.mirrorProfileY = new double[this.fftResultCopy.Length];
      this.mirrorProfileXMedian = new double[this.fftResultCopy.Length];
      this.mirrorProfileYMedian = new double[this.fftResultCopy.Length];
      this.mirrorProfileXMedianFloat = new float[this.fftResultCopy.Length];
      this.mirrorProfileYMedianFloat = new float[this.fftResultCopy.Length];
    }

    public void GetMirrorProfile(float[] fftResult, int count)
    {
      this.count = count;
      if (count % 10 == 0)
      {
        for (int index = 0; index < this.fftResultCopy.Length; ++index)
        {
          this.mirrorProfileX[index] = 0.0;
          this.mirrorProfileY[index] = 0.0;
        }
      }
      this.CopyData(fftResult);
      this.GetXYProfile();
      if (count != 9)
        return;
      for (int index = 0; index < this.fftResultCopy.Length; ++index)
      {
        this.mirrorProfileX[index] = this.mirrorProfileX[index] / 5.0;
        this.mirrorProfileY[index] = this.mirrorProfileY[index] / 5.0;
      }
      this.mirrorProfileXMedian = ((OnlineFilter) this.medianFilter).ProcessSamples(this.mirrorProfileX);
      this.mirrorProfileYMedian = ((OnlineFilter) this.medianFilter).ProcessSamples(this.mirrorProfileY);
      for (int index = 0; index < this.fftResultCopy.Length; ++index)
      {
        this.mirrorProfileXMedianFloat[index] = (float) this.mirrorProfileXMedian[index];
        this.mirrorProfileYMedianFloat[index] = (float) this.mirrorProfileYMedian[index];
      }
      using (BinaryWriter binaryWriter = new BinaryWriter((Stream) new FileStream(this.para.SaveFileName, FileMode.Create)))
      {
        for (int index = 0; index < this.fftResultCopy.Length; ++index)
          binaryWriter.Write(this.mirrorProfileXMedianFloat[index]);
        for (int index = 0; index < this.fftResultCopy.Length; ++index)
          binaryWriter.Write(this.mirrorProfileYMedianFloat[index]);
      }
    }

    private void CopyData(float[] floats)
    {
      for (int index = 0; index < 957; ++index)
        Array.Copy((Array) floats, index * 4096 + this.para.InspectionStartIndex, (Array) this.fftResultCopy[index], 0, this.fftResultCopy[0].Length);
    }

    private void GetXYProfile()
    {
      if (this.count < 5)
      {
        for (int index = 0; index < this.fftResultCopy.Length; ++index)
        {
          this.maxValue = ((IEnumerable<float>) this.fftResultCopy[index]).Max();
          this.mirrorProfileX[index] += (double) Array.IndexOf<float>(this.fftResultCopy[index], this.maxValue);
        }
      }
      else
      {
        for (int index = 0; index < this.fftResultCopy.Length; ++index)
        {
          this.maxValue = ((IEnumerable<float>) this.fftResultCopy[index]).Max();
          this.mirrorProfileY[index] += (double) Array.IndexOf<float>(this.fftResultCopy[index], this.maxValue);
        }
      }
    }
  }
}
