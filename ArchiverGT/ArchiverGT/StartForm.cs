using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Numerics;

namespace ArchiverGT
{
    public partial class StartForm : Form
    {

        Archiving A = null;
        Point LastPoint;

        public StartForm()
        {
            InitializeComponent();
            this.Icon = Properties.Resources.Icon;
        }  

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if(e.Button == MouseButtons.Left)
            {
                this.Left += e.X - LastPoint.X;
                this.Top += e.Y - LastPoint.Y;
            }
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            LastPoint = new Point(e.X, e.Y);
        }

        private void OpenButton_MouseEnter(object sender, EventArgs e)
        {
            OpenButton.Image = Properties.Resources.path3;
        }

        private void OpenButton_MouseLeave(object sender, EventArgs e)
        {
            OpenButton.Image = Properties.Resources.path2;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            labelPercent.Text = "" + Obmen.ProgressPercent + " %";
            ProgressArchiving.Value = Obmen.ProgressPercent;
            AppConsole.WriteLog();

            if (A.Flag_Stop == 1)
            {
                textBox1.Text = "";
                ProgressArchiving.Value = 0;
                Obmen.ProgressPercent = 0;
                ArchBotton.Enabled = true;
                UnzipBotton.Enabled = true;
            }
        }
        private void timer2_Tick(object sender, EventArgs e)
        {
            AppConsole2.Print_Console();
        }

