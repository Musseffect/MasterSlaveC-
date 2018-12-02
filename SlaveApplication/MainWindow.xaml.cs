using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace SlaveApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(String property)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }
        private Slave slave;
        private ObservableCollection<String> logMessages;
        public Slave SlaveObject
        {
            get { return slave; }
        }
        public ObservableCollection<String> LogItems
        {
            get { return logMessages; }
        }
        Thread tcpThread;
        public MainWindow()
        {
            logMessages = new ObservableCollection<String>() {"Программа запущена"};
            slave=new Slave();
            InitializeComponent();
            slave.Log+=LogHandler;
            slave.ExceptionRestart+=ExceptionHandler;
            RunSlave();
            DataContext = this;
        }
        private void RunSlave()
        {
            tcpThread = new Thread(slave.listenTCP);
            tcpThread.IsBackground = true;
            tcpThread.Start();
        }
        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            tcpThread.Abort();
        }
        private void RestartSlave()
        {
            tcpThread.Abort();
            tcpThread = new Thread(slave.listenTCP);
            tcpThread.IsBackground = true;
            tcpThread.Start();
        }
        private void LogHandler(string message)
        {
            Dispatcher.BeginInvoke((Action)(delegate { Log(message); }));
        }
        private void Log(string message)
        {
            logMessages.Insert(0, DateTime.Now.ToString("[h:mm:tt] ") + message);
            for (int i = logMessages.Count - 1; i > 11; i--)
                logMessages.RemoveAt(i);
        }
        private void ExceptionHandler(string message)
        {
            LogHandler(message);
            RestartSlave();
        }
        ~MainWindow()
        {
            tcpThread.Abort();
        }
    }
}
