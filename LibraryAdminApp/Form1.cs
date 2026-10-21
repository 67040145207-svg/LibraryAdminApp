using System.Diagnostics;
using LibraryAdminApp.Services;
using RoomReservation.Contracts;

namespace LibraryAdminApp
{
    public partial class Form1 : Form
    {
        private readonly ReservationApiClient _api = new("http://localhost:5180");

        // เก็บข้อมูลปัจจุบันไว้ใช้ผูกกับ DataGridView ผ่าน BindingSource
        private readonly BindingSource _bookingsBinding = new();
        private readonly BindingSource _utilizationBinding = new();
        private readonly BindingSource _penaltiesBinding = new();
        private readonly BindingSource _maintenanceBinding = new();
        private readonly BindingSource _notificationsBinding = new();

        private List<RoomDto> _rooms = new();
        private List<BookingDto> _currentBookings = new();
        private bool _isInitializing = true;

        // Dashboard Controls
        private Label _lblKpiUtilization = new();
        private Label _lblKpiActiveBookings = new();
        private Label _lblKpiCheckedIn = new();
        private Label _lblKpiAutoReleased = new();
        private Label _lblKpiPenalties = new();
        private Label _lblKpiMaintenance = new();
        private DataGridView _dgvUtilization = new();
        private DataGridView _dgvNotifications = new();

        // Bookings Tab Controls
        private ComboBox _cboFilterStatus = new();
        private TextBox _txtFilterStudentId = new();
        private TextBox _txtOverrideReason = new();
        private Label _lblSelectedBookingInfo = new();

        // Admin Desk Check-in Tab Controls (ลบ QR Code ออกทั้งหมด เหลือเฉพาะกรอกรหัสนักศึกษาและกรอก PIN)
        private TextBox _txtCheckInPinOrQr = new();
        private TextBox _txtVerifyMemberId = new();
        private CheckBox _chkInsideBuildingWifi = new();

        // Penalties Tab Controls
        private DataGridView _dgvPenalties = new();
        private TextBox _txtQuotaLookupStudentId = new();
        private Label _lblQuotaLookupResult = new();

        // Maintenance Tab Controls
        private DataGridView _dgvMaintenance = new();
        private ComboBox _cboMaintRoom = new();
        private TextBox _txtMaintEquip = new();
        private TextBox _txtMaintTitle = new();
        private ComboBox _cboMaintStatusUpdate = new();

        public Form1()
        {
            InitializeComponent();
            BuildDashboardTab();
            BuildBookingsTab();
            BuildKioskTab();
            BuildPenaltiesTab();
            BuildMaintenanceTab();

            _isInitializing = false;

            this.Load += async (s, e) =>
            {
                await LoadAllDataAsync();
            };
        }

        private static Button CreateStyledButton(string text, Color backColor, int minWidth = 140, int minHeight = 38)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(minWidth, minHeight),
                Padding = new Padding(12, 4, 12, 4),
                Margin = new Padding(4),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // ==================== 1. DASHBOARD TAB (4 ห้อง: ห้องใหญ่ 601 / ห้องกลาง 602, 603, 604) ====================

