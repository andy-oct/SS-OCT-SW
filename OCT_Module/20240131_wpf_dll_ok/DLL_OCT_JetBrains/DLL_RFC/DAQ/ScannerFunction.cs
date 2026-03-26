// Type: DLL_RFC.DAQ.ScannerFunction
// Assembly: DLL_RFC, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F1BFAA5E-869A-4B1B-9510-E2ECF7495221
// Assembly location: C:\Users\samsung\Desktop\DLL_RFC.dll

namespace DLL_RFC.DAQ
{
  internal class ScannerFunction
  {
    public static double[] GenerateTriangeWave(int bscan, double amplitude, double offset)
    {
      double[] triangeWave = new double[bscan * 2];
      for (int index = 0; index < bscan; ++index)
      {
        triangeWave[index] = ((double) index / (double) bscan - 0.5) * 2.0 * amplitude + offset;
        triangeWave[triangeWave.Length - 1 - index] = triangeWave[index];
      }
      return triangeWave;
    }

    public static double[,] GenerateMultiWaveforms_LineScan_XY(
      int bscan,
      double amplitude_x,
      double offset_x,
      double offset_y)
    {
      double[,] waveformsLineScanXy = new double[2, bscan * 2];
      for (int index = 0; index < bscan; ++index)
      {
        waveformsLineScanXy[0, index] = ((double) index / (double) bscan - 0.5) * 2.0 * amplitude_x + offset_x;
        waveformsLineScanXy[0, bscan * 2 - 1 - index] = waveformsLineScanXy[0, index];
      }
      for (int index = 0; index < bscan * 2; ++index)
        waveformsLineScanXy[1, index] = offset_y;
      return waveformsLineScanXy;
    }

    public static double[,] GenerateMultiWaveforms_LineScan_YX(
      int bscan,
      double amplitude_y,
      double offset_x,
      double offset_y)
    {
      double[,] waveformsLineScanYx = new double[2, bscan * 2];
      for (int index = 0; index < bscan; ++index)
      {
        waveformsLineScanYx[1, index] = ((double) index / (double) bscan - 0.5) * 2.0 * amplitude_y + offset_y;
        waveformsLineScanYx[1, bscan * 2 - 1 - index] = waveformsLineScanYx[1, index];
      }
      for (int index = 0; index < bscan * 2; ++index)
        waveformsLineScanYx[0, index] = offset_x;
      return waveformsLineScanYx;
    }

    public static double[,] GenerateMultiWaveforms_CScan(
      int bscan,
      double amplitude_x,
      double offset_x,
      double amplitude_y,
      double offset_y,
      int cscan)
    {
      double[,] multiWaveformsCscan = new double[2, bscan * 2 * cscan];
      for (int index1 = 0; index1 < cscan; ++index1)
      {
        for (int index2 = 0; index2 < bscan; ++index2)
        {
          multiWaveformsCscan[0, index2 + bscan * 2 * index1] = ((double) index2 / (double) bscan - 0.5) * 2.0 * amplitude_x + offset_x;
          multiWaveformsCscan[0, bscan * 2 - 1 - index2 + bscan * 2 * index1] = multiWaveformsCscan[0, index2];
        }
      }
      double[] numArray = new double[cscan];
      for (int index = 0; index < numArray.Length; ++index)
        numArray[index] = (2.0 / (double) numArray.Length * (double) index - 1.0) * amplitude_y + offset_y;
      for (int index3 = 0; index3 < numArray.Length; ++index3)
      {
        for (int index4 = 0; index4 < bscan * 2; ++index4)
          multiWaveformsCscan[1, index4 + index3 * bscan * 2] = numArray[index3];
      }
      return multiWaveformsCscan;
    }
  }
}
