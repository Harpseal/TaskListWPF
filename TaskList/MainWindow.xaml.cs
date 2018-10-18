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
using System.Windows.Threading;
using System.ComponentModel;
using System.Threading;
using System.Text.RegularExpressions;

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
    public class TaskItemBase
    {
        public TaskStatus Status { get; set; }
        public DateTime TimeStart { get; set; }
        public double TimeTotalSeconds { get; set; }
        public string TimeStr { get; set; }
        public string NoteValue { get; set; }

        public void UpdateStatus(TaskStatus status)
        {
            Status = status;
        }
    }
 
    public class TaskItem : INotifyPropertyChanged
    {
        public TaskItemBase mBase { get; }
        public string TimeStr {
            get
            {
                return mBase.TimeStr;
            }
            set
            {
                if (value != this.mBase.TimeStr)
                {
                    this.mBase.TimeStr = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public TaskStatus Status
        {
            get
            {  return mBase.Status;  }
            set
            {
                if (value != this.mBase.Status)
                {
                    this.mBase.Status = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public DateTime TimeStart
        {
            get
            { return mBase.TimeStart; }
            set
            {
                if (value != this.mBase.TimeStart)
                {
                    this.mBase.TimeStart = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public TimeSpan TimeTotal
        {
            get
            { return TimeSpan.FromSeconds(mBase.TimeTotalSeconds);  }
            set
            {
                if (value.TotalSeconds != this.mBase.TimeTotalSeconds)
                {
                    this.mBase.TimeTotalSeconds = value.TotalSeconds;
                    NotifyPropertyChanged();
                }
            }
        }

        public string Note
        {
            get
            {
                return mBase.NoteValue;
            }
            set
            {
                if (value != this.mBase.NoteValue)
                {
                    this.mBase.NoteValue = value;
                    //NotifyPropertyChanged();
                }
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }


        public bool IsHighlight
        {
            get
            {
                return mBase.Status == TaskStatus.WORKING;
            }

        }

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
                switch (mBase.Status)
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

        public TaskItem(TaskItemBase itemBase)
        {
            mBase = itemBase;
            mBase.UpdateStatus(itemBase.Status);
        }


        public TaskItem()
        {
            mBase = new TaskItemBase();
            mBase.TimeStart = DateTime.Now;
            mBase.TimeTotalSeconds = 0;
            mBase.TimeStr = "00.00";
            mBase.NoteValue = "note";
            mBase.UpdateStatus(TaskStatus.IDLE);
        }

        public TaskItem(TaskStatus status, string note)
        {
            mBase = new TaskItemBase();
            mBase.TimeStart = DateTime.Now;
            mBase.TimeTotalSeconds = 0;
            mBase.TimeStr = "00.00";
            mBase.NoteValue = note;
            mBase.UpdateStatus(status);
        }

        public TaskItem(TaskStatus status, DateTime timeStart, string note)
        {
            mBase = new TaskItemBase();
            mBase.TimeStart = timeStart;
            mBase.TimeTotalSeconds = 0;
            mBase.TimeStr = "00.00";
            mBase.NoteValue = note;
            mBase.UpdateStatus(status);
        }

        public void UpdateStatus(TaskStatus status)
        {
            if (mBase == null) return;
            mBase.UpdateStatus(status);
        }

    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point mStartPoint = new Point();
        private ObservableCollection<TaskItem> mTaskItemAllCollection = null;
        private ObservableCollection<TaskItem> mTaskItemWorkingCollection = null;
        private List<TaskItem> mTaskItemUndoList = null;
        private int mStartIndex = -1;

        private Storyboard mSBAniOut;
        private Storyboard mSBAniIn;

        private System.Windows.Forms.Timer mTimer = new System.Windows.Forms.Timer();

        public const int MinNoteWidth = 120;

        private const int mAutoSaveCountdownTotal =  5 * 60 * 1000; //5 mins
        private int mAutoSaveCountdown = 0;
        private int mResetFocusCountdown = 0;

        System.Windows.Forms.NotifyIcon mNotifyIcon = null;
        public MainWindow()
        {
            InitializeComponent();

            mTaskItemAllCollection = new ObservableCollection<TaskItem>();
            mTaskItemWorkingCollection = new ObservableCollection<TaskItem>();

            this.ShowInTaskbar = TaskList.Properties.Settings.Default.ShowInTaskbar;
            string taskListBase64 = TaskList.Properties.Settings.Default.TaskListBase64;
            if (taskListBase64.Length != 0)
            {

                byte[] arr = Convert.FromBase64String(taskListBase64);
                BinaryFormatter bf = new BinaryFormatter();
                MemoryStream ms = new MemoryStream(arr);
                ms.Position = 0;

                List<TaskItemBase> baseList = new List<TaskItemBase>();
                
                try
                {
                    baseList = bf.Deserialize(ms) as List<TaskItemBase>;
                    if (baseList != null)
                    {
                        foreach (var itemBase in baseList)
                            mTaskItemAllCollection.Add(new TaskItem(itemBase));
                    }
                }
                catch (System.Runtime.Serialization.SerializationException se)
                {
                    Console.WriteLine(se.ToString());
                }
                

            }
            mTaskItemUndoList = new List<TaskItem>();
            mListView.ItemsSource = mTaskItemAllCollection;

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
            //daFadeIn.From = 0.0;
            daFadeIn.To = 1.0;

            mSBAniIn.Children.Add(daFadeIn);
            Storyboard.SetTargetProperty(daFadeIn, new PropertyPath(UIElement.OpacityProperty));

            Timer_Tick(null, null);
            mTimer.Interval = 500;
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
                    if (btnAlwaysOnTop.IsChecked == true)
                        this.Topmost = true;

                }
            };

            UpdateTitle();

            mNoteTypeFace = new Typeface(new FontFamily("Microsoft JhengHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            mNoteFontSize = 13;
        }

        //ref: https://www.codeproject.com/Articles/487571/XML-Serialization-and-Deserialization-Part-2
        private void SerializeList(List<TaskItemBase> list, string TargetPath)
        {
            System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(List<TaskItemBase>));
            using (TextWriter writer = new StreamWriter(TargetPath))
            {
                serializer.Serialize(writer, list);
            }
        }

        private List<TaskItemBase> DeserializeList(string TargetPath)
        {
            System.Xml.Serialization.XmlSerializer deserializer = new System.Xml.Serialization.XmlSerializer(typeof(List<TaskItemBase>));
            TextReader reader = new StreamReader(TargetPath);
            List<TaskItemBase> list = null;
            try
            {
                object obj = deserializer.Deserialize(reader);
                list = (List<TaskItemBase>)obj;
            }
            catch (System.InvalidOperationException e)
            {
                Console.WriteLine(e.ToString());
                list = null;
            }

            reader.Close();
            return list;
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

        int UpdateTitle()
        {
            int nWorking = 0;
            foreach (var item in mTaskItemAllCollection)
                if (item.Status == TaskStatus.WORKING)
                    nWorking++;
            mLabelTitle.Content = "Task (" + nWorking + "/" + mTaskItemAllCollection.Count + ")";

            if (nWorking == 0 && mAutoSaveCountdown == 0 && mResetFocusCountdown == 0)
                mTimer.Stop();
            else if (!mTimer.Enabled)
                mTimer.Start();

            btnRemove.IsEnabled = mListView.SelectedItem != null;
            if (btnRemove.IsEnabled)
                btnRemove.Opacity = 1.0;
            else
                btnRemove.Opacity = 0.2;
            btnUndo.Visibility = mTaskItemUndoList.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            return nWorking;
        }

        private const double MinTimeWidth = 40;
        private const double MidTimeWidth = 45;
        private const double MaxTimeWidth = 55;
        void UpdateListViewTimer()
        {
            if (mListView.Items == null || mListView.Items.Count == 0)
                return;
            bool isMidTimeWidth = false;
            bool isMaxTimeWidth = false;
            //foreach (var item in mTaskItemAllCollection)
            foreach (TaskItem item in mListView.Items)
            {

                TimeSpan timeDiff = item.TimeTotal;
                if (item.Status == TaskStatus.WORKING)
                {
                    timeDiff += DateTime.Now - item.mBase.TimeStart;
                }
                //timeDiff += new TimeSpan(9,4, 0);
                //int totalSeconds = (int)timeDiff.TotalSeconds;
                if (timeDiff.TotalMinutes < 60)
                    item.TimeStr = timeDiff.ToString(@"mm\:ss");
                else if (timeDiff.TotalHours >= 24)
                { 
                    item.TimeStr = "23:59:59";
                    isMaxTimeWidth = true;
                }
                else
                {
                    isMaxTimeWidth |= timeDiff.TotalHours >= 10;
                    isMidTimeWidth |= !isMaxTimeWidth;
                    item.TimeStr = timeDiff.ToString(@"h\:mm\:ss");
                }
                //Console.WriteLine("item" + item.TimeStr);
            }
            //mListView.Items.Refresh();
            //System.ComponentModel.ICollectionView view = CollectionViewSource.GetDefaultView(mListView.ItemsSource);
            //view.Refresh();
            if (mListGridView.Columns.Count >= 2)
            {
                double oldWidth = mListGridView.Columns[1].Width;
                if (isMaxTimeWidth)
                {
                    if (oldWidth != MaxTimeWidth)
                        mListGridView.Columns[1].Width = MaxTimeWidth;
                }
                else if (isMidTimeWidth)
                {
                    if (oldWidth != MidTimeWidth)
                        mListGridView.Columns[1].Width = MidTimeWidth;
                }
                else
                {
                    if (oldWidth != MinTimeWidth)
                        mListGridView.Columns[1].Width = MinTimeWidth;
                }
            }
        }

        void Timer_Tick(object sender, EventArgs e)
        {
            UpdateListViewTimer();

            if (mResetFocusCountdown > 0)
            {
                mResetFocusCountdown -= mTimer.Interval;
                Console.WriteLine("mResetFocusCountdown " + mResetFocusCountdown);
                if (mResetFocusCountdown<=0)
                {
                    if (mFocusedTextBox != null)
                    {
                        mFocusedTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                        Keyboard.ClearFocus();
                        mFocusedTextBox = null;
                    }

                    if (mFocusedRichTextBox != null)
                    {
                        mFocusedRichTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                        Keyboard.ClearFocus();
                        mFocusedRichTextBox = null;
                    }
                    mListView.SelectedItem = null;
                }
            }

            if (!mMenuAutoSave.IsChecked)
            {
                mAutoSaveCountdown = 0;
                mProgressBarAutoSave.Visibility = Visibility.Collapsed;
            }
            else if (mAutoSaveCountdown > 0)
            {
                if (mMenuAutoSaveShowProgress.IsChecked)
                {
                    if (mProgressBarAutoSave.Visibility != Visibility.Visible)
                        mProgressBarAutoSave.Visibility = Visibility.Visible;
                    mProgressBarAutoSave.Value = (1.0 - (float)mAutoSaveCountdown / mAutoSaveCountdownTotal) * mProgressBarAutoSave.Maximum;
                }
                else
                {
                    if (mProgressBarAutoSave.Visibility != Visibility.Collapsed)
                        mProgressBarAutoSave.Visibility = Visibility.Collapsed;
                }
                mAutoSaveCountdown -= mTimer.Interval;
                Console.WriteLine("mAutoSaveCountdown " + mAutoSaveCountdown + "  " + mMenuAutoSave.IsChecked + " " + mProgressBarAutoSave.Value);
                SaveTaskList();
                if (mAutoSaveCountdown <= 0 && mMenuAutoSave.IsChecked == true)
                {
                    mProgressBarAutoSave.Visibility = Visibility.Collapsed;
                    string savePath = TaskList.Properties.Settings.Default.AutoSavePath;
                    if (Directory.Exists(savePath))
                    {
                        if (!savePath.EndsWith("/") && !savePath.EndsWith("\\") && !savePath.EndsWith(""+System.IO.Path.DirectorySeparatorChar))
                            savePath += System.IO.Path.DirectorySeparatorChar;// System.IO.Path.PathSeparator;
                        savePath += "task_" + DateTime.Now.ToString("yyMMdd_HHmmss") + "_" + mTaskItemAllCollection.Count.ToString("00") + "_autosave.xml";

                        List<TaskItemBase> listBase = new List<TaskItemBase>();
                        foreach (var item in mTaskItemAllCollection)
                        {
                            listBase.Add(item.mBase);
                        }
                        SerializeList(listBase, savePath);
                        Console.WriteLine(savePath);
                    }
                    else
                    {
                        Console.WriteLine("error: Folder does not exist!! " + TaskList.Properties.Settings.Default.AutoSavePath);
                        mMenuAutoSave.IsChecked = false;
                    }
                }
            }
         }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            btnAlwaysOnTop.IsChecked = TaskList.Properties.Settings.Default.AlwaysOnTop;

            mMenuAutoSaveSkipStatusChanges.IsChecked = TaskList.Properties.Settings.Default.EnableAutoSaveSkipStatusChanges;
            mMenuAutoSaveShowProgress.IsChecked = TaskList.Properties.Settings.Default.EnableAutoSaveProgressBar;

            mTextBoxRegexInput.Text = TaskList.Properties.Settings.Default.RegexBold;

            ToggleAlwaysOnTop();

            if (TaskList.Properties.Settings.Default.AutoSavePath.Length != 0 && Directory.Exists(TaskList.Properties.Settings.Default.AutoSavePath))
            {
                mMenuAutoSave.ToolTip = "Path: " + TaskList.Properties.Settings.Default.AutoSavePath;
                mMenuAutoSavePathSelection.ToolTip = "Path: " + TaskList.Properties.Settings.Default.AutoSavePath;
            }
            else
            {
                TaskList.Properties.Settings.Default.EnableAutoSave = false;
                TaskList.Properties.Settings.Default.AutoSavePath = "";
            }
            mMenuAutoSave.IsChecked = TaskList.Properties.Settings.Default.EnableAutoSave;

            mProgressBarAutoSave.Visibility = (mMenuAutoSave.IsChecked == true && mMenuAutoSaveShowProgress.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            mSBAniOut.Begin(gridControlPanel);
        }



        private bool SaveTaskList()
        {
            List<TaskItemBase> listBase = new List<TaskItemBase>();
            foreach (var item in mTaskItemAllCollection)
            {
                listBase.Add(item.mBase);
            }
            MemoryStream stream = new MemoryStream();
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(stream, listBase);

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
                try
                {
                    current = VisualTreeHelper.GetParent(current);
                }catch (System.InvalidOperationException ioe)
                {
                    Console.WriteLine(ioe.ToString());
                    current = null;
                }
            }
            while (current != null);
            return null;
        }


        private void lstView_MouseMove(object sender, MouseEventArgs e)
        {
            //Console.WriteLine("lstView_MouseMove " + btnMove.ContextMenu.IsOpen + " " + mMainContextMenu.IsVisible + " " + mMainContextMenu.Visibility);
            if (mMainContextMenu.IsVisible == true) return;
            if (mFocusedRichTextBox != null || mFocusedTextBox != null)
                return;
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
                try
                {
                    DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Copy | DragDropEffects.Move);
                }
                catch (System.InvalidOperationException ioe)
                {
                    Console.WriteLine(ioe);
                }
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
                index = mTaskItemAllCollection.IndexOf(item);
                if (mStartIndex >= 0 && index >= 0)
                {
                    mTaskItemAllCollection.Move(mStartIndex, index);
                }
                mStartIndex = -1;        // Done!
            }
        }


        private void TimerTextBlock_MouseUp(object sender, MouseButtonEventArgs e)
        {
            TextBlock text = sender as TextBlock;
            TaskItem item = text != null ? text.DataContext as TaskItem : null;
            if (item != null)
            {
                mDateTimePreTextChange = DateTime.Now;
                if (e.LeftButton == MouseButtonState.Released)
                {
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
                //else if (e.MiddleButton == MouseButtonState.Pressed)
                //{
                //    //Hide the clicked task.
                //    if (btnFold.IsChecked == true)
                //    {
                //        if (mTaskItemWorkingCollection.Contains(item))
                //            mTaskItemWorkingCollection.Remove(item);
                //    }
                //    else
                //    {
                //        mTaskItemWorkingCollection.Clear();
                //        foreach (var i in mTaskItemAllCollection)
                //            if (i != item)
                //                mTaskItemWorkingCollection.Add(i);
                //        mListView.ItemsSource = mTaskItemWorkingCollection;
                //        btnFold.IsChecked = true;

                //        imgUnfold.Visibility = btnFold.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
                //        imgFold.Visibility = btnFold.IsChecked == false ? Visibility.Collapsed : Visibility.Visible;
                //    }
                //    e.Handled = true;
                //    UpdateListViewTaskNote();
                //}
                UpdateListViewTimer();
            }
        }

        private void TaskItemMouseDown(TaskItem item, MouseButtonEventArgs e)
        {
            if (item == null) return;
            if (e.RightButton == MouseButtonState.Pressed)
            {
                //Move the clicked task to the first position.
                if (btnFold.IsChecked == true)
                {
                    int idx = mTaskItemWorkingCollection.IndexOf(item);
                    if (idx > 0)
                        mTaskItemWorkingCollection.Move(idx, 0);
                }
                else
                {
                    int idx = mTaskItemAllCollection.IndexOf(item);
                    if (idx > 0)
                        mTaskItemAllCollection.Move(idx, 0);
                }
                e.Handled = true;
            }
            else if (e.MiddleButton == MouseButtonState.Pressed)
            {
                //Hide the clicked task.
                if (btnFold.IsChecked == true)
                {
                    if (mTaskItemWorkingCollection.Contains(item))
                        mTaskItemWorkingCollection.Remove(item);
                }
                else
                {
                    mTaskItemWorkingCollection.Clear();
                    foreach (var i in mTaskItemAllCollection)
                        if (i != item)
                            mTaskItemWorkingCollection.Add(i);
                    mListView.ItemsSource = mTaskItemWorkingCollection;
                    btnFold.IsChecked = true;

                    imgUnfold.Visibility = btnFold.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
                    imgFold.Visibility = btnFold.IsChecked == false ? Visibility.Collapsed : Visibility.Visible;
                }
                e.Handled = true;
                mDateTimePreTextChange = DateTime.Now;
                UpdateListViewTimer();
                UpdateListViewTaskNote();
            }
        }

        private void ListViewItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ListViewItem litem = sender as ListViewItem;
            TaskItem item = litem != null ? litem.DataContext as TaskItem : null;

            if (item != null)
                TaskItemMouseDown(item, e);
        }

        private void StatusButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Button btn = sender as Button;
            TaskItem item = btn != null ? btn.DataContext as TaskItem : null;
            if (item != null)
                TaskItemMouseDown(item, e);
        }


        private void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            TaskItem item = btn != null ? btn.DataContext as TaskItem : null;
            if (item != null)
            {
                int idx = mTaskItemAllCollection.IndexOf(item);

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
                //Console.WriteLine(item.Status.ToString());
                Console.WriteLine("mAutoSaveCountdown Button_Click");
                if (mMenuAutoSaveSkipStatusChanges.IsChecked == false)
                    mAutoSaveCountdown = mAutoSaveCountdownTotal;

                //System.ComponentModel.ICollectionView view = CollectionViewSource.GetDefaultView(mListView.ItemsSource);
                //view.Refresh();

                UpdateTitle();
                UpdateListViewTimer();
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
            btnAlwaysOnTop.ToolTip = "Always On Top (" + (btnAlwaysOnTop.IsChecked==true ? "enabled" : "disabled") + ")";
        }

        private void btnFold_Click(object sender, RoutedEventArgs e)
        {
            ToggleFolding();
            UpdateListViewTimer();
        }

        private void ToggleFolding()
        {
            bool isFolding = btnFold.IsChecked.HasValue && btnFold.IsChecked.Value;

            imgUnfold.Visibility = isFolding == true ? Visibility.Collapsed : Visibility.Visible;
            imgFold.Visibility = isFolding == false ? Visibility.Collapsed : Visibility.Visible;

            if (isFolding)
            {
                mTaskItemWorkingCollection.Clear();
                foreach (var item in mTaskItemAllCollection)
                    if (item.Status == TaskStatus.WORKING)
                        mTaskItemWorkingCollection.Add(item);
                mListView.ItemsSource = mTaskItemWorkingCollection;
            }
            else
            {
                mListView.ItemsSource = mTaskItemAllCollection;
            }
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
            bool isTpying = (DateTime.Now - mDateTimePreTextChange).TotalSeconds < 1;
            Console.WriteLine("" + e.PreviousSize.Width + " -> " + e.NewSize.Width + " " + this.IsLoaded + " " + this.IsActive + " isTpying " + isTpying);
            if (this.IsActive && !isTpying)
                this.Left += e.PreviousSize.Width - e.NewSize.Width;
        }


        private Size MeasureString(string candidate)
        {
            if (mNoteTypeFace == null)
                return new Size(MinNoteWidth, 0);

            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                mNoteTypeFace,
                mNoteFontSize,
                Brushes.Black,
                new NumberSubstitution(),
                TextFormattingMode.Ideal);

            return new Size(formattedText.Width, formattedText.Height);
        }

        private void UpdateListViewTaskNote()
        {
            double maxWidth = MinNoteWidth;

            //foreach (var task in mTaskItemAllCollection)
            foreach (TaskItem task in mListView.Items)
            {
                Size strSize = MeasureString(task.Note);
                if (maxWidth < strSize.Width)
                    maxWidth = strSize.Width;
            }

            //Point relativePoint = textBox.TransformToAncestor(this).Transform(new Point(maxWidth, 0));

            double newWidth = Math.Max(MinNoteWidth, maxWidth + 20);
            if (mListGridView.Columns.Count > 0)
            {
                mListGridView.Columns[mListGridView.Columns.Count - 1].Width = newWidth;
            }
            //Console.WriteLine("UpdateListView max: " + maxWidth + "  new: " + newWidth  + "[" + mEditingTextBox.Text);
        }

        private DateTime mDateTimePreTextChange = DateTime.Now;
        private void TextBoxList_TextChanged(object sender, TextChangedEventArgs e)
        {
            RichTextBox rtBox = sender as RichTextBox;
            if (rtBox != null)
            {
                TaskItem item = rtBox != null ? rtBox.DataContext as TaskItem : null;

                string richText = new TextRange(rtBox.Document.ContentStart, rtBox.Document.ContentEnd).Text.TrimEnd(Environment.NewLine.ToCharArray());
                item.Note = richText;
            }

            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {

                TaskItem item = textBox != null ? textBox.DataContext as TaskItem : null;

                if (item != null)
                    item.Note = textBox.Text;


            }
            mDateTimePreTextChange = DateTime.Now;

            mResetFocusCountdown = 5000;
            if (mAutoSaveCountdown != 0)
                mAutoSaveCountdown = mAutoSaveCountdownTotal;
            UpdateListViewTaskNote();

        }

        private void lstView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnRemove.IsEnabled = mListView.SelectedItem != null;
            if (btnRemove.IsEnabled)
                btnRemove.Opacity = 1.0;
            else
                btnRemove.Opacity = 0.2;
        }

        private void btnTask_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;
            if (btn == btnAdd)
            {
                TaskItem itemNew = new TaskItem(TaskStatus.IDLE, "Task " + mTaskItemAllCollection.Count);
                mTaskItemAllCollection.Insert(0, itemNew);
                if (mTaskItemWorkingCollection != null)
                    mTaskItemWorkingCollection.Insert(0, itemNew);
            }
            if (btn == btnRemove)
            {
                int selectedIdx = mListView.SelectedIndex;
                if (mListView.SelectedItem != null)
                {
                    TaskItem item = (TaskItem)mListView.SelectedItem;
                    mTaskItemUndoList.Add(item);
                    mTaskItemAllCollection.Remove(item);
                    if (mTaskItemWorkingCollection != null && mTaskItemWorkingCollection.Contains(item))
                        mTaskItemWorkingCollection.Remove(item);
                }
                if (selectedIdx >= mListView.Items.Count) selectedIdx = mListView.Items.Count - 1;
                if (selectedIdx >= 0)
                    mListView.SelectedItem = mListView.Items[selectedIdx];
                //Console.WriteLine("" + (mListView.SelectedItem != null) + " " + mListView.SelectedIndex);
                //if (mListView.SelectedItem != null && )
            }
            if (btn == btnUndo)
            {
                if (mTaskItemUndoList.Count > 0)
                {
                    TaskItem item = mTaskItemUndoList[mTaskItemUndoList.Count - 1];
                    mTaskItemAllCollection.Add(item);
                    if (mTaskItemWorkingCollection != null)
                        mTaskItemWorkingCollection.Add(item);
                    mTaskItemUndoList.Remove(item);
                }
            }

            mAutoSaveCountdown = mAutoSaveCountdownTotal;

            UpdateListViewTaskNote();
            UpdateTitle();
            UpdateListViewTimer();
            
        }


        private void Grid_MouseEnter(object sender, MouseEventArgs e)
        {
            mResetFocusCountdown = 0;
            UpdateTitle();
            mSBAniIn.Begin(gridControlPanel);
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            //Point mousePos = new Point(System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y);
            //bool isMouseStillInside = mousePos.X >= this.Left && mousePos.X <= this.Left + this.Width && mousePos.Y >= this.Top && mousePos.X <= this.Top + this.Width;
            //Console.WriteLine("Control.MousePosition " + mousePos + " " + isMouseStillInside);

            mSBAniOut.Begin(gridControlPanel);

            mResetFocusCountdown = 5000;
            if (!mTimer.Enabled)
                mTimer.Start();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //Console.WriteLine("Grid_MouseDown");
        }


        private void UpdateRichTextBox(RichTextBox rtbox, string text, bool isBold)
        {
            if (rtbox == null) return;
            text = text.TrimEnd(Environment.NewLine.ToCharArray());

            bool isUpdated = false;
            if (isBold)
            {
                MatchCollection matches = Regex.Matches(text, mTextBoxRegexInput.Text);
                if (matches.Count > 0)// && mNoteTypeFace != null
                {
                    isUpdated = true;
                    Paragraph para = new Paragraph();
                    int pos = 0;

                    foreach (Match m in matches)
                    {
                        if (pos < m.Index)
                            para.Inlines.Add(new Run(text.Substring(pos, m.Index - pos)));
                        para.Inlines.Add(new Bold(new Run(text.Substring(m.Index, m.Length))));
                        pos = m.Index + m.Length;
                    }

                    if (pos + 1 < text.Length)
                        para.Inlines.Add(new Run(text.Substring(pos, text.Length - 1 - pos)));
                    rtbox.Document.Blocks.Clear();
                    rtbox.Document.Blocks.Add(para);
                }
            }

            if (!isUpdated)
            {
                rtbox.Document.Blocks.Clear();
                rtbox.Document.Blocks.Add(new Paragraph(new Run(text)));
            }
        }

        private string mFocusedText = "";
        private TextBox mFocusedTextBox = null;
        private RichTextBox mFocusedRichTextBox = null;
        private void TextBoxList_GotFocus(object sender, RoutedEventArgs e)
        {
            mAutoSaveCountdown = mAutoSaveCountdownTotal;

            mFocusedText = "";

            mFocusedTextBox = sender as TextBox;
            if (mFocusedTextBox != null)
                mFocusedText = mFocusedTextBox.Text;
            mFocusedRichTextBox = sender as RichTextBox;
            TaskItem item = mFocusedRichTextBox != null ? mFocusedRichTextBox.DataContext as TaskItem : null;
            if (mFocusedRichTextBox != null && item != null)
            {
                //mFocusedRichTextBox.Document.Blocks.Clear();
                //mFocusedRichTextBox.Document.Blocks.Add(new Paragraph(new Run(item.Note)));
                UpdateRichTextBox(mFocusedRichTextBox, item.Note, false);
                mFocusedText = item.Note;
            }

            mTimer.Stop();
        }

        private void TextBoxList_LostFocus(object sender, RoutedEventArgs e)
        {
            if (mFocusedTextBox != null)
            {
                if (mFocusedText == mFocusedTextBox.Text)
                    mAutoSaveCountdown = 0; //no changes, skip autosave....
                else
                    mAutoSaveCountdown = mAutoSaveCountdownTotal;
                //Console.WriteLine(mFocusedText + " -> " + mFocusedTextBox.Text + "  " + mAutoSaveCountdown);
            }
            if (mFocusedRichTextBox != null)
            {
                TaskItem item = mFocusedRichTextBox != null ? mFocusedRichTextBox.DataContext as TaskItem : null;
                if (item != null)
                {
                    string richText = new TextRange(mFocusedRichTextBox.Document.ContentStart, mFocusedRichTextBox.Document.ContentEnd).Text.TrimEnd(Environment.NewLine.ToCharArray());
                    item.Note = richText;
                    UpdateRichTextBox(mFocusedRichTextBox, item.Note, true);

                    //MatchCollection matches = Regex.Matches(richText, "\\[[^\\n\\r]*?\\]");
                    //if (matches.Count > 0 && mNoteTypeFace != null)
                    //{
                    //    Paragraph para = new Paragraph();
                    //    int pos = 0;

                    //    foreach (Match m in matches)
                    //    {
                    //        if (pos < m.Index)
                    //            para.Inlines.Add(new Run(richText.Substring(pos, m.Index - pos)));
                    //        para.Inlines.Add(new Bold(new Run(richText.Substring(m.Index, m.Length))));
                    //        pos = m.Index + m.Length;
                    //    }

                    //    if (pos + 1 < richText.Length)
                    //        para.Inlines.Add(new Run(richText.Substring(pos, richText.Length-1 - pos)));
                    //    mFocusedRichTextBox.Document.Blocks.Clear();
                    //    mFocusedRichTextBox.Document.Blocks.Add(para);
                    //}
                }
                
            }
            mFocusedTextBox = null;
            mFocusedRichTextBox = null;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (mFocusedTextBox!=null)
                mFocusedTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            if (mFocusedRichTextBox != null)
                mFocusedRichTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            //foreach (var item in mTaskItemAllCollection)
            //    Console.WriteLine(item.Note);
            SaveTaskList();

            if (mNotifyIcon != null)
            {
                mNotifyIcon.Dispose();
                mNotifyIcon = null;
            }
        }

        private void TextBoxList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Console.WriteLine("TextBoxList_PreviewKeyDown");
            if (e.Key == Key.Escape)
            {
                Console.WriteLine("press ESC");
                e.Handled = true;
                if (mFocusedTextBox != null)
                    mFocusedTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                if (mFocusedRichTextBox != null)
                    mFocusedRichTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                Keyboard.ClearFocus();
                mListView.SelectedItem = null;
            }
        }

        private Typeface mNoteTypeFace = null;
        private double mNoteFontSize = 12;
        private void TextBoxList_Loaded(object sender, RoutedEventArgs e)
        {
            TextBox text = sender as TextBox;

            if (text != null)
            {
                //Copy the settings of note text box.
                if (mNoteTypeFace == null)
                {
                    mNoteTypeFace = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);
                    mNoteFontSize = text.FontSize;
                }
                else
                {
                    text.FontFamily = mNoteTypeFace.FontFamily;
                    text.FontStyle = mNoteTypeFace.Style;
                    text.FontWeight = mNoteTypeFace.Weight;
                    text.FontStretch = mNoteTypeFace.Stretch;
                    text.FontSize = mNoteFontSize;
                }
            }

            RichTextBox rtBox = sender as RichTextBox;
            TaskItem item = rtBox != null ? rtBox.DataContext as TaskItem : null;
            if (rtBox != null && item != null)
            {
                UpdateRichTextBox(rtBox, item.Note, true);
                //rtBox.Document.Blocks.Clear();
                //rtBox.Document.Blocks.Add(new Paragraph(new Run(item.Note)));
            }
            UpdateListViewTaskNote();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender == mMenuExit)
                this.Close();
            else if (sender == mMenuLoad)
            {
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
                openFileDialog.Filter = "Xml file (*.xml)|*.xml|All file (*.*)|*.*";
                if (openFileDialog.ShowDialog() == true)
                {
                    List<TaskItemBase> listBase = DeserializeList(openFileDialog.FileName);
                    if (listBase != null)
                    {
                        mTaskItemAllCollection.Clear();
                        foreach (var item in listBase)
                        {
                            mTaskItemAllCollection.Add(new TaskItem(item));
                        }
                        UpdateTitle();
                        btnFold.IsChecked = false;
                        ToggleFolding();
                        UpdateListViewTaskNote();
                    }
                    else
                    {
                        MessageBox.Show("Can\'t open xml file named : " + openFileDialog.FileName, "Error",
                             MessageBoxButton.OK,
                             MessageBoxImage.Error);
                    }

                }
            }
            else if (sender == mMenuSave)
            {
                
                Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
                saveFileDialog.Filter = "Xml file (*.xml)|*.xml|All file (*.*)|*.*";
                saveFileDialog.FileName = "task_" + DateTime.Now.ToString("yyMMdd_HHmmss") + "_" + mTaskItemAllCollection.Count.ToString("00") + ".xml";
                if (saveFileDialog.ShowDialog() == true)
                {
                    List<TaskItemBase> listBase = new List<TaskItemBase>();
                    foreach (var item in mTaskItemAllCollection)
                    {
                        listBase.Add(item.mBase);
                    }
                    SerializeList(listBase, saveFileDialog.FileName);

                }
            }
            else if (sender == mMenuAutoSave)
            {
                Console.WriteLine("mMenuAutoSave " + mMenuAutoSave.IsChecked);

                if (!mMenuAutoSave.IsChecked)
                {
                    mAutoSaveCountdown = 0;
                    mProgressBarAutoSave.Visibility = Visibility.Collapsed;
                }
                else if (mMenuAutoSave.IsChecked && 
                    (TaskList.Properties.Settings.Default.AutoSavePath.Length == 0 || !Directory.Exists(TaskList.Properties.Settings.Default.AutoSavePath)))
                {
                    using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                        Console.WriteLine("mMenuAutoSave result " + result + "　" + dialog.SelectedPath);
                        if (result == System.Windows.Forms.DialogResult.OK)
                        {
                            string savePath = dialog.SelectedPath;
                            if (!savePath.EndsWith("/") && !savePath.EndsWith("\\") && !savePath.EndsWith("" + System.IO.Path.DirectorySeparatorChar))
                                savePath += System.IO.Path.DirectorySeparatorChar;// System.IO.Path.PathSeparator;

                            TaskList.Properties.Settings.Default.AutoSavePath = savePath;

                            mMenuAutoSave.ToolTip = "Path: " + TaskList.Properties.Settings.Default.AutoSavePath;
                            mMenuAutoSavePathSelection.ToolTip = "Path: " + TaskList.Properties.Settings.Default.AutoSavePath;
                        }
                        else
                            mMenuAutoSave.IsChecked = false;
                    }
                }
                TaskList.Properties.Settings.Default.EnableAutoSave = mMenuAutoSave.IsChecked == true;
                TaskList.Properties.Settings.Default.Save();

            }
            else if (sender == mMenuAutoSavePathSelection)
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.SelectedPath = TaskList.Properties.Settings.Default.AutoSavePath;
                    System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                    Console.WriteLine("mMenuAutoSave result " + result + "　" + dialog.SelectedPath);
                    if (result == System.Windows.Forms.DialogResult.OK)
                    {
                        string savePath = dialog.SelectedPath;
                        if (!savePath.EndsWith("/") && !savePath.EndsWith("\\") && !savePath.EndsWith("" + System.IO.Path.DirectorySeparatorChar))
                            savePath += System.IO.Path.DirectorySeparatorChar;// System.IO.Path.PathSeparator;
                        
                        Console.WriteLine("mMenuAutoSave path exist? " + Directory.Exists(savePath));

                        TaskList.Properties.Settings.Default.AutoSavePath = savePath;
                        TaskList.Properties.Settings.Default.Save();
                        mMenuAutoSave.ToolTip = "Path: " + TaskList.Properties.Settings.Default.AutoSavePath;
                        mMenuAutoSavePathSelection.ToolTip = "Path: " + TaskList.Properties.Settings.Default.AutoSavePath;
                    }
                }
            }
            else if (sender == mMenuAutoSaveShowProgress)
            {
                TaskList.Properties.Settings.Default.EnableAutoSaveProgressBar = mMenuAutoSaveShowProgress.IsChecked;
                TaskList.Properties.Settings.Default.Save();
            }
            else if (sender == mMenuAutoSaveSkipStatusChanges)
            {
                TaskList.Properties.Settings.Default.EnableAutoSaveSkipStatusChanges = mMenuAutoSaveSkipStatusChanges.IsChecked;
                TaskList.Properties.Settings.Default.Save();
            }
        }


        private void RegexInputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("RegexInputTextBox_LostFocus");
            
            if (sender != mTextBoxRegexInput)
            {
                mTextBoxRegexInput.Text = "(\\[[^\\n\\r]*?\\]|\\s{1}\\d\\.|\\s{1}[\\*,@,#,\\+,\\-,>]\\s{1})";
            }
            TaskList.Properties.Settings.Default.RegexBold = mTextBoxRegexInput.Text;
            TaskList.Properties.Settings.Default.Save();
        }

    }
}
