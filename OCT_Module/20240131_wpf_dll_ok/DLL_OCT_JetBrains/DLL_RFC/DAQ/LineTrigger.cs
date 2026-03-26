// Type: DLL_RFC.DAQ.LineTrigger
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using NationalInstruments.DAQmx;
using System;

namespace DLL_RFC.DAQ
{
    // Line Trigger (A-scan trigger) 생성 클래스
  internal class LineTrigger
  {
     // DAQmx Task (하드웨어 리소스 및 설정을 관리)
    private Task myTask;
    private COPulseIdleState idleState; // Pulse가 출력되지 않을 때의 기본 상태 (Idle 상태)
    private DigitalEdgeStartTriggerEdge triggerEdge; // Start Trigger의 Edge (Rising / Falling)

    public LineTrigger()
    {
      this.idleState = (COPulseIdleState) 10214; // 10214 → Low 상태 (Idle = LOW)
      this.triggerEdge = (DigitalEdgeStartTriggerEdge) 10280; // 10280 → Rising Edge (상승 에지에서 시작)
    }

    // Pulse 생성 시작
    public void PulseGenStart()
    {
      if (this.myTask != null) // 이미 실행 중이면 중복 생성 방지
      return;

      this.myTask = new Task();
      // Counter Output Pulse 설정( 사용 Counter 채널, 채널이름, Hz 단위, Idle 상태(LOW), 초기 Delay 없음(0.0), 주파수(110 KHz, A-Scan rate), Duty Cycle (50%) )
      this.myTask.COChannels.CreatePulseChannelFrequency("Dev1/ctr1", "ContinuousPulseTrain", (COPulseFrequencyUnits) 10373, this.idleState, 0.0, 110000.0, 0.5);
      this.myTask.Triggers.StartTrigger.Type = (StartTriggerType) 10150; // Start Trigger 타입: Digital Edge Trigger
      this.myTask.Triggers.StartTrigger.DigitalEdge.Edge = this.triggerEdge; // Trigger Edge: Rising Edge
      this.myTask.Triggers.StartTrigger.DigitalEdge.Source = "/Dev1/PFI0"; // Trigger 입력 핀: PFI0
      this.myTask.Triggers.StartTrigger.Retriggerable = true; // Retriggerable: Trigger가 들어올 때마다 다시 시작 가능
      this.myTask.Timing.ConfigureImplicit((SampleQuantityMode) 10178, 1); // Counter Timing 설정 (Implicit), Finite Sample (Trigger 기반)
      this.myTask.Start(); // Task 시작 (Trigger 대기 상태)
    }

    public void PulseGenStop() // Pulse 생성 중지
    {
      if (this.myTask == null)
        return;
      this.myTask.Stop();
      this.myTask.Dispose(); // DAQ 리소스 해제
      this.myTask = (Task) null; // 객체 초기화
      Console.WriteLine("Line Trigger Stop"); // 로그 출력
    }
  }
}
