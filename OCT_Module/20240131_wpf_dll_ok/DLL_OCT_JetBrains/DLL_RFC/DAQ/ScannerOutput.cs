// Type: DLL_RFC.DAQ.ScannerOutput
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using NationalInstruments.DAQmx;

namespace DLL_RFC.DAQ
{
  internal class ScannerOutput
  {
    private InputParameters para;   // 스캔 설정
    private Task myTask;            // DAQ 작업
    private DigitalEdgeStartTriggerEdge edge = (DigitalEdgeStartTriggerEdge) 10280; // 10280 ? : Rising edge
    private double[,] lineScan_XY;  // X fast scan
    private double[,] lineScan_YX;  // Y fast scan
    private double[,] areaScan;     // C-scan

    public ScannerOutput(InputParameters para)
    {
      this.para = para;
      this.lineScan_XY = ScannerFunction.GenerateMultiWaveforms_LineScan_XY((int) para.BScan, para.Amplitude_X, para.Offset_X, para.Offset_Y);
      this.lineScan_YX = ScannerFunction.GenerateMultiWaveforms_LineScan_YX((int) para.BScan, para.Amplitude_Y, para.Offset_X, para.Offset_Y);
      this.areaScan = ScannerFunction.GenerateMultiWaveforms_CScan((int) para.BScan, para.Amplitude_X, para.Offset_X, para.Amplitude_Y, para.Offset_Y, (int) para.CScan);
    }

    public void Start(bool direction)
    {
      this.myTask = new Task();
      this.myTask.AOChannels.CreateVoltageChannel("Dev1/ao0:1", "", -5.0, 5.0, (AOVoltageUnits) 10348); // Device : Dev1, Channel : ao0, ao1, Range : -5V ~ +5V
      this.myTask.Control((TaskAction) 2);  // Commit : DAQ 하드웨어 리소스 예약 (DAQ 보드는 동시에 여러 프로그램이 접근할 수 없음, 하드웨어 리소스를 예약)
      // Sample Clock 설정, Clock source : PFI13, Clock rate : 2000Hz, Mode : Finite(정해진 개수의 샘플만 취득하고 종료하는 모드), Samples : BScan x 2
      this.myTask.Timing.ConfigureSampleClock("/Dev1/PFI13", 2000.0, (SampleClockActiveEdge) 10280, (SampleQuantityMode) 10178, (int) this.para.BScan * 2); // BScan = 1000, Samples(BScan*2) = 2000
      this.myTask.Triggers.StartTrigger.ConfigureDigitalEdgeTrigger("/Dev1/PFI12", this.edge);  // Trigger source : PFI12
      // Trigger 반복 허용(Frame 마다 스캔 반복), 1 frame waveformm : 한 프레임 영상을 만들기 위해 스캐너가 움직이는 동안 출력되는 전체 Galvo 제어 파형
      // 한 장의 OCT 영상(Frame)을 만들기 위해 Scanner가 움직이는 동안 출력되는 전체 X-Y 파형, Frame waveform = 모든 B-scan 라인의 집합
      this.myTask.Triggers.StartTrigger.Retriggerable = true;
      this.myTask.Done += new TaskDoneEventHandler(this.MyTask_Done);
      AnalogMultiChannelWriter multiChannelWriter = new AnalogMultiChannelWriter(this.myTask.Stream); // PC memory -> DAQ output buffer
      if (this.para.BoolCScan)
        multiChannelWriter.WriteMultiSample(false, this.areaScan);    // C-scan
      else if (direction)
        multiChannelWriter.WriteMultiSample(false, this.lineScan_XY); // XY scan
      else
        multiChannelWriter.WriteMultiSample(false, this.lineScan_YX); // YX scan

      //
      this.myTask.Start(); // DAQ 출력 준비 (실제출력조건 PFI12 trigger)
    }

    public void Stop()
    {
      if (this.myTask != null)
      {
        this.myTask.Stop();
        this.myTask.Dispose();
      }
      this.myTask = (Task) null;
    }

    private void MyTask_Done(object sender, TaskDoneEventArgs e)
    {
      if (e.Error == null)
        return;

      if (this.myTask == null)
        return;

      this.myTask.Dispose(); // Task 종료 이벤트
      this.myTask = (Task) null;
    }
  }
}
