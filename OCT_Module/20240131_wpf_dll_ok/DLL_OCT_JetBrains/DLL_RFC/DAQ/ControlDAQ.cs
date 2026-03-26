// Type: DLL_RFC.DAQ.ControlDAQ
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

namespace DLL_RFC.DAQ
{
  internal class ControlDAQ
  {
    private InputParameters para; // 파라메터 설정 값
    private LineTrigger lineTrigger; // A-scan Trigger 생성
    private FrameTriggerExternal ft_External; // 외부 Trigger 기반 Frmae 생성
    private FrameTriggerFinite ft_Finite; // 정해진 횟수만큼 Frame 생성(Repeat Scan)
    private FrameTriggerInternal ft_Internal; // 내부 클럭 기반 Frame 생성
    private ScannerOutput scanner; // Galvo Scanner  제어(X/Y Scan)

    public ControlDAQ(InputParameters para)
    {
      this.para = para;
      this.lineTrigger = new LineTrigger();
      this.ft_External = new FrameTriggerExternal(para);
      this.ft_Finite = new FrameTriggerFinite(para);
      this.ft_Internal = new FrameTriggerInternal(para);
      this.scanner = new ScannerOutput(para);
    }

    public void DAQInitialize() => this.lineTrigger.PulseGenStart(); // Line Trigger (A-scan clock) 항상 먼저 시작(기준 클럭 역활)

    public void DAQStartInternal() // 내부 클럭 기반 Frame 생성
    {
      this.scanner.Start(this.para.Direction); // Scanner 시작
      this.ft_Internal.CO_Start(); // 내부 Frame Trigger 시작
    }

    public void DAQStartRepeat() // 정해진 횟수(BScan) 반복 스캔 (평균/누적 측정)
    {
      this.scanner.Start(this.para.Direction);
      this.ft_Finite.GenRepeatPulse();
    }

    public void DAQStartExternal() // 외부(Encoder/PLC) Trigger 기준 Frmae 동기화
    {
      this.scanner.Start(this.para.Direction);
      this.ft_External.PulseGenStart();
    }

    public void DAQStop()
    {
      this.scanner.Stop();
      this.ft_Internal.CO_Stop();
      this.ft_Finite.StopRepeatPulse();
      this.ft_External.PulseGenStop();
    }

    public void DAQStopProgram() => this.lineTrigger.PulseGenStop();
  }
}
