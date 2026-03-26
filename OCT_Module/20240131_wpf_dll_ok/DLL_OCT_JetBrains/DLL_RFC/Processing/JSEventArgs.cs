// Type: DLL_RFC.Processing.JSEventArgs
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using System;

namespace DLL_RFC.Processing
{
  public class JSEventArgs : EventArgs
  {
    private float[][][] depthImage_Clone;
    private double[] topProfile_Clone;
    private double[] laserProfile_Clone;
    private double[] laserCordination_Clone;
    private int count;

    public double GlassDepth { get; private set; }

    public double LaserHeight { get; private set; }

    public double ReferencePosition { get; private set; }

    public float[][] DepthImage => this.depthImage_Clone[this.count % 2];

    public double[] TopProfile => this.topProfile_Clone;

    public double[] LaserProfile => this.laserProfile_Clone;

    public double[] LaserCordination => this.laserCordination_Clone;

    public JSEventArgs()
    {
      this.depthImage_Clone = new float[2][][];
      for (int index1 = 0; index1 < this.depthImage_Clone.Length; ++index1)
      {
        this.depthImage_Clone[index1] = new float[957][];
        for (int index2 = 0; index2 < 957; ++index2)
          this.depthImage_Clone[index1][index2] = new float[1500];
      }
      this.topProfile_Clone = new double[957];
      this.laserProfile_Clone = new double[957];
      this.laserCordination_Clone = new double[20];
    }

    public void SetData(
      double glassDepth,
      double laserHeight,
      double referencePosition,
      float[][] depthImage,
      double[] topProfile,
      double[] laserProfile,
      double[] laserCordinatoin)
    {
      this.GlassDepth = glassDepth;
      this.LaserHeight = laserHeight;
      this.ReferencePosition = referencePosition;
      for (int index = 0; index < depthImage.Length; ++index)
        Array.Copy((Array) depthImage[index], (Array) this.depthImage_Clone[this.count % 2][index], 1500);
      Array.Copy((Array) topProfile, (Array) this.topProfile_Clone, 957);
      Array.Copy((Array) laserProfile, (Array) this.laserProfile_Clone, 957);
      Array.Copy((Array) laserCordinatoin, (Array) this.laserCordination_Clone, 20);
    }
  }
}
