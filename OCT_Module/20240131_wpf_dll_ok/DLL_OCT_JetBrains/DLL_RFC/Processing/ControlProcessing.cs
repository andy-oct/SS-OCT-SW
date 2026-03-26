// Type: DLL_RFC.Processing.ControlProcessing
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using System;
using System.Runtime.InteropServices;

namespace DLL_RFC.Processing
{
  internal class ControlProcessing
  {
    private InputParameters para;     // 입력 파라미터 (B-scan 수, depth index 등 OCT 설정값)
    private ConvertByteToInt convert; // byte 데이터를 int로 변환, Digitizer raw data → FFT 입력용 int 배열

    private float[] fftResult = new float[8192000]; // 8192000 = 4096 samples × 2000 A-scan
    private Average average;    // A-Scan 평균처리(noise 감소)
    public GetDepth depth;      // Depth profile 계산
    private GetMirror mirror;   // Mirror calibration
    private int count;          // Mirror calibration frame index 

    [DllImport("Transform_20230226.dll")] // FFT 라이브러리 호출
    // inputData : interferogram, outputData : FFT spectrum, bscan : number of A-Scans, cscan : frame count
    private static extern void Result_1(int[] inputData, float[] outputData, int bscan, int cscan);

    public ControlProcessing(InputParameters para)
    {
      this.para = para; // 파라메터 저장
      this.convert = new ConvertByteToInt(); // Digitizer 데이터 변환 객체 생성
      this.average = new Average(para);      // 평균 객체 생성
      this.depth = new GetDepth(para);       // Depth profile 객체 생성
      this.depth.inspectionStartIndex = para.InspectionStartIndex; // Depth 계산 시작 위치 설정(표면반사(Surface reflection) 제거)
      this.mirror = new GetMirror(para);     // Mirror calibration 객체 생성
    }

    // data : digitizer raw data, calData : calibration data
    public void GetResult(byte[] data, float[] calData)
    {
      this.convert.RunConvert(data); // byte[] -> int[] 형 변환
      ControlProcessing.Result_1(this.convert.IntData, this.fftResult, (int) this.para.BScan * 2, 1); // FFT 수행(Interferogram->depth spectrum)
      this.average.RunAverage(this.fftResult); // Averaging(Noise 감소)
      this.depth.GetDepthProfile(this.average.AverageResult, calData); // Depth Profile 계산
    }

    // Mirror Calibration 함수, (for k-linearization calibration, dispersion calibration)
    public void GetMirrorResult(byte[] data)
    {
      Console.WriteLine("Running Mirror Calibration");
      this.convert.RunConvert(data); // byte[] -> int[] 형 변환
      ControlProcessing.Result_1(this.convert.IntData, this.fftResult, (int) this.para.BScan * 2, 1); // FFT 수행(Interferogram->depth spectrum)
      this.average.RunAverage(this.fftResult); // Averaging(Noise 감소)
      this.mirror.GetMirrorProfile(this.average.AverageResult, this.count % 10); // Mirror Profile 추출
      ++this.count;
    }
  }
}
