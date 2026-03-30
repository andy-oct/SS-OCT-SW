// Type: DLL_RFC.DAQ.ScannerFunction
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

// ============================================================
// ScannerFunction 클래스
// ============================================================
// 갈바노 스캐너(Galvo Mirror) 구동을 위한 아날로그 출력 파형을 생성하는
// 유틸리티 클래스. 모든 메서드가 static이며, ScannerOutput 생성자에서 호출된다.
//
// [갈바노 스캐너 동작]
//   ao0 (X축): 전압에 비례하여 X 방향 미러 회전
//   ao1 (Y축): 전압에 비례하여 Y 방향 미러 회전
//   기본 범위: ±Amplitude(0.4V) + Offset(0.0V) = -0.4V ~ +0.4V
//
// [파형 종류]
//   1) TriangleWave       : 단일 삼각파 (기본 빌딩 블록)
//   2) LineScan_XY        : X축 삼각파 + Y축 고정 (Direction=true)
//   3) LineScan_YX        : X축 고정 + Y축 삼각파 (Direction=false)
//   4) CScan              : X축 삼각파 반복 + Y축 계단식 이동 (3D 스캔)
//
// [왕복 스캔 구조]
//   모든 파형은 bscan × 2 길이 (Forward + Backward):
//     Forward  (0 ~ bscan-1):      -Amp → +Amp (전진, 유효 데이터)
//     Backward (bscan ~ bscan*2-1): +Amp → -Amp (복귀, Average에서 평균 사용)
//
// [삼각파 형태]
//   전압 (V)
//   +0.4 ─              /\
//        │            /    \
//    0.0 ─          /        \
//        │        /            \
//   -0.4 ─      /                \
//               |← Forward →|← Backward →|
//               0           1000         2000
// ============================================================

namespace DLL_RFC.DAQ
{
  internal class ScannerFunction
  {
    // ============================================================
    // [1] 기본 삼각파 생성
    // ============================================================
    /// <summary>
    /// 단일 축 삼각파를 생성한다.
    ///
    /// [공식] value = (index/bscan - 0.5) × 2.0 × amplitude + offset
    ///   index=0:      (-0.5) × 2 × amp + offset = -amp + offset
    ///   index=bscan/2: (0.0) × 2 × amp + offset =       offset  (중앙)
    ///   index=bscan-1: (+0.5) × 2 × amp + offset = +amp + offset
    ///
    /// Forward(0~999)와 Backward(1999~1000)는 대칭 복사:
    ///   triangeWave[length-1-i] = triangeWave[i]
    /// </summary>
    /// <param name="bscan">A-scan 수 (1000)</param>
    /// <param name="amplitude">전압 진폭 (±0.4V)</param>
    /// <param name="offset">중심 전압 오프셋 (0.0V)</param>
    /// <returns>삼각파 배열 [bscan × 2 = 2000]</returns>
    public static double[] GenerateTriangeWave(int bscan, double amplitude, double offset)
    {
      double[] triangeWave = new double[bscan * 2];
      for (int index = 0; index < bscan; ++index)
      {
        // Forward: -amplitude → +amplitude (선형 증가)
        triangeWave[index] = ((double) index / (double) bscan - 0.5) * 2.0 * amplitude + offset;
        // Backward: Forward의 대칭 복사 (+amplitude → -amplitude)
        triangeWave[triangeWave.Length - 1 - index] = triangeWave[index];
      }
      return triangeWave;
    }

    // ============================================================
    // [2] X축 라인 스캔 파형 (LineScan_XY)
    // ============================================================
    /// <summary>
    /// X축 빠른 스캔 + Y축 고정 파형을 생성한다.
    /// Direction == true 일 때 사용.
    ///
    /// [채널 0 (ao0, X축)] 삼각파
    ///   Forward:  -0.4V → +0.4V (1000 samples)
    ///   Backward: +0.4V → -0.4V (1000 samples)
    ///
    /// [채널 1 (ao1, Y축)] 고정값
    ///   전체 2000 samples = offset_y
    ///
    /// [스캔 패턴]
    ///   Y ↑
    ///     │  ●──────────────→ (Forward)
    ///     │  ←──────────────● (Backward)   Y = offset_y 고정
    ///     └───────────────────→ X
    ///      -0.4V             +0.4V
    /// </summary>
    /// <returns>double[2, bscan×2] - [채널, 샘플]</returns>
    public static double[,] GenerateMultiWaveforms_LineScan_XY(
      int bscan,
      double amplitude_x,
      double offset_x,
      double offset_y)
    {
      double[,] waveformsLineScanXy = new double[2, bscan * 2];

      // 채널 0 (X축): 삼각파 생성
      for (int index = 0; index < bscan; ++index)
      {
        // Forward: -amplitude_x → +amplitude_x
        waveformsLineScanXy[0, index] = ((double) index / (double) bscan - 0.5) * 2.0 * amplitude_x + offset_x;
        // Backward: 대칭 복사
        waveformsLineScanXy[0, bscan * 2 - 1 - index] = waveformsLineScanXy[0, index];
      }

      // 채널 1 (Y축): 고정값 (offset_y)
      for (int index = 0; index < bscan * 2; ++index)
        waveformsLineScanXy[1, index] = offset_y;

      return waveformsLineScanXy;
    }

