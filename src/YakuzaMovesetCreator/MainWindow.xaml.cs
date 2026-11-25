using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using YakuzaMovesetCore;

namespace YakuzaMovesetCreator;

public partial class MainWindow : Window
{
    private readonly MainViewModel model = new();
    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;
        model.DrawGraph = DrawGraph;
        Loaded += (_, _) => DrawGraph();
    }

    private void DrawGraph()
    {
        GraphCanvas.Children.Clear();
        if (model.SelectedMoveset == null) return;
        var commands = model.SelectedMoveset.Commands.Where(item => item.AnimationOffset.HasValue).Take(48).ToList();
        var positions = commands.Select((item, index) => (item.Offset, X: 20.0 + index % 4 * 260, Y: 20.0 + index / 4 * 62))
            .ToDictionary(item => item.Offset, item => (item.X, item.Y));
        foreach (var edge in model.VisibleEdges)
        {
            if (!positions.TryGetValue(edge.FromCommandOffset, out var start) || !positions.TryGetValue(edge.ToCommandOffset, out var end)) continue;
            var line = new Line { X1 = start.X + 220, Y1 = start.Y + 18, X2 = end.X, Y2 = end.Y + 18,
                Stroke = Brushes.YellowGreen, StrokeThickness = 2, ToolTip = edge.Label };
            GraphCanvas.Children.Add(line);
        }
        foreach (var command in commands)
        {
            var pos = positions[command.Offset];
            var box = new Border { Background = new SolidColorBrush(Color.FromRgb(49, 61, 46)), BorderBrush = Brushes.YellowGreen,
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Width = 225, Height = 39,
                Child = new TextBlock { Text = $"{command.Name}  ·  {command.Offset}", Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) } };
            Canvas.SetLeft(box, pos.X); Canvas.SetTop(box, pos.Y);
            GraphCanvas.Children.Add(box);
        }
    }
}
