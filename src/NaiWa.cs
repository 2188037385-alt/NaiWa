// 奶蛙 NaiWa · Q 版像素桌宠 & 桌面效率助理（Windows）
// 截图（窗口识别 / 标注 / 取字 / 翻译 / 钉图）· 划词翻译与报告润色 · AI 聊天 · 自然语言提醒 · Claude Code 回复结束提醒
// 编译：仓库根目录的 build.ps1（只用 Windows 自带的 .NET Framework 4 编译器，无需安装 SDK）
// 许可：MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Shapes = System.Windows.Shapes;

[assembly: System.Reflection.AssemblyTitle("奶蛙助理")]
[assembly: System.Reflection.AssemblyProduct("NaiWa")]
[assembly: System.Reflection.AssemblyVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyDescription("Q 版像素桌宠 & 桌面效率助理")]

namespace NaiWa
{
    // ====================================================================
    // 主题：白底、大圆角、安静的灰字，鼠尾草绿做唯一主色（v2 视觉，按 PM 评审定稿）
    // ====================================================================
    static class Theme
    {
        public static readonly FontFamily Ui = new FontFamily("Segoe UI, Microsoft YaHei UI");   // 小字号用带屏幕微调的字体，ClearType 下更清晰
        public static readonly FontFamily Serif = Ui;   // v2 撤掉衬线标题，统一无衬线
        public static readonly FontFamily Icons = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        public static SolidColorBrush B(string hex)
        {
            var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
            b.Freeze();
            return b;
        }
        public static readonly SolidColorBrush Cream = B("#FFFFFF");      // 主卡片
        public static readonly SolidColorBrush White = B("#FFFFFF");
        public static readonly SolidColorBrush Soft = B("#F6F7F8");       // 次级区域、输入区、用户消息
        public static readonly SolidColorBrush Line = B("#E8EBED");
        public static readonly SolidColorBrush Title = B("#24282C");
        public static readonly SolidColorBrush Text = B("#41474D");
        public static readonly SolidColorBrush Muted = B("#667078");
        public static readonly SolidColorBrush Accent = B("#57715F");     // 鼠尾草绿
        public static readonly SolidColorBrush AccentHover = B("#48604F");
        public static readonly SolidColorBrush AccentSoft = B("#EDF3EF");
        public static readonly SolidColorBrush Gold = B("#D79538");       // 星芒、少量提示
        public static readonly SolidColorBrush Hover = B("#F1F3F4");
        public static readonly SolidColorBrush Press = B("#E6E9EB");
        public static readonly SolidColorBrush Clear = B("#01FFFFFF");
        public static readonly SolidColorBrush Danger = B("#B64C4C");
    }

    static class UiKit
    {
        public static void SetupPanel(Window w, double width)
        {
            w.WindowStyle = WindowStyle.None;
            w.AllowsTransparency = true;
            w.Background = Brushes.Transparent;
            w.ShowInTaskbar = false;
            w.ResizeMode = ResizeMode.NoResize;
            w.Topmost = true;
            if (width > 0) w.Width = width;
            w.UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(w, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(w, TextRenderingMode.ClearType);
        }

        // 外层阴影 + 不透明卡片（不透明才能让文字走 ClearType，不发糊）
        public static Grid Shell(out Border card, double radius)
        {
            var root = new Grid { Margin = new Thickness(16, 12, 16, 20) };
            var shadow = new Border
            {
                CornerRadius = new CornerRadius(radius),
                Background = Theme.Cream,
                Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Direction = 270, Opacity = 0.10, Color = Color.FromRgb(0x18, 0x20, 0x28) }
            };
            card = new Border
            {
                CornerRadius = new CornerRadius(radius),
                Background = Theme.Cream,
                BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1)
            };
            RenderOptions.SetClearTypeHint(card, ClearTypeHint.Enabled);
            root.Children.Add(shadow);
            root.Children.Add(card);
            return root;
        }

