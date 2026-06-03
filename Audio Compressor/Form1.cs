using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using NAudio.Wave;
using System.Windows.Forms.DataVisualization.Charting;

namespace Audio_Compressor
{
    public partial class Form1 : Form
    {
        private AudioFileReader audioFileReader;
        private WaveOutEvent outputDevice;
        private string currentFilePath = "";
        private System.Windows.Forms.Timer playbackTimer;
        private bool isDragging = false;

        private float[] waveformSamplesOrig;
        private float[] waveformSamplesDecomp;
        private float[] decompressedSamples;

        private BackgroundWorker compressionWorker;
        private CancellationTokenSource cancellationTokenSource;
        private bool isCompressing = false;
        private CompressionSettings currentCompSettings;
        private CompressedAudio currentCompressedAudio;
        private float[] originalSamples;
        private DateTime compressionStartTime;
        private long originalSizeBytes;
        private long compressedSizeBytes;
        private float mse, rmse, snr, psnr;
        private bool qualityCalculated = false;

        // متغير لحفظ معدل البت الأصلي لتجنب استثناء تعطيل القارئ
        private int compressionOriginalBitRate;

        private Panel mainPanel, playerPanel, infoPanel, dropPanel, compressedPlayerPanel;
        private Label lblDropHint, lblStatus;
        private Button btnLoad, btnPlay, btnPause, btnStop;
        private Label lblFileName, lblFileSize, lblDuration, lblSampleRate, lblChannels, lblBitRate, lblEncoding;
        private bool fileLoaded = false;

        private GroupBox grpCompression;
        private ComboBox cmbAlgorithm;
        private NumericUpDown nudTargetSampleRate, nudQuantBits;
        private Button btnCompress, btnDecompressOnly, btnCancel, btnResetComp, btnSaveCompressed, btnLoadCompressed, btnSaveDecompWav;
        private ProgressBar progressBar;
        private Chart chartRatio, chartSpeed, chartSizeBar;
        private Label lblReport;

        private PictureBox waveformBoxOrig;
        private TrackBar seekBar;
        private Label lblPlaybackTime;
        private bool userSeeking = false;

        private PictureBox waveformBoxDecomp;

        private WaveOutEvent compressedOutputDevice;
        private System.Windows.Forms.Timer compressedPlaybackTimer;
        private Button btnCompPlay, btnCompPause, btnCompStop;
        private TrackBar seekBarComp;
        private Label lblCompPlaybackTime;
        private bool userSeekingComp = false;
        private int currentCompPosSamples = 0;
        private int totalCompSamples = 0;
        private DateTime compPlaybackStartTime;

        public Form1()
        {
            InitializeComponent();
            SetupUI();
            SetupDragDrop();
            InitializeCompression();
        }

        private void Form1_Load(object sender, EventArgs e) { }

        // ==================== Setup UI ====================
        private void SetupUI()
        {
            this.Text = "Audio Compressor - Professional Edition";
            this.Size = new Size(1340, 1100);
            this.MinimumSize = new Size(1200, 950);
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            this.StartPosition = FormStartPosition.CenterScreen;

            mainPanel = new Panel()
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(15),
                AutoScroll = true
            };
            this.Controls.Add(mainPanel);

            // Drop panel (waveform display)
            dropPanel = new Panel()
            {
                Location = new Point(15, 15),
                Size = new Size(890, 300),
                BackColor = Color.FromArgb(45, 45, 48),
                BorderStyle = BorderStyle.FixedSingle
            };
            dropPanel.Paint += DropPanel_Paint;
            mainPanel.Controls.Add(dropPanel);

            lblDropHint = new Label()
            {
                Text = "Drag & Drop Audio File Here\nor Click 'Load Audio' to Browse",
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 18, FontStyle.Italic),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            dropPanel.Controls.Add(lblDropHint);

            waveformBoxOrig = new PictureBox()
            {
                Location = new Point(0, 0),
                Size = new Size(445, 300),
                BackColor = Color.FromArgb(40, 40, 43),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Visible = false
            };
            waveformBoxOrig.Paint += WaveformBoxOrig_Paint;
            dropPanel.Controls.Add(waveformBoxOrig);

            waveformBoxDecomp = new PictureBox()
            {
                Location = new Point(445, 0),
                Size = new Size(445, 300),
                BackColor = Color.FromArgb(40, 40, 43),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Visible = false
            };
            waveformBoxDecomp.Paint += WaveformBoxDecomp_Paint;
            dropPanel.Controls.Add(waveformBoxDecomp);