        private void OpenButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.Cancel)
                return;
            Obmen.Source_FilePath = openFileDialog1.FileName;
            textBox1.Text = openFileDialog1.FileName;

            A = new Archiving();
        }

        private void ArchBotton_Click(object sender, EventArgs e)
        {
            if (A != null)
            {
                if (A.Flag_Start == 0)
                {
                    var VarThread = new Thread(A.Gain_Thread);
                    VarThread.IsBackground = true;
                    VarThread.Start();
                    timer1.Start();
                    timer2.Start();
                    ArchBotton.Enabled = false;
                }
            }
        }

        private void UnzipBotton_Click(object sender, EventArgs e)
        {
            if (A != null)
            {
                if (A.Flag_Start == 0)
                {
                    var VarThread = new Thread(A.ArcMethod_Decompress_GZip_Custom);
                    VarThread.IsBackground = true;
                    VarThread.Start();
                    timer1.Start();
                    timer2.Start();
                    UnzipBotton.Enabled = true;
                }
            }   
        }
    }

    class Archiving
    {
        public delegate void ArchHandler(string message);
        public event ArchHandler Notify;
        private string SourceFile = "";
        private string CompressedFile = "";
        private string[] CompressedFile2 = new string[2];
        private string TargetFile = "";
        public object Locker_Compressed = new object();
        public object Locker_Withdrawal = new object();
        public FileStream source_stream;
        public FileStream target_stream;

        public int PackageNumber_Withdrawal = 0;
        public int PackageNumber_Compressed = 1;
        public int Number_Thread = 0;
        public int FlagShowTime = 0;
        public int Flag_Start = 0;
        public int Flag_Stop = 0;
        public long Byte_Priority_Withdrawal = 0;
        public long Byte_Priority_Compression = 1;
        public long CountFinish = 0;
        public int CountFile = 0;
        public int[] SizeFile = new int[1000000];
        public int Flag_Finish_GL = 0;
        public int Flag_Stop_Thread = 0;
        public long Priority_Gl = 0;
        public ConcurrentDictionary<long, byte[]> Dict_Gl = new ConcurrentDictionary<long, byte[]>();
        public int SumPackages = 0;

        //Конструктор
        public Archiving()
        {
            SourceFile = Obmen.Source_FilePath;           
        }

        public void LockField_FileStream_Compressed()
        {
            while (Flag_Stop != 1)
            {
                try
                {
                    if (Dict_Gl[Byte_Priority_Compression] != null)
                    {
                        target_stream.Write(Dict_Gl[Byte_Priority_Compression], 0, Dict_Gl[Byte_Priority_Compression].Length);
                        Dict_Gl[Byte_Priority_Compression] = null;
                        Obmen.ProgressPercent = (int)((100 * Byte_Priority_Compression) / SumPackages);
                        Byte_Priority_Compression++;
                    }
                }
                catch
                {

                }
            }
        }

        public void LockField_Compressed()
        {
            byte[] ByteArr;
            byte[] ByteArr2;
            var VarThread = new Thread(this.LockField_FileStream_Compressed);
            MemoryStream StreamPart;
            GZipStream compression_stream;
            long Priority = 0;
            int Read = 0;
            long Position = 0;
            int flag_finish = 0;

            try
            {
                while (Flag_Finish_GL == 0)
                {
                    ByteArr = new byte[1000000];
                    LockField_Withdrawal(ByteArr, ref Priority, ref Read, ref Position, ref flag_finish);

                    if (Flag_Finish_GL == 0)
                    {
                        StreamPart = new MemoryStream();
                        compression_stream = new GZipStream(StreamPart, CompressionMode.Compress);
                        compression_stream.Write(ByteArr, 0, Read);
                        compression_stream.Close();
                        ByteArr2 = StreamPart.ToArray();
                        SizeFile[Priority - 1] = (int)ByteArr2.Length;
                        Priority_Gl = Priority;

                        Dict_Gl.TryAdd(Priority, ByteArr2);
                    }
                }
            }
            catch (Exception ex)
            {
                AppConsole.Console_Byte(ex.Message);
            }    
        }

        public void LockField_Withdrawal(byte[] ByteArr, ref long Priority, ref int Read, ref long Position, ref int flag_finish)
        {
            int RAM_Buff = 0;
            lock (Locker_Withdrawal)
            {
                try
                {
                    RAM_Buff = (int)Process.GetProcessesByName("ArchiverGT")[0].WorkingSet64 / 1024 / 1024;
                    if (RAM_Buff > 700)
                    {
                        Monitor.Wait(Locker_Withdrawal);
                    }
                    else
                    {
                        Monitor.PulseAll(Locker_Withdrawal);
                    }

                    if (Flag_Finish_GL == 0)
                    {
                        if ((source_stream.Position != source_stream.Length))
                        {
                            Byte_Priority_Withdrawal++;
                            Priority = Byte_Priority_Withdrawal;
                            Read = source_stream.Read(ByteArr, 0, 1000000);
                            Position = source_stream.Position;

                            Obmen.Position_Source = Position;
                            CountFile = (int)Priority;
                        }
                        else
                        {
                            Flag_Finish_GL = 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppConsole.Console_Byte(ex.Message);
                }
            }
        }

        public void Gain_Thread()
        {
            int flag_finish = 0;
            Flag_Start = 1;

            CompressedFile2 = SourceFile.Split(new string[] { Path.GetExtension(Obmen.Source_FilePath) }, StringSplitOptions.RemoveEmptyEntries);
            CompressedFile = CompressedFile2[0] + ".gz";

            try
            {
                source_stream = new FileStream(SourceFile, FileMode.Open, FileAccess.Read);
            }
            catch (Exception ex)
            {
                AppConsole.Console_Byte(ex.Message);
            }
            Obmen.Lenght_Source = source_stream.Length;
            SumPackages = (int)(Obmen.Lenght_Source / 1000000);
            SumPackages = ((int)(Obmen.Lenght_Source % 1000000)) > 0 ? (SumPackages + 1) : SumPackages;

            target_stream = new FileStream(CompressedFile, FileMode.Create, FileAccess.Write);
            target_stream.Position = 4 * 1000000 + 4 + 10;

            var sw = new Stopwatch();
            Obmen.Sw_Obmen = sw;
            sw.Start();

            var VarThread = new Thread(this.LockField_FileStream_Compressed);
            VarThread.IsBackground = true;
            VarThread.Start();

            AppConsole.Console_Byte("Архивирование началось");

            VarThread = new Thread(this.LockField_Compressed);
            VarThread.IsBackground = true;
            VarThread.Start();

            VarThread = new Thread(this.LockField_Compressed);
            VarThread.IsBackground = true;
            VarThread.Start();

            VarThread = new Thread(this.LockField_Compressed);
            VarThread.IsBackground = true;
            VarThread.Start();

            VarThread = new Thread(this.LockField_Compressed);
            VarThread.IsBackground = true;
            VarThread.Start();

            VarThread = new Thread(this.LockField_Compressed);
            VarThread.IsBackground = true;
            VarThread.Start();

            while (flag_finish == 0)
            {
                if (SumPackages + 1 == Byte_Priority_Compression)
                {          
                    target_stream.Position = 0;

                    byte[] Format_Bytes = Encoding.UTF8.GetBytes(Path.GetExtension(Obmen.Source_FilePath));
                    byte[] Format_Bytes_Size = new byte[1];
                    Format_Bytes_Size[0] = (byte)Format_Bytes.Length;

                    target_stream.Write(Format_Bytes_Size, 0, 1);
                    target_stream.Write(Format_Bytes, 0, Format_Bytes.Length);

                    byte[] CountFile_Bytes = BitConverter.GetBytes(CountFile);
                    target_stream.Write(CountFile_Bytes, 0, CountFile_Bytes.Length);

                    byte[][] SizeFile_Bytes = new byte[1000000][];
                    for (int i = 0; i < CountFile; i++)
                    {
                        SizeFile_Bytes[i] = BitConverter.GetBytes(SizeFile[i]);
                    }
                    for (int i = 0; i < CountFile; i++)
                    {
                        target_stream.Write(SizeFile_Bytes[i], 0, SizeFile_Bytes[i].Length);
                    }

                    sw.Stop();
                    Obmen.Time1 = Convert.ToString(sw.ElapsedMilliseconds);
                    AppConsole.Console_Byte("Архивирование закончено");
                    AppConsole.Console_Byte("Время " + Obmen.Time1);
                    FlagShowTime = 0;
                    flag_finish = 1;
                    Flag_Stop = 1;

                    source_stream.Close();
                    source_stream.Dispose();
                    source_stream = null;
                    target_stream.Close();
                    target_stream.Dispose();
                    target_stream = null;
                }
            }
        }

        public void ArcMethod_Decompress_GZip_Custom()
        {
            byte[] ByteArr = new byte[1000000];
            byte[] ByteArr2 = new byte[1000000];
            string Format;
            int Read = 0;
            int Read2 = 0;
            MemoryStream Part2;

            CompressedFile2 = SourceFile.Split(new string[] { Path.GetExtension(Obmen.Source_FilePath) }, StringSplitOptions.RemoveEmptyEntries);
            CompressedFile = SourceFile;
            TargetFile = CompressedFile2[0];

            var sw = new Stopwatch();
            sw.Start();

            try
            {
                using (FileStream source_stream = new FileStream(CompressedFile, FileMode.Open, FileAccess.Read))
                {
                    int size = 0;
                    byte[] bytes_inf0 = new byte[4];
                    byte[] bytes_inf1 = new byte[10];
                    byte[] bytes_inf2 = new byte[4];
                    byte[] bytes_inf3 = new byte[7];

                    AppConsole.Console_Byte("1 ");
                    source_stream.Read(bytes_inf0, 0, 1);
                    AppConsole.Console_Byte("2 ");
                    size = BitConverter.ToInt32(bytes_inf0, 0);
                    AppConsole.Console_Byte("3 ");

                    source_stream.Read(bytes_inf1, 0, 4);
                    Format = Encoding.UTF8.GetString(bytes_inf1, 0, 4);
                    AppConsole.Console_Byte("Format " + Format);
                    TargetFile += Format;

                    //source_stream.Position = 10;
                    source_stream.Read(bytes_inf2, 0 , 4);
                    CountFile = BitConverter.ToInt32(bytes_inf2, 0);
                    AppConsole.Console_Byte("CountFile " + CountFile);

                    for (int i=0; i< CountFile; i++)
                    {
                        source_stream.Read(bytes_inf3, 0, 4);
                        SizeFile[i] = BitConverter.ToInt32(bytes_inf3, 0);
                        //AppConsole.Console_Byte("SizeFile " + SizeFile[i]);
                    }

                    source_stream.Position = 10 + 4 + 4 * 1000000;

                    AppConsole.Console_Byte("Разархивирование GZip");

                    //Поток для записи востановленнго файла
                    using (FileStream target_stream = File.Create(TargetFile))
                    {
                        for (int j = 0; j < CountFile; j++)
                        {
                            ByteArr = new byte[1200000];
                            ByteArr2 = new byte[1200000];
                            Part2 = new MemoryStream();
                            using (MemoryStream Part = new MemoryStream())
                            {
                                Read = source_stream.Read(ByteArr, 0, SizeFile[j]);
                                Part.Write(ByteArr, 0, Read);
                                Part.Position = 0;
                                AppConsole.Console_Byte("Размер Orig - " + SizeFile[j]);
                                AppConsole.Console_Byte("Размер Part - " + Part.Length);

                                using (GZipStream decompression_stream = new GZipStream(Part, CompressionMode.Decompress))
                                {
                                    AppConsole.Console_Byte("Посылка " + j);
                                    decompression_stream.CopyTo(Part2);
                                    Part2.Position = 0;
                                    AppConsole.Console_Byte("Размер Part2 - " + Part2.Length);
                                    Read2 = Part2.Read(ByteArr2, 0, (int)Part2.Length);
                                    target_stream.Write(ByteArr2, 0, Read2);
                                    Obmen.ProgressPercent = (int)((100 * j) / (CountFile-1));
                                }
                            }
                        }

                        //Завершение разархивирования
                        Flag_Stop = 1;
                    }

                }

                AppConsole.Console_Byte("Файл разархивирован");
            }
            catch (Exception ex)
            {
                AppConsole.Console_Byte(ex.Message);
            }

            sw.Stop();
            Obmen.Time1 = Convert.ToString(sw.ElapsedMilliseconds);
            AppConsole.Console_Byte("Время " + Obmen.Time1);
        }
    }

    struct Package
    {
        public long Priority;
        //public long position;
        public int Read;
        public object Array;
        public object Stream_M;
    }

    class Obmen
    {
        public static object Locker = new object();
        public static int Send_Flag = 0;
        public static int Flag_Log2 = 0;
        public static int Flag_Dec_Cus = 0;
        public static string Time1;
        public static string Source_FilePath = null;

        //Для вывода//
        public static long Lenght_Source = 0;
        public static long Position_Source = 0;
        public static int ProgressPercent = 0;
        public static Stopwatch Sw_Obmen = new Stopwatch();

        //public static void StopNeural()
        //{
        //    lock (Locker)
        //    {

        //    }
        //}
    }

    class AppConsole
    {

        public static string GeneralArrStr = "";
        public static string ReadArrStr = "";

        //Печать в консоль построчно (с переходим на новую строку):
        public static void Console_Line(object txt)
        {
            //MyForm.TxtConsole.AppendText(txt + "");
            //MyForm.TxtConsole.Text = txt + "";
        }

        //Заполнение массива на вывод:
        public static void Console_Byte(object txt)
        {
            GeneralArrStr = GeneralArrStr + txt + "\r" + "\n";
        }

        //Запись  в лог файл
        public static void WriteLog()
        {
            string writePath = "Log.txt";

            try
            {
                using (StreamWriter sw = new StreamWriter(writePath, false, System.Text.Encoding.Default))
                {
                    sw.WriteLine(GeneralArrStr);
                }
            }
            catch (Exception ex)
            {
                AppConsole.Console_Byte(ex.Message);
            }
        }

        //Запись  в лог файл
        public static void WriteLog2(string str)
        {
            string writePath = "Log2.txt";

            try
            {
                using (StreamWriter sw = new StreamWriter(writePath, true, System.Text.Encoding.Default))
                {
                    sw.WriteLine("///////////////////////////////");
                    sw.WriteLine("///////////////////////////////");
                    sw.WriteLine(str);
                }
            }
            catch (Exception ex)
            {
                AppConsole.Console_Byte(ex.Message);
            }
        }
    }

    class AppConsole2
    {
        public static void Print_Console()
        {
            string str = "";

            str += "Размер файла:" + "\r" + "\n" + (Obmen.Lenght_Source / 1000000) + " MB" + "\r" + "\n";
            str += "Пройдено времени:" + "\r" + "\n" + (Obmen.Sw_Obmen.ElapsedMilliseconds)/1000 + " Секунд" + "\r" + "\n";
            str += "Объем занимаемой памяти:" + "\r" + "\n" + (Process.GetProcessesByName("ArchiverGT")[0].WorkingSet64 / 1024 / 1024).ToString() + " MB" + "\r" + "\n";

            StartForm.Console.Text = str;
        }
    }
}
