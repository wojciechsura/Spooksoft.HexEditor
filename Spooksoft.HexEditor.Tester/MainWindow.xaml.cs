using Spooksoft.HexEditor.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
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

namespace Spooksoft.HexEditor.Tester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var ms = new MemoryStream();
            for (int i = 0; i < 2048; i++)
                ms.WriteByte((byte)(i % 256));

            ms.Seek(0, SeekOrigin.Begin);

            heEditor.Document = new HexByteContainer(ms);
        }
    }
}
