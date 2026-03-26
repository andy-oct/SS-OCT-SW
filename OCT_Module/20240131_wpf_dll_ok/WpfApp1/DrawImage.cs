using DLL_RFC;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace WpfApp1
{
    internal class DrawImage
    {
        private InputParameters para;
        private byte[] data;
        private int realBscan;
        private int cutLength = 1500;

        public DrawImage(InputParameters para)
        {
            this.para = para;
            //realBscan = (int)(para.BScan - para.NumberOfCutLength);
            realBscan = 957;
            data = new byte[realBscan * cutLength];
        }

        private Mat grayImage;
        public WriteableBitmap OutputImage(float[][] rawData)
        {
            //for (int i = 0; i < realBscan; i++)
            //{
            //    for (int j = 0; j < rawData[0].Length; j++)
            //    {
            //        data[i * cutLength + j] = (byte)rawData[i][j];
            //        if (data[i * cutLength + j] > 85)               //Threshold
            //            data[i * cutLength + j] = 255;
            //    }
            //}

            Parallel.For(0, realBscan, i =>
            {
                for (int j = 0; j < rawData[0].Length; j++)
                {
                    data[i * cutLength + j] = (byte)rawData[i][j];
                    if (data[i * cutLength + j] > para.Threshold)               //Threshold
                        data[i * cutLength + j] = 255;
                }
            });

            grayImage = new Mat(realBscan, cutLength, MatType.CV_8UC1, data);
            Cv2.Resize(grayImage, grayImage, new OpenCvSharp.Size(500, 800));
            Cv2.Rotate(grayImage, grayImage, RotateFlags.Rotate90Clockwise);

            return OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(grayImage);
        }
    }
}
