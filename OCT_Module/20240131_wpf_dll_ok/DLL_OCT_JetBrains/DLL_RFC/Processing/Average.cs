// Type: DLL_RFC.Processing.Average
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using System;
using System.Threading.Tasks;

namespace DLL_RFC.Processing
{
  internal class Average
  {
    private InputParameters para;
    private float[] averageResult;
    private int count;
    private int realBscan;
    private int halfFFTLength;

    public float[] AverageResult => this.averageResult;

    public Average(InputParameters para)
    {
      this.para = para;
      this.halfFFTLength = para.HalfFFTLength;
      this.realBscan = (int) ((long) para.BScan - (long) para.NumberOfCutLength); // 실제 B-Scan 길이
      this.count = para.HalfFFTLength * this.realBscan; // 
      this.averageResult = new float[this.count];
    }

    public void RunAverage(float[] fftResult) => Parallel.For(0, this.realBscan, (Action<int>) (i =>
    {
      for (int index = 0; index < this.halfFFTLength; ++index)
      {
        this.averageResult[this.halfFFTLength * i + index] = 0.0f;
        this.averageResult[this.halfFFTLength * i + index] = (float) (((double) fftResult[this.halfFFTLength * (i + this.para.NumberOfCutLength) + index] + (double) fftResult[this.halfFFTLength * (1999 - i) + index]) / 2.0);
      }
    }));
  }
}