        private void BuildDashboardTab()
        {
            var pnlKpis = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 105,
                ColumnCount = 6,
                RowCount = 1,
                Padding = new Padding(10, 10, 10, 6)
            };
            for (int i = 0; i < 6; i++)
                pnlKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66f));

            pnlKpis.Controls.Add(CreateKpiCard("อัตราการใช้ห้องเฉลี่ย", out _lblKpiUtilization, Color.FromArgb(5, 150, 105)), 0, 0);
            pnlKpis.Controls.Add(CreateKpiCard("การจองวันนี้ (Active)", out _lblKpiActiveBookings, Color.FromArgb(30, 64, 175)), 1, 0);
            pnlKpis.Controls.Add(CreateKpiCard("เปิดประตูห้องใช้งานอยู่", out _lblKpiCheckedIn, Color.FromArgb(79, 70, 229)), 2, 0);
            pnlKpis.Controls.Add(CreateKpiCard("ตัดสิทธิ์อัตโนมัติ (>15m)", out _lblKpiAutoReleased, Color.FromArgb(217, 119, 6)), 3, 0);
            pnlKpis.Controls.Add(CreateKpiCard("ติดโทษแบน (2 วัน)", out _lblKpiPenalties, Color.FromArgb(225, 29, 72)), 4, 0);
            pnlKpis.Controls.Add(CreateKpiCard("แจ้งซ่อมคงค้าง", out _lblKpiMaintenance, Color.FromArgb(234, 88, 12)), 5, 0);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 270,
                Padding = new Padding(10)
            };

            var pnlUtilHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(4)
            };
            pnlUtilHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlUtilHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblUtilTitle = new Label
            {
                Text = "📈 สถิติอัตราการใช้งานห้องทั้ง 4 ห้อง (ห้องใหญ่ 601 ขั้นต่ำ 7 คน • ห้องกลาง 602, 603, 604 ขั้นต่ำ 5 คน • เปิด 09:00–17:00 น.)",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnRunAutoRelease = CreateStyledButton(
                "⚡ รันตรวจสอบ Auto-Release (เกิน 15 นาที) & แบน 2 วันทันที",
                Color.FromArgb(217, 119, 6),
                280, 36);
            btnRunAutoRelease.Click += async (s, e) => await TriggerAutoReleaseAsync();

            pnlUtilHeader.Controls.Add(lblUtilTitle, 0, 0);
            pnlUtilHeader.Controls.Add(btnRunAutoRelease, 1, 0);

            ConfigureGrid(_dgvUtilization, _utilizationBinding);
            split.Panel1.Controls.Add(_dgvUtilization);
            split.Panel1.Controls.Add(pnlUtilHeader);

            var lblNotifTitle = new Label
            {
                Text = "🔔 ประวัติการส่งแจ้งเตือนอัตโนมัติ (LINE Messaging API & Email Notifications)",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            ConfigureGrid(_dgvNotifications, _notificationsBinding);
            split.Panel2.Controls.Add(_dgvNotifications);
            split.Panel2.Controls.Add(lblNotifTitle);

            tabDashboard.Controls.Add(split);
            tabDashboard.Controls.Add(pnlKpis);
        }

        // ==================== 2. BOOKINGS & DOOR UNLOCK / OVERRIDE TAB ====================

        private void BuildBookingsTab()
        {
            var pnlFilter = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 52),
                Padding = new Padding(10, 8, 10, 8),
                BackColor = Color.White,
                WrapContents = false
            };

            pnlFilter.Controls.Add(new Label
            {
                Text = "กรองสถานะ:",
                AutoSize = true,
                Margin = new Padding(4, 8, 4, 0),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            });

            _cboFilterStatus = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 170,
                Margin = new Padding(4, 4, 12, 4)
            };
            _cboFilterStatus.Items.AddRange(new object[] { "ทั้งหมด", "Reserved", "CheckedIn", "AutoReleased", "Cancelled", "EmergencyOverride", "Completed" });
            _cboFilterStatus.SelectedIndex = 0;
            _cboFilterStatus.SelectedIndexChanged += async (s, e) =>
            {
                if (!_isInitializing)
                    await LoadBookingsAsync(showNotFoundAlert: false);
            };
            pnlFilter.Controls.Add(_cboFilterStatus);

            pnlFilter.Controls.Add(new Label
            {
                Text = "ค้นหารหัสนักศึกษา/บุคลากร:",
                AutoSize = true,
                Margin = new Padding(4, 8, 4, 0),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            });

            _txtFilterStudentId = new TextBox
            {
                Width = 160,
                PlaceholderText = "เช่น 6610400001",
                Margin = new Padding(4, 4, 8, 4)
            };
            pnlFilter.Controls.Add(_txtFilterStudentId);

            var btnSearch = CreateStyledButton("🔍 ค้นหา", Color.FromArgb(15, 118, 110), 95, 32);
            btnSearch.Click += async (s, e) => await LoadBookingsAsync(showNotFoundAlert: true);
            pnlFilter.Controls.Add(btnSearch);

            var pnlBottom = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 195,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.White,
                Padding = new Padding(10, 6, 10, 8)
            };
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));

            // Left Group: เช็คอินที่ห้อง Admin & เปิดประตูห้อง + เปลี่ยนสถานะ
            var grpCheckInDoor = new GroupBox
            {
                Text = "🔑 สำหรับเจ้าหน้าที่ห้อง Admin: รับเช็คอิน & ไปเปิดประตูห้องให้ผู้จอง",
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };

            var leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lblSelectedBookingInfo = new Label
            {
                Text = "💡 คลิกเลือกรายการจองในตารางด้านบน เมื่อผู้จองเดินมาติดต่อที่ห้อง Admin แล้วกดปุ่มด้านล่างเพื่อเช็คอินและเปิดประตูห้อง",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(51, 65, 85),
                Margin = new Padding(2, 2, 2, 8)
            };

            var btnAdminUnlockCheckIn = CreateStyledButton(
                "🔑 ยืนยันเช็คอินที่ห้อง Admin & ไปเปิดประตูห้องให้รายการที่เลือก",
                Color.FromArgb(5, 150, 105),
                380, 40);
            btnAdminUnlockCheckIn.Dock = DockStyle.Top;
            btnAdminUnlockCheckIn.Click += async (s, e) => await PerformKioskCheckInAsync(CheckInMethod.AdminCounter);

            var statusFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 8, 0, 0)
            };
            statusFlow.Controls.Add(new Label
            {
                Text = "หรือปรับสถานะโดย Admin:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(2, 8, 6, 0)
            });

            cboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStatus.Items.Clear();
            cboStatus.Items.AddRange(new object[] { "Reserved", "CheckedIn", "Completed", "AutoReleased", "Cancelled" });
            cboStatus.SelectedIndex = 1;
            cboStatus.Width = 165;
            cboStatus.Font = new Font("Segoe UI", 9.5f);
            cboStatus.Margin = new Padding(4, 4, 8, 4);
            statusFlow.Controls.Add(cboStatus);

            btnUpdateStatus.Text = "💾 อัปเดตสถานะ";
            btnUpdateStatus.AutoSize = true;
            btnUpdateStatus.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnUpdateStatus.MinimumSize = new Size(140, 34);
            btnUpdateStatus.Padding = new Padding(10, 3, 10, 3);
            btnUpdateStatus.BackColor = Color.FromArgb(37, 99, 235);
            btnUpdateStatus.ForeColor = Color.White;
            btnUpdateStatus.FlatStyle = FlatStyle.Flat;
            btnUpdateStatus.FlatAppearance.BorderSize = 0;
            btnUpdateStatus.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            btnUpdateStatus.Margin = new Padding(4, 2, 4, 2);
            btnUpdateStatus.Click -= btnUpdateStatus_Click;
            btnUpdateStatus.Click += btnUpdateStatus_Click;
            statusFlow.Controls.Add(btnUpdateStatus);

            leftLayout.Controls.Add(_lblSelectedBookingInfo, 0, 0);
            leftLayout.Controls.Add(btnAdminUnlockCheckIn, 0, 1);
            leftLayout.Controls.Add(statusFlow, 0, 2);
            grpCheckInDoor.Controls.Add(leftLayout);

            // Right Group: Emergency Override
            var grpOverride = new GroupBox
            {
                Text = "🚨 ยกเลิกการจองกรณีฉุกเฉิน (Emergency Override)",
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            };

            var rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            rightLayout.Controls.Add(new Label
            {
                Text = "ระบุเหตุผลฉุกเฉิน (ระบบจะส่งแจ้งเตือน LINE / Email ให้ผู้จองทันทีโดยไม่เสียโควตา):",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(51, 65, 85),
                Margin = new Padding(2, 2, 2, 6)
            }, 0, 0);

            _txtOverrideReason = new TextBox
            {
                Text = "ห้องต้องใช้จัดสอบด่วน / ระบบปรับอากาศชำรุดกะทันหัน",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(2, 2, 2, 10)
            };
            rightLayout.Controls.Add(_txtOverrideReason, 0, 1);

            var btnEmergencyOverride = CreateStyledButton(
                "🚨 ยืนยันยกเลิกฉุกเฉิน (Emergency Override) & แจ้งผู้จอง",
                Color.FromArgb(225, 29, 72),
                360, 40);
            btnEmergencyOverride.Dock = DockStyle.Top;
            btnEmergencyOverride.Click += async (s, e) => await ExecuteEmergencyOverrideAsync();
            rightLayout.Controls.Add(btnEmergencyOverride, 0, 2);

            grpOverride.Controls.Add(rightLayout);

            pnlBottom.Controls.Add(grpCheckInDoor, 0, 0);
            pnlBottom.Controls.Add(grpOverride, 1, 0);

            ConfigureGrid(dgvBookings, _bookingsBinding);
            dgvBookings.SelectionChanged += (s, e) =>
            {
                try
                {
                    if (dgvBookings.CurrentRow?.DataBoundItem is BookingDto b)
                    {
                        _lblSelectedBookingInfo.Text =
                            $"รายการที่เลือก: {b.BookingNo} | ห้อง {b.RoomCode} | ผู้จอง: {b.HostFullName} ({b.HostStudentOrStaffId}) | PIN: {b.CheckInPinCode}";
                        _txtCheckInPinOrQr.Text = b.CheckInPinCode;
                        _txtVerifyMemberId.Text = b.HostStudentOrStaffId;
                    }
                }
                catch
                {
                    // Ignore selection binding errors
                }
            };

            tabBookings.Controls.Add(dgvBookings);
            tabBookings.Controls.Add(pnlBottom);
            tabBookings.Controls.Add(pnlFilter);
        }

        // ==================== 3. ADMIN DESK CHECK-IN & DOOR UNLOCK TAB (ไม่มี QR Code เหลือเฉพาะกรอกรหัสนักศึกษาและกรอก PIN) ====================

        private void BuildKioskTab()
        {
            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24)
            };

            var grpCheckIn = new GroupBox
            {
                Text = "🔑 เจ้าหน้าที่ห้อง Admin: จุดตรวจสอบรหัสนักศึกษา & รหัส PIN เพื่อเช็คอินและเปิดประตูห้อง",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(20),
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold)
            };

            var checkInLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 5
            };
            for (int i = 0; i < 5; i++)
                checkInLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblRule = new Label
            {
                Text = "• เวลาเปิดบริการ: 09:00 – 17:00 น. (ห้องใหญ่ 601 ขั้นต่ำ 7 คน • ห้องกลาง 602, 603, 604 ขั้นต่ำ 5 คน)\n" +
                       "• เงื่อนไขเวลาเช็คอิน: สามารถเข้ามาเช็คอินล่วงหน้าได้ตั้งแต่ 30 นาที ถึง 1 ชั่วโมง ก่อนถึงเวลาจอง (และไม่เกิน 15 นาทีหลังเวลาเริ่มจอง)\n" +
                       "• ขั้นตอน: กรอกรหัสนักศึกษาและรหัส PIN 6 หลัก (หรือรหัสการจอง) ที่ผู้ใช้งานนำมาแจ้งที่ห้อง Admin เพื่อกดเช็คอินและไปเปิดประตูห้องให้",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.75f),
                ForeColor = Color.FromArgb(51, 65, 85),
                Margin = new Padding(4, 6, 4, 14)
            };

            _chkInsideBuildingWifi = new CheckBox
            {
                Text = "✅ ตรวจสอบเครือข่ายภายในอาคารห้องสมุด (IP: 10.20.1.55 / พิกัดห้อง Admin)",
                Checked = true,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(5, 150, 105),
                Margin = new Padding(4, 4, 4, 16)
            };

            // Row 3: กรอกรหัสนักศึกษา
            var verifyFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 4, 0, 12)
            };
            verifyFlow.Controls.Add(new Label
            {
                Text = "1) กรอกรหัสนักศึกษา/บุคลากร (ผู้จอง):",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
                Margin = new Padding(4, 10, 8, 0)
            });
            _txtVerifyMemberId = new TextBox
            {
                Text = "6610400002",
                Width = 180,
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(4, 6, 8, 4)
            };
            verifyFlow.Controls.Add(_txtVerifyMemberId);

            var btnVerifyMember = CreateStyledButton(
                "🔍 ค้นหา & ตรวจสอบรหัสนักศึกษา",
                Color.FromArgb(217, 119, 6),
                230, 38);
            btnVerifyMember.Click += async (s, e) => await VerifyGroupMemberFromKioskAsync();
            verifyFlow.Controls.Add(btnVerifyMember);

            // Row 4: กรอก PIN ตรวจสอบ
            var pinFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 4, 0, 14)
            };
            pinFlow.Controls.Add(new Label
            {
                Text = "2) กรอกรหัส PIN 6 หลัก (หรือรหัสการจอง):",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.75f, FontStyle.Bold),
                Margin = new Padding(4, 10, 8, 0)
            });
            _txtCheckInPinOrQr = new TextBox
            {
                Text = "739201",
                Width = 180,
                Font = new Font("Segoe UI", 10f),
                Margin = new Padding(4, 6, 8, 4)
            };
            pinFlow.Controls.Add(_txtCheckInPinOrQr);

            var btnCheckInPin = CreateStyledButton(
                "🔑 ตรวจรหัส PIN & เช็คอินเปิดประตูห้อง",
                Color.FromArgb(5, 150, 105),
                260, 38);
            btnCheckInPin.Click += async (s, e) => await PerformKioskCheckInAsync(CheckInMethod.PinCode);
            pinFlow.Controls.Add(btnCheckInPin);

            // Row 5: ปุ่มยืนยันเช็คอินโดยเจ้าหน้าที่ห้อง Admin ทันที
            var btnCheckInAdminDesk = CreateStyledButton(
                "✅ ยืนยันเช็คอินที่ห้อง Admin & ไปเปิดประตูห้องให้ผู้ใช้งานทันที",
                Color.FromArgb(79, 70, 229),
                440, 44);
            btnCheckInAdminDesk.Margin = new Padding(4, 8, 4, 6);
            btnCheckInAdminDesk.Click += async (s, e) => await PerformKioskCheckInAsync(CheckInMethod.AdminCounter);

            checkInLayout.Controls.Add(lblRule, 0, 0);
            checkInLayout.Controls.Add(_chkInsideBuildingWifi, 0, 1);
            checkInLayout.Controls.Add(verifyFlow, 0, 2);
            checkInLayout.Controls.Add(pinFlow, 0, 3);
            checkInLayout.Controls.Add(btnCheckInAdminDesk, 0, 4);

            grpCheckIn.Controls.Add(checkInLayout);
            pnlMain.Controls.Add(grpCheckIn);

            tabKiosk.Controls.Add(pnlMain);
        }

        // ==================== 4. PENALTIES TAB (2-DAY NO-SHOW BAN) ====================

        private void BuildPenaltiesTab()
        {
            var pnlTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 56),
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8),
                WrapContents = true
            };

            pnlTop.Controls.Add(new Label
            {
                Text = "ตรวจสอบโควตารายวัน & สถานะโทษแบนของรหัสนักศึกษา/บุคลากร:",
                AutoSize = true,
                Margin = new Padding(4, 10, 6, 0),
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold)
            });

            _txtQuotaLookupStudentId = new TextBox
            {
                Text = "6610400001",
                Width = 140,
                Margin = new Padding(4, 6, 8, 4)
            };
            pnlTop.Controls.Add(_txtQuotaLookupStudentId);

            var btnCheckQuota = CreateStyledButton("🔍 ตรวจสิทธิ์/โควตา", Color.FromArgb(15, 118, 110), 140, 34);
            btnCheckQuota.Click += async (s, e) => await LookupStudentQuotaAsync();
            pnlTop.Controls.Add(btnCheckQuota);

            _lblQuotaLookupResult = new Label
            {
                Text = "กดปุ่มตรวจสิทธิ์เพื่อดูโควตาคงเหลือและสถานะโทษแบน",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                Margin = new Padding(10, 10, 4, 0)
            };
            pnlTop.Controls.Add(_lblQuotaLookupResult);

            var pnlBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                MinimumSize = new Size(0, 56),
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };
            var btnPardon = CreateStyledButton(
                "🔓 ปลดแบน (Pardon) ให้รายการที่เลือกในตาราง",
                Color.FromArgb(30, 41, 59),
                300, 38);
            btnPardon.Click += async (s, e) => await PardonSelectedPenaltyAsync();
            pnlBottom.Controls.Add(btnPardon);

            ConfigureGrid(_dgvPenalties, _penaltiesBinding);
            tabPenalties.Controls.Add(_dgvPenalties);
            tabPenalties.Controls.Add(pnlBottom);
            tabPenalties.Controls.Add(pnlTop);
        }

        // ==================== 5. MAINTENANCE TAB ====================

        private void BuildMaintenanceTab()
        {
            var pnlTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, 60),
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8),
                WrapContents = true
            };

            pnlTop.Controls.Add(new Label { Text = "ห้อง:", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _cboMaintRoom = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 260,
                Margin = new Padding(4, 6, 10, 4)
            };
            pnlTop.Controls.Add(_cboMaintRoom);

            pnlTop.Controls.Add(new Label { Text = "อุปกรณ์:", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _txtMaintEquip = new TextBox
            {
                Text = "Smart TV 65 นิ้ว",
                Width = 150,
                Margin = new Padding(4, 6, 10, 4)
            };
            pnlTop.Controls.Add(_txtMaintEquip);

            pnlTop.Controls.Add(new Label { Text = "อาการชำรุด:", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _txtMaintTitle = new TextBox
            {
                Text = "หน้าจอเปิดไม่ติด / สาย HDMI ชำรุด",
                Width = 240,
                Margin = new Padding(4, 6, 10, 4)
            };
            pnlTop.Controls.Add(_txtMaintTitle);

            var btnCreateMaint = CreateStyledButton("🛠️ บันทึกแจ้งซ่อมใหม่", Color.FromArgb(5, 150, 105), 165, 34);
            btnCreateMaint.Click += async (s, e) => await CreateMaintenanceAsync();
            pnlTop.Controls.Add(btnCreateMaint);

            var pnlBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                MinimumSize = new Size(0, 56),
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };
            pnlBottom.Controls.Add(new Label
            {
                Text = "อัปเดตสถานะงานซ่อมที่เลือก:",
                AutoSize = true,
                Margin = new Padding(4, 10, 6, 0)
            });

            _cboMaintStatusUpdate = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 160,
                Margin = new Padding(4, 6, 10, 4)
            };
            _cboMaintStatusUpdate.Items.AddRange(new object[] { "Open", "InProgress", "Resolved" });
            _cboMaintStatusUpdate.SelectedIndex = 2;
            pnlBottom.Controls.Add(_cboMaintStatusUpdate);

            var btnUpdateMaint = CreateStyledButton("💾 บันทึกสถานะงานซ่อม", Color.FromArgb(37, 99, 235), 175, 34);
            btnUpdateMaint.Click += async (s, e) => await UpdateSelectedMaintenanceAsync();
            pnlBottom.Controls.Add(btnUpdateMaint);

            ConfigureGrid(_dgvMaintenance, _maintenanceBinding);
            tabMaintenance.Controls.Add(_dgvMaintenance);
            tabMaintenance.Controls.Add(pnlBottom);
            tabMaintenance.Controls.Add(pnlTop);
        }

        private static Panel CreateKpiCard(string title, out Label valueLabel, Color accentColor)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(5),
                Padding = new Padding(10)
            };
            var bar = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accentColor };
            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(14, 10)
            };
            valueLabel = new Label
            {
                Text = "-",
                Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize = true,
                Location = new Point(14, 34)
            };
            card.Controls.Add(valueLabel);
            card.Controls.Add(lblTitle);
            card.Controls.Add(bar);
            return card;
        }

        private static void ConfigureGrid(DataGridView dgv, BindingSource source)
        {
            dgv.Dock = DockStyle.Fill;
            dgv.DataSource = source;
            dgv.ReadOnly = true;
            dgv.AllowUserToAddRows = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.RowHeadersVisible = false;
        }

        // ==================== DATA LOADING & ACTIONS (พร้อม try-catch ดักจับ Error ทุกจุด) ====================

        private async void btnRefresh_Click(object? sender, EventArgs e)
        {
            await LoadAllDataAsync();
        }

        private void btnOpenWeb_Click(object? sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _api.BaseUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("ไม่สามารถเปิดเบราว์เซอร์ได้: " + ex.Message, "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        [DebuggerStepThrough]
        private async Task EnsureWebServerUpdatedAndRunningAsync()
        {
            bool needsRestart = false;
            try
            {
                using var checkHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                using var resp = await checkHttp.GetAsync(_api.BaseUrl);
                if (!resp.IsSuccessStatusCode)
                {
                    needsRestart = true;
                }
                else
                {
                    var html = await resp.Content.ReadAsStringAsync();
                    var testRooms = await _api.GetRoomsAsync();

                    // ตรวจสอบว่ารหัสห้องอัปเดตเป็น 601 (ห้องใหญ่) และ 602, 603, 604 (ห้องกลาง) เรียบร้อยแล้วหรือยัง
                    if (!html.Contains("เข้าสู่ระบบจองห้องสมุด") ||
                        testRooms.Count != 4 ||
                        !testRooms.Any(r => r.RoomCode == "601") ||
                        !testRooms.Any(r => r.RoomCode == "602"))
                    {
                        needsRestart = true;
                    }
                }
            }
            catch
            {
                needsRestart = true;
            }

            if (!needsRestart) return;

            lblStatus.Text = "🔄 กำลังรีสตาร์ทและอัปเดต Web Server (พอร์ต 5180) เชื่อมต่อฐานข้อมูล MySQL (ห้อง 601, 602, 603, 604)...";
            await Task.Run(() =>
            {
                try
                {
                    var psiKill = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = "-NoProfile -Command \"Get-NetTCPConnection -LocalPort 5180 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }; Get-Process -Name 'RoomReservation.Web' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var pKill = Process.Start(psiKill))
                    {
                        pKill?.WaitForExit(4000);
                    }

                    Thread.Sleep(600);

                    string webProjPath = @"D:\projectapp\LibraryAdminApp\RoomReservation.Web\RoomReservation.Web.csproj";
                    if (File.Exists(webProjPath))
                    {
                        var psiRun = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"run --project \"{webProjPath}\" --urls http://localhost:5180",
                            WorkingDirectory = @"D:\projectapp\LibraryAdminApp\RoomReservation.Web",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psiRun);
                    }
                }
                catch
                {
                    // Ignore process management errors
                }
            });

            for (int i = 0; i < 15; i++)
            {
                await Task.Delay(1000);
                try
                {
                    var checkRooms = await _api.GetRoomsAsync();
                    if (checkRooms.Count == 4 && checkRooms.Any(r => r.RoomCode == "601"))
                        break;
                }
                catch
                {
                    // รอจนกว่าเซิร์ฟเวอร์จะบูตเสร็จ
                }
            }
        }

        private async Task LoadAllDataAsync()
        {
            try
            {
                lblStatus.Text = "กำลังซิงก์ข้อมูลกับ ASP.NET Core Server & MySQL (http://localhost:5180)...";
                btnRefresh.Enabled = false;

                await EnsureWebServerUpdatedAndRunningAsync();

                _rooms = await _api.GetRoomsAsync();
                PopulateRoomSelectors();

                await LoadBookingsAsync(showNotFoundAlert: false);

                var dash = await _api.GetDashboardAsync();
                if (dash != null)
                {
                    _lblKpiUtilization.Text = $"{dash.OverallUtilizationPercent}%";
                    _lblKpiActiveBookings.Text = dash.ActiveBookingsToday.ToString();
                    _lblKpiCheckedIn.Text = dash.CheckedInNowCount.ToString();
                    _lblKpiAutoReleased.Text = dash.AutoReleasedTodayCount.ToString();
                    _lblKpiPenalties.Text = dash.ActivePenaltiesCount.ToString();
                    _lblKpiMaintenance.Text = dash.OpenMaintenanceCount.ToString();

                    _utilizationBinding.DataSource = dash.RoomUtilizations;
                    _notificationsBinding.DataSource = dash.RecentNotifications;
                }

                _penaltiesBinding.DataSource = await _api.GetPenaltiesAsync();
                _maintenanceBinding.DataSource = await _api.GetMaintenanceReportsAsync();

                lblStatus.Text = $"เชื่อมต่อสำเร็จ — ระบบมี {_rooms.Count} ห้อง (ห้องใหญ่ 601 / ห้องกลาง 602, 603, 604), รายการจอง {_currentBookings.Count} รายการ (อัปเดตล่าสุด {DateTime.Now:HH:mm:ss})";
            }
            catch
            {
                lblStatus.Text = "⚠️ ไม่สามารถดึงข้อมูลจากฐานข้อมูลได้ กรุณาตรวจสอบการรัน WAMP MySQL และ Web Server พอร์ต 5180";
                MessageBox.Show("ไม่พบข้อมูล", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        private void PopulateRoomSelectors()
        {
            try
            {
                if (_rooms.Count == 0) return;

                if (_cboMaintRoom.Items.Count != _rooms.Count)
                {
                    _cboMaintRoom.DataSource = _rooms.ToList();
                    _cboMaintRoom.DisplayMember = "RoomName";
                    _cboMaintRoom.ValueMember = "Id";
                }
            }
            catch
            {
                // Ignore binding errors
            }
        }

        private async Task LoadBookingsAsync(bool showNotFoundAlert = false)
        {
            try
            {
                var status = _cboFilterStatus.SelectedItem?.ToString();
                if (status == "ทั้งหมด") status = null;

                var studentFilter = _txtFilterStudentId.Text.Trim();
                _currentBookings = await _api.GetBookingsAsync(status, studentFilter);
                _bookingsBinding.DataSource = _currentBookings;

                if (showNotFoundAlert && _currentBookings.Count == 0)
                {
                    MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch
            {
                MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void btnUpdateStatus_Click(object? sender, EventArgs e)
        {
            try
            {
                if (dgvBookings.CurrentRow?.DataBoundItem is not BookingDto selected)
                {
                    MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!Enum.TryParse<BookingStatus>(cboStatus.SelectedItem?.ToString(), true, out var newStatus))
                    return;

                var res = await _api.UpdateBookingStatusAsync(selected.Id, newStatus);
                if (!res.Success)
                {
                    MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                lblStatus.Text = res.Message;
                await LoadAllDataAsync();
            }
            catch
            {
                MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task ExecuteEmergencyOverrideAsync()
        {
            try
            {
                if (dgvBookings.CurrentRow?.DataBoundItem is not BookingDto selected)
                {
                    MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var res = await _api.EmergencyOverrideAsync(new EmergencyOverrideRequest
                {
                    BookingId = selected.Id,
                    AdminId = "ADMIN-ROOM",
                    Reason = _txtOverrideReason.Text.Trim()
                });

                if (!res.Success)
                {
                    MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(res.Message, "Emergency Override", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadAllDataAsync();
            }
            catch
            {
                MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task VerifyGroupMemberFromKioskAsync()
        {
            try
            {
                var studentId = _txtVerifyMemberId.Text.Trim();
                if (string.IsNullOrWhiteSpace(studentId))
                {
                    MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var matchedBookings = await _api.GetBookingsAsync(null, studentId);
                var target = matchedBookings.FirstOrDefault(b => b.Status == BookingStatus.Reserved)
                             ?? matchedBookings.FirstOrDefault();

                if (target == null)
                {
                    MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _txtCheckInPinOrQr.Text = target.CheckInPinCode;
                MessageBox.Show(
                    $"พบรายการจองของ {target.HostFullName} ({target.HostStudentOrStaffId})\n" +
                    $"• รหัสการจอง: {target.BookingNo}\n" +
                    $"• ห้อง: {target.RoomCode} ({target.RoomName})\n" +
                    $"• เวลา: {target.StartTime:dd/MM/yyyy HH:mm} – {target.EndTime:HH:mm} น.\n" +
                    $"• รหัส PIN: {target.CheckInPinCode}",
                    "พบข้อมูลการจอง",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task PerformKioskCheckInAsync(CheckInMethod method)
        {
            try
            {
                string pinInput = _txtCheckInPinOrQr.Text.Trim();
                string studentInput = _txtVerifyMemberId.Text.Trim();

                BookingDto? target = null;

                if (method == CheckInMethod.PinCode)
                {
                    if (string.IsNullOrWhiteSpace(pinInput))
                    {
                        MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // ค้นหารายการจองจาก PIN หรือรหัสการจอง
                    target = _currentBookings.FirstOrDefault(b =>
                        b.CheckInPinCode.Equals(pinInput, StringComparison.OrdinalIgnoreCase) ||
                        b.BookingNo.Equals(pinInput, StringComparison.OrdinalIgnoreCase));

                    if (target == null)
                    {
                        MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                else
                {
                    target = (dgvBookings.CurrentRow?.DataBoundItem as BookingDto)
                             ?? _currentBookings.FirstOrDefault(b =>
                                 !string.IsNullOrWhiteSpace(pinInput) &&
                                 (b.CheckInPinCode.Equals(pinInput, StringComparison.OrdinalIgnoreCase) ||
                                  b.BookingNo.Equals(pinInput, StringComparison.OrdinalIgnoreCase)))
                             ?? _currentBookings.FirstOrDefault(b =>
                                 !string.IsNullOrWhiteSpace(studentInput) &&
                                 b.HostStudentOrStaffId.Equals(studentInput, StringComparison.OrdinalIgnoreCase) &&
                                 b.Status == BookingStatus.Reserved);

                    if (target == null)
                    {
                        MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                var res = await _api.CheckInAsync(new CheckInRequest
                {
                    BookingId = target.Id,
                    StudentOrStaffId = target.HostStudentOrStaffId,
                    Method = method,
                    CodeOrQrToken = pinInput,
                    ClientIpAddress = _chkInsideBuildingWifi.Checked ? "10.20.1.55" : "49.230.10.9",
                    ClientLatitude = _chkInsideBuildingWifi.Checked ? 13.8476 : 13.9200,
                    ClientLongitude = _chkInsideBuildingWifi.Checked ? 100.5696 : 100.6400
                });

                if (!res.Success)
                {
                    MessageBox.Show(
                        string.IsNullOrWhiteSpace(res.Message) ? "ไม่พบรายการ" : res.Message,
                        "แจ้งเตือน",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(res.Message, "เช็คอิน & เปิดประตูห้องสำเร็จ",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadAllDataAsync();
            }
            catch
            {
                MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task TriggerAutoReleaseAsync()
        {
            try
            {
                var res = await _api.TriggerAutoReleaseAsync();
                if (!res.Success)
                {
                    MessageBox.Show("ไม่พบข้อมูล", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(res.Message, "Auto-Release & No-Show Scheduler",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadAllDataAsync();
            }
            catch
            {
                MessageBox.Show("ไม่พบข้อมูล", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task LookupStudentQuotaAsync()
        {
            try
            {
                var sid = _txtQuotaLookupStudentId.Text.Trim();
                if (string.IsNullOrWhiteSpace(sid))
                {
                    MessageBox.Show("ไม่พบข้อมูล", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var q = await _api.GetUserQuotaAsync(sid);
                if (q == null || q.UserId == Guid.Empty)
                {
                    _lblQuotaLookupResult.ForeColor = Color.FromArgb(225, 29, 72);
                    _lblQuotaLookupResult.Text = "⚠️ ไม่พบข้อมูลผู้ใช้งานในระบบ";
                    MessageBox.Show("ไม่พบข้อมูล", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (q.IsSuspended)
                {
                    _lblQuotaLookupResult.ForeColor = Color.FromArgb(225, 29, 72);
                    _lblQuotaLookupResult.Text = $"⛔ ถูกระงับสิทธิ์ถึง {q.SuspendedUntil:dd/MM/yyyy HH:mm} ({q.SuspensionReason})";
                }
                else
                {
                    _lblQuotaLookupResult.ForeColor = Color.FromArgb(5, 150, 105);
                    _lblQuotaLookupResult.Text = $"✅ {q.FullName} — โควตาคงเหลือวันนี้: {q.RemainingMinutesToday} นาที (ใช้ไป {q.UsedMinutesToday}/{q.DailyQuotaMinutes} นาที)";
                }
            }
            catch
            {
                MessageBox.Show("ไม่พบข้อมูล", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task PardonSelectedPenaltyAsync()
        {
            try
            {
                if (_dgvPenalties.CurrentRow?.DataBoundItem is not PenaltyDto selected)
                {
                    MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var res = await _api.PardonPenaltyAsync(selected.Id);
                if (!res.Success)
                {
                    MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(res.Message, "ปลดแบน", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadAllDataAsync();
            }
            catch
            {
                MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task CreateMaintenanceAsync()
        {
            try
            {
                if (_cboMaintRoom.SelectedValue is not Guid roomId)
                {
                    MessageBox.Show("ไม่พบข้อมูล", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var res = await _api.CreateMaintenanceReportAsync(new CreateMaintenanceReportRequest
                {
                    RoomId = roomId,
                    EquipmentName = _txtMaintEquip.Text.Trim(),
                    ReportedBy = "เจ้าหน้าที่ห้อง Admin",
                    Title = _txtMaintTitle.Text.Trim(),
                    Description = _txtMaintTitle.Text.Trim()
                });

                if (!res.Success)
                {
                    MessageBox.Show(res.Message, "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(res.Message, "แจ้งซ่อมอุปกรณ์", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadAllDataAsync();
            }
            catch
            {
                MessageBox.Show("ไม่พบข้อมูล", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task UpdateSelectedMaintenanceAsync()
        {
            try
            {
                if (_dgvMaintenance.CurrentRow?.DataBoundItem is not MaintenanceReportDto selected)
                {
                    MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (Enum.TryParse<MaintenanceStatus>(_cboMaintStatusUpdate.SelectedItem?.ToString(), true, out var status))
                {
                    var res = await _api.UpdateMaintenanceStatusAsync(selected.Id, status);
                    if (!res.Success)
                    {
                        MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    lblStatus.Text = res.Message;
                    await LoadAllDataAsync();
                }
            }
            catch
            {
                MessageBox.Show("ไม่พบรายการ", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}