// Type: DLL_RFC.Processing.GetDepth
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

// ============================================================
// GetDepth 클래스
// ============================================================
// SS-OCT FFT 결과로부터 유리 상면(Top), 하면(Bottom), 레이저 위치(Center)를
// 검출하고, 유리 두께(GlassDepth)와 레이저 높이(LaserHeight)를 계산한다.
//
// [처리 파이프라인]
//   FFT 결과 (1D: 957 x 4096)
//     → GetRawData()      : 2D 배열 변환 + Calibration 보정
//     → GetTopProfile()   : 유리 상면 검출 (앞쪽 300 내 Max peak)
//     → GetBottomProfile(): 유리 하면 검출 (Top+550 부근 Max peak)
//     → GetCenterProfile(): 레이저 위치 검출 (Threshold 초과 첫 지점)
//     → GetNumerics()     : GlassDepth, LaserHeight 계산
//
// [OCT 단면 구조 (Depth 방향)]
//   Index 0 ────────────────────────────
//            노이즈 영역 (inspectionStartIndex=700까지 제외)
//            ★ Top Surface (유리 상면)     ← temp1[300] 내 Max peak
//            ...
//            ★ Laser Position              ← temp3[480] 내 Threshold 초과 첫 지점
//            ...
//            ★ Bottom Surface (유리 하면)  ← temp2[300] 내 Max peak
//   Index 1500 ─────────────────────────
// ============================================================

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
    // ============================================================
    // [기본 설정]
    // ============================================================
    private InputParameters para;          // OCT 시스템 파라미터 (BScan, Threshold, MicroPerPixel 등)
    private int realBscan;                 // 실제 유효 A-scan 수 = BScan(1000) - NumberOfCutLength(43) = 957
    public float[] calData;                // Mirror Calibration 보정 데이터 (X방향 957 + Y방향 957 = 1914개)
    public int inspectionStartIndex;       // Depth 시작 index (=700, 표면 반사 노이즈 제외)

    // ============================================================
    // [Raw 데이터 버퍼]
    // ============================================================
    private byte[] rawData_byte;           // [미사용] byte 형식 raw 데이터 (레거시)
    private float[][] rawData_float;       // FFT 결과 2D 배열 [957][1500] - 처리용 (프로파일 추출에 사용)
    private float[][] rawData_float_ori;   // FFT 결과 2D 배열 [957][1500] - 원본 보관용 (이미지 표시에 사용)

    // ============================================================
    // [필터]
    // ============================================================
    private OnlineMedianFilter medianFilter; // MathNet Median 필터 (커널 크기 = NumberOfMedianFilter = 3)

    // ============================================================
    // [프로파일 데이터]
    // ============================================================
    private double[] topProfile;           // 유리 상면 위치 (raw) [957] - 각 A-scan의 Top peak index
    private double[] topProfile_filter;    // 유리 상면 위치 (Median 필터 적용) [957]
    private double[] bottomProfile;        // 유리 하면 위치 (raw) [957]
    private double[] bottomProfile_filter; // 유리 하면 위치 (Median 필터 적용) [957]
    private double[] laserProfile;         // 레이저 위치 (raw) [957]
    private double[] laserProfile_filter;  // 레이저 위치 (Median 필터 적용) [957]
    private double[] laserProfile_filter_sort; // 레이저 위치 (오름차순 정렬) - 최소값 N개 선택용

    // ============================================================
    // [Top Surface 검출용 임시 변수]
    // ============================================================
    private float maxValue1;               // temp1 내 최대 FFT 크기 (Top peak 값)
    private float[] temp1 = new float[300]; // 각 A-scan의 앞쪽 300개 depth 샘플 (Top 탐색 영역)
    private double topProfile_average;     // 상면 위치의 평균값 (pixel)

    // ============================================================
    // [Bottom Surface 검출용 임시 변수]
    // ============================================================
    private float maxValue2;               // temp2 내 최대 FFT 크기 (Bottom peak 값)
    private float bottomStartIndex;        // Bottom 탐색 시작 위치 = topProfile_average + 630 - 80
    private float[] temp2 = new float[300]; // Bottom 탐색용 300개 depth 샘플
    private double bottomProfile_average;  // 하면 위치의 평균값 (pixel)

    // ============================================================
    // [Laser(Center) Position 검출용 임시 변수]
    // ============================================================
    private float maxValue3;               // Threshold 초과한 첫 번째 신호값
    private float centerStartIndex;        // Center 탐색 시작 위치 = Top 평균 + StartIndexBelowTopPosition(100)
    private float[] temp3;                 // Center 탐색 영역 [580 - StartIndexBelowTopPosition = 480]
    private List<float> temp4 = new List<float>(); // Threshold 초과 신호를 임시 저장하는 리스트
    private float linearThreshold;         // dB → Linear 변환된 Threshold = 10^(Threshold/20)
    private int count1;                    // GetCenterProfile 호출 횟수 (10번째에서 디버그 파일 저장)

    // ============================================================
    // [최종 높이 계산용]
    // ============================================================
    private double[] selectedHeight;       // 정렬된 laser profile에서 선택된 최소 N개 높이값

    // ============================================================
    // [미사용 변수 (레거시/예약)]
    // ============================================================
    private int tempX;
    private int tempY;
    private int x_scope = 5;
    private int x_decrease = 0;
    private int y_increase = 0;
    private int startIndexForContour;
    private int endIndexForContour;

    // ============================================================
    // [Public 프로퍼티]
    // ============================================================

    /// <summary>
    /// Raw FFT 데이터를 dB 스케일로 변환하여 반환 (이미지 표시용).
    /// 변환식: dB = 20 * log10(linear)
    /// 병렬 처리(Parallel.For)로 성능 최적화.
    /// 주의: 호출 시 rawData_float_ori가 dB로 덮어쓰여지므로 1회만 호출해야 함.
    /// </summary>
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

    /// <summary>유리 상면 프로파일 [957] (Median 필터 적용, pixel 단위)</summary>
    public double[] TopProfile => this.topProfile_filter;

    /// <summary>유리 하면 프로파일 [957] (Median 필터 적용, pixel 단위)</summary>
    public double[] BottomProfile => this.bottomProfile_filter;

    /// <summary>레이저 위치 프로파일 [957] (Median 필터 적용, pixel 단위)</summary>
    public double[] LaserProfile => this.laserProfile_filter;

    /// <summary>유리 두께 (μm) = |Top평균 - Bottom평균| × 0.79 μm/pixel</summary>
    public double GlassDepth { get; private set; }

    /// <summary>
    /// 레이저 높이 (μm) = (626 - (Laser위치 - Top위치)) × 0.79 μm/pixel.
    /// 626 = 유리 하면 기준 거리 (설계 상수, pixel 단위).
    /// </summary>
    public double LaserHeight { get; private set; }

    /// <summary>상면 위치의 평균값 (기준면 위치, pixel 단위)</summary>
    public double ReferencePosition => ((IEnumerable<double>) this.topProfile_filter).Average();

    /// <summary>
    /// 레이저 좌표 [X0, Y0, X1, Y1, ...] - 최소 높이 N개 지점의 (A-scan index, depth index) 쌍.
    /// 크기 = SelectHeightPoint × 2.
    /// </summary>
    public double[] LaserCordination { get; private set; }

    // ============================================================
    // [생성자]
    // ============================================================
    /// <summary>
    /// GetDepth 생성자. 모든 배열 버퍼를 사전 할당한다.
    /// realBscan = BScan(1000) - NumberOfCutLength(43) = 957.
    /// 각 A-scan당 1500개의 depth 포인트를 사용한다.
    /// </summary>
    public GetDepth(InputParameters para)
    {
      this.para = para;
      // 유효 A-scan 수 = 전체 B-scan - 초기 버림 구간
      this.realBscan = (int) ((long) para.BScan - (long) para.NumberOfCutLength);

      // Raw 데이터 버퍼 할당 (957 A-scans × 1500 depth points)
      this.rawData_byte = new byte[this.realBscan * 1500];
      this.rawData_float = new float[this.realBscan][];
      this.rawData_float_ori = new float[this.realBscan][];
      for (int index = 0; index < this.rawData_float.Length; ++index)
      {
        this.rawData_float[index] = new float[1500];
        this.rawData_float_ori[index] = new float[1500];
      }

      // Median 필터 초기화 (커널 크기 = 3)
      this.medianFilter = new OnlineMedianFilter(para.NumberOfMedianFilter);

      // 프로파일 배열 할당 (각 957개)
      this.topProfile = new double[this.realBscan];
      this.topProfile_filter = new double[this.realBscan];
      this.bottomProfile = new double[this.realBscan];
      this.bottomProfile_filter = new double[this.realBscan];
      this.laserProfile = new double[this.realBscan];
      this.laserProfile_filter = new double[this.realBscan];
      this.laserProfile_filter_sort = new double[this.realBscan];

      // Center(레이저) 탐색 영역 = 580 - StartIndexBelowTopPosition(100) = 480
      this.temp3 = new float[580 - para.StartIndexBelowTopPosition];

      // 최소 높이 선택용 배열 (SelectHeightPoint = 10)
      this.selectedHeight = new double[para.SelectHeightPoint];
      // 레이저 좌표 (X, Y 쌍) = 10 × 2 = 20
      this.LaserCordination = new double[para.SelectHeightPoint * 2];
    }

    // ============================================================
    // [메인 파이프라인]
    // ============================================================
    /// <summary>
    /// Depth Profile 계산의 메인 진입점.
    /// FFT 결과와 Calibration 데이터를 받아 순차적으로 처리한다.
    ///   1) GetRawData      : FFT 1D → 2D 변환 + Mirror Calibration 보정
    ///   2) GetTopProfile   : 유리 상면 위치 검출
    ///   3) GetBottomProfile: 유리 하면 위치 검출
    ///   4) GetCenterProfile: 레이저 위치 검출
    ///   5) GetNumerics     : GlassDepth, LaserHeight 수치 계산
    /// </summary>
    /// <param name="data">FFT 결과 (1D float 배열, 크기 = 957 × 4096)</param>
    /// <param name="calData">Mirror Calibration 보정 데이터 (크기 = 1914)</param>
    public void GetDepthProfile(float[] data, float[] calData)
    {
      this.calData = calData;
      this.GetRawData(data);
      this.GetTopProfile();
      this.GetBottomProfile();
      this.GetCenterProfile();
      this.GetNumerics();
    }

    // ============================================================
    // [Step 1] FFT 결과를 2D 배열로 재배치 + Calibration 보정
    // ============================================================
    /// <summary>
    /// 1D FFT 결과를 2D 배열 [957][1500]으로 재배치한다.
    ///
    /// 각 A-scan의 복사 시작 위치:
    ///   offset = i × HalfFFTLength(4096) + inspectionStartIndex(700) + calData[i]
    ///
    /// - inspectionStartIndex : 노이즈 영역 제외 (앞쪽 700 포인트 건너뜀)
    /// - calData[i]           : Mirror Calibration 보정값 적용 (Depth 위치 보정)
    ///
    /// Direction에 따른 Calibration 데이터 선택:
    ///   true  (X→Y 스캔): calData[0..956]   (X 방향 보정값)
    ///   false (Y→X 스캔): calData[957..1913] (Y 방향 보정값)
    ///
    /// rawData_float    : 프로파일 추출 처리에 사용
    /// rawData_float_ori: 원본 보관 (dB 변환 후 이미지 표시에 사용)
    /// </summary>
    private void GetRawData(float[] floatData)
    {
      if (this.para.Direction)
        // X→Y 스캔 방향: calData의 앞쪽 957개 (X 보정값) 사용
        Parallel.For(0, this.realBscan, (Action<int>) (i =>
        {
          Array.Copy((Array) floatData, i * this.para.HalfFFTLength + this.inspectionStartIndex + (int) this.calData[i], (Array) this.rawData_float[i], 0, this.rawData_float[0].Length);
          Array.Copy((Array) floatData, i * this.para.HalfFFTLength + this.inspectionStartIndex + (int) this.calData[i], (Array) this.rawData_float_ori[i], 0, this.rawData_float[0].Length);
        }));
      else
        // Y→X 스캔 방향: calData의 뒤쪽 957개 (Y 보정값) 사용
        Parallel.For(0, this.realBscan, (Action<int>) (i =>
        {
          Array.Copy((Array) floatData, i * this.para.HalfFFTLength + this.inspectionStartIndex + (int) this.calData[i + this.realBscan], (Array) this.rawData_float[i], 0, this.rawData_float[0].Length);
          Array.Copy((Array) floatData, i * this.para.HalfFFTLength + this.inspectionStartIndex + (int) this.calData[i + this.realBscan], (Array) this.rawData_float_ori[i], 0, this.rawData_float[0].Length);
        }));
    }

    // ============================================================
    // [Step 2] 유리 상면(Top Surface) 검출
    // ============================================================
    /// <summary>
    /// 각 A-scan의 앞쪽 300개 depth 샘플에서 최대 FFT magnitude의 index를 찾는다.
    /// 유리 상면은 가장 강한 반사 신호이므로 Max peak = Top Surface 위치.
    ///
    /// 탐색 범위: rawData_float[i][0..299] → temp1[300]
    /// 결과: topProfile[i] = Max peak의 depth index
    ///
    /// 후처리:
    ///   - Median 필터 적용 → topProfile_filter (A-scan 간 노이즈 제거)
    ///   - topProfile_average 계산 (Bottom/Center 탐색 기준점으로 사용)
    /// </summary>
    private void GetTopProfile()
    {
      for (int index = 0; index < this.realBscan; ++index)
      {
        // 각 A-scan에서 앞쪽 300개 샘플 복사
        Array.Copy((Array) this.rawData_float[index], 0, (Array) this.temp1, 0, this.temp1.Length);
        // 최대값 및 해당 index 검출 → Top 위치
        this.maxValue1 = ((IEnumerable<float>) this.temp1).Max();
        this.topProfile[index] = (double) Array.IndexOf<float>(this.temp1, this.maxValue1);
      }
      // Median 필터로 A-scan 간 노이즈 제거
      this.topProfile_filter = ((OnlineFilter) this.medianFilter).ProcessSamples(this.topProfile);
      // Top 평균 위치 계산 (이후 Bottom, Center 탐색의 기준점)
      this.topProfile_average = ((IEnumerable<double>) this.topProfile_filter).Average();
    }

    // ============================================================
    // [Step 3] 유리 하면(Bottom Surface) 검출
    // ============================================================
    /// <summary>
    /// Top 위치를 기준으로 오프셋을 더한 영역에서 Max peak를 찾는다.
    ///
    /// bottomStartIndex = topProfile_average + 630 - 80 = Top평균 + 550
    ///   - 630: 유리 두께에 해당하는 예상 depth 거리 (pixel)
    ///   - -80: 여유분 (하면 피크가 탐색 범위 안에 확실히 들어오도록)
    ///
    /// 탐색 범위: rawData_float[i][bottomStartIndex .. bottomStartIndex+299] → temp2[300]
    /// 결과: bottomProfile[i] = temp2 내 Max peak의 index + bottomStartIndex (절대 위치)
    ///
    /// 후처리:
    ///   - Median 필터 적용
    ///   - bottomProfile_filter[0] = [1] (필터 경계 아티팩트 보정)
    ///   - bottomProfile_average 계산
    /// </summary>
    private void GetBottomProfile()
    {
      // Bottom 탐색 시작 위치 = Top 평균 + 550 pixel
      this.bottomStartIndex = (float) (this.topProfile_average + 630.0 - 80.0);
      for (int index = 0; index < this.realBscan; ++index)
      {
        // bottomStartIndex부터 300개 샘플 복사
        Array.Copy((Array) this.rawData_float[index], (int) this.bottomStartIndex, (Array) this.temp2, 0, this.temp2.Length);
        // 최대값 및 해당 index 검출
        this.maxValue2 = ((IEnumerable<float>) this.temp2).Max();
        // 절대 depth 위치 = temp2 내 상대 index + bottomStartIndex
        this.bottomProfile[index] = (double) Array.IndexOf<float>(this.temp2, this.maxValue2) + (double) this.bottomStartIndex;
      }
      // Median 필터로 노이즈 제거
      this.bottomProfile_filter = ((OnlineFilter) this.medianFilter).ProcessSamples(this.bottomProfile);
      // 첫 번째 값 보정 (Median 필터 경계 아티팩트 제거)
      this.bottomProfile_filter[0] = this.bottomProfile_filter[1];
      this.bottomProfile_average = ((IEnumerable<double>) this.bottomProfile_filter).Average();
    }

    // ============================================================
    // [Step 4] 레이저 위치(Center/Laser Position) 검출
    // ============================================================
    /// <summary>
    /// Top Surface 아래 일정 위치부터 Threshold를 초과하는 첫 번째 신호를 검출한다.
    ///
    /// linearThreshold = 10^(Threshold_dB / 20)
    ///   예: Threshold=100 dB → linearThreshold = 10^5 = 100,000
    ///
    /// centerStartIndex = Top평균 + StartIndexBelowTopPosition(100)
    ///   → Top Surface 바로 아래의 잔여 반사를 건너뛰기 위해 100 pixel 오프셋
    ///
    /// 탐색 범위: rawData_float[i][centerStartIndex .. centerStartIndex+479] → temp3[480]
    ///   → 위에서 아래로 순차 스캔하며 linearThreshold 초과하는 첫 지점 = 레이저 위치
    ///   → 미검출 시: laserProfile = bottomProfile (하면 위치로 대체)
    ///
    /// 후처리:
    ///   - 스캔 양 끝단 제거 (EndIndex, StartIndex 영역 → Bottom으로 대체)
    ///     → 갈바노 스캐너 가/감속 구간의 불안정한 데이터 제거
    ///   - Median 필터 적용
    ///   - 10번째 프레임에서 디버그 파일 저장 (laserProfile_filter.bin)
    ///   - 오름차순 정렬 복사본 생성 (laserProfile_filter_sort) → GetNumerics()에서 최소값 선택용
    /// </summary>
    private void GetCenterProfile()
    {
      // dB Threshold → Linear 스케일 변환
      this.linearThreshold = (float) Math.Pow(10.0, (double) this.para.Threshold / 20.0);
      // Center 탐색 시작 위치 = Top 평균 + 100 pixel (상면 반사 잔여 신호 건너뜀)
      this.centerStartIndex = (float) ((IEnumerable<double>) this.topProfile_filter).Average() + (float) this.para.StartIndexBelowTopPosition;

      for (int index1 = 0; index1 < this.realBscan; ++index1)
      {
        this.temp4.Clear();
        // centerStartIndex부터 480개 depth 샘플 복사
        Array.Copy((Array) this.rawData_float[index1], (int) this.centerStartIndex, (Array) this.temp3, 0, this.temp3.Length);

        // 위→아래 순차 스캔: Threshold 초과하는 첫 번째 신호 검출
        for (int index2 = 0; index2 < this.temp3.Length; ++index2)
        {
          if ((double) this.temp3[index2] > (double) this.linearThreshold)
          {
            this.temp4.Add(this.temp3[index2]);
            break; // 첫 번째 Threshold 초과 지점만 사용
          }
        }

        if (this.temp4.Count<float>() > 0)
        {
          // Threshold 초과 신호 발견 → 해당 위치를 레이저 위치로 설정
          this.maxValue3 = this.temp4[0];
          // 절대 depth 위치 = temp3 내 상대 index + centerStartIndex
          this.laserProfile[index1] = (double) Array.IndexOf<float>(this.temp3, this.maxValue3) + (double) this.centerStartIndex;
        }
        else
        {
          // 미검출 → 하면 위치로 대체 (레이저가 유리 밖에 있는 경우)
          this.laserProfile[index1] = this.bottomProfile[index1];
        }
      }

      // 스캔 시작부 제거: 앞쪽 EndIndex개를 Bottom으로 대체
      // → 갈바노 스캐너 가속 구간의 불안정 데이터 제거
      for (int index = 0; index < this.para.EndIndex; ++index)
        this.laserProfile[index] = this.bottomProfile[index];

      // 스캔 끝부 제거: 뒤쪽 StartIndex개를 Bottom으로 대체
      // → 갈바노 스캐너 감속 구간의 불안정 데이터 제거
      for (int index = 0; index < this.para.StartIndex; ++index)
        this.laserProfile[index + (this.realBscan - this.para.StartIndex)] = this.bottomProfile[index + (this.realBscan - this.para.StartIndex)];

      // Median 필터 적용
      this.laserProfile_filter = ((OnlineFilter) this.medianFilter).ProcessSamples(this.laserProfile);

      // 10번째 프레임에서 디버그용 파일 저장
      if (this.count1 == 10)
      {
        using (BinaryWriter binaryWriter = new BinaryWriter((Stream) new FileStream("laserProfile_filter.bin", FileMode.Create)))
        {
          for (int index = 0; index < this.laserProfile_filter.Length; ++index)
            binaryWriter.Write(this.laserProfile_filter[index]);
        }
      }
      ++this.count1;

      // 오름차순 정렬 복사본 생성 → GetNumerics()에서 최소값(가장 깊은 위치) 선택용
      Array.Copy((Array) this.laserProfile_filter, (Array) this.laserProfile_filter_sort, this.realBscan);
      Array.Sort<double>(this.laserProfile_filter_sort);
    }

    // ============================================================
    // [Step 5] 최종 수치 계산 (GlassDepth, LaserHeight)
    // ============================================================
    /// <summary>
    /// 검출된 프로파일로부터 최종 측정값을 계산한다.
    ///
    /// [유리 두께]
    ///   GlassDepth = |Top평균 - Bottom평균| × MicroPerPixel(0.79)
    ///   예: |50 - 680| × 0.79 = 497.7 μm
    ///
    /// [레이저 높이]
    ///   1) 정렬된 laser profile에서 최소값 N개(SelectHeightPoint=10) 선택
    ///      → 유리에 가장 가까운(depth가 가장 깊은) 지점들
    ///   2) LaserHeight = (626 - (selectedHeight평균 - Top평균)) × 0.79
    ///      - 626 = 유리 하면 기준 거리 (설계 상수, pixel)
    ///      - (selectedHeight평균 - Top평균) = Top 대비 레이저의 상대 깊이
    ///      → 결과: 유리 하면으로부터 레이저까지의 거리 (μm)
    ///
    /// [레이저 좌표]
    ///   LaserCordination = [x0, y0, x1, y1, ...]
    ///   x = A-scan index (횡방향 위치), y = depth index (깊이 위치)
    ///   → 최소 높이 지점들의 2D 좌표 (UI 표시용)
    /// </summary>
    private void GetNumerics()
    {
      // 유리 두께 (μm) = |상면 평균 - 하면 평균| × pixel당 거리
      this.GlassDepth = Math.Abs(((IEnumerable<double>) this.topProfile_filter).Average() - ((IEnumerable<double>) this.bottomProfile_filter).Average()) * (double) this.para.MicroPerPixel;

      // 정렬된 laser profile에서 최소값 N개 선택 + 좌표 저장
      for (int index = 0; index < this.selectedHeight.Length; ++index)
      {
        // 오름차순 정렬에서 앞쪽 N개 = 가장 깊은(유리에 가까운) 위치
        this.selectedHeight[index] = this.laserProfile_filter_sort[index];
        // 좌표 저장: [x, y] = [A-scan index, depth index]
        this.LaserCordination[index * 2 + 1] = this.selectedHeight[index];
        this.LaserCordination[index * 2] = (double) Array.IndexOf<double>(this.laserProfile_filter, this.selectedHeight[index]);
      }

      // 레이저 높이 (μm)
      // = (626 - (레이저 깊이 - 상면 깊이)) × pixel당 거리
      // = 유리 하면 기준으로부터 레이저까지의 거리
      this.LaserHeight = (626.0 - (((IEnumerable<double>) this.selectedHeight).Average() - ((IEnumerable<double>) this.topProfile_filter).Average())) * (double) this.para.MicroPerPixel;
    }
  }
}
