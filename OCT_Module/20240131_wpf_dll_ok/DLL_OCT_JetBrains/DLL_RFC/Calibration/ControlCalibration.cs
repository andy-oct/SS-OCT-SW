// Type: DLL_RFC.Calibration.ControlCalibration
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

namespace DLL_RFC.Calibration
{
  internal class ControlCalibration
  {
    private InputParameters para;
    private ReadCalFile readCal;
    public float[] CalData;

    public ControlCalibration(InputParameters para)
    {
      this.para = para;
      this.readCal = new ReadCalFile(para);
      this.CalData = this.readCal.CalData;
    }

    public void UpdateCalfile(string calFile) // Calibration 업데이트
    {
      this.readCal.UpdateCalFile(calFile);
      this.CalData = this.readCal.CalData;
    }
  }
}
