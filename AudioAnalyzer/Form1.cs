using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Un4seen.Bass;
using Un4seen.BassWasapi;

namespace AudioAnalyzer
{
    public partial class Form1 : Form
    {
        List<ProgressBar> bars = new List<ProgressBar>();
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Init();
            StartAnalysis();
        }

        // BASS Process
        private WASAPIPROC _WasapiProcess = new WASAPIPROC(Process);
        private Thread _AnalysisThread;
        private const int BANDS = 10;
        private float[] _data = new float[512];
        // Analysis settings
        private int _SamplingRate = 44100;
        private int _DeviceCode = 17;

        // BASS initialization method
        private void Init()
        {
            bool result = false;

            foreach (Control control in this.Controls)
            {
                if (control.GetType() == typeof(ProgressBar))
                {
                    bars.Add((ProgressBar)control);
                }
            }

            // Initialize BASS on default device
            result = Bass.BASS_Init(0, _SamplingRate, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);

            if (!result)
            {
                throw new Exception(Bass.BASS_ErrorGetCode().ToString());
            }

            // Initialize WASAPI
            result = BassWasapi.BASS_WASAPI_Init(_DeviceCode, 0, 0, BASSWASAPIInit.BASS_WASAPI_BUFFER, 1f, 0.05f, _WasapiProcess, IntPtr.Zero);

            if (!result)
            {
                throw new Exception(Bass.BASS_ErrorGetCode().ToString());
            }

            BassWasapi.BASS_WASAPI_Start();
            Thread.Sleep(500);
        }

        // Starts a new Analysis Thread
        public void StartAnalysis()
        {
            // Kills currently running analysis thread if alive
            if (_AnalysisThread != null && _AnalysisThread.IsAlive)
            {
                _AnalysisThread.Abort();
            }

            // Starts a new high-priority thread
            _AnalysisThread = new Thread(new ThreadStart(this.ThreadProcSafe));

            _AnalysisThread.Priority = ThreadPriority.Highest;
            _AnalysisThread.Start();
        }

        // a thread-safe call on the TextBox control. 
        private void ThreadProcSafe()
        {
            while (true)
            {
                Thread.Sleep(5);
                var bands = TakeSample();
                double bandSum = bands.Sum();
                for (int i = 0; i < bands.Length; i++)
                {
                    double barPercent = bands[i] / bandSum * 100;
                    ThreadHelperClass.UpdateProgressBar(this, bars[i], Convert.ToInt32(barPercent));
                }
                    
            }

        }

        private double[] TakeSample()
        {
            int[] BandRange = { 4, 8, 18, 38, 48, 94, 140, 186, 466, 1022, 22000 };
            double[] BandsTemp = new double[BANDS];
            int n = 0;
            float sum = 0;
            int level = BassWasapi.BASS_WASAPI_GetLevel();
            int ret = BassWasapi.BASS_WASAPI_GetData(_data, (int)BASSData.BASS_DATA_FFT1024); //get channel fft data
            if (ret < -1) return null;
            for (int i = 0; i < _data.Length; i++)
            {
                //Console.WriteLine("Frequency: " + i * _SamplingRate / 1024 + " value: " + _data[i]);
                sum += _data[i];

                if (i == BandRange[n])
                {
                    BandsTemp[n++] = (BANDS * sum) / 1024;
                    Console.WriteLine("Band: " + BandRange[n] + " magnitude: " + sum);
                    sum = 0;
                }
            }

            return BandsTemp;
        }

        // WASAPI callback, required for continuous recording
        private static int Process(IntPtr buffer, int length, IntPtr user)
        {
            return length;
        }
    }

    public static class ThreadHelperClass
    {
        delegate void SetTextCallback(Form f, ProgressBar ctrl, int value);

        public static void UpdateProgressBar(Form form, ProgressBar bar, int value)
        {
            // InvokeRequired required compares the thread ID of the 
            // calling thread to the thread ID of the creating thread. 
            // If these threads are different, it returns true. 
            if (bar.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(UpdateProgressBar);
                form.Invoke(d, new object[] { form, bar, value });
            }
            else
            {
                bar.Value = value;
            }
        }
    }
}
