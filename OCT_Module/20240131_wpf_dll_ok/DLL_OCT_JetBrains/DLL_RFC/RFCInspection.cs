// Type: DLL_RFC.RFCInspection
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

// ============================================================
// RFCInspection 클래스
// ============================================================
// SS-OCT 시스템의 최상위 컨트롤러 (Facade 패턴).
// 하드웨어(ADC, DAQ), 신호처리(Processing), 보정(Calibration)을
// 통합 관리하며, UI(ViewModel)와 하드웨어 사이를 연결하는 유일한 진입점.
//
// [아키텍처]
//   ViewModel (UI)
//       ↕ JSHandler 이벤트
//   RFCInspection (이 클래스)
//       ├─ ControlADBoard    (Alazar ADC 디지타이저)
//       ├─ ControlDAQ        (NI DAQ 갈바노 스캐너 + Trigger)
//       ├─ ControlProcessing (FFT + Depth 신호처리)
//       ├─ ControlCalibration(Mirror 보정 데이터 관리)
//       └─ ControlManualTest (파일 기반 오프라인 테스트)
//
// [데이터 흐름]
//   ADC 수집 완료 (DHEvent)
//     → BoardControl_DHEvent()
//       → Processing.GetResult() (일반 측정)
//       → Processing.GetMirrorResult() (Mirror Calibration)
//     → jsEventArgs.SetData() → JSHandler 이벤트 → UI 업데이트
//
// [동작 모드]
//   1) Internal  : 내부 10 Hz 연속 측정
//   2) External  : 외부 Trigger(PLC/Encoder) 동기 측정
//   3) Repeat    : 정해진 횟수 반복 스캔
//   4) Mirror Cal: X/Y 각 5회 스캔 → 보정 데이터 생성
//   5) Manual    : 파일 기반 오프라인 분석
//
// [running 플래그 (주의: 직관과 반대)]
//   running = true  → 스캔 "정지" 상태 (Start 가능)
//   running = false → 스캔 "동작 중" (Stop 가능)
// ============================================================

