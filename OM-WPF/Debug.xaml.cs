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
using System.Windows.Shapes;

namespace OM_WPF
{
    /// <summary>
    /// Interaction logic for Debug.xaml
    /// </summary>
    public partial class Debug : Window
    {
        public Debug()
        {
            InitializeComponent();
        }

        public void Print(string s)
        {
            Output.AppendText(s);
        }

        public void Clear()
        {
            Output.Clear();
        }
    }
}
