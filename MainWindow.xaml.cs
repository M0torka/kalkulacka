using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace kalkulacka
{
    public partial class MainWindow : Window
    {
        private double? _accumulator = null;
        private string? _pendingOperator = null;
        private bool _isNewEntry = true;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Number_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
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
            Display.Text = "0";
            _accumulator = null;
            _pendingOperator = null;
            _isNewEntry = true;
        }

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
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
    }
}