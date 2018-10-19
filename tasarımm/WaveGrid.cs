using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace WaveSim
{
    class WaveGrid
    {
        // Constants
        const int MinDimension = 5;//min boyut
        const double Damping = 0.96;    // SAVE: 0.96 ıslatma
        const double SmoothingFactor = 2.0;     //Hızdan ziyade prüzsüzleştirmeye daha cok ağırlık verir.

        //Özel üye verileri
        private Point3DCollection _ptBuffer1;
        private Point3DCollection _ptBuffer2;
        private Int32Collection _triangleIndices;
        private Random _rnd = new Random(48339);

        private int _dimension;

        // bufferların içerdiği pointerlar
        //    - Current: En yeni veriler
        //    - Old: Daha önceki veriler
        // Bu iki işaretçi, arabellekleri dolaştırırken ptBuffer1 / ptBuffer2'ye işaret ederek değiş tokuş yapacaktır.
        private Point3DCollection _currBuffer;
        private Point3DCollection _oldBuffer;

        /// <summary>
        /// Belirli bir boyutun yeni grid'inin oluşturulması
        /// </summary>
        /// <param name="Dimension"></param>
        /// // Wawe grid fonksiyonu grid oluşturuyo
        public WaveGrid(int Dimension)
        {
            if (Dimension < MinDimension)
                throw new ApplicationException(string.Format("Dimension must be at least {0}", MinDimension.ToString()));
            // kare şeklinde buffer oluşturuyo
            _ptBuffer1 = new Point3DCollection(Dimension * Dimension);
            _ptBuffer2 = new Point3DCollection(Dimension * Dimension);
            // dikdörtgen şeklinde alan yaratıyo
            _triangleIndices = new Int32Collection((Dimension - 1) * (Dimension - 1) * 2);

            _dimension = Dimension;

            InitializePointsAndTriangles();

            _currBuffer = _ptBuffer2;
            _oldBuffer = _ptBuffer1;
        }

        /// <summary>
        ///Altta olan grid'e erişim
        /// </summary>
        public Point3DCollection Points
        {
            get { return _currBuffer; }
        }

        /// <summary>
        /// Altta yatan triangle index collection a erişim 
        /// </summary>
        public Int32Collection TriangleIndices
        {
            get { return _triangleIndices; }
        }

        /// <summary>
        ///  X & Y için grid boyutu aynı 
        /// </summary>
        public int Dimension
        {
            get { return _dimension; }
        }

        /// <summary>
        /// Grid'de herhangi bir konumda yeni bir hareket yaratın:yüksekliği
        /// PeakValue +/- Delta.  (Bu aralıkta rastgele değer) //tepe değeri +/- Delta
        /// </summary>
        /// <param name="BasePeakValue">grid da yeni tepe yükseliği</param> taban tepe değeri
        /// <param name="PlusOrMinus">Gerçek değeri elde etmek için BasePeakValue'dan eklenecek / alta kadar olan maksimum miktarda</param>
        /// <param name="PeakWidth"># pixels genişliği, [1,4]</param> // tepe genişliği
        /// // Girişte belirtilen damla sayısı kadar bu fonksiyon döndürülerek setleme gerçekleşiyo
        public void SetRandomPeak(double BasePeakValue, double Delta, int PeakWidth)
        {
            if ((PeakWidth < 1) || (PeakWidth > (_dimension / 2)))
                throw new ApplicationException("WaveGrid.SetRandomPeak: PeakWidth param must be <= half the dimension");
            // oluşturulan random sayı satır ve sütuna atılıyo
            int row = (int)(_rnd.NextDouble() * ((double)_dimension - 1.0));
            int col = (int)(_rnd.NextDouble() * ((double)_dimension - 1.0));

            //Arayan 0,0 zirve belirlediğinde, daima 0,0 olarak varsayılır, bu nedenle delta eklemek
            if (BasePeakValue == 0.0)
                Delta = 0.0;

            double PeakValue = BasePeakValue + (_rnd.NextDouble() * 2 * Delta) - Delta;

            // sol üst köşede satır / süt kullanılacaktır. Ancak, ayarlayın, eğer öyleyse
            // bizi ızgaradan çıkarır.
            if ((row + (PeakWidth - 1)) > (_dimension - 1))
                row = _dimension - PeakWidth;
            if ((col + (PeakWidth - 1)) > (_dimension - 1))
                col = _dimension - PeakWidth;

            // Verileri değiştir
            for (int ir = row; ir < (row + PeakWidth); ir++)
                for (int ic = col; ic < (col + PeakWidth); ic++)
                {
                    Point3D pt = _oldBuffer[(ir * _dimension) + ic];
                    pt.Y = pt.Y + (int)PeakValue;
                    _oldBuffer[(ir * _dimension) + ic] = pt;
                }
        }

        /// <summary>
        ///Şebeke arka kenarı boyunca dalga oluşturun
        /// Duvar
        /// </summary>
        /// <param name="WaveHeight"></param>
      

        /// <summary>
        /// Tamponları yerinde bırakın, ancak en yeni olanını değiştirme gösterimini değiştirin.
        /// </summary>
        private void SwapBuffers()
        {
            Point3DCollection temp = _currBuffer;
            _currBuffer = _oldBuffer;
            _oldBuffer = temp;
        }

        /// <summary>
        /// Noktaları / üçgenleri temizleyin ve yeniler
        /// </summary>
        /// <param name="grid"></param>
        private void InitializePointsAndTriangles()
        {
            _ptBuffer1.Clear();
            _ptBuffer2.Clear();
            _triangleIndices.Clear();

            int nCurrIndex = 0;     // March through 1-D arrays

            for (int row = 0; row < _dimension; row++)
            {
                for (int col = 0; col < _dimension; col++)
                {
                    //ızgarada, X / Y değerleri yalnızca satır / sütun sayılarıdır
                    _ptBuffer1.Add(new Point3D(col, 0.0, row));

                    // Yeni kare tamamlandığında, 2 üçgen ekleyin
                    if ((row > 0) && (col > 0))
                    {
                        // Triangle 1
                        _triangleIndices.Add(nCurrIndex - _dimension - 1);
                        _triangleIndices.Add(nCurrIndex);
                        _triangleIndices.Add(nCurrIndex - _dimension);

                        // Triangle 2
                        _triangleIndices.Add(nCurrIndex - _dimension - 1);
                        _triangleIndices.Add(nCurrIndex - 1);
                        _triangleIndices.Add(nCurrIndex);
                    }

                    nCurrIndex++;
                }
            }

            //2. tampon, yalnızca 2. Z değeri setine sahip
            _ptBuffer2 = _ptBuffer1.Clone();
        }

        /// <summary>
        /// Örgü içindeki tüm noktaların yüksekliğini 0.0 olarak ayarlayın. Tamponları da sıfırlar
        /// original state.
        /// </summary>
        public void FlattenGrid()
        {
            Point3D pt;

            for (int i = 0; i < (_dimension * _dimension); i++)
            {
                pt = _ptBuffer1[i];
                pt.Y = 0.0;
                _ptBuffer1[i] = pt;
            }

            _ptBuffer2 = _ptBuffer1.Clone();
            _currBuffer = _ptBuffer2;
            _oldBuffer = _ptBuffer1;
        }

        /// <summary>
        /// Önceki iki duruma dayalı olarak, ızgaranın bir sonraki durumunu belirleyin.
        /// Bu dalgalanmaları dışarıya doğru yayma etkisine sahip olacak.
        /// </summary>
        public void ProcessWater()
        {
            // Eski tampona yazdığımızı unutmayın; bu tampon daha sonra bizim tamponumuz haline gelecektir.
            //    "current" tampon ve akım eski haline gelecektir.  
            // Yani _currBuffer'da başlayan şeyler _oldBuffer'a geçer ve biz 
            // _currBuffer'a yeni veri yazın. Fakat işaretçileri değiştirdiğimiz için, 
            // biz aslında verileri etrafında taşımak zorunda değilsiniz.

            // Verileri hesaplarken, etrafındaki hücreler için veri üretmiyoruz
            // ızgaranın kenarı, çünkü veri yumuşatma tüm bitişik
            // Hücreler. Dolayısıyla, [0, n-1] 'i çalıştırmak yerine, [1, n-2]' yi çalıştırıyoruz.

            double velocity;    // Eskiden güncele değişim oranı
            double smoothed;    // Bitişik hücreler tarafından pürüzsüzleştirildi.
            double newHeight;
            int neighbors;

            int nPtIndex = 0;   // 1 Boyutlu nokta dizisi boyunca yürüyen endeks

            // Y değerinin yüksekliği (animasyon yaptığımız değer) olduğunu unutmayın.
            for (int row = 0; row < _dimension; row++)
            {
                for (int col = 0; col < _dimension; col++)
                {
                    velocity = -1.0 * _oldBuffer[nPtIndex].Y;     // sıra, sütun
                    smoothed = 0.0;

                    neighbors = 0;
                    if (row > 0)    // satır-1, sütun
                    {
                        smoothed += _currBuffer[nPtIndex - _dimension].Y;
                        neighbors++;
                    }

                    if (row < (_dimension - 1))   // satır-1, sütun
                    {
                        smoothed += _currBuffer[nPtIndex + _dimension].Y;
                        neighbors++;
                    }

                    if (col > 0)          // sıra, col-1
                    {
                        smoothed += _currBuffer[nPtIndex - 1].Y;
                        neighbors++;
                    }

                    if (col < (_dimension - 1))   // sıra, col+1
                    {
                        smoothed += _currBuffer[nPtIndex + 1].Y;
                        neighbors++;
                    }

                    // Her zaman en az 2 komşu olacak mı? Dalgalanmayı azaltıyo
                    smoothed /= (double)neighbors;

                    // Yeni yükseklik pürüzsüzleştirmenin ve hızın bileşimi. smothing factor düzleşme süresi 
                    newHeight = smoothed * SmoothingFactor + velocity;

                    // ıslatma
                    newHeight = newHeight * Damping;

                    // Eski tampona yeni veri yazıyoruz yeni veri dediği dalganın genişlemesi
                    Point3D pt = _oldBuffer[nPtIndex];
                    pt.Y = newHeight;   // satır, sütun
                    _oldBuffer[nPtIndex] = pt;

                    nPtIndex++;
                }
            }

            SwapBuffers();
        }
    }
}