            Label lblOrig = new Label()
            {
                Text = "Original",
                Location = new Point(5, 5),
                AutoSize = true,
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            dropPanel.Controls.Add(lblOrig);
            lblOrig.BringToFront();

            Label lblDecomp = new Label()
            {
                Text = "Decompressed",
                Location = new Point(450, 5),
                AutoSize = true,
                ForeColor = Color.Orange,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            dropPanel.Controls.Add(lblDecomp);
            lblDecomp.BringToFront();

            btnLoad = new Button()
            {
                Text = "Load Audio",
                Location = new Point(730, 8),
                Size = new Size(150, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 140, 220),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnLoad.FlatAppearance.BorderSize = 0;
            btnLoad.Click += BtnLoad_Click;
            dropPanel.Controls.Add(btnLoad);
            btnLoad.BringToFront();

            // Player panel (original)
            playerPanel = new Panel()
            {
                Location = new Point(15, 325),
                Size = new Size(890, 65),
                BackColor = Color.FromArgb(45, 45, 48)
            };
            mainPanel.Controls.Add(playerPanel);

            btnPlay = CreateStyledButton("Play", new Point(15, 12), Color.FromArgb(0, 180, 80), 110, 44);
            btnPlay.Click += BtnPlay_Click;
            btnPlay.Enabled = false;
            playerPanel.Controls.Add(btnPlay);

            btnPause = CreateStyledButton("Pause", new Point(135, 12), Color.FromArgb(255, 180, 0), 110, 44);
            btnPause.Click += BtnPause_Click;
            btnPause.Enabled = false;
            playerPanel.Controls.Add(btnPause);

            btnStop = CreateStyledButton("Stop", new Point(255, 12), Color.FromArgb(220, 50, 50), 110, 44);
            btnStop.Click += BtnStop_Click;
            btnStop.Enabled = false;
            playerPanel.Controls.Add(btnStop);

            seekBar = new TrackBar()
            {
                Location = new Point(380, 14),
                Size = new Size(380, 40),
                BackColor = Color.FromArgb(45, 45, 48),
                TickStyle = TickStyle.None,
                Enabled = false,
                Maximum = 1000
            };
            seekBar.Scroll += SeekBar_Scroll;
            seekBar.MouseDown += (s, e) => userSeeking = true;
            seekBar.MouseUp += (s, e) => userSeeking = false;
            playerPanel.Controls.Add(seekBar);

            lblPlaybackTime = new Label()
            {
                Location = new Point(770, 18),
                Size = new Size(115, 25),
                Text = "00:00 / 00:00",
                ForeColor = Color.White,
                Font = new Font("Consolas", 11),
                TextAlign = ContentAlignment.MiddleLeft
            };
            playerPanel.Controls.Add(lblPlaybackTime);

            // Compressed player panel
            compressedPlayerPanel = new Panel()
            {
                Location = new Point(15, 400),
                Size = new Size(890, 65),
                BackColor = Color.FromArgb(55, 55, 58)
            };
            mainPanel.Controls.Add(compressedPlayerPanel);

            btnDecompressOnly = new Button()
            {
                Text = "Decompress",
                Location = new Point(15, 12),
                Size = new Size(115, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 140, 0),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnDecompressOnly.FlatAppearance.BorderSize = 0;
            btnDecompressOnly.Click += BtnDecompressOnly_Click;
            compressedPlayerPanel.Controls.Add(btnDecompressOnly);

            btnCompPlay = CreateStyledButton("Play", new Point(140, 12), Color.FromArgb(0, 180, 80), 105, 44);
            btnCompPlay.Click += BtnCompPlay_Click;
            btnCompPlay.Enabled = false;
            compressedPlayerPanel.Controls.Add(btnCompPlay);

            btnCompPause = CreateStyledButton("Pause", new Point(255, 12), Color.FromArgb(255, 180, 0), 105, 44);
            btnCompPause.Click += BtnCompPause_Click;
            btnCompPause.Enabled = false;
            compressedPlayerPanel.Controls.Add(btnCompPause);

            btnCompStop = CreateStyledButton("Stop", new Point(370, 12), Color.FromArgb(220, 50, 50), 105, 44);
            btnCompStop.Click += BtnCompStop_Click;
            btnCompStop.Enabled = false;
            compressedPlayerPanel.Controls.Add(btnCompStop);

            btnSaveDecompWav = new Button()
            {
                Text = "Save WAV",
                Location = new Point(485, 12),
                Size = new Size(105, 44),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 200, 200),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnSaveDecompWav.FlatAppearance.BorderSize = 0;
            btnSaveDecompWav.Click += BtnSaveDecompWav_Click;
            compressedPlayerPanel.Controls.Add(btnSaveDecompWav);

            seekBarComp = new TrackBar()
            {
                Location = new Point(600, 14),
                Size = new Size(175, 40),
                BackColor = Color.FromArgb(55, 55, 58),
                TickStyle = TickStyle.None,
                Enabled = false,
                Maximum = 1000
            };
            seekBarComp.Scroll += SeekBarComp_Scroll;
            seekBarComp.MouseDown += (s, e) => userSeekingComp = true;
            seekBarComp.MouseUp += (s, e) => userSeekingComp = false;
            compressedPlayerPanel.Controls.Add(seekBarComp);

            lblCompPlaybackTime = new Label()
            {
                Location = new Point(785, 18),
                Size = new Size(100, 25),
                Text = "00:00 / 00:00",
                ForeColor = Color.White,
                Font = new Font("Consolas", 11),
                TextAlign = ContentAlignment.MiddleLeft
            };
            compressedPlayerPanel.Controls.Add(lblCompPlaybackTime);

            // Information panel
            infoPanel = new Panel()
            {
                Location = new Point(15, 475),
                Size = new Size(890, 160),
                BackColor = Color.FromArgb(45, 45, 48)
            };
            mainPanel.Controls.Add(infoPanel);

            Label lblInfoTitle = new Label()
            {
                Text = "Audio File Information",
                Location = new Point(15, 12),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 200, 255)
            };
            infoPanel.Controls.Add(lblInfoTitle);

            int yStart = 50, lineH = 32;
            lblFileName = CreateInfoLabel("File Name: ---", new Point(20, yStart));
            infoPanel.Controls.Add(lblFileName);
            lblFileSize = CreateInfoLabel("File Size: ---", new Point(20, yStart + lineH));
            infoPanel.Controls.Add(lblFileSize);
            lblDuration = CreateInfoLabel("Duration: ---", new Point(20, yStart + lineH * 2));
            infoPanel.Controls.Add(lblDuration);
            lblSampleRate = CreateInfoLabel("Sample Rate: ---", new Point(360, yStart));
            infoPanel.Controls.Add(lblSampleRate);
            lblChannels = CreateInfoLabel("Channels: ---", new Point(360, yStart + lineH));
            infoPanel.Controls.Add(lblChannels);
            lblBitRate = CreateInfoLabel("Bit Rate: ---", new Point(360, yStart + lineH * 2));
            infoPanel.Controls.Add(lblBitRate);
            lblEncoding = CreateInfoLabel("Encoding: ---", new Point(660, yStart));
            infoPanel.Controls.Add(lblEncoding);

            // Report label (larger)
            lblReport = new Label()
            {
                Location = new Point(15, 648),
                Size = new Size(890, 265),
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 10),
                BackColor = Color.FromArgb(35, 35, 35),
                BorderStyle = BorderStyle.FixedSingle
            };
            mainPanel.Controls.Add(lblReport);

            // Status label
            lblStatus = new Label()
            {
                Text = "Ready",
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 9),
                AutoSize = true,
                Location = new Point(15, 923)
            };
            mainPanel.Controls.Add(lblStatus);

            // Compression group box (enhanced layout)
            grpCompression = new GroupBox()
            {
                Text = "Compression",
                Location = new Point(920, 15),
                Size = new Size(400, 875),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            mainPanel.Controls.Add(grpCompression);

            cmbAlgorithm = new ComboBox()
            {
                Location = new Point(18, 32),
                Width = 364,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10)
            };
            cmbAlgorithm.Items.AddRange(new[] { "Delta Modulation", "DPCM", "Adaptive Delta Modulation", "Nonlinear Quantization", "Predictive Differential Coding" });
            cmbAlgorithm.SelectedIndex = 0;
            grpCompression.Controls.Add(cmbAlgorithm);

            Label lblTargetSR = new Label()
            {
                Text = "Target Sample Rate (Hz):",
                Location = new Point(18, 68),
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9)
            };
            grpCompression.Controls.Add(lblTargetSR);

            nudTargetSampleRate = new NumericUpDown()
            {
                Location = new Point(18, 88),
                Width = 364,
                Minimum = 4000,
                Maximum = 44100,
                Value = 16000,
                Increment = 1000,
                Font = new Font("Segoe UI", 10)
            };
            grpCompression.Controls.Add(nudTargetSampleRate);

            Label lblQuantBits = new Label()
            {
                Text = "Bits / Step Size:",
                Location = new Point(18, 122),
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 9)
            };
            grpCompression.Controls.Add(lblQuantBits);

            nudQuantBits = new NumericUpDown()
            {
                Location = new Point(18, 142),
                Width = 364,
                Minimum = 1,
                Maximum = 16,
                Value = 4,
                DecimalPlaces = 0,
                Font = new Font("Segoe UI", 10)
            };
            grpCompression.Controls.Add(nudQuantBits);

