// Type: DLL_RFC.Processing.GetDepth
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using MathNet.Filtering;
using MathNet.Filtering.Median;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DLL_RFC.Processing
{
  internal class GetDepth
  {
    private InputParameters para;
    private int realBscan;
    public float[] calData;
    private byte[] rawData_byte;
    private float[][] rawData_float;
    private float[][] rawData_float_ori;
    private OnlineMedianFilter medianFilter;
    private double[] topProfile;
    private double[] topProfile_filter;
    private double[] bottomProfile;
    private double[] bottomProfile_filter;
    private double[] laserProfile;
    private double[] laserProfile_filter;
    private double[] laserProfile_filter_sort;
    public int inspectionStartIndex;
    private int tempX;
    private int tempY;
    private int x_scope = 5;
    private int x_decrease = 0;
    private int y_increase = 0;
    private float maxValue1;
    private float[] temp1 = new float[300];
    private float maxValue2;
    private float bottomStartIndex;
    private float[] temp2 = new float[300];
    private int count1;
    private double topProfile_average;
    private double bottomProfile_average;
    private int startIndexForContour;
    private int endIndexForContour;
    private float maxValue3;
    private float centerStartIndex;
    private float[] temp3;
    private List<float> temp4 = new List<float>();
    private float linearThreshold;
    private double[] selectedHeight;

    public float[][] RawData
    {
      get
      {
        Parallel.For(0, this.rawData_float_ori.Length, (Action<int>) (i =>
        {
          for (int index = 0; index < this.rawData_float_ori[0].Length; ++index)
            this.rawData_float_ori[i][index] = (float) Math.Log10((double) this.rawData_float_ori[i][index]) * 20f;
        }));
        return this.rawData_float_ori;
      }
    }

    public double[] TopProfile => this.topProfile_filter;

    public double[] BottomProfile => this.bottomProfile_filter;

    public double[] LaserProfile => this.laserProfile_filter;

    public double GlassDepth { get; private set; }

    public double LaserHeight { get; private set; }

    public double ReferencePosition => ((IEnumerable<double>) this.topProfile_filter).Average();

    public double[] LaserCordination { get; private set; }

    public GetDepth(InputParameters para)
    {
      this.para = para;
      this.realBscan = (int) ((long) para.BScan - (long) para.NumberOfCutLength);
      this.rawData_byte = new byte[this.realBscan * 1500];
      this.rawData_float = new float[this.realBscan][];
      this.rawData_float_ori = new float[this.realBscan][];
      for (int index = 0; index < this.rawData_float.Length; ++index)
      {
        this.rawData_float[index] = new float[1500];
        this.rawData_float_ori[index] = new float[1500];
      }
      this.medianFilter = new OnlineMedianFilter(para.NumberOfMedianFilter);
      this.topProfile = new double[this.realBscan];
      this.topProfile_filter = new double[this.realBscan];
      this.bottomProfile = new double[this.realBscan];
      this.bottomProfile_filter = new double[this.realBscan];
      this.laserProfile = new double[this.realBscan];
      this.laserProfile_filter = new double[this.realBscan];
      this.laserProfile_filter_sort = new double[this.realBscan];
      this.temp3 = new float[580 - para.StartIndexBelowTopPosition];
      this.selectedHeight = new double[para.SelectHeightPoint];
      this.LaserCordination = new double[para.SelectHeightPoint * 2];
    }

    public void GetDepthProfile(float[] data, float[] calData)
    {
      this.calData = calData;
      this.GetRawData(data);
      this.GetTopProfile();
      this.GetBottomProfile();
      this.GetCenterProfile();
      this.GetNumerics();
    }

    private void GetRawData(float[] floatData)
    {
      if (this.para.Direction)
        Parallel.For(0, this.realBscan, (Action<int>) (i =>
        {
          Array.Copy((Array) floatData, i * this.para.HalfFFTLength + this.inspectionStartIndex + (int) this.calData[i], (Array) this.rawData_float[i], 0, this.rawData_float[0].Length);
          Array.Copy((Array) floatData, i * this.para.HalfFFTLength + this.inspectionStartIndex + (int) this.calData[i], (Array) this.rawData_float_ori[i], 0, this.rawData_float[0].Length);
        }));
      else
        Parallel.For(0, this.realBscan, (Action<int>) (i =>
        {
          Array.Copy((Array) floatData, i * this.para.HalfFFTLength + this.inspectionStartIndex + (int) this.calData[i + this.realBscan], (Array) this.rawData_float[i], 0, this.rawData_float[0].Length);
          Array.Copy((Array) floatData, i * this.para.HalfFFTLength + this.inspectionStartIndex + (int) this.calData[i + this.realBscan], (Array) this.rawData_float_ori[i], 0, this.rawData_float[0].Length);
        }));
    }

    private void GetTopProfile()
    {
      for (int index = 0; index < this.realBscan; ++index)
      {
        Array.Copy((Array) this.rawData_float[index], 0, (Array) this.temp1, 0, this.temp1.Length);
        this.maxValue1 = ((IEnumerable<float>) this.temp1).Max();
        this.topProfile[index] = (double) Array.IndexOf<float>(this.temp1, this.maxValue1);
      }
      this.topProfile_filter = ((OnlineFilter) this.medianFilter).ProcessSamples(this.topProfile);
      this.topProfile_average = ((IEnumerable<double>) this.topProfile_filter).Average();
    }

    private void GetBottomProfile()
    {
      this.bottomStartIndex = (float) (this.topProfile_average + 630.0 - 80.0);
      for (int index = 0; index < this.realBscan; ++index)
      {
        Array.Copy((Array) this.rawData_float[index], (int) this.bottomStartIndex, (Array) this.temp2, 0, this.temp2.Length);
        this.maxValue2 = ((IEnumerable<float>) this.temp2).Max();
        this.bottomProfile[index] = (double) Array.IndexOf<float>(this.temp2, this.maxValue2) + (double) this.bottomStartIndex;
      }
      this.bottomProfile_filter = ((OnlineFilter) this.medianFilter).ProcessSamples(this.bottomProfile);
      this.bottomProfile_filter[0] = this.bottomProfile_filter[1];
      this.bottomProfile_average = ((IEnumerable<double>) this.bottomProfile_filter).Average();
    }

    private void GetCenterProfile()
    {
      this.linearThreshold = (float) Math.Pow(10.0, (double) this.para.Threshold / 20.0);
      this.centerStartIndex = (float) ((IEnumerable<double>) this.topProfile_filter).Average() + (float) this.para.StartIndexBelowTopPosition;
      for (int index1 = 0; index1 < this.realBscan; ++index1)
      {
        this.temp4.Clear();
        Array.Copy((Array) this.rawData_float[index1], (int) this.centerStartIndex, (Array) this.temp3, 0, this.temp3.Length);
        for (int index2 = 0; index2 < this.temp3.Length; ++index2)
        {
          if ((double) this.temp3[index2] > (double) this.linearThreshold)
          {
            this.temp4.Add(this.temp3[index2]);
            break;
          }
        }
        if (this.temp4.Count<float>() > 0)
        {
          this.maxValue3 = this.temp4[0];
          this.laserProfile[index1] = (double) Array.IndexOf<float>(this.temp3, this.maxValue3) + (double) this.centerStartIndex;
        }
        else
          this.laserProfile[index1] = this.bottomProfile[index1];
      }
      for (int index = 0; index < this.para.EndIndex; ++index)
        this.laserProfile[index] = this.bottomProfile[index];
      for (int index = 0; index < this.para.StartIndex; ++index)
        this.laserProfile[index + (this.realBscan - this.para.StartIndex)] = this.bottomProfile[index + (this.realBscan - this.para.StartIndex)];
      this.laserProfile_filter = ((OnlineFilter) this.medianFilter).ProcessSamples(this.laserProfile);
      if (this.count1 == 10)
      {
        using (BinaryWriter binaryWriter = new BinaryWriter((Stream) new FileStream("laserProfile_filter.bin", FileMode.Create)))
        {
          for (int index = 0; index < this.laserProfile_filter.Length; ++index)
            binaryWriter.Write(this.laserProfile_filter[index]);
        }
      }
      ++this.count1;
      Array.Copy((Array) this.laserProfile_filter, (Array) this.laserProfile_filter_sort, this.realBscan);
      Array.Sort<double>(this.laserProfile_filter_sort);
    }

    private void GetNumerics()
    {
      this.GlassDepth = Math.Abs(((IEnumerable<double>) this.topProfile_filter).Average() - ((IEnumerable<double>) this.bottomProfile_filter).Average()) * (double) this.para.MicroPerPixel;
      for (int index = 0; index < this.selectedHeight.Length; ++index)
      {
        this.selectedHeight[index] = this.laserProfile_filter_sort[index];
        this.LaserCordination[index * 2 + 1] = this.selectedHeight[index];
        this.LaserCordination[index * 2] = (double) Array.IndexOf<double>(this.laserProfile_filter, this.selectedHeight[index]);
      }
      this.LaserHeight = (626.0 - (((IEnumerable<double>) this.selectedHeight).Average() - ((IEnumerable<double>) this.topProfile_filter).Average())) * (double) this.para.MicroPerPixel;
    }
  }
}
