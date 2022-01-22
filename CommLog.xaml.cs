using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Shapes;

namespace Trend
{

    // 送信文表示用のクラス
    public class SendFrame
    {
        public string msg { get; set; }   // 送信文
        public override string ToString()
        {
            return msg;
        }

    }
    // 受信文表示用のクラス
    public class RcvFrame
    {
        public string msg { get; set; }   // 受信文
        public override string ToString()
        {
            return msg;
        }

    }

    /// <summary>
    /// CommLog.xaml の相互作用ロジック
    /// </summary>
    public partial class CommLog : Window
    {
        public static ObservableCollection<SendFrame> sendframe_list;
        public static ObservableCollection<RcvFrame> rcvframe_list;


        public CommLog()
        {
            InitializeComponent();

            sendframe_list = new ObservableCollection<SendFrame>();   // クラス SendFrameのコレクションをデータバインディングするため
            rcvframe_list = new ObservableCollection<RcvFrame>();

            this.SendmsgItems.ItemsSource = sendframe_list;
            this.RcvmsgItems.ItemsSource = rcvframe_list;

        }



        // 送信データのリストへの追加
        public static void sendmsg_disp()
        {
            if (MainWindow.send_msg_cnt > MainWindow.disp_msg_cnt_max) // 最大表示行数より大きい場合、リストをクリア
            {
                sendframe_list.Clear();
                rcvframe_list.Clear();

                MainWindow.send_msg_cnt = 0;
            }

            string msg = "";

            for (int i = 0; i < MainWindow.sendByteLen; i++)
            {
                msg += MainWindow.sendBuf[i].ToString("X2") + " ";
            }

            msg += "(" + DateTime.Now.ToString("HH:mm:ss.fff") + ")" + "\r\n";

            SendFrame sendFrame = new SendFrame();
            sendFrame.msg = msg;

            sendframe_list.Add(sendFrame);  　// Listに追加
        }



        // 受信データの表示
        //
        public static void rcvmsg_disp()
        {
            string rcv_str = "";

            for (int i = 0; i < MainWindow.srcv_pt; i++)   // 表示用の文字列作成
            {
                rcv_str = rcv_str + MainWindow.rcvBuf[i].ToString("X2") + " ";
            }

            // 受信文と時刻
           　rcv_str += "(" + MainWindow.receiveDateTime.ToString("HH:mm:ss.fff") + ")(" + MainWindow.srcv_pt.ToString() + " bytes )" + "\r\n";

            RcvFrame rcvFrame = new RcvFrame();
            rcvFrame.msg = rcv_str;

            rcvframe_list.Add(rcvFrame);     // Listへ追加
        }


        // Windowが閉じられる際の処理
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainWindow.commlog_window_cnt = 0;     // 通信ログ用ウィンドウの表示個数のクリア

        }
    }
}
