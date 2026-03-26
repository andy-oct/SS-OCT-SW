// Type: DLL_RFC.DAQ.FrameTriggerFinite
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using NationalInstruments.DAQmx;
using System;
using System.Threading;

namespace DLL_RFC.DAQ
{
  // Frame Trigger (Finite 모드: 정해진 횟수만 생성)
  internal class FrameTriggerFinite
  {
    private InputParameters para; // 파라메터 설정 값
    private Task myTask; // DAQ Task

    public FrameTriggerFinite(InputParameters para) => this.para = para;

    // 반복 Pulse 생서(Finite)
    public void GenRepeatPulse()
    {
      try
      {
        this.myTask = new Task();
        // Counter Output Pulse 설정 Counter : ctr0(Counter 0 사용), 채널이름, Hz, Idle Low, Delay : 0.0(초기지연 없음), Frame 속도, Duty : 50%
        this.myTask.COChannels.CreatePulseChannelFrequency("Dev1/ctr0", "FinitePulseTrain", (COPulseFrequencyUnits) 10373, (COPulseIdleState) 10214, 0.0, this.para.Frequency, 0.5);
        // Finite sample 설정(Pulse 개수 제한)
        this.myTask.Timing.ConfigureImplicit((SampleQuantityMode) 10178, this.para.NumberOfLine); // Finite samples, 생성할 Pulse 개수
        this.myTask.Start(); // Pulse 생성 시작
        Thread.Sleep((int) ((double) this.para.NumberOfLine * (1.0 / this.para.Frequency) * 1000.0) + 20); // Pulse가 끝날 때까지 대기
      }
      catch (Exception ex)
      {
        this.myTask.Dispose(); // 에러 시 Task 해제
      }
    }

    public void StopRepeatPulse()
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