            btnCompress = new Button()
            {
                Text = "Compress",
                Location = new Point(18, 185),
                Size = new Size(175, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 140, 220),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCompress.FlatAppearance.BorderSize = 0;
            btnCompress.Click += BtnCompress_Click;
            grpCompression.Controls.Add(btnCompress);

            btnCancel = new Button()
            {
                Text = "Cancel",
                Location = new Point(207, 185),
                Size = new Size(175, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(200, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += BtnCancel_Click;
            grpCompression.Controls.Add(btnCancel);

            btnResetComp = new Button()
            {
                Text = "Reset Original",
                Location = new Point(18, 237),
                Size = new Size(175, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(80, 80, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnResetComp.FlatAppearance.BorderSize = 0;
            btnResetComp.Click += BtnResetComp_Click;
            grpCompression.Controls.Add(btnResetComp);

            btnSaveCompressed = new Button()
            {
                Text = "Save Compressed",
                Location = new Point(207, 237),
                Size = new Size(175, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 180, 100),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnSaveCompressed.FlatAppearance.BorderSize = 0;
            btnSaveCompressed.Click += BtnSaveCompressed_Click;
            grpCompression.Controls.Add(btnSaveCompressed);

            btnLoadCompressed = new Button()
            {
                Text = "Load Compressed",
                Location = new Point(18, 289),
                Size = new Size(364, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(100, 100, 180),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnLoadCompressed.FlatAppearance.BorderSize = 0;
            btnLoadCompressed.Click += BtnLoadCompressed_Click;
            grpCompression.Controls.Add(btnLoadCompressed);

            progressBar = new ProgressBar()
            {
                Location = new Point(18, 346),
                Width = 364,
                Height = 26
            };
            grpCompression.Controls.Add(progressBar);

            // Chart: Compression Ratio
            chartRatio = new Chart()
            {
                Location = new Point(18, 390),
                Size = new Size(364, 165),
                BackColor = Color.FromArgb(30, 30, 30)
            };
            ChartArea ca1 = new ChartArea() { BackColor = Color.FromArgb(30, 30, 30) };
            ca1.AxisX.LabelStyle.ForeColor = Color.White;
            ca1.AxisY.LabelStyle.ForeColor = Color.White;
            ca1.AxisX.Title = "Progress (%)";          // تم تعديل التسمية
            ca1.AxisY.Title = "Ratio";
            ca1.AxisX.TitleForeColor = Color.White;
            ca1.AxisY.TitleForeColor = Color.White;
            chartRatio.ChartAreas.Add(ca1);
            chartRatio.Series.Add(new Series("Compression Ratio") { ChartType = SeriesChartType.Line, Color = Color.Cyan, BorderWidth = 2 });
            chartRatio.Legends.Clear();
            grpCompression.Controls.Add(chartRatio);

            // Chart: Compression Speed
            chartSpeed = new Chart()
            {
                Location = new Point(18, 568),
                Size = new Size(364, 155),
                BackColor = Color.FromArgb(30, 30, 30)
            };
            ChartArea ca2 = new ChartArea() { BackColor = Color.FromArgb(30, 30, 30) };
            ca2.AxisX.LabelStyle.ForeColor = Color.White;
            ca2.AxisY.LabelStyle.ForeColor = Color.White;
            ca2.AxisX.Title = "Progress (%)";          // تم تعديل التسمية
            ca2.AxisY.Title = "Samples/s";
            ca2.AxisX.TitleForeColor = Color.White;
            ca2.AxisY.TitleForeColor = Color.White;
            chartSpeed.ChartAreas.Add(ca2);
            chartSpeed.Series.Add(new Series("Speed") { ChartType = SeriesChartType.Line, Color = Color.Yellow, BorderWidth = 2 });
            chartSpeed.Legends.Clear();
            grpCompression.Controls.Add(chartSpeed);

            // Chart: Size Comparison Bar
            chartSizeBar = new Chart()
            {
                Location = new Point(18, 736),
                Size = new Size(364, 128),
                BackColor = Color.FromArgb(30, 30, 30)
            };
            ChartArea ca3 = new ChartArea() { BackColor = Color.FromArgb(30, 30, 30) };
            ca3.AxisX.LabelStyle.ForeColor = Color.White;
            ca3.AxisY.LabelStyle.ForeColor = Color.White;
            chartSizeBar.ChartAreas.Add(ca3);
            Series sizeSeries = new Series("Sizes") { ChartType = SeriesChartType.Column, Color = Color.LightGreen };
            sizeSeries.Points.AddXY("Original", 0);
            sizeSeries.Points.AddXY("Compressed", 0);
            chartSizeBar.Series.Add(sizeSeries);
            chartSizeBar.Legends.Clear();
            grpCompression.Controls.Add(chartSizeBar);

            playbackTimer = new System.Windows.Forms.Timer() { Interval = 50 };
            playbackTimer.Tick += PlaybackTimer_Tick;

            compressedPlaybackTimer = new System.Windows.Forms.Timer() { Interval = 50 };
            compressedPlaybackTimer.Tick += CompressedPlaybackTimer_Tick;
        }

        // ==================== Helper Methods ====================
        private Button CreateStyledButton(string text, Point location, Color backColor, int width, int height)
        {
            Button btn = new Button()
            {
                Text = text,
                Location = location,
                Size = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Region = new Region(RoundedRect(new Rectangle(0, 0, width, height), 8));
            return btn;
        }

        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Label CreateInfoLabel(string text, Point location)
        {
            return new Label()
            {
                Text = text,
                Location = location,
                AutoSize = true,
                ForeColor = Color.FromArgb(210, 210, 210),
                Font = new Font("Segoe UI", 10)
            };
        }

        private string FormatSize(long bytes)
        {
            if (bytes >= 1073741824) return $"{bytes / 1073741824.0:F2} GB";
            if (bytes >= 1048576) return $"{bytes / 1048576.0:F2} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F2} KB";
            return $"{bytes} bytes";
        }

        private bool IsAudioFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".wav" || ext == ".mp3" || ext == ".aiff" || ext == ".aif" || ext == ".flac" || ext == ".m4a";
        }

        // ==================== Drag & Drop ====================
        private void SetupDragDrop()
        {
            this.AllowDrop = true;
            this.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length > 0 && IsAudioFile(files[0]))
                    {
                        e.Effect = DragDropEffects.Copy;
                        dropPanel.BackColor = Color.FromArgb(60, 60, 65);
                        isDragging = true;
                        dropPanel.Invalidate();
                    }
                }
            };
            this.DragLeave += (s, e) =>
            {
                isDragging = false;
                dropPanel.BackColor = Color.FromArgb(45, 45, 48);
                dropPanel.Invalidate();
            };
            this.DragDrop += (s, e) =>
            {
                isDragging = false;
                dropPanel.BackColor = Color.FromArgb(45, 45, 48);
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) LoadAudioFile(files[0]);
            };
        }

        private void DropPanel_Paint(object sender, PaintEventArgs e)
        {
            if (isDragging)
            {
                using (Pen pen = new Pen(Color.FromArgb(0, 200, 255), 3) { DashStyle = DashStyle.Dash })
                {
                    e.Graphics.DrawRectangle(pen, 3, 3, dropPanel.Width - 7, dropPanel.Height - 7);
                }
            }
        }

        // ==================== Load & Play Audio ====================
        private void BtnLoad_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "Audio Files|*.wav;*.mp3;*.aiff;*.aif;*.flac;*.m4a|All Files|*.*",
                Title = "Select Audio File"
            })
            {
                if (ofd.ShowDialog() == DialogResult.OK) LoadAudioFile(ofd.FileName);
            }
        }

        private void LoadAudioFile(string path)
        {
            try
            {
                StopPlayback();
                StopCompressedPlayback();
                currentFilePath = path;
                audioFileReader = new AudioFileReader(path);
                fileLoaded = true;
                ExtractWaveformSamples(audioFileReader, out waveformSamplesOrig);
                waveformSamplesDecomp = null;
                UpdateFileInfo(path);
                UpdatePlayerControls(true);
                lblStatus.Text = "File loaded";
                lblDropHint.Visible = false;
                waveformBoxOrig.Visible = true;
                waveformBoxDecomp.Visible = false;
                waveformBoxOrig.Invalidate();
                seekBar.Maximum = (int)audioFileReader.TotalTime.TotalMilliseconds;
                seekBar.Value = 0;
                seekBar.Enabled = true;
                lblPlaybackTime.Text = $"00:00 / {audioFileReader.TotalTime:mm\\:ss}";
                originalSamples = ReadAllMonoSamples(audioFileReader);
                originalSizeBytes = new FileInfo(path).Length;
                ResetCompressionUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading audio file:\n{ex.Message}");
                ResetUI();
            }
        }

        private float[] ReadAllMonoSamples(AudioFileReader reader)
        {
            reader.Position = 0;
            List<float> all = new List<float>();
            float[] buf = new float[4096];
            int read;
            while ((read = reader.Read(buf, 0, buf.Length)) > 0)
            {
                if (reader.WaveFormat.Channels == 1)
                {
                    all.AddRange(buf.Take(read));
                }
                else
                {
                    for (int i = 0; i < read; i += reader.WaveFormat.Channels)
                    {
                        all.Add(buf[i]);
                    }
                }
            }
            reader.Position = 0;
            return all.ToArray();
        }

        private void ExtractWaveformSamples(AudioFileReader reader, out float[] waveform)
        {
            waveform = null;
            if (reader == null) return;
            int count = 1200;
            long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
            waveform = new float[count];
            reader.Position = 0;
            int perPoint = Math.Max(1, (int)(totalSamples / count));
            float[] buf = new float[perPoint * reader.WaveFormat.Channels];
            for (int i = 0; i < count && reader.Position < reader.Length; i++)
            {
                int rd = reader.Read(buf, 0, buf.Length);
                if (rd > 0) waveform[i] = buf.Take(rd).Max(Math.Abs);
            }
            reader.Position = 0;
        }

        private void WaveformBoxOrig_Paint(object sender, PaintEventArgs e)
        {
            if (waveformSamplesOrig != null)
                DrawWaveform(e.Graphics, waveformSamplesOrig, waveformBoxOrig.Width, waveformBoxOrig.Height, Color.Cyan);
        }

        private void WaveformBoxDecomp_Paint(object sender, PaintEventArgs e)
        {
            if (waveformSamplesDecomp != null)
                DrawWaveform(e.Graphics, waveformSamplesDecomp, waveformBoxDecomp.Width, waveformBoxDecomp.Height, Color.Orange);
        }

        private void DrawWaveform(Graphics g, float[] samples, int width, int height, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int mid = height / 2;
            float px = (float)width / samples.Length;
            using (Pen pen = new Pen(color, 2f))
            {
                for (int i = 0; i < samples.Length - 1; i++)
                {
                    float x = i * px;
                    g.DrawLine(pen, x, mid - samples[i] * mid * 0.85f, x + px, mid - samples[i + 1] * mid * 0.85f);
                    g.DrawLine(pen, x, mid + samples[i] * mid * 0.85f, x + px, mid + samples[i + 1] * mid * 0.85f);
                }
            }
        }

        private void UpdateFileInfo(string path)
        {
            FileInfo fi = new FileInfo(path);
            lblFileName.Text = $"File Name: {fi.Name}";
            lblFileSize.Text = $"File Size: {FormatSize(fi.Length)}";
            if (audioFileReader != null)
            {
                lblDuration.Text = $"Duration: {audioFileReader.TotalTime:mm\\:ss\\.fff}";
                lblSampleRate.Text = $"Sample Rate: {audioFileReader.WaveFormat.SampleRate} Hz";
                lblChannels.Text = $"Channels: {audioFileReader.WaveFormat.Channels}";
                lblBitRate.Text = $"Bit Rate: {audioFileReader.WaveFormat.AverageBytesPerSecond * 8} bps";
                lblEncoding.Text = $"Encoding: {audioFileReader.WaveFormat.Encoding} ({audioFileReader.WaveFormat.BitsPerSample}-bit)";
            }
        }

        // ==================== Original Playback Controls ====================
        private void BtnPlay_Click(object sender, EventArgs e)
        {
            if (!fileLoaded || audioFileReader == null) return;
            try
            {
                if (outputDevice == null)
                {
                    outputDevice = new WaveOutEvent();
                    outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
                    outputDevice.Init(audioFileReader);
                }
                if (outputDevice.PlaybackState == PlaybackState.Paused)
                {
                    outputDevice.Play();
                }
                else
                {
                    audioFileReader.Position = 0;
                    outputDevice.Play();
                }
                playbackTimer.Start();
                UpdatePlayerState(PlaybackState.Playing);
                lblStatus.Text = "Playing...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Playback error:\n{ex.Message}");
            }
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            if (outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing)
            {
                outputDevice.Pause();
                playbackTimer.Stop();
                UpdatePlayerState(PlaybackState.Paused);
                lblStatus.Text = "Paused";
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            StopPlayback();
            lblStatus.Text = "Stopped";
        }

        private void StopPlayback()
        {
            playbackTimer.Stop();
            if (outputDevice != null)
            {
                outputDevice.Stop();
                outputDevice.Dispose();
                outputDevice = null;
            }
            if (audioFileReader != null) audioFileReader.Position = 0;
            UpdatePlayerState(PlaybackState.Stopped);
            seekBar.Value = 0;
            lblPlaybackTime.Text = audioFileReader != null ? $"00:00 / {audioFileReader.TotalTime:mm\\:ss}" : "00:00 / 00:00";
        }

        private void OutputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OutputDevice_PlaybackStopped(sender, e)));
                return;
            }
            playbackTimer.Stop();
            UpdatePlayerState(PlaybackState.Stopped);
            seekBar.Value = seekBar.Maximum;
            audioFileReader.Position = 0;
            lblStatus.Text = "Playback completed";
        }

        private void SeekBar_Scroll(object sender, EventArgs e)
        {
            if (userSeeking && audioFileReader != null)
            {
                audioFileReader.CurrentTime = TimeSpan.FromMilliseconds(seekBar.Value);
                waveformBoxOrig.Invalidate();
            }
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (audioFileReader != null && outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing && !userSeeking)
            {
                seekBar.Value = (int)audioFileReader.CurrentTime.TotalMilliseconds;
                lblPlaybackTime.Text = $"{audioFileReader.CurrentTime:mm\\:ss} / {audioFileReader.TotalTime:mm\\:ss}";
            }
        }

        private void UpdatePlayerState(PlaybackState state)
        {
            btnPlay.Enabled = fileLoaded && state != PlaybackState.Playing;
            btnPause.Enabled = state == PlaybackState.Playing;
            btnStop.Enabled = state != PlaybackState.Stopped;
        }

        private void UpdatePlayerControls(bool ready)
        {
            btnPlay.Enabled = ready;
            btnPause.Enabled = false;
            btnStop.Enabled = false;
            seekBar.Enabled = ready;
        }

        // ==================== Compressed Playback Controls ====================
        private void BtnCompPlay_Click(object sender, EventArgs e)
        {
            if (decompressedSamples == null) return;
            try
            {
                StopCompressedPlayback();
                var format = WaveFormat.CreateIeeeFloatWaveFormat(currentCompressedAudio.OriginalSampleRate, 1);
                byte[] bytes = new byte[decompressedSamples.Length * 4];
                Buffer.BlockCopy(decompressedSamples, 0, bytes, 0, bytes.Length);
                int startByte = currentCompPosSamples * 4;
                var provider = new RawSourceWaveStream(bytes, Math.Min(startByte, bytes.Length), bytes.Length - Math.Min(startByte, bytes.Length), format);
                compressedOutputDevice = new WaveOutEvent();
                compressedOutputDevice.PlaybackStopped += CompressedOutputDevice_PlaybackStopped;
                compressedOutputDevice.Init(provider);
                compressedOutputDevice.Play();
                compPlaybackStartTime = DateTime.Now;
                compressedPlaybackTimer.Start();
                UpdateCompressedPlayerState(PlaybackState.Playing);
                lblStatus.Text = "Playing decompressed audio...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Playback error:\n{ex.Message}");
            }
        }

        private void BtnCompPause_Click(object sender, EventArgs e)
        {
            if (compressedOutputDevice?.PlaybackState == PlaybackState.Playing)
            {
                compressedOutputDevice.Pause();
                compressedPlaybackTimer.Stop();
                currentCompPosSamples += (int)((DateTime.Now - compPlaybackStartTime).TotalSeconds * currentCompressedAudio.OriginalSampleRate);
                UpdateCompressedPlayerState(PlaybackState.Paused);
                lblStatus.Text = "Paused";
            }
        }

        private void BtnCompStop_Click(object sender, EventArgs e)
        {
            StopCompressedPlayback();
            currentCompPosSamples = 0;
            seekBarComp.Value = 0;
            lblStatus.Text = "Stopped";
        }

        private void StopCompressedPlayback()
        {
            compressedPlaybackTimer.Stop();
            if (compressedOutputDevice != null)
            {
                compressedOutputDevice.Stop();
                compressedOutputDevice.Dispose();
                compressedOutputDevice = null;
            }
            UpdateCompressedPlayerState(PlaybackState.Stopped);
        }

        private void CompressedOutputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => CompressedOutputDevice_PlaybackStopped(sender, e)));
                return;
            }
            compressedPlaybackTimer.Stop();
            UpdateCompressedPlayerState(PlaybackState.Stopped);
            seekBarComp.Value = seekBarComp.Maximum;
            lblStatus.Text = "Playback completed";
        }

        private void SeekBarComp_Scroll(object sender, EventArgs e)
        {
            if (userSeekingComp && decompressedSamples != null && currentCompressedAudio != null)
            {
                currentCompPosSamples = (int)(seekBarComp.Value / 1000.0 * currentCompressedAudio.OriginalSampleRate);
                if (compressedOutputDevice?.PlaybackState == PlaybackState.Playing ||
                    compressedOutputDevice?.PlaybackState == PlaybackState.Paused)
                {
                    StopCompressedPlayback();
                    BtnCompPlay_Click(null, null);
                }
            }
        }

        private void CompressedPlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (compressedOutputDevice?.PlaybackState == PlaybackState.Playing &&
                decompressedSamples != null &&
                !userSeekingComp &&
                currentCompressedAudio != null)
            {
                double elapsed = (DateTime.Now - compPlaybackStartTime).TotalSeconds;
                int pos = currentCompPosSamples + (int)(elapsed * currentCompressedAudio.OriginalSampleRate);
                if (pos >= totalCompSamples)
                {
                    pos = totalCompSamples;
                    StopCompressedPlayback();
                }
                seekBarComp.Value = (int)(pos / (double)currentCompressedAudio.OriginalSampleRate * 1000);
                lblCompPlaybackTime.Text = $"{TimeSpan.FromSeconds((double)pos / currentCompressedAudio.OriginalSampleRate):mm\\:ss} / {TimeSpan.FromSeconds((double)totalCompSamples / currentCompressedAudio.OriginalSampleRate):mm\\:ss}";
            }
        }

        private void UpdateCompressedPlayerState(PlaybackState state)
        {
            btnCompPlay.Enabled = decompressedSamples != null && state != PlaybackState.Playing;
            btnCompPause.Enabled = state == PlaybackState.Playing;
            btnCompStop.Enabled = state != PlaybackState.Stopped;
        }

        // ==================== UI Reset & Compression Flow ====================
        private void ResetUI()
        {
            fileLoaded = false;
            currentFilePath = "";
            StopPlayback();
            StopCompressedPlayback();
            waveformSamplesOrig = null;
            waveformSamplesDecomp = null;
            waveformBoxOrig.Visible = false;
            waveformBoxDecomp.Visible = false;
            lblDropHint.Visible = true;
            UpdatePlayerControls(false);
            seekBar.Value = 0;
            lblPlaybackTime.Text = "00:00 / 00:00";
            lblFileName.Text = "File Name: ---";
            lblFileSize.Text = "File Size: ---";
            lblDuration.Text = "Duration: ---";
            lblSampleRate.Text = "Sample Rate: ---";
            lblChannels.Text = "Channels: ---";
            lblBitRate.Text = "Bit Rate: ---";
            lblEncoding.Text = "Encoding: ---";
            ResetCompressionUI();
            GC.Collect();
        }

        private void ResetCompressionUI()
        {
            btnDecompressOnly.Enabled = false;
            btnSaveCompressed.Enabled = false;
            btnSaveDecompWav.Enabled = false;
            btnCompPlay.Enabled = false;
            btnCompPause.Enabled = false;
            btnCompStop.Enabled = false;
            seekBarComp.Enabled = false;
            seekBarComp.Value = 0;
            lblCompPlaybackTime.Text = "00:00 / 00:00";
            progressBar.Value = 0;
            chartRatio.Series[0].Points.Clear();
            chartSpeed.Series[0].Points.Clear();
            chartSizeBar.Series[0].Points.Clear();
            chartSizeBar.Series[0].Points.AddXY("Original", 0);
            chartSizeBar.Series[0].Points.AddXY("Compressed", 0);
            lblReport.Text = "";
            currentCompressedAudio = null;
            decompressedSamples = null;
            waveformSamplesDecomp = null;
            qualityCalculated = false;
            waveformBoxDecomp.Visible = false;
            mse = rmse = snr = psnr = 0;
            GC.Collect();
        }

        private void InitializeCompression()
        {
            compressionWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            compressionWorker.DoWork += CompressionWorker_DoWork;
            compressionWorker.ProgressChanged += CompressionWorker_ProgressChanged;
            compressionWorker.RunWorkerCompleted += CompressionWorker_RunWorkerCompleted;
        }

        private void BtnCompress_Click(object sender, EventArgs e)
        {
            if (!fileLoaded || originalSamples == null || isCompressing) return;

            // حفظ معدل البت الأصلي لتجنب استثناء القارئ المحتمل
            compressionOriginalBitRate = audioFileReader.WaveFormat.AverageBytesPerSecond * 8;

            currentCompSettings = new CompressionSettings
            {
                Algorithm = cmbAlgorithm.SelectedItem.ToString(),
                TargetSampleRate = (int)nudTargetSampleRate.Value,
                QuantBits = (int)nudQuantBits.Value
            };
            compressionStartTime = DateTime.Now;
            cancellationTokenSource = new CancellationTokenSource();
            compressionWorker.RunWorkerAsync(currentCompSettings);
            btnCompress.Enabled = false;
            btnCancel.Enabled = true;
            isCompressing = true;
            progressBar.Value = 0;
            chartRatio.Series[0].Points.Clear();
            chartSpeed.Series[0].Points.Clear();
        }

        private void BtnDecompressOnly_Click(object sender, EventArgs e)
        {
            if (currentCompressedAudio == null) return;
            try
            {
                decompressedSamples = currentCompressedAudio.Decode();
                if (originalSamples != null && decompressedSamples.Length != originalSamples.Length)
                    decompressedSamples = MatchLength(decompressedSamples, originalSamples.Length);
                ExtractWaveformSamplesFromArray(decompressedSamples, out waveformSamplesDecomp);
                waveformBoxDecomp.Visible = true;
                waveformBoxDecomp.Invalidate();
                CalculateQualityMetrics(originalSamples, decompressedSamples);
                totalCompSamples = decompressedSamples.Length;
                btnCompPlay.Enabled = true;
                btnCompPause.Enabled = false;
                btnCompStop.Enabled = false;
                btnSaveDecompWav.Enabled = true;
                btnDecompressOnly.Enabled = true;   // تأكيد التفعيل
                seekBarComp.Maximum = (int)((double)totalCompSamples / currentCompressedAudio.OriginalSampleRate * 1000);
                seekBarComp.Value = 0;
                seekBarComp.Enabled = true;
                lblCompPlaybackTime.Text = $"00:00 / {TimeSpan.FromSeconds((double)totalCompSamples / currentCompressedAudio.OriginalSampleRate):mm\\:ss}";
                currentCompPosSamples = 0;
                lblStatus.Text = "Decompressed – ready to play";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Decompression error: " + ex.Message);
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            cancellationTokenSource?.Cancel();
        }

        private void BtnResetComp_Click(object sender, EventArgs e)
        {
            if (!fileLoaded) return;
            StopPlayback();
            StopCompressedPlayback();
            audioFileReader?.Dispose();
            audioFileReader = new AudioFileReader(currentFilePath);
            originalSamples = ReadAllMonoSamples(audioFileReader);

            // تحديث معلومات الملف على الواجهة بعد إعادة التعيين
            UpdateFileInfo(currentFilePath);

            ResetCompressionUI();
            lblStatus.Text = "Reset to original";
        }

        private void BtnSaveCompressed_Click(object sender, EventArgs e)
        {
            if (currentCompressedAudio == null) return;
            using (SaveFileDialog sfd = new SaveFileDialog()
            {
                Filter = "Compressed Audio (*.dat)|*.dat",
                FileName = Path.GetFileNameWithoutExtension(currentFilePath) + "_compressed"
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    currentCompressedAudio.SaveToFile(sfd.FileName);
                    MessageBox.Show("Compressed file saved.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnSaveDecompWav_Click(object sender, EventArgs e)
        {
            if (decompressedSamples == null || currentCompressedAudio == null) return;
            using (SaveFileDialog sfd = new SaveFileDialog()
            {
                Filter = "WAV File (*.wav)|*.wav",
                FileName = Path.GetFileNameWithoutExtension(currentFilePath) + "_decompressed.wav"
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var format = WaveFormat.CreateIeeeFloatWaveFormat(currentCompressedAudio.OriginalSampleRate, 1);
                        using (var writer = new WaveFileWriter(sfd.FileName, format))
                        {
                            writer.WriteSamples(decompressedSamples, 0, decompressedSamples.Length);
                        }
                        MessageBox.Show("Decompressed WAV saved.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error saving WAV: " + ex.Message);
                    }
                }
            }
        }

        private void BtnLoadCompressed_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "Compressed Audio (*.dat)|*.dat" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        CompressedAudio loaded = CompressedAudio.LoadFromFile(ofd.FileName);
                        currentCompressedAudio = loaded;
                        decompressedSamples = loaded.Decode();
                        if (originalSamples != null && decompressedSamples.Length != originalSamples.Length)
                            decompressedSamples = MatchLength(decompressedSamples, originalSamples.Length);
                        ExtractWaveformSamplesFromArray(decompressedSamples, out waveformSamplesDecomp);
                        waveformBoxDecomp.Visible = true;
                        waveformBoxDecomp.Invalidate();
                        CalculateQualityMetrics(originalSamples, decompressedSamples);
                        totalCompSamples = decompressedSamples.Length;
                        currentCompPosSamples = 0;
                        btnCompPlay.Enabled = true;
                        btnCompPause.Enabled = false;
                        btnCompStop.Enabled = false;
                        btnSaveDecompWav.Enabled = true;
                        btnDecompressOnly.Enabled = true;   // تمت الإضافة: تفعيل زر Decompress
                        seekBarComp.Maximum = (int)((double)totalCompSamples / loaded.OriginalSampleRate * 1000);
                        seekBarComp.Value = 0;
                        seekBarComp.Enabled = true;
                        lblCompPlaybackTime.Text = $"00:00 / {TimeSpan.FromSeconds((double)totalCompSamples / loaded.OriginalSampleRate):mm\\:ss}";
                        lblStatus.Text = "Loaded compressed file – ready to play";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: " + ex.Message);
                    }
                }
            }
        }

        private void ExtractWaveformSamplesFromArray(float[] samples, out float[] waveform)
        {
            int count = 1200;
            waveform = new float[count];
            if (samples == null || samples.Length == 0) return;
            int step = Math.Max(1, samples.Length / count);
            for (int i = 0; i < count; i++)
            {
                int idx = i * step;
                if (idx < samples.Length) waveform[i] = Math.Abs(samples[idx]);
            }
        }

        private float[] MatchLength(float[] source, int targetLength)
        {
            if (source.Length == targetLength) return source;
            float[] result = new float[targetLength];
            double ratio = (double)source.Length / targetLength;
            for (int i = 0; i < targetLength; i++)
            {
                double srcIndex = i * ratio;
                int idx1 = (int)srcIndex;
                int idx2 = Math.Min(idx1 + 1, source.Length - 1);
                double frac = srcIndex - idx1;
                result[i] = (float)(source[idx1] * (1 - frac) + source[idx2] * frac);
            }
            return result;
        }

        private void CalculateQualityMetrics(float[] original, float[] decompressed)
        {
            if (original == null || decompressed == null || original.Length != decompressed.Length)
            {
                qualityCalculated = false;
                return;
            }
            double sumSqErr = 0, sumSqOrig = 0;
            for (int i = 0; i < original.Length; i++)
            {
                float err = original[i] - decompressed[i];
                sumSqErr += err * err;
                sumSqOrig += original[i] * original[i];
            }
            mse = (float)(sumSqErr / original.Length);
            rmse = (float)Math.Sqrt(mse);
            psnr = sumSqErr == 0 ? float.MaxValue : (float)(10 * Math.Log10(1.0 / mse));
            snr = sumSqOrig == 0 ? 0 : (float)(10 * Math.Log10(sumSqOrig / sumSqErr));
            qualityCalculated = true;
        }

        // ==================== Compression Worker ====================
        private void CompressionWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            CompressionSettings settings = (CompressionSettings)e.Argument;
            IAudioCompressor compressor = GetCompressor(settings.Algorithm, settings.QuantBits);
            float[] samples = originalSamples;
            if (settings.TargetSampleRate != audioFileReader.WaveFormat.SampleRate)
                samples = Resample(samples, audioFileReader.WaveFormat.SampleRate, settings.TargetSampleRate);

            int totalSamples = samples.Length;
            long totalInputBytes = totalSamples * sizeof(float);
            DateTime start = DateTime.Now;
            BackgroundWorker worker = (BackgroundWorker)sender;

            CompressedAudio result = compressor.Compress(samples, settings.TargetSampleRate,
                (percent, currentOutputBytes) =>
                {
                    if (worker.CancellationPending) throw new OperationCanceledException();
                    double elapsed = (DateTime.Now - start).TotalSeconds;
                    double speed = elapsed > 0 ? (totalSamples * percent / 100.0) / elapsed : 0;
                    double ratio = currentOutputBytes > 0 ? (double)totalInputBytes / currentOutputBytes : 1.0;
                    worker.ReportProgress(percent, new ProgressInfo { Percent = percent, Speed = speed, Ratio = ratio });
                },
                cancellationTokenSource.Token);
            e.Result = result;
        }

        private void CompressionWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.UserState is ProgressInfo info)
            {
                progressBar.Value = info.Percent;
                chartRatio.Series[0].Points.AddXY(chartRatio.Series[0].Points.Count, info.Ratio);
                chartSpeed.Series[0].Points.AddXY(chartSpeed.Series[0].Points.Count, info.Speed);
            }
        }

        private void CompressionWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            isCompressing = false;
            btnCompress.Enabled = true;
            btnCancel.Enabled = false;
            if (e.Error != null && !(e.Error is OperationCanceledException))
            {
                MessageBox.Show($"Compression error: {e.Error.Message}");
            }
            else if (e.Cancelled)
            {
                lblStatus.Text = "Compression cancelled.";
            }
            else
            {
                currentCompressedAudio = (CompressedAudio)e.Result;
                string tempFile = Path.GetTempFileName();
                currentCompressedAudio.SaveToFile(tempFile);
                compressedSizeBytes = new FileInfo(tempFile).Length;
                File.Delete(tempFile);

                decompressedSamples = currentCompressedAudio.Decode();
                if (originalSamples != null && decompressedSamples.Length != originalSamples.Length)
                    decompressedSamples = MatchLength(decompressedSamples, originalSamples.Length);
                ExtractWaveformSamplesFromArray(decompressedSamples, out waveformSamplesDecomp);
                waveformBoxDecomp.Visible = true;
                waveformBoxDecomp.Invalidate();

                CalculateQualityMetrics(originalSamples, decompressedSamples);

                totalCompSamples = decompressedSamples.Length;
                btnDecompressOnly.Enabled = true;
                btnSaveCompressed.Enabled = true;
                btnSaveDecompWav.Enabled = true;
                btnCompPlay.Enabled = true;
                seekBarComp.Maximum = (int)((double)totalCompSamples / currentCompressedAudio.OriginalSampleRate * 1000);
                seekBarComp.Value = 0;
                seekBarComp.Enabled = true;
                lblCompPlaybackTime.Text = $"00:00 / {TimeSpan.FromSeconds((double)totalCompSamples / currentCompressedAudio.OriginalSampleRate):mm\\:ss}";
                lblStatus.Text = "Compression completed";
                ShowReport();
            }
        }

        private void ShowReport()
        {
            double ratio = (double)originalSizeBytes / compressedSizeBytes;
            double saving = (1.0 - (double)compressedSizeBytes / originalSizeBytes) * 100;
            TimeSpan duration = DateTime.Now - compressionStartTime;

            // استخدام المتغير المحفوظ لتجنب الوصول إلى قارئ قد تم تعطيله
            int originalBitRate = compressionOriginalBitRate;

            string qualityStr = qualityCalculated ?
                $"MSE: {mse:F6}  RMSE: {rmse:F4}\nSNR: {snr:F2} dB  PSNR: {psnr:F2} dB" :
                "MSE: N/A  RMSE: N/A\nSNR: N/A  PSNR: N/A";

            lblReport.Text = $"--- Compression Report ---\n" +
                             $"Algorithm: {currentCompSettings.Algorithm}\n" +
                             $"Target SR: {currentCompSettings.TargetSampleRate} Hz\n" +
                             $"Quant bits: {currentCompSettings.QuantBits}\n" +
                             $"Original size: {FormatSize(originalSizeBytes)}\n" +
                             $"Compressed size: {FormatSize(compressedSizeBytes)}\n" +
                             $"Compression ratio: {ratio:F2}:1\n" +
                             $"Space saving: {saving:F1}%\n" +
                             $"Original Bit Rate: {originalBitRate} bps\n" +
                             $"Time taken: {duration.TotalSeconds:F2} sec\n" +
                             qualityStr;

            chartSizeBar.Series[0].Points.Clear();
            chartSizeBar.Series[0].Points.AddXY("Original", originalSizeBytes);
            chartSizeBar.Series[0].Points.AddXY("Compressed", compressedSizeBytes);
            chartSizeBar.Series[0].Points[0].Color = Color.Cyan;
            chartSizeBar.Series[0].Points[1].Color = Color.Orange;
        }

        // ==================== Resample (Catmull-Rom cubic interpolation) ====================
        private float[] Resample(float[] input, int srcRate, int tgtRate)
        {
            if (srcRate == tgtRate) return input;
            double ratio = (double)tgtRate / srcRate;
            int newLen = (int)(input.Length * ratio);
            float[] output = new float[newLen];

            for (int i = 0; i < newLen; i++)
            {
                double srcIndex = i / ratio;
                int index0 = (int)Math.Floor(srcIndex);
                double frac = srcIndex - index0;

                int idx_1 = Math.Max(0, index0 - 1);
                int idx0 = index0;
                int idx1 = Math.Min(input.Length - 1, index0 + 1);
                int idx2 = Math.Min(input.Length - 1, index0 + 2);

                float p0 = input[idx_1];
                float p1 = input[idx0];
                float p2 = input[idx1];
                float p3 = input[idx2];

                // Catmull-Rom coefficients
                float a = -0.5f * p0 + 1.5f * p1 - 1.5f * p2 + 0.5f * p3;
                float b = p0 - 2.5f * p1 + 2.0f * p2 - 0.5f * p3;
                float c = -0.5f * p0 + 0.5f * p2;
                float d = p1;

                output[i] = (float)(a * frac * frac * frac + b * frac * frac + c * frac + d);
            }
            return output;
        }

        // ==================== Compression Algorithm Factory ====================
        private IAudioCompressor GetCompressor(string name, int bits)
        {
            if (name == "Delta Modulation") return new DeltaModulationCompressor();
            if (name == "DPCM") return new DPCMCompressor(bits);
            if (name == "Adaptive Delta Modulation") return new AdaptiveDeltaModulationCompressor();
            if (name == "Nonlinear Quantization") return new NonlinearQuantizationCompressor(bits);
            if (name == "Predictive Differential Coding") return new PredictiveDifferentialCodingCompressor(bits);
            throw new ArgumentException("Unknown algorithm");
        }

        // ==================== Interfaces and Data Classes ====================
        public interface IAudioCompressor
        {
            CompressedAudio Compress(float[] samples, int sampleRate, Action<int, long> reportProgress, CancellationToken cancel);
        }

        public class CompressionSettings
        {
            public string Algorithm;
            public int TargetSampleRate;
            public int QuantBits;
        }

        public class ProgressInfo
        {
            public int Percent;
            public double Speed;
            public double Ratio;
        }

        public class CompressedAudio
        {
            public string Algorithm;
            public int OriginalSampleRate;
            public List<byte[]> ChannelData = new List<byte[]>();
            public float MaxVal;
            public int QuantBits;
            public int SampleCount;

            public float[] Decode()
            {
                if (Algorithm == "Delta Modulation") return DeltaModulationCompressor.Decode(ChannelData[0], OriginalSampleRate, SampleCount, MaxVal);
                if (Algorithm == "DPCM") return DPCMCompressor.Decode(ChannelData[0], OriginalSampleRate, MaxVal, QuantBits, SampleCount);
                if (Algorithm == "Adaptive Delta Modulation") return AdaptiveDeltaModulationCompressor.Decode(ChannelData[0], OriginalSampleRate, SampleCount);
                if (Algorithm == "Nonlinear Quantization") return NonlinearQuantizationCompressor.Decode(ChannelData[0], OriginalSampleRate, MaxVal, QuantBits, SampleCount);
                if (Algorithm == "Predictive Differential Coding") return PredictiveDifferentialCodingCompressor.Decode(ChannelData[0], OriginalSampleRate, MaxVal, QuantBits, SampleCount);
                throw new Exception("Unknown algorithm");
            }

            public long GetSizeBytes()
            {
                long s = 0;
                foreach (var d in ChannelData) s += d.Length;
                return s;
            }

            public void SaveToFile(string path)
            {
                using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(path)))
                {
                    bw.Write(Algorithm);
                    bw.Write(OriginalSampleRate);
                    bw.Write(ChannelData.Count);
                    bw.Write(MaxVal);
                    bw.Write(QuantBits);
                    bw.Write(SampleCount);
                    foreach (var d in ChannelData)
                    {
                        bw.Write(d.Length);
                        bw.Write(d);
                    }
                }
            }

            public static CompressedAudio LoadFromFile(string path)
            {
                using (BinaryReader br = new BinaryReader(File.OpenRead(path)))
                {
                    var ca = new CompressedAudio();
                    ca.Algorithm = br.ReadString();
                    ca.OriginalSampleRate = br.ReadInt32();
                    int count = br.ReadInt32();
                    ca.MaxVal = br.ReadSingle();
                    ca.QuantBits = br.ReadInt32();
                    ca.SampleCount = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        int len = br.ReadInt32();
                        ca.ChannelData.Add(br.ReadBytes(len));
                    }
                    return ca;
                }
            }
        }

        // ==================== Bit Packing Utilities ====================
        private static byte[] PackBits(bool[] bits)
        {
            byte[] b = new byte[(bits.Length + 7) / 8];
            for (int i = 0; i < bits.Length; i++)
                if (bits[i]) b[i / 8] |= (byte)(1 << (i % 8));
            return b;
        }

        private static bool[] UnpackBits(byte[] bytes, int count)
        {
            bool[] bits = new bool[count];
            for (int i = 0; i < count; i++)
                bits[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
            return bits;
        }

        private static byte[] PackValues(int[] values, int bitsPerValue)
        {
            int totalBits = values.Length * bitsPerValue;
            byte[] bytes = new byte[(totalBits + 7) / 8];
            int bitIdx = 0;
            foreach (int val in values)
            {
                for (int b = 0; b < bitsPerValue; b++)
                {
                    if ((val & (1 << b)) != 0) bytes[bitIdx / 8] |= (byte)(1 << (bitIdx % 8));
                    bitIdx++;
                }
            }
            return bytes;
        }

        private static int[] UnpackValues(byte[] bytes, int bitsPerValue, int count)
        {
            int[] vals = new int[count];
            int bitIdx = 0;
            for (int i = 0; i < count; i++)
            {
                int v = 0;
                for (int b = 0; b < bitsPerValue; b++)
                {
                    if ((bytes[bitIdx / 8] & (1 << (bitIdx % 8))) != 0) v |= (1 << b);
                    bitIdx++;
                }
                vals[i] = v;
            }
            return vals;
        }

        // ==================== Compression Algorithm Implementations ====================
        public class DeltaModulationCompressor : IAudioCompressor
        {
            private const float StepFactor = 0.02f;

            public CompressedAudio Compress(float[] samples, int sampleRate, Action<int, long> reportProgress, CancellationToken cancel)
            {
                float maxVal = samples.Max(Math.Abs);
                if (maxVal < 1e-6f) maxVal = 1e-6f;
                float step = maxVal * StepFactor;
                bool[] bits = new bool[samples.Length];
                float recon = 0;
                int total = samples.Length;
                int reportStep = Math.Max(1, total / 100);
                long bytes = 0;
                for (int i = 0; i < total; i++)
                {
                    if (cancel.IsCancellationRequested) throw new OperationCanceledException();
                    bits[i] = samples[i] >= recon;
                    recon += bits[i] ? step : -step;
                    if (i % reportStep == 0 || i == total - 1)
                    {
                        bytes = (i + 8) / 8;
                        reportProgress(i == total - 1 ? 100 : (i * 100) / total, bytes);
                    }
                }
                return new CompressedAudio
                {
                    Algorithm = "Delta Modulation",
                    OriginalSampleRate = sampleRate,
                    ChannelData = new List<byte[]> { PackBits(bits) },
                    SampleCount = total,
                    MaxVal = maxVal
                };
            }

            public static float[] Decode(byte[] packed, int sampleRate, int sampleCount, float maxVal)
            {
                if (maxVal < 1e-6f) maxVal = 1e-6f;
                float step = maxVal * StepFactor;
                bool[] bits = UnpackBits(packed, sampleCount);
                float[] s = new float[sampleCount];
                float rec = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    rec += bits[i] ? step : -step;
                    s[i] = rec;
                }
                return s;
            }
        }

        public class DPCMCompressor : IAudioCompressor
        {
            private int bits;

            public DPCMCompressor(int bits) => this.bits = bits;

            public CompressedAudio Compress(float[] samples, int sampleRate, Action<int, long> reportProgress, CancellationToken cancel)
            {
                float maxVal = samples.Max(Math.Abs);
                if (maxVal < 1e-6f) maxVal = 1e-6f;
                int levels = 1 << bits;
                float step = 2 * maxVal / levels;
                int total = samples.Length;
                int[] quant = new int[total];
                float pred = 0;
                int reportStep = Math.Max(1, total / 100);
                for (int i = 0; i < total; i++)
                {
                    if (cancel.IsCancellationRequested) throw new OperationCanceledException();
                    float diff = samples[i] - pred;
                    int q = (int)Math.Round((diff + maxVal) / step);
                    if (q < 0) q = 0;
                    if (q >= levels) q = levels - 1;
                    quant[i] = q;
                    pred += (q * step - maxVal);
                    if (i % reportStep == 0 || i == total - 1)
                        reportProgress(i == total - 1 ? 100 : (i * 100) / total, (i + 1) * bits / 8);
                }
                return new CompressedAudio
                {
                    Algorithm = "DPCM",
                    OriginalSampleRate = sampleRate,
                    ChannelData = new List<byte[]> { PackValues(quant, bits) },
                    MaxVal = maxVal,
                    QuantBits = bits,
                    SampleCount = total
                };
            }

            public static float[] Decode(byte[] packed, int sampleRate, float maxVal, int bits, int sampleCount)
            {
                if (maxVal < 1e-6f) maxVal = 1e-6f;
                int levels = 1 << bits;
                float step = 2 * maxVal / levels;
                int[] quant = UnpackValues(packed, bits, sampleCount);
                float[] s = new float[sampleCount];
                float pred = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    pred += (quant[i] * step - maxVal);
                    s[i] = pred;
                }
                return s;
            }
        }

        public class AdaptiveDeltaModulationCompressor : IAudioCompressor
        {
            private const float InitStep = 0.02f, StepUp = 1.5f, StepDown = 0.8f;

            public CompressedAudio Compress(float[] samples, int sampleRate, Action<int, long> reportProgress, CancellationToken cancel)
            {
                bool[] bits = new bool[samples.Length];
                float step = InitStep, recon = 0;
                int total = samples.Length;
                int reportStep = Math.Max(1, total / 100);
                long bytes = 0;
                for (int i = 0; i < total; i++)
                {
                    if (cancel.IsCancellationRequested) throw new OperationCanceledException();
                    bool bit = samples[i] >= recon;
                    bits[i] = bit;
                    recon += bit ? step : -step;
                    if (i > 0 && bit == bits[i - 1]) step *= StepUp;
                    else step *= StepDown;
                    if (step < 0.001f) step = 0.001f;
                    if (step > 1.0f) step = 1.0f;
                    if (i % reportStep == 0 || i == total - 1)
                    {
                        bytes = (i + 8) / 8;
                        reportProgress(i == total - 1 ? 100 : (i * 100) / total, bytes);
                    }
                }
                return new CompressedAudio
                {
                    Algorithm = "Adaptive Delta Modulation",
                    OriginalSampleRate = sampleRate,
                    ChannelData = new List<byte[]> { PackBits(bits) },
                    SampleCount = total
                };
            }

            public static float[] Decode(byte[] packed, int sampleRate, int sampleCount)
            {
                bool[] bits = UnpackBits(packed, sampleCount);
                float[] s = new float[sampleCount];
                float step = InitStep, rec = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    rec += bits[i] ? step : -step;
                    s[i] = rec;
                    if (i > 0 && bits[i] == bits[i - 1]) step *= StepUp;
                    else step *= StepDown;
                    if (step < 0.001f) step = 0.001f;
                    if (step > 1.0f) step = 1.0f;
                }
                return s;
            }
        }

        public class NonlinearQuantizationCompressor : IAudioCompressor
        {
            private int bits;

            public NonlinearQuantizationCompressor(int bits) => this.bits = bits;

            public CompressedAudio Compress(float[] samples, int sampleRate, Action<int, long> reportProgress, CancellationToken cancel)
            {
                float maxVal = samples.Max(Math.Abs);
                if (maxVal < 1e-6f) maxVal = 1e-6f;
                int levels = 1 << bits;
                int total = samples.Length;
                int[] quant = new int[total];
                const float mu = 255f;
                int reportStep = Math.Max(1, total / 100);
                for (int i = 0; i < total; i++)
                {
                    if (cancel.IsCancellationRequested) throw new OperationCanceledException();
                    float x = samples[i] / maxVal;
                    float y = Math.Sign(x) * (float)(Math.Log(1 + mu * Math.Abs(x)) / Math.Log(1 + mu));
                    int idx = (int)Math.Round((y + 1) / 2 * (levels - 1));
                    if (idx < 0) idx = 0;
                    if (idx >= levels) idx = levels - 1;
                    quant[i] = idx;
                    if (i % reportStep == 0 || i == total - 1)
                        reportProgress(i == total - 1 ? 100 : (i * 100) / total, (i + 1) * bits / 8);
                }
                return new CompressedAudio
                {
                    Algorithm = "Nonlinear Quantization",
                    OriginalSampleRate = sampleRate,
                    ChannelData = new List<byte[]> { PackValues(quant, bits) },
                    MaxVal = maxVal,
                    QuantBits = bits,
                    SampleCount = total
                };
            }

            public static float[] Decode(byte[] packed, int sampleRate, float maxVal, int bits, int sampleCount)
            {
                if (maxVal < 1e-6f) maxVal = 1e-6f;
                int levels = 1 << bits;
                int[] vals = UnpackValues(packed, bits, sampleCount);
                float[] s = new float[sampleCount];
                const float mu = 255f;
                for (int i = 0; i < sampleCount; i++)
                {
                    float y = ((float)vals[i] / (levels - 1)) * 2 - 1;
                    float x = Math.Sign(y) * (float)((Math.Pow(1 + mu, Math.Abs(y)) - 1) / mu);
                    s[i] = x * maxVal;
                }
                return s;
            }
        }

        public class PredictiveDifferentialCodingCompressor : IAudioCompressor
        {
            private int bits;

            public PredictiveDifferentialCodingCompressor(int bits) => this.bits = bits;

            public CompressedAudio Compress(float[] samples, int sampleRate, Action<int, long> reportProgress, CancellationToken cancel)
            {
                float maxVal = samples.Max(Math.Abs);
                if (maxVal < 1e-6f) maxVal = 1e-6f;
                int levels = 1 << bits;
                int total = samples.Length;
                int[] quant = new int[total];
                float prev1 = 0, prev2 = 0, coeff1 = 0.7f, coeff2 = 0.3f;
                int reportStep = Math.Max(1, total / 100);
                for (int i = 0; i < total; i++)
                {
                    if (cancel.IsCancellationRequested) throw new OperationCanceledException();
                    float pred = coeff1 * prev1 + coeff2 * prev2;
                    float error = samples[i] - pred;
                    float step = 2 * maxVal / levels;
                    int q = (int)Math.Round((error + maxVal) / step);
                    if (q < 0) q = 0;
                    if (q >= levels) q = levels - 1;
                    quant[i] = q;
                    float recon = pred + (q * step - maxVal);
                    prev2 = prev1;
                    prev1 = recon;
                    if (i % reportStep == 0 || i == total - 1)
                        reportProgress(i == total - 1 ? 100 : (i * 100) / total, (i + 1) * bits / 8);
                }
                return new CompressedAudio
                {
                    Algorithm = "Predictive Differential Coding",
                    OriginalSampleRate = sampleRate,
                    ChannelData = new List<byte[]> { PackValues(quant, bits) },
                    MaxVal = maxVal,
                    QuantBits = bits,
                    SampleCount = total
                };
            }

            public static float[] Decode(byte[] packed, int sampleRate, float maxVal, int bits, int sampleCount)
            {
                if (maxVal < 1e-6f) maxVal = 1e-6f;
                int levels = 1 << bits;
                float step = 2 * maxVal / levels;
                int[] quant = UnpackValues(packed, bits, sampleCount);
                float[] s = new float[sampleCount];
                float prev1 = 0, prev2 = 0, coeff1 = 0.7f, coeff2 = 0.3f;
                for (int i = 0; i < sampleCount; i++)
                {
                    float pred = coeff1 * prev1 + coeff2 * prev2;
                    float rec = pred + (quant[i] * step - maxVal);
                    s[i] = rec;
                    prev2 = prev1;
                    prev1 = rec;
                }
                return s;
            }
        }
    }
}