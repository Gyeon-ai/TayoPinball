using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SoopPinballCollector
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    WriteCrashLog(e.Exception);
                    MessageBox.Show(e.Exception.Message, "타요의 종겜핀볼 실행 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    WriteCrashLog(e.ExceptionObject as Exception);
                };
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex);
                MessageBox.Show(ex.Message, "타요의 종겜핀볼 실행 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void WriteCrashLog(Exception ex)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(baseDir, "SooP-Pinball-Collector-error.log");
                string message = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine;
                message += ex == null ? "Unknown error" : ex.ToString();
                message += Environment.NewLine + Environment.NewLine;
                File.AppendAllText(path, message, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    internal static class UiFont
    {
        private static readonly string FamilyName = ResolveFamily();

        public static Font Make(float size, FontStyle style)
        {
            return new Font(FamilyName, size, style, GraphicsUnit.Point);
        }

        private static string ResolveFamily()
        {
            string[] preferred = new string[]
            {
                "Pretendard",
                "Noto Sans KR",
                "Malgun Gothic",
                "Segoe UI Variable Text",
                "Segoe UI"
            };

            try
            {
                foreach (FontFamily family in FontFamily.Families)
                {
                    for (int i = 0; i < preferred.Length; i++)
                    {
                        if (String.Equals(family.Name, preferred[i], StringComparison.OrdinalIgnoreCase))
                        {
                            return family.Name;
                        }
                    }
                }
            }
            catch
            {
            }

            return "Malgun Gothic";
        }
    }

    internal sealed class MainForm : Form
    {
        private const string PinballUrl = "https://lazygyu.github.io/roulette/";
        private const int DirectPinballUrlLimit = 1800;

        private readonly Color _text = Color.FromArgb(10, 18, 34);
        private readonly Color _muted = Color.FromArgb(68, 83, 111);
        private readonly Color _softMuted = Color.FromArgb(112, 129, 159);
        private readonly Color _purple = Color.FromArgb(37, 99, 235);
        private readonly Color _purpleDark = Color.FromArgb(23, 54, 130);
        private readonly Color _lavender = Color.FromArgb(226, 239, 255);
        private readonly Color _line = Color.FromArgb(180, 205, 242);
        private readonly Color _card = Color.FromArgb(251, 253, 255);
        private readonly Color _soft = Color.FromArgb(244, 248, 255);
        private readonly Color _mint = Color.FromArgb(255, 238, 248);
        private readonly Color _green = Color.FromArgb(31, 190, 138);
        private readonly Color _amber = Color.FromArgb(239, 154, 68);
        private readonly Color _red = Color.FromArgb(222, 67, 98);

        private GradientSurface _surface;
        private ToastBanner _toast;
        private FocusSinkPanel _focusSink;

        private ImageBadge _logo;
        private Label _appTitle;
        private Label _appSubtitle;
        private PillLabel _statusPill;

        private RoundedPanel _setupCard;
        private Label _streamLabel;
        private RoundTextBox _streamInput;
        private Label _conditionLabel;
        private RoundButton _exactButton;
        private RoundButton _atLeastButton;
        private Label _thresholdLabel;
        private NumberBox _thresholdInput;
        private Label _sourceLabel;
        private RoundButton _nicknameSourceButton;
        private RoundButton _contentSourceButton;
        private RoundButton _connectButton;

        private RoundedPanel _collectionCard;
        private Label _collectionEyebrow;
        private Label _collectionTitle;
        private RoundTextBox _searchInput;
        private Label _searchCountLabel;
        private RoundButton _manualTestButton;
        private RoundButton _clearCollectionButton;
        private Panel _collectionDivider;
        private Panel _emptyState;
        private GradientBadge _emptyIcon;
        private Label _emptyTitle;
        private Label _emptyText;
        private Panel _entryList;
        private readonly List<CollectedEntry> _visibleEntries = new List<CollectedEntry>();
        private bool _layingOutEntryRows;
        private bool _scrollEntryListToBottomAfterRender;

        private RoundedPanel _pinballCard;
        private Label _pinballEyebrow;
        private Label _pinballTitle;
        private Label _pinballDesc;
        private Label _pinballInputLabel;
        private Label _pinballCountLabel;
        private RoundTextBox _pinballText;
        private RoundButton _openPinballButton;
        private RoundButton _copyButton;
        private RoundButton _saveButton;

        private Label _footerLeft;
        private CreditBadge _footerRight;

        private readonly List<CollectedEntry> _entries = new List<CollectedEntry>();
        private readonly List<PendingGift> _pending = new List<PendingGift>();
        private readonly System.Windows.Forms.Timer _connectTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _reconnectTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _toastTimer = new System.Windows.Forms.Timer();

        private SoopLiveChatClient _chatClient;
        private string _streamerId = "";
        private string _streamerName = "";
        private bool _connected;
        private bool _connecting;
        private bool _manualDisconnect;
        private bool _exactMode;
        private bool _nicknamePinballMode;
        private int _reconnectAttempt;

        public MainForm()
        {
            Text = "타요의 종겜핀볼";
            Width = 1180;
            Height = 740;
            MinimumSize = new Size(700, 720);
            StartPosition = FormStartPosition.CenterScreen;
            Font = UiFont.Make(9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(4, 9, 22);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;
            Icon = LoadEmbeddedIcon("TayoPinballIcon");

            BuildUi();
            WireEvents();
            int previewCount = GetArgumentInt("--preview-count=", 0, 1000);
            if (previewCount > 0)
            {
                SeedManyPreviewRows(previewCount);
            }
            else if (IsPreviewManyMode())
            {
                SeedManyPreviewRows();
            }
            else if (IsPreviewMode())
            {
                SeedPreviewRows();
            }
            string searchPreview = GetArgumentValue("--search-preview=");
            if (searchPreview.Length > 0)
            {
                _searchInput.Text = searchPreview;
            }
            RefreshModeButtons();
            RefreshSourceButtons();
            RefreshConnectionButton();
            RefreshStatus("● 연결 안 됨", _muted, Color.FromArgb(249, 252, 255));
            RefreshCounts();

            Resize += delegate { LayoutUi(); };
            Shown += delegate
            {
                LayoutUi();
                int stressCount = GetArgumentInt("--stress-count=", 0, 1000);
                if (stressCount > 0)
                {
                    AddStressEntries(stressCount);
                    return;
                }

                string livePreviewId = GetArgumentValue("--live-preview=");
                if (livePreviewId.Length > 0)
                {
                    _streamInput.Text = livePreviewId;
                    StartConnection();
                    return;
                }

                if (IsManualTestPreviewMode())
                {
                    AddManualTestEntry();
                    return;
                }

                if (IsClearDialogPreviewMode())
                {
                    ClearEntries();
                    return;
                }

                if (IsConnectingPreviewMode())
                {
                    _streamInput.Text = "dan259";
                    _connectTimer.Interval = 7000;
                    StartConnection();
                    return;
                }

                if (IsCollectingPreviewMode())
                {
                    _streamInput.Text = "dan259";
                    StartConnection();
                    return;
                }

                if (IsToastPreviewMode())
                {
                    ShowToast(GetStartToastText());
                }
            };
        }

        private void BuildUi()
        {
            _surface = new GradientSurface();
            _surface.Dock = DockStyle.Fill;
            _surface.AutoScroll = true;
            _surface.TopColor = Color.FromArgb(4, 9, 22);
            _surface.BottomColor = Color.FromArgb(8, 28, 64);
            Controls.Add(_surface);

            _focusSink = new FocusSinkPanel();
            _focusSink.SetBounds(0, 0, 1, 1);
            _surface.Controls.Add(_focusSink);
            _focusSink.SendToBack();

            _logo = new ImageBadge();
            _logo.Image = LoadEmbeddedImage("TayoPinballLogo");
            _logo.Radius = 12;
            _logo.FillColor = Color.FromArgb(232, 242, 255);
            _logo.BorderColor = Color.FromArgb(86, 154, 255);
            _surface.Controls.Add(_logo);

            _appTitle = PlainLabel("타요의 종겜핀볼", 17.0f, FontStyle.Bold, _text);
            _appTitle.ForeColor = Color.FromArgb(250, 253, 255);
            _surface.Controls.Add(_appTitle);

            _appSubtitle = PlainLabel("오늘 걸릴 종겜은?", 9.2f, FontStyle.Bold, _muted);
            _appSubtitle.ForeColor = Color.FromArgb(194, 215, 250);
            _surface.Controls.Add(_appSubtitle);

            _statusPill = new PillLabel();
            _statusPill.Font = UiFont.Make(9.0f, FontStyle.Bold);
            _statusPill.Radius = 16;
            _surface.Controls.Add(_statusPill);

            BuildSetupCard();
            BuildCollectionCard();
            BuildPinballCard();

            _footerLeft = PlainLabel("실시간 수집 • 자동 코인 계산 • 핀볼 사이트 연동", 8.0f, FontStyle.Bold, _softMuted);
            _footerLeft.ForeColor = Color.FromArgb(190, 208, 239);
            _footerLeft.TextAlign = ContentAlignment.MiddleLeft;
            _surface.Controls.Add(_footerLeft);

            _footerRight = new CreditBadge();
            _footerRight.Text = "단즈 x 견아";
            _footerRight.Font = UiFont.Make(7.3f, FontStyle.Bold);
            _footerRight.ForeColor = Color.FromArgb(190, 208, 239);
            _footerRight.FillColor = Color.FromArgb(17, 33, 68);
            _footerRight.BorderColor = Color.FromArgb(58, 94, 153);
            _footerRight.MarkStartColor = Color.FromArgb(248, 147, 202);
            _footerRight.MarkEndColor = Color.FromArgb(53, 144, 246);
            _footerRight.Radius = 8;
            _surface.Controls.Add(_footerRight);

            _toast = new ToastBanner();
            _toast.Visible = false;
            _surface.Controls.Add(_toast);
            _toast.BringToFront();

            WireBlankFocusClearers();
        }

        private void WireBlankFocusClearers()
        {
            AttachClearFocusOnMouseDown(_surface);
            AttachClearFocusOnMouseDown(_setupCard);
            AttachClearFocusOnMouseDown(_collectionCard);
            AttachClearFocusOnMouseDown(_emptyState);
            AttachClearFocusOnMouseDown(_entryList);
            AttachClearFocusOnMouseDown(_pinballCard);
            AttachClearFocusOnMouseDown(_collectionEyebrow);
            AttachClearFocusOnMouseDown(_collectionTitle);
            AttachClearFocusOnMouseDown(_searchCountLabel);
            AttachClearFocusOnMouseDown(_pinballEyebrow);
            AttachClearFocusOnMouseDown(_pinballTitle);
            AttachClearFocusOnMouseDown(_pinballDesc);
            AttachClearFocusOnMouseDown(_pinballInputLabel);
            AttachClearFocusOnMouseDown(_footerLeft);
            AttachClearFocusOnMouseDown(_footerRight);
        }

        private void AttachClearFocusOnMouseDown(Control control)
        {
            if (control == null)
            {
                return;
            }

            control.MouseDown += delegate { ClearEditingFocus(); };
        }

        private void ClearEditingFocus()
        {
            if (_focusSink != null && _focusSink.CanFocus)
            {
                _focusSink.Focus();
                return;
            }

            ActiveControl = null;
        }

        private Image LoadEmbeddedImage(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return null;
            }

            using (stream)
            {
                return new Bitmap(stream);
            }
        }

        private Icon LoadEmbeddedIcon(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return null;
            }

            using (stream)
            using (var icon = new Icon(stream))
            {
                return (Icon)icon.Clone();
            }
        }

        private void BuildSetupCard()
        {
            _setupCard = Card();
            _surface.Controls.Add(_setupCard);

            _streamLabel = SmallHeader("방송 주소 또는 SOOP ID");
            _setupCard.Controls.Add(_streamLabel);

            _streamInput = new RoundTextBox();
            _streamInput.Placeholder = "예: SOOP 방송 주소 또는 SOOP ID";
            _setupCard.Controls.Add(_streamInput);

            _conditionLabel = SmallHeader("수집 조건");
            _setupCard.Controls.Add(_conditionLabel);

            _exactButton = SegmentButton("정확히 N개");
            _setupCard.Controls.Add(_exactButton);

            _atLeastButton = SegmentButton("N개 이상");
            _setupCard.Controls.Add(_atLeastButton);

            _thresholdLabel = SmallHeader("기준 별풍선");
            _setupCard.Controls.Add(_thresholdLabel);

            _thresholdInput = new NumberBox();
            _thresholdInput.Value = 100;
            _thresholdInput.Minimum = 1;
            _thresholdInput.Maximum = 30000;
            _thresholdInput.SuffixText = "개";
            _setupCard.Controls.Add(_thresholdInput);

            _sourceLabel = SmallHeader("핀볼 반영");
            _setupCard.Controls.Add(_sourceLabel);

            _nicknameSourceButton = SegmentButton("닉네임으로");
            _setupCard.Controls.Add(_nicknameSourceButton);

            _contentSourceButton = SegmentButton("채팅내용으로");
            _setupCard.Controls.Add(_contentSourceButton);

            _connectButton = PrimaryButton("수집 시작");
            _connectButton.Font = UiFont.Make(11.0f, FontStyle.Bold);
            _setupCard.Controls.Add(_connectButton);
        }

        private void BuildCollectionCard()
        {
            _collectionCard = Card();
            _surface.Controls.Add(_collectionCard);

            _collectionEyebrow = Eyebrow("LIVE COLLECTION");
            _collectionCard.Controls.Add(_collectionEyebrow);

            _collectionTitle = PlainLabel("수집된 핀볼 내용", 13.7f, FontStyle.Bold, _text);
            _collectionCard.Controls.Add(_collectionTitle);

            _searchInput = new RoundTextBox();
            _searchInput.Placeholder = "닉네임, 내용, 별풍선, 코인 검색";
            _searchInput.InnerTextChanged += delegate
            {
                RefreshCounts();
            };
            _collectionCard.Controls.Add(_searchInput);

            _searchCountLabel = PlainLabel("", 8.2f, FontStyle.Bold, _purple);
            _searchCountLabel.TextAlign = ContentAlignment.MiddleRight;
            _collectionCard.Controls.Add(_searchCountLabel);

            _manualTestButton = HeaderActionButton("수동 테스트", true);
            _manualTestButton.Click += delegate { AddManualTestEntry(); };
            _collectionCard.Controls.Add(_manualTestButton);

            _clearCollectionButton = HeaderActionButton("모두 지우기", false);
            _clearCollectionButton.Click += delegate { ClearEntries(); };
            _collectionCard.Controls.Add(_clearCollectionButton);

            _collectionDivider = new Panel();
            _collectionDivider.BackColor = _line;
            _collectionCard.Controls.Add(_collectionDivider);

            _emptyState = new BufferedPanel();
            _emptyState.BackColor = _card;
            _collectionCard.Controls.Add(_emptyState);

            _emptyIcon = new GradientBadge();
            _emptyIcon.Text = "✦";
            _emptyIcon.Font = UiFont.Make(20f, FontStyle.Bold);
            _emptyIcon.ForeColor = _purple;
            _emptyIcon.StartColor = Color.FromArgb(226, 239, 255);
            _emptyIcon.EndColor = Color.FromArgb(255, 235, 247);
            _emptyState.Controls.Add(_emptyIcon);

            _emptyTitle = PlainLabel("아직 수집된 채팅이 없어요", 11.5f, FontStyle.Bold, _text);
            _emptyTitle.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Controls.Add(_emptyTitle);

            _emptyText = PlainLabel("정해진 별풍선 조건을 보낸 시청자의\r\n다음 채팅 1회가 여기에 추가됩니다.", 8.9f, FontStyle.Regular, _muted);
            _emptyText.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Controls.Add(_emptyText);

            _entryList = new VerticalScrollPanel();
            _entryList.Visible = false;
            _entryList.BackColor = _card;
            _entryList.AutoScroll = true;
            _entryList.Resize += delegate { LayoutEntryRows(); };
            _collectionCard.Controls.Add(_entryList);
        }

        private void BuildPinballCard()
        {
            _pinballCard = Card();
            _surface.Controls.Add(_pinballCard);

            _pinballEyebrow = Eyebrow("PINBALL COIN");
            _pinballCard.Controls.Add(_pinballEyebrow);

            _pinballTitle = PlainLabel("핀볼은 여기로", 13.7f, FontStyle.Bold, _text);
            _pinballCard.Controls.Add(_pinballTitle);

            _pinballDesc = PlainLabel(BuildCoinGuideText(), 8.8f, FontStyle.Regular, _muted);
            _pinballDesc.AutoEllipsis = false;
            _pinballCard.Controls.Add(_pinballDesc);

            _pinballInputLabel = PlainLabel("반영된 코인", 8.6f, FontStyle.Bold, _text);
            _pinballCard.Controls.Add(_pinballInputLabel);

            _pinballCountLabel = PlainLabel("0개", 8.5f, FontStyle.Bold, _purple);
            _pinballCountLabel.TextAlign = ContentAlignment.MiddleRight;
            _pinballCard.Controls.Add(_pinballCountLabel);

            _pinballText = new RoundTextBox();
            _pinballText.Multiline = true;
            _pinballText.Placeholder = "수집된 내용이 이곳에 표시됩니다";
            _pinballText.InnerTextChanged += delegate { RefreshPinballTotalLabel(); };
            _pinballCard.Controls.Add(_pinballText);

            _openPinballButton = GradientButton("핀볼 사이트 열기  ↗");
            _openPinballButton.Font = UiFont.Make(11.4f, FontStyle.Bold);
            _openPinballButton.FillColor = Color.FromArgb(37, 99, 235);
            _openPinballButton.GradientColor = Color.FromArgb(129, 114, 235);
            _openPinballButton.BorderColor = Color.FromArgb(111, 154, 255);
            _pinballCard.Controls.Add(_openPinballButton);

            _copyButton = SecondaryButton("내용 복사");
            _pinballCard.Controls.Add(_copyButton);

            _saveButton = SecondaryButton("메모장 저장");
            _pinballCard.Controls.Add(_saveButton);

        }

        private void WireEvents()
        {
            _exactButton.Click += delegate
            {
                _exactMode = true;
                RefreshModeButtons();
                ShowToast(GetConditionToastText());
            };
            _atLeastButton.Click += delegate
            {
                _exactMode = false;
                RefreshModeButtons();
                ShowToast(GetConditionToastText());
            };
            _nicknameSourceButton.Click += delegate
            {
                _nicknamePinballMode = true;
                RefreshSourceButtons();
                ShowToast("닉네임을 핀볼에 반영합니다.");
            };
            _contentSourceButton.Click += delegate
            {
                _nicknamePinballMode = false;
                RefreshSourceButtons();
                ShowToast("채팅 내용을 핀볼에 반영합니다.");
            };
            _thresholdInput.ValueChanged += delegate
            {
                RecalculateCoins();
                RefreshCoinGuide();
                RefreshPinballText(false);
                RefreshCounts();
            };
            _connectButton.Click += delegate
            {
                if (_connected || _connectTimer.Enabled || _reconnectTimer.Enabled)
                {
                    Disconnect(true);
                }
                else
                {
                    StartConnection();
                }
            };
            _openPinballButton.Click += delegate { OpenPinballSite(); };
            _copyButton.Click += delegate { CopyPinballText(); };
            _saveButton.Click += delegate { SavePinballText(); };

            _toastTimer.Interval = 2600;
            _toastTimer.Tick += delegate
            {
                _toastTimer.Stop();
                if (_toast != null)
                {
                    _toast.Visible = false;
                }
            };

            _connectTimer.Interval = 800;
            _connectTimer.Tick += delegate
            {
                _connectTimer.Stop();
            };

            _reconnectTimer.Tick += delegate
            {
                _reconnectTimer.Stop();
                if (!_manualDisconnect && !_connected)
                {
                    StartConnection();
                }
            };
        }

        private void LayoutUi()
        {
            if (_surface == null)
            {
                return;
            }

            _surface.SuspendLayout();

            int viewportWidth = Math.Max(320, _surface.Width);
            int margin = viewportWidth < 520 ? 6 : 16;
            int rightMargin = margin;
            int contentWidth = Math.Max(300, viewportWidth - margin - rightMargin);
            bool stackedLayout = contentWidth < 860;
            int previousSurfaceScrollY = stackedLayout ? Math.Max(0, -_surface.AutoScrollPosition.Y) : 0;
            _surface.AutoScrollPosition = Point.Empty;
            if (stackedLayout)
            {
                contentWidth = Math.Max(300, viewportWidth - margin - rightMargin - SystemInformation.VerticalScrollBarWidth);
                _surface.AutoScroll = true;
            }
            else
            {
                _surface.AutoScrollPosition = Point.Empty;
                _surface.AutoScrollMinSize = Size.Empty;
                _surface.AutoScroll = false;
            }
            int y = viewportWidth < 520 ? 12 : 18;

            LayoutHeader(margin, y, contentWidth);
            LayoutToast(margin, y, contentWidth);
            y += viewportWidth < 520 ? 76 : 64;

            int setupHeight = LayoutSetupCard(margin, y, contentWidth);
            y += setupHeight + 14;

            if (!stackedLayout)
            {
                int gap = 14;
                int bodyX = margin;
                int leftWidth;
                int rightWidth;

                if (contentWidth >= 1500)
                {
                    leftWidth = (contentWidth - gap) / 2;
                    rightWidth = contentWidth - leftWidth - gap;
                }
                else
                {
                    int minRightWidth = 330;
                    int maxLeftWidth = 690;
                    leftWidth = Math.Min(maxLeftWidth, Math.Max(520, contentWidth - minRightWidth - gap));
                    rightWidth = contentWidth - leftWidth - gap;
                }

                int bodyHeight = Math.Max(400, _surface.ClientSize.Height - y - 26);
                LayoutCollectionCard(bodyX, y, leftWidth, bodyHeight);
                LayoutPinballCard(bodyX + leftWidth + gap, y, rightWidth, bodyHeight);
                y += bodyHeight + 1;
            }
            else
            {
                int collectionHeight = contentWidth < 520 ? 400 : 410;
                int pinballHeight = contentWidth < 520 ? 430 : 410;
                LayoutCollectionCard(margin, y, contentWidth, collectionHeight);
                y += collectionHeight + 12;
                LayoutPinballCard(margin, y, contentWidth, pinballHeight);
                y += pinballHeight + 2;
            }

            LayoutFooter(margin, y, contentWidth);
            y += stackedLayout ? 34 : 18;

            int scrollContentHeight = y + (stackedLayout ? 10 : 2);
            _surface.AutoScrollMinSize = stackedLayout ? new Size(0, scrollContentHeight) : Size.Empty;
            _surface.ResumeLayout();
            if (stackedLayout && previousSurfaceScrollY > 0)
            {
                int maxScrollY = Math.Max(0, scrollContentHeight - _surface.ClientSize.Height);
                int restoreScrollY = Math.Min(previousSurfaceScrollY, maxScrollY);
                if (restoreScrollY > 0)
                {
                    _surface.AutoScrollPosition = new Point(0, restoreScrollY);
                }
            }
            _surface.Invalidate();
        }

        private void LayoutToast(int x, int y, int width)
        {
            if (_toast == null)
            {
                return;
            }

            int measured = TextRenderer.MeasureText(_toast.Text, _toast.Font).Width + 92;
            int available = Math.Max(220, width - 48);
            int toastWidth = Math.Min(available, Math.Min(380, Math.Max(320, measured)));
            _toast.SetBounds(x + ((width - toastWidth) / 2), y + 5, toastWidth, 38);
            if (_toast.Visible)
            {
                _toast.BringToFront();
            }
        }

        private void LayoutHeader(int x, int y, int width)
        {
            int measuredStatus = TextRenderer.MeasureText(_statusPill.Text, _statusPill.Font).Width + 38;
            int maxStatus = width < 520 ? Math.Max(112, width - 300) : Math.Max(132, width - 430);
            int statusWidth = Math.Min(Math.Max(width < 460 ? 112 : 132, measuredStatus), maxStatus);
            int titleWidth = Math.Max(120, width - statusWidth - 76);

            _logo.SetBounds(x, y + 2, 42, 42);
            _appTitle.SetBounds(x + 52, y - 2, titleWidth + 2, 34);
            _appSubtitle.SetBounds(x + 54, y + 30, titleWidth, 22);
            _appSubtitle.Visible = width >= 420;

            _statusPill.SetBounds(x + width - statusWidth, y + 8, statusWidth, 32);
        }

        private int LayoutSetupCard(int x, int y, int width)
        {
            if (width >= 1040)
            {
                int height = 86;
                _setupCard.SetBounds(x, y, width, height);
                int innerY = 14;
                int inputX = 24;
                int buttonArea = 140;
                int buttonX = width - buttonArea;
                int thresholdX = buttonX - 116;
                int sourceX = thresholdX - 212;
                int conditionX = sourceX - 202;
                int inputW = Math.Max(240, conditionX - inputX - 18);

                _streamLabel.SetBounds(inputX, innerY, inputW, 18);
                _streamInput.SetBounds(inputX, 38, inputW, 34);
                _conditionLabel.SetBounds(conditionX, innerY, 160, 18);
                _exactButton.SetBounds(conditionX, 38, 88, 34);
                _atLeastButton.SetBounds(conditionX + 94, 38, 84, 34);
                _sourceLabel.SetBounds(sourceX, innerY, 160, 18);
                _nicknameSourceButton.SetBounds(sourceX, 38, 84, 34);
                _contentSourceButton.SetBounds(sourceX + 92, 38, 96, 34);
                _thresholdLabel.SetBounds(thresholdX, innerY, 110, 18);
                _thresholdInput.SetBounds(thresholdX, 38, 92, 34);
                _connectButton.SetBounds(buttonX, innerY, 120, 58);
                return height;
            }

            if (width >= 760)
            {
                int height = 156;
                _setupCard.SetBounds(x, y, width, height);

                int mediumPad = 18;
                int streamW = width - (mediumPad * 2);
                int labelY = 84;
                int controlY = 108;
                int connectW = 120;
                int connectH = 58;
                int connectX = width - mediumPad - connectW;
                int conditionX = mediumPad;
                int conditionW = 202;
                int sourceW = 202;
                int thresholdW = 100;
                int groupGap = 26;
                int sourceX = conditionX + conditionW + groupGap;
                int thresholdX = sourceX + sourceW + groupGap;
                if (thresholdX + thresholdW + 24 > connectX)
                {
                    groupGap = Math.Max(14, (connectX - conditionX - conditionW - sourceW - thresholdW - 24) / 2);
                    sourceX = conditionX + conditionW + groupGap;
                    thresholdX = sourceX + sourceW + groupGap;
                }

                _streamLabel.SetBounds(mediumPad, 16, streamW, 18);
                _streamInput.SetBounds(mediumPad, 40, streamW, 34);
                _conditionLabel.SetBounds(conditionX, labelY, 160, 18);
                _exactButton.SetBounds(conditionX, controlY, 98, 34);
                _atLeastButton.SetBounds(conditionX + 104, controlY, 92, 34);
                _sourceLabel.SetBounds(sourceX, labelY, 160, 18);
                _nicknameSourceButton.SetBounds(sourceX, controlY, 90, 34);
                _contentSourceButton.SetBounds(sourceX + 96, controlY, 106, 34);
                _thresholdLabel.SetBounds(thresholdX, labelY, 110, 18);
                _thresholdInput.SetBounds(thresholdX, controlY, thresholdW, 34);
                _connectButton.SetBounds(connectX, labelY, connectW, connectH);
                return height;
            }

            if (width >= 560)
            {
                int height = 228;
                int rightColumnX = width - 140;
                int thresholdX = rightColumnX + 10;
                _setupCard.SetBounds(x, y, width, height);
                _streamLabel.SetBounds(18, 16, width - 38, 18);
                _streamInput.SetBounds(18, 40, width - 38, 34);
                _conditionLabel.SetBounds(18, 84, 160, 18);
                _exactButton.SetBounds(18, 108, 98, 34);
                _atLeastButton.SetBounds(122, 108, 92, 34);
                _sourceLabel.SetBounds(258, 84, 160, 18);
                _nicknameSourceButton.SetBounds(258, 108, 90, 34);
                _contentSourceButton.SetBounds(354, 108, 106, 34);
                _thresholdLabel.SetBounds(thresholdX, 84, 110, 18);
                _thresholdInput.SetBounds(thresholdX, 108, 100, 34);
                _connectButton.SetBounds(rightColumnX, 154, 120, 58);
                return height;
            }

            int narrowHeight = 350;
            _setupCard.SetBounds(x, y, width, narrowHeight);
            int pad = 14;
            int fieldW = width - (pad * 2);
            int halfW = (fieldW - 8) / 2;

            _streamLabel.SetBounds(pad, 14, fieldW, 18);
            _streamInput.SetBounds(pad, 46, fieldW, 34);
            _conditionLabel.SetBounds(pad, 90, fieldW, 18);
            _exactButton.SetBounds(pad, 114, halfW, 34);
            _atLeastButton.SetBounds(pad + halfW + 8, 114, halfW, 34);
            _sourceLabel.SetBounds(pad, 158, fieldW, 18);
            _nicknameSourceButton.SetBounds(pad, 182, halfW, 34);
            _contentSourceButton.SetBounds(pad + halfW + 8, 182, halfW, 34);
            _thresholdLabel.SetBounds(pad, 226, fieldW, 18);
            _thresholdInput.SetBounds(pad, 250, fieldW, 34);
            _connectButton.SetBounds(pad, 294, fieldW, 42);
            return narrowHeight;
        }

        private void LayoutCollectionCard(int x, int y, int width, int height)
        {
            _collectionCard.SetBounds(x, y, width, height);
            int pad = width < 420 ? 16 : 24;

            _collectionEyebrow.SetBounds(pad, 20, 180, 18);

            if (width >= 520)
            {
                int actionsX = width - pad - 234;
                int headerDividerY = 116;
                int actionY = 20;
                _collectionTitle.SetBounds(pad - 2, 42, Math.Max(180, actionsX - pad - 16), 28);
                _manualTestButton.SetBounds(actionsX, actionY, 112, 34);
                _clearCollectionButton.SetBounds(actionsX + 126, actionY, 108, 34);
                int searchY = 78;
                int countW = 64;
                _searchInput.SetBounds(pad, searchY, Math.Max(180, width - (pad * 2) - countW - 10), 32);
                _searchCountLabel.SetBounds(width - pad - countW, searchY + 5, countW, 20);
                _collectionDivider.SetBounds(pad, headerDividerY, width - (pad * 2), 1);
            }
            else if (width >= 430)
            {
                int actionsX = width - pad - 234;
                _collectionTitle.SetBounds(pad - 2, 42, Math.Max(160, actionsX - pad - 12), 28);
                _manualTestButton.SetBounds(actionsX, 66, 112, 34);
                _clearCollectionButton.SetBounds(actionsX + 126, 66, 108, 34);
                int searchY = 112;
                int countW = 62;
                _searchInput.SetBounds(pad, searchY, Math.Max(150, width - (pad * 2) - countW - 8), 32);
                _searchCountLabel.SetBounds(width - pad - countW, searchY + 5, countW, 20);
                _collectionDivider.SetBounds(pad, 150, width - (pad * 2), 1);
            }
            else
            {
                _collectionTitle.SetBounds(pad - 2, 42, width - (pad * 2) + 2, 28);
                _manualTestButton.SetBounds(pad, 78, 112, 34);
                _clearCollectionButton.SetBounds(pad + 126, 78, Math.Min(108, width - (pad * 2) - 126), 34);
                int searchY = 124;
                _searchInput.SetBounds(pad, searchY, width - (pad * 2), 32);
                _searchCountLabel.SetBounds(pad, searchY + 34, width - (pad * 2), 18);
                _collectionDivider.SetBounds(pad, 178, width - (pad * 2), 1);
            }

            int dividerY = _collectionDivider.Top;
            int bodyY = dividerY + (_visibleEntries.Count > 0 ? 1 : 14);
            int bodyH = Math.Max(180, height - bodyY - 22);
            _emptyState.SetBounds(pad, bodyY, width - (pad * 2), bodyH);
            _entryList.SetBounds(pad, bodyY, width - (pad * 2), bodyH);
            LayoutEmptyState();
            LayoutEntryRows();
        }

        private void LayoutEmptyState()
        {
            int w = _emptyState.Width;
            int h = _emptyState.Height;
            int centerX = w / 2;
            int groupHeight = 70 + 22 + 28 + 10 + 48;
            int startY = Math.Max(10, (h - groupHeight) / 2);
            int titleW = Math.Min(320, Math.Max(220, w - 24));
            int textW = Math.Min(360, Math.Max(240, w - 24));

            _emptyIcon.SetBounds(centerX - 35, startY, 70, 70);
            _emptyTitle.SetBounds(centerX - (titleW / 2), startY + 92, titleW, 28);
            _emptyText.SetBounds(centerX - (textW / 2), startY + 130, textW, 48);
        }

        private void RenderEntryRows()
        {
            if (_entryList == null)
            {
                return;
            }

            _entryList.SuspendLayout();
            while (_entryList.Controls.Count > _visibleEntries.Count)
            {
                Control last = _entryList.Controls[_entryList.Controls.Count - 1];
                _entryList.Controls.RemoveAt(_entryList.Controls.Count - 1);
                last.Dispose();
            }

            for (int i = 0; i < _visibleEntries.Count; i++)
            {
                CollectedEntry entry = _visibleEntries[i];
                EntryRowControl row = i < _entryList.Controls.Count ? _entryList.Controls[i] as EntryRowControl : null;
                if (row == null || row.Entry != entry)
                {
                    row = new EntryRowControl(entry, i + 1, _text, _muted, _purple, _lavender, _line, _card);
                    row.EntryChanged += delegate { RefreshPinballText(false); };
                    row.BlankClicked += delegate { ClearEditingFocus(); };
                    EntryRowControl rowForEvent = row;
                    row.DeleteClicked += delegate
                    {
                        int index = _entries.IndexOf(rowForEvent.Entry);
                        if (index >= 0)
                        {
                            _entries.RemoveAt(index);
                            RefreshPinballText(false);
                            RefreshCounts();
                        }
                    };

                    if (i < _entryList.Controls.Count)
                    {
                        Control old = _entryList.Controls[i];
                        _entryList.Controls.RemoveAt(i);
                        old.Dispose();
                        _entryList.Controls.Add(row);
                        _entryList.Controls.SetChildIndex(row, i);
                    }
                    else
                    {
                        _entryList.Controls.Add(row);
                    }
                }

                row.SetIndex(i + 1);
                row.RefreshEntryValues();
            }

            _entryList.ResumeLayout();
            LayoutEntryRows();
        }

        private void LayoutEntryRows()
        {
            if (_entryList == null || _layingOutEntryRows)
            {
                return;
            }

            _layingOutEntryRows = true;
            _entryList.SuspendLayout();
            try
            {
                int totalHeight = _visibleEntries.Count * EntryRowControl.RowHeight;
                int previousScrollY = Math.Max(0, -_entryList.AutoScrollPosition.Y);

                _entryList.AutoScrollPosition = Point.Empty;
                _entryList.AutoScrollMinSize = new Size(0, totalHeight);

                int rowWidth = Math.Max(240, _entryList.ClientSize.Width - 2);
                int y = 0;

                foreach (Control control in _entryList.Controls)
                {
                    control.SetBounds(0, y, rowWidth, EntryRowControl.RowHeight);
                    y += EntryRowControl.RowHeight;
                }

                int maxScrollY = Math.Max(0, totalHeight - _entryList.ClientSize.Height);
                int restoreScrollY = _scrollEntryListToBottomAfterRender ? maxScrollY : Math.Min(previousScrollY, maxScrollY);
                if (restoreScrollY > 0)
                {
                    _entryList.AutoScrollPosition = new Point(0, restoreScrollY);
                }
            }
            finally
            {
                bool scrollToBottom = _scrollEntryListToBottomAfterRender;
                _entryList.ResumeLayout();
                _scrollEntryListToBottomAfterRender = false;
                _layingOutEntryRows = false;
                if (scrollToBottom)
                {
                    ScrollEntryListToBottom();
                }
            }
        }

        private void ScrollEntryListToBottom()
        {
            if (_entryList == null || !_entryList.IsHandleCreated)
            {
                return;
            }

            _entryList.AutoScrollPosition = new Point(0, Math.Max(0, _entryList.AutoScrollMinSize.Height));
            _entryList.BeginInvoke((MethodInvoker)delegate
            {
                if (_entryList != null && !_entryList.IsDisposed)
                {
                    _entryList.AutoScrollPosition = new Point(0, Math.Max(0, _entryList.AutoScrollMinSize.Height));
                }
            });
        }

        private void LayoutPinballCard(int x, int y, int width, int height)
        {
            _pinballCard.SetBounds(x, y, width, height);
            int pad = width < 420 ? 16 : 24;

            _pinballEyebrow.SetBounds(pad, 22, 180, 18);
            _pinballTitle.SetBounds(pad - 2, 46, width - (pad * 2) + 2, 30);
            _pinballDesc.SetBounds(pad, 80, width - (pad * 2), 70);
            int totalLabelW = width < 380 ? 88 : 116;
            _pinballInputLabel.SetBounds(pad, 160, Math.Max(120, width - (pad * 2) - totalLabelW - 8), 20);
            _pinballCountLabel.SetBounds(width - pad - totalLabelW, 160, totalLabelW, 20);

            int textY = 184;
            int textHeight = Math.Max(88, height - 328);
            _pinballText.SetBounds(pad, textY, width - (pad * 2), textHeight);

            int buttonY = textY + textHeight + 18;
            _openPinballButton.SetBounds(pad, buttonY, width - (pad * 2), 46);

            int smallY = buttonY + 62;
            int smallGap = 10;
            int smallW = (width - (pad * 2) - smallGap) / 2;
            _copyButton.SetBounds(pad, smallY, smallW, 36);
            _saveButton.SetBounds(pad + smallW + smallGap, smallY, smallW, 36);

        }

        private void LayoutFooter(int x, int y, int width)
        {
            int footerY = y;
            int creditTextW = TextRenderer.MeasureText(
                _footerRight.Text,
                _footerRight.Font,
                Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            int creditW = Math.Max(48, creditTextW + 27);
            _footerLeft.SetBounds(x, footerY, Math.Max(180, width - creditW - 16), 18);
            _footerRight.SetBounds(x + width - creditW, footerY + 3, creditW, 18);
            _footerRight.Visible = width >= 560;
        }

        private async void StartConnection()
        {
            string raw = _streamInput.Text.Trim();
            if (raw.Length == 0)
            {
                MessageBox.Show("SOOP 방송 주소 또는 SOOP ID를 입력해 주세요.", "입력 필요", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _manualDisconnect = false;
            _connected = false;
            _connecting = true;
            _reconnectTimer.Stop();
            _connectTimer.Stop();
            CloseChatClient();
            _streamerId = ExtractStreamerId(raw);
            _streamerName = ResolveDisplayName(_streamerId);
                RefreshStatus("● " + _streamerName + " 방송 확인 중", _amber, Color.FromArgb(255, 246, 239));
            ShowToast(_streamerName + " 방송 정보를 확인합니다.");
            RefreshConnectionButton();

            var client = new SoopLiveChatClient();
            _chatClient = client;
            AttachChatClient(client);

            try
            {
                await client.ConnectAsync(_streamerId, "");
            }
            catch (Exception ex)
            {
                if (_chatClient != client)
                {
                    return;
                }

                CloseChatClient();
                _connected = false;
                _connecting = false;
                RefreshConnectionButton();
                RefreshStatus("● " + _streamerName + " 연결 실패", _red, Color.FromArgb(255, 242, 247));
                ShowToast(ex.Message);
                if (!_manualDisconnect)
                {
                    ScheduleReconnect();
                }
            }
        }

        private void Disconnect(bool manual)
        {
            _manualDisconnect = manual;
            _connected = false;
            _connecting = false;
            _connectTimer.Stop();
            _reconnectTimer.Stop();
            CloseChatClient();
            RefreshConnectionButton();
            RefreshStatus("● 연결 안 됨", _muted, Color.FromArgb(249, 252, 255));
            if (manual)
            {
                ShowToast("수집을 중지했습니다.");
            }
        }

        private void ScheduleReconnect()
        {
            if (_manualDisconnect)
            {
                return;
            }

            _reconnectAttempt++;
            int wait = Math.Min(12000, 1000 + (_reconnectAttempt * 1500));
            _reconnectTimer.Interval = wait;
            RefreshStatus("● " + _streamerName + " 재연결 대기", _amber, Color.FromArgb(255, 246, 239));
            _reconnectTimer.Start();
            RefreshConnectionButton();
        }

        private void AttachChatClient(SoopLiveChatClient client)
        {
            client.BroadcastResolved += delegate(SoopLiveInfo info)
            {
                RunOnUiThread(delegate
                {
                    if (_chatClient != client)
                    {
                        return;
                    }

                    _streamerName = info.StreamerName;
                    RefreshStatus("● " + _streamerName + " 채팅 연결 중", _amber, Color.FromArgb(255, 246, 239));
                    ShowToast(GetStartToastText());
                });
            };

            client.Joined += delegate
            {
                RunOnUiThread(delegate
                {
                    if (_chatClient != client)
                    {
                        return;
                    }

                    _connected = true;
                    _connecting = false;
                    _reconnectAttempt = 0;
                    RefreshConnectionButton();
                    RefreshStatus("● " + _streamerName + " 연결됨", _green, Color.FromArgb(235, 250, 245));
                    ShowToast(_streamerName + " 방송 채팅에 연결됐습니다.");
                });
            };

            client.BalloonReceived += delegate(string nickname, int count)
            {
                RunOnUiThread(delegate
                {
                    if (_chatClient == client)
                    {
                        HandleBalloonGift(nickname, count);
                    }
                });
            };

            client.ChatReceived += delegate(string nickname, string message)
            {
                RunOnUiThread(delegate
                {
                    if (_chatClient == client)
                    {
                        HandleChatMessage(nickname, message);
                    }
                });
            };

            client.Disconnected += delegate(string reason)
            {
                RunOnUiThread(delegate
                {
                    if (_chatClient != client)
                    {
                        return;
                    }

                    CloseChatClient();
                    _connected = false;
                    _connecting = false;
                    RefreshConnectionButton();
                    if (!_manualDisconnect)
                    {
                        RefreshStatus("● " + _streamerName + " 연결 끊김", _amber, Color.FromArgb(255, 246, 239));
                        ShowToast("채팅 연결이 끊겨 자동 재연결합니다.");
                        ScheduleReconnect();
                    }
                });
            };
        }

        private void CloseChatClient()
        {
            SoopLiveChatClient client = _chatClient;
            _chatClient = null;
            if (client != null)
            {
                client.Dispose();
            }
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || action == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(action);
                }
                catch
                {
                }
                return;
            }

            action();
        }

        private void AddManualTestEntry()
        {
            var entry = new CollectedEntry();
            entry.Nickname = "테스트";
            entry.BalloonCount = _thresholdInput.Value;
            entry.CoinCount = CalculateCoins(entry.BalloonCount);
            entry.PinballName = _nicknamePinballMode ? "테스트" : "종겜핀볼 테스트";
            entry.ReceivedAt = DateTime.Now.ToString("HH:mm:ss");
            AddCollectedEntry(entry);
            ShowToast("수동 테스트 1건을 추가했습니다.");
        }

        private void HandleBalloonGift(string nickname, int count)
        {
            if (nickname.Length == 0)
            {
                nickname = "익명";
            }

            bool passed = _exactMode ? count == _thresholdInput.Value : count >= _thresholdInput.Value;
            if (!passed)
            {
                return;
            }

            var gift = new PendingGift();
            gift.Nickname = nickname;
            gift.BalloonCount = count;
            gift.CoinCount = CalculateCoins(count);
            gift.CreatedAt = DateTime.Now;
            _pending.Add(gift);
            PrunePendingGifts();
        }

        private void HandleChatMessage(string nickname, string message)
        {
            if (nickname.Length == 0)
            {
                nickname = "익명";
            }

            if (message.Length == 0)
            {
                message = nickname;
            }

            PrunePendingGifts();
            PendingGift matched = null;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (String.Equals(_pending[i].Nickname, nickname, StringComparison.OrdinalIgnoreCase))
                {
                    matched = _pending[i];
                    _pending.RemoveAt(i);
                    break;
                }
            }

            if (matched == null)
            {
                return;
            }

            var entry = new CollectedEntry();
            entry.Nickname = matched.Nickname;
            entry.BalloonCount = matched.BalloonCount;
            entry.CoinCount = matched.CoinCount;
            entry.PinballName = _nicknamePinballMode ? matched.Nickname : message;
            entry.ReceivedAt = DateTime.Now.ToString("HH:mm:ss");
            AddCollectedEntry(entry);
        }

        private void AddCollectedEntry(CollectedEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            _entries.Add(entry);
            _scrollEntryListToBottomAfterRender = !IsEditingEntryList();
            RefreshPinballText(_scrollEntryListToBottomAfterRender);
            RefreshCounts();
        }

        private bool IsEditingEntryList()
        {
            if (_entryList == null)
            {
                return false;
            }

            foreach (Control control in _entryList.Controls)
            {
                EntryRowControl row = control as EntryRowControl;
                if (row != null && row.IsEditing)
                {
                    return true;
                }
            }

            return false;
        }

        private void PrunePendingGifts()
        {
            DateTime cutoff = DateTime.Now.AddMinutes(-10);
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].CreatedAt < cutoff)
                {
                    _pending.RemoveAt(i);
                }
            }
        }

        private int CalculateCoins(int balloonCount)
        {
            int unit = GetCoinUnit();
            int baseCoins = balloonCount / unit;
            int bonusCoins = baseCoins / 10;
            return Math.Max(1, baseCoins + bonusCoins);
        }

        private int GetCoinUnit()
        {
            if (_thresholdInput == null)
            {
                return 100;
            }

            return Math.Max(1, _thresholdInput.Value);
        }

        private void RecalculateCoins()
        {
            foreach (CollectedEntry entry in _entries)
            {
                if (entry != null)
                {
                    entry.CoinCount = CalculateCoins(entry.BalloonCount);
                }
            }

            foreach (PendingGift gift in _pending)
            {
                if (gift != null)
                {
                    gift.CoinCount = CalculateCoins(gift.BalloonCount);
                }
            }
        }

        private void RefreshPinballText()
        {
            RefreshPinballText(false);
        }

        private void RefreshPinballText(bool scrollToBottom)
        {
            var parts = new List<string>();
            foreach (CollectedEntry entry in _entries)
            {
                if (entry == null || entry.CoinCount <= 0)
                {
                    continue;
                }

                string name = SanitizePinballName(entry.PinballName);
                if (name.Length == 0)
                {
                    name = SanitizePinballName(entry.Nickname);
                }

                if (name.Length > 0)
                {
                    parts.Add(name + "*" + entry.CoinCount);
                }
            }

            string nextText = String.Join(",", parts.ToArray());
            if (_pinballText.Text != nextText)
            {
                if (scrollToBottom)
                {
                    _pinballText.SetTextScrollToBottom(nextText);
                }
                else
                {
                    _pinballText.SetTextPreserveView(nextText);
                }
            }
            RefreshPinballTotalLabel();
        }

        private void RefreshCounts()
        {
            RebuildVisibleEntries();
            RefreshPinballTotalLabel();

            bool searchActive = GetSearchText().Length > 0;
            bool hasVisibleEntries = _visibleEntries.Count > 0;
            bool visibilityChanged = _entryList.Visible != hasVisibleEntries;
            _entryList.Visible = hasVisibleEntries;
            _emptyState.Visible = !hasVisibleEntries;
            if (searchActive)
            {
                _searchCountLabel.Text = _visibleEntries.Count + "/" + _entries.Count;
            }
            else
            {
                _searchCountLabel.Text = _entries.Count > 0 ? _entries.Count + "개" : "";
            }
            RefreshEmptyStateText(searchActive);
            RenderEntryRows();
            if (visibilityChanged && _surface != null && _surface.IsHandleCreated)
            {
                LayoutUi();
            }
        }

        private void RefreshPinballTotalLabel()
        {
            if (_pinballCountLabel != null)
            {
                _pinballCountLabel.Text = "총 " + CalculateReflectedCoinTotal().ToString("#,0") + "코인";
            }
        }

        private long CalculateReflectedCoinTotal()
        {
            if (_pinballText != null)
            {
                return CalculatePinballTextCoinTotal(NormalizePinballNames(_pinballText.Text));
            }

            long total = 0;
            foreach (CollectedEntry entry in _entries)
            {
                if (entry == null || entry.CoinCount <= 0)
                {
                    continue;
                }

                string name = SanitizePinballName(entry.PinballName);
                if (name.Length == 0)
                {
                    name = SanitizePinballName(entry.Nickname);
                }

                if (name.Length > 0)
                {
                    total += entry.CoinCount;
                }
            }

            return total;
        }

        private long CalculatePinballTextCoinTotal(string value)
        {
            if (value == null || value.Trim().Length == 0)
            {
                return 0;
            }

            long total = 0;
            string[] parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawPart in parts)
            {
                string part = rawPart.Trim();
                if (part.Length == 0)
                {
                    continue;
                }

                Match match = Regex.Match(part, "\\*([0-9]+)\\s*$");
                if (match.Success)
                {
                    long coins;
                    if (Int64.TryParse(match.Groups[1].Value, out coins) && coins > 0)
                    {
                        total += coins;
                        continue;
                    }

                    if (coins == 0)
                    {
                        continue;
                    }
                }

                total += 1;
            }

            return total;
        }

        private void RebuildVisibleEntries()
        {
            _visibleEntries.Clear();
            string query = GetSearchText();
            foreach (CollectedEntry entry in _entries)
            {
                if (query.Length == 0 || EntryMatchesSearch(entry, query))
                {
                    _visibleEntries.Add(entry);
                }
            }
        }

        private string GetSearchText()
        {
            return _searchInput == null ? "" : (_searchInput.Text ?? "").Trim();
        }

        private bool EntryMatchesSearch(CollectedEntry entry, string query)
        {
            if (entry == null)
            {
                return false;
            }

            return ContainsSearch(entry.Nickname, query) ||
                   ContainsSearch(entry.PinballName, query) ||
                   ContainsSearch(entry.BalloonCount.ToString(), query) ||
                   ContainsSearch(entry.CoinCount.ToString() + "코인", query) ||
                   ContainsSearch(entry.ReceivedAt, query);
        }

        private static bool ContainsSearch(string value, string query)
        {
            return (value ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RefreshEmptyStateText(bool searchActive)
        {
            if (searchActive && _entries.Count > 0)
            {
                _emptyTitle.Text = "검색 결과가 없어요";
                _emptyText.Text = "닉네임, 핀볼 내용, 별풍선, 코인 기준으로\r\n다시 검색해 보세요.";
                return;
            }

            _emptyTitle.Text = "아직 수집된 채팅이 없어요";
            _emptyText.Text = "정해진 별풍선 조건을 보낸 시청자의\r\n다음 채팅 1회가 여기에 추가됩니다.";
        }

        private void RefreshStatus(string text, Color textColor, Color fillColor)
        {
            _statusPill.Text = text;
            _statusPill.ForeColor = textColor;
            _statusPill.FillColor = fillColor;
            _statusPill.BorderColor = Color.FromArgb(214, 228, 250);
            _statusPill.Invalidate();
            if (_surface != null && _surface.IsHandleCreated)
            {
                LayoutUi();
            }
        }

        private void RefreshConnectionButton()
        {
            if (_connectButton == null)
            {
                return;
            }

            bool collecting = _connected || _connecting || _connectTimer.Enabled || _reconnectTimer.Enabled;
            if (collecting)
            {
                _connectButton.Text = "수집 중지";
                _connectButton.FillColor = Color.FromArgb(255, 238, 248);
                _connectButton.GradientColor = Color.Empty;
                _connectButton.BorderColor = Color.FromArgb(244, 162, 208);
                _connectButton.ForeColor = Color.FromArgb(132, 38, 91);
                _connectButton.UseGradient = false;
            }
            else
            {
                _connectButton.Text = "수집 시작";
                _connectButton.FillColor = Color.FromArgb(25, 106, 246);
                _connectButton.GradientColor = Color.FromArgb(66, 139, 255);
                _connectButton.BorderColor = Color.FromArgb(94, 155, 255);
                _connectButton.ForeColor = Color.White;
                _connectButton.UseGradient = true;
            }

            _connectButton.Invalidate();
        }

        private bool IsPreviewMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--preview", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPreviewManyMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--preview-many", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsToastPreviewMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--toast-preview", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCollectingPreviewMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--collecting-preview", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsManualTestPreviewMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--manual-test-preview", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsClearDialogPreviewMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--clear-dialog-preview", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsConnectingPreviewMode()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--connecting-preview", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string GetArgumentValue(string prefix)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i].Substring(prefix.Length).Trim();
                }
            }

            return "";
        }

        private int GetArgumentInt(string prefix, int fallback, int max)
        {
            string value = GetArgumentValue(prefix);
            int parsed;
            if (!Int32.TryParse(value, out parsed))
            {
                return fallback;
            }

            return Math.Max(0, Math.Min(max, parsed));
        }

        private string GetStartToastText()
        {
            int threshold = _thresholdInput.Value;
            string name = _streamerName.Length > 0 ? _streamerName : "방송";
            string condition = _exactMode ? "정확히 " + threshold + "개" : threshold + "개 이상";
            return name + " 방송 연결 중 · " + condition + " 수집";
        }

        private void ShowToast(string message)
        {
            if (_toast == null || message == null || message.Length == 0)
            {
                return;
            }

            _toast.Text = message;
            _toast.Visible = true;
            _toast.BringToFront();
            _toast.Invalidate();
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void SeedPreviewRows()
        {
            _entries.Add(new CollectedEntry { Nickname = "테스트", BalloonCount = 100, CoinCount = 1, PinballName = "테스트", ReceivedAt = DateTime.Now.ToString("HH:mm:ss") });
            _entries.Add(new CollectedEntry { Nickname = "타요", BalloonCount = 1017, CoinCount = 11, PinballName = "타요의 종겜핀볼", ReceivedAt = DateTime.Now.ToString("HH:mm:ss") });
            _entries.Add(new CollectedEntry { Nickname = "시소즈", BalloonCount = 200, CoinCount = 2, PinballName = "테스트2", ReceivedAt = DateTime.Now.ToString("HH:mm:ss") });
            RefreshPinballText();
        }

        private void SeedManyPreviewRows()
        {
            SeedManyPreviewRows(12);
        }

        private void SeedManyPreviewRows(int count)
        {
            for (int i = 1; i <= count; i++)
            {
                int balloons = i == 1 ? 100 : i * 100;
                _entries.Add(new CollectedEntry
                {
                    Nickname = "테스트" + i,
                    BalloonCount = balloons,
                    CoinCount = CalculateCoins(balloons),
                    PinballName = "종겜핀볼 테스트 " + i,
                    ReceivedAt = DateTime.Now.ToString("HH:mm:ss")
                });
            }

            RefreshPinballText();
        }

        private void AddStressEntries(int count)
        {
            for (int i = 1; i <= count; i++)
            {
                int balloons = i == 1 ? 100 : i * 100;
                var entry = new CollectedEntry();
                entry.Nickname = "테스트" + i;
                entry.BalloonCount = balloons;
                entry.CoinCount = CalculateCoins(balloons);
                entry.PinballName = "종겜핀볼 스트레스 " + i;
                entry.ReceivedAt = DateTime.Now.ToString("HH:mm:ss");
                AddCollectedEntry(entry);
            }

            ShowToast(count + "개 항목 테스트를 완료했습니다.");
        }

        private void RefreshModeButtons()
        {
            StyleSegment(_exactButton, _exactMode);
            StyleSegment(_atLeastButton, !_exactMode);
        }

        private void RefreshSourceButtons()
        {
            StyleSegment(_nicknameSourceButton, _nicknamePinballMode);
            StyleSegment(_contentSourceButton, !_nicknamePinballMode);
        }

        private void StyleSegment(RoundButton button, bool active)
        {
            if (button == null)
            {
                return;
            }

            if (active)
            {
                button.FillColor = _lavender;
                button.BorderColor = Color.FromArgb(132, 184, 255);
                button.ForeColor = _purpleDark;
            }
            else
            {
                button.FillColor = Color.FromArgb(251, 253, 255);
                button.BorderColor = _line;
                button.ForeColor = _muted;
            }
            button.Invalidate();
        }

        private void CopyPinballText()
        {
            string text = _pinballText.Text.Trim();
            if (text.Length == 0)
            {
                MessageBox.Show("복사할 핀볼 입력값이 없습니다.", "목록 없음", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Clipboard.SetText(text);
        }

        private void SavePinballText()
        {
            string text = _pinballText.Text.Trim();
            if (text.Length == 0)
            {
                MessageBox.Show("저장할 핀볼 입력값이 없습니다.", "목록 없음", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dialog = new SaveFileDialog();
            dialog.Title = "핀볼 입력값 저장";
            dialog.Filter = "Text file (*.txt)|*.txt";
            dialog.FileName = "pinball-list.txt";
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                File.WriteAllText(dialog.FileName, text, Encoding.UTF8);
            }
            dialog.Dispose();
        }

        private void OpenPinballSite()
        {
            try
            {
                string names = NormalizePinballNames(_pinballText.Text);
                if (names.Length == 0)
                {
                    Process.Start(PinballUrl);
                    return;
                }

                string url = BuildPinballSiteUrl(names);
                if (url.Length <= DirectPinballUrlLimit)
                {
                    Process.Start(url);
                    return;
                }

                Clipboard.SetText(names);
                Process.Start(PinballUrl);
                ShowToast("목록이 길어 클립보드에 복사했습니다. 핀볼 사이트에 붙여넣어 주세요.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "사이트 열기 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string BuildPinballSiteUrl()
        {
            string names = NormalizePinballNames(_pinballText.Text);
            return BuildPinballSiteUrl(names);
        }

        private string BuildPinballSiteUrl(string names)
        {
            if (names.Length == 0)
            {
                return PinballUrl;
            }

            return PinballUrl + "?names=" + Uri.EscapeDataString(names);
        }

        private string NormalizePinballNames(string value)
        {
            if (value == null)
            {
                return "";
            }

            string[] rawParts = value.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var parts = new List<string>();
            foreach (string rawPart in rawParts)
            {
                string part = rawPart.Trim();
                if (part.Length > 0)
                {
                    parts.Add(part);
                }
            }

            return String.Join(",", parts.ToArray());
        }

        private void ClearEntries()
        {
            if (_entries.Count == 0)
            {
                return;
            }

            using (var dialog = new ClearEntriesDialog(Font, _text, _muted, _purple, _line))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            if (_entries.Count == 0)
            {
                return;
            }

            _entries.Clear();
            RefreshPinballText();
            RefreshCounts();
        }

        private string ExtractStreamerId(string raw)
        {
            string trimmed = raw.Trim();
            Uri uri;
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out uri))
            {
                string[] parts = uri.AbsolutePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!Regex.IsMatch(parts[i], "^\\d+$"))
                    {
                        return parts[i];
                    }
                }
            }

            trimmed = trimmed.Replace("https://", "").Replace("http://", "");
            int slash = trimmed.IndexOf('/');
            if (slash >= 0)
            {
                trimmed = trimmed.Substring(0, slash);
            }

            return Regex.Replace(trimmed, "[^A-Za-z0-9_\\-]", "");
        }

        private string ResolveDisplayName(string streamerId)
        {
            if (streamerId == null || streamerId.Length == 0)
            {
                return "알 수 없음";
            }

            string key = streamerId.ToLowerInvariant();
            if (key == "dan259")
            {
                return "단즈_";
            }

            return streamerId;
        }

        private string SanitizePinballName(string value)
        {
            if (value == null)
            {
                return "";
            }

            string cleaned = value.Trim();
            cleaned = cleaned.Replace(",", " ");
            cleaned = cleaned.Replace("*", "x");
            cleaned = Regex.Replace(cleaned, "\\s+", " ");
            return cleaned;
        }

        private string GetConditionText()
        {
            return _exactMode ? "정확히 " + _thresholdInput.Value + "개" : _thresholdInput.Value + "개 이상";
        }

        private string GetConditionToastText()
        {
            int threshold = _thresholdInput.Value;
            return _exactMode ? "정확히 " + threshold + "개만 수집합니다." : threshold + "개 이상을 모두 수집합니다.";
        }

        private string BuildCoinGuideText()
        {
            int unit = GetCoinUnit();
            int bonusAt = unit * 10;
            return "수집된 내용을 직접 수정할 수 있습니다.\r\n기준 별풍선 " + unit + "개마다 1코인으로 반영됩니다.\r\n예 : " + unit + "개 = 1코인, " + bonusAt + "개 = 11코인";
        }

        private void RefreshCoinGuide()
        {
            if (_pinballDesc != null)
            {
                _pinballDesc.Text = BuildCoinGuideText();
            }
        }

        private RoundedPanel Card()
        {
            var panel = new RoundedPanel();
            panel.Radius = 16;
            panel.FillColor = _card;
            panel.BorderColor = Color.FromArgb(142, 177, 230);
            return panel;
        }

        private PillLabel StepBadge(string text)
        {
            var label = new PillLabel();
            label.Text = text;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Font = UiFont.Make(10.2f, FontStyle.Bold);
            label.ForeColor = _purpleDark;
            label.FillColor = _lavender;
            label.BorderColor = Color.FromArgb(180, 213, 255);
            label.Radius = 11;
            return label;
        }

        private Label PlainLabel(string text, float size, FontStyle style, Color color)
        {
            var label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.BackColor = Color.Transparent;
            label.ForeColor = color;
            label.Font = UiFont.Make(size, style);
            label.AutoEllipsis = true;
            return label;
        }

        private Label SmallHeader(string text)
        {
            return PlainLabel(text, 8.8f, FontStyle.Bold, _text);
        }

        private Label Eyebrow(string text)
        {
            return PlainLabel(text, 7.9f, FontStyle.Bold, Color.FromArgb(24, 103, 245));
        }

        private RoundButton BaseButton(string text)
        {
            var button = new RoundButton();
            button.Text = text;
            button.Font = UiFont.Make(9.2f, FontStyle.Bold);
            button.Radius = 9;
            button.CanvasColor = _card;
            return button;
        }

        private RoundButton PrimaryButton(string text)
        {
            var button = BaseButton(text);
            button.FillColor = Color.FromArgb(25, 106, 246);
            button.BorderColor = Color.FromArgb(25, 106, 246);
            button.ForeColor = Color.White;
            return button;
        }

        private RoundButton GradientButton(string text)
        {
            var button = BaseButton(text);
            button.UseGradient = true;
            button.FillColor = Color.FromArgb(46, 101, 238);
            button.GradientColor = Color.FromArgb(135, 111, 236);
            button.BorderColor = Color.FromArgb(111, 154, 255);
            button.ForeColor = Color.White;
            return button;
        }

        private RoundButton SecondaryButton(string text)
        {
            var button = BaseButton(text);
            button.FillColor = Color.FromArgb(251, 253, 255);
            button.BorderColor = _line;
            button.ForeColor = _muted;
            return button;
        }

        private RoundButton SegmentButton(string text)
        {
            var button = BaseButton(text);
            button.Font = UiFont.Make(8.9f, FontStyle.Bold);
            return button;
        }

        private RoundButton HeaderActionButton(string text, bool primary)
        {
            var button = BaseButton(text);
            button.Font = UiFont.Make(8.9f, FontStyle.Bold);
            button.Radius = 10;
            if (primary)
            {
                button.FillColor = Color.FromArgb(255, 239, 249);
                button.BorderColor = Color.FromArgb(244, 162, 208);
                button.ForeColor = Color.FromArgb(132, 38, 91);
            }
            else
            {
                button.FillColor = Color.FromArgb(251, 253, 255);
                button.BorderColor = _line;
                button.ForeColor = _muted;
            }

            return button;
        }

        private RoundButton MintButton(string text)
        {
            var button = BaseButton(text);
            button.FillColor = _mint;
            button.BorderColor = Color.FromArgb(244, 162, 208);
            button.ForeColor = Color.FromArgb(132, 38, 91);
            return button;
        }

        private RoundButton LinkButton(string text)
        {
            var button = BaseButton(text);
            button.FillColor = _card;
            button.BorderColor = _card;
            button.ForeColor = _muted;
            return button;
        }

    }

    internal sealed class SoopLiveInfo
    {
        public string StreamerId;
        public string StreamerName;
        public string Title;
        public string ChatNo;
        public string Token;
        public string Host;
        public int Port;
        public string Bps;
        public string GeoCc;
        public string GeoRc;
        public string AcceptLanguage;
        public string ServiceLanguage;
    }

    internal sealed class SoopLiveChatClient : IDisposable
    {
        private const string Fs = "\u000c";
        private const string Esc = "\u001b";
        private const string Tab = "\u0009";
        private const string CmdConnect = "\u001b\u0009000100000600\u000c\u000c\u000c16\u000c";

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private SoopLiveInfo _info;
        private bool _disposed;
        private bool _joinedRaised;

        public event Action<SoopLiveInfo> BroadcastResolved = delegate { };
        public event Action Joined = delegate { };
        public event Action<string, int> BalloonReceived = delegate { };
        public event Action<string, string> ChatReceived = delegate { };
        public event Action<string> Disconnected = delegate { };

        public async Task ConnectAsync(string streamerId, string password)
        {
            if (String.IsNullOrWhiteSpace(streamerId))
            {
                throw new InvalidOperationException("SOOP ID가 비어 있습니다.");
            }

            _disposed = false;
            _joinedRaised = false;
            _cts = new CancellationTokenSource();
            _info = await LoadLiveInfoAsync(streamerId.Trim(), password ?? "");
            BroadcastResolved(_info);

            string wsUrl = "wss://" + _info.Host.ToLowerInvariant() + ":" + _info.Port + "/Websocket/" + _info.StreamerId;
            _socket = new ClientWebSocket();
            _socket.Options.AddSubProtocol("chat");
            await _socket.ConnectAsync(new Uri(wsUrl), _cts.Token);
            await SendRawAsync(CmdConnect);
            StartBackgroundLoops();
        }

        public void Dispose()
        {
            _disposed = true;
            try
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                }
            }
            catch
            {
            }

            try
            {
                if (_socket != null)
                {
                    _socket.Abort();
                    _socket.Dispose();
                }
            }
            catch
            {
            }
        }

        private void StartBackgroundLoops()
        {
            Task.Run((Func<Task>)ReceiveLoopAsync);
            Task.Run((Func<Task>)PingLoopAsync);
        }

        private async Task<SoopLiveInfo> LoadLiveInfoAsync(string streamerId, string password)
        {
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072;

            string json = await Task.Run(delegate
            {
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0";
                    client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    var values = new NameValueCollection();
                    values["bid"] = streamerId;
                    values["type"] = "live";
                    values["pwd"] = password ?? "";
                    values["player_type"] = "html5";
                    values["stream_type"] = "common";
                    values["mode"] = "landing";
                    values["from_api"] = "0";
                    byte[] response = client.UploadValues("https://live.sooplive.co.kr/afreeca/player_live_api.php?bjid=" + Uri.EscapeDataString(streamerId), "POST", values);
                    return Encoding.UTF8.GetString(response);
                }
            });

            var root = _serializer.DeserializeObject(json) as Dictionary<string, object>;
            Dictionary<string, object> channel = GetObject(root, "CHANNEL");
            if (channel == null)
            {
                throw new InvalidOperationException("SOOP 방송 정보를 읽지 못했습니다.");
            }

            int result = GetInt(channel, "RESULT");
            if (result != 1)
            {
                throw new InvalidOperationException(streamerId + " 방송이 현재 라이브 상태가 아닙니다.");
            }

            string host = GetString(channel, "CHDOMAIN");
            if (host.Length == 0)
            {
                host = GetString(channel, "CHIP");
            }

            int chatPort = GetInt(channel, "CHPT") + 1;
            if (host.Length == 0 || chatPort <= 1)
            {
                throw new InvalidOperationException("채팅 서버 정보를 찾지 못했습니다.");
            }

            var info = new SoopLiveInfo();
            info.StreamerId = GetString(channel, "BJID");
            if (info.StreamerId.Length == 0)
            {
                info.StreamerId = streamerId;
            }
            info.StreamerName = GetString(channel, "BJNICK");
            if (info.StreamerName.Length == 0)
            {
                info.StreamerName = info.StreamerId;
            }
            info.Title = GetString(channel, "TITLE");
            info.ChatNo = GetString(channel, "CHATNO");
            info.Token = GetString(channel, "FTK");
            info.Host = host;
            info.Port = chatPort;
            info.Bps = GetString(channel, "BPS");
            info.GeoCc = GetString(channel, "geo_cc");
            info.GeoRc = GetString(channel, "geo_rc");
            info.AcceptLanguage = GetString(channel, "acpt_lang");
            info.ServiceLanguage = GetString(channel, "svc_lang");
            return info;
        }

        private async Task ReceiveLoopAsync()
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (!_disposed && _socket != null && _socket.State == WebSocketState.Open)
                {
                    using (var stream = new MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                RaiseDisconnected("채팅 서버가 연결을 종료했습니다.");
                                return;
                            }

                            stream.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        byte[] packet = stream.ToArray();
                        if (packet.Length > 0)
                        {
                            await ProcessPacketAsync(packet);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                RaiseDisconnected(ex.Message);
            }
        }

        private async Task PingLoopAsync()
        {
            try
            {
                while (!_disposed && _cts != null && !_cts.IsCancellationRequested)
                {
                    await Task.Delay(60000, _cts.Token);
                    await SendPacketAsync(0, Fs);
                }
            }
            catch
            {
            }
        }

        private async Task ProcessPacketAsync(byte[] packet)
        {
            if (packet.Length < 14)
            {
                return;
            }

            string header = Encoding.UTF8.GetString(packet, 0, 14);
            int serviceCommand;
            if (!Int32.TryParse(header.Substring(2, 4), out serviceCommand))
            {
                return;
            }

            int bodyStart = packet.Length > 15 ? 15 : 14;
            string body = packet.Length > bodyStart ? Encoding.UTF8.GetString(packet, bodyStart, packet.Length - bodyStart) : "";
            string[] parts = body.Split('\u000c');

            if (serviceCommand == 1)
            {
                await SendJoinAsync();
                return;
            }

            if (serviceCommand == 2)
            {
                if (parts.Length > 0 && parts[0] == "비밀번호가 틀렸습니다.")
                {
                    RaiseDisconnected("비밀번호가 틀렸습니다.");
                    return;
                }

                if (!_joinedRaised)
                {
                    _joinedRaised = true;
                    Joined();
                }
                return;
            }

            if (serviceCommand == 5)
            {
                if (parts.Length >= 6)
                {
                    string message = parts[0];
                    string nickname = parts[5];
                    if (nickname.Length > 0 && message.Length > 0)
                    {
                        ChatReceived(nickname, message);
                    }
                }
                return;
            }

            if (serviceCommand == 18)
            {
                if (parts.Length >= 4)
                {
                    int count;
                    if (Int32.TryParse(parts[3], out count) && count > 0)
                    {
                        BalloonReceived(parts[2], count);
                    }
                }
                return;
            }

            if (serviceCommand == 87)
            {
                if (parts.Length >= 10)
                {
                    string nickname = parts[3];
                    if (nickname.Length == 0)
                    {
                        nickname = parts[2];
                    }

                    int count;
                    if (nickname.Length > 0 && Int32.TryParse(parts[9], out count) && count > 0)
                    {
                        BalloonReceived(nickname, count);
                    }
                }
                return;
            }

            if (serviceCommand == 121)
            {
                if (parts.Length > 0)
                {
                    HandleChallengeGift(parts[0]);
                }
                return;
            }

            if (serviceCommand == 88)
            {
                RaiseDisconnected("방송이 종료되었습니다.");
            }
        }

        private void HandleChallengeGift(string json)
        {
            if (String.IsNullOrWhiteSpace(json))
            {
                return;
            }

            Dictionary<string, object> gift;
            try
            {
                gift = _serializer.DeserializeObject(json) as Dictionary<string, object>;
            }
            catch
            {
                return;
            }

            if (gift == null)
            {
                return;
            }

            if (!String.Equals(GetString(gift, "type"), "CHALLENGE_GIFT", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string nickname = GetString(gift, "user_nick");
            if (nickname.Length == 0)
            {
                nickname = GetString(gift, "user_id");
            }

            int count = GetInt(gift, "gift_count");
            if (nickname.Length > 0 && count > 0)
            {
                BalloonReceived(nickname, count);
            }
        }

        private async Task SendJoinAsync()
        {
            if (_info == null)
            {
                return;
            }

            var body = new StringBuilder();
            body.Append(Fs).Append(_info.ChatNo);
            body.Append(Fs).Append(_info.Token);
            body.Append(Fs).Append("0").Append(Fs).Append(Fs).Append("log\u0011");
            body.Append("\u0006&\u0006set_bps\u0006=\u0006").Append(_info.Bps);
            body.Append("\u0006&\u0006view_bps\u0006=\u0006").Append(_info.Bps);
            body.Append("\u0006&\u0006quality\u0006=\u0006ori");
            body.Append("\u0006&\u0006geo_cc\u0006=\u0006").Append(_info.GeoCc);
            body.Append("\u0006&\u0006geo_rc\u0006=\u0006").Append(_info.GeoRc);
            body.Append("\u0006&\u0006acpt_lang\u0006=\u0006").Append(_info.AcceptLanguage);
            body.Append("\u0006&\u0006svc_lang\u0006=\u0006").Append(_info.ServiceLanguage);
            body.Append("\u0006&\u0006subscribe\u0006=\u00060");
            body.Append("\u0006&\u0006lowlatency\u0006=\u00061");
            body.Append("\u0012pwd\u0011\u0012");
            body.Append("auth_info\u0011NULL\u0012");
            body.Append("pver\u00112\u0012");
            body.Append("access_system\u0011html5\u0012");
            body.Append(Fs);
            await SendPacketAsync(2, body.ToString());
        }

        private async Task SendPacketAsync(int serviceCommand, string body)
        {
            string header = Esc + Tab + serviceCommand.ToString("D4") + body.Length.ToString("D6") + "00";
            await SendRawAsync(header + body);
        }

        private async Task SendRawAsync(string text)
        {
            if (_disposed || _socket == null || _socket.State != WebSocketState.Open)
            {
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }

        private void RaiseDisconnected(string reason)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Disconnected(reason);
        }

        private static Dictionary<string, object> GetObject(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.ContainsKey(key))
            {
                return null;
            }

            return source[key] as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.ContainsKey(key) || source[key] == null)
            {
                return "";
            }

            return Convert.ToString(source[key]);
        }

        private static int GetInt(Dictionary<string, object> source, string key)
        {
            string value = GetString(source, key);
            int parsed;
            return Int32.TryParse(value, out parsed) ? parsed : 0;
        }
    }

    internal sealed class EntryRowControl : UserControl
    {
        public const int RowHeight = 64;

        private readonly CollectedEntry _entry;
        private int _index;
        private readonly Color _text;
        private readonly Color _muted;
        private readonly Color _purple;
        private readonly Color _lavender;
        private readonly Color _line;
        private readonly Color _card;
        private readonly Color _editFill = Color.FromArgb(248, 252, 255);
        private readonly Color _editBorder = Color.FromArgb(184, 210, 250);

        private readonly PillLabel _indexBadge;
        private readonly RoundedPanel _editFrame;
        private readonly TextBox _nameBox;
        private readonly Label _metaLabel;
        private readonly RoundedPanel _coinFrame;
        private readonly TextBox _coinBox;
        private readonly Label _coinSuffixLabel;
        private readonly Label _deleteLabel;
        private bool _syncingEntryValues;

        public event EventHandler EntryChanged;
        public event EventHandler DeleteClicked;
        public event EventHandler BlankClicked;

        public CollectedEntry Entry { get { return _entry; } }
        public bool IsEditing { get { return (_nameBox != null && _nameBox.Focused) || (_coinBox != null && _coinBox.Focused); } }

        public EntryRowControl(CollectedEntry entry, int index, Color text, Color muted, Color purple, Color lavender, Color line, Color card)
        {
            _entry = entry;
            _index = index;
            _text = text;
            _muted = muted;
            _purple = purple;
            _lavender = lavender;
            _line = line;
            _card = card;

            BackColor = _card;
            Height = RowHeight;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            _indexBadge = new PillLabel();
            _indexBadge.Text = _index.ToString();
            _indexBadge.Font = UiFont.Make(8.6f, FontStyle.Bold);
            _indexBadge.ForeColor = _purple;
            _indexBadge.FillColor = _lavender;
            _indexBadge.BorderColor = _lavender;
            _indexBadge.Radius = 8;
            Controls.Add(_indexBadge);

            _editFrame = new RoundedPanel();
            _editFrame.Radius = 7;
            _editFrame.FillColor = _editFill;
            _editFrame.BorderColor = _editBorder;
            _editFrame.BackColor = _card;
            Controls.Add(_editFrame);

            _nameBox = new TextBox();
            _nameBox.BorderStyle = BorderStyle.None;
            _nameBox.BackColor = _editFill;
            _nameBox.ForeColor = _text;
            _nameBox.Font = UiFont.Make(9.8f, FontStyle.Bold);
            _nameBox.Text = _entry.PinballName;
            _nameBox.TextChanged += delegate
            {
                _entry.PinballName = _nameBox.Text;
                if (EntryChanged != null)
                {
                    EntryChanged(this, EventArgs.Empty);
                }
            };
            _nameBox.GotFocus += delegate
            {
                _editFrame.BorderColor = _purple;
                _editFrame.Invalidate();
            };
            _nameBox.LostFocus += delegate
            {
                _editFrame.BorderColor = _editBorder;
                _editFrame.Invalidate();
            };
            Controls.Add(_nameBox);
            _nameBox.BringToFront();

            _metaLabel = new Label();
            _metaLabel.AutoSize = false;
            _metaLabel.BackColor = _card;
            _metaLabel.ForeColor = _muted;
            _metaLabel.Font = UiFont.Make(8.2f, FontStyle.Regular);
            _metaLabel.Text = _entry.Nickname + " · 별풍선 " + _entry.BalloonCount + "개";
            Controls.Add(_metaLabel);

            _coinFrame = new RoundedPanel();
            _coinFrame.Radius = 7;
            _coinFrame.FillColor = _lavender;
            _coinFrame.BorderColor = Color.FromArgb(139, 184, 255);
            _coinFrame.BackColor = _card;
            _coinFrame.Cursor = Cursors.IBeam;
            Controls.Add(_coinFrame);

            _coinBox = new TextBox();
            _coinBox.BorderStyle = BorderStyle.None;
            _coinBox.BackColor = _lavender;
            _coinBox.ForeColor = Color.FromArgb(13, 49, 133);
            _coinBox.Font = UiFont.Make(8.1f, FontStyle.Bold);
            _coinBox.TextAlign = HorizontalAlignment.Right;
            _coinBox.Text = _entry.CoinCount.ToString();
            _coinBox.TextChanged += delegate
            {
                if (_syncingEntryValues)
                {
                    return;
                }

                int coins;
                if (Int32.TryParse(_coinBox.Text.Trim(), out coins) && coins > 0)
                {
                    if (coins > 9999)
                    {
                        coins = 9999;
                        _syncingEntryValues = true;
                        _coinBox.Text = coins.ToString();
                        _coinBox.SelectionStart = _coinBox.TextLength;
                        _syncingEntryValues = false;
                    }

                    _entry.CoinCount = coins;
                    if (EntryChanged != null)
                    {
                        EntryChanged(this, EventArgs.Empty);
                    }
                }
            };
            _coinBox.KeyPress += delegate(object sender, KeyPressEventArgs e)
            {
                if (!Char.IsControl(e.KeyChar) && !Char.IsDigit(e.KeyChar))
                {
                    e.Handled = true;
                }
            };
            _coinBox.GotFocus += delegate
            {
                _coinFrame.BorderColor = _purple;
                _coinFrame.Invalidate();
                MoveCoinCursorToEnd();
            };
            _coinBox.MouseUp += delegate
            {
                MoveCoinCursorToEnd();
            };
            _coinBox.LostFocus += delegate
            {
                if (_coinBox.Text.Trim().Length == 0 || _entry.CoinCount <= 0)
                {
                    _entry.CoinCount = 1;
                }

                _syncingEntryValues = true;
                _coinBox.Text = _entry.CoinCount.ToString();
                _syncingEntryValues = false;
                _coinFrame.BorderColor = Color.FromArgb(139, 184, 255);
                _coinFrame.Invalidate();
                if (EntryChanged != null)
                {
                    EntryChanged(this, EventArgs.Empty);
                }
            };
            Controls.Add(_coinBox);
            _coinBox.BringToFront();

            _coinSuffixLabel = new Label();
            _coinSuffixLabel.Text = "코인";
            _coinSuffixLabel.AutoSize = false;
            _coinSuffixLabel.TextAlign = ContentAlignment.MiddleLeft;
            _coinSuffixLabel.BackColor = _lavender;
            _coinSuffixLabel.ForeColor = Color.FromArgb(13, 49, 133);
            _coinSuffixLabel.Font = UiFont.Make(8.1f, FontStyle.Bold);
            _coinSuffixLabel.Cursor = Cursors.IBeam;
            Controls.Add(_coinSuffixLabel);
            _coinSuffixLabel.BringToFront();

            _deleteLabel = new Label();
            _deleteLabel.Text = "×";
            _deleteLabel.AutoSize = false;
            _deleteLabel.TextAlign = ContentAlignment.MiddleCenter;
            _deleteLabel.BackColor = _card;
            _deleteLabel.ForeColor = _muted;
            _deleteLabel.Font = UiFont.Make(9.2f, FontStyle.Regular);
            _deleteLabel.Cursor = Cursors.Hand;
            _deleteLabel.Click += delegate
            {
                if (DeleteClicked != null)
                {
                    DeleteClicked(this, EventArgs.Empty);
                }
            };
            Controls.Add(_deleteLabel);

            MouseDown += RaiseBlankClicked;
            _indexBadge.MouseDown += RaiseBlankClicked;
            _editFrame.MouseDown += RaiseBlankClicked;
            _metaLabel.MouseDown += RaiseBlankClicked;
            _coinFrame.MouseDown += delegate { _coinBox.Focus(); };
            _coinSuffixLabel.MouseDown += delegate { _coinBox.Focus(); };
            _deleteLabel.MouseDown += RaiseBlankClicked;

            Resize += delegate { LayoutChildren(); };
            LayoutChildren();
        }

        private void RaiseBlankClicked(object sender, MouseEventArgs e)
        {
            if (BlankClicked != null)
            {
                BlankClicked(this, EventArgs.Empty);
            }
        }

        public void SetIndex(int index)
        {
            if (_index == index)
            {
                return;
            }

            _index = index;
            _indexBadge.Text = _index.ToString();
            _indexBadge.Invalidate();
        }

        public void RefreshEntryValues()
        {
            _metaLabel.Text = _entry.Nickname + " · 별풍선 " + _entry.BalloonCount + "개";
            if (!_coinBox.Focused && _coinBox.Text != _entry.CoinCount.ToString())
            {
                _syncingEntryValues = true;
                _coinBox.Text = _entry.CoinCount.ToString();
                _syncingEntryValues = false;
            }
            LayoutChildren();
            Invalidate();
        }

        private void LayoutChildren()
        {
            int badge = 26;
            int left = 0;
            int editX = left + badge + 8;
            int contentX = editX + 8;
            int deleteW = 28;
            int right = Width - deleteW - 10;
            int contentW = Math.Max(80, right - editX - 6);
            int coinW = _entry.CoinCount >= 1000 ? 82 : (_entry.CoinCount >= 100 ? 74 : 62);

            _indexBadge.SetBounds(left, 7, badge, badge);
            _deleteLabel.SetBounds(Width - deleteW - 4, 18, deleteW, 28);
            int editW = Math.Max(90, contentW + 4);
            int coinX = editX + editW - coinW - 6;
            int metaX = contentX - 4;
            int metaW = Math.Max(80, coinX - metaX - 8);
            _editFrame.SetBounds(editX, 6, editW, 28);
            _nameBox.SetBounds(contentX, 12, Math.Max(40, editW - 18), 18);
            _metaLabel.SetBounds(metaX, 40, metaW, 18);
            _coinFrame.SetBounds(coinX, 39, coinW, 20);
            int suffixW = 30;
            _coinSuffixLabel.SetBounds(coinX + coinW - suffixW - 6, 40, suffixW, 18);
            _coinBox.SetBounds(coinX + 6, 41, Math.Max(18, coinW - suffixW - 14), 15);

            if (Width < 360)
            {
                _coinFrame.Visible = false;
                _coinBox.Visible = false;
                _coinSuffixLabel.Visible = false;
                _metaLabel.SetBounds(metaX, 40, contentW, 18);
            }
            else
            {
                _coinFrame.Visible = true;
                _coinBox.Visible = true;
                _coinSuffixLabel.Visible = true;
            }
        }

        private void MoveCoinCursorToEnd()
        {
            if (_coinBox == null)
            {
                return;
            }

            Action move = delegate
            {
                if (!_coinBox.IsDisposed)
                {
                    _coinBox.SelectionStart = _coinBox.TextLength;
                    _coinBox.SelectionLength = 0;
                }
            };

            if (IsHandleCreated)
            {
                BeginInvoke(move);
            }
            else
            {
                move();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(_card))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            using (var pen = new Pen(_line))
            {
                e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }
    }

    internal sealed class ClearEntriesDialog : Form
    {
        private readonly Color _line;
        private readonly Color _surface;

        public ClearEntriesDialog(Font baseFont, Color text, Color muted, Color purple, Color line)
        {
            _line = line;
            _surface = Color.FromArgb(249, 251, 255);

            Text = "목록 비우기";
            ClientSize = new Size(334, 152);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = _surface;
            Font = baseFont;
            Padding = new Padding(1);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            var close = new Label();
            close.Text = "×";
            close.AutoSize = false;
            close.TextAlign = ContentAlignment.MiddleCenter;
            close.ForeColor = muted;
            close.Font = UiFont.Make(11f, FontStyle.Regular);
            close.Cursor = Cursors.Hand;
            close.SetBounds(ClientSize.Width - 42, 18, 24, 24);
            close.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(close);

            var title = new Label();
            title.Text = "수집 목록을 비울까요?";
            title.AutoSize = false;
            title.BackColor = Color.Transparent;
            title.ForeColor = text;
            title.Font = UiFont.Make(13.2f, FontStyle.Bold);
            title.SetBounds(24, 22, 250, 28);
            Controls.Add(title);

            var body = new Label();
            body.Text = "목록과 핀볼 입력값이 함께 비워집니다.";
            body.AutoSize = false;
            body.BackColor = Color.Transparent;
            body.ForeColor = Color.FromArgb(79, 90, 118);
            body.Font = UiFont.Make(9.1f, FontStyle.Regular);
            body.SetBounds(24, 52, 276, 34);
            Controls.Add(body);

            var cancel = DialogButton("취소", muted, Color.White, line);
            cancel.SetBounds(142, 98, 82, 36);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            var clear = DialogButton("비우기", Color.White, purple, purple);
            clear.SetBounds(234, 98, 78, 36);
            clear.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(clear);

            Resize += delegate { ApplyWindowRegion(); };
            ApplyWindowRegion();
        }

        private RoundButton DialogButton(string text, Color foreColor, Color fillColor, Color borderColor)
        {
            var button = new RoundButton();
            button.Text = text;
            button.ForeColor = foreColor;
            button.FillColor = fillColor;
            button.BorderColor = borderColor;
            button.CanvasColor = _surface;
            button.Radius = 11;
            button.Font = UiFont.Make(9.2f, FontStyle.Bold);
            return button;
        }

        private void ApplyWindowRegion()
        {
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            using (GraphicsPath path = Shape.Rounded(new Rectangle(0, 0, Width, Height), 18))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null)
                {
                    previous.Dispose();
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shape.Rounded(rect, 18))
            using (SolidBrush brush = new SolidBrush(_surface))
            using (Pen pen = new Pen(_line))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal sealed class FocusSinkPanel : Panel
    {
        public FocusSinkPanel()
        {
            SetStyle(ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            TabStop = false;
            BackColor = Color.Transparent;
        }
    }

    internal sealed class ToastBanner : Control
    {
        public ToastBanner()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = UiFont.Make(9.0f, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle shadowRect = new Rectangle(2, 3, Width - 5, Height - 5);
            Rectangle bodyRect = new Rectangle(0, 0, Width - 5, Height - 6);

            using (GraphicsPath shadowPath = Shape.Rounded(shadowRect, 12))
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(58, 4, 8, 24)))
            {
                e.Graphics.FillPath(shadow, shadowPath);
            }

            using (GraphicsPath bodyPath = Shape.Rounded(bodyRect, 12))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(10, 20, 42)))
            using (Pen border = new Pen(Color.FromArgb(69, 135, 255)))
            {
                e.Graphics.FillPath(fill, bodyPath);
                e.Graphics.DrawPath(border, bodyPath);
            }

            using (SolidBrush accent = new SolidBrush(Color.FromArgb(71, 146, 255)))
            {
                e.Graphics.FillEllipse(accent, bodyRect.X + 15, bodyRect.Y + ((bodyRect.Height - 8) / 2), 8, 8);
            }

            Rectangle textRect = new Rectangle(bodyRect.X + 40, bodyRect.Y + 1, bodyRect.Width - 56, bodyRect.Height - 2);

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textRect,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    internal sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            DoubleBuffered = true;
        }
    }

    internal sealed class VerticalScrollPanel : Panel
    {
        private const int SB_HORZ = 0;

        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        public VerticalScrollPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            DoubleBuffered = true;
            AutoScroll = true;
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            HideHorizontalScroll();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            HideHorizontalScroll();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x5 || m.Msg == 0xf || m.Msg == 0x85)
            {
                HideHorizontalScroll();
            }
        }

        private void HideHorizontalScroll()
        {
            HorizontalScroll.Enabled = false;
            HorizontalScroll.Maximum = 0;
            if (IsHandleCreated)
            {
                ShowScrollBar(Handle, SB_HORZ, false);
            }
        }
    }

    internal sealed class GradientSurface : Panel
    {
        public Color TopColor { get; set; }
        public Color BottomColor { get; set; }

        public GradientSurface()
        {
            TopColor = Color.White;
            BottomColor = Color.White;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using (var brush = new LinearGradientBrush(bounds, TopColor, BottomColor, LinearGradientMode.ForwardDiagonal))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }

            using (var dot = new SolidBrush(Color.FromArgb(24, 92, 139, 230)))
            {
                for (int x = 12; x < Width; x += 28)
                {
                    for (int y = 8; y < Height + Math.Abs(AutoScrollPosition.Y); y += 28)
                    {
                        e.Graphics.FillEllipse(dot, x, y + AutoScrollPosition.Y, 2, 2);
                    }
                }
            }
        }
    }

    internal class RoundedPanel : Panel
    {
        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }

        public RoundedPanel()
        {
            Radius = 16;
            FillColor = Color.White;
            BorderColor = Color.FromArgb(187, 210, 248);
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shape.Rounded(rect, Radius))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal sealed class PillLabel : Label
    {
        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }

        public PillLabel()
        {
            Radius = 12;
            FillColor = Color.White;
            BorderColor = Color.White;
            BackColor = Color.Transparent;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shape.Rounded(rect, Radius))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, rect, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    internal sealed class CreditBadge : Control
    {
        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }
        public Color MarkStartColor { get; set; }
        public Color MarkEndColor { get; set; }

        public CreditBadge()
        {
            Radius = 10;
            FillColor = Color.FromArgb(22, 38, 72);
            BorderColor = Color.FromArgb(53, 82, 132);
            MarkStartColor = Color.FromArgb(247, 151, 199);
            MarkEndColor = Color.FromArgb(43, 145, 238);
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shape.Rounded(rect, Radius))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            int markSize = Math.Min(8, Math.Max(7, Height - 11));
            Rectangle markRect = new Rectangle(7, (Height - markSize) / 2, markSize, markSize);
            using (LinearGradientBrush markBrush = new LinearGradientBrush(markRect, MarkStartColor, MarkEndColor, LinearGradientMode.ForwardDiagonal))
            {
                e.Graphics.FillEllipse(markBrush, markRect);
            }

            using (SolidBrush shine = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
            {
                e.Graphics.FillEllipse(shine, markRect.X + 2, markRect.Y + 2, 2, 2);
            }

            Rectangle textRect = new Rectangle(markRect.Right + 4, -1, Width - markRect.Right - 5, Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textRect,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class GradientBadge : Control
    {
        public Color StartColor { get; set; }
        public Color EndColor { get; set; }

        public GradientBadge()
        {
            StartColor = Color.White;
            EndColor = Color.White;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shape.Rounded(rect, 12))
            using (LinearGradientBrush brush = new LinearGradientBrush(rect, StartColor, EndColor, LinearGradientMode.ForwardDiagonal))
            {
                e.Graphics.FillPath(brush, path);
            }

            TextRenderer.DrawText(e.Graphics, Text, Font, rect, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    internal sealed class ImageBadge : Control
    {
        public Image Image { get; set; }
        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }

        public ImageBadge()
        {
            Radius = 12;
            FillColor = Color.White;
            BorderColor = Color.FromArgb(187, 210, 248);
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shape.Rounded(rect, Radius))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.FillPath(brush, path);

                GraphicsState state = e.Graphics.Save();
                e.Graphics.SetClip(path);
                if (Image != null)
                {
                    Rectangle imageRect = Cover(Image.Size, rect);
                    e.Graphics.DrawImage(Image, imageRect);
                }
                e.Graphics.Restore(state);

                e.Graphics.DrawPath(pen, path);
            }
        }

        private static Rectangle Cover(Size imageSize, Rectangle bounds)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return bounds;
            }

            float scale = Math.Max((float)bounds.Width / imageSize.Width, (float)bounds.Height / imageSize.Height);
            int width = (int)Math.Ceiling(imageSize.Width * scale);
            int height = (int)Math.Ceiling(imageSize.Height * scale);
            int x = bounds.X + ((bounds.Width - width) / 2);
            int y = bounds.Y + ((bounds.Height - height) / 2);
            return new Rectangle(x, y, width, height);
        }
    }

    internal sealed class RoundButton : Control
    {
        private bool _hover;
        private bool _pressed;

        public int Radius { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }
        public Color GradientColor { get; set; }
        public Color CanvasColor { get; set; }
        public bool UseGradient { get; set; }

        public RoundButton()
        {
            Radius = 10;
            FillColor = Color.White;
            BorderColor = Color.FromArgb(187, 210, 248);
            GradientColor = Color.Empty;
            CanvasColor = Color.White;
            UseGradient = false;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint,
                true);
            BackColor = CanvasColor;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _pressed = true;
            Invalidate();
            Focus();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = FillColor;
            if (_pressed)
            {
                fill = Shift(fill, -14);
            }
            else if (_hover)
            {
                fill = Shift(fill, 8);
            }

            using (SolidBrush background = new SolidBrush(CanvasColor))
            {
                e.Graphics.FillRectangle(background, ClientRectangle);
            }

            using (GraphicsPath path = Shape.Rounded(rect, Radius))
            using (Pen pen = new Pen(BorderColor))
            {
                if (UseGradient && GradientColor != Color.Empty)
                {
                    using (LinearGradientBrush brush = new LinearGradientBrush(rect, fill, GradientColor, LinearGradientMode.Horizontal))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }
                else
                {
                    using (SolidBrush brush = new SolidBrush(fill))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }

                e.Graphics.DrawPath(pen, path);
            }

            DrawCenteredButtonText(e.Graphics, rect);
        }

        private void DrawCenteredButtonText(Graphics graphics, Rectangle rect)
        {
            Rectangle textRect = new Rectangle(rect.X + 2, rect.Y, Math.Max(1, rect.Width - 4), rect.Height);
            TextRenderer.DrawText(
                graphics,
                Text ?? "",
                Font,
                textRect,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        private static Color Shift(Color color, int amount)
        {
            return Color.FromArgb(Clamp(color.R + amount), Clamp(color.G + amount), Clamp(color.B + amount));
        }

        private static int Clamp(int value)
        {
            return Math.Max(0, Math.Min(255, value));
        }
    }

    internal class RoundTextBox : UserControl
    {
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        private const int EM_LINESCROLL = 0x00B6;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly TextBox _box;
        private bool _placeholderActive;
        private string _suffixText;

        public event EventHandler InnerTextChanged;

        public string Placeholder { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }
        public Color FocusBorderColor { get; set; }
        public Color SuffixColor { get; set; }
        public int Radius { get; set; }

        public string SuffixText
        {
            get { return _suffixText; }
            set
            {
                _suffixText = value ?? "";
                LayoutInner();
                Invalidate();
            }
        }

        public bool Multiline
        {
            get { return _box.Multiline; }
            set
            {
                _box.Multiline = value;
                _box.ScrollBars = value ? ScrollBars.Vertical : ScrollBars.None;
                LayoutInner();
            }
        }

        public override string Text
        {
            get { return _placeholderActive ? "" : _box.Text; }
            set
            {
                _placeholderActive = false;
                _box.ForeColor = Color.FromArgb(9, 17, 39);
                _box.Text = value;
                ApplyPlaceholder();
            }
        }

        public void SetTextPreserveView(string value)
        {
            value = value ?? "";
            bool hadFocus = _box.Focused;
            int firstVisibleLine = 0;
            if (_box.Multiline && _box.IsHandleCreated)
            {
                firstVisibleLine = SendMessage(_box.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
            }

            int selectionStart = Math.Max(0, Math.Min(_box.SelectionStart, value.Length));
            int selectionLength = Math.Max(0, Math.Min(_box.SelectionLength, value.Length - selectionStart));
            _placeholderActive = false;
            _box.ForeColor = Color.FromArgb(9, 17, 39);
            _box.Text = value;
            _box.SelectionStart = selectionStart;
            _box.SelectionLength = selectionLength;

            if (_box.Multiline && _box.IsHandleCreated)
            {
                int currentFirstLine = SendMessage(_box.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
                int delta = firstVisibleLine - currentFirstLine;
                if (delta != 0)
                {
                    SendMessage(_box.Handle, EM_LINESCROLL, IntPtr.Zero, new IntPtr(delta));
                }
            }

            if (hadFocus)
            {
                _box.Focus();
            }
            ApplyPlaceholder();
        }

        public void SetTextScrollToBottom(string value)
        {
            value = value ?? "";
            bool hadFocus = _box.Focused;
            _placeholderActive = false;
            _box.ForeColor = Color.FromArgb(9, 17, 39);
            _box.Text = value;
            if (_box.Multiline)
            {
                _box.SelectionStart = _box.TextLength;
                _box.SelectionLength = 0;
                _box.ScrollToCaret();
            }
            else
            {
                _box.SelectionStart = Math.Min(_box.TextLength, value.Length);
                _box.SelectionLength = 0;
            }
            if (hadFocus)
            {
                _box.Focus();
            }
            ApplyPlaceholder();
        }

        public RoundTextBox()
        {
            Placeholder = "";
            _suffixText = "";
            FillColor = Color.FromArgb(247, 250, 255);
            BorderColor = Color.FromArgb(180, 205, 242);
            FocusBorderColor = Color.FromArgb(37, 99, 235);
            SuffixColor = Color.FromArgb(68, 83, 111);
            Radius = 10;
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            _box = new TextBox();
            _box.BorderStyle = BorderStyle.None;
            _box.BackColor = FillColor;
            _box.ForeColor = Color.FromArgb(9, 17, 39);
            _box.Font = UiFont.Make(9.5f, FontStyle.Regular);
            _box.TextChanged += delegate
            {
                if (!_placeholderActive && InnerTextChanged != null)
                {
                    InnerTextChanged(this, EventArgs.Empty);
                }
            };
            _box.GotFocus += delegate
            {
                if (_placeholderActive)
                {
                    _box.Text = "";
                    _box.ForeColor = Color.FromArgb(9, 17, 39);
                    _placeholderActive = false;
                }
                Invalidate();
            };
            _box.LostFocus += delegate
            {
                ApplyPlaceholder();
                Invalidate();
            };
            Controls.Add(_box);

            Resize += delegate { LayoutInner(); };
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            ApplyPlaceholder();
            LayoutInner();
        }

        private void ApplyPlaceholder()
        {
            if (!Focused && !_box.Focused && _box.Text.Length == 0 && Placeholder.Length > 0)
            {
                _placeholderActive = true;
                _box.ForeColor = Color.FromArgb(124, 140, 174);
                _box.Text = Placeholder;
            }
        }

        private void LayoutInner()
        {
            int padX = 13;
            int padY = _box.Multiline ? 11 : Math.Max(6, (Height - _box.Font.Height) / 2);
            int suffixReserve = GetSuffixReserve();
            _box.BackColor = FillColor;
            _box.SetBounds(padX, padY, Math.Max(1, Width - (padX * 2) - suffixReserve), Math.Max(1, Height - (padY * 2)));
        }

        private int GetSuffixReserve()
        {
            if (String.IsNullOrEmpty(_suffixText))
            {
                return 0;
            }

            return Math.Max(24, TextRenderer.MeasureText(_suffixText, _box.Font).Width + 12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 1 || Height <= 1)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Shape.Rounded(rect, Radius))
            using (SolidBrush brush = new SolidBrush(FillColor))
            using (Pen pen = new Pen(_box.Focused ? FocusBorderColor : BorderColor))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            if (!String.IsNullOrEmpty(_suffixText))
            {
                int padX = 13;
                int suffixReserve = GetSuffixReserve();
                Rectangle suffixRect = new Rectangle(Width - padX - suffixReserve, 0, suffixReserve, Height - 1);
                TextRenderer.DrawText(e.Graphics, _suffixText, _box.Font, suffixRect, SuffixColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }

    internal sealed class NumberBox : RoundTextBox
    {
        public event EventHandler ValueChanged;

        public int Minimum { get; set; }
        public int Maximum { get; set; }

        public int Value
        {
            get
            {
                int value;
                if (!Int32.TryParse(Text, out value))
                {
                    value = Minimum;
                }
                return Math.Max(Minimum, Math.Min(Maximum, value));
            }
            set
            {
                Text = Math.Max(Minimum, Math.Min(Maximum, value)).ToString();
            }
        }

        public NumberBox()
        {
            Minimum = 0;
            Maximum = 30000;
            InnerTextChanged += delegate
            {
                string cleaned = Regex.Replace(Text, "[^0-9]", "");
                if (cleaned != Text)
                {
                    Text = cleaned;
                }
                if (ValueChanged != null)
                {
                    ValueChanged(this, EventArgs.Empty);
                }
            };
        }
    }

    internal static class Shape
    {
        public static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return path;
            }

            int safeRadius = Math.Max(1, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
            int diameter = Math.Max(1, safeRadius * 2);
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class PendingGift
    {
        public string Nickname { get; set; }
        public int BalloonCount { get; set; }
        public int CoinCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    internal sealed class CollectedEntry
    {
        public string Nickname { get; set; }
        public int BalloonCount { get; set; }
        public int CoinCount { get; set; }
        public string PinballName { get; set; }
        public string ReceivedAt { get; set; }
    }
}