        public static TextBlock Label(string text, double size, Brush fg, bool serif)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = fg,
                FontFamily = serif ? Theme.Serif : Theme.Ui,
                TextWrapping = TextWrapping.Wrap
            };
        }

        public static Border Pill(string text, bool primary, Action click)
        {
            // 胶囊：高 32、圆角 16；主操作鼠尾草绿，其余浅灰
            var t = new TextBlock
            {
                Text = text,
                FontFamily = Theme.Ui,
                FontSize = 13,
                Foreground = primary ? Brushes.White : Theme.Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var normal = primary ? Theme.Accent : Theme.Hover;
            var hover = primary ? Theme.AccentHover : Theme.Press;
            var b = new Border
            {
                Child = t,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 0, 14, 1),
                Background = normal,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 6)
            };
            b.MouseEnter += (s, e) => { b.Background = hover; };
            b.MouseLeave += (s, e) => { b.Background = normal; };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            b.MouseLeftButtonUp += (s, e) => { if (click != null) { e.Handled = true; click(); } };
            return b;
        }

        // 改按钮文字（胶囊里的 TextBlock）
        public static void SetPillText(Border pill, string text)
        {
            var t = pill.Child as TextBlock;
            if (t != null) t.Text = text;
        }

        public static Border IconButton(string glyph, string tip, Action click)
        {
            // 圆形图标按钮：32×32、浅灰底
            var t = new TextBlock
            {
                Text = glyph,
                FontFamily = Theme.Icons,
                FontSize = 13,
                Foreground = Theme.Muted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var b = new Border
            {
                Child = t,
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = Theme.Clear,
                Cursor = Cursors.Hand,
                ToolTip = tip,
                Margin = new Thickness(2, 0, 0, 0)
            };
            b.MouseEnter += (s, e) => { b.Background = Theme.Hover; };
            b.MouseLeave += (s, e) => { b.Background = Theme.Clear; };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            b.MouseLeftButtonUp += (s, e) => { if (click != null) { e.Handled = true; click(); } };
            return b;
        }

        public static TextBox SelectableText(string text, Brush fg, double size)
        {
            return new TextBox
            {
                Text = text,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = fg,
                FontFamily = Theme.Ui,
                FontSize = size,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(0),
                Cursor = Cursors.IBeam
            };
        }

        public static Border InputBox(Control inner)
        {
            inner.BorderThickness = new Thickness(0);
            inner.Background = Brushes.Transparent;
            inner.FontFamily = Theme.Ui;
            inner.FontSize = 13;
            inner.Foreground = Theme.Text;
            inner.VerticalContentAlignment = VerticalAlignment.Center;
            return new Border
            {
                Child = inner,
                Background = Theme.Soft,
                BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 4, 0, 10)
            };
        }

        // 右键菜单：白底圆角卡片、行高 36、快捷键右对齐灰字
        static ResourceDictionary menuDict;
        public static ContextMenu StyledMenu()
        {
            if (menuDict == null)
            {
                const string xaml =
@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style x:Key='nwMenu' TargetType='ContextMenu'>
    <Setter Property='HasDropShadow' Value='False'/>
    <Setter Property='FontFamily' Value='Segoe UI, Microsoft YaHei UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Foreground' Value='#24282C'/>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='ContextMenu'>
        <Border Margin='6,4,14,16'>
          <Border.Effect><DropShadowEffect BlurRadius='18' ShadowDepth='4' Direction='270' Opacity='0.12' Color='#182028'/></Border.Effect>
          <Border Background='White' BorderBrush='#E8EBED' BorderThickness='1' CornerRadius='12' Padding='0,5' MinWidth='228'>
            <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>
          </Border>
        </Border>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style x:Key='{x:Static MenuItem.SeparatorStyleKey}' TargetType='Separator'>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='Separator'><Border Height='1' Background='#E8EBED' Margin='12,4,12,4'/></ControlTemplate>
    </Setter.Value></Setter>
  </Style>
  <Style TargetType='MenuItem'>
    <Setter Property='Template'><Setter.Value>
      <ControlTemplate TargetType='MenuItem'>
        <Grid>
          <Border x:Name='Bd' Height='36' CornerRadius='8' Background='Transparent' Margin='5,0,5,0' Padding='12,0,12,0'>
            <Grid>
              <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
              <ContentPresenter ContentSource='Header' VerticalAlignment='Center' RecognizesAccessKey='True'/>
              <TextBlock x:Name='Gest' Grid.Column='1' Text='{TemplateBinding InputGestureText}' Foreground='#667078' FontSize='12' Margin='28,0,0,0' VerticalAlignment='Center'/>
              <TextBlock x:Name='Arrow' Grid.Column='1' Text='›' Visibility='Collapsed' Foreground='#667078' FontSize='15' Margin='28,0,0,2' VerticalAlignment='Center'/>
            </Grid>
          </Border>
          <Popup x:Name='PART_Popup' Placement='Right' HorizontalOffset='-4' VerticalOffset='-9' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'
                 IsOpen='{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}'>
            <Border Margin='0,4,14,16'>
              <Border.Effect><DropShadowEffect BlurRadius='18' ShadowDepth='4' Direction='270' Opacity='0.12' Color='#182028'/></Border.Effect>
              <Border Background='White' BorderBrush='#E8EBED' BorderThickness='1' CornerRadius='12' Padding='0,5' MinWidth='120'>
                <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>
              </Border>
            </Border>
          </Popup>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Bd' Property='Background' Value='#F1F3F4'/></Trigger>
          <Trigger Property='Role' Value='SubmenuHeader'>
            <Setter TargetName='Arrow' Property='Visibility' Value='Visible'/>
            <Setter TargetName='Gest' Property='Visibility' Value='Collapsed'/>
          </Trigger>
          <Trigger Property='IsChecked' Value='True'><Setter Property='FontWeight' Value='SemiBold'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value></Setter>
  </Style>
</ResourceDictionary>";
                menuDict = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml);
            }
            var m = new ContextMenu();
            m.Resources.MergedDictionaries.Add(menuDict);
            m.Style = (Style)menuDict["nwMenu"];
            return m;
        }

        public static void Toast(string title, string text)
        {
            var app = Application.Current;
            if (app == null) return;
            var pet = app.Properties["pet"] as PetWindow;
            if (pet != null) pet.Say(text, 4);
        }
    }

    // ====================================================================
    // 像素奶蛙
    // ====================================================================
    static class SpriteData
    {
        // 左半边（16 列），右半边镜像生成
        public static readonly string[] Left =
        {
            "..KKKKK.........",
            ".KGGGGGK........",
            "KGGGGGGGK.......",
            "KGGGGGGGKKKKKKKK",
            "KGGGBBGGKYYYYYYY",
            "KGGGBBGGKYYYYYYY",
            "KGGGGGGGKYYYYYYY",
            ".KGGGGGKYYYYYYYY",
            "..KKKKKYYYYYYYYY",
            "..KYYYYYYYYYYYYY",
            "..KYYYYYYYYYYYYY",
            "..KYYYYYYYYKKKKK",
            ".KYYYYYYYYYYYYYY",
            ".KYYYYYYYYYYYYYY",
            ".KYYYYYLLLLLLLLL",
            "KYYYYLLLLLLLLLLL",
            "KYYYLLLLLLLLLLLL",
            "KYYHHLLLLLLLLLLL",
            "KYYHHHHLLLLLLLLL",
            "KYYYHHHHHLLLLLLL",
            "KYYYYYHHLLLLLLLL",
            "KYYYYLLLLLLLLLLL",
            "KYYYYLLLLLLLLLLL",
            ".KYYYYLLLLLLLLLL",
            ".KYYYYYLLLLLLLLL",
            "..KYYYYYYLLLLLLL",
            "..KKYYYYYYYYYYYY",
            ".KHHKKKKKKKKKKKK",
            ".KHHHHK.........",
            "..KKKK.........."
        };

        public static readonly Dictionary<string, string[]> Eyes = new Dictionary<string, string[]>
        {
            { "stare", new[] { "..KKKKK..", ".KGGGGGK.", "KGGGGGGGK", "KGGGGGGGK", "KGGGBBGGK", "KGGGBBGGK", "KGGGGGGGK", ".KGGGGGKY", "..KKKKKYY" } },
            { "blink", new[] { "..KKKKK..", ".KYYYYYK.", "KYYYYYYYK", "KYYYYYYYK", "KYKKKKKYK", "KYYYYYYYK", "KYYYYYYYK", ".KYYYYYKY", "..KKKKKYY" } },
            { "laugh", new[] { "..KKKKK..", ".KYYYYYK.", "KYYYYYYYK", "KYYKKKYYK", "KYKYYYKYK", "KYYYYYYYK", "KYYYYYYYK", ".KYYYYYKY", "..KKKKKYY" } },
            { "think", new[] { "..KKKKK..", ".KGGGGGK.", "KGGGGBBGK", "KGGGGBBGK", "KGGGGGGGK", "KGGGGGGGK", "KGGGGGGGK", ".KGGGGGKY", "..KKKKKYY" } },
            { "sleep", new[] { "..KKKKK..", ".KYYYYYK.", "KYYYYYYYK", "KYYYYYYYK", "KYYYYYYYK", "KYYYYYYYK", "KYKKKKKYK", ".KYYYYYKY", "..KKKKKYY" } }
        };

        public static readonly string[] SparkA = { "..C..", "C.C.C", ".CCC.", "C.C.C", "..C.." };
        public static readonly string[] SparkB = { "C...C", ".C.C.", "..C..", ".C.C.", "C...C" };

        public static string[] FrogRows(string face)
        {
            string eyeKey = face == "talk" ? "stare" : face;
            if (!Eyes.ContainsKey(eyeKey)) eyeKey = "stare";
            var half = Left.Select(r => r.ToCharArray()).ToArray();
            var e = Eyes[eyeKey];
            for (int r = 0; r < 9; r++)
                for (int c = 0; c < 9; c++)
                    half[r][c] = e[r][c];
            if (face == "laugh" || face == "talk")
            {
                for (int c = 9; c <= 15; c++) half[11][c] = 'K';
                half[12][9] = 'K';
                for (int c = 10; c <= 15; c++) half[12][c] = 'R';
                half[13][10] = 'K';
                for (int c = 11; c <= 15; c++) half[13][c] = face == "laugh" ? 'P' : 'K';
                if (face == "laugh") for (int c = 11; c <= 15; c++) half[14][c] = 'K';
            }
            var rows = new string[half.Length];
            for (int r = 0; r < half.Length; r++)
            {
                var l = new string(half[r]);
                var rc = (char[])half[r].Clone();
                Array.Reverse(rc);
                rows[r] = l + new string(rc);
            }
            return rows;
        }

    }

    static class Sprites
    {
        static readonly Dictionary<char, Color> Pal = new Dictionary<char, Color>
        {
            // v2 调色：轮廓更柔、身体奶白偏暖、手脚阴影改成灰绿，不再像污渍
            { 'K', Color.FromRgb(0x34, 0x3C, 0x34) },
            { 'Y', Color.FromRgb(0xF7, 0xEE, 0xC6) },
            { 'L', Color.FromRgb(0xFF, 0xFD, 0xF5) },
            { 'G', Color.FromRgb(0xD8, 0xE6, 0xC5) },
            { 'B', Color.FromRgb(0x1C, 0x22, 0x1C) },
            { 'H', Color.FromRgb(0x8E, 0x94, 0x71) },
            { 'R', Color.FromRgb(0x8E, 0x3B, 0x2E) },
            { 'P', Color.FromRgb(0xF2, 0x9A, 0x8E) },
            { 'C', Color.FromRgb(0xD7, 0x95, 0x38) }
        };

        static readonly Dictionary<string, BitmapSource> cache = new Dictionary<string, BitmapSource>();

        static BitmapSource Render(string[] rows)
        {
            int w = rows[0].Length, h = rows.Length;
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char ch = rows[y][x];
                    Color c;
                    if (ch == '.' || !Pal.TryGetValue(ch, out c)) continue;
                    int i = (y * w + x) * 4;
                    px[i] = c.B; px[i + 1] = c.G; px[i + 2] = c.R; px[i + 3] = 255;
                }
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
            bmp.Freeze();
            return bmp;
        }

        public static BitmapSource Frog(string face)
        {
            BitmapSource b;
            if (!cache.TryGetValue("f_" + face, out b)) { b = Render(SpriteData.FrogRows(face)); cache["f_" + face] = b; }
            return b;
        }

        public static BitmapSource Spark(int frame)
        {
            string key = "s_" + (frame % 2);
            BitmapSource b;
            if (!cache.TryGetValue(key, out b)) { b = Render(frame % 2 == 0 ? SpriteData.SparkA : SpriteData.SparkB); cache[key] = b; }
            return b;
        }

        public static Image View(BitmapSource src, double scale)
        {
            var img = new Image { Source = src, Width = src.PixelWidth * scale, Height = src.PixelHeight * scale, Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
            return img;
        }

        public static System.Drawing.Icon TrayIcon()
        {
            var rows = SpriteData.FrogRows("stare");
            var bmp = new System.Drawing.Bitmap(32, 32);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color c;
                    if (rows[y][x] == '.' || !Pal.TryGetValue(rows[y][x], out c)) continue;
                    bmp.SetPixel(x, y + 1, System.Drawing.Color.FromArgb(255, c.R, c.G, c.B));
                }
            return System.Drawing.Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ====================================================================
    // 设置（密钥用 Windows DPAPI 加密，只有当前 Windows 用户能解开）
    // ====================================================================
    public class Reminder
    {
        public long At;
        public string Text;
    }

    public class Settings
    {
        public string DeepSeekKeyEnc = "";
        public string DoubaoKeyEnc = "";
        public string DeepSeekModel = "deepseek-chat";
        public string DoubaoModel = "doubao-seed-1-6-250615";
        public string TextProvider = "auto";
        public bool Topmost = true;
        public bool AutoStart = false;
        public double PetLeft = -1;
        public double PetTop = -1;
        public double PetScale = 2;     // 像素放大倍数：2 小 / 3 中 / 4 大（默认小）
        public bool Onboarded = false;
        public List<Reminder> Reminders = new List<Reminder>();

        [ScriptIgnore]
        public string DeepSeekKey { get { return Unprotect(DeepSeekKeyEnc); } set { DeepSeekKeyEnc = Protect(value); } }
        [ScriptIgnore]
        public string DoubaoKey { get { return Unprotect(DoubaoKeyEnc); } set { DoubaoKeyEnc = Protect(value); } }

        public static readonly string Dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);   // 设置、日志都放在程序自己的文件夹
        static string FilePath { get { return Path.Combine(Dir, "settings.json"); } }
        public static Settings Current;

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    Current = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FilePath, Encoding.UTF8));
            }
            catch (Exception ex) { Program.Log("读取设置失败: " + ex.Message); }
            if (Current == null) Current = new Settings();
            if (Current.Reminders == null) Current.Reminders = new List<Reminder>();
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(Current), new UTF8Encoding(false));
            }
            catch (Exception ex) { Program.Log("保存设置失败: " + ex.Message); }
        }

        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NaiWa.v1");
        static string Protect(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), Entropy, DataProtectionScope.CurrentUser));
        }
        static string Unprotect(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), Entropy, DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }
    }

    // ====================================================================
    // 大模型：DeepSeek / 豆包（火山方舟），均为 OpenAI 兼容接口，流式输出
    // ====================================================================
    static class Ai
    {
        public static bool HasDeepSeek { get { return !string.IsNullOrEmpty(Settings.Current.DeepSeekKey); } }
        public static bool HasDoubao { get { return !string.IsNullOrEmpty(Settings.Current.DoubaoKey); } }

        public static string PickText()
        {
            var s = Settings.Current;
            if (s.TextProvider == "doubao" && HasDoubao) return "doubao";
            if (s.TextProvider == "deepseek" && HasDeepSeek) return "deepseek";
            if (HasDeepSeek) return "deepseek";
            if (HasDoubao) return "doubao";
            return null;
        }

        public static string Display(string provider) { return provider == "deepseek" ? "DeepSeek" : "豆包"; }

        public static Dictionary<string, object> Msg(string role, object content)
        {
            return new Dictionary<string, object> { { "role", role }, { "content", content } };
        }

        public static object WithImage(string text, string dataUrl)
        {
            return new object[]
            {
                new Dictionary<string, object> { { "type", "image_url" }, { "image_url", new Dictionary<string, object> { { "url", dataUrl } } } },
                new Dictionary<string, object> { { "type", "text" }, { "text", text } }
            };
        }

        public static Task Stream(string provider, List<Dictionary<string, object>> messages, Action<string> onDelta, CancellationToken ct, string keyOverride = null, string modelOverride = null)
        {
            return Task.Run(() =>
            {
                var s = Settings.Current;
                bool ds = provider == "deepseek";
                string key = keyOverride ?? (ds ? s.DeepSeekKey : s.DoubaoKey);
                string model = modelOverride ?? (ds ? s.DeepSeekModel : s.DoubaoModel);
                if (string.IsNullOrEmpty(key)) throw new Exception("还没有填" + Display(provider) + "的密钥，右键奶蛙 →「设置」里填一下吧。");
                if (string.IsNullOrEmpty(model)) throw new Exception(Display(provider) + "的模型 ID 是空的，请在设置里填写。");
                string url = ds ? "https://api.deepseek.com/chat/completions" : "https://ark.cn-beijing.volces.com/api/v3/chat/completions";

                var body = new Dictionary<string, object> { { "model", model }, { "messages", messages }, { "stream", true } };
                if (!ds && model.StartsWith("doubao-seed", StringComparison.OrdinalIgnoreCase))
                    body["thinking"] = new Dictionary<string, object> { { "type", "disabled" } };
                var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                byte[] bytes = Encoding.UTF8.GetBytes(ser.Serialize(body));

                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Accept = "text/event-stream";
                req.Headers["Authorization"] = "Bearer " + key;
                req.Timeout = 120000;
                req.ReadWriteTimeout = 120000;
                req.ContentLength = bytes.Length;
                using (ct.Register(() => { try { req.Abort(); } catch { } }))
                {
                    using (var rs = req.GetRequestStream()) rs.Write(bytes, 0, bytes.Length);
                    HttpWebResponse resp;
                    try { resp = (HttpWebResponse)req.GetResponse(); }
                    catch (WebException ex)
                    {
                        if (ct.IsCancellationRequested) return;
                        string msg = ex.Message;
                        int code = 0;
                        var er = ex.Response as HttpWebResponse;
                        if (er != null)
                        {
                            code = (int)er.StatusCode;
                            try { using (var sr = new StreamReader(er.GetResponseStream(), Encoding.UTF8)) msg = ErrorText(sr.ReadToEnd(), msg); } catch { }
                        }
                        throw new Exception(Friendly(provider, code, msg));
                    }
                    using (resp)
                    using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        string line;
                        var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (ct.IsCancellationRequested) return;
                            if (!line.StartsWith("data:")) continue;
                            string data = line.Substring(5).Trim();
                            if (data == "[DONE]") break;
                            Dictionary<string, object> obj;
                            try { obj = js.Deserialize<Dictionary<string, object>>(data); } catch { continue; }
                            object choicesObj;
                            if (obj == null || !obj.TryGetValue("choices", out choicesObj)) continue;
                            var choices = choicesObj as IList;
                            if (choices == null || choices.Count == 0) continue;
                            var ch = choices[0] as Dictionary<string, object>;
                            object deltaObj;
                            if (ch == null || !ch.TryGetValue("delta", out deltaObj)) continue;
                            var delta = deltaObj as Dictionary<string, object>;
                            object c;
                            if (delta != null && delta.TryGetValue("content", out c))
                            {
                                var txt = c as string;
                                if (!string.IsNullOrEmpty(txt)) onDelta(txt);
                            }
                        }
                    }
                }
            });
        }

        public static async Task<string> Complete(string provider, List<Dictionary<string, object>> messages, CancellationToken ct, string keyOverride = null, string modelOverride = null)
        {
            var sb = new StringBuilder();
            await Stream(provider, messages, d => { lock (sb) sb.Append(d); }, ct, keyOverride, modelOverride);
            return sb.ToString();
        }

        static string ErrorText(string body, string fallback)
        {
            try
            {
                var o = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
                object e;
                if (o != null && o.TryGetValue("error", out e))
                {
                    var ed = e as Dictionary<string, object>;
                    object m;
                    if (ed != null && ed.TryGetValue("message", out m) && m != null) return m.ToString();
                    if (e is string) return (string)e;
                }
            }
            catch { }
            return string.IsNullOrWhiteSpace(body) ? fallback : (body.Length > 300 ? body.Substring(0, 300) : body);
        }

        static string Friendly(string provider, int code, string msg)
        {
            string p = Display(provider);
            if (code == 401) return p + " 密钥无效或已过期，请到设置里重新填写。（" + msg + "）";
            if (code == 402) return p + " 账户余额不足，充值后再试。（" + msg + "）";
            if (code == 404) return p + " 找不到这个模型，请检查设置里的模型 ID，豆包需要先在火山方舟开通模型。（" + msg + "）";
            if (code == 429) return p + " 请求太频繁或额度用完了，稍等一会儿再试。（" + msg + "）";
            if (code == 0) return "连不上 " + p + "，请检查网络或代理。（" + msg + "）";
            return p + " 出错了（" + code + "）：" + msg;
        }
    }

    // ====================================================================
    // 系统接口
    // ====================================================================
    static class Native
    {
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);

        public static bool IsBusyFullscreen()
        {
            int s;
            if (SHQueryUserNotificationState(out s) != 0) return false;
            return s == 2 || s == 3 || s == 4;
        }

        // ---- 窗口识别（截图用）----
        public delegate bool EnumProc(IntPtr h, IntPtr l);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);

        // 按从上到下的层叠顺序列出屏幕上看得见的窗口（物理像素坐标）
        public static List<System.Drawing.Rectangle> VisibleWindows()
        {
            var list = new List<System.Drawing.Rectangle>();
            uint self = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            var sb = new StringBuilder(64);
            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h) || IsIconic(h)) return true;
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (pid == self) return true;
                int ex = GetWindowLong(h, -20);
                if ((ex & 0x20) != 0) return true;                       // 鼠标穿透的装饰层
                int cloaked;
                if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;   // 其他虚拟桌面 / 隐身的 UWP
                sb.Length = 0; GetClassName(h, sb, 64);
                string cls = sb.ToString();
                if (cls == "Progman" || cls == "WorkerW") return true;
                RECT r;
                if (DwmGetWindowAttribute(h, 9, out r, 16) != 0) GetWindowRect(h, out r);   // 去掉阴影后的真实边框
                if (r.R - r.L < 24 || r.B - r.T < 24) return true;
                list.Add(new System.Drawing.Rectangle(r.L, r.T, r.R - r.L, r.B - r.T));
                return true;
            }, IntPtr.Zero);
            return list;
        }

        public static string ForegroundProcessName()
        {
            try
            {
                uint pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
            }
            catch { return ""; }
        }

        // 把 Claude 桌面版叫到前台
        public static void ActivateClaude()
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("claude"))
            {
                IntPtr h = p.MainWindowHandle;
                if (h == IntPtr.Zero) continue;
                if (IsIconic(h)) ShowWindow(h, 9);
                SetForegroundWindow(h);
                return;
            }
        }
    }

    // ====================================================================
    // Claude 任务完成提醒：Claude Code 的 Stop 钩子运行 NaiWa.exe --claude-hook，
    // 把这次回复的开头写进 inbox 文件夹，正在运行的奶蛙读到后提醒你
    // ====================================================================
    static class ClaudeHook
    {
        public static string Inbox { get { return Path.Combine(Settings.Dir, "inbox"); } }

        // 钩子进程：读 stdin 的 JSON，写一个小文件后立刻退出
        public static void RunHook()
        {
            try
            {
                string input = "";
                var reader = new Thread(() => { try { using (var sr = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8)) input = sr.ReadToEnd(); } catch { } });
                reader.IsBackground = true;
                reader.Start();
                reader.Join(3000);
                var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var d = string.IsNullOrWhiteSpace(input) ? new Dictionary<string, object>() : js.Deserialize<Dictionary<string, object>>(input);
                string ev = Str(d, "hook_event_name"), last = Str(d, "last_assistant_message");
                if (string.IsNullOrEmpty(last)) last = LastAssistantText(Str(d, "transcript_path"));
                string cwd = Str(d, "cwd");
                var msg = new Dictionary<string, object>
                {
                    { "event", string.IsNullOrEmpty(ev) ? "Stop" : ev },
                    { "text", Clean(last) },
                    { "project", string.IsNullOrEmpty(cwd) ? "" : Path.GetFileName(cwd.TrimEnd('\\', '/')) },
                    { "at", DateTime.Now.ToString("HH:mm") }
                };
                Directory.CreateDirectory(Inbox);
                string tmp = Path.Combine(Inbox, Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(tmp, js.Serialize(msg), Encoding.UTF8);
                File.Move(tmp, Path.ChangeExtension(tmp, ".json"));
            }
            catch (Exception ex) { Program.Log("claude-hook: " + ex.Message); }
        }

        static string Str(Dictionary<string, object> d, string k)
        {
            object v; return d != null && d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        // 从对话记录（JSONL）末尾往前找最后一条 Claude 的文字回复
        static string LastAssistantText(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
                byte[] buf;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long len = Math.Min(fs.Length, 1 << 20);
                    fs.Seek(-len, SeekOrigin.End);
                    buf = new byte[len];
                    int got = 0; while (got < len) { int n = fs.Read(buf, got, (int)len - got); if (n <= 0) break; got += n; }
                }
                var lines = Encoding.UTF8.GetString(buf).Split('\n');
                var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string ln = lines[i].Trim();
                    if (ln.Length == 0 || ln.IndexOf("\"assistant\"", StringComparison.Ordinal) < 0) continue;
                    Dictionary<string, object> o;
                    try { o = js.Deserialize<Dictionary<string, object>>(ln); } catch { continue; }
                    if (Str(o, "type") != "assistant") continue;
                    var m = o.ContainsKey("message") ? o["message"] as Dictionary<string, object> : null;
                    var content = m != null && m.ContainsKey("content") ? m["content"] as ArrayList : null;
                    if (content == null) continue;
                    var sb = new StringBuilder();
                    foreach (var c in content)
                    {
                        var cd = c as Dictionary<string, object>;
                        if (cd != null && Str(cd, "type") == "text") sb.Append(Str(cd, "text")).Append(' ');
                    }
                    if (sb.ToString().Trim().Length > 0) return sb.ToString();
                }
            }
            catch { }
            return "";
        }

        // 去掉 Markdown 记号，只留开头一小段
        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, @"```[\s\S]*?```", " ");
            s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]*\)", "$1");
            s = Regex.Replace(s, @"[*`#>|_]+", "");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s.Length > 80 ? s.Substring(0, 78) + "…" : s;
        }

        // 奶蛙进程：取出所有待处理的消息
        public static List<Dictionary<string, object>> Drain()
        {
            var result = new List<Dictionary<string, object>>();
            try
            {
                if (!Directory.Exists(Inbox)) return result;
                var js = new JavaScriptSerializer();
                foreach (var f in Directory.GetFiles(Inbox, "*.json").OrderBy(x => File.GetCreationTimeUtc(x)))
                {
                    try { result.Add(js.Deserialize<Dictionary<string, object>>(File.ReadAllText(f, Encoding.UTF8))); } catch { }
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
            return result;
        }
    }

    class HotkeyHost
    {
        readonly HwndSource src;
        readonly Dictionary<int, Action> map = new Dictionary<int, Action>();
        int next = 1;
        public HotkeyHost()
        {
            var p = new HwndSourceParameters("NaiWaHotkeys") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 };
            src = new HwndSource(p);
            src.AddHook(Hook);
        }
        public bool Register(uint mod, uint vk, Action a)
        {
            int id = next++;
            map[id] = a;
            return Native.RegisterHotKey(src.Handle, id, mod | 0x4000, vk);
        }
        IntPtr Hook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if (msg == 0x0312)
            {
                Action a;
                if (map.TryGetValue(w.ToInt32(), out a)) { handled = true; a(); }
            }
            return IntPtr.Zero;
        }
    }

    static class Img
    {
        public static BitmapSource CaptureScreen(out System.Drawing.Rectangle bounds)
        {
            bounds = Forms.SystemInformation.VirtualScreen;
            using (var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bmp.Size);
                IntPtr hb = bmp.GetHbitmap();
                try
                {
                    var src = Imaging.CreateBitmapSourceFromHBitmap(hb, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    return src;
                }
                finally { Native.DeleteObject(hb); }
            }
        }

        public static byte[] Png(BitmapSource bmp)
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using (var ms = new MemoryStream()) { enc.Save(ms); return ms.ToArray(); }
        }

        public static string ToDataUrl(BitmapSource bmp)
        {
            double max = Math.Max(bmp.PixelWidth, bmp.PixelHeight);
            BitmapSource src = bmp;
            if (max > 1800)
            {
                double k = 1800 / max;
                var tb = new TransformedBitmap(bmp, new ScaleTransform(k, k));
                tb.Freeze();
                src = tb;
            }
            return "data:image/png;base64," + Convert.ToBase64String(Png(src));
        }

        public static string SaveToPictures(BitmapSource bmp)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "奶蛙截图");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            File.WriteAllBytes(path, Png(bmp));
            return path;
        }

        public static bool CopyImage(BitmapSource bmp)
        {
            for (int i = 0; i < 5; i++)
            {
                try { Clipboard.SetImage(bmp); return true; } catch { Thread.Sleep(60); }
            }
            return false;
        }

        public static double DpiScale()
        {
            using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96.0;
        }
    }

    // ====================================================================
    // 提醒：本地解析「20 分钟后提醒我喝水」「下午 3 点半提醒我开会」，解析不了再交给大模型
    // ====================================================================
    static class ReminderParser
    {
        public static bool TryParse(string s, out DateTime when, out string what)
        {
            when = DateTime.MinValue; what = null;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            var m = Regex.Match(s, @"(\d+(?:\.\d+)?)\s*(秒钟|秒|分钟|分|小时|个小时|钟头|个钟头)\s*(?:以后|之后|后)?\s*(?:再)?(?:提醒|叫|喊)我?\s*[，,：:]?\s*(.*)$");
            if (m.Success)
            {
                double n = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                string u = m.Groups[2].Value;
                TimeSpan ts = u.StartsWith("秒") ? TimeSpan.FromSeconds(n)
                    : (u.Contains("时") || u.Contains("钟头")) ? TimeSpan.FromHours(n)
                    : TimeSpan.FromMinutes(n);
                when = DateTime.Now + ts;
                what = Clean(m.Groups[3].Value);
                return true;
            }
            m = Regex.Match(s, @"(今天|明天|后天)?\s*(凌晨|早上|上午|中午|下午|傍晚|晚上)?\s*(\d{1,2})\s*(?:[:：]|点)\s*(半|\d{1,2})?\s*分?\s*(?:的时候|时)?\s*(?:提醒|叫|喊)我?\s*[，,：:]?\s*(.*)$");
            if (m.Success)
            {
                int h = int.Parse(m.Groups[3].Value);
                string mm = m.Groups[4].Value;
                int mi = mm == "半" ? 30 : (mm == "" ? 0 : int.Parse(mm));
                string ap = m.Groups[2].Value, day = m.Groups[1].Value;
                if ((ap == "下午" || ap == "傍晚" || ap == "晚上") && h < 12) h += 12;
                if (ap == "中午" && h < 11) h += 12;
                if (h > 23 || mi > 59) return false;
                DateTime d = DateTime.Today;
                if (day == "明天") d = d.AddDays(1);
                else if (day == "后天") d = d.AddDays(2);
                when = d.AddHours(h).AddMinutes(mi);
                if (day == "" && when <= DateTime.Now)
                {
                    if (ap == "" && h < 12 && when.AddHours(12) > DateTime.Now) when = when.AddHours(12);
                    else when = when.AddDays(1);
                }
                what = Clean(m.Groups[5].Value);
                return true;
            }
            return false;
        }

        static string Clean(string t)
        {
            t = (t ?? "").Trim().TrimEnd('。', '!', '！', '~', '～');
            if (t.StartsWith("一下")) t = t.Substring(2);
            return string.IsNullOrWhiteSpace(t) ? "时间到啦" : t;
        }

        public static bool LooksLikeReminder(string s) { return s.Contains("提醒") || s.Contains("叫我") || s.Contains("喊我"); }
    }

    // ====================================================================
    // 小气泡：奶蛙头顶说话
    // ====================================================================
    // 气泡 = 一条能处理的通知：标题一行、正文最多三行、操作最多两个；鼠标悬停时暂停计时
    class BubbleWindow : Window
    {
        readonly TextBlock titleText, bodyText;
        readonly Grid titleRow;
        readonly Shapes.Ellipse dot;
        readonly StackPanel actions;
        readonly DispatcherTimer hideTimer;
        double seconds;

        public BubbleWindow()
        {
            UiKit.SetupPanel(this, 0);
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowActivated = false;          // 弹出时不抢焦点
            Border card;
            var root = UiKit.Shell(out card, 16);
            root.Margin = new Thickness(14, 10, 14, 18);
            card.Padding = new Thickness(14, 11, 10, 12);
            var sp = new StackPanel { MaxWidth = 264, MinWidth = 120 };

            titleRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dot = new Shapes.Ellipse { Width = 7, Height = 7, Fill = Theme.Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 7, 0) };
            titleRow.Children.Add(dot);
            titleText = new TextBlock { FontFamily = Theme.Ui, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Theme.Title, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(titleText, 1);
            titleRow.Children.Add(titleText);
            var close = UiKit.IconButton("", "关闭", () => Hide());
            close.Width = 22; close.Height = 22; close.Margin = new Thickness(6, -3, 0, -3);
            ((TextBlock)close.Child).FontSize = 9;
            Grid.SetColumn(close, 2);
            titleRow.Children.Add(close);
            sp.Children.Add(titleRow);

            bodyText = new TextBlock { FontFamily = Theme.Ui, FontSize = 13, Foreground = Theme.Text, TextWrapping = TextWrapping.Wrap, LineHeight = 19, MaxHeight = 59, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 4, 0) };
            sp.Children.Add(bodyText);
            actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, -6) };
            sp.Children.Add(actions);
            card.Child = sp;
            Content = root;

            hideTimer = new DispatcherTimer();
            hideTimer.Tick += (s, e) => { hideTimer.Stop(); Hide(); };
            MouseEnter += (s, e) => hideTimer.Stop();
            MouseLeave += (s, e) => { if (IsVisible) { hideTimer.Interval = TimeSpan.FromSeconds(Math.Max(2.5, seconds / 3)); hideTimer.Start(); } };
            // 只有纯文字的小提示，点一下就收起；带操作的通知只认按钮
            MouseLeftButtonUp += (s, e) => { if (actions.Children.Count == 0) Hide(); };
        }

        public void ShowText(string t, double seconds, Window anchor) { Notify(null, t, seconds, anchor, null); }

        // actions: 依次为（按钮文字, 动作）；第一个是主操作
        public void Notify(string title, string body, double secs, Window anchor, List<KeyValuePair<string, Action>> acts, bool gold = false)
        {
            seconds = secs;
            Fill(title, body, acts, gold);
            hideTimer.Stop();
            if (!IsVisible) { Opacity = 0; Show(); }
            UpdateLayout();
            var wa = SystemParameters.WorkArea;
            double left = anchor.Left + anchor.Width / 2 - ActualWidth + 40;
            if (left < wa.Left) left = wa.Left;
            if (left + ActualWidth > wa.Right) left = wa.Right - ActualWidth;
            double top = anchor.Top - ActualHeight + 22;
            if (top < wa.Top) top = anchor.Top + anchor.Height - 10;
            Left = left; Top = top;
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
            hideTimer.Interval = TimeSpan.FromSeconds(secs);
            if (!IsMouseOver) hideTimer.Start();
        }

        // 只填内容不显示（--snapshot 也用它离屏渲染）
        public void Fill(string title, string body, List<KeyValuePair<string, Action>> acts, bool gold)
        {
            titleRow.Visibility = string.IsNullOrEmpty(title) ? Visibility.Collapsed : Visibility.Visible;
            titleText.Text = title ?? "";
            dot.Fill = gold ? Theme.Gold : Theme.Accent;
            bodyText.Text = body ?? "";
            bodyText.Visibility = string.IsNullOrEmpty(body) ? Visibility.Collapsed : Visibility.Visible;
            actions.Children.Clear();
            if (acts != null)
                for (int i = 0; i < acts.Count && i < 2; i++)
                {
                    var a = acts[i].Value;
                    var p = UiKit.Pill(acts[i].Key, i == 0, () => { Hide(); a(); });
                    p.Height = 30; p.CornerRadius = new CornerRadius(15);
                    actions.Children.Add(p);
                }
            actions.Visibility = actions.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            Cursor = actions.Children.Count == 0 ? Cursors.Hand : Cursors.Arrow;
        }
    }

    // ====================================================================
    // 对话窗
    // ====================================================================
    class ChatWindow : Window
    {
        readonly PetWindow pet;
        readonly StackPanel list;
        readonly ScrollViewer scroller;
        readonly TextBox input;
        readonly TextBlock placeholder;
        readonly Border attachChip;
        readonly TextBlock attachText;
        BitmapSource pendingImage;
        readonly List<Dictionary<string, object>> history = new List<Dictionary<string, object>>();
        CancellationTokenSource cts;

        public ChatWindow(PetWindow pet)
        {
            this.pet = pet;
            UiKit.SetupPanel(this, 432);
            Height = 552;                 // 可见卡片约 400×520
            Topmost = false;              // 对话窗是普通窗口，不永久压在别的软件上
            Border card;
            var root = UiKit.Shell(out card, 20);
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            card.Child = grid;

            // 标题栏
            var header = new Grid { Background = Theme.Clear, Margin = new Thickness(16, 12, 10, 8) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            // 头像：浅绿圆底 + 像素奶蛙
            var avatarBg = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Theme.AccentSoft, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
            var avatar = Sprites.View(Sprites.Frog("stare"), 0.875);
            avatar.HorizontalAlignment = HorizontalAlignment.Center; avatar.VerticalAlignment = VerticalAlignment.Center;
            avatar.Margin = new Thickness(0, 2, 0, 0);
            avatarBg.Child = avatar;
            header.Children.Add(avatarBg);
            var title = UiKit.Label("奶蛙", 16, Theme.Title, false);
            title.FontWeight = FontWeights.SemiBold;
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(10, 0, 0, 1);
            Grid.SetColumn(title, 1);
            header.Children.Add(title);
            var tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            tools.Children.Add(UiKit.IconButton("", "清空对话", ClearChat));
            tools.Children.Add(UiKit.IconButton("", "设置", () => pet.OpenSettings()));
            tools.Children.Add(UiKit.IconButton("", "收起（Esc）", Hide));
            Grid.SetColumn(tools, 2);
            header.Children.Add(tools);
            header.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            grid.Children.Add(header);
            var divider = new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(16, 0, 16, 0), VerticalAlignment = VerticalAlignment.Bottom };
            grid.Children.Add(divider);

            // 消息列表
            list = new StackPanel { Margin = new Thickness(16, 8, 16, 8) };
            scroller = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Margin = new Thickness(0, 0, 2, 0) };
            scroller.ScrollChanged += (s, e) => { nearBottom = scroller.VerticalOffset >= scroller.ScrollableHeight - 24; };
            Grid.SetRow(scroller, 2);
            grid.Children.Add(scroller);

            // 输入区
            var bottom = new StackPanel { Margin = new Thickness(16, 4, 16, 16) };
            attachText = new TextBlock { FontFamily = Theme.Ui, FontSize = 12, Foreground = Theme.Text, VerticalAlignment = VerticalAlignment.Center };
            var attachRow = new StackPanel { Orientation = Orientation.Horizontal };
            attachRow.Children.Add(new TextBlock { Text = "", FontFamily = Theme.Icons, FontSize = 12, Foreground = Theme.Accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            attachRow.Children.Add(attachText);
            var x = UiKit.IconButton("", "移除截图", () => SetAttachment(null));
            x.Width = 22; x.Height = 22; x.Margin = new Thickness(6, 0, 0, 0);
            attachRow.Children.Add(x);
            attachChip = new Border { Child = attachRow, Background = Theme.AccentSoft, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 3, 4, 3), Margin = new Thickness(0, 0, 0, 6), HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
            bottom.Children.Add(attachChip);

            // 输入框：左边「＋」收纳快捷任务，右边发送 / 停止
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 104, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 32, FontSize = 14 };
            var inputBox = new Border { Background = Theme.Soft, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(6, 6, 6, 6), MinHeight = 48 };
            inputBox.Child = inputGrid;
            var plus = UiKit.IconButton("", "截图提问、翻译剪贴板、提醒…", null);
            plus.VerticalAlignment = VerticalAlignment.Bottom;
            plus.Margin = new Thickness(0);
            var plusMenu = UiKit.StyledMenu();
            plusMenu.Items.Add(MenuItemOf("截图提问", "Ctrl+Alt+S", () => pet.StartScreenshot(false)));
            plusMenu.Items.Add(MenuItemOf("截图翻译", "Ctrl+Alt+D", () => pet.StartScreenshot(true)));
            plusMenu.Items.Add(new Separator());
            plusMenu.Items.Add(MenuItemOf("翻译剪贴板", null, () => RunClipboardTask("translate")));
            plusMenu.Items.Add(MenuItemOf("润色剪贴板（报告）", null, () => RunClipboardTask("polish")));
            plusMenu.Items.Add(new Separator());
            plusMenu.Items.Add(MenuItemOf("查看提醒", null, ShowReminders));
            plusMenu.PlacementTarget = plus;
            plusMenu.Placement = PlacementMode.Top;
            plus.MouseLeftButtonUp += (s, e) => { e.Handled = true; plusMenu.IsOpen = true; };
            inputGrid.Children.Add(plus);

            input.BorderThickness = new Thickness(0);
            input.Background = Brushes.Transparent;
            input.FontFamily = Theme.Ui;
            input.Foreground = Theme.Title;
            input.VerticalContentAlignment = VerticalAlignment.Center;
            input.Padding = new Thickness(4, 0, 4, 0);
            var inputCell = new Grid();
            inputCell.Children.Add(input);
            placeholder = new TextBlock { Text = "问点什么，或「3 点提醒我交报告」", FontFamily = Theme.Ui, FontSize = 14, Foreground = Theme.Muted, IsHitTestVisible = false, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            inputCell.Children.Add(placeholder);
            Grid.SetColumn(inputCell, 1);
            inputGrid.Children.Add(inputCell);
            send = UiKit.Pill("发送", true, () => { if (cts != null && streaming) cts.Cancel(); else SendFromInput(); });
            send.Margin = new Thickness(6, 0, 0, 0);
            send.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetColumn(send, 2);
            inputGrid.Children.Add(send);
            var inputHolder = new Grid();
            inputHolder.Children.Add(inputBox);
            bottom.Children.Add(inputHolder);
            Grid.SetRow(bottom, 3);
            grid.Children.Add(bottom);

            input.TextChanged += (s, e) => { placeholder.Visibility = input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            input.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { e.Handled = true; SendFromInput(); }
            };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { e.Handled = true; Hide(); } };
            Content = root;
            Closing += (s, e) => { e.Cancel = true; Hide(); };

            AddAssistantText("选中文字按 Ctrl+Alt+P 润色报告、Ctrl+Alt+T 翻译；Ctrl+Alt+S 截图。\n也可以直接说「下午 3 点提醒我交实验报告」。", false);
        }

        readonly Border send;
        bool streaming;
        public TextBox LastReply;

        static MenuItem MenuItemOf(string header, string gesture, Action a)
        {
            var mi = new MenuItem { Header = header, InputGestureText = gesture ?? "" };
            mi.Click += (s, e) => a();
            return mi;
        }

        // 只有本来就在底部附近时才自动跟随新内容；用户往上翻看时不打扰
        bool nearBottom = true;
        void FollowScroll() { if (nearBottom) scroller.ScrollToEnd(); }

        public void ShowNear(Window anchor)
        {
            var wa = SystemParameters.WorkArea;
            if (!IsVisible) Show();
            double left = anchor.Left - Width + 30;
            if (left < wa.Left) left = anchor.Left + anchor.Width - 30;
            if (left + Width > wa.Right) left = wa.Right - Width;
            double top = anchor.Top + anchor.Height - Height + 10;
            if (top < wa.Top) top = wa.Top;
            if (top + Height > wa.Bottom) top = wa.Bottom - Height;
            Left = left; Top = top;
            Raise();
            input.Focus();
        }

        // 对话窗不再永久置顶，所以每次被功能调起时都要主动浮到最前面
        void Raise()
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Topmost = true;      // 先临时置顶再取消：保证盖过当前窗口，之后仍是普通层级
            Topmost = false;
            Activate();
        }

        // 已打开（可能被别的窗口压住）就原地浮上来，没打开就在奶蛙旁边打开
        public void BringUp()
        {
            if (!IsVisible) ShowNear(pet);
            else Raise();
        }

        public void Toggle(Window anchor)
        {
            if (IsVisible && IsActive) Hide(); else ShowNear(anchor);
        }

        void ClearChat()
        {
            if (cts != null) cts.Cancel();
            history.Clear();
            convHasImage = false;
            list.Children.Clear();
            SetAttachment(null);
            AddNote("对话已清空");
        }

        bool convHasImage;   // 这段对话里发过截图：后续追问继续走看图模型，并带上最近那张图

        // ---------- 消息气泡 ----------
        // 用户消息：浅灰小卡片，右对齐；fullText 不为空时可展开查看完整原文
        void AddUser(string text, bool hasImage, string fullText = null)
        {
            var sp = new StackPanel();
            if (hasImage) sp.Children.Add(new TextBlock { Text = "  截图", FontFamily = Theme.Icons, FontSize = 12, Foreground = Theme.Accent, Margin = new Thickness(0, 1, 0, 3) });
            var body = UiKit.SelectableText(text, Theme.Title, 14);
            sp.Children.Add(body);
            if (fullText != null)
            {
                var toggle = new TextBlock { Text = "展开原文 ▾", FontFamily = Theme.Ui, FontSize = 12, Foreground = Theme.Accent, Cursor = Cursors.Hand, Margin = new Thickness(0, 4, 0, 0) };
                var full = UiKit.SelectableText(fullText, Theme.Text, 13);
                full.Margin = new Thickness(0, 4, 0, 0);
                full.Visibility = Visibility.Collapsed;
                toggle.MouseLeftButtonUp += (s, e) =>
                {
                    bool open = full.Visibility != Visibility.Visible;
                    full.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
                    toggle.Text = open ? "收起原文 ▴" : "展开原文 ▾";
                };
                sp.Children.Add(full);
                sp.Children.Add(toggle);
            }
            list.Children.Add(new Border
            {
                Child = sp,
                Background = Theme.Soft,
                CornerRadius = new CornerRadius(16, 16, 4, 16),
                Padding = new Thickness(12, 8, 12, 9),
                Margin = new Thickness(48, 8, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            nearBottom = true;
            scroller.ScrollToEnd();
        }

        // 奶蛙的回答：直接写在白底上，不再层层套边框
        TextBox AddAssistantText(string text, bool copyLink = true)
        {
            var tb = UiKit.SelectableText(text, Theme.Text, 14);
            var sp = new StackPanel();
            sp.Children.Add(tb);
            if (copyLink)
            {
                var copy = new TextBlock { Text = "复制", FontFamily = Theme.Ui, FontSize = 12, Foreground = Theme.Muted, Cursor = Cursors.Hand, Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
                copy.MouseEnter += (s, e) => { copy.Foreground = Theme.Accent; };
                copy.MouseLeave += (s, e) => { copy.Foreground = Theme.Muted; };
                copy.MouseLeftButtonUp += (s, e) =>
                {
                    try { Clipboard.SetText(tb.Text); copy.Text = "已复制"; } catch { copy.Text = "复制失败"; }
                };
                sp.Children.Add(copy);
            }
            list.Children.Add(new Border
            {
                Child = sp,
                Padding = new Thickness(2, 6, 2, 4),
                Margin = new Thickness(0, 6, 24, 2),
                HorizontalAlignment = HorizontalAlignment.Left
            });
            FollowScroll();
            return tb;
        }

        // 在某条回答下面加一排操作按钮
        void AddActions(TextBox reply, List<KeyValuePair<string, Action>> acts)
        {
            var sp = reply.Parent as StackPanel;
            if (sp == null) return;
            var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < acts.Count; i++)
            {
                var p = UiKit.Pill(acts[i].Key, i == 0, acts[i].Value);
                p.Height = 30; p.CornerRadius = new CornerRadius(15);
                row.Children.Add(p);
            }
            sp.Children.Add(row);
            FollowScroll();
        }

        static KeyValuePair<string, Action> Act(string label, Action a) { return new KeyValuePair<string, Action>(label, a); }

        // 仅供 --snapshot：摆一段示例对话
        public void DemoFill()
        {
            string src = "The heating module is consist of a resistive coil which heat the water to 100 °C within 8 min, the temperature is detect by NTC sensor (R25 = 10 kΩ) and send to MCU [3].";
            AddUser("报告润色 · " + src.Length + " 字\n" + src.Substring(0, 60) + "…", false, src);
            var r = AddAssistantText("The heating module consists of a resistive coil that heats the water to 100 °C within 8 min. The temperature is detected by an NTC sensor (R25 = 10 kΩ) and sent to the MCU [3].", false);
            AddActions(r, new List<KeyValuePair<string, Action>> { Act("复制结果", () => { }), Act("更简洁", () => { }) });
            AddNote("20:12 Claude 回复结束：测试全部通过");
            ShowReminders();
        }

        public void AddNote(string text)
        {
            list.Children.Add(new TextBlock { Text = text, FontFamily = Theme.Ui, FontSize = 12, Foreground = Theme.Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 4), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            FollowScroll();
        }

        public void SetAttachment(BitmapSource img)
        {
            pendingImage = img;
            if (img == null) { attachChip.Visibility = Visibility.Collapsed; return; }
            attachText.Text = "已附上截图 " + img.PixelWidth + "×" + img.PixelHeight + "，输入问题后发送";
            attachChip.Visibility = Visibility.Visible;
        }

        // ---------- 发送 ----------
        async void SendFromInput()
        {
            string text = input.Text.Trim();
            if (text.Length == 0 && pendingImage == null) return;
            input.Clear();
            var img = pendingImage;
            SetAttachment(null);
            AddUser(text.Length == 0 ? "（看看这张截图）" : text, img != null);

            if (img == null && text.Contains("取消") && text.Contains("提醒"))
            {
                var rs = Settings.Current.Reminders;
                if (text.Contains("全部") || text.Contains("所有"))
                {
                    int n = rs.Count;
                    rs.Clear();
                    Settings.Save();
                    AddAssistantText(n > 0 ? "已取消全部 " + n + " 个提醒。" : "现在没有提醒要取消。", false);
                    return;
                }
                // 只取消说到的那一个；对不上就把列表摆出来让用户点
                string key = Regex.Replace(text, "取消|提醒|一下|那个|这个|我的|帮我|的|吧|。|，|,", "").Trim();
                var hit = key.Length == 0 ? new List<Reminder>() : rs.Where(r => r.Text.Contains(key) || key.Contains(r.Text)).ToList();
                if (hit.Count == 1)
                {
                    rs.Remove(hit[0]);
                    Settings.Save();
                    AddAssistantText("已取消：" + new DateTime(hit[0].At).ToString("M月d日 HH:mm") + "　" + hit[0].Text, false);
                    return;
                }
                ShowReminders();
                return;
            }
            if (img == null && ReminderParser.LooksLikeReminder(text))
            {
                DateTime when; string what;
                if (ReminderParser.TryParse(text, out when, out what)) { AddReminder(when, what); return; }
                if (Ai.PickText() != null && await TryAiReminder(text)) return;
            }

            if (img != null)
            {
                if (!Ai.HasDoubao) { AddAssistantText("看图需要豆包的密钥（DeepSeek 目前不能看图），右上角 ⚙ 里填一下吧。", false); return; }
                history.Add(Ai.Msg("user", Ai.WithImage(text.Length == 0 ? "请描述这张截图的内容，如果有文字请提取出来。" : text, Img.ToDataUrl(img))));
                convHasImage = true;
                await RunChat("doubao");
            }
            else if (convHasImage && Ai.HasDoubao)
            {
                // 追问刚才那张截图：继续用看图模型，最近一张图会一起带上
                history.Add(Ai.Msg("user", text));
                await RunChat("doubao");
            }
            else
            {
                string p = Ai.PickText();
                if (p == null) { AddAssistantText("我还没有接上大脑 🐸 右上角 ⚙ 里填一个 DeepSeek 或豆包的密钥，我就能聊天、翻译啦。"); return; }
                history.Add(Ai.Msg("user", text));
                await RunChat(p);
            }
        }

        static string Persona()
        {
            return "你是「奶蛙」，一只住在用户 Windows 桌面上的 Q 版像素奶黄色小青蛙，是用户的生活助理。" +
                   "回答简洁、实用、友好，偶尔带一点呆萌；默认用简体中文。现在是 " + DateTime.Now.ToString("yyyy年M月d日 dddd HH:mm", new CultureInfo("zh-CN")) + "。";
        }

        async Task RunChat(string provider)
        {
            var msgs = new List<Dictionary<string, object>> { Ai.Msg("system", Persona()) };
            // 只保留最近 12 条；最近一张截图原样保留（追问时模型还看得到），更早的图换成文字占位
            var recent = history.Skip(Math.Max(0, history.Count - 12)).ToList();
            int lastImg = recent.FindLastIndex(m => !(m["content"] is string));
            for (int i = 0; i < recent.Count; i++)
            {
                var m = recent[i];
                if (i != lastImg && !(m["content"] is string))
                    msgs.Add(Ai.Msg((string)m["role"], "（之前发过一张截图）"));
                else msgs.Add(m);
            }
            string reply = await StreamInto(provider, msgs, null);
            if (reply != null) history.Add(Ai.Msg("assistant", reply));
        }

        // 流式显示到一个新气泡里，返回完整文本（失败返回 null）
        async Task<string> StreamInto(string provider, List<Dictionary<string, object>> msgs, string header, bool copyLink = true)
        {
            if (cts != null) cts.Cancel();
            cts = new CancellationTokenSource();
            var my = cts;
            var tb = AddAssistantText(header == null ? "…" : header + "\n", copyLink);
            LastReply = tb;
            bool first = true;
            var sb = new StringBuilder();
            pet.BeginBusy();
            streaming = true;
            UiKit.SetPillText(send, "停止");
            try
            {
                await Ai.Stream(provider, msgs, d =>
                {
                    lock (sb) sb.Append(d);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (first) { tb.Text = header == null ? "" : header + "\n"; first = false; }
                        tb.AppendText(d);
                        FollowScroll();
                    }));
                }, my.Token);
                await Dispatcher.InvokeAsync(() => { });
                if (first) tb.Text = (header == null ? "" : header + "\n") + "（没有返回内容）";
                return sb.ToString();
            }
            catch (Exception ex)
            {
                if (my.IsCancellationRequested) { tb.Text = "（已停止）"; return null; }
                tb.Text = "😵 " + ex.Message;
                tb.Foreground = Theme.Danger;
                return null;
            }
            finally
            {
                pet.EndBusy();
                if (cts == my) { streaming = false; UiKit.SetPillText(send, "发送"); }
            }
        }

        // ---------- 快捷任务 ----------
        public static string PromptFor(string kind)
        {
            switch (kind)
            {
                case "translate": return "你是专业翻译。把用户给的文本翻译：如果原文主要是中文，译成自然地道的英文；否则译成简体中文。只输出译文，保留原有段落和格式，不要解释。";
                case "polish": return "你是理工科报告的写作编辑（实验报告、设计说明、课程作业）。润色用户给的文字：保持原意和原语言；表达改得准确、简洁、正式，修正语法和用词；" +
                                      "所有数值、单位、公式、变量名、引用编号、图表编号和技术术语原样保留；不补写原文没有的信息、数据或结论。只输出润色后的文字，不要解释。";
                case "concise": return "你是理工科报告的写作编辑。把用户给的文字改得更简洁（目标缩短约 20%）：保持原意和原语言，删掉重复和空话；" +
                                      "所有数值、单位、公式、变量名、引用编号和技术术语原样保留，不新增信息。只输出改写后的文字，不要解释。";
                case "summary": return "用简体中文总结用户给的内容：先一句话概括，再列出 3-5 个要点。简洁。";
                default: return Persona();
            }
        }
        public static string KindName(string kind)
        {
            switch (kind) { case "translate": return "翻译"; case "polish": return "报告润色"; case "concise": return "更简洁"; case "summary": return "总结"; default: return "处理"; }
        }

        // 划词时没取到选中的文字：如实告诉用户，不偷偷拿剪贴板里的旧内容
        public void SelectionFailed(string kind)
        {
            BringUp();
            var tb = AddAssistantText("没取到选中的文字。有些软件不支持自动复制，可以先 Ctrl+C 复制，再用剪贴板里的内容" + KindName(kind) + "。", false);
            AddActions(tb, new List<KeyValuePair<string, Action>> { Act("用剪贴板里的文字", () => RunClipboardTask(kind)) });
        }

        async void RunClipboardTask(string kind)
        {
            string text = null;
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }
            await RunTextTask(kind, text);
        }

        public async Task RunTextTask(string kind, string text)
        {
            BringUp();
            if (string.IsNullOrWhiteSpace(text)) { AddAssistantText("没拿到文字呢。先选中或复制一段文字再试试～", false); return; }
            string p = Ai.PickText();
            // 卡片标题写任务和字数，完整原文默认折叠、可展开
            string preview = text.Length > 60 ? text.Substring(0, 60).Replace("\r", "").Replace("\n", " ") + "…" : text;
            AddUser(KindName(kind) + " · " + text.Length + " 字\n" + preview, false, text.Length > 60 ? text : null);
            if (p == null) { AddAssistantText("需要先在 ⚙ 设置里填 DeepSeek 或豆包的密钥才能" + KindName(kind) + "哦。", false); return; }
            var msgs = new List<Dictionary<string, object>> { Ai.Msg("system", PromptFor(kind)), Ai.Msg("user", text) };
            string result = await StreamInto(p, msgs, null, false);
            if (result == null) return;
            // 结果默认不自动覆盖剪贴板，由用户明确点「复制结果」
            var reply = LastReply;
            var acts = new List<KeyValuePair<string, Action>>();
            acts.Add(Act("复制结果", () => { try { Clipboard.SetText(reply.Text.Trim()); pet.Say("已复制 ✓", 2); } catch { pet.Say("复制失败了，再点一次？", 3); } }));
            if (kind == "polish") acts.Add(Act("更简洁", () => { var _ = RunTextTask("concise", reply.Text.Trim()); }));
            if (kind == "concise") acts.Add(Act("再简洁一点", () => { var _ = RunTextTask("concise", reply.Text.Trim()); }));
            AddActions(reply, acts);
        }

        // kind: "ocr" 只提取文字；"translate" 原文 + 译文
        public async void RunVisionTask(BitmapSource img, string kind = "translate")
        {
            BringUp();
            bool ocr = kind == "ocr";
            AddUser(ocr ? "提取截图里的文字" : "翻译这张截图", true);
            if (!Ai.HasDoubao) { AddAssistantText("截图识别要用豆包的看图模型，右上角 ⚙ 里填一下豆包（火山方舟）的密钥吧。截图已经帮你复制到剪贴板了。", false); Img.CopyImage(img); return; }
            string ask = ocr
                ? "提取图片中的全部文字，保持原有排版和换行，原样输出，不翻译、不解释。如果图里没有文字，就回答「图里没有文字」并一句话描述画面。"
                : "识别图片中的全部文字。先输出「原文：」及识别结果（保持排版），再输出「译文：」：若原文主要是中文则译成英文，否则译成简体中文。如果图里没有文字，就简要描述画面内容。";
            var userMsg = Ai.Msg("user", Ai.WithImage(ask, Img.ToDataUrl(img)));
            var msgs = new List<Dictionary<string, object>> { Ai.Msg("system", "你是精确的 OCR 与翻译助手。"), userMsg };
            string result = await StreamInto("doubao", msgs, null, false);
            if (result == null) return;
            // 记进对话，接下来可以直接追问「这里是什么意思」
            history.Add(userMsg);
            history.Add(Ai.Msg("assistant", result));
            convHasImage = true;
            var reply = LastReply;
            AddActions(reply, new List<KeyValuePair<string, Action>>
            {
                Act(ocr ? "复制文字" : "复制结果", () => { try { Clipboard.SetText(reply.Text.Trim()); pet.Say("已复制 ✓", 2); } catch { } }),
                Act(ocr ? "翻译" : "只要原文", () => RunVisionTask(img, ocr ? "translate" : "ocr"))
            });
        }

        // ---------- 提醒 ----------
        async Task<bool> TryAiReminder(string text)
        {
            try
            {
                pet.BeginBusy();
                var msgs = new List<Dictionary<string, object>>
                {
                    Ai.Msg("system", "现在是 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm dddd", new CultureInfo("zh-CN")) + "。从用户的话里提取提醒。只输出一行 JSON：{\"at\":\"yyyy-MM-dd HH:mm\",\"text\":\"提醒内容\"}；如果不是设置提醒，输出 {}。"),
                    Ai.Msg("user", text)
                };
                string r = await Ai.Complete(Ai.PickText(), msgs, CancellationToken.None);
                var at = Regex.Match(r, "\"at\"\\s*:\\s*\"([^\"]+)\"");
                var tx = Regex.Match(r, "\"text\"\\s*:\\s*\"([^\"]*)\"");
                DateTime when;
                if (at.Success && DateTime.TryParseExact(at.Groups[1].Value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out when) && when > DateTime.Now)
                {
                    AddReminder(when, tx.Success && tx.Groups[1].Value.Length > 0 ? tx.Groups[1].Value : "时间到啦");
                    return true;
                }
            }
            catch (Exception ex) { Program.Log("AI 解析提醒失败: " + ex.Message); }
            finally { pet.EndBusy(); }
            return false;
        }

        void AddReminder(DateTime when, string what)
        {
            Settings.Current.Reminders.Add(new Reminder { At = when.Ticks, Text = what });
            Settings.Save();
            string day = when.Date == DateTime.Today ? "今天" : when.Date == DateTime.Today.AddDays(1) ? "明天" : when.ToString("M月d日");
            var left = when - DateTime.Now;
            string rel = left.TotalMinutes < 60 ? Math.Max(1, (int)Math.Round(left.TotalMinutes)) + " 分钟后" : left.TotalHours < 24 ? left.TotalHours.ToString("0.#") + " 小时后" : "";
            AddAssistantText("好嘞，" + day + " " + when.ToString("HH:mm") + (rel.Length > 0 ? "（" + rel + "）" : "") + " 提醒你：" + what + " ⏰");
            pet.Laugh();
        }

        // 提醒列表：每条一行，右边 ✕ 单独取消
        public void ShowReminders()
        {
            BringUp();
            var rs = Settings.Current.Reminders.OrderBy(r => r.At).ToList();
            if (rs.Count == 0) { AddAssistantText("现在没有待办提醒。说「30 分钟后提醒我站起来活动」试试～", false); return; }
            var card = new StackPanel();
            card.Children.Add(new TextBlock { Text = "待办提醒 · " + rs.Count, FontFamily = Theme.Ui, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Theme.Title, Margin = new Thickness(2, 0, 0, 6) });
            foreach (var r in rs)
            {
                var item = r;
                var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var when = new DateTime(r.At);
                string day = when.Date == DateTime.Today ? "今天" : when.Date == DateTime.Today.AddDays(1) ? "明天" : when.ToString("M/d");
                row.Children.Add(new TextBlock { Text = day + " " + when.ToString("HH:mm"), FontFamily = Theme.Ui, FontSize = 13, Foreground = Theme.Muted, Width = 82, VerticalAlignment = VerticalAlignment.Center });
                var what = new TextBlock { Text = r.Text, FontFamily = Theme.Ui, FontSize = 13, Foreground = Theme.Title, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(what, 1);
                row.Children.Add(what);
                Border x = null;
                x = UiKit.IconButton("", "取消这个提醒", () =>
                {
                    Settings.Current.Reminders.Remove(item);
                    Settings.Save();
                    what.TextDecorations = TextDecorations.Strikethrough;
                    what.Foreground = Theme.Muted;
                    x.Visibility = Visibility.Hidden;
                });
                x.Width = 26; x.Height = 26;
                ((TextBlock)x.Child).FontSize = 10;
                Grid.SetColumn(x, 2);
                row.Children.Add(x);
                card.Children.Add(row);
            }
            list.Children.Add(new Border { Child = card, Background = Theme.Soft, CornerRadius = new CornerRadius(16), Padding = new Thickness(12, 10, 8, 8), Margin = new Thickness(0, 8, 24, 2) });
            FollowScroll();
        }

        // 到点后「10 分钟后再提醒」
        public static void Snooze(string text, int minutes)
        {
            Settings.Current.Reminders.Add(new Reminder { At = DateTime.Now.AddMinutes(minutes).Ticks, Text = text });
            Settings.Save();
        }

    }

    // ====================================================================
    // 设置窗
    // ====================================================================
    class SettingsWindow : Window
    {
        readonly PasswordBox dsKey, dbKey;
        readonly TextBox dsModel, dbModel;
        readonly CheckBox topmost, autostart;
        readonly TextBlock status;
        string provider;
        readonly StackPanel providerRow;
        readonly PetWindow pet;

        public SettingsWindow(PetWindow pet)
        {
            this.pet = pet;
            UiKit.SetupPanel(this, 460);
            SizeToContent = SizeToContent.Height;
            Border card;
            var root = UiKit.Shell(out card, 16);
            var sp = new StackPanel { Margin = new Thickness(22, 16, 22, 18) };
            card.Child = sp;
            var s = Settings.Current;

            var head = new Grid { Background = Theme.Clear };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.Children.Add(UiKit.Label("设置", 20, Theme.Title, true));
            var close = UiKit.IconButton("", "关闭", Close);
            Grid.SetColumn(close, 1);
            head.Children.Add(close);
            head.MouseLeftButtonDown += (o, e) => { try { DragMove(); } catch { } };
            sp.Children.Add(head);
            var privacy = UiKit.Label("密钥只用 Windows 加密保存在这台电脑上，只会发给对应的官方接口。", 11.5, Theme.Muted, false);
            privacy.Margin = new Thickness(0, 2, 0, 12);
            sp.Children.Add(privacy);

            // DeepSeek
            sp.Children.Add(Section("DeepSeek", "聊天、翻译、润色", "https://platform.deepseek.com/api_keys", "获取密钥"));
            dsKey = new PasswordBox();
            sp.Children.Add(Field("API 密钥", dsKey, s.DeepSeekKeyEnc.Length > 0 ? "已保存，留空表示不修改" : "sk-..."));
            dsModel = new TextBox { Text = s.DeepSeekModel };
            sp.Children.Add(Field("模型", dsModel, null));

            // 豆包
            sp.Children.Add(Section("豆包 · 火山方舟", "截图识别等看图功能，也能聊天翻译", "https://console.volcengine.com/ark/region:ark+cn-beijing/apiKey", "获取密钥"));
            dbKey = new PasswordBox();
            sp.Children.Add(Field("API 密钥", dbKey, s.DoubaoKeyEnc.Length > 0 ? "已保存，留空表示不修改" : "在火山方舟控制台创建"));
            dbModel = new TextBox { Text = s.DoubaoModel };
            sp.Children.Add(Field("模型 ID（需支持看图，可填推理接入点 ep-...）", dbModel, null));

            // 文字任务用谁
            sp.Children.Add(Sub("文字任务优先使用"));
            providerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
            provider = s.TextProvider;
            sp.Children.Add(providerRow);
            RenderProviderRow();

            topmost = new CheckBox { Content = "奶蛙始终显示在最上层", IsChecked = s.Topmost, FontFamily = Theme.Ui, FontSize = 13, Foreground = Theme.Text, Margin = new Thickness(0, 4, 0, 4) };
            autostart = new CheckBox { Content = "开机自动启动", IsChecked = s.AutoStart, FontFamily = Theme.Ui, FontSize = 13, Foreground = Theme.Text, Margin = new Thickness(0, 2, 0, 10) };
            sp.Children.Add(topmost);
            sp.Children.Add(autostart);

            var keys = UiKit.Label("快捷键：Ctrl+Alt+S 截图 · Ctrl+Alt+D 截图识别翻译 · Ctrl+Alt+T 翻译选中文字 · Ctrl+Alt+P 润色选中文字", 11.5, Theme.Muted, false);
            keys.Margin = new Thickness(0, 0, 0, 12);
            sp.Children.Add(keys);

            status = UiKit.Label("", 12, Theme.Muted, false);
            status.Margin = new Thickness(0, 0, 0, 8);
            sp.Children.Add(status);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            btns.Children.Add(UiKit.Pill("测试连接", false, TestConnection));
            btns.Children.Add(UiKit.Pill("保存", true, SaveAndClose));
            sp.Children.Add(btns);

            Content = root;
            PreviewKeyDown += (o, e) => { if (e.Key == Key.Escape) Close(); };
        }

        FrameworkElement Section(string title, string desc, string url, string linkText)
        {
            var g = new Grid { Margin = new Thickness(0, 8, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var st = new StackPanel();
            st.Children.Add(UiKit.Label(title, 15, Theme.Title, true));
            st.Children.Add(UiKit.Label(desc, 11.5, Theme.Muted, false));
            g.Children.Add(st);
            var link = new TextBlock { Text = linkText + " ↗", FontFamily = Theme.Ui, FontSize = 12, Foreground = Theme.Accent, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Bottom };
            link.MouseLeftButtonUp += (o, e) => { try { System.Diagnostics.Process.Start(url); } catch { } };
            Grid.SetColumn(link, 1);
            g.Children.Add(link);
            return g;
        }

        FrameworkElement Sub(string t)
        {
            var l = UiKit.Label(t, 12, Theme.Text, false);
            l.Margin = new Thickness(0, 6, 0, 0);
            return l;
        }

        FrameworkElement Field(string label, Control box, string hint)
        {
            var sp = new StackPanel();
            var l = UiKit.Label(label, 12, Theme.Text, false);
            l.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(l);
            var cell = new Grid();
            cell.Children.Add(UiKit.InputBox(box));
            if (hint != null)
            {
                var ph = new TextBlock { Text = hint, FontFamily = Theme.Ui, FontSize = 12.5, Foreground = Theme.Muted, IsHitTestVisible = false, Margin = new Thickness(11, 11, 0, 0) };
                cell.Children.Add(ph);
                var pb = box as PasswordBox;
                if (pb != null) pb.PasswordChanged += (o, e) => { ph.Visibility = pb.Password.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            }
            sp.Children.Add(cell);
            return sp;
        }

        void RenderProviderRow()
        {
            providerRow.Children.Clear();
            foreach (var opt in new[] { new[] { "auto", "自动" }, new[] { "deepseek", "DeepSeek" }, new[] { "doubao", "豆包" } })
            {
                string key = opt[0];
                var pill = UiKit.Pill(opt[1], provider == key, () => { provider = key; RenderProviderRow(); });
                providerRow.Children.Add(pill);
            }
        }

        string KeyFor(PasswordBox pb, bool deepseek)
        {
            if (pb.Password.Trim().Length > 0) return pb.Password.Trim();
            return deepseek ? Settings.Current.DeepSeekKey : Settings.Current.DoubaoKey;
        }

        async void TestConnection()
        {
            status.Foreground = Theme.Muted;
            status.Text = "测试中…";
            var sb = new StringBuilder();
            foreach (var p in new[] { "deepseek", "doubao" })
            {
                bool ds = p == "deepseek";
                string key = KeyFor(ds ? dsKey : dbKey, ds);
                if (string.IsNullOrEmpty(key)) { sb.Append(Ai.Display(p) + "：未填写　"); continue; }
                try
                {
                    var msgs = new List<Dictionary<string, object>> { Ai.Msg("user", "只回复两个字：在呢") };
                    var cts = new CancellationTokenSource(25000);
                    string r = await Ai.Complete(p, msgs, cts.Token, key, ds ? dsModel.Text.Trim() : dbModel.Text.Trim());
                    sb.Append(Ai.Display(p) + "：✓ 连接成功　");
                }
                catch (Exception ex) { sb.Append(Ai.Display(p) + "：✗ " + ex.Message + "\n"); }
            }
            status.Text = sb.ToString().Trim();
            status.Foreground = status.Text.Contains("✗") ? Theme.Danger : Theme.Text;
        }

        void SaveAndClose()
        {
            var s = Settings.Current;
            if (dsKey.Password.Trim().Length > 0) s.DeepSeekKey = dsKey.Password.Trim();
            if (dbKey.Password.Trim().Length > 0) s.DoubaoKey = dbKey.Password.Trim();
            s.DeepSeekModel = dsModel.Text.Trim().Length > 0 ? dsModel.Text.Trim() : "deepseek-chat";
            s.DoubaoModel = dbModel.Text.Trim();
            s.TextProvider = provider;
            s.Topmost = topmost.IsChecked == true;
            bool wantAuto = autostart.IsChecked == true;
            if (wantAuto != s.AutoStart) Program.SetAutoStart(wantAuto);
            s.AutoStart = wantAuto;
            Settings.Save();
            pet.ApplySettings();
            pet.Say("设置保存好啦 ✓", 3);
            Close();
        }
    }

    // ====================================================================
    // 截图：全屏冻结 → 框选 → 复制 / 保存 / 钉图 / 识别翻译 / 问奶蛙
    // ====================================================================
    class ScreenshotOverlay : Window
    {
        readonly PetWindow pet;
        readonly bool ocrMode;
        readonly BitmapSource shot;
        readonly double k;
        readonly Canvas canvas;
        readonly Shapes.Path mask;
        readonly Shapes.Rectangle selRect;
        readonly Border sizeTag, toolbar, hint;
        readonly TextBlock sizeText;
        Point start;
        Rect sel = Rect.Empty, hover = Rect.Empty;
        bool selecting, dragged;
        readonly List<Rect> windows = new List<Rect>();   // 截图那一刻的窗口（从上到下），画布坐标

        // ---- 标注：画笔 / 荧光笔 ----
        string tool;                                        // null / "pen" / "hl"
        static readonly Color[] PenColors = { Color.FromRgb(0xE5, 0x48, 0x4D), Color.FromRgb(0x2F, 0x6F, 0xEB), Color.FromRgb(0x24, 0x28, 0x2C) };
        static readonly Color[] HlColors = { Color.FromRgb(0xFF, 0xD6, 0x0A), Color.FromRgb(0x5E, 0xD3, 0x7A), Color.FromRgb(0xFF, 0x7A, 0xA8) };
        Color penColor = PenColors[0], hlColor = HlColors[0];
        Canvas ink;
        readonly List<Shapes.Polyline> strokes = new List<Shapes.Polyline>();
        Shapes.Polyline cur;
        Border penBtn, hlBtn, undoBtn;
        StackPanel colorRow;

        public ScreenshotOverlay(PetWindow pet, bool ocrMode)
        {
            this.pet = pet;
            this.ocrMode = ocrMode;
            System.Drawing.Rectangle vb;
            var wins = Native.VisibleWindows();   // 在截图前记下窗口层叠，和冻结的画面一致
            shot = Img.CaptureScreen(out vb);
            k = Img.DpiScale();
            var bounds = new Rect(0, 0, vb.Width / k, vb.Height / k);
            foreach (var w in wins)
            {
                var r = new Rect((w.Left - vb.Left) / k, (w.Top - vb.Top) / k, w.Width / k, w.Height / k);
                r.Intersect(bounds);
                if (!r.IsEmpty && r.Width >= 8 && r.Height >= 8) windows.Add(r);
            }
            foreach (var sc in Forms.Screen.AllScreens)   // 落在桌面上 = 整块屏幕
            {
                var b = sc.Bounds;
                windows.Add(new Rect((b.Left - vb.Left) / k, (b.Top - vb.Top) / k, b.Width / k, b.Height / k));
            }
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Left = vb.Left / k; Top = vb.Top / k; Width = vb.Width / k; Height = vb.Height / k;
            Cursor = Cursors.Cross;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            var root = new Grid();
            root.Children.Add(new Image { Source = shot, Stretch = Stretch.Fill });
            canvas = new Canvas { Background = Theme.Clear };
            root.Children.Add(canvas);
            mask = new Shapes.Path { Fill = new SolidColorBrush(Color.FromArgb(0x78, 0x14, 0x14, 0x13)), IsHitTestVisible = false };
            canvas.Children.Add(mask);
            ink = new Canvas { IsHitTestVisible = false };   // 标注层，只显示在选区里
            canvas.Children.Add(ink);
            selRect = new Shapes.Rectangle { Stroke = Theme.Accent, StrokeThickness = 2, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
            canvas.Children.Add(selRect);
            sizeText = new TextBlock { FontFamily = Theme.Ui, FontSize = 11.5, Foreground = Brushes.White };
            sizeTag = new Border { Child = sizeText, Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x14, 0x14, 0x13)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), Visibility = Visibility.Collapsed, IsHitTestVisible = false };
            canvas.Children.Add(sizeTag);

            var hintText = new TextBlock { Text = ocrMode ? "单击选中窗口，或拖动框选要识别 / 翻译的区域　·　Esc 取消" : "单击选中窗口，或拖动框选区域　·　选好后可用画笔 / 荧光笔标注　·　Esc 取消", FontFamily = Theme.Ui, FontSize = 13, Foreground = Theme.Text };
            hint = new Border { Child = hintText, Background = Theme.Cream, CornerRadius = new CornerRadius(16), Padding = new Thickness(16, 7, 16, 8), BorderBrush = Theme.Line, BorderThickness = new Thickness(1), IsHitTestVisible = false };
            canvas.Children.Add(hint);

            toolbar = BuildToolbar();
            toolbar.Visibility = Visibility.Collapsed;
            canvas.Children.Add(toolbar);
            Content = root;

            Loaded += (s, e) =>
            {
                hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var wa = SystemParameters.WorkArea;
                Canvas.SetLeft(hint, (wa.Left - Left) + (wa.Width - hint.DesiredSize.Width) / 2);
                Canvas.SetTop(hint, (wa.Top - Top) + 24);
                UpdateHover(Mouse.GetPosition(canvas));
                Activate();
                Focus();
            };
            MouseLeftButtonDown += OnDown;
            MouseMove += OnMove;
            MouseLeftButtonUp += OnUp;
            MouseRightButtonUp += (s, e) =>
            {
                if (tool != null) { SetTool(tool); return; }          // 先退出画笔，标注保留
                if (!sel.IsEmpty && !selecting) ResetSelection(); else Finish();
            };
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { Undo(); return; }
                if (HasSelection && !ocrMode && !selecting && Keyboard.Modifiers == ModifierKeys.None)
                {
                    if (e.Key == Key.P) { SetTool("pen"); return; }
                    if (e.Key == Key.H) { SetTool("hl"); return; }
                }
                if (e.Key == Key.Escape) Finish();
                else if (e.Key == Key.Enter)
                {
                    if (!HasSelection && !selecting && !hover.IsEmpty) sel = hover;   // 没框选时，回车直接处理鼠标下的窗口
                    if (HasSelection) { if (ocrMode) DoOcr(); else DoCopy(); }
                }
            };
        }

        // 鼠标下最上层的窗口
        void UpdateHover(Point p)
        {
            Rect h = Rect.Empty;
            foreach (var r in windows) if (r.Contains(p)) { h = r; break; }
            if (h == hover) return;
            hover = h;
            if (hover.IsEmpty) { selRect.Visibility = Visibility.Collapsed; sizeTag.Visibility = Visibility.Collapsed; UpdateMask(); return; }
            ShowRect(hover);
        }

        bool HasSelection { get { return !sel.IsEmpty && sel.Width >= 4 && sel.Height >= 4; } }
        public Border ToolbarForSnapshot { get { canvas.Children.Remove(toolbar); toolbar.Visibility = Visibility.Visible; return toolbar; } }

        public void DemoTool() { SetTool("pen"); }   // 仅供 --snapshot

        // 工具栏：复制｜提取文字｜翻译｜问奶蛙｜更多｜关闭；普通截图主操作是复制，截图翻译模式主操作是翻译
        Border BuildToolbar()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 6, 2, 6) };
            Func<Border, Border> tight = p => { p.Margin = new Thickness(0, 0, 4, 0); return p; };
            // 标注工具组：画笔｜荧光笔｜(颜色)｜撤销
            penBtn = ToolButton("", "画笔（P）", "pen");
            hlBtn = ToolButton("", "荧光笔（H）", "hl");
            sp.Children.Add(penBtn);
            sp.Children.Add(hlBtn);
            colorRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), Visibility = Visibility.Collapsed };
            sp.Children.Add(colorRow);
            undoBtn = UiKit.IconButton("", "撤销（Ctrl+Z）", Undo);
            undoBtn.Opacity = 0.35;
            sp.Children.Add(undoBtn);
            sp.Children.Add(new Border { Width = 1, Height = 20, Background = Theme.Line, Margin = new Thickness(6, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(tight(UiKit.Pill("复制", !ocrMode, DoCopy)));
            sp.Children.Add(tight(UiKit.Pill("提取文字", false, DoExtract)));
            sp.Children.Add(tight(UiKit.Pill("翻译", ocrMode, DoOcr)));
            sp.Children.Add(tight(UiKit.Pill("问奶蛙", false, DoAsk)));
            var more = UiKit.Pill("更多 ▾", false, null);
            var moreMenu = UiKit.StyledMenu();
            var mSave = new MenuItem { Header = "保存到「图片」" }; mSave.Click += (s, e) => DoSave(); moreMenu.Items.Add(mSave);
            var mPin = new MenuItem { Header = "钉在桌面" }; mPin.Click += (s, e) => DoPin(); moreMenu.Items.Add(mPin);
            moreMenu.PlacementTarget = more; moreMenu.Placement = PlacementMode.Bottom;
            more.MouseLeftButtonUp += (s, e) => { e.Handled = true; moreMenu.IsOpen = true; };
            sp.Children.Add(tight(more));
            var close = UiKit.IconButton("", "关闭（Esc）", Finish);
            ((TextBlock)close.Child).FontSize = 11;
            sp.Children.Add(close);
            var b = new Border
            {
                Child = sp,
                Background = Theme.White,
                CornerRadius = new CornerRadius(16),
                BorderBrush = Theme.Line,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Arrow,
                Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Direction = 270, Opacity = 0.16, Color = Color.FromRgb(0x18, 0x20, 0x28) }
            };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return b;
        }

        // 工具按钮：选中时浅绿底 + 绿色图标
        Border ToolButton(string glyph, string tip, string name)
        {
            var btn = UiKit.IconButton(glyph, tip, () => SetTool(name));
            btn.MouseLeave += (s, e) => { if (tool == name) btn.Background = Theme.AccentSoft; };
            return btn;
        }

        void SetTool(string name)
        {
            tool = tool == name ? null : name;
            foreach (var pair in new[] { new KeyValuePair<Border, string>(penBtn, "pen"), new KeyValuePair<Border, string>(hlBtn, "hl") })
            {
                bool on = tool == pair.Value;
                pair.Key.Background = on ? Theme.AccentSoft : Theme.Clear;
                ((TextBlock)pair.Key.Child).Foreground = on ? Theme.Accent : Theme.Muted;
            }
            BuildColors();
            Cursor = tool != null ? Cursors.Pen : Cursors.Cross;
            if (toolbar.Visibility == Visibility.Visible) ShowToolbar();
        }

        void BuildColors()
        {
            colorRow.Children.Clear();
            colorRow.Visibility = tool == null ? Visibility.Collapsed : Visibility.Visible;
            if (tool == null) return;
            var colors = tool == "pen" ? PenColors : HlColors;
            var current = tool == "pen" ? penColor : hlColor;
            foreach (var c in colors)
            {
                var col = c;
                bool on = col == current;
                var dot = new Border
                {
                    Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Margin = new Thickness(2, 0, 2, 0),
                    BorderBrush = on ? Theme.Title : Brushes.Transparent, BorderThickness = new Thickness(2), Background = Brushes.White, Cursor = Cursors.Hand,
                    Child = new Border { CornerRadius = new CornerRadius(8), Margin = new Thickness(1.5), Background = new SolidColorBrush(col) }
                };
                dot.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                dot.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    if (tool == "pen") penColor = col; else hlColor = col;
                    BuildColors();
                };
                colorRow.Children.Add(dot);
            }
        }

        void BeginStroke(Point p)
        {
            bool hl = tool == "hl";
            cur = new Shapes.Polyline
            {
                Stroke = new SolidColorBrush(hl ? hlColor : penColor),
                StrokeThickness = hl ? 18 : 3,
                Opacity = hl ? 0.42 : 1,                   // 整条线一个透明度，自己和自己重叠不会越涂越深
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            cur.Points.Add(p);
            cur.Points.Add(new Point(p.X + 0.01, p.Y));    // 只点一下也留下一个圆点
            ink.Children.Add(cur);
            strokes.Add(cur);
            undoBtn.Opacity = 1;
            CaptureMouse();
        }

        void Undo()
        {
            if (strokes.Count == 0) return;
            var last = strokes[strokes.Count - 1];
            strokes.RemoveAt(strokes.Count - 1);
            ink.Children.Remove(last);
            undoBtn.Opacity = strokes.Count > 0 ? 1 : 0.35;
        }

        void ClearInk()
        {
            strokes.Clear();
            ink.Children.Clear();
            undoBtn.Opacity = 0.35;
            if (tool != null) SetTool(tool);
        }

        void ResetSelection()
        {
            ClearInk();
            sel = Rect.Empty;
            selRect.Visibility = Visibility.Collapsed;
            sizeTag.Visibility = Visibility.Collapsed;
            toolbar.Visibility = Visibility.Collapsed;
            hint.Visibility = Visibility.Visible;
            hover = Rect.Empty;
            UpdateHover(Mouse.GetPosition(canvas));
            UpdateMask();
        }

        void OnDown(object s, MouseButtonEventArgs e)
        {
            // 拿着画笔 / 荧光笔：在选区里画，选区外不响应（不会误把选区弄丢）
            if (tool != null && HasSelection && !selecting)
            {
                var p0 = e.GetPosition(canvas);
                if (sel.Contains(p0)) BeginStroke(p0);
                return;
            }
            if (e.ClickCount == 2 && HasSelection && sel.Contains(e.GetPosition(canvas))) { DoCopy(); return; }
            if (strokes.Count > 0) return;        // 已经有标注了，别因为点到外面就重新框选、丢掉标注
            start = e.GetPosition(canvas);
            selecting = true;
            dragged = false;
            toolbar.Visibility = Visibility.Collapsed;
            sel = Rect.Empty;
            CaptureMouse();
            // 先不清掉窗口高亮：拖动超过几像素才算框选，否则松开就是选中这个窗口
            UpdateHover(start);
        }

        void OnMove(object s, MouseEventArgs e)
        {
            var p = e.GetPosition(canvas);
            if (cur != null)
            {
                if (e.LeftButton != MouseButtonState.Pressed) { EndStroke(); return; }
                var last = cur.Points[cur.Points.Count - 1];
                if (Math.Abs(p.X - last.X) + Math.Abs(p.Y - last.Y) >= 1.5) cur.Points.Add(p);
                return;
            }
            if (!selecting)
            {
                if (sel.IsEmpty) UpdateHover(p);
                return;
            }
            if (!dragged)
            {
                if (Math.Abs(p.X - start.X) + Math.Abs(p.Y - start.Y) < 5) return;
                dragged = true;
                hint.Visibility = Visibility.Collapsed;
            }
            sel = new Rect(start, p);
            UpdateSel();
        }

        void EndStroke()
        {
            cur = null;
            ReleaseMouseCapture();
        }

        void OnUp(object s, MouseButtonEventArgs e)
        {
            if (cur != null) { EndStroke(); return; }
            if (!selecting) return;
            selecting = false;
            ReleaseMouseCapture();
            if (!dragged && !hover.IsEmpty) { sel = hover; hint.Visibility = Visibility.Collapsed; UpdateSel(); }
            if (!HasSelection) { ResetSelection(); return; }
            if (ocrMode) { DoOcr(); return; }
            ShowToolbar();
        }

        void UpdateSel()
        {
            ShowRect(sel);
            ink.Clip = new RectangleGeometry(sel);     // 标注只出现在选区里
        }

        void ShowRect(Rect r)
        {
            selRect.Visibility = Visibility.Visible;
            Canvas.SetLeft(selRect, r.X); Canvas.SetTop(selRect, r.Y);
            selRect.Width = r.Width; selRect.Height = r.Height;
            sizeText.Text = (int)Math.Round(r.Width * k) + " × " + (int)Math.Round(r.Height * k);
            sizeTag.Visibility = Visibility.Visible;
            Canvas.SetLeft(sizeTag, r.X + (r.Y > 26 ? 0 : 4));
            Canvas.SetTop(sizeTag, r.Y > 26 ? r.Y - 24 : r.Y + 4);
            UpdateMask();
        }

        void UpdateMask()
        {
            var full = new RectangleGeometry(new Rect(0, 0, Width, Height));
            Rect cut = !sel.IsEmpty ? sel : (selecting && dragged ? Rect.Empty : hover);
            mask.Data = cut.IsEmpty ? (Geometry)full : new CombinedGeometry(GeometryCombineMode.Exclude, full, new RectangleGeometry(cut));
        }

        void ShowToolbar()
        {
            toolbar.Visibility = Visibility.Visible;
            toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double w = toolbar.DesiredSize.Width, h = toolbar.DesiredSize.Height;
            double x = Math.Max(4, Math.Min(sel.Right - w, Width - w - 4));
            double y = sel.Bottom + 8;
            if (y + h > Height - 4) y = sel.Top - h - 8;
            if (y < 4) y = sel.Bottom - h - 8;
            Canvas.SetLeft(toolbar, x); Canvas.SetTop(toolbar, y);
        }

        BitmapSource Crop()
        {
            int x = (int)Math.Round(sel.X * k), y = (int)Math.Round(sel.Y * k);
            int w = (int)Math.Round(sel.Width * k), h = (int)Math.Round(sel.Height * k);
            x = Math.Max(0, Math.Min(x, shot.PixelWidth - 1)); y = Math.Max(0, Math.Min(y, shot.PixelHeight - 1));
            w = Math.Max(1, Math.Min(w, shot.PixelWidth - x)); h = Math.Max(1, Math.Min(h, shot.PixelHeight - y));
            var c = new CroppedBitmap(shot, new Int32Rect(x, y, w, h));
            if (strokes.Count == 0)
            {
                var wb = new WriteableBitmap(c);
                wb.Freeze();
                return wb;
            }
            // 有标注：把笔迹按物理像素画进截图里（复制、保存、钉图、问奶蛙都带着标注）
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(c, new Rect(0, 0, w, h));
                dc.PushTransform(new MatrixTransform(k, 0, 0, k, -x, -y));
                foreach (var st in strokes)
                {
                    if (st.Points.Count < 2) continue;
                    var geo = new StreamGeometry();
                    using (var g = geo.Open())
                    {
                        g.BeginFigure(st.Points[0], false, false);
                        g.PolyLineTo(st.Points.Skip(1).ToList(), true, true);
                    }
                    geo.Freeze();
                    var pen = new Pen(st.Stroke, st.StrokeThickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    dc.PushOpacity(st.Opacity);
                    dc.DrawGeometry(null, pen, geo);
                    dc.Pop();
                }
                dc.Pop();
            }
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        void Finish() { Close(); pet.EndScreenshot(); }

        void DoCopy()
        {
            if (!HasSelection) return;
            var img = Crop();
            Finish();
            pet.Say(Img.CopyImage(img) ? "截图已复制，可以直接粘贴 ✓" : "复制失败了，再试一次？", 3);
        }

        void DoSave()
        {
            var img = Crop();
            Finish();
            try
            {
                string path = Img.SaveToPictures(img);
                Img.CopyImage(img);
                pet.Say("已保存到「图片\\奶蛙截图」，也复制好了 ✓", 4);
            }
            catch (Exception ex) { pet.Say("保存失败：" + ex.Message, 5); }
        }

        void DoPin()
        {
            var img = Crop();
            var at = new Point(Left + sel.X, Top + sel.Y);
            Finish();
            new PinWindow(img, at, k).Show();
        }

        void DoOcr()
        {
            if (!HasSelection) return;
            var img = Crop();
            Finish();
            pet.Chat.RunVisionTask(img, "translate");
        }

        void DoExtract()
        {
            if (!HasSelection) return;
            var img = Crop();
            Finish();
            pet.Chat.RunVisionTask(img, "ocr");
        }

        void DoAsk()
        {
            var img = Crop();
            Finish();
            pet.Chat.SetAttachment(img);
            pet.Chat.ShowNear(pet);
        }
    }

    // 钉在桌面上的截图：拖动移动、滚轮缩放、双击关闭
    class PinWindow : Window
    {
        readonly BitmapSource img;
        readonly Image view;
        double zoom = 1;
        readonly double baseW, baseH;

        public PinWindow(BitmapSource img, Point at, double k)
        {
            this.img = img;
            UiKit.SetupPanel(this, 0);
            SizeToContent = SizeToContent.WidthAndHeight;
            baseW = img.PixelWidth / k; baseH = img.PixelHeight / k;
            view = new Image { Source = img, Width = baseW, Height = baseH, Stretch = Stretch.Fill };
            var frame = new Border { Child = view, BorderBrush = Theme.Accent, BorderThickness = new Thickness(1), Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = 0.3 } };
            var root = new Grid { Margin = new Thickness(12) };
            root.Children.Add(frame);
            Content = root;
            Left = at.X - 13; Top = at.Y - 13;
            Cursor = Cursors.SizeAll;
            ToolTip = "拖动移动 · 滚轮缩放 · 双击关闭 · 右键更多";
            MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { Close(); return; }
                try { DragMove(); } catch { }
            };
            MouseWheel += (s, e) =>
            {
                zoom = Math.Max(0.2, Math.Min(4, zoom * (e.Delta > 0 ? 1.1 : 1 / 1.1)));
                view.Width = baseW * zoom; view.Height = baseH * zoom;
            };
            var menu = new ContextMenu();
            menu.Items.Add(MakeItem("复制", () => Img.CopyImage(img)));
            menu.Items.Add(MakeItem("保存到图片文件夹", () => { try { Img.SaveToPictures(img); } catch { } }));
            menu.Items.Add(MakeItem("原始大小", () => { zoom = 1; view.Width = baseW; view.Height = baseH; }));
            menu.Items.Add(MakeItem("关闭", Close));
            ContextMenu = menu;
        }

        static MenuItem MakeItem(string header, Action a)
        {
            var mi = new MenuItem { Header = header, FontFamily = Theme.Ui };
            mi.Click += (s, e) => a();
            return mi;
        }
    }

    // ====================================================================
    // 奶蛙本体
    // ====================================================================
    class PetWindow : Window
    {
        double S = 2;                               // 像素放大倍数（可在右键菜单调整）
        readonly Image frog, spark;
        readonly TextBlock zz;
        readonly Shapes.Ellipse shadow;
        readonly ScaleTransform breathe = new ScaleTransform(1, 1);
        readonly RotateTransform tilt = new RotateTransform(0);
        readonly TranslateTransform hop = new TranslateTransform(0, 0);
        readonly TranslateTransform sparkBob = new TranslateTransform(0, 0);
        readonly TranslateTransform breath = new TranslateTransform(0, 0);
        readonly List<Dictionary<string, object>> quietClaude = new List<Dictionary<string, object>>();   // 全屏期间攒下的通知，退出全屏后合并提示一次
        readonly List<Reminder> quietReminders = new List<Reminder>();
        readonly Random rnd = new Random();
        readonly DispatcherTimer tick;
        public ChatWindow Chat;
        BubbleWindow bubble;
        Forms.NotifyIcon tray;
        HotkeyHost hotkeys;
        SettingsWindow settingsWin;
        string shownFace = "";
        int busy, sparkFrame, tickCount;
        DateTime lastInteract = DateTime.Now, blinkUntil = DateTime.MinValue, nextBlink, overrideUntil = DateTime.MinValue;
        string overrideFace;
        bool sleeping, userHidden, autoHidden, screenshotting;
        DateTime fsSince = DateTime.MinValue;
        Point downPos;
        bool mouseDown;

        public PetWindow()
        {
            S = Math.Max(2, Math.Min(4, Math.Round(Settings.Current.PetScale)));
            UiKit.SetupPanel(this, 0);
            Topmost = Settings.Current.Topmost;
            Title = "奶蛙";

            var canvas = new Canvas();
            shadow = new Shapes.Ellipse { Fill = new RadialGradientBrush(Color.FromArgb(0x55, 0, 0, 0), Color.FromArgb(0, 0, 0, 0)) };
            canvas.Children.Add(shadow);

            frog = Sprites.View(Sprites.Frog("stare"), S);
            var tg = new TransformGroup();
            tg.Children.Add(breathe); tg.Children.Add(tilt); tg.Children.Add(breath); tg.Children.Add(hop);
            frog.RenderTransform = tg;
            frog.RenderTransformOrigin = new Point(0.5, 1);
            canvas.Children.Add(frog);

            spark = Sprites.View(Sprites.Spark(0), S);
            var sg = new TransformGroup();
            sg.Children.Add(sparkBob); sg.Children.Add(breath); sg.Children.Add(hop);
            spark.RenderTransform = sg;
            canvas.Children.Add(spark);

            zz = new TextBlock { Text = "z Z", FontFamily = Theme.Serif, FontStyle = FontStyles.Italic, Foreground = Theme.B("#8A7A4A"), Visibility = Visibility.Collapsed };
            canvas.Children.Add(zz);
            Content = canvas;
            LayoutPet();

            // 呼吸 & 星芒浮动
            // 呼吸改成整像素的两帧起伏（不再连续拉伸整张像素图，像素行高保持一致）
            StartBreath();
            var bob = new DoubleAnimation(0, -4, TimeSpan.FromMilliseconds(1800)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            sparkBob.BeginAnimation(TranslateTransform.YProperty, bob);

            // 位置：默认右下角
            var wa = SystemParameters.WorkArea;
            var s = Settings.Current;
            if (s.PetLeft >= 0 && s.PetLeft < SystemParameters.VirtualScreenWidth - 40 && s.PetTop >= 0 && s.PetTop < SystemParameters.VirtualScreenHeight - 40)
            { Left = s.PetLeft; Top = s.PetTop; }
            else { Left = wa.Right - Width - 24; Top = wa.Bottom - Height + 6; }

            MouseLeftButtonDown += (o, e) => { mouseDown = true; downPos = PointToScreen(e.GetPosition(this)); Wake(); };
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += (o, e) => { if (mouseDown) { mouseDown = false; OnClick(); } };
            MouseEnter += (o, e) => { Wake(); };
            ContextMenu = BuildMenu();
            ContextMenuOpening += (o, e) => Wake();

            nextBlink = DateTime.Now.AddSeconds(3);
            tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            tick.Tick += OnTick;
            Loaded += (o, e) => Startup();
            ContentRendered += (o, e) => PetSplash.Close();   // 真身画好后撤掉快照
            Application.Current.Properties["pet"] = this;
        }

        // 按当前倍数摆放各部件（窗口只比奶蛙大一圈，留出跳跃和星芒的空间）
        void LayoutPet()
        {
            Width = 32 * S + 24;
            Height = 30 * S + 5 * S + 14 * S;
            frog.Width = 32 * S; frog.Height = 30 * S;
            spark.Width = 5 * S; spark.Height = 5 * S;
            double frogTop = Height - frog.Height - 3 * S;
            Canvas.SetLeft(frog, (Width - frog.Width) / 2);
            Canvas.SetTop(frog, frogTop);
            Canvas.SetLeft(spark, (Width - spark.Width) / 2);
            Canvas.SetTop(spark, frogTop - spark.Height - 1.5 * S);
            shadow.Width = 24 * S; shadow.Height = 3 * S;
            Canvas.SetLeft(shadow, (Width - shadow.Width) / 2);
            Canvas.SetTop(shadow, Height - 4.5 * S);
            zz.FontSize = 4.5 * S + 4;
            Canvas.SetLeft(zz, Width - 10 * S);
            Canvas.SetTop(zz, frogTop - 4 * S);
        }

        void StartBreath()
        {
            var br = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromMilliseconds(3200) };
            br.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            br.KeyFrames.Add(new DiscreteDoubleKeyFrame(-S, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1600))));
            breath.BeginAnimation(TranslateTransform.YProperty, br);
        }

        // 调整大小时保持脚底中心不动
        void SetPetScale(double s)
        {
            double cx = Left + Width / 2, bottom = Top + Height;
            S = s;
            LayoutPet();
            StartBreath();
            Left = cx - Width / 2;
            Top = bottom - Height;
            Settings.Current.PetScale = s;
            Settings.Current.PetLeft = Left; Settings.Current.PetTop = Top;
            Settings.Save();
            Laugh();
        }

        void Startup()
        {
            Chat = new ChatWindow(this);
            bubble = new BubbleWindow();
            SetupTray();
            hotkeys = new HotkeyHost();
            var failed = new List<string>();
            if (!hotkeys.Register(0x3, 0x53, () => StartScreenshot(false))) failed.Add("Ctrl+Alt+S");
            if (!hotkeys.Register(0x3, 0x44, () => StartScreenshot(true))) failed.Add("Ctrl+Alt+D");
            if (!hotkeys.Register(0x3, 0x54, () => SelectionTask("translate"))) failed.Add("Ctrl+Alt+T");
            if (!hotkeys.Register(0x3, 0x50, () => SelectionTask("polish"))) failed.Add("Ctrl+Alt+P");
            tick.Start();
            // 常驻工具不需要每天问好；只有第一次见面打个招呼
            if (!Settings.Current.Onboarded)
            {
                Settings.Current.Onboarded = true;
                Settings.Save();
                Notify("你好，我是奶蛙", "点我聊天；选中文字按 Ctrl+Alt+P 润色报告，Ctrl+Alt+S 截图。", 8, null);
            }
            if (failed.Count > 0)
            {
                Chat.AddNote("这些快捷键被别的软件占用了：" + string.Join("、", failed));
                Notify("有快捷键按了会没反应", string.Join("、", failed) + " 被别的软件占用了，可以从右键菜单使用这些功能。", 10, null);
            }
            if (!Ai.HasDeepSeek && !Ai.HasDoubao) Chat.AddNote("还没填 API 密钥：右上角 ⚙ 设置");
        }

        // ---------- 状态机 ----------
        void OnTick(object sender, EventArgs e)
        {
            tickCount++;
            var now = DateTime.Now;

            // 全屏游戏 / 视频持续 1.5 秒以上才躲起来（截图工具这种短暂的全屏层不算）；全屏一结束立刻回来
            if (tickCount % 2 == 0)
            {
                bool fs = !screenshotting && Native.IsBusyFullscreen();
                if (fs)
                {
                    if (fsSince == DateTime.MinValue) fsSince = now;
                    if (!autoHidden && IsVisible && !userHidden && (now - fsSince).TotalMilliseconds > 1500) { autoHidden = true; Hide(); if (bubble != null) bubble.Hide(); }
                }
                else
                {
                    fsSince = DateTime.MinValue;
                    if (autoHidden && !userHidden) { autoHidden = false; Show(); FlushQuiet(); }
                }
            }
            if (tickCount % 16 == 0) CheckReminders();
            if (tickCount % 4 == 0) CheckClaude();

            // 睡觉：闲置 15 分钟，或深夜闲置 3 分钟
            double idle = (now - lastInteract).TotalMinutes;
            bool night = now.Hour >= 23 || now.Hour < 6;
            sleeping = busy == 0 && (idle > 15 || (night && idle > 3));
            zz.Visibility = sleeping ? Visibility.Visible : Visibility.Collapsed;

            string f;
            if (busy > 0) f = "think";
            else if (overrideUntil > now) f = overrideFace;
            else if (sleeping) f = "sleep";
            else if (blinkUntil > now) f = "blink";
            else f = "stare";
            if (busy == 0 && !sleeping && overrideUntil <= now && now >= nextBlink)
            {
                blinkUntil = now.AddMilliseconds(140);
                nextBlink = now.AddMilliseconds(rnd.Next(2500, 6500));
                f = "blink";
            }
            SetFace(f);

            // 思考时星芒转起来
            if (busy > 0) { sparkFrame++; spark.Source = Sprites.Spark(sparkFrame / 2); }
            else if (spark.Source != Sprites.Spark(0)) spark.Source = Sprites.Spark(0);
            spark.Opacity = sleeping ? 0.35 : 1;
        }

        void SetFace(string f)
        {
            if (f == shownFace) return;
            shownFace = f;
            frog.Source = Sprites.Frog(f);
        }

        void Wake() { lastInteract = DateTime.Now; }

        public void BeginBusy() { busy++; Wake(); }
        public void EndBusy() { busy = Math.Max(0, busy - 1); Wake(); if (busy == 0) { overrideFace = "laugh"; overrideUntil = DateTime.Now.AddMilliseconds(700); } }

        public void Laugh()
        {
            overrideFace = "laugh";
            overrideUntil = DateTime.Now.AddMilliseconds(1300);
            var jump = new DoubleAnimationUsingKeyFrames();
            jump.KeyFrames.Add(new EasingDoubleKeyFrame(-14, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140)), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            jump.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)), new BounceEase { Bounces = 2, Bounciness = 3, EasingMode = EasingMode.EaseOut }));
            hop.BeginAnimation(TranslateTransform.YProperty, jump);
            var shake = new DoubleAnimationUsingKeyFrames();
            double[] seq = { 0, -5, 5, -4, 4, -2, 0 };
            for (int i = 0; i < seq.Length; i++) shake.KeyFrames.Add(new LinearDoubleKeyFrame(seq[i], KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500 + i * 90))));
            tilt.BeginAnimation(RotateTransform.AngleProperty, shake);
        }

        // ---------- 交互 ----------
        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!mouseDown || e.LeftButton != MouseButtonState.Pressed) return;
            if ((Native.GetAsyncKeyState(0x01) & 0x8000) == 0) return;   // 左键其实已松开：不拖动，留给松开事件当作点击
            var p = PointToScreen(e.GetPosition(this));
            if (Math.Abs(p.X - downPos.X) + Math.Abs(p.Y - downPos.Y) < 6) return;
            mouseDown = false;
            // 开始拖动：收起对话窗和气泡，免得留在原地
            if (Chat != null && Chat.IsVisible) Chat.Hide();
            if (bubble != null && bubble.IsVisible) bubble.Hide();
            try { DragMove(); } catch { }
            Settings.Current.PetLeft = Left; Settings.Current.PetTop = Top;
            Settings.Save();
        }

        // 点一下：轻轻弹一下（不再连跳带抖）
        void Hop()
        {
            var jump = new DoubleAnimationUsingKeyFrames();
            jump.KeyFrames.Add(new EasingDoubleKeyFrame(-2 * S, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110)), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            jump.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
            hop.BeginAnimation(TranslateTransform.YProperty, jump);
        }

        void OnClick()
        {
            Hop();
            Chat.Toggle(this);
        }

        public void Say(string text, double seconds)
        {
            if (bubble == null || !IsVisible) return;
            bubble.ShowText(text, seconds, this);
        }

        public void Notify(string title, string body, double seconds, List<KeyValuePair<string, Action>> acts, bool gold = false)
        {
            if (bubble == null || !IsVisible) return;
            bubble.Notify(title, body, seconds, this, acts, gold);
        }

        static KeyValuePair<string, Action> Act(string label, Action a) { return new KeyValuePair<string, Action>(label, a); }

        public ContextMenu MakeMenu() { return BuildMenu(); }

        ContextMenu BuildMenu()
        {
            var m = UiKit.StyledMenu();
            m.Items.Add(Item("截图", "Ctrl+Alt+S", () => StartScreenshot(false)));
            m.Items.Add(Item("截图翻译", "Ctrl+Alt+D", () => StartScreenshot(true)));
            m.Items.Add(Item("翻译选中文字", "Ctrl+Alt+T", () => SelectionTask("translate")));
            m.Items.Add(Item("润色选中文字（报告）", "Ctrl+Alt+P", () => SelectionTask("polish")));
            m.Items.Add(Item("查看提醒", null, () => Chat.ShowReminders()));
            m.Items.Add(new Separator());
            var size = new MenuItem { Header = "大小" };
            size.Items.Add(Item("小", null, () => SetPetScale(2)));
            size.Items.Add(Item("中", null, () => SetPetScale(3)));
            size.Items.Add(Item("大", null, () => SetPetScale(4)));
            m.Items.Add(size);
            m.Items.Add(Item("设置…", null, OpenSettings));
            m.Items.Add(Item("藏起来", null, () => { userHidden = true; Hide(); bubble.Hide(); tray.ShowBalloonTip(3000, "奶蛙藏起来了", "单击托盘里的奶蛙图标就能叫它回来", Forms.ToolTipIcon.Info); }));
            m.Items.Add(Item("退出", null, Quit));
            return m;
        }

        static MenuItem Item(string header, string gesture, Action a)
        {
            var mi = new MenuItem { Header = header, InputGestureText = gesture ?? "" };
            mi.Click += (s, e) => a();
            return mi;
        }

        void SetupTray()
        {
            tray = new Forms.NotifyIcon { Icon = Sprites.TrayIcon(), Text = "奶蛙助理 · 单击显示 / 隐藏", Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("和奶蛙聊天", null, (s, e) => { ShowPet(); Chat.ShowNear(this); });
            menu.Items.Add("截图　Ctrl+Alt+S", null, (s, e) => StartScreenshot(false));
            menu.Items.Add("截图识别 / 翻译　Ctrl+Alt+D", null, (s, e) => StartScreenshot(true));
            menu.Items.Add("设置…", null, (s, e) => OpenSettings());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => Quit());
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) =>
            {
                if (e.Button != Forms.MouseButtons.Left) return;
                if (IsVisible) { userHidden = true; Hide(); bubble.Hide(); } else ShowPet();
            };
        }

        void ShowPet()
        {
            userHidden = false; autoHidden = false;
            Show();
            Wake();
            Laugh();
        }

        public void ApplySettings()
        {
            Topmost = Settings.Current.Topmost;
        }

        public void OpenSettings()
        {
            if (settingsWin != null && settingsWin.IsVisible) { settingsWin.Activate(); return; }
            settingsWin = new SettingsWindow(this);
            settingsWin.Closed += (s, e) => settingsWin = null;
            var wa = SystemParameters.WorkArea;
            settingsWin.Left = wa.Left + (wa.Width - settingsWin.Width) / 2;
            settingsWin.Top = wa.Top + 80;
            settingsWin.Show();
            settingsWin.Activate();
        }

        // ---------- 截图 ----------
        public async void StartScreenshot(bool ocr)
        {
            if (screenshotting) return;
            screenshotting = true;
            Wake();
            bool petWasVisible = IsVisible, chatWasVisible = Chat.IsVisible;
            if (petWasVisible) Opacity = 0;
            if (chatWasVisible) Chat.Opacity = 0;
            if (bubble.IsVisible) bubble.Hide();
            await Task.Delay(140);
            try
            {
                var o = new ScreenshotOverlay(this, ocr);
                o.Show();
                o.Activate();
            }
            catch (Exception ex) { screenshotting = false; Program.Log("截图失败: " + ex); }
            Opacity = 1;
            Chat.Opacity = 1;
        }

        public void EndScreenshot()
        {
            screenshotting = false;
            // 截图结束立刻回来，不等下一次检查
            Opacity = 1;
            fsSince = DateTime.MinValue;
            if (autoHidden && !userHidden) { autoHidden = false; Show(); FlushQuiet(); }
        }

        // ---------- 划词翻译 / 润色 ----------
        async void SelectionTask(string kind)
        {
            Wake();
            string text = await GrabSelection();
            if (string.IsNullOrWhiteSpace(text)) { Chat.SelectionFailed(kind); return; }
            await Chat.RunTextTask(kind, text);
        }

        static async Task<string> GrabSelection()
        {
            // 等用户松开 Ctrl / Alt，再模拟一次 Ctrl+C 复制当前选中的文字
            for (int i = 0; i < 40; i++)
            {
                if ((Native.GetAsyncKeyState(0x11) & 0x8000) == 0 && (Native.GetAsyncKeyState(0x12) & 0x8000) == 0 && (Native.GetAsyncKeyState(0x10) & 0x8000) == 0) break;
                await Task.Delay(25);
            }
            uint before = Native.GetClipboardSequenceNumber();
            Native.keybd_event(0x11, 0, 0, UIntPtr.Zero);
            Native.keybd_event(0x43, 0, 0, UIntPtr.Zero);
            Native.keybd_event(0x43, 0, 2, UIntPtr.Zero);
            Native.keybd_event(0x11, 0, 2, UIntPtr.Zero);
            bool changed = false;
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(30);
                if (Native.GetClipboardSequenceNumber() != before) { changed = true; break; }
            }
            // 剪贴板没变 = 没复制到选中内容；绝不拿上一次复制的旧内容去处理
            if (!changed) return null;
            await Task.Delay(30);
            for (int i = 0; i < 5; i++)
            {
                bool ok = false; string text = null;
                try { text = Clipboard.ContainsText() ? Clipboard.GetText() : null; ok = true; }
                catch { }
                if (ok) return text;
                await Task.Delay(50);
            }
            return null;
        }

        // ---------- 提醒 ----------
        void CheckReminders()
        {
            var list = Settings.Current.Reminders;
            if (list.Count == 0) return;
            long now = DateTime.Now.Ticks;
            var due = list.Where(r => r.At <= now).OrderBy(r => r.At).ToList();
            if (due.Count == 0) return;
            foreach (var r in due)
            {
                list.Remove(r);
                Chat.AddNote(DateTime.Now.ToString("HH:mm") + " 提醒：" + r.Text);
            }
            Settings.Save();
            if (autoHidden) { quietReminders.AddRange(due); return; }   // 全屏中：先攒着，退出全屏再合并提示
            ShowReminders(due, null);
        }

        // 到点的提醒：一条通知，「完成」或「10 分钟后」；只响一次
        void ShowReminders(List<Reminder> due, string extra)
        {
            string title = due.Count == 1 ? "奶蛙提醒 · " + new DateTime(due[0].At).ToString("HH:mm") : due.Count + " 个提醒到了";
            string body = string.Join("；", due.Select(r => r.Text)) + (extra != null ? "\n" + extra : "");
            if (userHidden)
            {
                tray.ShowBalloonTip(10000, title, body, Forms.ToolTipIcon.Info);
                return;
            }
            Wake();
            Laugh();
            System.Media.SystemSounds.Exclamation.Play();
            var snooze = due.ToList();
            Notify(title, body, 30, new List<KeyValuePair<string, Action>>
            {
                Act("完成", () => { }),
                Act("10 分钟后", () => { foreach (var r in snooze) ChatWindow.Snooze(r.Text, 10); Say("好，10 分钟后再叫你", 2.5); })
            }, true);
        }

        // Claude Code 一轮回复结束（钩子只知道「回复结束」，不代表任务成功，所以如实这么说）
        void CheckClaude()
        {
            var msgs = ClaudeHook.Drain();
            if (msgs.Count == 0) return;
            foreach (var m in msgs) Chat.AddNote(DateTime.Now.ToString("HH:mm") + " Claude 回复结束" + (Str(m, "text").Length > 0 ? "：" + Str(m, "text") : ""));
            if (autoHidden) { quietClaude.AddRange(msgs); return; }   // 全屏中不打扰，退出后合并提示
            ShowClaude(msgs);
        }

        static string Str(Dictionary<string, object> m, string k)
        {
            object v; return m != null && m.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        void ShowClaude(List<Dictionary<string, object>> msgs)
        {
            var last = msgs[msgs.Count - 1];
            string text = Str(last, "text"), project = Str(last, "project");
            if (project.StartsWith("scratch-")) project = "";      // 临时工作区的名字没意义
            string title = (msgs.Count > 1 ? "Claude · " + msgs.Count + " 轮回复结束" : "Claude · 回复结束") + (project.Length > 0 ? " · " + project : "");
            bool watching = Native.ForegroundProcessName().Equals("claude", StringComparison.OrdinalIgnoreCase);
            if (userHidden)
            {
                if (!watching) tray.ShowBalloonTip(6000, title, text.Length > 0 ? text : "回去看看结果吧", Forms.ToolTipIcon.Info);
                return;
            }
            Wake();
            var acts = new List<KeyValuePair<string, Action>> { Act("打开 Claude", Native.ActivateClaude) };
            if (watching) { Notify(title, text, 4, null); return; }   // 正在看 Claude：轻轻提一句，不出声
            Hop();
            System.Media.SystemSounds.Asterisk.Play();
            Notify(title, text, 8, acts);
        }

        // 退出全屏：把攒下的提醒和 Claude 通知合并成一次
        void FlushQuiet()
        {
            if (quietReminders.Count == 0 && quietClaude.Count == 0) return;
            var rs = quietReminders.ToList(); quietReminders.Clear();
            var cs = quietClaude.ToList(); quietClaude.Clear();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (rs.Count > 0) ShowReminders(rs, cs.Count > 0 ? "另外 Claude 有 " + cs.Count + " 轮回复结束" : null);
                else ShowClaude(cs);
            }), DispatcherPriority.Background);
        }

        void Quit()
        {
            Settings.Current.PetLeft = Left; Settings.Current.PetTop = Top;
            Settings.Save();
            tray.Visible = false;
            tray.Dispose();
            Application.Current.Shutdown();
        }
    }

    // ====================================================================
    // 开机秒出：WPF 准备好之前，先用 Win32 分层窗口把奶蛙画出来（约 0.1 秒）
    // ====================================================================
    static class PetSplash
    {
        delegate IntPtr WndProcD(IntPtr h, uint m, IntPtr w, IntPtr l);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASS { public uint style; public WndProcD proc; public int c1, c2; public IntPtr inst, icon, cursor, bg; public string menu, name; }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClass(ref WNDCLASS c);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(int ex, string cls, string t, uint st, int x, int y, int w, int h, IntPtr p, IntPtr m, IntPtr i, IntPtr lp);
        [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
        [StructLayout(LayoutKind.Sequential)] struct PT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct SZ { public int W, H; }
        [StructLayout(LayoutKind.Sequential)] struct RC { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte op, flags, alpha, fmt; }
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref PT pos, ref SZ size, IntPtr src, ref PT srcPos, int key, ref BLEND b, int flags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint b, ref RC r, uint c);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        static WndProcD proc;
        static IntPtr hwnd;

        static readonly Dictionary<char, System.Drawing.Color> Pal = new Dictionary<char, System.Drawing.Color>
        {
            { 'K', System.Drawing.Color.FromArgb(0x34, 0x3C, 0x34) }, { 'Y', System.Drawing.Color.FromArgb(0xF7, 0xEE, 0xC6) },
            { 'L', System.Drawing.Color.FromArgb(0xFF, 0xFD, 0xF5) }, { 'G', System.Drawing.Color.FromArgb(0xD8, 0xE6, 0xC5) },
            { 'B', System.Drawing.Color.FromArgb(0x1C, 0x22, 0x1C) }, { 'H', System.Drawing.Color.FromArgb(0x8E, 0x94, 0x71) },
            { 'C', System.Drawing.Color.FromArgb(0xD7, 0x95, 0x38) }
        };

        static double Num(string json, string key, double def)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?[0-9.]+)");
            double v;
            return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : def;
        }

        public static void Show()
        {
            try
            {
                string file = Path.Combine(Settings.Dir, "settings.json");
                string json = File.Exists(file) ? File.ReadAllText(file, Encoding.UTF8) : "";
                int S = (int)Math.Max(2, Math.Min(4, Math.Round(Num(json, "PetScale", 2))));
                int W = 32 * S + 24, H = 49 * S;
                double left = Num(json, "PetLeft", -1), top = Num(json, "PetTop", -1);
                if (left < 0 || top < 0)
                {
                    var wa = new RC(); SystemParametersInfo(0x30, 0, ref wa, 0);
                    left = wa.R - W - 24; top = wa.B - H + 6;
                }
                // 与 PetWindow.LayoutPet 相同的摆放
                int frogTop = H - 30 * S - 3 * S, frogLeft = (W - 32 * S) / 2;
                using (var bmp = new System.Drawing.Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.Clear(System.Drawing.Color.Transparent);
                        Fill(g, SpriteData.FrogRows("stare"), frogLeft, frogTop, S);
                        Fill(g, SpriteData.SparkA, (W - 5 * S) / 2, (int)(frogTop - 5 * S - 1.5 * S), S);
                    }
                    if (proc == null) { proc = DefWindowProc; var wc = new WNDCLASS { proc = proc, name = "NaiWaSplash" }; RegisterClass(ref wc); }
                    hwnd = CreateWindowEx(0x80000 | 0x80 | 0x8000000 | 0x8 | 0x20, "NaiWaSplash", "", 0x80000000, (int)left, (int)top, W, H, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    IntPtr sdc = GetDC(IntPtr.Zero), mdc = CreateCompatibleDC(sdc);
                    IntPtr hb = bmp.GetHbitmap(System.Drawing.Color.FromArgb(0));
                    IntPtr old = SelectObject(mdc, hb);
                    var pos = new PT { X = (int)left, Y = (int)top }; var sz = new SZ { W = W, H = H }; var zero = new PT();
                    var bl = new BLEND { op = 0, flags = 0, alpha = 255, fmt = 1 };
                    UpdateLayeredWindow(hwnd, sdc, ref pos, ref sz, mdc, ref zero, 0, ref bl, 2);
                    SelectObject(mdc, old); Native.DeleteObject(hb); DeleteDC(mdc); ReleaseDC(IntPtr.Zero, sdc);
                    ShowWindow(hwnd, 4);
                }
            }
            catch (Exception ex) { Program.Log("开机快照失败: " + ex.Message); }
        }

        static void Fill(System.Drawing.Graphics g, string[] rows, int ox, int oy, int s)
        {
            var brushes = new Dictionary<char, System.Drawing.SolidBrush>();
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char ch = rows[y][x];
                    System.Drawing.Color col;
                    if (ch == '.' || !Pal.TryGetValue(ch, out col)) continue;
                    System.Drawing.SolidBrush b;
                    if (!brushes.TryGetValue(ch, out b)) { b = new System.Drawing.SolidBrush(col); brushes[ch] = b; }
                    g.FillRectangle(b, ox + x * s, oy + y * s, s, s);
                }
            foreach (var b in brushes.Values) b.Dispose();
        }

        public static void Close() { if (hwnd != IntPtr.Zero) { DestroyWindow(hwnd); hwnd = IntPtr.Zero; } }
    }

    // ====================================================================
    static class Program
    {
        public static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                File.AppendAllText(Path.Combine(Settings.Dir, "error.log"), "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        public static void SetAutoStart(bool on)
        {
            try
            {
                string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "奶蛙助理.lnk");
                if (!on) { if (File.Exists(lnk)) File.Delete(lnk); return; }
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic sh = Activator.CreateInstance(t);
                dynamic sc = sh.CreateShortcut(lnk);
                sc.TargetPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                sc.WorkingDirectory = Settings.Dir;
                sc.Description = "奶蛙助理";
                sc.Save();
            }
            catch (Exception ex) { Log("设置开机自启失败: " + ex.Message); }
        }

        // 开发用：NaiWa.exe --snapshot <目录>
        // 把形象、气泡、对话窗、右键菜单、截图工具栏离屏渲染成 PNG（不抢焦点、不出现在屏幕上），README 配图就是这样生成的
        static void Snapshot(string dir)
        {
            Directory.CreateDirectory(dir);
            Settings.Load();
            Settings.Current.Reminders = new List<Reminder>
            {
                new Reminder { At = DateTime.Today.AddHours(15).Ticks, Text = "交实验报告" },
                new Reminder { At = DateTime.Today.AddDays(1).AddHours(9).AddMinutes(30).Ticks, Text = "检查仿真结果" }
            };
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Startup += (s, e) =>
            {
                var pet = new PetWindow();
                var chat = new ChatWindow(pet);
                chat.ShowActivated = false; chat.Left = -6000; chat.Top = -6000;
                chat.Show();
                chat.DemoFill();
                var menu = pet.MakeMenu();   // 菜单不挂到窗口里，单独排版后直接渲染
                menu.ApplyTemplate();
                menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                menu.Arrange(new Rect(menu.DesiredSize));
                var tbars = new StackPanel { Background = Brushes.Transparent };
                foreach (bool ocr in new[] { false, true, false })
                {
                    var o = new ScreenshotOverlay(pet, ocr);
                    if (tbars.Children.Count == 2) o.DemoTool();
                    var tb = o.ToolbarForSnapshot;
                    tb.Margin = new Thickness(16, 12, 16, 12);
                    tbars.Children.Add(tb);
                }
                tbars.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                tbars.Arrange(new Rect(tbars.DesiredSize));

                // 两种通知气泡：Claude 回复结束 / 到点提醒
                var notes = new StackPanel { Orientation = Orientation.Horizontal };
                var noop = new Action(() => { });
                var b1 = new BubbleWindow();
                b1.Fill("Claude · 回复结束 · my-project", "测试全部通过，已经把修复推到 fix/login 分支。", new List<KeyValuePair<string, Action>> { new KeyValuePair<string, Action>("打开 Claude", noop) }, false);
                var b2 = new BubbleWindow();
                b2.Fill("奶蛙提醒 · 15:00", "交实验报告", new List<KeyValuePair<string, Action>> { new KeyValuePair<string, Action>("完成", noop), new KeyValuePair<string, Action>("10 分钟后", noop) }, true);
                foreach (var b in new[] { b1, b2 })
                {
                    var el = (FrameworkElement)b.Content;
                    b.Content = null;
                    notes.Children.Add(el);
                }
                notes.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                notes.Arrange(new Rect(notes.DesiredSize));

                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    SaveSprites(Path.Combine(dir, "sprites.png"));
                    Save(notes, Path.Combine(dir, "notify.png"));
                    Save((FrameworkElement)chat.Content, Path.Combine(dir, "chat.png"));
                    Save(menu, Path.Combine(dir, "menu.png"));
                    Save(tbars, Path.Combine(dir, "toolbar.png"));
                    app.Shutdown();
                }), DispatcherPriority.ApplicationIdle);
            };
            app.Run();
        }

        static readonly Color SnapBg = Color.FromRgb(0xE4, 0xE8, 0xEB);   // 浅灰底，衬出白卡片

        static void Save(FrameworkElement el, string path)
        {
            TextOptions.SetTextRenderingMode(el, TextRenderingMode.Grayscale);   // 导出图片用灰度抗锯齿，避免 ClearType 彩边
            el.UpdateLayout();
            int w = (int)Math.Ceiling(el.ActualWidth), h = (int)Math.Ceiling(el.ActualHeight);
            var bg = new DrawingVisual();
            using (var dc = bg.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(SnapBg), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new VisualBrush(el), null, new Rect(0, 0, w, h));
            }
            WritePng(bg, w, h, path);
        }

        // 五种表情一字排开，最近邻放大保持像素颗粒
        static void SaveSprites(string path)
        {
            string[] faces = { "stare", "blink", "laugh", "think", "sleep" };
            const int sc = 6, pad = 28, gap = 36;
            int fw = 32 * sc, fh = 30 * sc, sh = 5 * sc;
            int w = pad * 2 + faces.Length * fw + (faces.Length - 1) * gap, h = pad * 2 + sh + 8 + fh;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(SnapBg), null, new Rect(0, 0, w, h));
                for (int i = 0; i < faces.Length; i++)
                {
                    double x = pad + i * (fw + gap);
                    var spark = Sprites.Spark(faces[i] == "think" ? 1 : 0);
                    dc.DrawImage(Nearest(spark, sc), new Rect(x + (fw - sh) / 2, pad, sh, sh));
                    dc.DrawImage(Nearest(Sprites.Frog(faces[i]), sc), new Rect(x, pad + sh + 8, fw, fh));
                }
            }
            WritePng(dv, w, h, path);
        }

        // 逐像素放大（保证像素颗粒清晰，不被插值糊掉）
        static BitmapSource Nearest(BitmapSource src, int s)
        {
            var f = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int w = f.PixelWidth, h = f.PixelHeight;
            var px = new byte[w * h * 4];
            f.CopyPixels(px, w * 4, 0);
            var big = new byte[w * s * h * s * 4];
            for (int y = 0; y < h * s; y++)
                for (int x = 0; x < w * s; x++)
                    Buffer.BlockCopy(px, ((y / s) * w + x / s) * 4, big, (y * w * s + x) * 4, 4);
            var b = BitmapSource.Create(w * s, h * s, 96, 96, PixelFormats.Bgra32, null, big, w * s * 4);
            b.Freeze();
            return b;
        }

        static void WritePng(Visual v, int w, int h, string path)
        {
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(v);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(path)) enc.Save(fs);
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--claude-hook") { ClaudeHook.RunHook(); return; }
            if (args.Length > 1 && args[0] == "--snapshot") { Snapshot(args[1]); return; }
            bool created;
            var mutex = new Mutex(true, "Local\\NaiWa_SingleInstance", out created);
            if (!created) return;
            PetSplash.Show();   // 先把奶蛙画出来，再慢慢加载其余部分
            try { System.Runtime.ProfileOptimization.SetProfileRoot(Settings.Dir); System.Runtime.ProfileOptimization.StartProfile("startup.jit"); } catch { }
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;  // TLS 1.2
            Settings.Load();
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += (s, e) => { Log(e.Exception.ToString()); e.Handled = true; };
            TaskScheduler.UnobservedTaskException += (s, e) => { Log(e.Exception.ToString()); e.SetObserved(); };
            var pet = new PetWindow();
            pet.Show();
            app.Run();
            GC.KeepAlive(mutex);
        }
    }
}
