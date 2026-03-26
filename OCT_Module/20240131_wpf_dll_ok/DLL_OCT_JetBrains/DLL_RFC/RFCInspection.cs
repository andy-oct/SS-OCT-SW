// Type: DLL_RFC.RFCInspection
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

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
    private InputParameters para;
    private bool running;
    private ControlADBoard board;
    private ControlDAQ daq;
    private ControlProcessing processing;
    private ControlCalibration calibration;
    private JSEventArgs jsEventArgs;
    private ControlManualTest manual;
    private bool mirrorCalibration = false;

    public event EventHandler<JSEventArgs> JSHandler;


    public bool SIMULATION_DAQ = true;

    protected void OnJSHandler(JSEventArgs e)
    {
      EventHandler<JSEventArgs> jsHandler = this.JSHandler;
      if (jsHandler == null)
        return;
      jsHandler((object) this, e);
    }

    public RFCInspection(InputParameters para)
    {
      this.para = para;
      this.running = true;
      this.board = new ControlADBoard(para);
      this.board.DHEvent += new EventHandler<byte[]>(this.BoardControl_DHEvent);
      if(!SIMULATION_DAQ) this.daq = new ControlDAQ(para);
      this.processing = new ControlProcessing(para);
      this.calibration = new ControlCalibration(para);
      this.jsEventArgs = new JSEventArgs();
      this.manual = new ControlManualTest(para);
      this.Initialize();
    }

    private void BoardControl_DHEvent(object sender, byte[] e)
    {
      if (this.mirrorCalibration)
      {
        this.processing.GetMirrorResult(e);
      }
      else
      {
        this.processing.GetResult(e, this.calibration.CalData);
        this.jsEventArgs.SetData(this.processing.depth.GlassDepth, this.processing.depth.LaserHeight, this.processing.depth.ReferencePosition, this.processing.depth.RawData, this.processing.depth.TopProfile, this.processing.depth.LaserProfile, this.processing.depth.LaserCordination);
        this.OnJSHandler(this.jsEventArgs);
      }
    }

    private void Initialize()
    {
      if (!SIMULATION_DAQ) this.daq.DAQInitialize();
      Task.Run((Action) (() => this.board.AcquireData((uint) int.MaxValue)));
      this.StartInternal();
      Thread.Sleep(1000);
      this.Stop();
    }

    public void StartMirrorCalibration(string saveFileName)
    {
      this.para.SaveFileName = saveFileName;
      this.mirrorCalibration = true;
      this.para.Frequency = 5.0;
      this.para.NumberOfLine = 5;
      this.para.Direction = true;
      this.StartRepeat();
      Thread.Sleep(1000);
      this.para.Direction = false;
      this.StartRepeat();
      this.mirrorCalibration = false;
    }

    public void UpdateCalibrationFile(string calFile) => this.calibration.UpdateCalfile(calFile);

    public void StartInternal()
    {
      if (!this.running)
        return;
      this.para.Frequency = 10.0;
      if (!SIMULATION_DAQ) this.daq.DAQStartInternal();
      this.running = false;
    }

    public void StartRepeat()
    {
      this.Stop();
      if (!SIMULATION_DAQ) this.daq.DAQStartRepeat();
      this.running = false;
      this.Stop();
    }

    public void StartExternal()
    {
      if (!SIMULATION_DAQ) this.daq.DAQStartExternal();
      this.running = false;
    }

    public void TurnInternal()
    {
      this.Stop();
      this.para.Direction = !this.para.Direction;
      this.StartInternal();
    }

    public void Stop()
    {
      if (this.running)
        return;
     if (!SIMULATION_DAQ) this.daq.DAQStop();
      this.running = true;
    }

    ~RFCInspection()
    {
    }

    public void StopProgram()
    {
      this.StartInternal();
      Thread.Sleep(1000);
      this.board.StopAcquireData();
      Thread.Sleep(2000);
      if (!SIMULATION_DAQ) this.daq.DAQStopProgram();
    }

    public void StartManualTest(string fileName)
    {
      this.manual.Start(fileName);
      this.jsEventArgs.SetData(this.manual.GlassDepth, this.manual.LaserHeight, this.manual.ReferencePosition, this.manual.DepthImage, this.manual.TopProfile, this.manual.LaserProfile, this.manual.LaserCordination);
      this.OnJSHandler(this.jsEventArgs);
    }
  }
}