    // ============================================================
    // [3] Y축 라인 스캔 파형 (LineScan_YX)
    // ============================================================
    /// <summary>
    /// Y축 빠른 스캔 + X축 고정 파형을 생성한다.
    /// Direction == false 일 때 사용.
    /// Mirror Calibration에서 Y 방향 보정 데이터 생성 시에도 사용.
    ///
    /// [채널 0 (ao0, X축)] 고정값
    ///   전체 2000 samples = offset_x
    ///
    /// [채널 1 (ao1, Y축)] 삼각파
    ///   Forward:  -0.4V → +0.4V (1000 samples)
    ///   Backward: +0.4V → -0.4V (1000 samples)
    ///
    /// [스캔 패턴]
    ///   Y ↑ +0.4V  ●
    ///     │         │ (Forward)
    ///     │         │
    ///     │  -0.4V  ●
    ///     │         │ (Backward)
    ///     │  +0.4V  ●
    ///     └───────────────────→ X
    ///          offset_x 고정
    /// </summary>
    /// <returns>double[2, bscan×2] - [채널, 샘플]</returns>
    public static double[,] GenerateMultiWaveforms_LineScan_YX(
      int bscan,
      double amplitude_y,
      double offset_x,
      double offset_y)
    {
      double[,] waveformsLineScanYx = new double[2, bscan * 2];

      // 채널 1 (Y축): 삼각파 생성
      for (int index = 0; index < bscan; ++index)
      {
        // Forward: -amplitude_y → +amplitude_y
        waveformsLineScanYx[1, index] = ((double) index / (double) bscan - 0.5) * 2.0 * amplitude_y + offset_y;
        // Backward: 대칭 복사
        waveformsLineScanYx[1, bscan * 2 - 1 - index] = waveformsLineScanYx[1, index];
      }

      // 채널 0 (X축): 고정값 (offset_x)
      for (int index = 0; index < bscan * 2; ++index)
        waveformsLineScanYx[0, index] = offset_x;

      return waveformsLineScanYx;
    }

    // ============================================================
    // [4] 3D 볼륨 스캔 파형 (C-scan)
    // ============================================================
    /// <summary>
    /// X축 삼각파 반복 + Y축 계단식 이동으로 3D 볼륨 스캔 파형을 생성한다.
    /// BoolCScan == true 일 때 사용.
    ///
    /// [채널 0 (ao0, X축)] 삼각파 반복
    ///   cscan 라인마다 동일한 X 삼각파를 반복:
    ///   라인 0:  -0.4V → +0.4V → -0.4V  (index 0~1999)
    ///   라인 1:  -0.4V → +0.4V → -0.4V  (index 2000~3999)
    ///   ...
    ///   라인 99: -0.4V → +0.4V → -0.4V  (index 198000~199999)
    ///
    /// [채널 1 (ao1, Y축)] 계단식 증가
    ///   Y 위치 공식: (2.0/cscan × k - 1.0) × amplitude_y + offset_y
    ///   각 라인(2000 samples) 동안 Y값 고정:
    ///     라인 0:   Y = -0.400V
    ///     라인 50:  Y =  0.000V
    ///     라인 99:  Y = +0.392V
    ///
    /// [스캔 패턴 - 상단에서 본 모습]
    ///   Y ↑ +0.4V  ──────→ ←────── 라인 99
    ///     │          ...
    ///     │  0.0V  ──────→ ←────── 라인 50
    ///     │          ...
    ///     │ -0.4V  ──────→ ←────── 라인 0
    ///     └─────────────────────→ X
    ///            -0.4V    +0.4V
    ///
    /// 총 파형: bscan × 2 × cscan = 1000 × 2 × 100 = 200,000 samples/채널
    /// </summary>
    /// <returns>double[2, bscan×2×cscan] - [채널, 샘플]</returns>
    public static double[,] GenerateMultiWaveforms_CScan(
      int bscan,
      double amplitude_x,
      double offset_x,
      double amplitude_y,
      double offset_y,
      int cscan)
    {
      double[,] multiWaveformsCscan = new double[2, bscan * 2 * cscan];

      // 채널 0 (X축): cscan 라인마다 동일한 삼각파 반복
      for (int index1 = 0; index1 < cscan; ++index1)
      {
        for (int index2 = 0; index2 < bscan; ++index2)
        {
          // Forward: 각 라인의 전진 구간
          multiWaveformsCscan[0, index2 + bscan * 2 * index1] = ((double) index2 / (double) bscan - 0.5) * 2.0 * amplitude_x + offset_x;
          // Backward: 각 라인의 복귀 구간 (대칭 복사)
          multiWaveformsCscan[0, bscan * 2 - 1 - index2 + bscan * 2 * index1] = multiWaveformsCscan[0, index2];
        }
      }

      // 채널 1 (Y축): 계단식 Y 위치 계산
      // 각 라인의 Y 위치를 사전 계산
      double[] numArray = new double[cscan];
      for (int index = 0; index < numArray.Length; ++index)
        // Y 위치: -amplitude_y → +amplitude_y (cscan 단계로 균등 분할)
        numArray[index] = (2.0 / (double) numArray.Length * (double) index - 1.0) * amplitude_y + offset_y;

      // 각 라인(2000 samples) 동안 해당 Y 위치 값으로 고정
      for (int index3 = 0; index3 < numArray.Length; ++index3)
      {
        for (int index4 = 0; index4 < bscan * 2; ++index4)
          multiWaveformsCscan[1, index4 + index3 * bscan * 2] = numArray[index3];
      }

      return multiWaveformsCscan;
    }
  }
}
