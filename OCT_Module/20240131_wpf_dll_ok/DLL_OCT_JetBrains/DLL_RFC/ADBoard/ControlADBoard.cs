// Type: DLL_RFC.ADBoard.ControlADBoard
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using AlazarTech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DLL_RFC.ADBoard
{
  internal class ControlADBoard
  {
    //* FFT 깊이 축 길이, Full FFT = 8192, Half FFT = 4096, 깊이 방향으로 유효한 데이터는 앞쪽 4096 포인트만 사용
    private int halfFFTLength;

    //* A-scan 샘플 수
    private readonly uint ascan = 1024;
    //

    //* ADC 샘플링 속도 (Hz)
    private static double samplesPerSec;
    //

    //* System 1, Board 1번 Alazar 보드 연결
    private readonly uint systemId = 1;
    private uint boardId = 1;
    //

    //* Alazar ADC 보드 핸들(포인터)
    private IntPtr handle;
    //

    //* 입력 파라미터 구조체
    private InputParameters para;
    //

    //* 하드웨어 에러 로그 버퍼
    private List<string> errorMessage = new List<string>();
    //

    //* Trigger 이전 데이터 샘플 저장
    private uint preTriggerSamples;
    //

    //* Trigger 이후 데이터 샘플 저장
    private uint postTriggerSamples;
    //

    //* B-scan 폭
    private uint recordsPerBuffer;
    //

    //* 1: Channel A, 2: Channel B, 3: A+B  (어느 포토디텍터 채널을 쓸지 설정)
    private uint channelMask;
    //

    //* 1: Balanced PD 출력 사용, 2: Doppler/Polarization 가능
    private uint channelCount;
    //

    private bool saveData;          // Data 저장 여부
    private uint retCode;           // Alazar API 함수 호출 결과 코드 (Return Code)
    private FileStream fileStream;  // ADC 데이터 저장용 파일 스트림
    private bool success;           // 수집 성공 여부
    private object lock_AcquireData = new object(); // AcquireData() 함수의 멀티스레드 동기화를 위한 Lock 객체
    private int count;              // 수집한 프레임 수
    private bool acquireDataStop;   // 스트리밍 중단 플래그

    public bool ADBoardRunning { get; private set; } // AD(ADC) 보드가 현재 데이터 수집 중인지 여부 확인

    public List<string> ErrorMessage => this.errorMessage; // 에러 메세지 로그 리스트

    public void ErrorMessageClear() => this.errorMessage.Clear(); // 에러 메세지 Clear

    public ControlADBoard(InputParameters para)
    {
            this.para = para; // 외부에서 전달받은 설정 파라미터 저장
            this.halfFFTLength = para.HalfFFTLength; // 깊이 포인트 수
            //* 보드를 열어서 제어권 핸들 (하드웨어 메모리 주소를 handle로 받음)
            this.handle = AlazarAPI.AlazarGetBoardBySystemID(this.systemId, this.boardId);
            if (this.handle == IntPtr.Zero)
                this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: Open board failed");
            else if (!this.ConfigureBoard(this.handle)) // 보드 설정
                this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: Configure board failed");
            else
                this.AcquisitionParametersInitialize(); // 파라메터 초기화
    }

    private bool ConfigureBoard(IntPtr boardHandle)
    {
      ControlADBoard.samplesPerSec = 100000000.0; // 100 MS/s
      uint num1 = 64; // 샘플링 클럭 분주값 (클럭 파라미터) : 샘플링 속도 설정

     //* ADC 보드 핸들, 2U: CLOCK_SOURCE_EXTERNAL, OU: Rising edge(상승엣지샘플), 0U: No decimation(추가다운샘플링없음)
      uint num2 = AlazarAPI.AlazarSetCaptureClock(boardHandle, 2U, num1, 0U, 0U);
      if (num2 != 512U) // 512U: ApiSuccess
            {
        this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarSetCaptureClock failed -- " + AlazarAPI.AlazarErrorToText(num2));
        return false;
      }
      //* ADC 보드 핸들, 1U: 채널 번호: Channel A, 2U: Coupling 방식: DC coupling(DC 성분 포함해서 샘플링), 10U: 입력 전압 범위, 2U: 입력 임피던스: 50 Ohm
      uint num3 = AlazarAPI.AlazarInputControlEx(boardHandle, 1U, 2U, 10U, 2U);
      if (num3 != 512U)
      {
        this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarInputControlEx failed -- " + AlazarAPI.AlazarErrorToText(num3));
        return false;
      }
      //* ADC 보드 핸들, 1U: 채널 번호: Channel A, 0U: Bandwidth limit 설정 (Bandwidth Limit OFF)
      uint num4 = AlazarAPI.AlazarSetBWLimit(boardHandle, 1U, 0U);
      if (num4 != 512U)
      {
        this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarSetBWLimit failed -- " + AlazarAPI.AlazarErrorToText(num4));
        return false;
      }
      //* ADC 보드 핸들, 2U: 채널 번호(Channel B), 2U: DC coupling, 7U: 입력 전압 범위, 2U: 입력 임피던스(50 Ohm)
      uint num5 = AlazarAPI.AlazarInputControlEx(boardHandle, 2U, 2U, 7U, 2U);
      if (num5 != 512U)
      {
        this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarInputControlEx failed -- " + AlazarAPI.AlazarErrorToText(num5));
        return false;
      }
      // ADC 보드 핸들, 2U: 채널 번호(Channel B), OU: Bandwidth Limit: OFF (비활성화)
      uint num6 = AlazarAPI.AlazarSetBWLimit(boardHandle, 2U, 0U);
      if (num6 != 512U)
      {
        this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarSetBWLimit failed -- " + AlazarAPI.AlazarErrorToText(num6));
        return false;
      }
      // ADC 보드 핸들, OU: Trigger Operation Mode(OU: OR, 1U AND), 0U: Trigger Engine 1, 2U: , 1U: , 150U: 트리거 전압 임계값
      // 1U: , 3U: . 1U: , 128U: 트리거 전압 임계값
      uint num7 = AlazarAPI.AlazarSetTriggerOperation(boardHandle, 0U, 0U, 2U, 1U, 150U, 1U, 3U, 1U, 128U);
      if (num7 != 512U)
      {
        this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarSetTriggerOperation failed -- " + AlazarAPI.AlazarErrorToText(num7));
        return false;
      }
      // ADC 보드 핸들, 2U: External trigger coupling(1U:AC Coupling, 2U:DC Coupling), 0U: TTL 레벨 트리거 신호 사용
      uint num8 = AlazarAPI.AlazarSetExternalTrigger(boardHandle, 2U, 0U);
      if (num8 != 512U)
      {
        this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarSetExternalTrigger failed -- " + AlazarAPI.AlazarErrorToText(num8));
        return false;
      }
      // num9 = 6.5 µs × 100 MS/s
      //      = 6.5 × 10⁻⁶ × 100 × 10⁶
      //      = 650 samples
      uint num9 = (uint) (6.5E-06 * ControlADBoard.samplesPerSec + 0.5);
      // 650 샘플 동안 기다린 뒤부터 ADC 데이터 저장 시작
      uint num10 = AlazarAPI.AlazarSetTriggerDelay(boardHandle, num9);
      if (num10 != 512U)
      {
        this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarSetTriggerDelay failed -- " + AlazarAPI.AlazarErrorToText(num10));
        return false;
      }
      // 0U: 무한 대기 (트리거 올 때까지 기다림)(N × 10 ms (보드별) 후 자동 캡처)
      uint num11 = AlazarAPI.AlazarSetTriggerTimeOut(boardHandle, 0U);
      if (num11 != 512U)
      {
        this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarSetTriggerTimeOut failed -- " + AlazarAPI.AlazarErrorToText(num11));
        return false;
      }
      // ADC 보드 핸들, 1U: AUX I/O 모드(AUX 출력 (Output))(0U: AUX 입력 (External input)), 1U: AUX I/O 파라미터(Trigger 출력)
      uint num12 = AlazarAPI.AlazarConfigureAuxIO(boardHandle, 1U, 1U);
      if (num12 == 512U)
        return true;
      this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarConfigureAuxIO failed -- " + AlazarAPI.AlazarErrorToText(num12));
      return false;
    }

    private void AcquisitionParametersInitialize()
    {
      this.preTriggerSamples = 0U;                  // 트리거 신호 전 Samples 취득
      this.postTriggerSamples = this.ascan;         // 트리거 신호 후 Samples 취득
      this.recordsPerBuffer = this.para.BScan * 2U; // 왕복 스캔 구조
      this.channelMask = 1U;                        // CH A만 사용
      this.channelCount = 1U;                       // 단일 채널
      this.saveData = false;
      this.fileStream = (FileStream) null;
      this.success = true;
      Console.WriteLine("Initialize done...");
    }

    public event EventHandler<byte[]> DHEvent;

    protected void OnDHHandler(byte[] e)
    {
      EventHandler<byte[]> dhEvent = this.DHEvent;
      if (dhEvent == null)
        return;
      dhEvent((object) this, e);
    }

    public unsafe bool AcquireData(uint recordsPerAcquisition)
    {
      this.acquireDataStop = false; // 수집 종료 플래그 초기화
      uint num1; // num1: board memory Size
      byte num2; // num2: Sample 당 bit 수

      // Digitizer 보드의 채널 정보 조회
      uint channelInfo = AlazarAPI.AlazarGetChannelInfo(this.handle, &num1, &num2);
      if (channelInfo != 512U) // 512 == ApiSuccess
      {
        this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarGetChannelInfo failed -- " + AlazarAPI.AlazarErrorToText(channelInfo));
        return false;
      }
      
      uint num3 = ((uint) num2 + 7U) / 8U; // Sample 당 Byte 계산
      uint num4 = this.preTriggerSamples + this.postTriggerSamples; // Record 당 총 sample 수 (pre + post trigger)
      uint num5 = num3 * num4; //Record 당 byte 수
      uint length = num5 * this.recordsPerBuffer * this.channelCount; // Buffer 당 byte 수

    // 디버깅 출력
      Console.WriteLine("bytesPerSample : " + num3.ToString());
      Console.WriteLine("samplesPerRecord : " + num4.ToString());
      Console.WriteLine("bytesPerRecord : " + num5.ToString());
      Console.WriteLine("bytesPerBuffer : " + length.ToString());
      Console.WriteLine();

      byte[] e = new byte[(int) length]; // Raw 데이터 저장 버퍼
    // byte -> short 변환을 위한 구조체
      ControlADBoard.ByteToShortArray byteToShortArray = new ControlADBoard.ByteToShortArray();
      byteToShortArray.bytes = e;

      try
      {
        if (this.saveData) // 데이터 파일 저장 옵션
          this.fileStream = File.Create("data.bin");
        fixed (short* numPtr = byteToShortArray.shorts)
        {
          // Record 크기 설정 (pre/post trigger)
          uint num6 = AlazarAPI.AlazarSetRecordSize(this.handle, this.preTriggerSamples, this.postTriggerSamples);
          if (num6 != 512U)
          {
            this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarSetRecordSize failed -- " + AlazarAPI.AlazarErrorToText(num6));
            throw new Exception("[" + DateTime.Now.ToString("F") + "] Error: AlazarSetRecordSize failed -- " + AlazarAPI.AlazarErrorToText(num6));
          }
          // Async DMA acquisition 준비
          uint num7 = AlazarAPI.AlazarBeforeAsyncRead(this.handle, this.channelMask, -(int) this.preTriggerSamples, num4, this.recordsPerBuffer, recordsPerAcquisition, 545U);
          if (num7 != 512U)
          {
            this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarBeforeAsyncRead failed -- " + AlazarAPI.AlazarErrorToText(num7));
            throw new Exception("[" + DateTime.Now.ToString("F") + "] Error: AlazarBeforeAsyncRead failed -- " + AlazarAPI.AlazarErrorToText(num7));
          }
          // Digitizer 캡처 시작...
          uint num8 = AlazarAPI.AlazarStartCapture(this.handle);
          if (num8 != 512U)
          {
            this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarStartCapture failed -- " + AlazarAPI.AlazarErrorToText(num8));
            throw new Exception("[" + DateTime.Now.ToString("F") + "] Error: AlazarStartCapture failed -- " + AlazarAPI.AlazarErrorToText(num8));
          }
          uint num9 = 0;  // 수신된 buffer 수
          long num10 = 0; // 총 수신 byte

          // Data 수집 루프
          while (true)
          {
            uint num11 = 1728000000; // timeout(ms)
            uint num12 = AlazarAPI.AlazarWaitNextAsyncBufferComplete(this.handle, (void*) numPtr, length, num11); // DMA buffer 완료 대기
            switch (num12)
            {
              case 512: // 정산 수신
                this.OnDHHandler(e);
                break;
              case 589: // 전송 완료
                this.OnDHHandler(e);
                break;
              default: // error
                this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarWaitNextAsyncBufferComplete failed -- " + AlazarAPI.AlazarErrorToText(num12));
                break;
            }
            ++num9;
            num10 += (long) length;
            if (!this.acquireDataStop) // Data 취득 종료 여부 확인
                        {
              Console.WriteLine("Running acquire data : " + this.count.ToString());
              ++this.count;
            }
            else
              break;
          }
          Console.WriteLine("Aborted...\n");
          Console.WriteLine("Acquire Data Done... total count : " + this.count.ToString());
        }
      }
      catch (Exception ex)
      {
        this.ErrorMessage.Add(ex.ToString());
        this.success = false;
        Console.WriteLine(ex.ToString());
      }
      finally
      {
        if (this.fileStream != null)
          this.fileStream.Close(); // 파일 닫기
        uint num13 = AlazarAPI.AlazarAbortAsyncRead(this.handle); // Data 취득 중지
        if (num13 != 512U)
          this.errorMessage.Add("[" + DateTime.Now.ToString("F") + "] Error: AlazarAbortAsyncRead failed -- " + AlazarAPI.AlazarErrorToText(num13));
        Console.WriteLine("Finally");
      }
      return this.success;
    }

    public void StopAcquireData() => this.acquireDataStop = true;

    [StructLayout(LayoutKind.Explicit)]
    internal struct ByteToShortArray
    {
      [FieldOffset(0)]
      public byte[] bytes;
      [FieldOffset(0)]
      public short[] shorts;
    }
  }
}
