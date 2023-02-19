using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace DRFront
{
    // ■■ ユーザ回路のトップを選択するためのウィンドウ ■■
    public partial class TopSelectorWindow : Window
    {
        public ObservableCollection<EntityHierarchyItem> HierarchyCollection;
        public string ReturnValue;

        public TopSelectorWindow()
        {
            InitializeComponent();
            HierarchyCollection = new ObservableCollection<EntityHierarchyItem>();
            ReturnValue = null;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            lstHierarchy.ItemsSource = HierarchyCollection;

        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EntityHierarchyItem item = (EntityHierarchyItem)lstHierarchy.SelectedItem;
            if (item == null)
                return;
            ReturnValue = item.Name;
            Close();
        }
    }
}
