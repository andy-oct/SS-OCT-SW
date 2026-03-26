// Type: DLL_RFC.Processing.ConvertByteToInt
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

using System;
using System.Threading.Tasks;

namespace DLL_RFC.Processing
{
  internal class ConvertByteToInt
  {
    private int[][] intDataBuffer = new int[8][];
    private int count;

    public int[] IntData => this.intDataBuffer[this.count % 8];

    public ConvertByteToInt()
    {
      for (int index = 0; index < this.intDataBuffer.Length; ++index)
        this.intDataBuffer[index] = new int[2048000];
    }

    public void RunConvert(byte[] byteRawData)
    {
      Parallel.For(0, this.IntData.Length, (Action<int>) (i => this.intDataBuffer[this.count % 8][i] = (int) BitConverter.ToUInt16(byteRawData, i * 2)));
      ++this.count;
    }
  }
}
