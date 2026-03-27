// Type: DLL_RFC.Processing.GetMirror
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

// ============================================================
// GetMirror 클래스
// ============================================================
// Mirror Calibration 데이터를 생성하는 클래스.
// 평면 거울(Mirror)을 OCT로 스캔하여 X/Y 방향별 Depth 위치 오차를 측정하고,
// 이를 보정 데이터(mirrorCalData.bin)로 저장한다.
//
// [목적]
//   갈바노 스캐너는 각도에 따라 광경로 길이가 달라지므로,
//   평면 거울임에도 OCT 이미지에서 곡면처럼 보이는 왜곡이 발생한다.
//   이 왜곡을 보정하기 위해 Mirror의 peak 위치를 기준 데이터로 사용한다.
//
// [처리 흐름]
//   RFCInspection.StartMirrorCalibration()에서 호출:
//     1) X 방향 스캔 5회 (count 0~4) → mirrorProfileX에 peak 위치 누적
//     2) Y 방향 스캔 5회 (count 5~9) → mirrorProfileY에 peak 위치 누적
//     3) count == 9 에서:
//        - X/Y 각각 5회 평균 계산
//        - Median 필터 적용 (노이즈 제거)
//        - mirrorCalData.bin 파일로 저장 [X 957개 + Y 957개 = 1914 float]
//
// [보정 원리]
//   평면 거울의 실제 Depth는 모든 A-scan에서 동일해야 하지만,
//   갈바노 각도에 따라 peak 위치가 달라진다.
//   → 이 차이값(calData)을 GetDepth.GetRawData()에서 offset으로 적용하여
//     각 A-scan의 Depth 시작 위치를 보정한다.
//
// [보정 전/후 비교]
//   보정 전 (Mirror 스캔 결과):
//     A-scan:  0    100   200   300   ... 957
//     Peak:   120   115   110   108   ... 125   ← 갈바노 각도에 따라 변동
//
//   보정 후 (calData 적용):
//     A-scan:  0    100   200   300   ... 957
//     Peak:    0     0     0     0    ...  0    ← 모든 A-scan이 동일한 기준선
// ============================================================

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
    // ============================================================
    // [기본 설정]
    // ============================================================
    private InputParameters para;              // OCT 시스템 파라미터
    private OnlineMedianFilter medianFilter;   // Median 필터 (커널 크기 = 3)

    // ============================================================
    // [FFT 데이터 버퍼]
    // ============================================================
    private float[][] fftResultCopy = new float[957][]; // FFT 결과 2D 복사본 [957 A-scans][1500 depth points]

    // ============================================================
    // [Mirror Profile 데이터 - X 방향]
    // ============================================================
    private double[] mirrorProfileX;              // X 방향 peak 위치 누적 합 [957] (5회 스캔 누적)
    private double[] mirrorProfileXMedian;        // X 방향 Median 필터 적용 결과 (double)
    private float[] mirrorProfileXMedianFloat;    // X 방향 최종 결과 (float, 파일 저장용)

    // ============================================================
    // [Mirror Profile 데이터 - Y 방향]
    // ============================================================
    private double[] mirrorProfileY;              // Y 방향 peak 위치 누적 합 [957] (5회 스캔 누적)
    private double[] mirrorProfileYMedian;        // Y 방향 Median 필터 적용 결과 (double)
    private float[] mirrorProfileYMedianFloat;    // Y 방향 최종 결과 (float, 파일 저장용)

    // ============================================================
    // [상태 변수]
    // ============================================================
    private int count;       // 현재 프레임 인덱스 (0~9, 10회 측정 사이클)
    private float maxValue;  // 각 A-scan에서 검출된 Max peak 값

    // ============================================================
    // [생성자]
    // ============================================================
    /// <summary>
    /// GetMirror 생성자. 모든 배열 버퍼를 사전 할당한다.
    /// fftResultCopy: [957][1500] - 각 A-scan의 depth 데이터 복사본
    /// mirrorProfileX/Y: [957] - X/Y 방향 peak 위치 누적 배열
    /// </summary>
    public GetMirror(InputParameters para)
    {
      this.para = para;
      this.medianFilter = new OnlineMedianFilter(3);

      // FFT 결과 복사본 버퍼 할당 (957 A-scans × 1500 depth points)
      for (int index = 0; index < this.fftResultCopy.Length; ++index)
        this.fftResultCopy[index] = new float[1500];

      // X/Y 방향 프로파일 배열 할당 (각 957개)
      this.mirrorProfileX = new double[this.fftResultCopy.Length];
      this.mirrorProfileY = new double[this.fftResultCopy.Length];
      this.mirrorProfileXMedian = new double[this.fftResultCopy.Length];
      this.mirrorProfileYMedian = new double[this.fftResultCopy.Length];
      this.mirrorProfileXMedianFloat = new float[this.fftResultCopy.Length];
      this.mirrorProfileYMedianFloat = new float[this.fftResultCopy.Length];
    }

    // ============================================================
    // [메인 처리] Mirror Profile 추출
    // ============================================================
    /// <summary>
    /// Mirror Calibration의 메인 처리 함수.
    /// RFCInspection에서 10회 호출된다 (count 0~9).
    ///
    /// [호출 시퀀스]
    ///   count 0: mirrorProfileX/Y 초기화 → X 방향 peak 위치 누적
    ///   count 1~4: X 방향 peak 위치 계속 누적 (총 5회)
    ///   count 5~8: Y 방향 peak 위치 누적 (총 4회)
    ///   count 9: Y 방향 마지막 누적 → 5회 평균 → Median 필터 → 파일 저장
    ///
    /// [출력 파일] mirrorCalData.bin (para.SaveFileName)
    ///   구조: [X 보정값 957개 (float)] + [Y 보정값 957개 (float)]
    ///   총 크기: 957 × 2 × 4 bytes = 7,656 bytes
    /// </summary>
    /// <param name="fftResult">FFT 결과 (1D float 배열, 크기 = 957 × 4096)</param>
    /// <param name="count">현재 프레임 인덱스 (0~9)</param>
    public void GetMirrorProfile(float[] fftResult, int count)
    {
      this.count = count;

      // 10회 사이클 시작 시 누적 배열 초기화
      if (count % 10 == 0)
      {
        for (int index = 0; index < this.fftResultCopy.Length; ++index)
        {
          this.mirrorProfileX[index] = 0.0;
          this.mirrorProfileY[index] = 0.0;
        }
      }

      // [Step 1] FFT 1D → 2D 복사 (Calibration 보정 없이 원본 그대로)
      this.CopyData(fftResult);

      // [Step 2] count에 따라 X 또는 Y 방향 peak 위치 누적
      this.GetXYProfile();

      // 10회 사이클 완료 전이면 여기서 종료 (누적만 수행)
      if (count != 9)
        return;

      // ============================================================
      // [Step 3] count == 9: 5회 평균 → Median 필터 → 파일 저장
      // ============================================================

      // 5회 누적 합 → 평균 (각 A-scan별)
      for (int index = 0; index < this.fftResultCopy.Length; ++index)
      {
        this.mirrorProfileX[index] = this.mirrorProfileX[index] / 5.0;
        this.mirrorProfileY[index] = this.mirrorProfileY[index] / 5.0;
      }

      // Median 필터 적용 (A-scan 간 노이즈 제거)
      this.mirrorProfileXMedian = ((OnlineFilter) this.medianFilter).ProcessSamples(this.mirrorProfileX);
      this.mirrorProfileYMedian = ((OnlineFilter) this.medianFilter).ProcessSamples(this.mirrorProfileY);

      // double → float 변환 (파일 저장용)
      for (int index = 0; index < this.fftResultCopy.Length; ++index)
      {
        this.mirrorProfileXMedianFloat[index] = (float) this.mirrorProfileXMedian[index];
        this.mirrorProfileYMedianFloat[index] = (float) this.mirrorProfileYMedian[index];
      }

      // mirrorCalData.bin 파일 저장
      // 구조: [X 보정값 957개] + [Y 보정값 957개] = 1914 float
      // GetDepth.GetRawData()에서 calData[i] / calData[i + realBscan]으로 참조됨
      using (BinaryWriter binaryWriter = new BinaryWriter((Stream) new FileStream(this.para.SaveFileName, FileMode.Create)))
      {
        // X 방향 보정값 (calData[0..956])
        for (int index = 0; index < this.fftResultCopy.Length; ++index)
          binaryWriter.Write(this.mirrorProfileXMedianFloat[index]);
        // Y 방향 보정값 (calData[957..1913])
        for (int index = 0; index < this.fftResultCopy.Length; ++index)
          binaryWriter.Write(this.mirrorProfileYMedianFloat[index]);
      }
    }

    // ============================================================
    // [데이터 복사] FFT 1D → 2D 배열 변환
    // ============================================================
    /// <summary>
    /// 1D FFT 결과를 2D 배열 [957][1500]으로 복사한다.
    ///
    /// GetDepth.GetRawData()와 달리 calData 보정을 적용하지 않는다.
    /// (Mirror Calibration은 보정 데이터를 "생성"하는 과정이므로)
    ///
    /// 복사 시작 위치: i × 4096 + inspectionStartIndex(700)
    /// 복사 길이: 1500 depth points
    /// </summary>
    private void CopyData(float[] floats)
    {
      for (int index = 0; index < 957; ++index)
        Array.Copy((Array) floats, index * 4096 + this.para.InspectionStartIndex, (Array) this.fftResultCopy[index], 0, this.fftResultCopy[0].Length);
    }

    // ============================================================
    // [Peak 위치 검출] X/Y 방향 Mirror Peak 누적
    // ============================================================
    /// <summary>
    /// 각 A-scan에서 FFT magnitude의 Max peak 위치(depth index)를 찾아
    /// X 또는 Y 방향 배열에 누적한다.
    ///
    /// count 0~4 (X 방향 스캔):
    ///   mirrorProfileX[i] += Max peak의 depth index
    ///   → 5회 누적 후 GetMirrorProfile()에서 /5.0으로 평균
    ///
    /// count 5~9 (Y 방향 스캔):
    ///   mirrorProfileY[i] += Max peak의 depth index
    ///   → 5회 누적 후 GetMirrorProfile()에서 /5.0으로 평균
    ///
    /// 평면 거울이므로 이상적으로는 모든 A-scan에서 동일한 depth에 peak가 나타나야 하지만,
    /// 갈바노 각도에 따른 광경로 차이로 인해 A-scan마다 peak 위치가 달라진다.
    /// 이 차이가 곧 보정값(calData)이 된다.
    /// </summary>
    private void GetXYProfile()
    {
      if (this.count < 5)
      {
        // X 방향 스캔 (count 0~4): 각 A-scan의 Max peak depth index 누적
        for (int index = 0; index < this.fftResultCopy.Length; ++index)
        {
          this.maxValue = ((IEnumerable<float>) this.fftResultCopy[index]).Max();
          this.mirrorProfileX[index] += (double) Array.IndexOf<float>(this.fftResultCopy[index], this.maxValue);
        }
      }
      else
      {
        // Y 방향 스캔 (count 5~9): 각 A-scan의 Max peak depth index 누적
        for (int index = 0; index < this.fftResultCopy.Length; ++index)
        {
          this.maxValue = ((IEnumerable<float>) this.fftResultCopy[index]).Max();
          this.mirrorProfileY[index] += (double) Array.IndexOf<float>(this.fftResultCopy[index], this.maxValue);
        }
      }
    }
  }
}
