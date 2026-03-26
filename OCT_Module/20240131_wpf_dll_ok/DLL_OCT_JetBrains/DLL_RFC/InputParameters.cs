// Type: DLL_RFC.InputParameters
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using MathNet.Numerics;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;
using System.Reflection.Emit;

namespace DLL_RFC
{
  public class InputParameters
  {
    public readonly uint BScan = 1000; // Sample 개수(X 방향)
    // Full FFT = 8192, Half FFT = 4096, 깊이 방향으로 유효한 데이터는 앞쪽 4096 포인트만 사용
    // FFT = 8192 : Depth sampling 충분, 연산 속도 빠름, GPU / FFT library 최적화, 메모리 적절
    public readonly int HalfFFTLength = 4096;
    // 스캔 시작 부분에서 버릴 데이터 길이 (초기 43개 A-scan 버림)
    // (스캐너 시작 구간에서 galvo settling, mirror vibration, laser sweep unstable 현상이 발생하기 때문)
    public readonly int NumberOfCutLength = 43;
    public double Frequency = 10.0; // Scanner 동작 주파수(Hz) : Scan 속도
    public int NumberOfLine = 5;    // Scan 라인 수
    public int IdleTimerTick = 60;  // Timer Tick(ms)
    public bool UsingTimer = true;  // Timer 사용 여부
    public string CalFileFolder = "D:\\OCT_Module"; // Calibration 파일 위치
    public int InspectionStartIndex = 700; // 실제 검사 시작 Index (앞쪽 데이터에 노이즈가 많아서 제외)
    public string SaveFileName;     // 결과 저장 파일 이름

    public uint CScan { get; set; } = 100;          // C-Scan Sample 개수(Y 방향) : 3D Scan Depth

    public int NumberOfAverage { get; set; } = 2;   // Averaging 횟수

    public double Amplitude_X { get; set; } = 0.4;  // ±0.4V 범위로 갈바노 구동

    public double Amplitude_Y { get; set; } = 0.4;  // ±0.4V 범위로 갈바노 구동

    public double Offset_X { get; set; } = 0.0;     // 갈바노 X offset

    public double Offset_Y { get; set; } = 0.0;     // 갈바노 Y offset

    public float Threshold { get; set; } = 100f;    // 신호 검출 Threshold

    public bool Direction { get; set; } = true;     // Scan 방향, true : lineScan_XY, false : lineScan_YX

    public float MicroPerPixel { get; set; } = 0.79f; // Pixel → 실제 거리 변환, 1 pixel -> 0.79 μm

    public bool BoolCScan { get; set; } = false; // C-Scan 모드 사용 여부

    public int SelectHeightPoint { get; set; } = 10; // Height 계산에 사용할 포인트 개수

    public int NumberOfMedianFilter { get; set; } = 3; // Median filter 반복 횟수

    public int StartIndexBelowTopPosition { get; set; } = 100; // Top surface 아래에서 분석 시작 위치

    public int StartIndex { get; set; } = 100; // Depth 분석 시작 index ?

    public int EndIndex { get; set; } = 0; // Depth 분석 종료 index ?
    }
}
