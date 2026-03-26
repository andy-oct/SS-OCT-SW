using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    ///   

    public partial class MainWindow : Window
    {
        private ViewModel viewModel;


        public bool SIMULATION_DAQ = true;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = FindResource("viewModel")  as ViewModel;
        }

        private void btnInternalStart_Click(object sender, RoutedEventArgs e)
        {
            viewModel.StartInternal();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Stop();
        }

        private void btnStopProgram_Click(object sender, RoutedEventArgs e)
        {
            viewModel.StopProgram();
        }

        private void btnMirrorCal_Click(object sender, RoutedEventArgs e)
        {
            viewModel.StartMirrorCalibration();
        }

        private void btnUpdateCal_Click(object sender, RoutedEventArgs e)
        {
            viewModel.UpdateCalFile();
        }

        private void btnRepeat_Click(object sender, RoutedEventArgs e)
        {
            viewModel.StartRepeat();
        }

        private void btnTurn_Click(object sender, RoutedEventArgs e)
        {
            viewModel.TurnInternal();
        }

        private void btnManual_Click(object sender, RoutedEventArgs e)
        {
            viewModel.StartManualTest();
        }

        private void btnExternal_Click(object sender, RoutedEventArgs e)
        {
            viewModel.StartExternal();
        }
    }
}
