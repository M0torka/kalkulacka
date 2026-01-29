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
        // previous fields
        private double? _accumulator = null;
        private string? _pendingOperator = null;
        private bool _isNewEntry = true;
        private bool _isDark = false;

        // New: store entire expression as lists so we evaluate only on '='
        private readonly List<double> _numbers = new();
        private readonly List<string> _operators = new();

        // New: expression/history and current input
        private string _expression = string.Empty; // textual form
        private string _currentInput = "0";      // the number being typed

        // Particles
        private readonly List<Particle> _particles = new();
        private readonly Random _rand = new();

        public MainWindow()
        {
            InitializeComponent();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            UpdateDisplayText();
        }

        private void Number_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            // feedback
            TriggerButtonFeedback(btn);

            var digit = btn.Content.ToString() ?? "";
            if (_isNewEntry || _currentInput == "0")
            {
                _currentInput = digit;
                _isNewEntry = false;
            }
            else
            {
                _currentInput += digit;
            }

            UpdateDisplayText();
        }

        private void Decimal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b) TriggerButtonFeedback(b);

            if (_isNewEntry)
            {
                _currentInput = "0.";
                _isNewEntry = false;
                UpdateDisplayText();
                return;
            }

            if (!_currentInput.Contains("."))
                _currentInput += ".";

            UpdateDisplayText();
        }

        private void Operator_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            // feedback
            TriggerButtonFeedback(btn);

            var op = btn.Tag?.ToString() ?? btn.Content.ToString();

            // If user has typed a number, append it to numbers
            if (!string.IsNullOrEmpty(_currentInput))
            {
                if (double.TryParse(_currentInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                {
                    _numbers.Add(n);
                }
                else
                {
                    // fallback try current culture
                    double.TryParse(_currentInput, out n);
                    _numbers.Add(n);
                }
            }

            // If user pressed operator multiple times, replace last operator
            if (_operators.Count > 0 && _isNewEntry)
            {
                _operators[_operators.Count - 1] = op;
            }
            else
            {
                _operators.Add(op);
            }

            // build expression string
            _expression = BuildExpressionString();

            // prepare for next number
            _currentInput = string.Empty;
            _isNewEntry = true;

            // clear legacy accumulator/pending
            _accumulator = null;
            _pendingOperator = null;

            UpdateDisplayText();
        }

        private void Equals_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b) TriggerButtonFeedback(b);

            // nothing to compute
            if (string.IsNullOrEmpty(_currentInput) && _numbers.Count == 0)
                return;

            // append last typed number if any
            if (!string.IsNullOrEmpty(_currentInput))
            {
                if (double.TryParse(_currentInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                    _numbers.Add(n);
                else
                {
                    double.TryParse(_currentInput, out n);
                    _numbers.Add(n);
                }
            }

            // if there are no operators, just show the number
            if (_operators.Count == 0)
            {
                if (_numbers.Count > 0)
                    _currentInput = FormatNumber(_numbers[^1]);

                _expression = string.Empty;
                _numbers.Clear();
                _operators.Clear();
                _isNewEntry = true;
                UpdateDisplayText();
                return;
            }

            // Evaluate left-to-right
            double result = _numbers.Count > 0 ? _numbers[0] : 0;
            for (int i = 0; i < _operators.Count; i++)
            {
                double right = (i + 1 < _numbers.Count) ? _numbers[i + 1] : 0;
                var op = _operators[i];
                result = Calculate(result, right, op);
            }

            // show only result
            _currentInput = FormatNumber(result);
            _expression = string.Empty;

            // reset lists
            _numbers.Clear();
            _operators.Clear();

            // reset calc state
            _accumulator = null;
            _pendingOperator = null;
            _isNewEntry = true;

            UpdateDisplayText();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b) TriggerButtonFeedback(b);

            _currentInput = "0";
            _expression = string.Empty;
            _numbers.Clear();
            _operators.Clear();
            _accumulator = null;
            _pendingOperator = null;
            _isNewEntry = true;
            UpdateDisplayText();
        }

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b) TriggerButtonFeedback(b);

            if (_isNewEntry)
            {
                _currentInput = "0";
                UpdateDisplayText();
                return;
            }

            if (string.IsNullOrEmpty(_currentInput) || _currentInput.Length <= 1)
            {
                _currentInput = "0";
                _isNewEntry = true;
            }
            else
            {
                _currentInput = _currentInput[..^1];
            }

            UpdateDisplayText();
        }

        private void Negate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b) TriggerButtonFeedback(b);

            var value = ParseDisplay();
            value = -value;
            _currentInput = FormatNumber(value);
            UpdateDisplayText();
        }

        private double ParseDisplay()
        {
            if (double.TryParse(_currentInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;

            // Fallback: try current culture
            double.TryParse(_currentInput, out v);
            return v;
        }

        private void UpdateDisplayFromAccumulator()
        {
            if (_accumulator != null)
            {
                _currentInput = FormatNumber(_accumulator.Value);
                UpdateDisplayText();
            }
        }

        // helper: build expression string from numbers/operators
        private string BuildExpressionString()
        {
            var parts = new List<string>();
            int ncount = _numbers.Count;
            for (int i = 0; i < ncount; i++)
            {
                parts.Add(FormatNumber(_numbers[i]));
                if (i < _operators.Count)
                    parts.Add(_operators[i]);
            }

            // if there's no trailing number but there's a pending operator, include it
            if (_numbers.Count == 0 && _operators.Count > 0)
            {
                parts.Add(_operators[0]);
            }

            return string.Join(" ", parts);
        }

        // new helper: set Display.Text according to expression + current input
        private void UpdateDisplayText()
        {
            if (!string.IsNullOrEmpty(_expression))
            {
                if (string.IsNullOrEmpty(_currentInput))
                    Display.Text = _expression;
                else
                    Display.Text = _expression + " " + _currentInput;
            }
            else
            {
                Display.Text = string.IsNullOrEmpty(_currentInput) ? "0" : _currentInput;
            }
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
                // pick a uniform direction around the full circle so particles fly in all directions
                double angle = _rand.NextDouble() * 2 * Math.PI; // 0..360 degrees
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