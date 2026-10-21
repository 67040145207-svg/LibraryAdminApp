namespace LibraryAdminApp
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            pnlHeader = new Panel();
            lblAppTitle = new Label();
            lblAppSubtitle = new Label();
            btnOpenWeb = new Button();
            btnRefresh = new Button();
            lblStatus = new Label();
            tabMain = new TabControl();
            tabDashboard = new TabPage();
            tabBookings = new TabPage();
            tabKiosk = new TabPage();
            tabPenalties = new TabPage();
            tabMaintenance = new TabPage();
            dgvBookings = new DataGridView();
            cboStatus = new ComboBox();
            btnUpdateStatus = new Button();
            ((System.ComponentModel.ISupportInitialize)dgvBookings).BeginInit();
            SuspendLayout();

            // 
            // pnlHeader (ใช้ TableLayoutPanel ด้านในเพื่อไม่ให้ปุ่มและข้อความทับกันทุกขนาดหน้าจอ/DPI)
            // 
            pnlHeader.BackColor = Color.FromArgb(6, 78, 59);
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 82;
            pnlHeader.Padding = new Padding(16, 10, 16, 10);

            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));

            lblAppTitle.AutoSize = true;
            lblAppTitle.Dock = DockStyle.Fill;
            lblAppTitle.Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
            lblAppTitle.ForeColor = Color.White;
            lblAppTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblAppTitle.Text = "🏢 ระบบบริหารจัดการห้องสมุด — สำหรับเจ้าหน้าที่ห้อง Admin เท่านั้น";

            lblAppSubtitle.AutoSize = true;
            lblAppSubtitle.Dock = DockStyle.Fill;
            lblAppSubtitle.Font = new Font("Segoe UI", 9F);
            lblAppSubtitle.ForeColor = Color.FromArgb(167, 243, 208);
            lblAppSubtitle.TextAlign = ContentAlignment.MiddleLeft;
            lblAppSubtitle.Text = "เวลาทำการ 09:00–17:00 น. • ห้องใหญ่ 601 (ขั้นต่ำ 7 คน) / ห้องกลาง 602, 603, 604 (ขั้นต่ำ 5 คน) • รับเช็คอินล่วงหน้า 30 นาที–1 ชม.";

            var headerButtonsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 6, 0, 0)
            };

            btnRefresh.AutoSize = true;
            btnRefresh.MinimumSize = new Size(130, 40);
            btnRefresh.Padding = new Padding(10, 4, 10, 4);
            btnRefresh.BackColor = Color.FromArgb(16, 185, 129);
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnRefresh.ForeColor = Color.White;
            btnRefresh.Margin = new Padding(8, 0, 0, 0);
            btnRefresh.Text = "🔄 รีเฟรชข้อมูล";
            btnRefresh.Click += btnRefresh_Click;

            btnOpenWeb.AutoSize = true;
            btnOpenWeb.MinimumSize = new Size(170, 40);
            btnOpenWeb.Padding = new Padding(10, 4, 10, 4);
            btnOpenWeb.BackColor = Color.FromArgb(15, 118, 110);
            btnOpenWeb.FlatStyle = FlatStyle.Flat;
            btnOpenWeb.FlatAppearance.BorderSize = 0;
            btnOpenWeb.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            btnOpenWeb.ForeColor = Color.White;
            btnOpenWeb.Margin = new Padding(0);
            btnOpenWeb.Text = "🌐 เปิดเว็บจองห้อง (User)";
            btnOpenWeb.Click += btnOpenWeb_Click;

            headerButtonsFlow.Controls.Add(btnRefresh);
            headerButtonsFlow.Controls.Add(btnOpenWeb);

            headerLayout.Controls.Add(lblAppTitle, 0, 0);
            headerLayout.Controls.Add(lblAppSubtitle, 0, 1);
            headerLayout.Controls.Add(headerButtonsFlow, 1, 0);
            headerLayout.SetRowSpan(headerButtonsFlow, 2);

            pnlHeader.Controls.Add(headerLayout);

            // 
            // tabMain
            // 
            tabMain.Dock = DockStyle.Fill;
            tabMain.Font = new Font("Segoe UI", 9.75F);
            tabMain.Padding = new Point(14, 6);

            tabDashboard.Text = "📊 1. Dashboard & สถิติ (4 ห้อง)";
            tabDashboard.BackColor = Color.FromArgb(248, 250, 252);

            tabBookings.Text = "🗓️ 2. จัดการจอง & เปิดประตูห้อง";
            tabBookings.BackColor = Color.FromArgb(248, 250, 252);

            tabKiosk.Text = "🔑 3. จุดเช็คอินที่ห้อง Admin";
            tabKiosk.BackColor = Color.FromArgb(248, 250, 252);

            tabPenalties.Text = "⚖️ 4. โทษแบน No-Show (2 วัน)";
            tabPenalties.BackColor = Color.FromArgb(248, 250, 252);

            tabMaintenance.Text = "🛠️ 5. แจ้งซ่อมอุปกรณ์ห้อง";
            tabMaintenance.BackColor = Color.FromArgb(248, 250, 252);

            tabMain.Controls.Add(tabDashboard);
            tabMain.Controls.Add(tabBookings);
            tabMain.Controls.Add(tabKiosk);
            tabMain.Controls.Add(tabPenalties);
            tabMain.Controls.Add(tabMaintenance);

            // 
            // Status Footer
            // 
            lblStatus.Dock = DockStyle.Bottom;
            lblStatus.Height = 32;
            lblStatus.BackColor = Color.FromArgb(241, 245, 249);
            lblStatus.ForeColor = Color.FromArgb(30, 41, 59);
            lblStatus.Font = new Font("Segoe UI", 9.5F);
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Padding = new Padding(12, 0, 0, 0);
            lblStatus.Text = "พร้อมทำงาน (เฉพาะเจ้าหน้าที่ห้อง Admin)";

            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1280, 780);
            MinimumSize = new Size(1100, 700);
            Controls.Add(tabMain);
            Controls.Add(lblStatus);
            Controls.Add(pnlHeader);
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "ระบบบริหารจัดการห้องสมุด — เฉพาะเจ้าหน้าที่ห้อง Admin (Visual Studio Admin Console)";
            ((System.ComponentModel.ISupportInitialize)dgvBookings).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel pnlHeader;
        private Label lblAppTitle;
        private Label lblAppSubtitle;
        private Button btnOpenWeb;
        private Button btnRefresh;
        private Label lblStatus;
        private TabControl tabMain;
        private TabPage tabDashboard;
        private TabPage tabBookings;
        private TabPage tabKiosk;
        private TabPage tabPenalties;
        private TabPage tabMaintenance;
        private DataGridView dgvBookings;
        private ComboBox cboStatus;
        private Button btnUpdateStatus;
    }
}

