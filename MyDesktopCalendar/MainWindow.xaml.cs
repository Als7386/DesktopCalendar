using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices; // 윈도우 시스템 함수 사용
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace MyDesktopCalendar
{
    public partial class MainWindow : Window
    {
        // 현재 달력의 기준 날짜
        private DateTime _currentDate = DateTime.Today;

        // 메모 데이터 (날짜Key : 내용Value)
        private Dictionary<string, string> _memoData = new Dictionary<string, string>();

        // 데이터 저장 경로 (실행 파일 옆 data.txt)
        private string _saveFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.txt");

        // 팝업용 임시 변수
        private DateTime _selectedDateForMemo;

        public MainWindow()
        {
            InitializeComponent();

            // 1. 저장된 위치 불러오기 (Properties 설정 사용)
            // 만약 처음 켜서 값이 0이면 기본 위치 사용
            if (Properties.Settings.Default.WindowTop != 0)
            {
                this.Top = Properties.Settings.Default.WindowTop;
                this.Left = Properties.Settings.Default.WindowLeft;
            }

            // 2. 메모 파일 불러오기
            LoadMemos();

            // 3. 달력 그리기
            UpdateCalendar();

            // 4. 앱 로드 완료 시 이벤트 (바탕화면 고정용)
            this.Loaded += MainWindow_Loaded;

            // 5. 앱 종료 시 이벤트 (위치 저장용)
            this.Closing += MainWindow_Closing;
        }

        // =========================================================
        // [기능 1] 윈도우 기본 동작 (드래그, 종료, 위치 저장)
        // =========================================================

        // 마우스로 창 이동
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 마우스 왼쪽 버튼이 '현재 눌려있는 상태'인지 한번 더 확인
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // 닫기(X) 버튼 클릭
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // 앱 꺼질 때 위치 저장
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.WindowTop = this.Top;
            Properties.Settings.Default.WindowLeft = this.Left;
            Properties.Settings.Default.Save();
        }

        // =========================================================
        // [기능 2] 바탕화면 뒤에 박제 (Win32 API)
        // =========================================================

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WindowInteropHelper wih = new WindowInteropHelper(this);
            // 창을 가장 아래(Bottom)로 보냄
            SetWindowPos(wih.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;


        // =========================================================
        // [기능 3] 달력 로직 및 파일 입출력
        // =========================================================

        private void LoadMemos()
        {
            if (File.Exists(_saveFilePath))
            {
                string[] lines = File.ReadAllLines(_saveFilePath);
                foreach (var line in lines)
                {
                    var parts = line.Split(new char[] { '|' }, 2);
                    if (parts.Length == 2) _memoData[parts[0]] = parts[1];
                }
            }
        }

        private void SaveMemosToFile()
        {
            List<string> lines = new List<string>();
            foreach (var kvp in _memoData)
            {
                string cleanContent = kvp.Value.Replace("\n", "\\n"); // 줄바꿈 문자 처리
                lines.Add($"{kvp.Key}|{cleanContent}");
            }
            File.WriteAllLines(_saveFilePath, lines);
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentDate.AddMonths(-1);
            UpdateCalendar();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = _currentDate.AddMonths(1);
            UpdateCalendar();
        }

        private void UpdateCalendar()
        {
            TxtYearMonth.Text = _currentDate.ToString("yyyy년 M월");
            CalendarGrid.Children.Clear();

            DateTime firstDay = new DateTime(_currentDate.Year, _currentDate.Month, 1);
            int dayOfWeek = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);

            // 1. 빈칸 채우기
            for (int i = 0; i < dayOfWeek; i++) CalendarGrid.Children.Add(new TextBlock());

            // 2. 날짜 채우기
            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime thisDayDate = new DateTime(_currentDate.Year, _currentDate.Month, day);
                string dateKey = thisDayDate.ToString("yyyy-MM-dd");

                // 클릭 가능한 박스 (Border)
                Border dayBtn = new Border
                {
                    Background = Brushes.Transparent, // 투명 클릭 영역
                    Cursor = Cursors.Hand,
                    Tag = thisDayDate,
                    Padding = new Thickness(2) // 테두리와 글자 사이 여백 약간 줌
                };
                dayBtn.MouseLeftButtonDown += DayBtn_Click;

                // 내용물을 담을 패널 (날짜 + 메모 텍스트)
                StackPanel sp = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Top, // 위쪽 정렬 (글자가 많아지면 위에서부터 채움)
                    HorizontalAlignment = HorizontalAlignment.Stretch // 가로로 꽉 채우기
                };

                // [날짜 숫자 표시]
                TextBlock txtDate = new TextBlock
                {
                    Text = day.ToString(),
                    HorizontalAlignment = HorizontalAlignment.Center, // 날짜는 가운데 정렬
                    Foreground = Brushes.White,
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 0, 5) // 날짜와 메모 사이 간격
                };

                // 오늘 날짜면 노란색 강조
                if (thisDayDate == DateTime.Today)
                {
                    txtDate.Foreground = Brushes.Yellow;
                    txtDate.FontWeight = FontWeights.Bold;
                }
                sp.Children.Add(txtDate);

                // [메모 내용 표시] - 여기가 핵심 변경 사항
                if (_memoData.ContainsKey(dateKey))
                {
                    // 저장된 메모 내용 가져오기 (줄바꿈 문자가 있다면 원래대로 복구해서 보여주거나, 한 줄로 보여주거나 선택)
                    // 여기서는 깔끔하게 보여주기 위해 줄바꿈을 공백으로 바꿔서 한 줄 처럼 보여줍니다.
                    string memoContent = _memoData[dateKey].Replace("\\n", " ");

                    TextBlock txtMemo = new TextBlock
                    {
                        Text = memoContent,
                        FontSize = 11,              // 글자 크기는 작게
                        Foreground = Brushes.Cyan,  // 색상은 밝은 하늘색
                        TextWrapping = TextWrapping.Wrap, // 너무 길면 다음 줄로
                        TextTrimming = TextTrimming.CharacterEllipsis, // 그래도 넘치면 ... 처리
                        MaxHeight = 40,             // 최대 높이 제한 (칸이 너무 커지지 않게)
                        HorizontalAlignment = HorizontalAlignment.Center, // 가운데 정렬
                        TextAlignment = TextAlignment.Center // 텍스트 내부 정렬
                    };
                    sp.Children.Add(txtMemo);
                }

                dayBtn.Child = sp;
                CalendarGrid.Children.Add(dayBtn);
            }
        }

        // =========================================================
        // [기능 4] 메모 팝업 이벤트
        // =========================================================

        private void DayBtn_Click(object sender, MouseButtonEventArgs e)
        {
            // 클릭한 버튼에서 날짜 꺼내기
            Border clickedBtn = sender as Border;
            _selectedDateForMemo = (DateTime)clickedBtn.Tag;

            // 팝업 UI 세팅
            PopupDateText.Text = _selectedDateForMemo.ToString("yyyy년 M월 d일 일정");
            string dateKey = _selectedDateForMemo.ToString("yyyy-MM-dd");

            if (_memoData.ContainsKey(dateKey))
                MemoInput.Text = _memoData[dateKey].Replace("\\n", "\n");
            else
                MemoInput.Text = "";

            MemoPopup.Visibility = Visibility.Visible;
        }

        private void BtnSaveMemo_Click(object sender, RoutedEventArgs e)
        {
            string dateKey = _selectedDateForMemo.ToString("yyyy-MM-dd");
            string content = MemoInput.Text.Trim();

            if (string.IsNullOrEmpty(content))
            {
                if (_memoData.ContainsKey(dateKey)) _memoData.Remove(dateKey);
            }
            else
            {
                _memoData[dateKey] = content;
            }

            SaveMemosToFile();
            UpdateCalendar();
            MemoPopup.Visibility = Visibility.Collapsed;
        }

        private void BtnClosePopup_Click(object sender, RoutedEventArgs e)
        {
            MemoPopup.Visibility = Visibility.Collapsed;
        }
        // =========================================================
        // [기능 5] 윈도우 크기 조절 (Resize)
        // =========================================================

        // 리사이즈 핸들 클릭 시 실행
        private void Resize_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                WindowInteropHelper wih = new WindowInteropHelper(this);
                SendMessage(wih.Handle, 0x112, (IntPtr)0xF008, IntPtr.Zero);
                // 윈도우의 DragMove로 넘어가지 않게 막음
                e.Handled = true;
            }
        }

        // 윈도우 메시지 전송 함수 (기존에 SetWindowPos 때문에 DllImport 쓴 곳 아래에 추가)
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }

}
