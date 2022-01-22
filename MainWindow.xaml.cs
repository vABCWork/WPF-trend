using Microsoft.Win32;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
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
using System.Windows.Threading;

namespace Trend
{


    // 履歴(ヒストリ)データ　クラス
    // クラス名: HistoryData
    // メンバー:  double  data0
    //            double  data1
    //            double  data2
    //            double  data3
    //            double  data4
    //            double  dt
    //

    public class HistoryData
    {
        public double data0 { get; set; }       //  ch1
        public double data1 { get; set; }       //  ch2
        public double data2 { get; set; }       //  ch3
        public double data3 { get; set; }       //  ch4
        public double data4 { get; set; }       //  cjt
        public double dt { get; set; }         // 日時 (double型)
    }






    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public static byte[] sendBuf;          // 送信バッファ   
        public static int sendByteLen;         //　送信データのバイト数

        public static byte[] rcvBuf;           // 受信バッファ
        public static int srcv_pt;             // 受信データ格納位置

        public static DateTime receiveDateTime;           // 受信完了日時


        DispatcherTimer SendIntervalTimer;  // タイマー　モニタ用　電文送信間隔   
        DispatcherTimer RcvWaitTimer;       // タイマー　受信待ち用 

        public static ushort send_msg_cnt;              // 送信数 
        public static ushort disp_msg_cnt_max;          // 送受信文の表示最大数
                                             

        uint trend_data_item_max;             // 各リアルタイム　トレンドデータの保持数 

        double[] trend_data0;                 // トレンドデータ 0   ch1
        double[] trend_data1;                 // トレンドデータ 1   ch2
        double[] trend_data2;                 // トレンドデータ 2   ch3
        double[] trend_data3;                 // トレンドデータ 3   ch4
        double[] trend_data4;                 // トレンドデータ 4   cjt 

        double[] trend_dt;                    // トレンドデータ　収集日時

        ScottPlot.Plottable.ScatterPlot trend_scatter_0; // トレンドデータ0 
        ScottPlot.Plottable.ScatterPlot trend_scatter_1; // トレンドデータ1
        ScottPlot.Plottable.ScatterPlot trend_scatter_2; // トレンドデータ2
        ScottPlot.Plottable.ScatterPlot trend_scatter_3; // トレンドデータ3
        ScottPlot.Plottable.ScatterPlot trend_scatter_4; // トレンドデータ4


        public List<HistoryData> historyData_list;      // ヒストリデータ　データ収集時と保存データ読み出し時に使用

        ScottPlot.Plottable.ScatterPlot history_scatter_0;   // ヒストリデータ 0
        ScottPlot.Plottable.ScatterPlot history_scatter_1;   // ヒストリデータ 1
        ScottPlot.Plottable.ScatterPlot history_scatter_2;   // ヒストリデータ 2
        ScottPlot.Plottable.ScatterPlot history_scatter_3;   // ヒストリデータ 3
        ScottPlot.Plottable.ScatterPlot history_scatter_4;   // ヒストリデータ 4

        public static int commlog_window_cnt;                 // 通信ログ用ウィンドウの表示個数

        public MainWindow()
        {
            InitializeComponent();

            ConfSerial.serialPort = new SerialPort();    // シリアルポートのインスタンス生成

            ConfSerial.serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);  // データ受信時のイベント処理

            sendBuf = new byte[2048];     // 送信バッファ領域  serialPortのWriteBufferSize =2048 byte(デフォルト)
            rcvBuf = new byte[4096];      // 受信バッファ領域   SerialPort.ReadBufferSize = 4096 byte (デフォルト)

            disp_msg_cnt_max = 1000;        // 送受信文の表示最大数

            SendIntervalTimer = new System.Windows.Threading.DispatcherTimer();　　// タイマーの生成(定周期モニタ用)
            SendIntervalTimer.Tick += new EventHandler(SendIntervalTimer_Tick);  // タイマーイベント
            SendIntervalTimer.Interval = new TimeSpan(0, 0, 0, 0, 2000);         // タイマーイベント発生間隔 2sec(コマンド送信周期)

