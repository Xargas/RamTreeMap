using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RamTreeMap
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private RotateTransform? _progressRotation;

        private readonly double _smallProgramThresholdRelative = 0.01;
        private int _virtualPidForStandbyMemory = -1;
        private int _virtualPidForFreeMemory = -2;

        private ToolTip _sharedToolTip = new ToolTip
        {
            Background = new SolidColorBrush(Colors.Black),
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = 12,
            Padding = new Thickness(8),
            Placement = PlacementMode.Relative,
        };
        
        private readonly List<Color> _colors = new List<Color>
        {
            Colors.SteelBlue,
            Colors.Coral,
            Colors.MediumSeaGreen,
            Colors.Gold,
            Colors.Orchid,
            Colors.CornflowerBlue,
            Colors.Tomato,
            Colors.Teal,
            Colors.Chocolate,
            Colors.CadetBlue,
            Colors.SlateGray,
            Colors.OrangeRed,
            Colors.LimeGreen,
            Colors.DodgerBlue,
            Colors.MediumOrchid
        };

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
            KeyDown += MainWindow_KeyDown;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshData();
        }

        private void AnimateProgressCircleStart()
        {
            _progressRotation = new RotateTransform();
            ProgressCircle.RenderTransform = _progressRotation;
            ProgressCircle.RenderTransformOrigin = new Point(0.5, 0.5);

            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new TimeSpan(0, 0, 2),
                RepeatBehavior = RepeatBehavior.Forever
            };

            _progressRotation.BeginAnimation(RotateTransform.AngleProperty, animation);
        }

        private void AnimateProgressCircleStop()
        {
            ProgressCircle.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        private void RectVisual_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Rectangle rectVisual && rectVisual.Tag != null)
            {
                var rectData = (TreemapRect)rectVisual.Tag;
                _sharedToolTip.Content = $"{rectData.Label}\nRAM: {FormatBytes(rectData.RamUsage)}";
                ToolTipService.SetToolTip(rectVisual, _sharedToolTip);
            }
        }

        private void RectVisual_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Rectangle rectVisual && _sharedToolTip.IsOpen)
            {
                Point mousePosition = e.GetPosition(rectVisual);

                _sharedToolTip.HorizontalOffset = mousePosition.X + 20;
                _sharedToolTip.VerticalOffset = mousePosition.Y + 20;
            }
        }

        private void RenderTreemap()
        {
            TreemapCanvas.Children.Clear();

            if (_viewModel.Processes.Count == 0)
                return;

            double canvasWidth = TreemapCanvas.ActualWidth;
            double canvasHeight = TreemapCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0)
                return;

            var processList = GetFilteredProcessList();

            if (processList.Count == 0)
                return;

            var treemapRects = TreemapLayout.CalculateLayout(processList, canvasWidth, canvasHeight);

            int colorIndex = 0;

            foreach (var rect in treemapRects)
            {
                if (rect.Width <= 0 || rect.Height <= 0)
                    continue;

                #region Visualization rectangle

                var rectVisual = new Rectangle
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    Fill = new SolidColorBrush(_colors[colorIndex % _colors.Count]),
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 2,
                    Tag = rect
                };

                rectVisual.MouseEnter += RectVisual_MouseEnter;
                rectVisual.MouseMove += RectVisual_MouseMove;

                Canvas.SetLeft(rectVisual, rect.X);
                Canvas.SetTop(rectVisual, rect.Y);

                TreemapCanvas.Children.Add(rectVisual);

                #endregion

                #region Text info inside rectangle

                var stackPanel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Orientation = Orientation.Vertical
                };

                var textColor = GetTextColor(_colors[colorIndex % _colors.Count]);
                double fontSize = CalculateFontSize(rect.Width, rect.Height);

                var appNameBlock = new TextBlock
                {
                    Text = GetDisplayName(rect.Label),
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Padding = new Thickness(2),
                    Foreground = textColor
                };
                stackPanel.Children.Add(appNameBlock);

                if (Math.Min(rect.Width, rect.Height) >= 70)
                {
                    var ramBlock = new TextBlock
                    {
                        Text = $"RAM: {FormatBytes(rect.RamUsage)}",
                        FontSize = fontSize - 2,
                        TextAlignment = TextAlignment.Center,
                        Padding = new Thickness(2),
                        Foreground = textColor
                    };
                    stackPanel.Children.Add(ramBlock);
                }

                var textContainer = new Grid
                {
                    Width = rect.Width,
                    Height = rect.Height,
                    Children = { stackPanel }
                };

                Canvas.SetLeft(textContainer, rect.X);
                Canvas.SetTop(textContainer, rect.Y);

                TreemapCanvas.Children.Add(textContainer);

                #endregion

                colorIndex++;
            }
        }

        /// <summary>
        /// Gets the filtered process list based on current toggle states.
        /// </summary>
        private List<ProcessMemoryInfo> GetFilteredProcessList()
        {
            var processList = _viewModel.Processes.ToList();

            long totalMemory = processList.Sum(p => p.RamUsage);
            long smallProgramThresholdAbsolute = (long)(totalMemory * _smallProgramThresholdRelative);

            if (_viewModel.HideSmallPrograms)
            {
                processList = processList.Where(p => p.RamUsage >= smallProgramThresholdAbsolute).ToList();
            }

            if (_viewModel.ShowSystemMemory)
            {
                var memoryService = new ProcessMemoryService();
                var (totalSysMem, usedMem, freeMem, standbyMemory) = memoryService.GetSystemMemoryStats();

                // standbyMemory is the buffered/cached memory that Task Manager shows
                // freeMem is the truly free memory not in use

                if (standbyMemory > smallProgramThresholdAbsolute)
                {
                    processList.Add(new ProcessMemoryInfo(
                        _virtualPidForStandbyMemory,
                        "[Standby Memory]",
                        standbyMemory
                    ));
                }

                if (freeMem > smallProgramThresholdAbsolute)
                {
                    processList.Add(new ProcessMemoryInfo(
                        _virtualPidForFreeMemory,
                        "[Free Memory]",
                        freeMem
                    ));
                }
            }

            return processList;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.#} {sizes[order]}";
        }

        private string GetDisplayName(string appName)
        {
            return appName.Length > 25 ? appName.Substring(0, 22) + "..." : appName;
        }

        private double CalculateFontSize(double width, double height)
        {
            double minDimension = Math.Min(width, height);

            if (minDimension < 40)
                return 8;
            else if (minDimension < 80)
                return 10;
            else if (minDimension < 120)
                return 12;
            else if (minDimension < 180)
                return 14;
            else
                return 16;
        }

        private SolidColorBrush GetTextColor(Color backgroundColor)
        {
            double luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255.0;

            if (luminance > 0.5)
                return new SolidColorBrush(Colors.Black);
            else
                return new SolidColorBrush(Colors.White);
        }

        private void TreemapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel?.Processes.Count > 0 && LoadingOverlay.Visibility == Visibility.Collapsed)
            {
                RenderTreemap();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshData();
        }

        private async Task RefreshData()
        {
            LoadingOverlay.Visibility = Visibility.Visible;

            AnimateProgressCircleStart();
            await _viewModel.LoadProcessDataAsync();
            AnimateProgressCircleStop();

            LoadingOverlay.Visibility = Visibility.Collapsed;
            RenderTreemap();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                e.Handled = true;
                _ = RefreshData();
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                // Double-click to maximize/restore
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                // Single click drag to move
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void HideSmallProgramsButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.HideSmallPrograms = !_viewModel.HideSmallPrograms;

            // Update button appearance to reflect state
            var button = (Button)sender;
            button.Opacity = _viewModel.HideSmallPrograms ? 1.0 : 0.5;

            RenderTreemap();
        }

        private void ShowSystemMemoryButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ShowSystemMemory = !_viewModel.ShowSystemMemory;

            // Update button appearance to reflect state
            var button = (Button)sender;
            button.Opacity = _viewModel.ShowSystemMemory ? 1.0 : 0.5;

            RenderTreemap();
        }
    }
}