using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using System.Collections.Generic;

namespace kalkulacka
{
    public partial class MainWindow : Window
    {
        private double? _accumulator = null;
        private string? _pendingOperator = null;
        private bool _isNewEntry = true;
        private bool _isDark = false;

        // Particles
        private readonly List<Particle> _particles = new();
        private readonly Random _rand = new();

        public MainWindow()
        {
            InitializeComponent();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void Number_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            // feedback
            TriggerButtonFeedback(btn);

            var digit = btn.Content.ToString() ?? "";
            if (_isNewEntry || Display.Text == "0")
            {
                Display.Text = digit;
                _isNewEntry = false;
            }
            else
            {
                Display.Text += digit;
            }
        }

        private void Decimal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b) TriggerButtonFeedback(b);

            if (_isNewEntry)
            {
                Display.Text = "0.";
                _isNewEntry = false;
                return;
            }

            if (!Display.Text.Contains("."))
                Display.Text += ".";
        }

        private void Operator_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            // feedback
            TriggerButtonFeedback(btn);

            var op = btn.Tag?.ToString() ?? btn.Content.ToString();

            if (!_isNewEntry)
            {
                if (_accumulator == null)
                    _accumulator = ParseDisplay();
                else if (_pendingOperator != null)
                    _accumulator = Calculate(_accumulator.Value, ParseDisplay(), _pendingOperator);
            }

            _pendingOperator = op;
            _isNewEntry = true;
            UpdateDisplayFromAccumulator();
        }

        private void Equals_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b) TriggerButtonFeedback(b);

            if (_pendingOperator == null || _accumulator == null)
                return;

            var right = ParseDisplay();
            var result = Calculate(_accumulator.Value, right, _pendingOperator);
            Display.Text = FormatNumber(result);
            _accumulator = null;
            _pendingOperator = null;
            _isNewEntry = true;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b) TriggerButtonFeedback(b);

            Display.Text = "0";
            _accumulator = null;
            _pendingOperator = null;
            _isNewEntry = true;
        }

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b) TriggerButtonFeedback(b);

            if (_isNewEntry)
            {
                Display.Text = "0";
                return;
            }

            if (Display.Text.Length <= 1)
            {
                Display.Text = "0";
                _isNewEntry = true;
            }
            else
            {
                Display.Text = Display.Text[..^1];
            }
        }

        private void Negate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b) TriggerButtonFeedback(b);

            var value = ParseDisplay();
            value = -value;
            Display.Text = FormatNumber(value);
        }

        private double ParseDisplay()
        {
            if (double.TryParse(Display.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;

            // Fallback: try current culture
            double.TryParse(Display.Text, out v);
            return v;
        }

        private void UpdateDisplayFromAccumulator()
        {
            if (_accumulator != null)
                Display.Text = FormatNumber(_accumulator.Value);
        }

        private static string FormatNumber(double value)
        {
            // Limit precision and avoid scientific notation for normal results
            return value.ToString("G12", CultureInfo.InvariantCulture);
        }

        private static double Calculate(double left, double right, string op)
        {
            return op switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => right == 0 ? double.NaN : left / right,
                _ => right
            };
        }

        private void DarkMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn) TriggerButtonFeedback(btn);

            _isDark = !_isDark;

            if (_isDark)
            {
                // dark brushes
                Resources["AppBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                Resources["AppForegroundBrush"] = new SolidColorBrush(Colors.White);
                Resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                Resources["ButtonForegroundBrush"] = new SolidColorBrush(Colors.White);
                Resources["DisplayBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(20, 20, 20));
                Resources["DisplayForegroundBrush"] = new SolidColorBrush(Colors.White);
                DarkModeButton.Content = "Light Mode";
            }
            else
            {
                // light brushes
                Resources["AppBackgroundBrush"] = new SolidColorBrush(Colors.White);
                Resources["AppForegroundBrush"] = new SolidColorBrush(Colors.Black);
                Resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                Resources["ButtonForegroundBrush"] = new SolidColorBrush(Colors.Black);
                Resources["DisplayBackgroundBrush"] = new SolidColorBrush(Colors.White);
                Resources["DisplayForegroundBrush"] = new SolidColorBrush(Colors.Black);
                DarkModeButton.Content = "Dark Mode";
            }

            // Apply foreground/background to window and display explicitly
            Background = (Brush)Resources["AppBackgroundBrush"];
            Foreground = (Brush)Resources["AppForegroundBrush"];
            Display.Background = (Brush)Resources["DisplayBackgroundBrush"];
            Display.Foreground = (Brush)Resources["DisplayForegroundBrush"];
        }

        // --- Particles implementation ---
        private void TriggerButtonFeedback(Button btn)
        {
            // ensure each button has its own ScaleTransform
            if (btn.RenderTransform is not ScaleTransform s)
            {
                s = new ScaleTransform(1, 1);
                btn.RenderTransform = s;
                btn.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            var anim = new DoubleAnimation(1, 1.2, TimeSpan.FromMilliseconds(120)) { AutoReverse = true };
            s.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            s.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

            SpawnParticles(btn);
        }

        private void SpawnParticles(Button source)
        {
            // position of button center in Window coordinates
            var btnPos = source.TransformToAncestor(this).Transform(new Point(0, 0));
            var btnCenter = new Point(btnPos.X + source.ActualWidth / 2, btnPos.Y + source.ActualHeight / 2);

            int count = _rand.Next(7, 11);
            for (int i = 0; i < count; i++)
            {
                var ellipse = new Ellipse
                {
                    Width = _rand.Next(6, 12),
                    Height = _rand.Next(6, 12),
                    Fill = new SolidColorBrush(RandomParticleColor()),
                    Opacity = 1
                };

                // starting position (convert window coords to canvas coords)
                Canvas.SetLeft(ellipse, btnCenter.X - ellipse.Width / 2);
                Canvas.SetTop(ellipse, btnCenter.Y - ellipse.Height / 2);
                ParticleCanvas.Children.Add(ellipse);

                // initial velocity (random outward)
                double angle = (_rand.NextDouble() * Math.PI) - Math.PI / 2; // -90deg +- 90deg so mostly upwards
                double speed = 120 + _rand.NextDouble() * 180; // px/s
                var vx = Math.Cos(angle) * speed;
                var vy = Math.Sin(angle) * speed;

                var p = new Particle
                {
                    Shape = ellipse,
                    X = btnCenter.X - ellipse.Width / 2,
                    Y = btnCenter.Y - ellipse.Height / 2,
                    VX = vx,
                    VY = vy,
                    Life = 2.5 // seconds
                };

                _particles.Add(p);
            }
        }

        private Color RandomParticleColor()
        {
            var pick = _rand.Next(3);
            return pick switch
            {
                0 => Color.FromRgb(255, 200, 0), // yellow
                1 => Color.FromRgb(255, 120, 0), // orange
                _ => Color.FromRgb(220, 30, 30), // red
            };
        }

        private DateTime _lastFrame = DateTime.Now;
        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            var dt = (now - _lastFrame).TotalSeconds;
            _lastFrame = now;
            if (dt <= 0) return;

            // simple gravity constant
            const double gravity = 600; // px/s^2
            const double airResistance = 0.98; // per second factor (applied multiplicatively per frame)

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.VY += gravity * dt;
                p.X += p.VX * dt;
                p.Y += p.VY * dt;

                // simple ground collision at bottom of window (bounce a bit and lose energy)
                double ground = ActualHeight - 10; // leave margin
                if (p.Y > ground)
                {
                    p.Y = ground;
                    p.VY = -p.VY * 0.35; // bounce with energy loss
                    p.VX *= 0.6; // slow down horizontally

                    // small friction, if very slow remove
                    if (Math.Abs(p.VY) < 30 && Math.Abs(p.VX) < 10)
                    {
                        p.Life = 0.1; // let fade out quickly
                    }
                }

                // apply air resistance
                var resistance = Math.Pow(airResistance, dt);
                p.VX *= resistance;
                p.VY *= resistance;

                // update opacity based on life
                p.Life -= dt;
                double opacity = Math.Max(0, Math.Min(1, p.Life / 2.5));

                Canvas.SetLeft(p.Shape, p.X);
                Canvas.SetTop(p.Shape, p.Y);
                p.Shape.Opacity = opacity;

                if (p.Life <= 0 || p.Opacity <= 0.02)
                {
                    ParticleCanvas.Children.Remove(p.Shape);
                    _particles.RemoveAt(i);
                }
            }
        }

        private class Particle
        {
            public Shape? Shape;
            public double X;
            public double Y;
            public double VX;
            public double VY;
            public double Life;
            public double Opacity { get; set; } = 1;
        }
    }
}