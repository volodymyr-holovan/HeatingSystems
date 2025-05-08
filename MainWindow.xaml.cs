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
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HeatingSystems
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Implement the PropertyChanged event required by INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // Helper method to raise the PropertyChanged event
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Expose enum values for ComboBox binding
        public Array WallMaterialEnumValues => Enum.GetValues(typeof(WallMaterialEnum));
        public Array InsulationMaterialEnumValues => Enum.GetValues(typeof(InsulationMaterialEnum));
        public Array WindowMaterialEnumValues => Enum.GetValues(typeof(WindowMaterialEnum));

        private WallMaterialEnum _wallMaterial;
        public WallMaterialEnum WallMaterial
        {
            get => _wallMaterial;
            set
            {
                if (_wallMaterial != value)
                {
                    _wallMaterial = value;
                    GlobalWallMaterial = value;
                    OnPropertyChanged();
                }
            }
        }

        private InsulationMaterialEnum _insulationMaterial;
        public InsulationMaterialEnum InsulationMaterial
        {
            get => _insulationMaterial;
            set
            {
                if (_insulationMaterial != value)
                {
                    _insulationMaterial = value;
                    GlobalInsulationMaterial = value;
                    OnPropertyChanged();
                }
            }
        }

        private WindowMaterialEnum _windowMaterial;
        public WindowMaterialEnum WindowMaterial
        {
            get => _windowMaterial;
            set
            {
                if (_windowMaterial != value)
                {
                    _windowMaterial = value;
                    GlobalWindowMaterial = value;
                    OnPropertyChanged();
                }
            }
        }

        // Add static/global variables for the enums
        public static WallMaterialEnum GlobalWallMaterial;
        public static InsulationMaterialEnum GlobalInsulationMaterial;
        public static WindowMaterialEnum GlobalWindowMaterial;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize default values
            WallMaterial = WallMaterialEnum.Brick;
            InsulationMaterial = InsulationMaterialEnum.None;
            WindowMaterial = WindowMaterialEnum.Wood;

            // Initialize global variables
            GlobalWallMaterial = WallMaterial;
            GlobalInsulationMaterial = InsulationMaterial;
            GlobalWindowMaterial = WindowMaterial;
        }
    }

    // Перелік для матеріалів стін
    public enum WallMaterialEnum
    {
        Brick,
        Concrete,
        Wood,
        Other
    }

    // Перелік для матеріалів утеплення
    public enum InsulationMaterialEnum
    {
        Foam,
        MineralWool,
        None,
        Other
    }

    // Перелік для матеріалів вікон
    public enum WindowMaterialEnum
    {
        Wood,
        PVC,
        Aluminum,
        Other
    }
}