using DLL_RFC.ADBoard;
using DLL_RFC.Calibration;
using DLL_RFC.DAQ;
using DLL_RFC.Manual;
using DLL_RFC.Processing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DLL_RFC
{
  public class RFCInspection
  {
    // ============================================================
    // [모듈 객체]
    // ============================================================
    private InputParameters para;            // 시스템 파라미터 (공유, 모든 모듈이 참조)
    private ControlADBoard board;            // Alazar ADC 디지타이저 제어
    private ControlDAQ daq;                  // NI DAQ 갈바노 스캐너 + Trigger 제어
    private ControlProcessing processing;    // FFT + Depth 신호처리 파이프라인
    private ControlCalibration calibration;  // Mirror Calibration 데이터 관리
    private ControlManualTest manual;        // 파일 기반 오프라인 테스트
    private JSEventArgs jsEventArgs;         // UI 전달용 이벤트 데이터 (더블 버퍼)

    // ============================================================
    // [상태 플래그]
    // ============================================================
    private bool running;                    // 스캔 상태 (true=정지, false=동작 중) ★ 직관과 반대
    private bool mirrorCalibration = false;  // Mirror Calibration 모드 여부
                                             // true: GetMirrorResult() / false: GetResult()

    // ============================================================
    // [이벤트]
    // ============================================================
    /// <summary>Processing 완료 시 UI에 결과를 전달하는 이벤트</summary>
    public event EventHandler<JSEventArgs> JSHandler;

    // ============================================================
    // [시뮬레이션 모드]
    // ============================================================
    /// <summary>
    /// DAQ 시뮬레이션 모드.
    /// true: NI DAQ 없이 동작 (개발/테스트용, Scanner/Trigger 출력 없음)
    /// false: 실제 NI DAQ 하드웨어 사용
    /// </summary>
    public bool SIMULATION_DAQ = true;

    /// <summary>JSHandler 이벤트를 안전하게 발생시키는 헬퍼 (null 체크)</summary>
    protected void OnJSHandler(JSEventArgs e)
    {
      EventHandler<JSEventArgs> jsHandler = this.JSHandler;
      if (jsHandler == null)
        return;
      jsHandler((object) this, e);
    }

    // ============================================================
    // [생성자] 시스템 초기화
    // ============================================================
    /// <summary>
    /// RFCInspection 생성자. 모든 하위 모듈을 생성하고 초기화한다.
    ///
    /// 초기화 순서:
    ///   1) Alazar ADC 보드 생성 + DHEvent 이벤트 연결
    ///   2) NI DAQ 생성 (SIMULATION_DAQ=false일 때만)
    ///   3) Processing + Calibration 생성 (mirrorCalData.bin 로드)
    ///   4) Initialize() → DAQ 초기화 → ADC 수집 시작 → 워밍업 스캔
    /// </summary>
    public RFCInspection(InputParameters para)
    {
      this.para = para;
      this.running = true; // 정지 상태로 시작

      // Alazar ADC 보드 생성 + 데이터 수신 이벤트 연결
      this.board = new ControlADBoard(para);
      this.board.DHEvent += new EventHandler<byte[]>(this.BoardControl_DHEvent);

      // NI DAQ 생성 (실제 하드웨어 모드만)
      if(!SIMULATION_DAQ) this.daq = new ControlDAQ(para);

      // 신호처리 + 보정 + 이벤트 데이터 + Manual 테스트 생성
      this.processing = new ControlProcessing(para);
      this.calibration = new ControlCalibration(para);
      this.jsEventArgs = new JSEventArgs();
      this.manual = new ControlManualTest(para);

      // 시스템 초기화 (DAQ → ADC → 워밍업)
      this.Initialize();
    }

    // ============================================================
    // [핵심 콜백] ADC 데이터 수신 → Processing → UI 전달
    // ============================================================
    /// <summary>
    /// Alazar ADC에서 1 Frame 수집 완료 시 호출되는 콜백.
    ///
    /// [Mirror Calibration 모드] (mirrorCalibration == true)
    ///   → GetMirrorResult(): Mirror peak 위치 누적 (UI 이벤트 없음)
    ///
    /// [일반 측정 모드] (mirrorCalibration == false)
    ///   → GetResult(): FFT → Average → GetDepth → 측정값 계산
    ///   → jsEventArgs.SetData(): 결과를 더블 버퍼에 깊은 복사
    ///   → OnJSHandler(): JSHandler 이벤트 발생 → UI 업데이트
    /// </summary>
    private void BoardControl_DHEvent(object sender, byte[] e)
    {
      if (this.mirrorCalibration)
      {
        // Mirror Calibration: peak 위치 누적 (count 0~9)
        this.processing.GetMirrorResult(e);
      }
      else
      {
        // 일반 측정: FFT → Depth 추출 → 측정값 계산
        this.processing.GetResult(e, this.calibration.CalData);

        // 처리 결과를 UI 이벤트 데이터에 복사
        this.jsEventArgs.SetData(this.processing.depth.GlassDepth, this.processing.depth.LaserHeight, this.processing.depth.ReferencePosition, this.processing.depth.RawData, this.processing.depth.TopProfile, this.processing.depth.LaserProfile, this.processing.depth.LaserCordination);

        // UI에 결과 전달 (ViewModel.Inspection_JSHandler 호출)
        this.OnJSHandler(this.jsEventArgs);
      }
    }

    // ============================================================
    // [시스템 초기화]
    // ============================================================
    /// <summary>
    /// 시스템 초기화 시퀀스:
    ///   1) DAQ 초기화: LineTrigger 시작 (A-scan 기준 클럭)
    ///   2) ADC 수집: 백그라운드 스레드에서 무한 DMA 대기 루프
    ///   3) 워밍업: 1초간 Internal 스캔 후 정지
    ///      → ADC/DAQ 하드웨어 안정화 목적
    /// </summary>
    private void Initialize()
    {
      // [1] NI DAQ 초기화 (LineTrigger 시작, PFI0 Laser Sweep Trigger 대기)
      if (!SIMULATION_DAQ) this.daq.DAQInitialize();

      // [2] ADC 수집 시작 (백그라운드 스레드, 무한 루프)
      Task.Run((Action) (() => this.board.AcquireData((uint) int.MaxValue)));

      // [3] 워밍업 스캔 (1초간 동작 후 정지)
      this.StartInternal();
      Thread.Sleep(1000);
      this.Stop();
    }

    // ============================================================
    // [Mirror Calibration] X/Y 각 5회 스캔 → 보정 데이터 생성
    // ============================================================
    /// <summary>
    /// Mirror Calibration 시퀀스:
    ///   1) mirrorCalibration=true → DHEvent에서 GetMirrorResult() 호출
    ///   2) X 방향 5회 반복 스캔 (count 0~4, mirrorProfileX 누적)
    ///   3) 1초 대기 (X→Y 전환 안정화)
    ///   4) Y 방향 5회 반복 스캔 (count 5~9, mirrorProfileY 누적)
    ///   5) count==9에서: 평균 → Median 필터 → mirrorCalData.bin 저장
    ///   6) mirrorCalibration=false → 일반 측정 모드 복귀
    /// </summary>
    public void StartMirrorCalibration(string saveFileName)
    {
      this.para.SaveFileName = saveFileName;
      this.mirrorCalibration = true;  // Mirror 모드 ON
      this.para.Frequency = 5.0;     // 저속 스캔 (안정성 확보)
      this.para.NumberOfLine = 5;    // 5회 반복

      // X 방향 5회 스캔
      this.para.Direction = true;
      this.StartRepeat();

      Thread.Sleep(1000); // X→Y 전환 대기

      // Y 방향 5회 스캔
      this.para.Direction = false;
      this.StartRepeat();

      this.mirrorCalibration = false; // Mirror 모드 OFF → 일반 측정 복귀
    }

    /// <summary>런타임 Calibration 파일 갱신 (UI "Update CalFile" 버튼)</summary>
    public void UpdateCalibrationFile(string calFile) => this.calibration.UpdateCalfile(calFile);

    // ============================================================
    // [스캔 제어] Start / Stop / Turn
    // ============================================================

    /// <summary>
    /// 내부 Trigger 기반 연속 측정 시작 (10 Hz).
    /// Scanner 삼각파 출력 + Frame Trigger 연속 Pulse.
    /// running==false(이미 동작 중)이면 무시.
    /// </summary>
    public void StartInternal()
    {
      if (!this.running)  // 이미 동작 중이면 무시
        return;
      this.para.Frequency = 10.0; // 10 Hz Frame rate
      if (!SIMULATION_DAQ) this.daq.DAQStartInternal();
      this.running = false; // 동작 중으로 전환
    }

    /// <summary>
    /// 정해진 횟수(NumberOfLine) 반복 스캔.
    /// Stop → 스캔 → Stop 순서로 실행 (1사이클 후 자동 정지).
    /// Mirror Calibration, 평균/누적 측정에 사용.
    /// </summary>
    public void StartRepeat()
    {
      this.Stop();  // 먼저 현재 스캔 중지
      if (!SIMULATION_DAQ) this.daq.DAQStartRepeat();
      this.running = false;
      this.Stop();  // 반복 완료 후 자동 정지
    }

    /// <summary>
    /// 외부 Trigger(PLC/Encoder) 동기 측정 시작.
    /// PFI10에서 외부 Trigger 수신 시마다 Frame 스캔.
    /// </summary>
    public void StartExternal()
    {
      if (!SIMULATION_DAQ) this.daq.DAQStartExternal();
      this.running = false;
    }

    /// <summary>
    /// 스캔 방향 전환 (X↔Y).
    /// 현재 스캔 정지 → Direction 토글 → Internal 재시작.
    /// </summary>
    public void TurnInternal()
    {
      this.Stop();
      this.para.Direction = !this.para.Direction; // X↔Y 방향 토글
      this.StartInternal();
    }

    /// <summary>
    /// 스캔 정지. Scanner + Frame Trigger 출력 중단.
    /// running==true(이미 정지)이면 무시.
    /// </summary>
    public void Stop()
    {
      if (this.running)  // 이미 정지면 무시
        return;
     if (!SIMULATION_DAQ) this.daq.DAQStop();
      this.running = true; // 정지로 전환
    }

    ~RFCInspection()
    {
    }

    // ============================================================
    // [프로그램 종료] 안전한 종료 시퀀스
    // ============================================================
    /// <summary>
    /// 프로그램 종료 시퀀스 (순서 중요, hang 방지):
    ///   1) StartInternal(): ADC를 활성 상태로 전환
    ///      → 정지 상태에서 StopAcquireData 시 DMA 대기 hang 방지
    ///   2) Sleep(1000): ADC가 최소 1프레임 수집할 때까지 대기
    ///   3) StopAcquireData(): ADC DMA 루프 종료 플래그 설정
    ///   4) Sleep(2000): ADC 완전 종료 대기 (DMA 정리)
    ///   5) DAQStopProgram(): LineTrigger 중지 (기준 클럭 종료)
    /// </summary>
    public void StopProgram()
    {
      this.StartInternal();           // [1] ADC 활성 상태로 전환
      Thread.Sleep(1000);             // [2] 1프레임 수집 대기
      this.board.StopAcquireData();   // [3] ADC DMA 루프 종료
      Thread.Sleep(2000);             // [4] ADC 완전 종료 대기
      if (!SIMULATION_DAQ) this.daq.DAQStopProgram(); // [5] Line Trigger 중지
    }

    // ============================================================
    // [Manual Test] 파일 기반 오프라인 분석
    // ============================================================
    /// <summary>
    /// 저장된 이미지 파일로 오프라인 분석 수행.
    /// ADC 수집 없이 파일 → Processing → UI 표시.
    /// </summary>
    public void StartManualTest(string fileName)
    {
      // 파일 로드 + 오프라인 Processing
      this.manual.Start(fileName);

      // 결과를 UI 이벤트 데이터에 복사
      this.jsEventArgs.SetData(this.manual.GlassDepth, this.manual.LaserHeight, this.manual.ReferencePosition, this.manual.DepthImage, this.manual.TopProfile, this.manual.LaserProfile, this.manual.LaserCordination);

      // UI에 결과 전달
      this.OnJSHandler(this.jsEventArgs);
    }
  }
}
