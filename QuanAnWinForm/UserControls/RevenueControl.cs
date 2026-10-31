using System.Diagnostics;
using System.Globalization;
using QuanAnWinForm.Models;
using QuanAnWinForm.Services;
using QuanAnWinForm.Theme;

namespace QuanAnWinForm.UserControls;

// Man hinh "Thong ke & Bao cao" - QuanLy, ThuNgan.
// Bo loc: Hom nay / Chon ngay / Theo tuan / Theo thang / Theo quy (+ nut lui/tien ky).
// Co nut "Xuat Excel" xuat toan bo bao cao cua ky dang xem.
public partial class RevenueControl : UserControl, IRefreshableContent
{
    public Task RefreshDataAsync() => ReloadAllAsync();


    private const int PeriodToday = 0;
    private const int PeriodDay = 1;
    private const int PeriodWeek = 2;
    private const int PeriodMonth = 3;
    private const int PeriodQuarter = 4;

    private readonly ApiService _api = new();
    private DateTime _from = DateTime.Today;
    private DateTime _to = DateTime.Today;
    private string _periodLabel = "";
    private bool _suppress;   // true khi dang set gia tri bo loc bang code -> khong reload
    private int _version;     // chong ket qua cu ghi de ket qua moi khi bam loc nhanh

    // O chon "den ngay" + 2 nhan "Tu"/"den" cho kieu loc khoang ngay (tao bang code, khong dung Designer).
    private readonly DateTimePicker dtpDateTo = new();
    private readonly Panel _filterRow = new() { Dock = DockStyle.Fill };
    private readonly Label lblFrom = new();
    private readonly Label lblTo = new();

    private readonly Label[] _insightCaptions = new Label[4];
    private readonly Label[] _insightValues = new Label[4];

    private static readonly string[] InvoiceStatusLabels = { "Chưa thanh toán", "Đã thanh toán", "Đã hủy" };
    private static readonly string[] Roman = { "I", "II", "III", "IV" };

    public RevenueControl()
    {
        InitializeComponent();
        ArrangeFilterBar();
        BuildInsightRows();
        SetupGridColumns();
        ApplyTheme();
        InitPickers();
        ApplyModeUI();
        WireUpEvents();

        Load += async (s, e) =>
        {
            PositionHeaderButtons();
            LayoutFilterRow();
            await ReloadAllAsync();
        };
    }

    // ------------------------------------------------------------------
    // KHOI TAO
    // ------------------------------------------------------------------

    // Sap lai thanh loc tu dau (bo qua thu tu Designer): Xem theo | kieu loc | o chon | nam | nhan ky.
    private void ArrangeFilterBar()
    {
        lblFilterCaption.Width = 100; // tranh bi cat chu "Xem theo:"

        // Kieu loc "Chon ngay" nay cho chon ca khoang ngay (tu ngay - den ngay).
        cbPeriod.Items[PeriodDay] = "Khoảng ngày";

        lblFrom.Text = "Từ";
        lblTo.Text = "đến";
        foreach (var l in new[] { lblFrom, lblTo })
        {
            l.AutoSize = false;
            l.Font = new Font("Segoe UI", 10F);
            l.Margin = new Padding(4, 0, 4, 0);
            l.TextAlign = ContentAlignment.MiddleCenter;

            // Do theo chu that (tinh den DPI man hinh) thay vi gan cung, tranh bi cat chu / de len o ngay.
            var textWidth = TextRenderer.MeasureText(l.Text, l.Font).Width;
            l.Size = new Size(textWidth + 10, dtpDate.Height);
        }

        dtpDateTo.CustomFormat = dtpDate.CustomFormat;
        dtpDateTo.Format = DateTimePickerFormat.Custom;
        dtpDateTo.Font = dtpDate.Font;
        dtpDateTo.Margin = dtpDate.Margin;
        dtpDateTo.Size = dtpDate.Size;

        // Bo FlowLayoutPanel (thu tu bi phu thuoc vao Designer) -> dung 1 Panel va tu dat toa do
        // cho tung o trong LayoutFilterRow(): o NAM luon dung SAU o tuan / thang / quy.
        flowFilter.Controls.Clear();
        pnlFilter.Controls.Remove(flowFilter);
        _filterRow.Controls.AddRange(new Control[]
        {
            lblFilterCaption, cbPeriod,
            lblFrom, dtpDate, lblTo, dtpDateTo,
            cbWeek, cbMonth, cbQuarter,
            nudYear,
            lblRange
        });
        pnlFilter.Controls.Add(_filterRow);
        LayoutFilterRow();
    }

