using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Collections.ObjectModel;       // ObservableCollection class
using System.Windows.Media.Animation;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace TaskList
{
    public enum TaskStatus
    {
        IDLE,
        WORKING,
        PASUE,
        STOP
    }
    [Serializable]
    public class TaskItem
    {

        public TaskStatus Status { get; set; }
        public DateTime TimeStart { get; set; }
        public TimeSpan TimeTotal { get; set; }
        public string TimeStr { get; set; }
        public string Note { get; set; }
        
        public bool IsHighlight { get; set; }
        public string FontWeight
        {
            get
            {
                if (IsHighlight) return "Bold";
                else return "Normal";
            }

        }

        public SolidColorBrush FontColor
        {
            get
            {
                if (IsHighlight) return Brushes.Black;
                else return new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90));
            }
        }

        public string ImageSource
        {
            get
            {
                //string imageUri = "pack://application:,,,/TaskList;component/Resource/Icons/";
                //string imageUri = "pack://application:,,,/component/Resource/Icons/";
                string path = "Resource/Icons/";
                switch (Status)
                {

                    case TaskStatus.WORKING:
                        path += "baseline_pause_black_36dp.png"; break;
                    case TaskStatus.PASUE:
                        path += "baseline_play_arrow_black_36dp.png"; break;
                    case TaskStatus.STOP:
                        path += "baseline_play_arrow_black_36dp.png"; break;
                    case TaskStatus.IDLE:
                    default:
                        path += "baseline_play_arrow_black_36dp.png"; break;
                }
                return path;
            }
        }
        
        public TaskItem()
        {
            TimeStart = DateTime.Now;
            TimeTotal = TimeSpan.Zero;
            TimeStr = "00.00";
            Note = "note";
            UpdateStatus(TaskStatus.IDLE);
        }

        public TaskItem(TaskStatus status, string note)
        {
            TimeStart = DateTime.Now;
            TimeTotal = TimeSpan.Zero;
            TimeStr = "00.00";
            Note = note;
            UpdateStatus(status);
        }

        public TaskItem(TaskStatus status, DateTime timeStart, string note)
        {
            TimeStart = timeStart;
            TimeTotal = TimeSpan.Zero;
            TimeStr = "00.00";
            Note = note;
            UpdateStatus(status);
        }

        public void UpdateStatus(TaskStatus status)
        {
            Status = status;
            IsHighlight = status == TaskStatus.WORKING;
            //

        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point mStartPoint = new Point();
        private ObservableCollection<TaskItem> mTaskItemList = new ObservableCollection<TaskItem>();
        private int mStartIndex = -1;

        private Storyboard mSBAniOut;
        private Storyboard mSBAniIn;

        private System.Windows.Forms.Timer mTimer = new System.Windows.Forms.Timer();
        private bool mIsFirstTime = true;

        System.Windows.Forms.NotifyIcon mNotifyIcon = null;
        public MainWindow()
        {

            
            InitializeComponent();

            this.ShowInTaskbar = TaskList.Properties.Settings.Default.ShowInTaskbar;
            string taskListBase64 = TaskList.Properties.Settings.Default.TaskListBase64;
            if (taskListBase64.Length != 0)
            {

                byte[] arr = Convert.FromBase64String(taskListBase64);
                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream ms = new MemoryStream(arr);
                ms.Position = 0;
                mTaskItemList = bf.Deserialize(ms) as ObservableCollection<TaskItem>;
            }
            else
                mTaskItemList = new ObservableCollection<TaskItem>();

            UpdateTitle();

            mListView.ItemsSource = mTaskItemList;

            this.SizeToContent = SizeToContent.WidthAndHeight;

            mSBAniOut = new Storyboard();
            DoubleAnimation daFadeOut = new DoubleAnimation();
            daFadeOut.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));// 200.Milliseconds();
            daFadeOut.To = 0.0;

            mSBAniOut.Children.Add(daFadeOut);
            Storyboard.SetTargetProperty(daFadeOut, new PropertyPath(UIElement.OpacityProperty));

            mSBAniIn = new Storyboard();
            DoubleAnimation daFadeIn = new DoubleAnimation();
            daFadeIn.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 200));// 200.Milliseconds();
            daFadeIn.From = 0.0;
            daFadeIn.To = 1.0;

            mSBAniIn.Children.Add(daFadeIn);
            Storyboard.SetTargetProperty(daFadeIn, new PropertyPath(UIElement.OpacityProperty));

            Timer_Tick(null, null);
            mTimer.Interval = 1000;
            mTimer.Tick += new EventHandler(Timer_Tick);
            mTimer.Start();


            try
            {
                Rect bounds = Rect.Parse(TaskList.Properties.Settings.Default.WindowRestoreBounds);
                bounds = CheckBounds(bounds);
                this.Top = bounds.Top;
                this.Left = bounds.Left;
                this.Width = bounds.Width;
                this.Height = bounds.Height;
            }
            catch (Exception)
            {
                //MessageBox.Show(e.ToString());
                //MessageBox.Show("[" + TomatoTimerWPF.TimerSettings.Default.WindowRestoreBounds + "]");
            }


            mNotifyIcon = new System.Windows.Forms.NotifyIcon();

            Stream iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/TaskList;component/Resource/Icons/baseline_work_outline_black_36dp_icon.ico")).Stream;
            mNotifyIcon.Icon = new System.Drawing.Icon(iconStream);

            mNotifyIcon.Visible = true;
            mNotifyIcon.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    this.ShowInTaskbar = !this.ShowInTaskbar;
                };

            //System.Windows.Forms.MouseEventHandler mouseHandler = delegate (object sender, System.Windows.Forms.MouseEventArgs e) { };

            mNotifyIcon.MouseClick += delegate (object sender, System.Windows.Forms.MouseEventArgs e) {
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    MessageBoxResult result = MessageBox.Show("Do you want to close the task window ?",
                      "Confirmation",
                      MessageBoxButton.YesNo,
                      MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        this.Close();
                    }
                }
                else
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                    //this.Show();
                    //if (this.WindowState == WindowState.Minimized)
                    //    this.WindowState = WindowState.Normal;
                    //else
                    //    this.WindowState = WindowState.Minimized;
                }
            };

            
        }

        static public Rect CheckBounds(Rect bounds)
        {
            int iInsideLT, iInsideRB;
            iInsideLT = iInsideRB = -1;
            for (int s = 0; s < System.Windows.Forms.Screen.AllScreens.Length; s++)
            {
                System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.AllScreens[s];
                if (bounds.Left >= screen.Bounds.Left && bounds.Top >= screen.Bounds.Top &&
                    bounds.Left < screen.Bounds.Right && bounds.Top < screen.Bounds.Bottom)
                {
                    iInsideLT = s;
                }

                if (bounds.Right >= screen.Bounds.Left && bounds.Bottom >= screen.Bounds.Top &&
                    bounds.Right < screen.Bounds.Right && bounds.Bottom < screen.Bounds.Bottom)
                {
                    iInsideRB = s;
                }
            }

            //if (iInsideLT != -1 || iInsideRB != -1)
            {
                int recheckScreen = -1;

                if (iInsideLT == -1 && iInsideRB == -1)
                    recheckScreen = 0;
                else if (iInsideLT == -1)
                    recheckScreen = iInsideRB;
                else if (iInsideRB == -1)
                    recheckScreen = iInsideLT;

                if (recheckScreen != -1)
                {
                    System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.AllScreens[recheckScreen];

                    if (bounds.X < screen.Bounds.Left)
                        bounds.X = screen.Bounds.Left;
                    if (bounds.Y < screen.Bounds.Top)
                        bounds.Y = screen.Bounds.Top;
                    if (bounds.Width > screen.Bounds.Width)
                        bounds.Width = screen.Bounds.Width;
                    if (bounds.Height > screen.Bounds.Height)
                        bounds.Height = screen.Bounds.Height;

                    if (bounds.Right > screen.Bounds.Right)
                        bounds.X -= bounds.Right - screen.Bounds.Right;
                    if (bounds.Bottom > screen.Bounds.Bottom)
                        bounds.Y -= bounds.Bottom - screen.Bounds.Bottom;

                }
            }
            return bounds;
        }

        void UpdateTitle()
        {
            int nWorking = 0;
            foreach (var item in mTaskItemList)
                if (item.Status == TaskStatus.WORKING)
                    nWorking++;
            mLabelTitle.Content = "Task (" + nWorking + "/" + mTaskItemList.Count + ")";
        }

        void Timer_Tick(object sender, EventArgs e)
        {
            //UpdateUI();
            foreach(var item in mTaskItemList)
            {
                
                TimeSpan timeDiff = item.TimeTotal;
                if (item.Status == TaskStatus.WORKING)
                {
                    timeDiff += DateTime.Now - item.TimeStart;
                }
                //timeDiff += new TimeSpan(10, 0, 0);
                int totalSeconds = (int)timeDiff.TotalSeconds;
                if (totalSeconds <= 59 * 60 + 59)
                    item.TimeStr = timeDiff.ToString(@"mm\:ss");
                else if (timeDiff.TotalMinutes > 59 * 60 + 59)
                    item.TimeStr = "59:59:59";
                else
                    item.TimeStr = timeDiff.ToString(@"h\:mm\:ss");
                //if (totalSeconds > 9*3600 + 59*60+59)
                //{
                //    //item.TimeStr = timeDiff.ToString(@"hh\:mm") + ".0";
                //    item.TimeStr = timeDiff.ToString(@"h\:mm\:ss");
                //}
                //else if (timeDiff.TotalMinutes > 59 * 60 + 59)
                //{
                //    item.TimeStr = "59:59";
                //}
                //else
                //{
                //    //item.TimeStr = ((totalSeconds&1) == 0? timeDiff.ToString(@"mm\ ss") : timeDiff.ToString(@"mm\.ss"));
                //    item.TimeStr = timeDiff.ToString(@"h\:mm\.ss");
                //}
                
            }

            System.ComponentModel.ICollectionView view = CollectionViewSource.GetDefaultView(mListView.ItemsSource);
            view.Refresh();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            btnAlwaysOnTop.IsChecked = TaskList.Properties.Settings.Default.AlwaysOnTop;
            if (btnAlwaysOnTop.IsChecked == true)
                this.ToggleAlwaysOnTop();

            mSBAniOut.Begin(gridControlPanel);
        }

        private bool SaveTaskList()
        {
            MemoryStream stream = new MemoryStream();
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(stream, mTaskItemList);

            byte[] arr = new byte[stream.Length];
            stream.Position = 0;
            stream.Read(arr, 0, (int)stream.Length);
            stream.Close();
            TaskList.Properties.Settings.Default.TaskListBase64 = Convert.ToBase64String(arr);

            TaskList.Properties.Settings.Default.ShowInTaskbar = this.ShowInTaskbar;
            TaskList.Properties.Settings.Default.AlwaysOnTop = btnAlwaysOnTop.IsChecked == true;
            TaskList.Properties.Settings.Default.WindowRestoreBounds = this.RestoreBounds.ToString();
            TaskList.Properties.Settings.Default.Save();
            return true;
        }

        private void lstView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Get current mouse position
            mStartPoint = e.GetPosition(null);
        }

        // Helper to search up the VisualTree
        private static T FindAnchestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }


        private void lstView_MouseMove(object sender, MouseEventArgs e)
        {
            // Get the current mouse position
            Point mousePos = e.GetPosition(null);
            Vector diff = mStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                       Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                // Get the dragged ListViewItem
                ListView listView = sender as ListView;
                ListViewItem listViewItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
                if (listViewItem == null) return;           // Abort
                                                            // Find the data behind the ListViewItem
                TaskItem item = (TaskItem)mListView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                if (item == null) return;                   // Abort
                                                            // Initialize the drag & drop operation
                mStartIndex = mListView.SelectedIndex;
                DataObject dragData = new DataObject("TaskItem", item);
                DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Copy | DragDropEffects.Move);
            }
        }

        private void lstView_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("TaskItem") || sender != e.Source)
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void lstView_Drop(object sender, DragEventArgs e)
        {
            int index = -1;

            if (e.Data.GetDataPresent("TaskItem") && sender == e.Source)
            {
                // Get the drop ListViewItem destination
                ListView listView = sender as ListView;
                ListViewItem listViewItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
                if (listViewItem == null)
                {
                    // Abort
                    e.Effects = DragDropEffects.None;
                    return;
                }
                // Find the data behind the ListViewItem
                TaskItem item = (TaskItem)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                // Move item into observable collection 
                // (this will be automatically reflected to lstView.ItemsSource)
                e.Effects = DragDropEffects.Move;
                index = mTaskItemList.IndexOf(item);
                if (mStartIndex >= 0 && index >= 0)
                {
                    mTaskItemList.Move(mStartIndex, index);
                }
                mStartIndex = -1;        // Done!
            }
        }


        private void Button_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Button btn = sender as Button;
            TaskItem item = btn != null ? btn.DataContext as TaskItem : null;
            if (item != null)
            {
                if (e.RightButton == MouseButtonState.Pressed)
                {
                    int idx = mTaskItemList.IndexOf(item);
                    if (idx>0)
                        mTaskItemList.Move(idx, 0);
                    Console.WriteLine("RightButton " + idx);
                    e.Handled = true;
                }
                else if (e.MiddleButton == MouseButtonState.Pressed)
                {
                    Console.WriteLine("MiddleButton");
                    MessageBoxResult result = MessageBox.Show("Do you want to reset the timer of task:\n\n" + item.Note + " ?",
                                          "Confirmation",
                                          MessageBoxButton.YesNo,
                                          MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        item.TimeStart = DateTime.Now;
                        item.TimeTotal = TimeSpan.Zero;
                        Timer_Tick(null, null);
                    }
                    e.Handled = true;
                }
            }
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine("TextBlock_MouseDown");
            TextBlock text = sender as TextBlock;
            TaskItem item = text != null ? text.DataContext as TaskItem : null;
            if (item != null)
            {
                if (e.RightButton == MouseButtonState.Pressed)
                {
                    Console.WriteLine("MiddleButton");
                    MessageBoxResult result = MessageBox.Show("Do you want to reset the timer of task:\n\n" + item.Note + " ?",
                                          "Confirmation",
                                          MessageBoxButton.YesNo,
                                          MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        item.TimeStart = DateTime.Now;
                        item.TimeTotal = TimeSpan.Zero;
                        Timer_Tick(null, null);
                    }
                    e.Handled = true;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            TaskItem item = btn != null ? btn.DataContext as TaskItem : null;
            if (item != null)
            {
                int idx = mTaskItemList.IndexOf(item);

                switch (item.Status)
                {
                    case TaskStatus.IDLE:
                        item.UpdateStatus(TaskStatus.WORKING);
                        item.TimeStart = DateTime.Now;
                        item.TimeTotal = TimeSpan.Zero;
                        break;
                    case TaskStatus.WORKING:
                        item.UpdateStatus(TaskStatus.PASUE);
                        item.TimeTotal += DateTime.Now - item.TimeStart;
                        break;
                    case TaskStatus.PASUE:
                        item.UpdateStatus(TaskStatus.WORKING);
                        item.TimeStart = DateTime.Now;
                        break;
                    case TaskStatus.STOP:
                        item.UpdateStatus(TaskStatus.IDLE);
                        item.TimeTotal = TimeSpan.Zero;
                        break;

                }
                Console.WriteLine(item.Status.ToString());

                System.ComponentModel.ICollectionView view = CollectionViewSource.GetDefaultView(mListView.ItemsSource);
                view.Refresh();

                UpdateTitle();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnAlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            this.ToggleAlwaysOnTop();
        }

        private void ToggleAlwaysOnTop()
        {
            this.Topmost = btnAlwaysOnTop.IsChecked.HasValue && btnAlwaysOnTop.IsChecked.Value;

            imgAlwaysOnTopUnlock.Visibility = btnAlwaysOnTop.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
            imgAlwaysOnTopLock.Visibility = btnAlwaysOnTop.IsChecked == false ? Visibility.Collapsed : Visibility.Visible;
        }

        private System.Windows.Point m_MousePosition;
        public DateTime m_MouseDownTime;
        public bool m_bIsMouseDown = false;

        private void OnButtonMove_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Control uiCur = (sender as Control);
            if (uiCur == null) return;
            m_MousePosition = e.GetPosition(uiCur);
            m_MouseDownTime = DateTime.Now;
            m_bIsMouseDown = true;
        }

        private void OnButtonMove_MouseMove(object sender, MouseEventArgs e)
        {
            MainWindow window = this;
            //MessageBox.Show("MM");
            //SystemParameters.MinimumHorizontalDragDistance
            Control uiCur = (sender as Control);
            if (uiCur == null) return;
            double minDragDis = 10;// Math.Min(Math.Min(uiCur.ActualWidth, uiCur.ActualHeight) * 0.25, 15);
            var currentPoint = e.GetPosition(uiCur);

            if (e.LeftButton == MouseButtonState.Pressed
                &&
                //uiCur.IsMouseCaptured &&
                (Math.Abs(currentPoint.X - m_MousePosition.X) > minDragDis ||
                Math.Abs(currentPoint.Y - m_MousePosition.Y) > minDragDis))
            {
                // Prevent Click from firing
                uiCur.ReleaseMouseCapture();
                m_bIsMouseDown = false;

                try
                {
                    window.DragMove();
                }
                catch (System.InvalidOperationException)
                {

                }
            }

        }

        private void btnMove_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            Control uiCur = (sender as Control);
            if (uiCur == null) return;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Prevent Click from firing
                uiCur.ReleaseMouseCapture();
                m_bIsMouseDown = false;

                try
                {
                    this.DragMove();
                }
                catch (System.InvalidOperationException)
                {

                }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private Size MeasureString(string candidate, TextBox textBox)
        {
            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                textBox.FontSize,
                Brushes.Black,
                new NumberSubstitution(),
                TextFormattingMode.Display);

            return new Size(formattedText.Width, formattedText.Height);
        }

        List<TextBox> mTextBoxList = new List<TextBox>();
        private void TextBoxList_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null) return;

            if (!mTextBoxList.Contains(textBox))
                mTextBoxList.Add(textBox);

            double maxWidth = 0;
            foreach (var text in mTextBoxList)
            {
                
                Size strSize = MeasureString(text.Text, text);
                if (maxWidth < strSize.Width)
                    maxWidth = strSize.Width;
            }

            Point relativePoint = textBox.TransformToAncestor(this).Transform(new Point(maxWidth, 0));

            if (maxWidth < 64)
                maxWidth = 64;
            if (mListGridView.Columns.Count > 1)
            {
                mListGridView.Columns[mListGridView.Columns.Count - 1].Width = Math.Max(100, maxWidth + 20);
            }
            //Console.WriteLine("TextBoxList_TextChanged");
            //Console.WriteLine(textBox.ActualWidth.ToString() + " max: " + maxWidth + " win " + relativePoint + "   " + mListGridView.Columns[mListGridView.Columns.Count - 1].Width);
            //this.Width = relativePoint.X + 32;
        }

        private void mListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ListView list = sender as ListView;
            if (list == null) return;
            Point relativePoint = list.TransformToAncestor(this).Transform(new Point(e.NewSize.Width, e.NewSize.Height));
            Console.WriteLine("mListView_SizeChanged");
            Console.WriteLine(this.Height + " -> " + (relativePoint.Y + 32));
            //this.Height = Math.Max(100, relativePoint.Y + 32);
            //this.MinWidth = relativePoint.X + 32;
            Console.WriteLine(list.ActualWidth.ToString() + " x " + list.ActualHeight.ToString() + " " + relativePoint);
        }

        private void btnTask_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;
            if (btn == btnAdd)
            {
                mTaskItemList.Insert(0, new TaskItem(TaskStatus.IDLE, "Task " + mTaskItemList.Count));
            }
            if (btn == btnRemove)
            {
                int selectedIdx = mListView.SelectedIndex;
                if (mListView.SelectedItem != null)
                    mTaskItemList.Remove((TaskItem)mListView.SelectedItem);
                if (selectedIdx >= 0 && selectedIdx < mListView.Items.Count)
                    mListView.SelectedItem = mListView.Items[selectedIdx];
                //Console.WriteLine("" + (mListView.SelectedItem != null) + " " + mListView.SelectedIndex);
                //if (mListView.SelectedItem != null && )
            }
        }


        private void Grid_MouseEnter(object sender, MouseEventArgs e)
        {//gridControlPanel
            mSBAniIn.Begin(gridControlPanel);
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            mSBAniOut.Begin(gridControlPanel);
            mListView.SelectedItem = null;
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Console.WriteLine("Grid_MouseDown");
        }

        private TextBox mFocusedTextBox = null;
        private void TextBoxList_GotFocus(object sender, RoutedEventArgs e)
        {
            mFocusedTextBox = sender as TextBox;
            mTimer.Stop();
        }

        private void TextBoxList_LostFocus(object sender, RoutedEventArgs e)
        {
            mFocusedTextBox = null;
            mTimer.Start();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (mFocusedTextBox!=null)
                mFocusedTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            foreach (var item in mTaskItemList)
                Console.WriteLine(item.Note);
            SaveTaskList();

            if (mNotifyIcon != null)
            {
                mNotifyIcon.Dispose();
                mNotifyIcon = null;
            }
        }

        private void TextBoxList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            TextBox text = sender as TextBox;
            if (text != null && e.Key == Key.Escape)
            {
                Console.WriteLine("press ESC");
                e.Handled = true;
                text.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                mListView.SelectedItem = null;
            }
        }

        private void TextBoxList_Loaded(object sender, RoutedEventArgs e)
        {
            TextBox text = sender as TextBox;
            if (mIsFirstTime)
            {
                if (text != null)
                {
                    if (mTextBoxList.Count < mTaskItemList.Count && !mTextBoxList.Contains(text))
                        mTextBoxList.Add(text);
                    if (mTextBoxList.Count >= mTaskItemList.Count)
                    {
                        mIsFirstTime = false;
                        TextBoxList_TextChanged(sender, null);
                    }
                }
            }

        }


    }
}
