using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace SysMasterUltra.Forms
{
    public partial class MainForm : Form
    {
        // Таймеры с уникальными именами
        private System.Threading.Timer lab7ThreadTimer;
        private System.Windows.Forms.Timer monitorUITimer;
        private ManualResetEventSlim lab7PauseEvent = new ManualResetEventSlim(true);
        private int lab7Counter = 0;
        private CancellationTokenSource lab7Cts;

        // Лаб 8: Хук клавиатуры
        private IntPtr keyboardHookId = IntPtr.Zero;
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc keyboardProc;
        private ListBox keyLogListBox;
        private bool blockEscKey = true;
        private int keyPressCount = 0;
        private Label keyCountLabel;

        // Лаб 9: Мониторинг
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;
        private Chart perfChart;
        private NumericUpDown numHistoryPoints;

        // Хранилище для виджетов
        private Panel cpuWidget;
        private Panel ramWidget;
        private Panel procWidget;
        private Panel threadWidget;

        // ДЗ 5: Рефлексия
        private Assembly loadedAssembly;
        private Type selectedType;

        // UI элементы
        private TabControl mainTabs;
        private StatusStrip statusBar;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel cpuStatusLabel;
        private ToolStripStatusLabel ramStatusLabel;

        public MainForm()
        {
            // Сначала установите размер формы
            this.Size = new Size(1200, 800);
            this.MinimumSize = new Size(800, 600);

            InitializeCustomComponents();
            InitializeAllModules();
        }

        private void InitializeCustomComponents()
        {
            // Основные настройки
            this.Text = "🏆 SYS MASTER ULTRA - Системный мониторинг и управление";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(20, 20, 30);
            this.ForeColor = Color.White;
            this.KeyPreview = true;
            this.MinimumSize = new Size(800, 600);

            // Инициализация компонентов
            InitializeTabs();
            InitializeStatusBar();
            InitializeTopPanel();
            InitializeHotkeys();
        }

        private void InitializeTabs()
        {
            mainTabs = new TabControl();
            mainTabs.Dock = DockStyle.Fill;
            mainTabs.SizeMode = TabSizeMode.Fixed;
            mainTabs.ItemSize = new Size(100, 30);
            mainTabs.Padding = new Point(10, 3);

            // Все вкладки
            mainTabs.TabPages.Add(CreateProcessesTab());      // Лаб 2, 7
            mainTabs.TabPages.Add(CreateKeyboardTab());       // Лаб 8
            mainTabs.TabPages.Add(CreateMonitorTab());        // Лаб 9
            mainTabs.TabPages.Add(CreateThreadsTab());        // Лаб 6
            mainTabs.TabPages.Add(CreateReflectionTab());     // ДЗ 5
            mainTabs.TabPages.Add(CreateNetworkTab());        // Доп
            mainTabs.TabPages.Add(CreateSecurityTab());       // Доп
            mainTabs.TabPages.Add(CreateUtilitiesTab());      // Доп

            this.Controls.Add(mainTabs);
        }

        private void InitializeStatusBar()
        {
            statusBar = new StatusStrip();
            statusBar.BackColor = Color.FromArgb(40, 40, 50);
            statusBar.ForeColor = Color.White;
            statusBar.Size = new Size(1200, 24);

            statusLabel = new ToolStripStatusLabel("Готов");
            cpuStatusLabel = new ToolStripStatusLabel("CPU: 0%");
            ramStatusLabel = new ToolStripStatusLabel("RAM: 0%");
            var timeLabel = new ToolStripStatusLabel();

            statusBar.Items.AddRange(new ToolStripItem[] {
                statusLabel,
                new ToolStripSeparator(),
                cpuStatusLabel,
                new ToolStripSeparator(),
                ramStatusLabel,
                new ToolStripSeparator(),
                timeLabel
            });

            statusBar.Dock = DockStyle.Bottom;
            this.Controls.Add(statusBar);

            // Обновление времени
            System.Windows.Forms.Timer timeTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            timeTimer.Tick += (s, e) =>
            {
                timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            timeTimer.Start();
        }

        private void InitializeTopPanel()
        {
            Panel topPanel = new Panel();
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 50;
            topPanel.BackColor = Color.FromArgb(30, 30, 45);

            Label title = new Label();
            title.Text = "🏆 SYS MASTER ULTRA - Мониторинг системы и процессов";
            title.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            title.ForeColor = Color.Cyan;
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleCenter;

            topPanel.Controls.Add(title);
            this.Controls.Add(topPanel);
        }

        private void InitializeHotkeys()
        {
            this.KeyDown += (s, e) =>
            {
                if (e.Control && e.Shift && e.KeyCode == Keys.P)
                    mainTabs.SelectedIndex = 0;
                if (e.Control && e.Shift && e.KeyCode == Keys.K)
                    mainTabs.SelectedIndex = 1;
                if (e.Control && e.Shift && e.KeyCode == Keys.M)
                    mainTabs.SelectedIndex = 2;
                if (e.KeyCode == Keys.F12)
                    TakeScreenshot();
                if (e.Control && e.KeyCode == Keys.Q)
                    this.Close();
            };
        }

        // ============ ВКЛАДКА ПРОЦЕССОВ ============
        private TabPage CreateProcessesTab()
        {
            TabPage tab = new TabPage("🖥️ Процессы");
            tab.BackColor = Color.FromArgb(25, 25, 35);
            tab.Size = new Size(1190, 750);

            // Таблица процессов
            DataGridView dgv = new DataGridView();
            dgv.Dock = DockStyle.Fill;
            dgv.BackgroundColor = Color.FromArgb(30, 30, 40);
            dgv.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 55);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.RowHeadersVisible = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.MinimumSize = new Size(100, 100);

            dgv.Columns.Add("Name", "Имя процесса");
            dgv.Columns.Add("PID", "PID");
            dgv.Columns.Add("Memory", "Память (MB)");
            dgv.Columns.Add("Threads", "Потоки");
            dgv.Columns.Add("Priority", "Приоритет");

            // Стиль для отображения текста
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            dgv.EnableHeadersVisualStyles = false;

            // Панель управления
            Panel controlPanel = new Panel();
            controlPanel.Dock = DockStyle.Top;
            controlPanel.Height = 50;
            controlPanel.BackColor = Color.FromArgb(40, 40, 50);

            Button btnRefresh = CreateStyledButton("🔄 Обновить", Color.SteelBlue);
            Button btnKill = CreateStyledButton("⏹️ Завершить", Color.Red);
            Button btnStart = CreateStyledButton("🚀 Запустить", Color.Green);
            TextBox txtProcess = new TextBox
            {
                Text = "notepad.exe",
                Width = 150,
                Height = 25,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            ComboBox cmbPriority = new ComboBox
            {
                Items = { "Normal", "High", "Idle", "BelowNormal", "AboveNormal", "RealTime" },
                Width = 120,
                Height = 25,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                DropDownStyle = ComboBoxStyle.DropDownList,
                SelectedIndex = 0
            };

            Label lblProcess = new Label { Text = "Имя процесса:", ForeColor = Color.White, Font = new Font("Segoe UI", 9), Width = 100 };
            Label lblPriority = new Label { Text = "Приоритет:", ForeColor = Color.White, Font = new Font("Segoe UI", 9), Width = 80 };

            btnRefresh.Click += (s, e) => RefreshProcesses(dgv);
            btnKill.Click += (s, e) => KillSelectedProcess(dgv);
            btnStart.Click += (s, e) => StartProcess(txtProcess.Text, cmbPriority.Text);

            FlowLayoutPanel flowPanel = new FlowLayoutPanel();
            flowPanel.Dock = DockStyle.Fill;
            flowPanel.FlowDirection = FlowDirection.LeftToRight;
            flowPanel.Padding = new Padding(10);
            flowPanel.Controls.AddRange(new Control[] {
                btnRefresh, btnKill, lblProcess, txtProcess, lblPriority, cmbPriority, btnStart
            });

            controlPanel.Controls.Add(flowPanel);

            // Лаб 7: Таймер и синхронизация
            GroupBox lab7Group = new GroupBox();
            lab7Group.Text = "Таймеры и синхронизация (Лаб 7)";
            lab7Group.ForeColor = Color.Cyan;
            lab7Group.Dock = DockStyle.Bottom;
            lab7Group.Height = 120;
            lab7Group.BackColor = Color.FromArgb(35, 35, 45);
            lab7Group.Font = new Font("Segoe UI", 9);

            Label lblCounter = new Label
            {
                Text = "Счётчик: 0",
                Font = new Font("Arial", 14, FontStyle.Bold),
                ForeColor = Color.Lime,
                Dock = DockStyle.Left,
                Width = 150,
                Height = 50,
                TextAlign = ContentAlignment.MiddleCenter
            };

            Button btnStartCounter = CreateStyledButton("🚀 Старт", Color.SteelBlue);
            Button btnPause = CreateStyledButton("⏸️ Пауза", Color.Orange);
            Button btnResume = CreateStyledButton("▶️ Продолжить", Color.Green);
            Button btnStop = CreateStyledButton("⏹️ Стоп", Color.Red);

            btnStartCounter.Click += (s, e) => StartLab7Counter(lblCounter);
            btnPause.Click += (s, e) => {
                lab7PauseEvent.Reset();
                UpdateStatus("Счётчик на паузе");
                btnPause.Enabled = false;
                btnResume.Enabled = true;
            };
            btnResume.Click += (s, e) => {
                lab7PauseEvent.Set();
                UpdateStatus("Счётчик продолжен");
                btnPause.Enabled = true;
                btnResume.Enabled = false;
            };
            btnStop.Click += (s, e) => {
                lab7Cts?.Cancel();
                lab7Counter = 0;
                lblCounter.Text = "Счётчик: 0";
                UpdateStatus("Счётчик остановлен");
                btnPause.Enabled = true;
                btnResume.Enabled = false;
            };

            FlowLayoutPanel lab7Panel = new FlowLayoutPanel();
            lab7Panel.Dock = DockStyle.Fill;
            lab7Panel.FlowDirection = FlowDirection.LeftToRight;
            lab7Panel.Padding = new Padding(20);
            lab7Panel.Controls.AddRange(new Control[] { lblCounter, btnStartCounter, btnPause, btnResume, btnStop });
            lab7Group.Controls.Add(lab7Panel);

            tab.Controls.AddRange(new Control[] { dgv, controlPanel, lab7Group });

            // Автоматическое обновление процессов при открытии вкладки
            tab.Enter += (s, e) => RefreshProcesses(dgv);

            return tab;
        }

        // ============ ВКЛАДКА КЛАВИАТУРЫ (Лаб 8) - ПОЛЕЗНЫЙ ФУНКЦИОНАЛ ============
        private TabPage CreateKeyboardTab()
        {
            TabPage tab = new TabPage("⌨️ Клавиатура");
            tab.BackColor = Color.FromArgb(25, 25, 35);

            GroupBox infoGroup = new GroupBox();
            infoGroup.Text = "Информация о модуле";
            infoGroup.ForeColor = Color.Yellow;
            infoGroup.Dock = DockStyle.Top;
            infoGroup.Height = 100;
            infoGroup.BackColor = Color.FromArgb(35, 35, 45);
            infoGroup.Font = new Font("Segoe UI", 9);
            infoGroup.Padding = new Padding(10);

            Label infoLabel = new Label();
            infoLabel.Text = "📝 Этот модуль перехватывает нажатия клавиш в реальном времени.\n" +
                           "Используется для:\n" +
                           "• Отладки приложений\n" +
                           "• Создания макросов\n" +
                           "• Блокировки клавиш (например ESC)\n" +
                           "• Анализа активности пользователя";
            infoLabel.ForeColor = Color.White;
            infoLabel.Font = new Font("Segoe UI", 9);
            infoLabel.Dock = DockStyle.Fill;
            infoLabel.TextAlign = ContentAlignment.MiddleLeft;

            infoGroup.Controls.Add(infoLabel);

            keyLogListBox = new ListBox();
            keyLogListBox.Dock = DockStyle.Fill;
            keyLogListBox.BackColor = Color.FromArgb(20, 20, 30);
            keyLogListBox.ForeColor = Color.Lime;
            keyLogListBox.Font = new Font("Consolas", 10);
            keyLogListBox.Height = 550;

            Panel controlPanel = new Panel();
            controlPanel.Dock = DockStyle.Top;
            controlPanel.Height = 50;
            controlPanel.BackColor = Color.FromArgb(40, 40, 50);

            Button btnStartHook = CreateStyledButton("🎯 Включить хук", Color.Green);
            Button btnStopHook = CreateStyledButton("⏹️ Выключить", Color.Red);
            Button btnClearLog = CreateStyledButton("🗑️ Очистить", Color.Orange);
            CheckBox chkBlockEsc = new CheckBox
            {
                Text = " Блокировать ESC",
                ForeColor = Color.White,
                Checked = true,
                Font = new Font("Segoe UI", 9),
                Height = 25,
                Width = 150
            };
            keyCountLabel = new Label
            {
                Text = "Нажатий: 0",
                ForeColor = Color.Cyan,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                Width = 120,
                Height = 25
            };

            btnStartHook.Click += (s, e) => StartKeyboardHook();
            btnStopHook.Click += (s, e) => StopKeyboardHook();
            btnClearLog.Click += (s, e) => {
                keyLogListBox.Items.Clear();
                keyPressCount = 0;
                keyCountLabel.Text = "Нажатий: 0";
                UpdateStatus("Лог очищен");
            };
            chkBlockEsc.CheckedChanged += (s, e) => {
                blockEscKey = chkBlockEsc.Checked;
                UpdateStatus(blockEscKey ? "Клавиша ESC заблокирована" : "Клавиша ESC разблокирована");
            };

            FlowLayoutPanel flowPanel = new FlowLayoutPanel();
            flowPanel.Dock = DockStyle.Fill;
            flowPanel.FlowDirection = FlowDirection.LeftToRight;
            flowPanel.Padding = new Padding(10);
            flowPanel.Controls.AddRange(new Control[] {
                btnStartHook, btnStopHook, btnClearLog, chkBlockEsc, keyCountLabel
            });

            controlPanel.Controls.Add(flowPanel);
            tab.Controls.AddRange(new Control[] { keyLogListBox, controlPanel, infoGroup });

            return tab;
        }

        // ============ ВКЛАДКА МОНИТОРИНГА (Лаб 9) - ИСПРАВЛЕНА ============
        private TabPage CreateMonitorTab()
        {
            TabPage tab = new TabPage("📊 Мониторинг");
            tab.BackColor = Color.FromArgb(25, 25, 35);
            tab.Size = new Size(1190, 750);

            // Виджеты вверху
            TableLayoutPanel widgetPanel = new TableLayoutPanel();
            widgetPanel.Dock = DockStyle.Top;
            widgetPanel.Height = 150;
            widgetPanel.ColumnCount = 4;
            widgetPanel.RowCount = 1;
            widgetPanel.Width = 1180;
            widgetPanel.Padding = new Padding(5);

            // Создаем виджеты и сохраняем ссылки
            cpuWidget = CreateMonitorWidget("ЦП (CPU)", "0%", Color.Red, "Процессорная нагрузка");
            ramWidget = CreateMonitorWidget("Память (RAM)", "0%", Color.Blue, "Использование оперативной памяти");
            procWidget = CreateMonitorWidget("Процессы", Process.GetProcesses().Length.ToString(), Color.Green, "Активные процессы");
            threadWidget = CreateMonitorWidget("Потоки", Process.GetProcesses().Sum(p => p.Threads.Count).ToString(), Color.Purple, "Всего потоков в системе");

            widgetPanel.Controls.Add(cpuWidget, 0, 0);
            widgetPanel.Controls.Add(ramWidget, 1, 0);
            widgetPanel.Controls.Add(procWidget, 2, 0);
            widgetPanel.Controls.Add(threadWidget, 3, 0);

            // График
            perfChart = new Chart();
            perfChart.Dock = DockStyle.Fill;
            perfChart.BackColor = Color.FromArgb(30, 30, 40);
            perfChart.Size = new Size(1180, 550);

            ChartArea chartArea = new ChartArea("Main");
            chartArea.BackColor = Color.FromArgb(30, 30, 40);
            chartArea.AxisX.LabelStyle.ForeColor = Color.White;
            chartArea.AxisY.LabelStyle.ForeColor = Color.White;
            chartArea.AxisX.Title = "Время (секунды)";
            chartArea.AxisY.Title = "Использование (%)";
            chartArea.AxisY.Maximum = 100;
            chartArea.AxisY.Minimum = 0;
            perfChart.ChartAreas.Add(chartArea);

            Series cpuSeries = new Series("CPU");
            cpuSeries.ChartType = SeriesChartType.Spline;
            cpuSeries.Color = Color.Red;
            cpuSeries.BorderWidth = 3;
            cpuSeries.LegendText = "Процессор";

            Series ramSeries = new Series("RAM");
            ramSeries.ChartType = SeriesChartType.Spline;
            ramSeries.Color = Color.Blue;
            ramSeries.BorderWidth = 3;
            ramSeries.LegendText = "Память";

            perfChart.Series.Add(cpuSeries);
            perfChart.Series.Add(ramSeries);

            // Легенда
            Legend legend = new Legend();
            legend.BackColor = Color.FromArgb(40, 40, 50);
            legend.ForeColor = Color.White;
            legend.Font = new Font("Segoe UI", 9);
            perfChart.Legends.Add(legend);

            // Панель управления графиком
            Panel chartControlPanel = new Panel();
            chartControlPanel.Dock = DockStyle.Top;
            chartControlPanel.Height = 40;
            chartControlPanel.BackColor = Color.FromArgb(40, 40, 50);

            Button btnClearChart = CreateStyledButton("🗑️ Очистить график", Color.Orange);
            CheckBox chkShowCPU = new CheckBox { Text = " CPU", Checked = true, ForeColor = Color.White, Font = new Font("Segoe UI", 9) };
            CheckBox chkShowRAM = new CheckBox { Text = " RAM", Checked = true, ForeColor = Color.White, Font = new Font("Segoe UI", 9) };
            Label lblHistory = new Label { Text = "История (точек):", ForeColor = Color.White, Font = new Font("Segoe UI", 9) };
            numHistoryPoints = new NumericUpDown { Value = 50, Minimum = 10, Maximum = 200, Width = 60, Font = new Font("Segoe UI", 9) };

            btnClearChart.Click += (s, e) => {
                perfChart.Series["CPU"].Points.Clear();
                perfChart.Series["RAM"].Points.Clear();
                UpdateStatus("График очищен");
            };

            chkShowCPU.CheckedChanged += (s, e) => perfChart.Series["CPU"].Enabled = chkShowCPU.Checked;
            chkShowRAM.CheckedChanged += (s, e) => perfChart.Series["RAM"].Enabled = chkShowRAM.Checked;

            FlowLayoutPanel chartFlow = new FlowLayoutPanel();
            chartFlow.Dock = DockStyle.Fill;
            chartFlow.FlowDirection = FlowDirection.LeftToRight;
            chartFlow.Padding = new Padding(10);
            chartFlow.Controls.AddRange(new Control[] {
                btnClearChart, chkShowCPU, chkShowRAM, lblHistory, numHistoryPoints
            });

            chartControlPanel.Controls.Add(chartFlow);

            tab.Controls.AddRange(new Control[] { perfChart, chartControlPanel, widgetPanel });
            return tab;
        }

        // ============ ВКЛАДКА ПОТОКОВ (Лаб 6) ============
        private TabPage CreateThreadsTab()
        {
            TabPage tab = new TabPage("⚡ Потоки");
            tab.BackColor = Color.FromArgb(25, 25, 35);

            GroupBox infoGroup = new GroupBox();
            infoGroup.Text = "Демонстрация работы с потоками";
            infoGroup.ForeColor = Color.Yellow;
            infoGroup.Dock = DockStyle.Top;
            infoGroup.Height = 120;
            infoGroup.BackColor = Color.FromArgb(35, 35, 45);
            infoGroup.Font = new Font("Segoe UI", 9);
            infoGroup.Padding = new Padding(10);

            Label infoLabel = new Label();
            infoLabel.Text = "Этот модуль демонстрирует различные способы работы с потоками:\n" +
                           "1. ThreadPool - использование пула потоков для фоновых задач\n" +
                           "2. Async/Await - современный асинхронный подход\n" +
                           "3. Manual Thread - прямое создание и управление потоками";
            infoLabel.ForeColor = Color.White;
            infoLabel.Font = new Font("Segoe UI", 9);
            infoLabel.Dock = DockStyle.Fill;
            infoLabel.TextAlign = ContentAlignment.MiddleLeft;

            infoGroup.Controls.Add(infoLabel);

            ListBox logBox = new ListBox();
            logBox.Dock = DockStyle.Fill;
            logBox.BackColor = Color.FromArgb(20, 20, 30);
            logBox.ForeColor = Color.Cyan;
            logBox.Font = new Font("Consolas", 10);
            logBox.Height = 550;

            Panel controlPanel = new Panel();
            controlPanel.Dock = DockStyle.Top;
            controlPanel.Height = 50;
            controlPanel.BackColor = Color.FromArgb(40, 40, 50);

            Button btnThreadPool = CreateStyledButton("ThreadPool", Color.Teal);
            Button btnAsync = CreateStyledButton("Async/Await", Color.Orange);
            Button btnManual = CreateStyledButton("Ручной поток", Color.Purple);
            Button btnClearLog = CreateStyledButton("🗑️ Очистить", Color.Gray);

            btnThreadPool.Click += (s, e) => ThreadPoolDemo(logBox);
            btnAsync.Click += async (s, e) => await AsyncDemo(logBox);
            btnManual.Click += (s, e) => ManualThreadDemo(logBox);
            btnClearLog.Click += (s, e) => logBox.Items.Clear();

            FlowLayoutPanel flowPanel = new FlowLayoutPanel();
            flowPanel.Dock = DockStyle.Fill;
            flowPanel.FlowDirection = FlowDirection.LeftToRight;
            flowPanel.Padding = new Padding(10);
            flowPanel.Controls.AddRange(new Control[] {
                btnThreadPool, btnAsync, btnManual, btnClearLog
            });

            controlPanel.Controls.Add(flowPanel);
            tab.Controls.AddRange(new Control[] { logBox, controlPanel, infoGroup });

            return tab;
        }

        // ============ ВКЛАДКА РЕФЛЕКСИИ (ДЗ 5) ============
        private TabPage CreateReflectionTab()
        {
            TabPage tab = new TabPage("🔮 Рефлексия");
            tab.BackColor = Color.FromArgb(25, 25, 35);
            tab.Size = new Size(1190, 750);

            // Информационная панель
            GroupBox infoGroup = new GroupBox();
            infoGroup.Text = "📚 Что такое рефлексия и зачем она нужна?";
            infoGroup.ForeColor = Color.Yellow;
            infoGroup.Dock = DockStyle.Top;
            infoGroup.Height = 150;
            infoGroup.BackColor = Color.FromArgb(35, 35, 45);
            infoGroup.Font = new Font("Segoe UI", 9);
            infoGroup.Padding = new Padding(10);

            Label infoLabel = new Label();
            infoLabel.Text = "Рефлексия (Reflection) - это механизм, который позволяет:\n\n" +
                           "• Исследовать структуру сборок (.dll/.exe) во время выполнения\n" +
                           "• Получать информацию о классах, методах, свойствах\n" +
                           "• Создавать экземпляры классов динамически\n" +
                           "• Вызывать методы по имени\n\n" +
                           "ПРИМЕНЕНИЕ:\n" +
                           "1. Нажмите 'Загрузить DLL'\n" +
                           "2. Выберите любой .dll файл (например TestLibrary.dll)\n" +
                           "3. Выберите класс из списка\n" +
                           "4. Выберите метод и нажмите 'Выполнить'";
            infoLabel.ForeColor = Color.White;
            infoLabel.Font = new Font("Segoe UI", 9);
            infoLabel.Dock = DockStyle.Fill;
            infoLabel.TextAlign = ContentAlignment.MiddleLeft;

            infoGroup.Controls.Add(infoLabel);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SplitterDistance = 300;
            split.Size = new Size(1180, 500);
            split.Panel1MinSize = 200;
            split.Panel2MinSize = 200;

            // Левая панель - Список классов
            Panel leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Fill;
            leftPanel.BackColor = Color.FromArgb(30, 30, 40);

            Panel leftHeader = new Panel();
            leftHeader.Dock = DockStyle.Top;
            leftHeader.Height = 40;
            leftHeader.BackColor = Color.FromArgb(40, 40, 50);

            Label lblClasses = new Label();
            lblClasses.Text = "📂 Классы в сборке:";
            lblClasses.ForeColor = Color.Cyan;
            lblClasses.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            lblClasses.Dock = DockStyle.Fill;
            lblClasses.TextAlign = ContentAlignment.MiddleLeft;
            lblClasses.Padding = new Padding(10, 0, 0, 0);

            leftHeader.Controls.Add(lblClasses);

            Button btnLoadDll = CreateStyledButton("📂 Загрузить DLL", Color.Purple);
            btnLoadDll.Dock = DockStyle.Top;
            btnLoadDll.Height = 40;
            btnLoadDll.Font = new Font("Segoe UI", 9, FontStyle.Bold);

            ListBox dllList = new ListBox();
            dllList.Dock = DockStyle.Fill;
            dllList.BackColor = Color.FromArgb(20, 20, 30);
            dllList.ForeColor = Color.White;
            dllList.Font = new Font("Consolas", 9);
            dllList.Height = 400;

            btnLoadDll.Click += (s, e) => LoadAssembly(dllList);

            leftPanel.Controls.AddRange(new Control[] { dllList, leftHeader, btnLoadDll });

            // Правая панель - Методы класса
            Panel rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Fill;
            rightPanel.BackColor = Color.FromArgb(35, 35, 45);

            Panel rightHeader = new Panel();
            rightHeader.Dock = DockStyle.Top;
            rightHeader.Height = 40;
            rightHeader.BackColor = Color.FromArgb(40, 40, 50);

            Label lblMethods = new Label();
            lblMethods.Text = "⚡ Методы класса:";
            lblMethods.ForeColor = Color.Cyan;
            lblMethods.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            lblMethods.Dock = DockStyle.Fill;
            lblMethods.TextAlign = ContentAlignment.MiddleLeft;
            lblMethods.Padding = new Padding(10, 0, 0, 0);

            rightHeader.Controls.Add(lblMethods);

            ListBox methodsList = new ListBox();
            methodsList.Dock = DockStyle.Fill;
            methodsList.BackColor = Color.FromArgb(25, 25, 35);
            methodsList.ForeColor = Color.White;
            methodsList.Font = new Font("Consolas", 9);
            methodsList.Height = 400;

            Button btnInvoke = CreateStyledButton("⚡ Выполнить метод", Color.Green);
            btnInvoke.Dock = DockStyle.Bottom;
            btnInvoke.Height = 40;
            btnInvoke.Font = new Font("Segoe UI", 9, FontStyle.Bold);

            dllList.SelectedIndexChanged += (s, e) => LoadMethods(dllList, methodsList);
            btnInvoke.Click += (s, e) => InvokeMethod(methodsList);

            rightPanel.Controls.AddRange(new Control[] { methodsList, rightHeader, btnInvoke });

            split.Panel1.Controls.Add(leftPanel);
            split.Panel2.Controls.Add(rightPanel);

            tab.Controls.AddRange(new Control[] { split, infoGroup });

            return tab;
        }

        // ============ ВКЛАДКА СЕТИ ============
        private TabPage CreateNetworkTab()
        {
            TabPage tab = new TabPage("🌐 Сеть");
            tab.BackColor = Color.FromArgb(25, 25, 35);
            tab.Size = new Size(1190, 750);

            TabControl networkTabs = new TabControl();
            networkTabs.Dock = DockStyle.Fill;
            networkTabs.Size = new Size(1180, 700);
            networkTabs.Font = new Font("Segoe UI", 9);

            // Ping
            TabPage pingTab = new TabPage("Ping");
            pingTab.Size = new Size(1170, 650);
            pingTab.BackColor = Color.FromArgb(30, 30, 40);

            GroupBox pingGroup = new GroupBox();
            pingGroup.Text = "Проверка доступности хоста";
            pingGroup.ForeColor = Color.Green;
            pingGroup.Dock = DockStyle.Fill;
            pingGroup.BackColor = Color.FromArgb(35, 35, 45);
            pingGroup.Font = new Font("Segoe UI", 9);
            pingGroup.Padding = new Padding(10);

            Panel pingInputPanel = new Panel();
            pingInputPanel.Dock = DockStyle.Top;
            pingInputPanel.Height = 50;
            pingInputPanel.BackColor = Color.FromArgb(40, 40, 50);

            Label lblHost = new Label
            {
                Text = "Хост или IP:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Width = 80,
                TextAlign = ContentAlignment.MiddleRight
            };
            TextBox txtHost = new TextBox
            {
                Text = "google.com",
                Width = 200,
                Height = 25,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            Button btnPing = CreateStyledButton("▶️ Выполнить Ping", Color.Green);
            btnPing.Font = new Font("Segoe UI", 9);

            NumericUpDown numPingCount = new NumericUpDown
            {
                Value = 4,
                Minimum = 1,
                Maximum = 20,
                Width = 60,
                Font = new Font("Segoe UI", 9)
            };
            Label lblCount = new Label
            {
                Text = "кол-во:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Width = 50
            };

            RichTextBox pingResult = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 10)
            };

            Button btnClearPing = CreateStyledButton("🗑️ Очистить", Color.Orange);
            btnClearPing.Font = new Font("Segoe UI", 9);
            btnClearPing.Click += (s, e) => pingResult.Clear();

            btnPing.Click += async (s, e) => {
                pingResult.AppendText($"\n[{DateTime.Now:HH:mm:ss}] Ping {txtHost.Text}...\n");
                for (int i = 0; i < (int)numPingCount.Value; i++)
                {
                    await PingHost(txtHost.Text, pingResult);
                    await Task.Delay(1000);
                }
                pingResult.AppendText($"\n[{DateTime.Now:HH:mm:ss}] Завершено\n");
                pingResult.ScrollToCaret();
            };

            FlowLayoutPanel pingFlow = new FlowLayoutPanel();
            pingFlow.Dock = DockStyle.Fill;
            pingFlow.FlowDirection = FlowDirection.LeftToRight;
            pingFlow.Padding = new Padding(10);
            pingFlow.Controls.AddRange(new Control[] {
                lblHost, txtHost, lblCount, numPingCount, btnPing, btnClearPing
            });

            pingInputPanel.Controls.Add(pingFlow);
            pingGroup.Controls.AddRange(new Control[] { pingResult, pingInputPanel });
            pingTab.Controls.Add(pingGroup);

            // Сканер портов
            TabPage scanTab = new TabPage("Сканер портов");
            scanTab.Size = new Size(1170, 650);
            scanTab.BackColor = Color.FromArgb(30, 30, 40);

            GroupBox scanGroup = new GroupBox();
            scanGroup.Text = "Сканирование открытых портов";
            scanGroup.ForeColor = Color.Blue;
            scanGroup.Dock = DockStyle.Fill;
            scanGroup.BackColor = Color.FromArgb(35, 35, 45);
            scanGroup.Font = new Font("Segoe UI", 9);
            scanGroup.Padding = new Padding(10);

            Panel scanInputPanel = new Panel();
            scanInputPanel.Dock = DockStyle.Top;
            scanInputPanel.Height = 80;
            scanInputPanel.BackColor = Color.FromArgb(40, 40, 50);

            Label lblScanHost = new Label
            {
                Text = "Хост:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Width = 50
            };
            TextBox txtScanHost = new TextBox
            {
                Text = "localhost",
                Width = 150,
                Height = 25,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White
            };

            Label lblStartPort = new Label
            {
                Text = "Порт от:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Width = 60
            };
            NumericUpDown numStartPort = new NumericUpDown
            {
                Value = 1,
                Minimum = 1,
                Maximum = 65535,
                Width = 80,
                Height = 25
            };

            Label lblEndPort = new Label
            {
                Text = "до:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Width = 30
            };
            NumericUpDown numEndPort = new NumericUpDown
            {
                Value = 100,
                Minimum = 1,
                Maximum = 65535,
                Width = 80,
                Height = 25
            };

            Button btnScan = CreateStyledButton("🔍 Начать сканирование", Color.Blue);
            Button btnStopScan = CreateStyledButton("⏹️ Остановить", Color.Red);
            Button btnClearScan = CreateStyledButton("🗑️ Очистить", Color.Orange);

            ListBox portList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10)
            };

            CancellationTokenSource scanCts = new CancellationTokenSource();

            btnScan.Click += async (s, e) => {
                btnScan.Enabled = false;
                btnStopScan.Enabled = true;
                scanCts = new CancellationTokenSource();
                await Task.Run(() => ScanPorts(txtScanHost.Text,
                    (int)numStartPort.Value,
                    (int)numEndPort.Value,
                    portList,
                    scanCts.Token));
                btnScan.Enabled = true;
                btnStopScan.Enabled = false;
            };

            btnStopScan.Click += (s, e) => {
                scanCts.Cancel();
                btnScan.Enabled = true;
                btnStopScan.Enabled = false;
                portList.Items.Add("[Сканирование прервано пользователем]");
            };

            btnClearScan.Click += (s, e) => portList.Items.Clear();

            FlowLayoutPanel scanFlow1 = new FlowLayoutPanel();
            scanFlow1.Dock = DockStyle.Top;
            scanFlow1.Height = 35;
            scanFlow1.FlowDirection = FlowDirection.LeftToRight;
            scanFlow1.Padding = new Padding(10);
            scanFlow1.Controls.AddRange(new Control[] {
                lblScanHost, txtScanHost, lblStartPort, numStartPort, lblEndPort, numEndPort
            });

            FlowLayoutPanel scanFlow2 = new FlowLayoutPanel();
            scanFlow2.Dock = DockStyle.Top;
            scanFlow2.Height = 35;
            scanFlow2.FlowDirection = FlowDirection.LeftToRight;
            scanFlow2.Padding = new Padding(10);
            scanFlow2.Controls.AddRange(new Control[] {
                btnScan, btnStopScan, btnClearScan
            });

            scanInputPanel.Controls.AddRange(new Control[] { scanFlow2, scanFlow1 });
            scanGroup.Controls.AddRange(new Control[] { portList, scanInputPanel });
            scanTab.Controls.Add(scanGroup);

            networkTabs.TabPages.AddRange(new TabPage[] { pingTab, scanTab });
            tab.Controls.Add(networkTabs);

            return tab;
        }

        // ============ ВКЛАДКА БЕЗОПАСНОСТИ - ИСПРАВЛЕНА ============
        private TabPage CreateSecurityTab()
        {
            TabPage tab = new TabPage("🔐 Безопасность");
            tab.BackColor = Color.FromArgb(25, 25, 35);
            tab.Size = new Size(1190, 750);

            GroupBox infoGroup = new GroupBox();
            infoGroup.Text = "🔒 Шифрование и безопасность";
            infoGroup.ForeColor = Color.Yellow;
            infoGroup.Dock = DockStyle.Top;
            infoGroup.Height = 100;
            infoGroup.BackColor = Color.FromArgb(35, 35, 45);
            infoGroup.Font = new Font("Segoe UI", 9);
            infoGroup.Padding = new Padding(10);

            Label infoLabel = new Label();
            infoLabel.Text = "Инструменты для шифрования данных и работы с паролями.\n" +
                           "Используются стандартные криптографические алгоритмы .NET.";
            infoLabel.ForeColor = Color.White;
            infoLabel.Font = new Font("Segoe UI", 9);
            infoLabel.Dock = DockStyle.Fill;
            infoLabel.TextAlign = ContentAlignment.MiddleLeft;

            infoGroup.Controls.Add(infoLabel);

            SplitContainer mainSplit = new SplitContainer();
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.Orientation = Orientation.Vertical;
            mainSplit.SplitterDistance = 350;

            // Левая панель - Шифрование
            GroupBox encryptionGroup = new GroupBox();
            encryptionGroup.Text = "Шифрование текста (AES)";
            encryptionGroup.ForeColor = Color.Cyan;
            encryptionGroup.Dock = DockStyle.Fill;
            encryptionGroup.BackColor = Color.FromArgb(35, 35, 45);
            encryptionGroup.Font = new Font("Segoe UI", 9);
            encryptionGroup.Padding = new Padding(10);

            Panel encryptionPanel = new Panel();
            encryptionPanel.Dock = DockStyle.Fill;
            encryptionPanel.BackColor = Color.FromArgb(40, 40, 50);

            Label lblInput = new Label
            {
                Text = "Исходный текст:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, 20),
                Width = 120
            };

            TextBox txtInput = new TextBox
            {
                Location = new Point(150, 20),
                Width = 300,
                Height = 80,
                Multiline = true,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };

            Label lblKey = new Label
            {
                Text = "Ключ (минимум 8 символов):",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, 110),
                Width = 180
            };

            TextBox txtKey = new TextBox
            {
                Location = new Point(210, 110),
                Width = 200,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Text = "MySecretKey123"
            };

            Label lblOutput = new Label
            {
                Text = "Результат:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, 150),
                Width = 120
            };

            TextBox txtOutput = new TextBox
            {
                Location = new Point(150, 150),
                Width = 300,
                Height = 80,
                Multiline = true,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                ReadOnly = true
            };

            // ИСПРАВЛЕННЫЕ КНОПКИ
            Button btnEncrypt = CreateStyledButton("🔒 Зашифровать", Color.Green);
            btnEncrypt.Location = new Point(20, 240);
            btnEncrypt.Width = 130;
            btnEncrypt.Click += (s, e) => EncryptText(txtInput.Text, txtKey.Text, txtOutput);

            Button btnDecrypt = CreateStyledButton("🔓 Расшифровать", Color.Blue);
            btnDecrypt.Location = new Point(160, 240);
            btnDecrypt.Width = 130;
            btnDecrypt.Click += (s, e) => DecryptText(txtOutput.Text, txtKey.Text, txtInput);

            Button btnCopyResult = CreateStyledButton("📋 Копировать", Color.Purple);
            btnCopyResult.Location = new Point(300, 240);
            btnCopyResult.Width = 130;
            btnCopyResult.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtOutput.Text))
                {
                    Clipboard.SetText(txtOutput.Text);
                    UpdateStatus("Результат скопирован в буфер обмена");
                }
            };

            Button btnClearAll = CreateStyledButton("🗑️ Очистить", Color.Orange);
            btnClearAll.Location = new Point(20, 280);
            btnClearAll.Width = 130;
            btnClearAll.Click += (s, e) =>
            {
                txtInput.Clear();
                txtOutput.Clear();
                UpdateStatus("Поля очищены");
            };

            Button btnSwap = CreateStyledButton("🔄 Обменять", Color.Teal);
            btnSwap.Location = new Point(160, 280);
            btnSwap.Width = 130;
            btnSwap.Click += (s, e) =>
            {
                string temp = txtInput.Text;
                txtInput.Text = txtOutput.Text;
                txtOutput.Text = temp;
                UpdateStatus("Тексты обменяны местами");
            };

            encryptionPanel.Controls.AddRange(new Control[] {
                lblInput, txtInput, lblKey, txtKey, lblOutput, txtOutput,
                btnEncrypt, btnDecrypt, btnCopyResult, btnClearAll, btnSwap
            });

            encryptionGroup.Controls.Add(encryptionPanel);

            // Правая панель - Генератор паролей
            GroupBox passwordGroup = new GroupBox();
            passwordGroup.Text = "Генератор паролей";
            passwordGroup.ForeColor = Color.Lime;
            passwordGroup.Dock = DockStyle.Fill;
            passwordGroup.BackColor = Color.FromArgb(35, 35, 45);
            passwordGroup.Font = new Font("Segoe UI", 9);
            passwordGroup.Padding = new Padding(10);

            Panel passwordPanel = new Panel();
            passwordPanel.Dock = DockStyle.Fill;
            passwordPanel.BackColor = Color.FromArgb(40, 40, 50);

            Label lblLength = new Label
            {
                Text = "Длина пароля:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, 20),
                Width = 100
            };

            NumericUpDown numLength = new NumericUpDown
            {
                Location = new Point(130, 20),
                Width = 60,
                Value = 12,
                Minimum = 6,
                Maximum = 32
            };

            CheckBox chkUpper = new CheckBox
            {
                Text = "Заглавные буквы (A-Z)",
                Checked = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, 50),
                Width = 180
            };

            CheckBox chkLower = new CheckBox
            {
                Text = "Строчные буквы (a-z)",
                Checked = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, 80),
                Width = 180
            };

            CheckBox chkDigits = new CheckBox
            {
                Text = "Цифры (0-9)",
                Checked = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, 110),
                Width = 180
            };

            CheckBox chkSpecial = new CheckBox
            {
                Text = "Спецсимволы (!@# и т.д.)",
                Checked = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, 140),
                Width = 180
            };

            Button btnGenerate = CreateStyledButton("🎲 Сгенерировать", Color.Purple);
            btnGenerate.Location = new Point(220, 20);
            btnGenerate.Width = 150;

            TextBox txtPassword = new TextBox
            {
                Location = new Point(220, 60),
                Width = 250,
                Height = 100,
                Multiline = true,
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10),
                ReadOnly = true
            };

            Button btnCopy = CreateStyledButton("📋 Копировать", Color.Teal);
            btnCopy.Location = new Point(220, 170);
            btnCopy.Width = 120;
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtPassword.Text))
                {
                    Clipboard.SetText(txtPassword.Text);
                    UpdateStatus("Пароль скопирован в буфер обмена");
                }
            };

            Button btnGenerateMultiple = CreateStyledButton("🎲 5 паролей", Color.Orange);
            btnGenerateMultiple.Location = new Point(350, 170);
            btnGenerateMultiple.Width = 120;
            btnGenerateMultiple.Click += (s, e) =>
            {
                StringBuilder passwords = new StringBuilder();
                for (int i = 1; i <= 5; i++)
                {
                    string password = GeneratePassword(
                        (int)numLength.Value,
                        chkUpper.Checked,
                        chkLower.Checked,
                        chkDigits.Checked,
                        chkSpecial.Checked);
                    passwords.AppendLine($"{i}. {password}");
                }
                txtPassword.Text = passwords.ToString();
            };

            btnGenerate.Click += (s, e) =>
            {
                string password = GeneratePassword(
                    (int)numLength.Value,
                    chkUpper.Checked,
                    chkLower.Checked,
                    chkDigits.Checked,
                    chkSpecial.Checked);

                txtPassword.Text = password;
            };

            passwordPanel.Controls.AddRange(new Control[] {
                lblLength, numLength, chkUpper, chkLower, chkDigits, chkSpecial,
                btnGenerate, txtPassword, btnCopy, btnGenerateMultiple
            });

            passwordGroup.Controls.Add(passwordPanel);

            mainSplit.Panel1.Controls.Add(encryptionGroup);
            mainSplit.Panel2.Controls.Add(passwordGroup);

            tab.Controls.AddRange(new Control[] { mainSplit, infoGroup });

            return tab;
        }

        // ============ ВКЛАДКА УТИЛИТ ============
        private TabPage CreateUtilitiesTab()
        {
            TabPage tab = new TabPage("🛠️ Утилиты");
            tab.BackColor = Color.FromArgb(25, 25, 35);
            tab.Size = new Size(1190, 750);

            GroupBox infoGroup = new GroupBox();
            infoGroup.Text = "📦 Системные утилиты";
            infoGroup.ForeColor = Color.Yellow;
            infoGroup.Dock = DockStyle.Top;
            infoGroup.Height = 100;
            infoGroup.BackColor = Color.FromArgb(35, 35, 45);
            infoGroup.Font = new Font("Segoe UI", 9);
            infoGroup.Padding = new Padding(10);

            Label infoLabel = new Label();
            infoLabel.Text = "Полезные инструменты для повседневной работы с системой.\n" +
                           "Все инструменты безопасны и выполняют стандартные операции.";
            infoLabel.ForeColor = Color.White;
            infoLabel.Font = new Font("Segoe UI", 9);
            infoLabel.Dock = DockStyle.Fill;
            infoLabel.TextAlign = ContentAlignment.MiddleLeft;

            infoGroup.Controls.Add(infoLabel);

            // Панель с утилитами
            FlowLayoutPanel utilitiesPanel = new FlowLayoutPanel();
            utilitiesPanel.Dock = DockStyle.Fill;
            utilitiesPanel.BackColor = Color.FromArgb(30, 30, 40);
            utilitiesPanel.Padding = new Padding(15);
            utilitiesPanel.AutoScroll = true;
            utilitiesPanel.FlowDirection = FlowDirection.LeftToRight;
            utilitiesPanel.WrapContents = true;

            // Добавляем утилиты
            utilitiesPanel.Controls.Add(CreateUtilityPanel(
                "🧹 Очистка временных файлов",
                "Удаляет временные файлы из папки TEMP для освобождения места",
                Color.Orange,
                CleanTempFilesWithProgress));

            utilitiesPanel.Controls.Add(CreateUtilityPanel(
                "📸 Скриншот экрана",
                "Создает снимок экрана и сохраняет на Рабочий стол",
                Color.Blue,
                TakeScreenshot));

            utilitiesPanel.Controls.Add(CreateUtilityPanel(
                "💻 Системная информация",
                "Показывает подробную информацию о системе и оборудовании",
                Color.Green,
                ShowDetailedSystemInfo));

            utilitiesPanel.Controls.Add(CreateUtilityPanel(
                "🚀 Менеджер автозагрузки",
                "Показывает программы, которые запускаются автоматически",
                Color.Purple,
                ShowStartupManagerWithList));

            utilitiesPanel.Controls.Add(CreateUtilityPanel(
                "⚙️ Диспетчер служб",
                "Информация о службах Windows и управление ими",
                Color.Red,
                ShowServicesManager));

            utilitiesPanel.Controls.Add(CreateUtilityPanel(
                "📝 Просмотр событий",
                "Доступ к журналам событий Windows",
                Color.Teal,
                ShowEventViewer));

            utilitiesPanel.Controls.Add(CreateUtilityPanel(
                "🔧 Редактор реестра",
                "Информация о реестре Windows и работа с ним",
                Color.Yellow,
                ShowRegistryViewer));

            utilitiesPanel.Controls.Add(CreateUtilityPanel(
                "🎮 Мини-игры",
                "Небольшие игры для отдыха и развлечения",
                Color.Magenta,
                ShowMiniGames));

            tab.Controls.AddRange(new Control[] { utilitiesPanel, infoGroup });

            return tab;
        }

        // ============ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ UI ============
        private Button CreateStyledButton(string text, Color color)
        {
            return new Button
            {
                Text = text,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Height = 35,
                Width = 140,
                Margin = new Padding(5)
            };
        }

        private Panel CreateMonitorWidget(string title, string value, Color color, string description)
        {
            Panel widget = new Panel();
            widget.BackColor = Color.FromArgb(40, 40, 50);
            widget.BorderStyle = BorderStyle.FixedSingle;
            widget.Padding = new Padding(10);
            widget.Margin = new Padding(5);
            widget.Size = new Size(280, 130);
            widget.Cursor = Cursors.Hand;

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.ForeColor = color;
            lblTitle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Height = 25;
            lblTitle.TextAlign = ContentAlignment.MiddleLeft;

            Label lblValue = new Label();
            lblValue.Text = value;
            lblValue.ForeColor = Color.White;
            lblValue.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            lblValue.Dock = DockStyle.Fill;
            lblValue.TextAlign = ContentAlignment.MiddleCenter;
            lblValue.Height = 60;
            lblValue.Tag = "valueLabel";

            Label lblDesc = new Label();
            lblDesc.Text = description;
            lblDesc.ForeColor = Color.LightGray;
            lblDesc.Font = new Font("Segoe UI", 8);
            lblDesc.Dock = DockStyle.Bottom;
            lblDesc.Height = 30;
            lblDesc.TextAlign = ContentAlignment.MiddleLeft;

            widget.Controls.AddRange(new Control[] { lblDesc, lblValue, lblTitle });
            return widget;
        }

        private Panel CreateUtilityPanel(string title, string description, Color color, Action action)
        {
            Panel panel = new Panel();
            panel.BackColor = Color.FromArgb(40, 40, 50);
            panel.BorderStyle = BorderStyle.FixedSingle;
            panel.Padding = new Padding(15);
            panel.Margin = new Padding(5);
            panel.Cursor = Cursors.Hand;
            panel.Height = 120;
            panel.Width = 250;

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.ForeColor = color;
            lblTitle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Height = 30;
            lblTitle.TextAlign = ContentAlignment.MiddleLeft;

            Label lblDesc = new Label();
            lblDesc.Text = description;
            lblDesc.ForeColor = Color.LightGray;
            lblDesc.Font = new Font("Segoe UI", 9);
            lblDesc.Dock = DockStyle.Fill;
            lblDesc.TextAlign = ContentAlignment.MiddleLeft;
            lblDesc.Padding = new Padding(0, 5, 0, 0);

            panel.MouseEnter += (s, e) => {
                panel.BackColor = Color.FromArgb(50, 50, 60);
                panel.BorderStyle = BorderStyle.Fixed3D;
            };

            panel.MouseLeave += (s, e) => {
                panel.BackColor = Color.FromArgb(40, 40, 50);
                panel.BorderStyle = BorderStyle.FixedSingle;
            };

            panel.Click += (s, e) => action();

            ToolTip toolTip = new ToolTip();
            toolTip.SetToolTip(panel, $"Нажмите для запуска: {description}");

            panel.Controls.AddRange(new Control[] { lblDesc, lblTitle });
            return panel;
        }

        // ============ МЕТОДЫ ПРОЦЕССОВ (Лаб 2, 7) ============
        private void RefreshProcesses(DataGridView dgv)
        {
            try
            {
                dgv.Rows.Clear();
                var processes = Process.GetProcesses()
                    .OrderBy(p => p.ProcessName)
                    .Take(50)
                    .ToList();

                foreach (var proc in processes)
                {
                    try
                    {
                        string memoryMB = (proc.WorkingSet64 / 1024 / 1024).ToString("N0") + " MB";
                        string priority = proc.PriorityClass.ToString();

                        dgv.Rows.Add(
                            proc.ProcessName,
                            proc.Id,
                            memoryMB,
                            proc.Threads.Count,
                            priority
                        );
                    }
                    catch { }
                }

                foreach (DataGridViewRow row in dgv.Rows)
                {
                    row.DefaultCellStyle.ForeColor = Color.White;
                    row.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 40);
                    row.DefaultCellStyle.SelectionForeColor = Color.Black;
                    row.DefaultCellStyle.SelectionBackColor = Color.Cyan;
                    row.DefaultCellStyle.Font = new Font("Segoe UI", 9);
                }

                UpdateStatus($"Загружено процессов: {dgv.RowCount}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void KillSelectedProcess(DataGridView dgv)
        {
            if (dgv.SelectedRows.Count > 0)
            {
                try
                {
                    int pid = Convert.ToInt32(dgv.SelectedRows[0].Cells[1].Value);
                    string processName = dgv.SelectedRows[0].Cells[0].Value.ToString();

                    DialogResult result = MessageBox.Show(
                        $"Вы уверены, что хотите завершить процесс?\n\nИмя: {processName}\nPID: {pid}",
                        "Подтверждение завершения",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Yes)
                    {
                        Process.GetProcessById(pid).Kill();
                        UpdateStatus($"Процесс '{processName}' (PID: {pid}) завершён");
                        RefreshProcesses(dgv);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Выберите процесс для завершения", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void StartProcess(string name, string priority)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Введите имя процесса для запуска", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var proc = Process.Start(name);
                if (proc != null)
                {
                    try
                    {
                        proc.PriorityClass = (ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass), priority);
                        UpdateStatus($"Запущен '{name}' с приоритетом {priority} (PID: {proc.Id})");
                    }
                    catch { }
                }
                else
                {
                    MessageBox.Show($"Не удалось запустить процесс: {name}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartLab7Counter(Label lbl)
        {
            lab7Cts = new CancellationTokenSource();

            Task.Run(() =>
            {
                while (!lab7Cts.Token.IsCancellationRequested)
                {
                    lab7PauseEvent.Wait(lab7Cts.Token);
                    Interlocked.Increment(ref lab7Counter);

                    this.Invoke(new Action(() =>
                    {
                        lbl.Text = $"Счётчик: {lab7Counter}";
                    }));

                    Thread.Sleep(100);
                }
            }, lab7Cts.Token);

            lab7ThreadTimer = new System.Threading.Timer(_ =>
            {
                this.Invoke(new Action(() =>
                {
                    UpdateStatus($"Таймер: счётчик = {lab7Counter}");
                }));
            }, null, 0, 1000);
        }

        // ============ МЕТОДЫ КЛАВИАТУРЫ (Лаб 8) ============
        private void StartKeyboardHook()
        {
            if (keyboardHookId == IntPtr.Zero)
            {
                keyboardProc = HookCallback;
                using (var curProcess = Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    keyboardHookId = SetWindowsHookEx(13, keyboardProc,
                        GetModuleHandle(curModule.ModuleName), 0);
                }
                UpdateStatus("Хук клавиатуры активирован - нажимайте клавиши");
            }
        }

        private void StopKeyboardHook()
        {
            if (keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHookId);
                keyboardHookId = IntPtr.Zero;
                UpdateStatus("Хук клавиатуры деактивирован");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)0x0100) // WM_KEYDOWN
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                this.Invoke(new Action(() =>
                {
                    keyPressCount++;
                    string keyName = key.ToString();
                    string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] Клавиша: {keyName} (код: {vkCode})";
                    keyLogListBox.Items.Add(logEntry);

                    if (keyLogListBox.Items.Count > 0)
                        keyLogListBox.TopIndex = keyLogListBox.Items.Count - 1;

                    keyCountLabel.Text = $"Нажатий: {keyPressCount}";

                    if (blockEscKey && key == Keys.Escape)
                    {
                        UpdateStatus("Клавиша ESC заблокирована (нажатие перехвачено)");
                    }
                }));

                if (blockEscKey && key == Keys.Escape)
                    return (IntPtr)1; // Блокируем ESC
            }

            return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
        }

        // ============ МЕТОДЫ ПОТОКОВ (Лаб 6) ============
        private void ThreadPoolDemo(ListBox logBox)
        {
            logBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] 🚀 Запуск ThreadPool демо (5 задач)");

            for (int i = 1; i <= 5; i++)
            {
                int taskNum = i;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    int delay = new Random().Next(1000, 3000);
                    Thread.Sleep(delay);

                    this.Invoke(new Action(() =>
                    {
                        logBox.Items.Add($"✅ Задача {taskNum} завершена (задержка: {delay}мс)");
                        if (logBox.Items.Count > 0)
                            logBox.TopIndex = logBox.Items.Count - 1;
                    }));
                });
            }
        }

        private async Task AsyncDemo(ListBox logBox)
        {
            logBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] ⚡ Запуск Async/Await демо");

            var tasks = Enumerable.Range(1, 5).Select(async i =>
            {
                int delay = new Random().Next(1000, 3000);
                await Task.Delay(delay);

                this.Invoke(new Action(() =>
                {
                    logBox.Items.Add($"✅ Async задача {i} выполнена (задержка: {delay}мс)");
                    if (logBox.Items.Count > 0)
                        logBox.TopIndex = logBox.Items.Count - 1;
                }));
            });

            await Task.WhenAll(tasks);
            logBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] 🎉 Все async задачи завершены!");
        }

        private void ManualThreadDemo(ListBox logBox)
        {
            logBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] 👨‍💻 Ручной поток запущен");

            Thread thread = new Thread(() =>
            {
                for (int i = 1; i <= 5; i++)
                {
                    Thread.Sleep(1000);
                    this.Invoke(new Action(() =>
                    {
                        logBox.Items.Add($"📊 Ручной поток: шаг {i}/5");
                        if (logBox.Items.Count > 0)
                            logBox.TopIndex = logBox.Items.Count - 1;
                    }));
                }

                this.Invoke(new Action(() =>
                {
                    logBox.Items.Add($"[{DateTime.Now:HH:mm:ss}] 🏁 Ручной поток завершён");
                }));
            });

            thread.IsBackground = true;
            thread.Start();
        }

        // ============ МЕТОДЫ РЕФЛЕКСИИ (ДЗ 5) ============
        private void LoadAssembly(ListBox dllList)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Сборки (*.dll;*.exe)|*.dll;*.exe|Все файлы (*.*)|*.*";
            dlg.Title = "Выберите сборку для анализа";
            dlg.Multiselect = false;

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    loadedAssembly = Assembly.LoadFrom(dlg.FileName);
                    dllList.Items.Clear();

                    var types = loadedAssembly.GetTypes()
                        .Where(t => t.IsPublic || t.IsNestedPublic)
                        .OrderBy(t => t.Name)
                        .ToList();

                    foreach (Type type in types)
                    {
                        string typeInfo = $"{type.Name}";
                        if (!string.IsNullOrEmpty(type.Namespace))
                            typeInfo += $" ({type.Namespace})";

                        dllList.Items.Add(typeInfo);
                    }

                    UpdateStatus($"Загружена сборка: {Path.GetFileName(dlg.FileName)} ({types.Count} классов)");
                }
                catch (ReflectionTypeLoadException ex)
                {
                    MessageBox.Show($"Ошибка загрузки типов: {ex.LoaderExceptions[0]?.Message}",
                        "Ошибка рефлексии", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки: {ex.Message}",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadMethods(ListBox dllList, ListBox methodsList)
        {
            if (dllList.SelectedItem != null && loadedAssembly != null)
            {
                try
                {
                    string selectedTypeName = dllList.SelectedItem.ToString().Split(' ')[0];
                    selectedType = loadedAssembly.GetTypes()
                        .FirstOrDefault(t => t.Name == selectedTypeName);

                    if (selectedType != null)
                    {
                        methodsList.Items.Clear();

                        var methods = selectedType.GetMethods(
                            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                            BindingFlags.DeclaredOnly)
                            .OrderBy(m => m.Name)
                            .ToList();

                        foreach (MethodInfo method in methods)
                        {
                            string returnType = method.ReturnType.Name;
                            string parameters = string.Join(", ",
                                method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));

                            string methodInfo = $"{returnType} {method.Name}({parameters})";
                            if (method.IsStatic) methodInfo = "[static] " + methodInfo;

                            methodsList.Items.Add(methodInfo);
                        }

                        UpdateStatus($"Загружено {methods.Count} методов для {selectedType.Name}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки методов: {ex.Message}",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void InvokeMethod(ListBox methodsList)
        {
            if (methodsList.SelectedItem != null && selectedType != null)
            {
                try
                {
                    string methodSignature = methodsList.SelectedItem.ToString();
                    string methodName = methodSignature.Split('(')[0].Split(' ').Last();

                    // Находим метод с правильной сигнатурой
                    var methods = selectedType.GetMethods()
                        .Where(m => m.Name == methodName)
                        .ToList();

                    if (methods.Count == 0)
                    {
                        MessageBox.Show($"Метод '{methodName}' не найден",
                            "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Берем первый метод (простейшая реализация)
                    MethodInfo method = methods[0];

                    // Проверяем параметры
                    var parameters = method.GetParameters();
                    object[] paramValues = new object[parameters.Length];

                    if (parameters.Length > 0)
                    {
                        // Простой ввод параметров через собственную форму
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            string paramName = parameters[i].Name;
                            string paramType = parameters[i].ParameterType.Name;

                            // Создаем простую форму для ввода
                            Form inputForm = new Form();
                            inputForm.Text = $"Ввод параметра {paramName}";
                            inputForm.Size = new Size(400, 150);
                            inputForm.StartPosition = FormStartPosition.CenterParent;
                            inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                            inputForm.MaximizeBox = false;
                            inputForm.MinimizeBox = false;

                            Label lbl = new Label();
                            lbl.Text = $"Введите значение для параметра {paramName} ({paramType}):";
                            lbl.Location = new Point(20, 20);
                            lbl.Width = 350;

                            TextBox txt = new TextBox();
                            txt.Location = new Point(20, 50);
                            txt.Width = 350;

                            Button btnOk = new Button();
                            btnOk.Text = "OK";
                            btnOk.Location = new Point(150, 80);
                            btnOk.DialogResult = DialogResult.OK;

                            inputForm.Controls.AddRange(new Control[] { lbl, txt, btnOk });
                            inputForm.AcceptButton = btnOk;

                            if (inputForm.ShowDialog() == DialogResult.OK)
                            {
                                string input = txt.Text;

                                // Простая конвертация типов
                                try
                                {
                                    if (parameters[i].ParameterType == typeof(string))
                                    {
                                        paramValues[i] = input;
                                    }
                                    else if (parameters[i].ParameterType == typeof(int))
                                    {
                                        paramValues[i] = int.Parse(input);
                                    }
                                    else if (parameters[i].ParameterType == typeof(double))
                                    {
                                        paramValues[i] = double.Parse(input);
                                    }
                                    else if (parameters[i].ParameterType == typeof(bool))
                                    {
                                        paramValues[i] = bool.Parse(input);
                                    }
                                    else
                                    {
                                        paramValues[i] = input;
                                    }
                                }
                                catch
                                {
                                    MessageBox.Show($"Неверное значение для параметра {paramName}",
                                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    return;
                                }
                            }
                            else
                            {
                                return; // Пользователь отменил ввод
                            }
                        }
                    }

                    object result = null;
                    string invocationType = "";

                    if (method.IsStatic)
                    {
                        result = method.Invoke(null, paramValues);
                        invocationType = "статический";
                    }
                    else
                    {
                        object instance = Activator.CreateInstance(selectedType);
                        result = method.Invoke(instance, paramValues);
                        invocationType = "экземплярный";
                    }

                    string resultText = result != null ? result.ToString() : "null";

                    MessageBox.Show(
                        $"✅ Метод успешно выполнен!\n\n" +
                        $"Тип: {invocationType}\n" +
                        $"Метод: {method.Name}\n" +
                        $"Результат: {resultText}\n" +
                        $"Тип результата: {method.ReturnType.Name}",
                        "Результат выполнения",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка выполнения: {ex.InnerException?.Message ?? ex.Message}",
                        "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Выберите метод для выполнения",
                    "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ============ МЕТОДЫ СЕТИ ============
        private async Task PingHost(string host, RichTextBox resultBox)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(host, 3000); // таймаут 3 секунды

                    string status = reply.Status == IPStatus.Success ? "УСПЕШНО" : "ОШИБКА";
                    string message = $"[{DateTime.Now:HH:mm:ss}] {host} - {status}";

                    if (reply.Status == IPStatus.Success)
                    {
                        message += $", время: {reply.RoundtripTime}ms, IP: {reply.Address}";
                    }
                    else
                    {
                        message += $", статус: {reply.Status}";
                    }

                    resultBox.AppendText(message + "\n");
                    resultBox.ScrollToCaret();
                }
            }
            catch (Exception ex)
            {
                resultBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {host} - ОШИБКА: {ex.Message}\n");
                resultBox.ScrollToCaret();
            }
        }

        private void ScanPorts(string host, int startPort, int endPort, ListBox portList, CancellationToken token)
        {
            this.Invoke(new Action(() =>
            {
                portList.Items.Clear();
                portList.Items.Add($"[{DateTime.Now:HH:mm:ss}] Сканирование {host}:{startPort}-{endPort}...");
            }));

            int totalPorts = endPort - startPort + 1;
            int scanned = 0;
            int openPorts = 0;

            Parallel.For(startPort, endPort + 1, new ParallelOptions
            {
                MaxDegreeOfParallelism = 50,
                CancellationToken = token
            }, port =>
            {
                if (token.IsCancellationRequested)
                    return;

                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        client.ConnectAsync(host, port).Wait(500); // таймаут 500ms

                        this.Invoke(new Action(() =>
                        {
                            portList.Items.Add($"✅ Порт {port}: ОТКРЫТ");
                            openPorts++;
                        }));
                    }
                }
                catch
                {
                    // Порт закрыт или недоступен
                }
                finally
                {
                    scanned++;

                    if (scanned % 10 == 0 || scanned == totalPorts)
                    {
                        this.Invoke(new Action(() =>
                        {
                            portList.Items[0] = $"[{DateTime.Now:HH:mm:ss}] Прогресс: {scanned}/{totalPorts} портов, открыто: {openPorts}";
                            portList.TopIndex = portList.Items.Count - 1;
                        }));
                    }
                }
            });

            if (!token.IsCancellationRequested)
            {
                this.Invoke(new Action(() =>
                {
                    portList.Items.Add($"[{DateTime.Now:HH:mm:ss}] Сканирование завершено!");
                    portList.Items.Add($"📊 Результат: {openPorts} открытых портов из {totalPorts}");
                }));
            }
        }

        // ============ МЕТОДЫ БЕЗОПАСНОСТИ ============
        private void EncryptText(string text, string key, TextBox output)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    MessageBox.Show("Введите текст для шифрования", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(key) || key.Length < 8)
                {
                    MessageBox.Show("Ключ должен содержать минимум 8 символов", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                byte[] encrypted = EncryptString(text, key);
                string encryptedBase64 = Convert.ToBase64String(encrypted);

                output.Text = encryptedBase64;
                UpdateStatus($"Текст зашифрован ({encrypted.Length} байт)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка шифрования: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DecryptText(string encryptedText, string key, TextBox output)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(encryptedText))
                {
                    MessageBox.Show("Введите зашифрованный текст", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    MessageBox.Show("Введите ключ для расшифровки", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                byte[] data = Convert.FromBase64String(encryptedText);
                string decrypted = DecryptString(data, key);

                output.Text = decrypted;
                UpdateStatus("Текст расшифрован");
            }
            catch (FormatException)
            {
                MessageBox.Show("Неверный формат зашифрованного текста", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка дешифрования: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private byte[] EncryptString(string plainText, string key)
        {
            using (Aes aes = Aes.Create())
            {
                // Создаем ключ и IV из пароля
                using (var sha256 = SHA256.Create())
                {
                    byte[] keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
                    byte[] iv = new byte[16];
                    Array.Copy(keyBytes, iv, 16);

                    aes.Key = keyBytes;
                    aes.IV = iv;
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(plainBytes, 0, plainBytes.Length);
                        cs.FlushFinalBlock();
                    }
                    return ms.ToArray();
                }
            }
        }

        private string DecryptString(byte[] cipherText, string key)
        {
            using (Aes aes = Aes.Create())
            {
                // Создаем ключ и IV из пароля
                using (var sha256 = SHA256.Create())
                {
                    byte[] keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
                    byte[] iv = new byte[16];
                    Array.Copy(keyBytes, iv, 16);

                    aes.Key = keyBytes;
                    aes.IV = iv;
                }

                using (MemoryStream ms = new MemoryStream(cipherText))
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (StreamReader sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private string GeneratePassword(int length, bool upper, bool lower, bool digits, bool special)
        {
            string chars = "";
            if (upper) chars += "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            if (lower) chars += "abcdefghijklmnopqrstuvwxyz";
            if (digits) chars += "0123456789";
            if (special) chars += "!@#$%^&*()-_=+[]{}|;:,.<>?";

            if (string.IsNullOrEmpty(chars))
                return "❌ Выберите хотя бы один тип символов";

            if (length < 6 || length > 32)
                return "❌ Длина пароля должна быть от 6 до 32 символов";

            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] randomBytes = new byte[length];
                rng.GetBytes(randomBytes);

                char[] password = new char[length];
                for (int i = 0; i < length; i++)
                {
                    password[i] = chars[randomBytes[i] % chars.Length];
                }

                return new string(password);
            }
        }

        // ============ МЕТОДЫ УТИЛИТ ============
        private void CleanTempFilesWithProgress()
        {
            try
            {
                string tempPath = Path.GetTempPath();
                var tempFiles = Directory.GetFiles(tempPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.EndsWith(".tmp") || f.EndsWith(".temp") || f.EndsWith(".log"))
                    .ToList();

                if (tempFiles.Count == 0)
                {
                    MessageBox.Show("Временные файлы не найдены", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                DialogResult result = MessageBox.Show(
                    $"Найдено {tempFiles.Count} временных файлов.\nУдалить их?",
                    "Подтверждение",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    int deletedCount = 0;
                    long totalSize = 0;

                    foreach (string file in tempFiles)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            totalSize += fileInfo.Length;
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch { }
                    }

                    string sizeText = totalSize > 1024 * 1024 ?
                        $"{(totalSize / 1024.0 / 1024.0):F2} MB" :
                        $"{(totalSize / 1024.0):F2} KB";

                    MessageBox.Show(
                        $"✅ Удалено {deletedCount} файлов\n" +
                        $"📦 Освобождено: {sizeText}",
                        "Очистка завершена",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TakeScreenshot()
        {
            try
            {
                this.WindowState = FormWindowState.Minimized;
                Thread.Sleep(500); // Даем время для минимизации

                Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                    Screen.PrimaryScreen.Bounds.Height);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                }

                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(desktopPath, fileName);

                bmp.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
                bmp.Dispose();

                this.WindowState = FormWindowState.Normal;

                DialogResult result = MessageBox.Show(
                    $"Скриншот сохранён на Рабочий стол:\n{fileName}\n\nОткрыть папку?",
                    "Скриншот создан",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    Process.Start("explorer.exe", desktopPath);
                }

                UpdateStatus($"Скриншот сохранён: {fileName}");
            }
            catch (Exception ex)
            {
                this.WindowState = FormWindowState.Normal;
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowDetailedSystemInfo()
        {
            StringBuilder sb = new StringBuilder();

            try
            {
                sb.AppendLine("=== ИНФОРМАЦИЯ О СИСТЕМЕ ===\n");
                sb.AppendLine($"Операционная система: {Environment.OSVersion}");
                sb.AppendLine($"Версия ОС: {Environment.OSVersion.Version}");
                sb.AppendLine($"Платформа: {Environment.OSVersion.Platform}");
                sb.AppendLine($"Пользователь: {Environment.UserName}");
                sb.AppendLine($"Домен: {Environment.UserDomainName}");
                sb.AppendLine($"Версия .NET: {Environment.Version}");
                sb.AppendLine($"64-битная ОС: {Environment.Is64BitOperatingSystem}");
                sb.AppendLine($"64-битный процесс: {Environment.Is64BitProcess}");
                sb.AppendLine($"Процессоров: {Environment.ProcessorCount}");
                sb.AppendLine($"Системная папка: {Environment.SystemDirectory}");

                sb.AppendLine("\n=== ПАМЯТЬ ===\n");
                var proc = Process.GetCurrentProcess();
                sb.AppendLine($"Всего физической памяти: {GetTotalPhysicalMemory() / 1024 / 1024 / 1024} GB");
                sb.AppendLine($"Доступно памяти: {Environment.WorkingSet / 1024 / 1024} MB");

                sb.AppendLine("\n=== ДИСКИ ===\n");
                foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    sb.AppendLine($"{drive.Name} ({drive.DriveType})");
                    sb.AppendLine($"  Файловая система: {drive.DriveFormat}");
                    sb.AppendLine($"  Всего места: {drive.TotalSize / 1024 / 1024 / 1024} GB");
                    sb.AppendLine($"  Свободно: {drive.TotalFreeSpace / 1024 / 1024 / 1024} GB");
                    sb.AppendLine($"  Использовано: {((drive.TotalSize - drive.TotalFreeSpace) * 100.0 / drive.TotalSize):F1}%");
                }

                sb.AppendLine("\n=== СЕТЬ ===\n");
                string hostName = Dns.GetHostName();
                sb.AppendLine($"Имя компьютера: {hostName}");

                try
                {
                    var ipAddresses = Dns.GetHostAddresses(hostName)
                        .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                    foreach (var ip in ipAddresses)
                    {
                        sb.AppendLine($"IP адрес: {ip}");
                    }
                }
                catch { }

                sb.AppendLine("\n=== ВРЕМЯ ===\n");
                sb.AppendLine($"Текущее время: {DateTime.Now}");
                sb.AppendLine($"Время UTC: {DateTime.UtcNow}");
                sb.AppendLine($"Часовой пояс: {TimeZoneInfo.Local.DisplayName}");

            }
            catch (Exception ex)
            {
                sb.AppendLine($"\nОшибка получения информации: {ex.Message}");
            }

            MessageBox.Show(sb.ToString(), "Системная информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private long GetTotalPhysicalMemory()
        {
            try
            {
                // Простой способ без System.Management
                return Environment.WorkingSet * 10; // Примерное значение
            }
            catch
            {
                return 0;
            }
        }

        private void ShowStartupManagerWithList()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== ПРОГРАММЫ В АВТОЗАГРУЗКЕ ===\n");

            try
            {
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                sb.AppendLine($"Папка автозагрузки: {startupPath}\n");

                if (Directory.Exists(startupPath))
                {
                    var startupFiles = Directory.GetFiles(startupPath, "*.lnk")
                        .Concat(Directory.GetFiles(startupPath, "*.exe"))
                        .Concat(Directory.GetFiles(startupPath, "*.bat"))
                        .ToList();

                    if (startupFiles.Count > 0)
                    {
                        foreach (string file in startupFiles)
                        {
                            string fileName = Path.GetFileName(file);
                            FileInfo fi = new FileInfo(file);
                            sb.AppendLine($"📌 {fileName}");
                            sb.AppendLine($"   Размер: {fi.Length / 1024} KB");
                            sb.AppendLine($"   Дата: {fi.LastWriteTime:dd.MM.yyyy HH:mm}");
                            sb.AppendLine();
                        }
                    }
                    else
                    {
                        sb.AppendLine("❌ Файлов автозагрузки не найдено");
                    }
                }
                else
                {
                    sb.AppendLine("❌ Папка автозагрузки не существует");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"\nОшибка: {ex.Message}");
            }

            MessageBox.Show(sb.ToString(), "Менеджер автозагрузки", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowServicesManager()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== СЛУЖБЫ WINDOWS ===\n");
            sb.AppendLine("Для полного управления службами используйте:\n");
            sb.AppendLine("1. services.msc - Диспетчер служб");
            sb.AppendLine("2. taskmgr.exe - Диспетчер задач (вкладка 'Службы')");
            sb.AppendLine("3. sc.exe - Командная строка управления службами\n");
            sb.AppendLine("Примеры команд:\n");
            sb.AppendLine("• sc query - список всех служб");
            sb.AppendLine("• sc start ИмяСлужбы - запуск службы");
            sb.AppendLine("• sc stop ИмяСлужбы - остановка службы");
            sb.AppendLine("• sc config ИмяСлужбы start= auto - автозапуск");
            sb.AppendLine("• sc config ИмяСлужбы start= disabled - отключить");

            MessageBox.Show(sb.ToString(), "Диспетчер служб", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowEventViewer()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== ПРОСМОТР СОБЫТИЙ WINDOWS ===\n");
            sb.AppendLine("Для просмотра событий используйте:\n");
            sb.AppendLine("1. eventvwr.msc - Просмотр событий");
            sb.AppendLine("2. wevtutil.exe - Утилита командной строки\n");
            sb.AppendLine("Основные журналы:\n");
            sb.AppendLine("• Application - события приложений");
            sb.AppendLine("• System - системные события");
            sb.AppendLine("• Security - события безопасности");
            sb.AppendLine("• Setup - события установки\n");
            sb.AppendLine("Примеры команд:\n");
            sb.AppendLine("• wevtutil qe Application /c:10 /f:text");
            sb.AppendLine("  (последние 10 событий приложений)");
            sb.AppendLine("• wevtutil qe System /c:5 /f:text");
            sb.AppendLine("  (последние 5 системных событий)");

            MessageBox.Show(sb.ToString(), "Просмотр событий", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowRegistryViewer()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== РЕЕСТР WINDOWS ===\n");
            sb.AppendLine("⚠️ ВНИМАНИЕ: Неправильное изменение реестра может повредить систему!\n");
            sb.AppendLine("Для работы с реестром используйте:\n");
            sb.AppendLine("1. regedit.exe - Редактор реестра");
            sb.AppendLine("2. reg.exe - Утилита командной строки\n");
            sb.AppendLine("Основные ветки реестра:\n");
            sb.AppendLine("• HKEY_CLASSES_ROOT - ассоциации файлов");
            sb.AppendLine("• HKEY_CURRENT_USER - настройки текущего пользователя");
            sb.AppendLine("• HKEY_LOCAL_MACHINE - системные настройки");
            sb.AppendLine("• HKEY_USERS - настройки всех пользователей");
            sb.AppendLine("• HKEY_CURRENT_CONFIG - текущая конфигурация\n");
            sb.AppendLine("Пример безопасных команд:\n");
            sb.AppendLine("• reg query HKCU\\Software\\Microsoft\\Windows");
            sb.AppendLine("  (просмотр настроек Windows)");
            sb.AppendLine("• reg export HKCU\\MySettings C:\\backup.reg");
            sb.AppendLine("  (экспорт настроек в файл)");

            MessageBox.Show(sb.ToString(), "Редактор реестра", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowMiniGames()
        {
            Form gameForm = new Form();
            gameForm.Text = "🎮 Мини-игры";
            gameForm.Size = new Size(400, 300);
            gameForm.StartPosition = FormStartPosition.CenterScreen;
            gameForm.BackColor = Color.FromArgb(30, 30, 40);
            gameForm.ForeColor = Color.White;
            gameForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            gameForm.MaximizeBox = false;
            gameForm.MinimizeBox = false;

            Label title = new Label();
            title.Text = "Выберите игру:";
            title.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            title.ForeColor = Color.Cyan;
            title.Dock = DockStyle.Top;
            title.Height = 50;
            title.TextAlign = ContentAlignment.MiddleCenter;

            Button btnGuessNumber = new Button();
            btnGuessNumber.Text = "🎯 Угадай число";
            btnGuessNumber.BackColor = Color.Purple;
            btnGuessNumber.ForeColor = Color.White;
            btnGuessNumber.FlatStyle = FlatStyle.Flat;
            btnGuessNumber.Font = new Font("Segoe UI", 11);
            btnGuessNumber.Size = new Size(300, 40);
            btnGuessNumber.Location = new Point(50, 70);
            btnGuessNumber.Click += (s, e) => PlayGuessNumberGame();

            Button btnReaction = new Button();
            btnReaction.Text = "⚡ Тест реакции";
            btnReaction.BackColor = Color.Blue;
            btnReaction.ForeColor = Color.White;
            btnReaction.FlatStyle = FlatStyle.Flat;
            btnReaction.Font = new Font("Segoe UI", 11);
            btnReaction.Size = new Size(300, 40);
            btnReaction.Location = new Point(50, 120);
            btnReaction.Click += (s, e) => PlayReactionTest();

            Button btnClose = new Button();
            btnClose.Text = "Закрыть";
            btnClose.BackColor = Color.Gray;
            btnClose.ForeColor = Color.White;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Font = new Font("Segoe UI", 11);
            btnClose.Size = new Size(300, 40);
            btnClose.Location = new Point(50, 170);
            btnClose.Click += (s, e) => gameForm.Close();

            gameForm.Controls.AddRange(new Control[] { btnClose, btnReaction, btnGuessNumber, title });
            gameForm.ShowDialog();
        }

        private void PlayGuessNumberGame()
        {
            Random rnd = new Random();
            int secretNumber = rnd.Next(1, 101);
            int attempts = 0;

            Form game = new Form();
            game.Text = "🎯 Угадай число (1-100)";
            game.Size = new Size(350, 200);
            game.StartPosition = FormStartPosition.CenterScreen;
            game.BackColor = Color.FromArgb(30, 30, 40);
            game.ForeColor = Color.White;
            game.FormBorderStyle = FormBorderStyle.FixedDialog;

            Label lblPrompt = new Label();
            lblPrompt.Text = "Введите число от 1 до 100:";
            lblPrompt.Font = new Font("Segoe UI", 11);
            lblPrompt.Location = new Point(20, 20);
            lblPrompt.Size = new Size(300, 30);

            TextBox txtGuess = new TextBox();
            txtGuess.Location = new Point(20, 60);
            txtGuess.Size = new Size(100, 30);
            txtGuess.Font = new Font("Segoe UI", 11);
            txtGuess.BackColor = Color.FromArgb(60, 60, 70);
            txtGuess.ForeColor = Color.White;

            Button btnSubmit = new Button();
            btnSubmit.Text = "Проверить";
            btnSubmit.Location = new Point(130, 60);
            btnSubmit.Size = new Size(100, 30);
            btnSubmit.BackColor = Color.Green;
            btnSubmit.ForeColor = Color.White;
            btnSubmit.Font = new Font("Segoe UI", 10);

            Label lblResult = new Label();
            lblResult.Location = new Point(20, 100);
            lblResult.Size = new Size(300, 50);
            lblResult.Font = new Font("Segoe UI", 10);

            btnSubmit.Click += (s, e) =>
            {
                attempts++;
                if (int.TryParse(txtGuess.Text, out int guess))
                {
                    if (guess < 1 || guess > 100)
                    {
                        lblResult.Text = "Число должно быть от 1 до 100!";
                        lblResult.ForeColor = Color.Orange;
                    }
                    else if (guess < secretNumber)
                    {
                        lblResult.Text = $"Слишком маленькое! Попытка: {attempts}";
                        lblResult.ForeColor = Color.Yellow;
                    }
                    else if (guess > secretNumber)
                    {
                        lblResult.Text = $"Слишком большое! Попытка: {attempts}";
                        lblResult.ForeColor = Color.Yellow;
                    }
                    else
                    {
                        lblResult.Text = $"🎉 Поздравляем! Вы угадали за {attempts} попыток!";
                        lblResult.ForeColor = Color.Lime;
                        btnSubmit.Enabled = false;
                        txtGuess.Enabled = false;
                    }
                }
                else
                {
                    lblResult.Text = "Введите число!";
                    lblResult.ForeColor = Color.Red;
                }
                txtGuess.SelectAll();
                txtGuess.Focus();
            };

            game.Controls.AddRange(new Control[] { lblResult, btnSubmit, txtGuess, lblPrompt });
            game.ShowDialog();
        }

        private void PlayReactionTest()
        {
            Form game = new Form();
            game.Text = "⚡ Тест реакции";
            game.Size = new Size(400, 300);
            game.StartPosition = FormStartPosition.CenterScreen;
            game.BackColor = Color.Black;
            game.ForeColor = Color.White;
            game.FormBorderStyle = FormBorderStyle.FixedDialog;

            Label lblInstruction = new Label();
            lblInstruction.Text = "Нажмите кнопку когда она станет ЗЕЛЁНОЙ!";
            lblInstruction.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblInstruction.ForeColor = Color.White;
            lblInstruction.Location = new Point(20, 20);
            lblInstruction.Size = new Size(350, 30);
            lblInstruction.TextAlign = ContentAlignment.MiddleCenter;

            Button btnReact = new Button();
            btnReact.Text = "ЖДИ...";
            btnReact.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            btnReact.Size = new Size(200, 100);
            btnReact.Location = new Point(100, 70);
            btnReact.BackColor = Color.Red;
            btnReact.ForeColor = Color.White;
            btnReact.FlatStyle = FlatStyle.Flat;
            btnReact.Enabled = false;

            Label lblResult = new Label();
            lblResult.Font = new Font("Segoe UI", 11);
            lblResult.Location = new Point(20, 190);
            lblResult.Size = new Size(350, 50);
            lblResult.TextAlign = ContentAlignment.MiddleCenter;

            Button btnStart = new Button();
            btnStart.Text = "НАЧАТЬ ТЕСТ";
            btnStart.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnStart.Size = new Size(150, 40);
            btnStart.Location = new Point(125, 240);
            btnStart.BackColor = Color.Blue;
            btnStart.ForeColor = Color.White;
            btnStart.FlatStyle = FlatStyle.Flat;

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            Random rnd = new Random();
            DateTime startTime = DateTime.Now;
            bool testActive = false;

            btnStart.Click += (s, e) =>
            {
                if (!testActive)
                {
                    testActive = true;
                    btnStart.Enabled = false;
                    lblResult.Text = "Приготовьтесь...";
                    lblResult.ForeColor = Color.Yellow;

                    int delay = rnd.Next(1000, 5000);
                    timer.Interval = delay;
                    timer.Tick += (s2, e2) =>
                    {
                        timer.Stop();
                        btnReact.BackColor = Color.Lime;
                        btnReact.Text = "НАЖМИ!";
                        btnReact.Enabled = true;
                        startTime = DateTime.Now;
                    };
                    timer.Start();
                }
            };

            btnReact.Click += (s, e) =>
            {
                if (btnReact.BackColor == Color.Lime)
                {
                    TimeSpan reactionTime = DateTime.Now - startTime;
                    lblResult.Text = $"Реакция: {reactionTime.TotalMilliseconds:F0} мс";
                    lblResult.ForeColor = Color.Lime;
                    btnReact.BackColor = Color.Red;
                    btnReact.Text = "ЖДИ...";
                    btnReact.Enabled = false;
                    btnStart.Enabled = true;
                    testActive = false;
                }
            };

            game.Controls.AddRange(new Control[] { lblResult, btnStart, btnReact, lblInstruction });
            game.ShowDialog();
        }

        // ============ P/INVOKE ============
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // ============ ИНИЦИАЛИЗАЦИЯ И ЗАВЕРШЕНИЕ - ИСПРАВЛЕНА ============
        private void InitializeAllModules()
        {
            try
            {
                // Инициализация PerformanceCounter
                try
                {
                    cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    // Считываем первое значение для инициализации
                    float cpuInit = cpuCounter.NextValue();
                }
                catch
                {
                    // Альтернативный счетчик если первый не работает
                    cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "0");
                }

                try
                {
                    ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                    float ramInit = ramCounter.NextValue();
                }
                catch
                {
                    // Альтернативный счетчик памяти
                    ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                }

                // Мониторинг
                monitorUITimer = new System.Windows.Forms.Timer { Interval = 1000 };
                monitorUITimer.Tick += MonitorTimer_Tick;
                monitorUITimer.Start();

                UpdateStatus("Система готова к работе");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                float cpu = 0;
                float ram = 0;

                try
                {
                    cpu = cpuCounter?.NextValue() ?? 0;
                }
                catch
                {
                    // Альтернативный способ получения загрузки CPU
                    using (var cpuPerf = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                    {
                        cpu = cpuPerf.NextValue();
                    }
                }

                try
                {
                    if (ramCounter != null && ramCounter.CounterName == "% Committed Bytes In Use")
                    {
                        ram = ramCounter.NextValue();
                    }
                    else if (ramCounter != null && ramCounter.CounterName == "Available MBytes")
                    {
                        float availableMB = ramCounter.NextValue();
                        // Примерный расчет использования памяти
                        long totalMem = GetTotalPhysicalMemory();
                        if (totalMem > 0)
                        {
                            ram = 100 - (availableMB / (totalMem / 1024 / 1024) * 100);
                            if (ram < 0) ram = 0;
                            if (ram > 100) ram = 100;
                        }
                    }
                }
                catch
                {
                    // Простой расчет использования памяти
                    using (var proc = Process.GetCurrentProcess())
                    {
                        long totalMem = GetTotalPhysicalMemory();
                        if (totalMem > 0)
                        {
                            ram = (float)proc.WorkingSet64 / totalMem * 100;
                        }
                    }
                }

                // Обновляем статус бар
                cpuStatusLabel.Text = $"CPU: {cpu:F1}%";
                ramStatusLabel.Text = $"RAM: {ram:F1}%";

                // Обновляем график
                if (perfChart != null && perfChart.Series.Count >= 2)
                {
                    int maxPoints = (int)(numHistoryPoints?.Value ?? 50);

                    if (perfChart.Series["CPU"].Points.Count > maxPoints)
                    {
                        perfChart.Series["CPU"].Points.RemoveAt(0);
                        perfChart.Series["RAM"].Points.RemoveAt(0);
                    }

                    perfChart.Series["CPU"].Points.AddY(cpu);
                    perfChart.Series["RAM"].Points.AddY(ram);
                }

                // Обновляем виджеты через поля класса
                UpdateWidgetValue(cpuWidget, $"{cpu:F1}%");
                UpdateWidgetValue(ramWidget, $"{ram:F1}%");
                UpdateWidgetValue(procWidget, Process.GetProcesses().Length.ToString());
                UpdateWidgetValue(threadWidget, Process.GetProcesses().Sum(p => p.Threads.Count).ToString());
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки мониторинга
                Debug.WriteLine($"Monitor error: {ex.Message}");
            }
        }

        private void UpdateWidgetValue(Panel widget, string value)
        {
            if (widget != null && !widget.IsDisposed)
            {
                foreach (Control control in widget.Controls)
                {
                    if (control.Tag != null && control.Tag.ToString() == "valueLabel")
                    {
                        control.Text = value;
                        break;
                    }
                }
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusLabel != null && !statusLabel.IsDisposed)
            {
                statusLabel.Text = message;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            lab7Cts?.Cancel();
            StopKeyboardHook();
            monitorUITimer?.Stop();
            lab7ThreadTimer?.Dispose();
            cpuCounter?.Dispose();
            ramCounter?.Dispose();

            base.OnFormClosing(e);
        }

        // Пустой метод для совместимости с Designer
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Оставлен для совместимости
        }
    }
}