    private void InitPickers()
    {
        _suppress = true;
        var today = DateTime.Today;
        dtpDate.Value = today;
        dtpDateTo.Value = today;
        cbMonth.SelectedIndex = today.Month - 1;
        cbQuarter.SelectedIndex = (today.Month - 1) / 3;
        nudYear.Value = today.Year;
        PopulateWeeks(ISOWeek.GetYear(today) == today.Year ? ISOWeek.GetWeekOfYear(today) : 1);
        cbPeriod.SelectedIndex = PeriodToday;
        _suppress = false;
    }

    // Xep cac o cua thanh loc tu trai sang phai theo kieu loc dang chon:
    //   Xem theo | kieu loc | [Tu - den] HOAC [tuan | thang | quy] + NAM | nhan ky
    private void LayoutFilterRow()
    {
        var m = cbPeriod.SelectedIndex;
        var order = new List<Control> { lblFilterCaption, cbPeriod };
        switch (m)
        {
            case PeriodDay:
                order.AddRange(new Control[] { lblFrom, dtpDate, lblTo, dtpDateTo });
                break;
            case PeriodWeek:
                order.AddRange(new Control[] { cbWeek, nudYear });
                break;
            case PeriodMonth:
                order.AddRange(new Control[] { cbMonth, nudYear });
                break;
            case PeriodQuarter:
                order.AddRange(new Control[] { cbQuarter, nudYear });
                break;
        }
        order.Add(lblRange);

        var x = 14;
        foreach (var c in order)
        {
            x += c.Margin.Left;
            c.Location = new Point(x, 11);
            x += c.Width + c.Margin.Right;
        }
    }

    // Gan ngay cho 1 o chon bang code ma khong kich hoat tai du lieu.
    private void SetDateSilently(DateTimePicker picker, DateTime value)
    {
        var wasSuppressed = _suppress;
        _suppress = true;
        try { picker.Value = value; }
        finally { _suppress = wasSuppressed; }
    }

    private void WireUpEvents()
    {
        // Vien cho cac the trang (lambda khong duoc dat trong .Designer.cs).
        foreach (var ctl in new Control[] { pnlFilter, panelChartCard, panelInvoicesCard, pnlInsightsCard, pnlTop5Card })
        {
            var panel = ctl;
            panel.Paint += (s, e) =>
            {
                using var pen = new Pen(AppColors.OutlineVariant);
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };
            panel.Resize += (s, e) => panel.Invalidate();
        }

        pnlHeader.Resize += (s, e) => PositionHeaderButtons();

        cbPeriod.SelectedIndexChanged += async (s, e) =>
        {
            ApplyModeUI();
            if (cbPeriod.SelectedIndex == PeriodWeek) PopulateWeeks();
            await ReloadAllAsync();
        };
        // Khoang ngay: giu "Tu ngay" <= "Den ngay" (keo ngay con lai theo, khong bao loi).
        dtpDate.ValueChanged += async (s, e) =>
        {
            if (!_suppress && dtpDate.Value.Date > dtpDateTo.Value.Date)
                SetDateSilently(dtpDateTo, dtpDate.Value);
            await ReloadAllAsync();
        };
        dtpDateTo.ValueChanged += async (s, e) =>
        {
            if (!_suppress && dtpDateTo.Value.Date < dtpDate.Value.Date)
                SetDateSilently(dtpDate, dtpDateTo.Value);
            await ReloadAllAsync();
        };
        cbWeek.SelectedIndexChanged += async (s, e) => await ReloadAllAsync();
        cbMonth.SelectedIndexChanged += async (s, e) => await ReloadAllAsync();
        cbQuarter.SelectedIndexChanged += async (s, e) => await ReloadAllAsync();
        nudYear.ValueChanged += async (s, e) =>
        {
            if (cbPeriod.SelectedIndex == PeriodWeek) PopulateWeeks();
            await ReloadAllAsync();
        };
        btnRefresh.Click += async (s, e) => await ReloadAllAsync();
        btnExport.Click += async (s, e) => await ExportExcelAsync();

        // To mau trang thai hoa don.
        gridInvoices.CellFormatting += (s, e) =>
        {
            if (e.ColumnIndex != 4 || e.Value is not string text) return;
            e.CellStyle.ForeColor = text switch
            {
                "Đã thanh toán" => AppColors.Success,
                "Đã hủy" => AppColors.Error,
                _ => AppColors.Warning,
            };
        };
    }

