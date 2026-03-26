// Type: DLL_RFC.Manual.ControlManualTest
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using DLL_RFC.Calibration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace DLL_RFC.Manual
{
  internal class ControlManualTest
  {
    private string fileName;
    private InputParameters para;
    private ReadCalFile readCalFile;
    private float[] imageData_float;
    private ManualProcessing manualProcessing;
    private double[] topProfile = new double[957];
    private double[] laserProfile = new double[957];
    private Image image;
    private byte[] imageData;

    public ControlManualTest(InputParameters para)
    {
      this.para = para;
      this.imageData_float = new float[1435500];
    }

    public double GlassDepth => this.manualProcessing.GlassDepth * (double) this.para.MicroPerPixel;

    public double LaserHeight => this.manualProcessing.LaserHeight * (double) this.para.MicroPerPixel;

    public double ReferencePosition => this.manualProcessing.ReferencePosition;

    public float[][] DepthImage => this.manualProcessing.RawData;

    public double[] TopProfile
    {
      get
      {
        for (int index = 0; index < this.topProfile.Length; ++index)
          this.topProfile[index] = this.manualProcessing.TopProfile[index] * (double) this.para.MicroPerPixel;
        return this.topProfile;
      }
    }

    public double[] LaserProfile
    {
      get
      {
        for (int index = 0; index < this.laserProfile.Length; ++index)
          this.laserProfile[index] = this.manualProcessing.LaserProfile[index] * (double) this.para.MicroPerPixel;
        return this.laserProfile;
      }
    }

    public double[] LaserCordination => this.manualProcessing.LaserCordination;

    public void Start(string fileName)
    {
      this.manualProcessing = new ManualProcessing(this.para);
      this.ConvertBmpToByteArray(fileName);
      this.manualProcessing.GetDepthProfile(this.imageData_float);
    }

    private void ConvertBmpToByteArray(string fileName)
    {
      this.image = Image.FromFile(fileName);
      using (MemoryStream memoryStream = new MemoryStream())
      {
        this.image.Save((Stream) memoryStream, ImageFormat.Bmp);
        this.imageData = memoryStream.ToArray();
      }
      for (int index = 0; index < this.imageData_float.Length; ++index)
        this.imageData_float[index] = (float) this.imageData[index + 1078];
    }
  }
}
