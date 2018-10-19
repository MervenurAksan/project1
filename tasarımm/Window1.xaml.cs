using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WaveSim
{
    /// <summary>
    /// Window1.xaml için etkileşim mantığı
    /// </summary>
    public partial class Window1 : Window
    {
        private Vector3D zoomDelta;

        private WaveGrid _grid;
        private bool _rendering;
        private double _lastTimeRendered;
        private Random _rnd = new Random(1234);

        // Yağmur damlası parametreleri. Negatif amplitüd çok az kuleye neden olur
        // düşey vurduktan hemen sonra dikey olarak atlamak için su.
        private double _splashAmplitude; // Yağmur damlalarının sıçramasına neden olan ortalama yükseklik (derinlik, negatif olduğundan).  
        private double _splashDelta = 1.0;      // Gerçek sıçrama yüksekliği Ampl +/- Delta (rasgele)
        private double _raindropPeriodInMS;
        private int _dropSize;

        // Deneyecek değerler:
        //   GridSize=20, RenderPeriod=125
        //   GridSize=50, RenderPeriod=50
        private const int GridSize = 250; //50;    
        private const double RenderPeriodInMS = 60; //50;    

        public Window1()
        {
            InitializeComponent();

            _splashAmplitude = -3.0;
            slidPeakHeight.Value = -1.0 * _splashAmplitude;

            _raindropPeriodInMS = 35.0;
            slidNumDrops.Value = 1.0 / (_raindropPeriodInMS / 1000.0);

            _dropSize = 1;
          

            // Grid ayarlama
            _grid = new WaveGrid(GridSize);
            meshMain.Positions = _grid.Points;
            meshMain.TriangleIndices = _grid.TriangleIndices;

            // WheelMouse'daki her değişiklikte orijinal mesafenin belirli bir % 'ini yakınlaştırıp uzaklaştırırız
            const double ZoomPctEachWheelChange = 0.02;
            zoomDelta = Vector3D.Multiply(ZoomPctEachWheelChange, camMain.LookDirection);
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                // Zoom in
                camMain.Position = Point3D.Add(camMain.Position, zoomDelta);
            else
                // Zoom out
                camMain.Position = Point3D.Subtract(camMain.Position, zoomDelta);
        }

        // basla/durdur 
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!_rendering)
            {
                //_grid = new WaveGrid(GridSize);        //Yeni grid bufferi resetler
                _grid.FlattenGrid();
                meshMain.Positions = _grid.Points;

                _lastTimeRendered = 0.0;
                CompositionTarget.Rendering += new EventHandler(CompositionTarget_Rendering);
                btnStart.Content = "Durdur";
                _rendering = true;
            }
            else
            {
                CompositionTarget.Rendering -= new EventHandler(CompositionTarget_Rendering);
                btnStart.Content = "Başlat";
                _rendering = false;
            }
        }

        void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            RenderingEventArgs rargs = (RenderingEventArgs)e;
            if ((rargs.RenderingTime.TotalMilliseconds - _lastTimeRendered) > RenderPeriodInMS)
            {
                // Performans için, örgüden Positions koleksiyonunu çıkartın
                // (see http://blogs.msdn.com/timothyc/archive/2006/08/31/734308.aspx)
                meshMain.Positions = null;

                // grid de sonraki yinelemeyi yapılır, dalgaları yaymak
                double NumDropsThisTime = RenderPeriodInMS / _raindropPeriodInMS;

                // Bu noktada damla sayısı için bir sonuç gibi bir şey var.
                // 2.25. Tamsayı kısmı (ör. 2 damla), daha sonra üçüncü damla için% 25 şans vermeyi vereceğiz.
                int NumDrops = (int)NumDropsThisTime;   // trunc
                for (int i = 0; i < NumDrops; i++)
                    _grid.SetRandomPeak(_splashAmplitude, _splashDelta, _dropSize);

                if ((NumDropsThisTime - NumDrops) > 0)
                {
                    double DropChance = NumDropsThisTime - NumDrops;
                    if (_rnd.NextDouble() <= DropChance)
                        _grid.SetRandomPeak(_splashAmplitude, _splashDelta, _dropSize);
                }

                _grid.ProcessWater();

                // Ardından, yeni Z değerlerini kullanmak için ağımızı güncelleyin.
                meshMain.Positions = _grid.Points;

                _lastTimeRendered = rargs.RenderingTime.TotalMilliseconds;
            }
        }

        private void slidPeakHeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Kaydırmaç [0,30] çalışır, bu nedenle amplitüdümüz [-30, 0] geçer. 
            // Negatif genlik istenir çünkü küçük kuleler oluşturmalıyız damla düştüğünde
            _splashAmplitude = -1.0 * slidPeakHeight.Value;
        }

        private void slidNumDrops_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Slider, 1000 daha fazla damla (her ms için 1) ve daha azını temsil eden 1 (1000 milimetre ms) temsil ederek [1,1000] 'den çalışır. Bu kaydırıcıyı kullanıcıya doğal görünmesi için yapar. Fakat gerçek döneme girmek için (ms)
            _raindropPeriodInMS = (1.0 / slidNumDrops.Value) * 1000.0;
        }

        

       
    }
}
