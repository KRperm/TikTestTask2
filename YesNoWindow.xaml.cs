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

namespace tikTestTask2
{
    /// <summary>
    /// Окно для поддтверждения действий
    /// </summary>
    public partial class YesNoWindow : Window
    {
        public YesNoWindow(string description, Window owner)
        {
            InitializeComponent();
            textBlock.Text = description;
            Owner = owner;
        }

        public void OnClickAccept(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
