// Type: DLL_RFC.DAQ.FrameTriggerInternal
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using NationalInstruments.DAQmx;

namespace DLL_RFC.DAQ
{
  internal class FrameTriggerInternal
  {
    private InputParameters para;   // 파라메터
    private Task myTask;            // DAQ Task
    private COPulseIdleState idleState = (COPulseIdleState) 10214; // 출력 초기 상태

    public FrameTriggerInternal(InputParameters para) => this.para = para; // 파라메터 저장

    public void CO_Start() // Frame trigger pulse 생성 시작
    {
      this.myTask = new Task();
      // Counter 0, 채널이름, Hz, Low, 0, Pulse frequency, 50%
      this.myTask.COChannels.CreatePulseChannelFrequency("Dev1/ctr0", "ContinuousPulseTrain", (COPulseFrequencyUnits) 10373, this.idleState, 0.0, this.para.Frequency, 0.5);
      this.myTask.Timing.ConfigureImplicit((SampleQuantityMode) 10123, 1000); // 10123 : Continuous samples (Pulse 계속 출력), 1000(샘플 수) : Buffer size
      this.myTask.Start(); // counter 시작
    }

    public void CO_Stop()
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
