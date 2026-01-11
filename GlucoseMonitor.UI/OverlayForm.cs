using GlucoseMonitor.Core.Interfaces;
using GlucoseMonitor.Core.Models;

namespace GlucoseMonitor.UI;

public partial class OverlayForm : Form
{
    private readonly IGlucoseDataService _glucoseService;
    private readonly ILogger? _logger;
    private Label? _glucoseLabel; // main value + arrow (yellow)
    private Label? _deltaLabel;   // delta with units (red)
    private Label? _timeLabel;    // time-ago (green)
    private Label? _recentLabel;  // compact history (values + times) [deprecated]
    private TableLayoutPanel? _historyTable; // 3x5 table for Values/Changes/Times
    private Label[,]? _historyCells; // [row, col]

    public OverlayForm(IGlucoseDataService glucoseService, ILogger? logger, double opacity = 0.8)
    {
        _glucoseService = glucoseService;
        _logger = logger;

        InitializeComponent();

        Opacity = opacity;
        LoadPosition();
    }

    private void InitializeComponent()
    {
        Text = "Glucose Overlay";
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(270, 145); // compact like the screenshot
        BackColor = Color.Black;

        // ROOT layout: 2 rows (Top header, History table)
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(4),
            Margin = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 53)); // header
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 47)); // history

        // Header layout: 2 columns. Left: big glucose; Right: 2 rows (delta, time)
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); // big number
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); // delta/time stack

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _glucoseLabel = new Label
        {
            Text = "-- ? --",
            ForeColor = Color.Yellow,
            Font = new Font("Consolas", 42, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            Margin = new Padding(0)
        };

        _deltaLabel = new Label
        {
            Text = "",
            ForeColor = Color.Red,
            Font = new Font("Consolas", 12, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            Margin = new Padding(0, 4, 0, 2)
        };

        _timeLabel = new Label
        {
            Text = "",
            ForeColor = Color.Lime,
            Font = new Font("Consolas", 12, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            Margin = new Padding(0, 2, 0, 4)
        };

        rightPanel.Controls.Add(_deltaLabel, 0, 0);
        rightPanel.Controls.Add(_timeLabel, 0, 1);
        header.Controls.Add(_glucoseLabel, 0, 0);
        header.Controls.Add(rightPanel, 1, 0);

        // Build history table (3 rows x 5 columns) with equal widths
        _historyTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ColumnCount = 5,
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        for (int c = 0; c < 5; c++)
        {
            _historyTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        }
        for (int r = 0; r < 3; r++)
        {
            _historyTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        }

        _historyCells = new Label[3,5];
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 5; c++)
            {
                var cell = new Label
                {
                    Text = "",
                    ForeColor = Color.LightGray,
                    Font = new Font("Consolas", 9, FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    BackColor = Color.Black,
                    Margin = new Padding(0)
                };
                _historyCells[r, c] = cell;
                _historyTable.Controls.Add(cell, c, r);
            }
        }

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(_historyTable, 0, 1);
        Controls.Add(root);

        // Make draggable
        MouseDown += OverlayForm_MouseDown;
        MouseMove += OverlayForm_MouseMove;
        MouseUp += OverlayForm_MouseUp;

        _glucoseLabel.MouseDown += OverlayForm_MouseDown;
        _glucoseLabel.MouseMove += OverlayForm_MouseMove;
        _glucoseLabel.MouseUp += OverlayForm_MouseUp;
        _deltaLabel.MouseDown += OverlayForm_MouseDown;
        _deltaLabel.MouseMove += OverlayForm_MouseMove;
        _deltaLabel.MouseUp += OverlayForm_MouseUp;
        _timeLabel.MouseDown += OverlayForm_MouseDown;
        _timeLabel.MouseMove += OverlayForm_MouseMove;
        _timeLabel.MouseUp += OverlayForm_MouseUp;
    }

    private bool _isDragging = false;
    private Point _dragOffset;

    private void OverlayForm_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragOffset = e.Location;
            if (sender == _glucoseLabel)
            {
                _dragOffset = new Point(e.X, e.Y);
            }
        }
    }

    private void OverlayForm_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            Point newLocation;
            if (sender == _glucoseLabel)
            {
                newLocation = new Point(
                    Location.X + e.X - _dragOffset.X,
                    Location.Y + e.Y - _dragOffset.Y);
            }
            else
            {
                newLocation = new Point(
                    e.X + Location.X - _dragOffset.X,
                    e.Y + Location.Y - _dragOffset.Y);
            }

            Location = newLocation;
        }
    }

    private void OverlayForm_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            SavePosition();
        }
    }

    public void UpdateGlucose(GlucoseReading reading)
    {
        void updateUi()
        {
            if (_glucoseLabel != null)
            {
                _glucoseLabel.Text = $"{reading.Value:F0} {reading.GetDirectionArrow()}";
                _glucoseLabel.ForeColor = Color.Yellow;
            }
            if (_deltaLabel != null)
            {
                var sign = reading.Delta >= 0 ? "+" : string.Empty;
                _deltaLabel.Text = $"{sign}{reading.Delta:F1} {reading.Units}";
                _deltaLabel.ForeColor = Color.Red;
            }
            if (_timeLabel != null)
            {
                var age = DateTime.Now - reading.Timestamp;
                string text = age.TotalMinutes < 1
                    ? "just now"
                    : age.TotalMinutes < 60
                        ? $"{(int)age.TotalMinutes} minute{((int)age.TotalMinutes==1?"":"s")} ago"
                        : $"{(int)age.TotalHours} hour{((int)age.TotalHours==1?"":"s")} ago";
                _timeLabel.Text = text;
                _timeLabel.ForeColor = Color.Lime;
            }
        }

        if (InvokeRequired)
        {
            Invoke(updateUi);
        }
        else
        {
            updateUi();
        }
    }

    public void ApplyOpacity(double opacity)
    {
        Opacity = Math.Min(1.0, Math.Max(0.2, opacity));
    }

    public void UpdateRecentHistory(IEnumerable<GlucoseReading> readings)
    {
        void updateUi()
        {
            var ordered = readings?.OrderByDescending(r => r.Timestamp).ToList() ?? new List<GlucoseReading>();

            // Ensure exactly 5 columns: take first 5 (newest→oldest), else pad with blanks on the RIGHT
            if (ordered.Count > 5)
            {
                ordered = ordered.Take(5).ToList();
            }
            else if (ordered.Count < 5)
            {
                var padCount = 5 - ordered.Count;
                var padding = Enumerable.Repeat<GlucoseReading?>(null, padCount)
                    .Select(_ => new GlucoseReading { Value = double.NaN, Delta = 0, Timestamp = DateTime.MinValue, Units = "" })
                    .ToList();
                ordered = ordered.Concat(padding).ToList();
            }

            if (_historyTable != null && _historyCells != null)
            {
                for (int c = 0; c < 5; c++)
                {
                    var r0 = ordered[c];
                    // Values row (0)
                    _historyCells[0, c].Text = double.IsNaN(r0.Value) ? "" : r0.Value.ToString("F0");

                    // Changes row (1)
                    if (double.IsNaN(r0.Value))
                    {
                        _historyCells[1, c].Text = "";
                    }
                    else
                    {
                        // With newest on the left, compute change vs next (older) column to the right
                        if (c == 4 || double.IsNaN(ordered[c + 1].Value))
                        {
                            _historyCells[1, c].Text = "+0";
                        }
                        else
                        {
                            var delta = r0.Value - ordered[c + 1].Value;
                            _historyCells[1, c].Text = delta >= 0 ? $"+{delta:F0}" : $"{delta:F0}";
                        }
                    }

                    // Times row (2)
                    _historyCells[2, c].Text = double.IsNaN(r0.Value) || r0.Timestamp == DateTime.MinValue
                        ? ""
                        : r0.Timestamp.ToString("HH:mm");
                }
            }
            else if (_recentLabel != null)
            {
                // Fallback to text block if table not available
                var values = string.Join(" ", ordered.Select(r => double.IsNaN(r.Value) ? "" : r.Value.ToString("F0")));
                var changesList = new List<string>();
                for (int i = 0; i < ordered.Count; i++)
                {
                    if (i == ordered.Count - 1 || double.IsNaN(ordered[i].Value) || double.IsNaN(ordered[i + 1].Value))
                        changesList.Add("+0");
                    else
                    {
                        var delta = ordered[i].Value - ordered[i + 1].Value;
                        changesList.Add(delta >= 0 ? $"+{delta:F0}" : $"{delta:F0}");
                    }
                }
                var changes = string.Join(" ", changesList);
                var times = string.Join(" ", ordered.Select(r => double.IsNaN(r.Value) ? "" : r.Timestamp.ToString("HH:mm")));
                _recentLabel.Text = $"Values: {values}\nChanges: {changes}\nTimes: {times}";
            }
        }

        if (InvokeRequired)
        {
            Invoke(updateUi);
        }
        else
        {
            updateUi();
        }
    }

    private void LoadPosition()
    {
        try
        {
            var positionFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GlucoseMonitor",
                "overlay_position.txt");

            if (File.Exists(positionFile))
            {
                var lines = File.ReadAllLines(positionFile);
                if (lines.Length >= 2 &&
                    int.TryParse(lines[0], out int x) &&
                    int.TryParse(lines[1], out int y))
                {
                    // Ensure position is visible on current screen configuration
                    var bounds = Screen.GetWorkingArea(new Point(x, y));
                    if (bounds.Contains(x, y))
                    {
                        Location = new Point(x, y);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load overlay position: {ex.Message}");
        }

        // Default position - top right corner
        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(screen.Right - Width - 20, screen.Top + 20);
    }

    private void SavePosition()
    {
        try
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GlucoseMonitor");

            Directory.CreateDirectory(configDir);

            var positionFile = Path.Combine(configDir, "overlay_position.txt");
            File.WriteAllLines(positionFile, new[] { Location.X.ToString(), Location.Y.ToString() });
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to save overlay position: {ex.Message}");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SavePosition();
        base.OnFormClosing(e);
    }
}