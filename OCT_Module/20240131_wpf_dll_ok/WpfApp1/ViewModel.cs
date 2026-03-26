using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DLL_RFC;
using LiveCharts;

namespace WpfApp1
{
    internal class ViewModel : INotifyPropertyChanged
    {
        public InputParameters para;
        private RFCInspection inspection;
        private DrawImage drawImage;    // Raw 데이터 -> 이미지 변환
        private DispatcherTimer timer;  // UI Thread에서 주기적으로 화면 업데이트
        private float[][] rawData;      // OCT Depth Image(2D 데이터)
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            PropertyChangedEventHandler propertyChanged = PropertyChanged;
            if (propertyChanged != null)
            {
                propertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public ChartValues<double> TopProfile { get; private set; } = new ChartValues<double>();    // 상부표면(그래프)
        public ChartValues<double> LaserProfile { get; private set; } = new ChartValues<double>();  // 하부표면(그래프)
        public ChartValues<double> HeightPlot { get; private set; } = new ChartValues<double>();    // 높이변화(그래프)
        public string TopGlassPosition { get; set; }
        public string BottomGlassPosition { get; set; }
        private double[] reverseTopProfile = new double[957];
        private double[] reverseLaserProfile = new double[957];
        public string LaserHeight { get; set; }
        public string GlassThickness { get; set; }
        private double[][] laserProfile_Save;

        public ViewModel()
        {
            para = new InputParameters(); // 파라메터 생성
            inspection = new RFCInspection(para); // Inspection 생성
            inspection.JSHandler += Inspection_JSHandler; // Processing 완료 시 데이터 수신, 이벤트 연결
            drawImage = new DrawImage(para); // 이미지 처리 객체 생성
            timer = new DispatcherTimer(); // Timer 설정
            timer.Interval = TimeSpan.FromMilliseconds(200);
            timer.Tick += Timer_Tick;

            laserProfile_Save = new double[100][]; // 저장 버퍼 초기화
            for (int i = 0; i < laserProfile_Save.Length; i++)
            {
                laserProfile_Save[i] = new double[957];
            }
        }
        private double[] savearray1 = new double[100];
        private int count1;
        private void Inspection_JSHandler(object sender, DLL_RFC.Processing.JSEventArgs e) // Processing 결과 -> UI 데이터로 변환
        {
            if (e != null)
            {
                rawData = e.DepthImage; // Raw 데이터 저장

                TopProfile.Clear(); // 그래프 초기화
                LaserProfile.Clear(); // 그래프 초기화
                if (count1 % 100 == 0)
                    HeightPlot.Clear(); // 그래프 초기화(100개마다)

                for (int i = 0; i < reverseTopProfile.Length; i++) // 데이터 Reverse + 부호 반전 (좌우 반전 + 좌표계 변환(OCT Depth 방향 맞추기))
                {
                    reverseTopProfile[reverseTopProfile.Length - 1 - i] = e.TopProfile[i] * (-1);
                    reverseLaserProfile[reverseTopProfile.Length - 1 - i] = e.LaserProfile[i] * (-1);
                }
                Array.Copy(reverseLaserProfile, laserProfile_Save[count % 100], 957); // 데이터 저장

                TopProfile.AddRange(reverseTopProfile); // 그래프 업데이트
                LaserProfile.AddRange(reverseLaserProfile);

                TopGlassPosition = e.TopProfile.Average().ToString("0.00"); // 표면 위치 평균 값
                OnPropertyChanged(nameof(TopGlassPosition));

                BottomGlassPosition = e.LaserProfile.Average().ToString("0.00");
                OnPropertyChanged(nameof(BottomGlassPosition));

                LaserHeight = e.LaserHeight.ToString("0.00");
                OnPropertyChanged(nameof(LaserHeight));

                GlassThickness = e.GlassDepth.ToString("0.00");
                OnPropertyChanged(nameof(GlassThickness));

                HeightPlot.Add(e.LaserHeight);
                savearray1[count1 % 100] = e.LaserHeight;

                count1++;
            }
        }

        public void SaveProfile() // 프로파일 데이터를 파일로 저장
        {
            {
                using (BinaryWriter bw = new BinaryWriter(new FileStream(@"profile.bin", FileMode.Create)))
                {
                    for (int i = 0; i < laserProfile_Save.Length; i++)
                    {
                        for (int j = 0; j < laserProfile_Save[0].Length; j++)
                        {
                            bw.Write(laserProfile_Save[i][j]); // Laser profile(2D) 파일 저장
                        }
                    }
                }
                using (BinaryWriter bw2 = new BinaryWriter(new FileStream(@"laserhieght.bin", FileMode.Create))) // BinaryWriter 사용(빠르고 용량 작음)
                {
                    for (int i = 0; i < 100; i++)
                    {
                        bw2.Write(savearray1[i]);
                    }
                }
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (rawData != null)
            {
                UpdateImage();
            }
        }

        private int count;
        public WriteableBitmap CrossImage { get; private set; } // OCT 단면 이미지
        private void UpdateImage() // Raw 데이터 -> Bitmap 변환 -> UI 표시
        {
            CrossImage = drawImage.OutputImage(rawData);
            OnPropertyChanged("CrossImage"); // UI에 값 변경 알림

            Console.WriteLine("#############");
            Console.WriteLine("UpdateImage : " + count);
            count++;
        }

        public void StartInternal() // 내부 Trigger 기반 측정 시작
        {
            timer.Start();
            inspection.StartInternal();
        }

        public void StartRepeat()
        {
            timer.Start();
            inspection.StartRepeat();
            Stop();

            if (para.Direction == true)
                para.Direction = false;
            else para.Direction = true;
        }

        public void StartExternal() // 외부 Trigger 기반 측정 시작
        {
            timer.Start();
            inspection.StartExternal();
        }

        public void Stop()
        {
            timer.Stop();
            inspection.Stop();
        }

        public void TurnInternal()
        {
            inspection.TurnInternal();
        }

        public void StopProgram()
        {
            inspection.StopProgram();
        }

        public void StartManualTest() // 파일 데이터로 테스트( .bmp 이미지 파일)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                inspection.StartManualTest(openFileDialog.FileName);
            }

            UpdateImage();
        }
        public void StartMirrorCalibration() // 보정 데이터 생성
        {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            bool? result = saveFileDialog.ShowDialog();
            if (result == true)
            {
                inspection.StartMirrorCalibration(saveFileDialog.FileName);
            }

        }

        public void UpdateCalFile() // Calibration 업데이트
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                inspection.UpdateCalibrationFile(openFileDialog.FileName);
            }            
        }
    }
}
