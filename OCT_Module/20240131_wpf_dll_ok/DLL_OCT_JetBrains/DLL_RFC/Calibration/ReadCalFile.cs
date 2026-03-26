// Type: DLL_RFC.Calibration.ReadCalFile
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DLL_RFC.Calibration
{
  internal class ReadCalFile
  {
    private InputParameters para;
    private string calFilePath;
    private string calFileFolder;
    private string calFileName = "mirrorCalData.bin"; // Calibration 파일
    private float[] rawData_X;
    private float[] rawData_Y;
    private float[] calData;
    private bool isCalFileCorrect = true; // 파일 정상 여부 체크

    public float[] CalData => this.calData; // 출력 : 보정용 배열

    public ReadCalFile(InputParameters para)
    {
      this.para = para;
      this.calFileFolder = para.CalFileFolder;
      this.calFilePath = this.calFileFolder + "\\" + this.calFileName;
      this.rawData_X = new float[(long) para.BScan - (long) para.NumberOfCutLength]; // X 방향 원본 캘리브레이션 데이터
      this.rawData_Y = new float[(long) para.BScan - (long) para.NumberOfCutLength]; // Y 방향 원본 캘리브레이션 데이터
      this.calData = new float[((long) para.BScan - (long) para.NumberOfCutLength) * 2L]; // 최종 보정 데이터(X+Y 결합)
      try
      {
        this.ReadFile();
        this.GetCalData();
      }
      catch
      {
        Console.WriteLine("Calibration file is incorrect.");
        Console.WriteLine("Calibration values become zero.");
        for (int index = 0; index < this.calData.Length; ++index)
          this.calData[index] = 0.0f;
        this.isCalFileCorrect = false;
      }
      finally
      {
        if (this.isCalFileCorrect)
          Console.WriteLine("Reading calibration values from " + para.CalFileFolder + "\\" + this.calFileName);
      }
    }

    public void UpdateCalFile(string calFile)
    {
      try
      {
        this.UpdateCalFilePath(calFile);
        this.ReadFile();   // 파일 읽기
        this.GetCalData(); // 보정 데이터 생성
      }
      catch
      {
        Console.WriteLine("Calibration file is incorrect.");
        Console.WriteLine("Calibration values become zero.");
        for (int index = 0; index < this.calData.Length; ++index)
          this.calData[index] = 0.0f; // 모든 calData -> 0 (보정 비활성화 상태)
        this.isCalFileCorrect = false;
      }
      finally
      {
        if (this.isCalFileCorrect)
          Console.WriteLine("Updated cal values from " + this.calFilePath);
      }
    }

    private bool UpdateCalFilePath(string calFile)
    {
      this.calFilePath = calFile;
      return true;
    }

    private void ReadFile()
    {
      using (BinaryReader binaryReader = new BinaryReader((Stream) new FileStream(this.calFilePath, FileMode.Open)))
      {
        for (int index = 0; index < this.rawData_X.Length; ++index)
          this.rawData_X[index] = binaryReader.ReadSingle();
        for (int index = 0; index < this.rawData_Y.Length; ++index)
          this.rawData_Y[index] = binaryReader.ReadSingle();
      }
    }

    private void GetCalData()
    {
      float num1 = ((IEnumerable<float>) this.rawData_X).Min(); // 최소값 제거 (base offset 제거)
      float num2 = ((IEnumerable<float>) this.rawData_Y).Min(); // 최소값 제거 (base offset 제거)
      for (int index = 0; index < this.rawData_X.Length; ++index)
      {
        this.calData[index] = this.rawData_X[index] - num1;
        this.calData[index + this.rawData_X.Length] = this.rawData_Y[index] - num2;
      }
    }
  }
}
