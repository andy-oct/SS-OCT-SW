// Type: DLL_RFC.DAQ.FrameTriggerExternal
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using NationalInstruments.DAQmx;

namespace DLL_RFC.DAQ
{
// 외부 Trigger 입력을 기준으로 Frame Trigger Pulse 생성
internal class FrameTriggerExternal
  {
    private InputParameters para;
    private Task myTask;
    private COPulseIdleState idleState;
    private DigitalEdgeStartTriggerEdge triggerEdge;

    public FrameTriggerExternal(InputParameters para) // 기본 Trigger 조건 설정
    {
      this.para = para;
      this.idleState = (COPulseIdleState) 10214; // 10214 -> Low (Pulse가 없을 때 출력은 Low 유지)
      this.triggerEdge = (DigitalEdgeStartTriggerEdge) 10280; // 10280 -> Rising Edge (외부 Trigger 상승 에지에서 시작)
    }

    public void PulseGenStart() // Frame Trigger 생성 시작
    {
      this.myTask = new Task();
    // Counter : ctr0, Frequency : 110 Hz(초당 110 프레임), Duty : 50%, Delay : 0
      this.myTask.COChannels.CreatePulseChannelFrequency("Dev1/ctr0", "ContinuousPulseTrain", (COPulseFrequencyUnits) 10373, this.idleState, 0.0, 110.0, 0.5);
      this.myTask.Triggers.StartTrigger.Type = (StartTriggerType) 10150; // Digital Edge Trigger 사용
      this.myTask.Triggers.StartTrigger.DigitalEdge.Edge = this.triggerEdge; // Rising Edge
      this.myTask.Triggers.StartTrigger.DigitalEdge.Source = "/Dev1/PFI10"; // External Trigger(PFI10), PFI10 핀에서 들어오는 신호로 시작
      this.myTask.Triggers.StartTrigger.Retriggerable = true; // Trigger가 들어올 떄마다 Pulse Train 재시작
      this.myTask.Timing.ConfigureImplicit((SampleQuantityMode) 10178, 1); // Implicit Timing (Counter 내부 타이밍 사용, 외부 Sample Clock 없이 Counter 자체로 Pulse 생성
      this.myTask.Start();
    }

    public void PulseGenStop()
    {
      if (this.myTask != null)
      {
        this.myTask.Stop();
        this.myTask.Dispose();
      }
      this.myTask = (Task) null;
    }
  }
}