    // Dat 2 nut Xuat Excel / Lam moi sat mep phai cua dong tieu de.
    private void PositionHeaderButtons()
    {
        var right = pnlHeader.ClientSize.Width;
        btnRefresh.Location = new Point(right - btnRefresh.Width, 16);
        btnExport.Location = new Point(btnRefresh.Left - btnExport.Width - 10, 16);
    }

    private void BuildInsightRows()
    {
        tlInsights.RowStyles.Clear();
        tlInsights.RowCount = 4;
        for (var i = 0; i < 4; i++)
        {
            tlInsights.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

            var caption = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = AppColors.OnSurfaceVariant,
                Text = "—",
                TextAlign = ContentAlignment.BottomLeft,
            };
            var value = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = AppColors.OnSurface,
                Text = "—",
                TextAlign = ContentAlignment.TopLeft,
            };
            var box = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            box.Controls.Add(value);   // Fill them truoc
            box.Controls.Add(caption); // roi Top

            _insightCaptions[i] = caption;
            _insightValues[i] = value;
            tlInsights.Controls.Add(box, 0, i);
        }
    }

    private void SetupGridColumns()
    {
        // Hoa don gan day
        AddCol(gridInvoices, "colInvId", "Mã hóa đơn", 22);
        AddCol(gridInvoices, "colInvTable", "Bàn / Kênh", 22);
        AddCol(gridInvoices, "colInvTime", "Thời gian", 22);
        AddCol(gridInvoices, "colInvAmount", "Số tiền", 20, right: true);
        AddCol(gridInvoices, "colInvStatus", "Trạng thái", 22);

        // Top 5 mon (the ben phai)
        AddCol(gridTop5, "colTop5Rank", "#", 14);
        AddCol(gridTop5, "colTop5Name", "Món", 66);
        AddCol(gridTop5, "colTop5Qty", "SL", 20, right: true);

        // Mon ban chay
        AddCol(gridTopItems, "colRank", "Hạng", 10);
        AddCol(gridTopItems, "colItemName", "Tên món", 45);
        AddCol(gridTopItems, "colQty", "Số lượng bán", 20, right: true);
        AddCol(gridTopItems, "colItemRevenue", "Doanh thu", 25, right: true);

        // Theo nhan vien
        AddCol(gridByEmployee, "colEmpName", "Nhân viên", 35);
        AddCol(gridByEmployee, "colEmpInvoiceCount", "Số hóa đơn", 20, right: true);
        AddCol(gridByEmployee, "colEmpRevenue", "Doanh thu", 25, right: true);
        AddCol(gridByEmployee, "colEmpAvg", "Giá trị TB/hóa đơn", 25, right: true);

        // Khuyen mai
        AddCol(gridCampaigns, "colCampaignName", "Chiến dịch", 50);
        AddCol(gridCampaigns, "colCampaignInvoiceCount", "Số hóa đơn áp dụng", 25, right: true);
        AddCol(gridCampaigns, "colCampaignDiscount", "Tổng tiền đã giảm", 25, right: true);
    }

    private static void AddCol(DataGridView grid, string name, string header, float weight, bool right = false)
    {
        var col = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        if (right)
        {
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        grid.Columns.Add(col);
    }

    // ------------------------------------------------------------------
    // GIAO DIEN
    // ------------------------------------------------------------------

    private void ApplyTheme()
    {
        BackColor = AppColors.Background;
        lblTitle.ForeColor = AppColors.OnSurface;
        lblSubtitle.ForeColor = AppColors.OnSurfaceVariant;
        lblFilterCaption.ForeColor = AppColors.OnSurfaceVariant;
        lblFrom.ForeColor = AppColors.OnSurfaceVariant;
        lblTo.ForeColor = AppColors.OnSurfaceVariant;
        lblRange.ForeColor = AppColors.Primary;
        foreach (var l in new[] { lblChartTitle, lblInvoicesTitle, lblInsightsTitle, lblTop5Title })
            l.ForeColor = AppColors.OnSurface;

        // Nut chinh: Xuat Excel
        btnExport.FlatStyle = FlatStyle.Flat;
        btnExport.FlatAppearance.BorderSize = 0;
        btnExport.BackColor = AppColors.Primary;
        btnExport.ForeColor = Color.White;
        btnExport.Cursor = Cursors.Hand;

        // Nut phu
        foreach (var b in new[] { btnRefresh })
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = AppColors.OutlineVariant;
            b.BackColor = Color.White;
            b.ForeColor = AppColors.OnSurface;
            b.Cursor = Cursors.Hand;
        }

        StyleGrid(gridInvoices, border: false);
        StyleGrid(gridTop5, border: false);
        StyleGrid(gridTopItems, border: true);
        StyleGrid(gridByEmployee, border: true);
        StyleGrid(gridCampaigns, border: true);
    }

    private static void StyleGrid(DataGridView g, bool border)
    {
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.AllowUserToResizeRows = false;
        g.ReadOnly = true;
        g.RowHeadersVisible = false;
        g.MultiSelect = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.BackgroundColor = Color.White;
        g.BorderStyle = border ? BorderStyle.FixedSingle : BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.GridColor = AppColors.SurfaceContainer;

        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = 38;
        var h = g.ColumnHeadersDefaultCellStyle;
        h.BackColor = AppColors.SurfaceLow;
        h.ForeColor = AppColors.OnSurfaceVariant;
        h.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        h.SelectionBackColor = AppColors.SurfaceLow;
        h.SelectionForeColor = AppColors.OnSurfaceVariant;
        h.Padding = new Padding(8, 0, 8, 0);

        g.RowTemplate.Height = 36;
        var selectBg = Color.FromArgb(255, 237, 231);
        var c = g.DefaultCellStyle;
        c.Font = new Font("Segoe UI", 10F);
        c.ForeColor = AppColors.OnSurface;
        c.BackColor = Color.White;
        c.SelectionBackColor = selectBg;
        c.SelectionForeColor = AppColors.OnSurface;
        c.Padding = new Padding(8, 0, 8, 0);

        var a = g.AlternatingRowsDefaultCellStyle;
        a.BackColor = Color.FromArgb(250, 250, 250);
        a.SelectionBackColor = selectBg;
        a.SelectionForeColor = AppColors.OnSurface;
    }

    // Hien/an cac o chon theo kieu loc dang chon.
    private void ApplyModeUI()
    {
        var m = cbPeriod.SelectedIndex;
        lblFrom.Visible = m == PeriodDay;
        dtpDate.Visible = m == PeriodDay;
        lblTo.Visible = m == PeriodDay;
        dtpDateTo.Visible = m == PeriodDay;
        cbWeek.Visible = m == PeriodWeek;
        cbMonth.Visible = m == PeriodMonth;
        cbQuarter.Visible = m == PeriodQuarter;
        nudYear.Visible = m == PeriodWeek || m == PeriodMonth || m == PeriodQuarter;
        LayoutFilterRow();
    }

    // Nap danh sach tuan ISO (Thu 2 - Chu nhat) cua nam dang chon vao cbWeek, giu nguyen tuan dang chon.
    private void PopulateWeeks(int? selectWeek = null)
    {
        var wasSuppressed = _suppress;
        _suppress = true;
        try
        {
            var year = (int)nudYear.Value;
            var count = ISOWeek.GetWeeksInYear(year);
            var keep = selectWeek ?? Math.Max(1, cbWeek.SelectedIndex + 1);

            cbWeek.BeginUpdate();
            cbWeek.Items.Clear();
            for (var w = 1; w <= count; w++)
            {
                var start = ISOWeek.ToDateTime(year, w, DayOfWeek.Monday);
                cbWeek.Items.Add($"Tuần {w}  ({start:dd/MM} – {start.AddDays(6):dd/MM})");
            }
            cbWeek.EndUpdate();

            cbWeek.SelectedIndex = Math.Min(keep, count) - 1;
        }
        finally
        {
            _suppress = wasSuppressed;
        }
    }

    // ------------------------------------------------------------------
    // BO LOC THOI GIAN
    // ------------------------------------------------------------------

    // Quy doi lua chon thanh khoang ngay [_from, _to] (2 dau bao gom) + nhan hien thi.
    private void ResolvePeriod()
    {
        var today = DateTime.Today;
        DateTime from, to;

        switch (cbPeriod.SelectedIndex)
        {
            case PeriodDay:
                from = dtpDate.Value.Date;
                to = dtpDateTo.Value.Date;
                if (to < from) (from, to) = (to, from);
                _periodLabel = from == to
                    ? $"Ngày {from:dd/MM/yyyy}"
                    : $"Từ {from:dd/MM/yyyy} đến {to:dd/MM/yyyy}";
                break;

            case PeriodWeek:
                {
                    var year = (int)nudYear.Value;
                    var w = Math.Clamp(cbWeek.SelectedIndex + 1, 1, ISOWeek.GetWeeksInYear(year));
                    from = ISOWeek.ToDateTime(year, w, DayOfWeek.Monday); // Thu 2 dau tuan
                    to = from.AddDays(6);
                    _periodLabel = $"Tuần {w}/{year}  ({from:dd/MM} – {to:dd/MM/yyyy})";
                    break;
                }

            case PeriodMonth:
                {
                    from = new DateTime((int)nudYear.Value, cbMonth.SelectedIndex + 1, 1);
                    to = from.AddMonths(1).AddDays(-1);
                    _periodLabel = $"Tháng {from.Month}/{from.Year}  ({from:dd/MM} – {to:dd/MM/yyyy})";
                    break;
                }

            case PeriodQuarter:
                {
                    var q = Math.Max(0, cbQuarter.SelectedIndex);
                    from = new DateTime((int)nudYear.Value, q * 3 + 1, 1);
                    to = from.AddMonths(3).AddDays(-1);
                    _periodLabel = $"Quý {Roman[q]}/{from.Year}  ({from:dd/MM} – {to:dd/MM/yyyy})";
                    break;
                }

            default: // Hom nay
                from = to = today;
                _periodLabel = $"Hôm nay  ({today:dd/MM/yyyy})";
                break;
        }

        // Ky dang dien ra: chi tinh den hom nay (khong ve cac ngay tuong lai).
        if (from <= today && to > today) to = today;

        _from = from;
        _to = to;
        lblRange.Text = _periodLabel;
    }

    // Don vi cua truc bieu do: 1 ngay -> theo gio, <= 31 ngay -> theo ngay, con lai -> theo thang
    // (khop voi cach ReportsController nhom du lieu).
    private static string UnitOf(DateTime from, DateTime to)
    {
        if (from == to) return "khung giờ";
        return (to - from).Days + 1 > 31 ? "tháng" : "ngày";
    }

    private Task<List<RevenuePointDto>> LoadPointsAsync(DateTime from, DateTime to) =>
        from == to ? _api.GetRevenueByHourAsync(from) : _api.GetRevenueByPeriodAsync(from, to);

    // ------------------------------------------------------------------
    // TAI DU LIEU
    // ------------------------------------------------------------------

    private async Task ReloadAllAsync()
    {
        if (_suppress) return;

        ResolvePeriod();
        var ver = ++_version;
        var from = _from;
        var to = _to;

        lblSubtitle.Text = from == to
            ? $"Ngày {from:dd/MM/yyyy}"
            : $"Từ {from:dd/MM/yyyy} đến {to:dd/MM/yyyy}";

        UseWaitCursor = true;
        try
        {
            var tSummary = _api.GetReportSummaryAsync(from, to);
            var tPoints = LoadPointsAsync(from, to);
            var tInvoices = _api.GetRecentInvoicesReportAsync(from, to, take: 15);
            var tItems = _api.GetTopItemsAsync(from, to, take: 10);
            var tEmployees = _api.GetRevenueByEmployeeAsync(from, to);
            var tCampaigns = _api.GetCampaignReportAsync(from, to);

            await Task.WhenAll(new Task[] { tSummary, tPoints, tInvoices, tItems, tEmployees, tCampaigns });

            // Nguoi dung da doi bo loc / roi man hinh trong luc cho -> bo ket qua cu.
            if (ver != _version || IsDisposed) return;

            ApplySummary(await tSummary ?? new RevenueSummaryDto());
            ApplyChart(await tPoints, UnitOf(from, to));
            ApplyInvoices(await tInvoices);
            var items = await tItems;
            ApplyTopItems(items);
            ApplyEmployees(await tEmployees);
            ApplyCampaigns(await tCampaigns);
        }
        catch (Exception ex)
        {
            if (ver == _version && !IsDisposed)
                MessageBox.Show($"Không tải được báo cáo: {ex.Message}");
        }
        finally
        {
            if (ver == _version && !IsDisposed) UseWaitCursor = false;
        }
    }

    private void ApplySummary(RevenueSummaryDto summary)
    {
        var trendText = summary.TrendPercent.HasValue ? $"{Math.Abs(summary.TrendPercent.Value):0.#}% so với kỳ trước" : null;
        var trendUp = summary.TrendPercent is null or >= 0;

        cardRevenue.Configure("💰", AppColors.Primary, AppColors.ErrorContainer, "DOANH THU", $"{summary.Revenue:N0}đ", trendText, trendUp);
        cardProfit.Configure("🏷️", AppColors.Warning, Color.FromArgb(255, 243, 224), "TỔNG TIỀN ĐÃ GIẢM", $"{summary.TotalDiscount:N0}đ");
        cardCost.Configure("📊", AppColors.Secondary, AppColors.SecondaryContainer, "GIÁ TRỊ TB/HÓA ĐƠN", $"{summary.AvgOrderValue:N0}đ");
        cardOrders.Configure("🧾", AppColors.Tertiary, Color.FromArgb(224, 242, 241), "SỐ HÓA ĐƠN", summary.OrderCount.ToString("N0"));
    }

    private void ApplyChart(List<RevenuePointDto> points, string unit)
    {
        lblChartTitle.Text = $"Doanh thu theo {unit}";

        chartPanel.Data = points.Select(p => (p.Label, (double)p.Revenue)).ToList();

        // To dam cot cao nhat (neu co doanh thu).
        var maxIdx = -1;
        decimal maxVal = 0;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Revenue > maxVal)
            {
                maxVal = points[i].Revenue;
                maxIdx = i;
            }
        }
        chartPanel.HighlightIndex = maxIdx;
        chartPanel.Invalidate();

        ApplyInsights(points, unit, maxIdx);
    }

    // 4 chi so nhanh o the "Diem noi bat", tinh tu chinh du lieu bieu do.
    private void ApplyInsights(List<RevenuePointDto> points, string unit, int maxIdx)
    {
        var cap = char.ToUpper(unit[0]) + unit[1..];

        _insightCaptions[0].Text = $"{cap} doanh thu cao nhất";
        _insightCaptions[1].Text = $"Doanh thu trung bình mỗi {unit}";
        _insightCaptions[2].Text = $"{cap} doanh thu thấp nhất";
        _insightCaptions[3].Text = $"Số {unit} có doanh thu";

        if (points.Count == 0 || maxIdx < 0)
        {
            _insightValues[0].Text = "Chưa có doanh thu";
            _insightValues[1].Text = "0đ";
            _insightValues[2].Text = "—";
            _insightValues[3].Text = $"0/{points.Count}";
            return;
        }

        var max = points[maxIdx];
        var min = points.OrderBy(p => p.Revenue).First();
        var avg = points.Sum(p => p.Revenue) / points.Count;
        var active = points.Count(p => p.Revenue > 0);

        _insightValues[0].Text = $"{max.Label} · {max.Revenue:N0}đ";
        _insightValues[1].Text = $"{avg:N0}đ";
        _insightValues[2].Text = $"{min.Label} · {min.Revenue:N0}đ";
        _insightValues[3].Text = $"{active}/{points.Count}";
    }

    private void ApplyInvoices(List<RecentInvoiceReportDto> invoices)
    {
        gridInvoices.Rows.Clear();
        foreach (var inv in invoices)
        {
            var statusIndex = Math.Clamp(inv.Status, 0, InvoiceStatusLabels.Length - 1);
            gridInvoices.Rows.Add(
                $"#INV-{inv.Id}",
                string.IsNullOrEmpty(inv.TableName) ? "Mang về" : inv.TableName,
                inv.InvoiceDate.ToString("HH:mm, dd/MM"),
                $"{inv.TotalAmount:N0} đ",
                InvoiceStatusLabels[statusIndex]);
        }
        if (invoices.Count == 0)
            gridInvoices.Rows.Add("", "(Chưa có hóa đơn trong kỳ này)", "", "", "");
    }

    private void ApplyTopItems(List<TopItemDto> items)
    {
        gridTopItems.Rows.Clear();
        var rank = 1;
        foreach (var item in items)
            gridTopItems.Rows.Add(rank++, item.MenuItemName, item.QuantitySold.ToString("N0"), $"{item.Revenue:N0}đ");
        if (items.Count == 0)
            gridTopItems.Rows.Add("", "(Chưa có dữ liệu trong khoảng thời gian này)", "", "");

        // Top 5 o the ben phai cua tab Doanh thu.
        gridTop5.Rows.Clear();
        rank = 1;
        foreach (var item in items.Take(5))
            gridTop5.Rows.Add(rank++, item.MenuItemName, item.QuantitySold.ToString("N0"));
        if (items.Count == 0)
            gridTop5.Rows.Add("", "(Chưa có dữ liệu)", "");
    }

    private void ApplyEmployees(List<EmployeeRevenueDto> rows)
    {
        gridByEmployee.Rows.Clear();
        foreach (var r in rows)
        {
            var avg = r.InvoiceCount > 0 ? r.Revenue / r.InvoiceCount : 0;
            gridByEmployee.Rows.Add(r.EmployeeName, r.InvoiceCount.ToString("N0"), $"{r.Revenue:N0}đ", $"{avg:N0}đ");
        }
        if (rows.Count == 0)
            gridByEmployee.Rows.Add("(Chưa có dữ liệu trong khoảng thời gian này)", "", "", "");
    }

    private void ApplyCampaigns(List<CampaignReportDto> rows)
    {
        gridCampaigns.Rows.Clear();
        foreach (var r in rows)
            gridCampaigns.Rows.Add(r.CampaignName, r.InvoiceCount.ToString("N0"), $"{r.TotalDiscount:N0}đ");
        if (rows.Count == 0)
            gridCampaigns.Rows.Add("(Chưa có chiến dịch nào được áp dụng trong khoảng thời gian này)", "", "");
    }

    // ------------------------------------------------------------------
    // XUAT EXCEL
    // ------------------------------------------------------------------

    private async Task ExportExcelAsync()
    {
        ResolvePeriod();
        var from = _from;
        var to = _to;
        var periodLabel = _periodLabel;

        using var dlg = new SaveFileDialog
        {
            Title = "Xuất báo cáo doanh thu",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx",
            AddExtension = true,
            FileName = $"BaoCaoDoanhThu_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx",
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

        var oldText = btnExport.Text;
        btnExport.Enabled = false;
        btnExport.Text = "Đang xuất...";
        try
        {
            // Lay day du du lieu cua ky (khong gioi han 10-15 dong nhu man hinh).
            var tSummary = _api.GetReportSummaryAsync(from, to);
            var tPoints = LoadPointsAsync(from, to);
            var tInvoices = _api.GetRecentInvoicesReportAsync(from, to, take: 5000);
            var tItems = _api.GetTopItemsAsync(from, to, take: 200);
            var tEmployees = _api.GetRevenueByEmployeeAsync(from, to);
            var tCampaigns = _api.GetCampaignReportAsync(from, to);
            await Task.WhenAll(new Task[] { tSummary, tPoints, tInvoices, tItems, tEmployees, tCampaigns });

            var unit = UnitOf(from, to);
            var data = new ReportExportData
            {
                From = from,
                To = to,
                PeriodLabel = periodLabel,
                ChartUnit = char.ToUpper(unit[0]) + unit[1..],
                Summary = await tSummary ?? new RevenueSummaryDto(),
                Points = await tPoints,
                Invoices = await tInvoices,
                TopItems = await tItems,
                Employees = await tEmployees,
                Campaigns = await tCampaigns,
            };

            var path = dlg.FileName;
            await Task.Run(() => ExcelExportService.Export(path, data));

            var open = MessageBox.Show(
                "Đã xuất báo cáo thành công.\nBạn có muốn mở file ngay không?",
                "Xuất Excel", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (open == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (IOException)
        {
            MessageBox.Show(
                "Không ghi được file. Nếu file đang mở trong Excel, hãy đóng lại rồi thử lại.",
                "Xuất Excel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không xuất được báo cáo: {ex.Message}", "Xuất Excel",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed)
            {
                btnExport.Enabled = true;
                btnExport.Text = oldText;
            }
        }
    }
}