            RcvWaitTimer = new System.Windows.Threading.DispatcherTimer();　 // タイマーの生成(受信待ちタイマ)
            RcvWaitTimer.Tick += new EventHandler(RcvWaitTimer_Tick);        // タイマーイベント
            RcvWaitTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);          // タイマーイベント発生間隔 (受信待ち時間)



            historyData_list = new List<HistoryData>();     // トレンドデータ 記録用　
            
            historyData_list.Clear();                       // ヒストリデータのクリア

            Chart_Ini();                        // チャート(リアルタイム用)の初期化


        }

        // 　チャートの初期化(リアルタイム　チャート用)
        //  https://swharden.com/scottplot/faq/version-4.1/
        //
        private void Chart_Ini()
        {
            trend_data_item_max = 30;             // 各リアルタイム　トレンドデータの保持数(=30 ) 1秒毎に収集すると、30秒分のデータ

            trend_data0 = new double[trend_data_item_max];
            trend_data1 = new double[trend_data_item_max];
            trend_data2 = new double[trend_data_item_max];
            trend_data3 = new double[trend_data_item_max];
            trend_data4 = new double[trend_data_item_max];

            trend_dt = new double[trend_data_item_max];

            DateTime datetime = DateTime.Now;   // 現在の日時

            DateTime[] myDates = new DateTime[trend_data_item_max];


            for (int i = 0; i < trend_data_item_max; i++)
            {
                trend_data0[i] = i;
                trend_data1[i] = i + 1;
                trend_data2[i] = i + 2;
                trend_data3[i] = i + 3;
                trend_data4[i] = i + 4;

                myDates[i] = datetime + new TimeSpan(0, 0, i);  // i秒増やす

                trend_dt[i] = myDates[i].ToOADate();   // (現在の日時 + i 秒)をdouble型に変換
            }

            // X軸の日時リミットを、最終日時+1秒にする
            DateTime dt_end = DateTime.FromOADate(trend_dt[trend_data_item_max - 1]); // double型を　DateTime型に変換
            TimeSpan dt_sec = new TimeSpan(0, 0, 1);    // 1 秒
            DateTime dt_limit = dt_end + dt_sec;      // DateTime型(最終日時+ 1秒) 
            double dt_ax_limt = dt_limit.ToOADate();   // double型(最終日時+ 1秒) 

            wpfPlot_Trend.Refresh();        // データ変更後のリフレッシュ

            trend_scatter_0 = wpfPlot_Trend.Plot.AddScatter(trend_dt, trend_data0, color: System.Drawing.Color.Blue,  label: "ch1"); // プロット plot the data array only once
            trend_scatter_1 = wpfPlot_Trend.Plot.AddScatter(trend_dt, trend_data1, color: System.Drawing.Color.Orange,label: "ch2");
            trend_scatter_2 = wpfPlot_Trend.Plot.AddScatter(trend_dt, trend_data2, color: System.Drawing.Color.Green, label: "ch3");
            trend_scatter_3 = wpfPlot_Trend.Plot.AddScatter(trend_dt, trend_data3, color: System.Drawing.Color.Pink,  label: "ch4");
            trend_scatter_4 = wpfPlot_Trend.Plot.AddScatter(trend_dt, trend_data4, color: System.Drawing.Color.Black, label: "cjt");


            // PVグラフ
            wpfPlot_Trend.Configuration.Pan = false;               // パン(グラフの移動)不可
            wpfPlot_Trend.Configuration.ScrollWheelZoom = true;   // ズーム(グラフの拡大、縮小)可

            wpfPlot_Trend.Plot.SetAxisLimits(trend_dt[0], dt_ax_limt, 0, 50);  // X軸の最小=0,X軸の最大=tred_data0の大きさ,Y軸最小=0, Y軸最大=50℃


            wpfPlot_Trend.Plot.XAxis.Ticks(true, false, true);         // X軸の大きい目盛り=表示, X軸の小さい目盛り=非表示, X軸の目盛りのラベル=表示
            wpfPlot_Trend.Plot.XAxis.TickLabelStyle( fontSize: 16);

            wpfPlot_Trend.Plot.XAxis.TickLabelFormat("HH:mm:ss", dateTimeFormat: true); // X軸　時間の書式(例 12:30:15)、X軸の値は、日時型

            wpfPlot_Trend.Plot.XAxis.Label(label: "time", color: System.Drawing.Color.Black);  // X軸全体のラベル
            wpfPlot_Trend.Plot.YAxis.TickLabelStyle(fontSize: 16);     // Y軸   ラベルのフォントサイズ変更  :

            wpfPlot_Trend.Plot.YAxis.Label(label: "[℃]", color: System.Drawing.Color.Black);    // Y軸全体のラベル

            var legend1 = wpfPlot_Trend.Plot.Legend(enable: true, location: Alignment.UpperLeft);   // 凡例の表示
   
            legend1.FontSize = 12;      // 凡例のフォントサイズ


        }


        // モニタ用
        // 定周期にコマンドを送信する。
        private void SendIntervalTimer_Tick(object sender, EventArgs e)
        {
            bool fok = send_disp_data();       // データ送信
        }



        // 送信後、1000msec以内に受信文が得られないと、受信エラー
        //  
        private void RcvWaitTimer_Tick(object sender, EventArgs e)
        {

            RcvWaitTimer.Stop();        // 受信監視タイマの停止
            SendIntervalTimer.Stop();   // 定周期モニタ用タイマの停止

            StatusTextBlock.Text = "Receive time out";
        }


        // チェックボックスによるトレンド線の表示 (ch1,ch2,ch3,ch4,cjt 用)
        private void CH_N_Show(object sender, RoutedEventArgs e)
        {
            if (trend_scatter_0 is null) return;
            if (trend_scatter_1 is null) return;
            if (trend_scatter_2 is null) return;
            if (trend_scatter_3 is null) return;
            if (trend_scatter_4 is null) return;

            CheckBox checkBox = (CheckBox)sender;

            if (checkBox.Name == "Ch1_CheckBox")
            {
                trend_scatter_0.IsVisible = true;
            }
            else if (checkBox.Name == "Ch2_CheckBox")
            {
                trend_scatter_1.IsVisible = true;
            }
            else if (checkBox.Name == "Ch3_CheckBox")
            {
                trend_scatter_2.IsVisible = true;
            }
            else if (checkBox.Name == "Ch4_CheckBox")
            {
                trend_scatter_3.IsVisible = true;
            }
            else if (checkBox.Name == "Cjt_CheckBox")
            {
                trend_scatter_4.IsVisible = true;
            }

            wpfPlot_Trend.Render();   // グラフの更新
        }

        // チェックボックスによるトレンド線の非表示 (ch1,ch2,ch3,ch4,cjt 用)
        private void CH_N_Hide(object sender, RoutedEventArgs e)
        {

            if (trend_scatter_0 is null) return;
            if (trend_scatter_1 is null) return;
            if (trend_scatter_2 is null) return;
            if (trend_scatter_3 is null) return;
            if (trend_scatter_4 is null) return;

            CheckBox checkBox = (CheckBox)sender;

            if (checkBox.Name == "Ch1_CheckBox")
            {
                trend_scatter_0.IsVisible = false;
            }
            else if (checkBox.Name == "Ch2_CheckBox")
            {
                trend_scatter_1.IsVisible = false;
            }
            else if (checkBox.Name == "Ch3_CheckBox")
            {
                trend_scatter_2.IsVisible = false;
            }
            else if (checkBox.Name == "Ch4_CheckBox")
            {
                trend_scatter_3.IsVisible = false;
            }
            else if (checkBox.Name == "Cjt_CheckBox")
            {
                trend_scatter_4.IsVisible = false;
            }

            wpfPlot_Trend.Render();   // グラフの更新
        }


        // チェックボックスによるトレンド線の表示 履歴データ用 (ch1,ch2,ch3,ch4,cjt 用)
        private void History_CH_N_Show(object sender, RoutedEventArgs e)
        {
            if (history_scatter_0 is null) return;
            if (history_scatter_1 is null) return;
            if (history_scatter_2 is null) return;
            if (history_scatter_3 is null) return;
            if (history_scatter_4 is null) return;

            CheckBox checkBox = (CheckBox)sender;

            if (checkBox.Name == "History_Ch1_CheckBox")
            {
                history_scatter_0.IsVisible = true;
            }
            else if (checkBox.Name == "History_Ch2_CheckBox")
            {
                history_scatter_1.IsVisible = true;
            }
            else if (checkBox.Name == "History_Ch3_CheckBox")
            {
                history_scatter_2.IsVisible = true;
            }
            else if (checkBox.Name == "History_Ch4_CheckBox")
            {
                history_scatter_3.IsVisible = true;
            }
            else if (checkBox.Name == "History_Cjt_CheckBox")
            {
                history_scatter_4.IsVisible = true;
            }

            wpfPlot_History.Render();       // グラフの更新

        }
        // チェックボックスによるトレンド線の非表示 履歴データ用 (ch1,ch2,ch3,ch4,cjt 用)
        private void History_CH_N_Hide(object sender, RoutedEventArgs e)
        {

            if (history_scatter_0 is null) return;
            if (history_scatter_1 is null) return;
            if (history_scatter_2 is null) return;
            if (history_scatter_3 is null) return;
            if (history_scatter_4 is null) return;

            CheckBox checkBox = (CheckBox)sender;

            if (checkBox.Name == "History_Ch1_CheckBox")
            {
                history_scatter_0.IsVisible = false;
            }
            else if (checkBox.Name == "History_Ch2_CheckBox")
            {
                history_scatter_1.IsVisible = false;
            }
            else if (checkBox.Name == "History_Ch3_CheckBox")
            {
                history_scatter_2.IsVisible = false;
            }
            else if (checkBox.Name == "History_Ch4_CheckBox")
            {
                history_scatter_3.IsVisible = false;
            }
            else if (checkBox.Name == "History_Cjt_CheckBox")
            {
                history_scatter_4.IsVisible = false;
            }

            wpfPlot_History.Render();       // グラフの更新
        
        }


        private delegate void DelegateFn();

        // データ受信時のイベント処理
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            int id = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Console.WriteLine("DataReceivedHandlerのスレッドID : " + id);

            int rd_num = ConfSerial.serialPort.BytesToRead;       // 受信データ数

            ConfSerial.serialPort.Read(rcvBuf, srcv_pt, rd_num);   // 受信データの読み出し

            srcv_pt = srcv_pt + rd_num;     // 次回の保存位置

            int rcv_total_byte = 0;
            if (rcvBuf[0] == 0xd0)          // 温度モニタコマンドのレスポンスの場合
            {
                rcv_total_byte = 24;
            }


            if (srcv_pt == rcv_total_byte)  // 最終データ受信済み (受信データは、52byte固定)
            {
                RcvWaitTimer.Stop();        // 受信監視タイマー　停止

                receiveDateTime = DateTime.Now;   // 受信完了時刻を得る

                Dispatcher.BeginInvoke(new DelegateFn(RcvMsgProc)); // Delegateを生成して、RcvMsgProcを開始   (表示は別スレッドのため)
            }

        }


        //  
        //  最終データ受信後の処理
        private void RcvMsgProc()
        {

            if (rcvBuf[0] == 0xd0)     // 温度モニタコマンドのレスポンスの場合
            {
                 graph_trend_data();   //  モニタデータのグラフ表示と、記録用リストへの格納

            }

            if (CommLog.rcvframe_list != null)
            {
                CommLog.rcvmsg_disp();          // 受信データの表示       
            }
        }


        //  モニタデータのグラフ表示と、記録用リストへの格納
        // No. 受信データ :内容

        //  0: rcvBuf[0] : 0xd0 (コマンドに対するレスポンス)
        //     rcvBuf[1] : dummy 0
        //     rcvBuf[2] : dummy 0
        //     rcvBuf[3] : dummy 0
        //  1: rcvBuf[4] : ch1の最下位バイト  (単精度浮動小数点データ)
        //     rcvBuf[5] :
        //     rcvBuf[6] :    
        //     rcvBuf[7] : ch1の最上位バイト
        //  2: rcvBuf[8] : ch2の最下位バイト  (単精度浮動小数点データ)
        //     rcvBuf[9] :
        //     rcvBuf[10] :    
        //     rcvBuf[11] : ch2の最上位バイト
        //  3: rcvBuf[12] : ch3の最下位バイト (単精度浮動小数点データ) 
        //     rcvBuf[13] :
        //     rcvBuf[14] :    
        //     rcvBuf[15] : ch3の最上位バイト
        //  4: rcvBuf[16] : ch4の最下位バイト (単精度浮動小数点データ) 
        //     rcvBuf[17] :
        //     rcvBuf[18] :    
        //     rcvBuf[19] : ch4の最上位バイト        :
        //  5: rcvBuf[20]:  cjt の最下位バイト (単精度浮動小数点データ) 
        //     rcvBuf[21]:
        //     rcvBuf[22]:
        //     rcvBuf[23]:  cjtの最上位バイト
        //

        private void graph_trend_data()
        {
            float t;
            float ch1, ch2, ch3, ch4, cjt;

            t = BitConverter.ToSingle(rcvBuf, 4);    // rcvBuf[4]から 単精度浮動小数点へ変換
            Ch1_TextBox.Text = t.ToString("F1");      //  Ch1
            ch1 = t;

            t = BitConverter.ToSingle(rcvBuf, 8);    // rcvBuf[8]から 単精度浮動小数点へ変換
            Ch2_TextBox.Text = t.ToString("F1");     //  Ch2
            ch2 = t;

            t = BitConverter.ToSingle(rcvBuf, 12);    // rcvBuf[12]から 単精度浮動小数点へ変換
            Ch3_TextBox.Text = t.ToString("F1");     //  Ch3
            ch3 = t;

            t = BitConverter.ToSingle(rcvBuf, 16);   // rcvBuf[16]から 単精度浮動小数点へ変換
            Ch4_TextBox.Text = t.ToString("F1");     //  Ch4
            ch4 = t;

            t = BitConverter.ToSingle(rcvBuf, 20);   // rcvBuf[20]から 単精度浮動小数点へ変換
            Cjt_TextBox.Text = t.ToString("F1");     //  Cjt
            cjt = t;

            Array.Copy(trend_data0, 1, trend_data0, 0, trend_data_item_max - 1);
            trend_data0[trend_data_item_max - 1] = ch1;

            Array.Copy(trend_data1, 1, trend_data1, 0, trend_data_item_max - 1);
            trend_data1[trend_data_item_max - 1] = ch2;

            Array.Copy(trend_data2, 1, trend_data2, 0, trend_data_item_max - 1);
            trend_data2[trend_data_item_max - 1] = ch3;

            Array.Copy(trend_data3, 1, trend_data3, 0, trend_data_item_max - 1);
            trend_data3[trend_data_item_max - 1] = ch4;

            Array.Copy(trend_data4, 1, trend_data4, 0, trend_data_item_max - 1);
            trend_data4[trend_data_item_max - 1] = cjt;


            Array.Copy(trend_dt, 1, trend_dt, 0, trend_data_item_max - 1);
            trend_dt[trend_data_item_max - 1] = receiveDateTime.ToOADate();    // 受信日時 double型に変換して、格納

            wpfPlot_Trend.Render();   // リアルタイム グラフの更新
       
            wpfPlot_Trend.Plot.AxisAuto();     // X軸の範囲を更新


            HistoryData historyData = new HistoryData();     // 保存用ヒストリデータ

            historyData.data0 = ch1;
            historyData.data1 = ch2;
            historyData.data2 = ch3;
            historyData.data3 = ch4;
            historyData.data4 = cjt;
            historyData.dt = receiveDateTime.ToOADate();   // 受信日時を deouble型で格納

            historyData_list.Add(historyData);      　　　　// Listへ保持
        }



        // モニタ開始コマンド
        private void Start_Monitor_Button_Click(object sender, RoutedEventArgs e)
        {
          
            sendBuf[0] = 0x50;     // 送信コマンド  
            sendBuf[1] = 0;
            sendBuf[2] = 0;
            sendBuf[3] = 0;
            sendByteLen = 4;                // 送信バイト数

            send_msg_cnt = 0;              // 送信数 

            SendIntervalTimer.Start();   // 定周期　送信用タイマの開始

        }

        // コマンド送信の停止
        private void Stop_Monitor_Button_Click(object sender, RoutedEventArgs e)
        {
            SendIntervalTimer.Stop();   // 定周期　送信用タイマの停止
        }


        //  送信と送信データの表示
        // sendBuf[]のデータを、sendByteLenバイト　送信する
        // 戻り値  送信成功時: true
        //         送信失敗時: false

        private bool send_disp_data()
        {
            if (ConfSerial.serialPort.IsOpen == true)
            {
                srcv_pt = 0;                   // 受信データ格納位置クリア

                ConfSerial.serialPort.Write(sendBuf, 0, sendByteLen);     // データ送信

                send_msg_cnt++;              // 送信数インクリメント 

                if (CommLog.sendframe_list != null)
                {
                    CommLog.sendmsg_disp();          // 送信データの表示
                }

                RcvWaitTimer.Start();        // 受信監視タイマー　開始

                StatusTextBlock.Text = "";
                return true;
            }

            else
            {
                StatusTextBlock.Text = "Comm port closed !";
                SendIntervalTimer.Stop();
                return false;
            }

        }

        //　通信条件の設定 ダイアログを開く
        //  
        private void Serial_Button_Click(object sender, RoutedEventArgs e)
        {
            new ConfSerial().ShowDialog();
        }

        //
        //  保存ボタンの処理
        //
        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            string path;

            string str_one_line;


            SaveFileDialog sfd = new SaveFileDialog();           //　SaveFileDialogクラスのインスタンスを作成 

            sfd.FileName = "trend.csv";                              //「ファイル名」で表示される文字列を指定する

            sfd.Title = "保存先のファイルを選択してください。";        //タイトルを設定する 

            sfd.RestoreDirectory = true;                 //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする

            if (sfd.ShowDialog() == true)            //ダイアログを表示する
            {
                path = sfd.FileName;

                try
                {
                    System.IO.StreamWriter sw = new System.IO.StreamWriter(path, false, System.Text.Encoding.Default);

                    str_one_line = DataMemoTextBox.Text; // メモ欄
                    sw.WriteLine(str_one_line);         // 1行保存

                    str_one_line = "DateTime" + "," + "ch1" + "," + "ch2" + "," + "ch3" + "," + "ch4" + "," + "cjt";

                    sw.WriteLine(str_one_line);         // 1行保存


                    foreach (HistoryData historyData in historyData_list)         // historyData_listの内容を保存
                    {
                        DateTime dateTime = DateTime.FromOADate(historyData.dt); // 記録されている日時(double型)を　DateTime型に変換


                        string st_dateTime = dateTime.ToString("yyyy/MM/dd HH:mm:ss.fff");             // DateTime型を文字型に変換　（2021/10/22 11:09:06.125 )
                        //string st_dateTime = dateTime.ToString("G");             // DateTime型を文字型に変換　（2021/10/22 11:09:06 )

                        string st_dt0 = historyData.data0.ToString("F1");       // ch1 文字型に変換 (25.0)
                        string st_dt1 = historyData.data1.ToString("F1");       // ch2
                        string st_dt2 = historyData.data2.ToString("F1");       // ch3
                        string st_dt3 = historyData.data3.ToString("F1");       // ch4
                        string st_dt4 = historyData.data4.ToString("F1");       // cjt


                        str_one_line = st_dateTime + "," + st_dt0 + "," + st_dt1 + "," + st_dt2 + "," + st_dt3 + "," + st_dt4;

                        sw.WriteLine(str_one_line);         // 1行保存

                    }

                    sw.Close();
                }

                catch (System.Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

            }
        }

        // Openボタンの処理
        private void Open_Button_Click(object sender, RoutedEventArgs e)
        {


            var dialog = new OpenFileDialog();   // ダイアログのインスタンスを生成

            dialog.Filter = "csvファイル (*.csv)|*.csv|全てのファイル (*.*)|*.*";  //  // ファイルの種類を設定

            dialog.RestoreDirectory = true;                 //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする


            if (dialog.ShowDialog() == false)     // ダイアログを表示する
            {
                return;                          // キャンセルの場合、リターン
            }


            try
            {
                historyData_list.Clear();            // ヒストリデータのクリア

                StreamReader sr = new StreamReader(dialog.FileName, Encoding.GetEncoding("SHIFT_JIS"));    //  CSVファイルを読みだし

                FileNameTextBox.Text = dialog.FileName;                // ファイル名の表示


                HistoryDataMemoTextBox.Text = sr.ReadLine();              // 先頭 Memoの読み出し、表示

                sr.ReadLine();              // 読み飛ばし (2行目は、日時、ch名のため)

                while (!sr.EndOfStream)     // ファイル最終行まで、繰り返し
                {
                    HistoryData historyData = new HistoryData();        // 読み出しデータを格納するクラス

                    string line = sr.ReadLine();        // 1行の読み出し

                    string[] items = line.Split(',');       // 1行を、,(カンマ)毎に items[]に格納 

                    DateTime dateTime;
                    DateTime.TryParse(items[0], out dateTime);  // 日付の文字列を DateTime型へ変換

                    historyData.dt = dateTime.ToOADate();       // DateTiem型を double型へ変換


                    double.TryParse(items[1], out double d0); // ch1の値　文字列を double型へ変換
                    historyData.data0 = d0;                   // クラスのメンバーへ格納

                    double.TryParse(items[2], out double d1); // ch2の値　文字列を double型へ変換
                    historyData.data1 = d1;                   // クラスのメンバーへ格納

                    double.TryParse(items[3], out double d2); // ch3の値　文字列を double型へ変換
                    historyData.data2 = d2;                   // クラスのメンバーへ格納

                    double.TryParse(items[4], out double d3); // ch4の値　文字列を double型へ変換
                    historyData.data3 = d3;                   // クラスのメンバーへ格納

                    double.TryParse(items[5], out double d4); // cjt　文字列を double型へ変換
                    historyData.data4 = d4;                   // クラスのメンバーへ格納


                    historyData_list.Add(historyData);      // Listへ追加

                }

                 disp_history_graph();       // ヒストリトレンドデータのグラフ表示

            }

            catch (Exception ex) when (ex is IOException || ex is IndexOutOfRangeException)
            {

                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            }
        }
        //
        //  ヒストリトレンドデータのグラフ表示
        void disp_history_graph()
        {
            wpfPlot_History.Plot.Clear();

            int cnt_max = historyData_list.Count;       // 行数分の配列

            double[] t_data0 = new double[cnt_max];   // トレンドデータ 0   ch1
            double[] t_data1 = new double[cnt_max];   // トレンドデータ 1   ch2
            double[] t_data2 = new double[cnt_max];   // トレンドデータ 2   ch3
            double[] t_data3 = new double[cnt_max];   // トレンドデータ 3   ch4
            double[] t_data4 = new double[cnt_max];   // トレンドデータ 4   cjt

            double[] t_dt = new double[cnt_max];       //  date time


            for (int i = 0; i < cnt_max; i++)                   // List化された、historyDataクラスの情報をグラフ表示用の配列にコピー 
            {
                t_data0[i] = historyData_list[i].data0;       // ch1
                t_data1[i] = historyData_list[i].data1;       // ch2
                t_data2[i] = historyData_list[i].data2;       // ch3
                t_data3[i] = historyData_list[i].data3;       // ch4 
                t_data4[i] = historyData_list[i].data4;       // cjt

                t_dt[i] = historyData_list[i].dt;           // data tiem
            }

            wpfPlot_History.Refresh();       // データ変更後は、Refresh
            

            history_scatter_0 = wpfPlot_History.Plot.AddScatter(t_dt, t_data0, color: System.Drawing.Color.Blue, label: "ch1");    // 散布図へ表示　t_data0)
            history_scatter_1 = wpfPlot_History.Plot.AddScatter(t_dt, t_data1, color: System.Drawing.Color.Orange, label: "ch2");
            history_scatter_2 = wpfPlot_History.Plot.AddScatter(t_dt, t_data2, color: System.Drawing.Color.Green, label: "ch3");
            history_scatter_3 = wpfPlot_History.Plot.AddScatter(t_dt, t_data3, color: System.Drawing.Color.Pink, label: "ch4");
            history_scatter_4 = wpfPlot_History.Plot.AddScatter(t_dt, t_data4, color: System.Drawing.Color.Black, label: "cjt");


            // PV History用
            wpfPlot_History.Configuration.Pan = true;               // パン(グラフの移動)　
            wpfPlot_History.Configuration.ScrollWheelZoom = true;   // ズーム(グラフの拡大、縮小)可

            wpfPlot_History.Plot.AxisAuto();                        // X軸、Y軸の上下限をデータに自動調整

            wpfPlot_History.Plot.XAxis.Ticks(true, false, true);         // X軸の大きい目盛り=表示, X軸の小さい目盛り=非表示, X軸の目盛りのラベル=表示
            wpfPlot_History.Plot.XAxis.DateTimeFormat(true);             // X軸の値は、日時型


            wpfPlot_History.Plot.XAxis.TickLabelStyle(fontSize: 16);     // X軸  Tickラベルのフォントサイズ変更  :
            wpfPlot_History.Plot.XLabel("time");                         // X軸全体のラベル

            wpfPlot_History.Plot.YAxis.TickLabelStyle(fontSize: 16);    // Y軸   ラベルのフォントサイズ変更  :
            wpfPlot_History.Plot.YLabel("[℃]");                         // Y軸全体のラベル

            wpfPlot_History.Render();   // ヒストリデータ グラフの更新

            var legend1 = wpfPlot_History.Plot.Legend(enable: true, location: Alignment.UpperLeft);   // 凡例の表示
            legend1.FontSize = 12;      // 凡例のフォントサイズ

        }

            // 収集済みのデータをクリアの確認
            private void Clear_Button_Click(object sender, RoutedEventArgs e)
        {
            string messageBoxText = "収集済みのデータがクリアされます。";
            string caption = "Check clear";

            MessageBoxButton button = MessageBoxButton.YesNoCancel;
            MessageBoxImage icon = MessageBoxImage.Warning;
            MessageBoxResult result;

            result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);

            switch (result)
            {
                case MessageBoxResult.Yes:      // Yesを押した場合
                    historyData_list.Clear();   // 収集済みのデータのクリア
                    break;

                case MessageBoxResult.No: 
                    break;

                case MessageBoxResult.Cancel:
                    break;
            }


        }
        // 通信メッセージ表示用のウィンドウを開く
        private void Comm_Log_Button_Click(object sender, RoutedEventArgs e)
        {

            if (commlog_window_cnt > 0) return;   // 既に開いている場合、リターン

            var window = new CommLog();

            window.Owner = this;   // Paraウィンドウの親は、このMainWindow

            window.Show();

            commlog_window_cnt++;     // カウンタインクリメント
        }

    }
}
