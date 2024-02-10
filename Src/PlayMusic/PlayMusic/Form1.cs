using System;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Drawing;
using System.ComponentModel;
using NAudio.Wave;
using CSCore.Streams;
using CSCore.SoundIn;
using CSCore;
using CSCore.DSP;
using WinformsVisualization.Visualization;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using NAudio.Extras;
using PlayMusic;
using System.Data;
using System.Text;
using NAudio.WaveFormRenderer;
using System.Drawing.Imaging;
using NAudio.Utils;
using System.Threading;

namespace PlayMusic
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private NAudio.Extras.Equalizer equalizer;
        private EqualizerBand[] bands;
        public int numBars = 20;
        public float[] barData = new float[20];
        public int minFreq = 1;
        public int maxFreq = 20000;
        public int barSpacing = 0;
        public bool logScale = true;
        public bool isAverage = false;
        public float highScaleAverage = 1.0f;
        public float highScaleNotAverage = 2.0f;
        public LineSpectrum lineSpectrum;
        public WasapiCapture capture;
        public FftSize fftSize;
        public float[] fftBuffer;
        public BasicSpectrumProvider spectrumProvider;
        public IWaveSource finalSource;
        public static string backgroundcolor = "";
        public static bool closed = false, setprogresssong = false;
        public static int size = 0;
        public static int width = 320, height = 180;
        public static Brush brush = (Brush)Brushes.MediumPurple;
        public static Image img;
        private static string song = "";
        private static MediaFoundationReader audioFileReader;
        private static IWavePlayer waveOutDevice;
        private static Dictionary<string, string[]> Playlist = new Dictionary<string, string[]>(30000);
        private static string play = "", music = "", listenplay = "", listenmusic = "", buttonpressed = "";
        private static int playinc = 0, musicinc = 0, listenplayinc = 0, listenmusicinc = 0, listinc;
        private static Random rnd = new Random();
        public static float volumeleft, volumeright;
        public static double totaltime, currenttime;
        public static List<int[]> list = new List<int[]>();
        public static Task taskspectrum;
        private static StandardWaveFormRendererSettings myRendererSettings = new StandardWaveFormRendererSettings();
        private static WaveFormRenderer renderer = new WaveFormRenderer();
        private static Image image;
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            using (System.IO.StreamWriter createdfile = new System.IO.StreamWriter("tempsave"))
            {
                createdfile.WriteLine(trackBar1.Value);
                createdfile.WriteLine(trackBar2.Value);
                createdfile.WriteLine(trackBar3.Value);
                createdfile.WriteLine(trackBar4.Value);
                createdfile.WriteLine(trackBar5.Value);
                createdfile.WriteLine(trackBar6.Value);
                createdfile.WriteLine(trackBar7.Value);
                createdfile.WriteLine(trackBar8.Value);
                createdfile.WriteLine(trackBar9.Value);
                createdfile.WriteLine(trackBar10.Value);
                createdfile.WriteLine(textBox2.Text);
                createdfile.WriteLine(textBox3.Text);
            }
            closed = true;
            Stop();
            Process.GetCurrentProcess().Kill();
        }
        private void Form1_Shown(object sender, EventArgs e)
        {
            Playlist.Add("Random", new string[1] { "" });
            string folderpath = System.Reflection.Assembly.GetEntryAssembly().Location.Replace(@"file:\", "").Replace(Process.GetCurrentProcess().ProcessName + ".exe", "").Replace(@"\", "/").Replace(@"//", "");
            string[] fileFolders = Directory.GetDirectories(folderpath);
            foreach (string fileFolder in fileFolders)
            {
                string folder = fileFolder.Replace(folderpath, "");
                string[] fileEntries = Directory.GetFiles(folderpath + folder + "/");
                Playlist.Add(folder, new string[fileEntries.Length / 2]);
                string[] songs = new string[fileEntries.Length / 2];
                int inc = 0;
                foreach (string fileEntrieImg in fileEntries)
                {
                    if (fileEntrieImg.EndsWith(".jpg"))
                    {
                        songs[inc] = fileEntrieImg.Replace(folderpath + folder + "/", "");
                        inc++;
                    }
                }
                Playlist[folder] = songs;
            }
            if (System.IO.File.Exists("tempsave"))
            {
                using (System.IO.StreamReader file = new System.IO.StreamReader("tempsave"))
                {
                    trackBar1.Value = Convert.ToInt32(file.ReadLine());
                    trackBar2.Value = Convert.ToInt32(file.ReadLine());
                    trackBar3.Value = Convert.ToInt32(file.ReadLine());
                    trackBar4.Value = Convert.ToInt32(file.ReadLine());
                    trackBar5.Value = Convert.ToInt32(file.ReadLine());
                    trackBar6.Value = Convert.ToInt32(file.ReadLine());
                    trackBar7.Value = Convert.ToInt32(file.ReadLine());
                    trackBar8.Value = Convert.ToInt32(file.ReadLine());
                    trackBar9.Value = Convert.ToInt32(file.ReadLine());
                    trackBar10.Value = Convert.ToInt32(file.ReadLine());
                    textBox2.Text = file.ReadLine();
                    textBox3.Text = file.ReadLine();
                }
            }
            this.textBox1.Text = DateTime.Now.ToString("HH:mm");
            img = Image.FromFile("pm.png");
            GetAudioByteArray();
            Task.Run(() => Start());
        }
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown(e.KeyData);
        }
        private void OnKeyDown(Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                const string message = "• Author: Michaël André Franiatte.\n\r\n\r• Contact: michael.franiatte@gmail.com.\n\r\n\r• Publisher: https://github.com/michaelandrefraniatte.\n\r\n\r• Copyrights: All rights reserved, no permissions granted.\n\r\n\r• License: Not open source, not free of charge to use.";
                const string caption = "About";
                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (keyData == Keys.Escape)
            {
                this.Close();
            }
        }
        private void SetEqualizer()
        {
            bands = new EqualizerBand[]
                    {
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 100, Gain = trackBar1.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 200, Gain = trackBar2.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 400, Gain = trackBar3.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 800, Gain = trackBar4.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 1200, Gain = trackBar5.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 2400, Gain = trackBar6.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 4800, Gain = trackBar7.Value},
                        new EqualizerBand {Bandwidth = 0.8f, Frequency = 9600, Gain = trackBar8.Value},
                    };
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (label1.Text == "Random")
            {
                checkBox1.Checked = true;
                checkBox2.Checked = true;
                checkBox3.Checked = false;
                ChooseRandom();
            }
            Play();
        }
        private void pictureBox1_Click(object sender, EventArgs e)
        { 
            if (playinc != 0 & !checkBox2.Checked)
            {
                checkBox1.Checked = true;
                checkBox2.Checked = false;
                checkBox3.Checked = false;
                ChooseMusicRandom();
            }
            else if (label1.Text == "Random" | checkBox2.Checked)
            {
                checkBox1.Checked = true;
                checkBox2.Checked = true;
                checkBox3.Checked = false;
                ChooseRandom();
            }
            Play();
        }
        private void WaveOutDevice_PlaybackStopped(object sender, NAudio.Wave.StoppedEventArgs e)
        {
            if (label1.Text == "Random")
            {
                checkBox1.Checked = true;
                checkBox2.Checked = true;
                checkBox3.Checked = false;
                ChooseRandom();
            }
            else if (checkBox1.Checked & checkBox2.Checked)
            {
                checkBox1.Checked = true;
                checkBox2.Checked = true;
                checkBox3.Checked = false;
                ChooseRandom();
            }
            else if (checkBox1.Checked & !checkBox2.Checked)
            {
                checkBox1.Checked = true;
                checkBox2.Checked = false;
                checkBox3.Checked = false;
                ChooseMusicRandom();
            }
            else if (checkBox3.Checked)
            {
                ChooseSame();
            }
            else
            {
                try
                {
                    buttonpressed = "play";
                    musicinc++;
                    string[] musics = Playlist[play];
                    if (musicinc >= musics.Length)
                    {
                        musicinc = 0;
                    }
                    ChangeContent();
                }
                catch { }
            }
            Play();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            waveOutDevice.Pause();
            buttonpressed = "pause";
        }
        private void button4_Click(object sender, EventArgs e)
        {
            Stop();
            buttonpressed = "stop";
        }
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                buttonpressed = "play";
                musicinc--;
                if (musicinc < 0)
                {
                    string[] musics = Playlist[play];
                    musicinc = musics.Length - 1;
                }
                ChangeContent();
            }
            catch { }
        }
        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                buttonpressed = "play";
                musicinc++;
                string[] musics = Playlist[play];
                if (musicinc >= musics.Length)
                {
                    musicinc = 0;
                }
                ChangeContent();
            }
            catch { }
        }
        private void button6_Click(object sender, EventArgs e)
        {
            playinc--;
            if (playinc < 1)
            {
                playinc = Playlist.Count - 1;
            }
            musicinc = 0;
            ChangeContent();
        }
        private void button7_Click(object sender, EventArgs e)
        {
            playinc++;
            if (playinc >= Playlist.Count)
            {
                playinc = 1;
            }
            musicinc = 0;
            ChangeContent();
        }
        private void ChangeContent()
        {
            try
            {
                if (checkBox2.Checked)
                {
                    playinc = rnd.Next(Playlist.Count - 1) + 1;
                    play = Playlist.ElementAt(playinc).Key;
                    string[] musics = Playlist[play];
                    musicinc = rnd.Next(musics.Length);
                    music = musics[musicinc];
                    SetContent();
                }
                if (checkBox1.Checked)
                {
                    play = Playlist.ElementAt(playinc).Key;
                    string[] musics = Playlist[play];
                    musicinc = rnd.Next(musics.Length);
                    music = musics[musicinc];
                    SetContent();
                }
                else
                {
                    play = Playlist.ElementAt(playinc).Key;
                    string[] musics = Playlist[play];
                    music = musics[musicinc];
                    SetContent();
                }
            }
            catch { }
        }
        private void SetContent()
        {
            this.label1.Text = play;
            this.label2.Text = music.Replace(".jpg", "");
            song = play + "/" + music.Replace(".jpg", ".mp3");
            img = Image.FromFile(play + "/" + music);
        }
        private void checkBox1_CheckStateChanged(object sender, EventArgs e)
        {
            if (!checkBox1.Checked)
            {
                checkBox2.Checked = false;
            }
            else
            {
                checkBox3.Checked = false;
            }
        }
        private void checkBox2_CheckStateChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                checkBox1.Checked = true;
                checkBox3.Checked = false;
            }
        }
        private void checkBox3_CheckStateChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked)
            {
                checkBox1.Checked = false;
                checkBox2.Checked = false;
            }
        }
        private void label1_Click(object sender, EventArgs e)
        {
            this.label1.Text = "Random";
            this.label2.Text = "";
            img = Image.FromFile("pm.png");
        }
        private void label2_Click(object sender, EventArgs e)
        {
            ChooseSame();
        }
        private void ChooseSame()
        {
            if (listenplayinc != 0)
            {
                playinc = listenplayinc;
                play = listenplay;
                musicinc = listenmusicinc;
                music = listenmusic;
                SetContent();
            }
        }
        private void ChooseRandom()
        {
            playinc = rnd.Next(Playlist.Count - 1) + 1;
            play = Playlist.ElementAt(playinc).Key;
            string[] musics = Playlist[play];
            musicinc = rnd.Next(musics.Length);
            music = musics[musicinc];
            SetContent();
        }
        private void ChooseMusicRandom()
        {
            string[] musics = Playlist[play];
            musicinc = rnd.Next(musics.Length);
            music = musics[musicinc];
            SetContent();
        }
        private void button8_Click(object sender, EventArgs e)
        {
            listinc++;
            if (listinc > list.Count - 1)
            {
                listinc = 0;
            }
            Rewind();
        }
        private void button9_Click(object sender, EventArgs e)
        {
            listinc--;
            if (listinc < 0)
            {
                listinc = list.Count - 1;
            }
            Rewind();
        }
        private void Rewind()
        {
            try
            {
                if (list.Count > 0)
                {
                    int[] inlist = list[listinc];
                    playinc = inlist[0];
                    musicinc = inlist[1];
                    play = Playlist.ElementAt(playinc).Key;
                    string[] musics = Playlist[play];
                    music = musics[musicinc];
                    SetContent();
                }
            }
            catch { }
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            DateTime hour = DateTime.Now;
            this.textBox1.Text = hour.ToString("HH:mm");
            if (hour.ToString("HH:mm") == this.textBox2.Text)
            {
                if (label1.Text == "Random")
                {
                    checkBox1.Checked = true;
                    checkBox2.Checked = true;
                    checkBox3.Checked = false;
                    ChooseRandom();
                }
                Play();
            }
            if (hour.ToString("HH:mm") == this.textBox3.Text)
            {
                Stop();
                buttonpressed = "stop";
            }
        }
        private void Play()
        {
            if (buttonpressed == "pause")
            {
                waveOutDevice.Play();
            }
            else
            {
                listenplayinc = playinc;
                listenmusicinc = musicinc;
                listenplay = play;
                listenmusic = music;
                list.Add(new int[] { playinc, musicinc });
                listinc = list.Count - 1;
                Stop();
                SetEqualizer();
                waveOutDevice = new NAudio.Wave.WaveOut();
                volumeleft = trackBar9.Value / 100f;
                volumeright = trackBar10.Value / 100f;
                audioFileReader = new MediaFoundationReader(song);
                VolumeStereoSampleProvider stereo = new VolumeStereoSampleProvider(audioFileReader.ToSampleProvider());
                equalizer = new NAudio.Extras.Equalizer(stereo, bands);
                waveOutDevice.Init(equalizer);
                waveOutDevice.Play();
                equalizer.Update();
                totaltime = audioFileReader.TotalTime.TotalSeconds;
                trackBar11.Value = 0;
                try
                {
                    waveOutDevice.PlaybackStopped -= WaveOutDevice_PlaybackStopped;
                }
                catch { }
                waveOutDevice.PlaybackStopped += WaveOutDevice_PlaybackStopped;
                try
                {
                    taskspectrum.Dispose();
                }
                catch { }
                taskspectrum = Task.Run(() => SetSpectrum());
            }
            buttonpressed = "play";
        }
        private void SetSpectrum()
        {
            MediaFoundationReader reader = new MediaFoundationReader(song);
            image = renderer.Render(reader, myRendererSettings);
            Bitmap bmp = image as Bitmap;
            bmp.MakeTransparent(Color.White);
            this.pictureBox2.BackgroundImage = bmp;
        }
        private void Stop()
        {
            try
            {
                waveOutDevice.Stop();
                audioFileReader.Dispose();
                waveOutDevice.Dispose();
                waveOutDevice.PlaybackStopped -= WaveOutDevice_PlaybackStopped;
            }
            catch { }
        }
        private void trackBar11_MouseDown(object sender, MouseEventArgs e)
        {
            setprogresssong = true;
        }
        private void trackBar11_MouseUp(object sender, MouseEventArgs e)
        {
            audioFileReader.CurrentTime = TimeSpan.FromSeconds(trackBar11.Value / 1000f * totaltime);
            setprogresssong = false;
        }
        public void GetAudioByteArray()
        {
            capture = new CSCore.SoundIn.WasapiLoopbackCapture();
            capture.Initialize();
            IWaveSource source = new SoundInSource(capture);
            fftSize = FftSize.Fft4096;
            fftBuffer = new float[(int)fftSize];
            spectrumProvider = new BasicSpectrumProvider(capture.WaveFormat.Channels, capture.WaveFormat.SampleRate, fftSize);
            lineSpectrum = new LineSpectrum(fftSize)
            {
                SpectrumProvider = spectrumProvider,
                UseAverage = true,
                BarCount = numBars,
                BarSpacing = 2,
                IsXLogScale = false,
                ScalingStrategy = ScalingStrategy.Sqrt
            };
            var notificationSource = new SingleBlockNotificationStream(source.ToSampleSource());
            notificationSource.SingleBlockRead += NotificationSource_SingleBlockRead;
            finalSource = notificationSource.ToWaveSource();
            capture.DataAvailable += Capture_DataAvailable;
            capture.Start();
        }
        public void Capture_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            finalSource.Read(e.Data, e.Offset, e.ByteCount);
        }
        public void NotificationSource_SingleBlockRead(object sender, SingleBlockReadEventArgs e)
        {
            spectrumProvider.Add(e.Left, e.Right);
        }
        public float[] GetFFtData()
        {
            lock (barData)
            {
                lineSpectrum.BarCount = numBars;
                if (numBars != barData.Length)
                {
                    barData = new float[numBars];
                }
            }
            if (spectrumProvider.IsNewDataAvailable)
            {
                lineSpectrum.MinimumFrequency = minFreq;
                lineSpectrum.MaximumFrequency = maxFreq;
                lineSpectrum.IsXLogScale = logScale;
                lineSpectrum.BarSpacing = barSpacing;
                lineSpectrum.SpectrumProvider.GetFftData(fftBuffer, this);
                return lineSpectrum.GetSpectrumPoints(100.0f, fftBuffer);
            }
            else
            {
                return null;
            }
        }
        public void ComputeData()
        {
            float[] resData = GetFFtData();
            int numBars = barData.Length;
            if (resData == null)
            {
                return;
            }
            lock (barData)
            {
                for (int i = 0; i < numBars && i < resData.Length; i++)
                {
                    barData[i] = resData[i] / 100.0f;
                }
                for (int i = 0; i < numBars && i < resData.Length; i++)
                {
                    if (lineSpectrum.UseAverage)
                    {
                        barData[i] = barData[i] + highScaleAverage * (float)Math.Sqrt(i / (numBars + 0.0f)) * barData[i];
                    }
                    else
                    {
                        barData[i] = barData[i] + highScaleNotAverage * (float)Math.Sqrt(i / (numBars + 0.0f)) * barData[i];
                    }
                }
            }
        }
        public void Start()
        {
            while (!closed)
            {
                if (this.WindowState != FormWindowState.Minimized)
                {
                    ComputeData();
                    Bitmap bmp = new Bitmap(img);
                    Graphics graphics = Graphics.FromImage(bmp as Image);
                    int bar1 = Convert.ToInt32(barData[0] * 100f);
                    int bar2 = Convert.ToInt32(barData[1] * 100f);
                    int bar3 = Convert.ToInt32(barData[2] * 100f);
                    int bar4 = Convert.ToInt32(barData[3] * 100f);
                    int bar5 = Convert.ToInt32(barData[4] * 100f);
                    int bar6 = Convert.ToInt32(barData[5] * 100f);
                    int bar7 = Convert.ToInt32(barData[6] * 100f);
                    int bar8 = Convert.ToInt32(barData[7] * 100f);
                    int bar9 = Convert.ToInt32(barData[8] * 100f);
                    int bar10 = Convert.ToInt32(barData[9] * 100f);
                    int bar11 = Convert.ToInt32(barData[10] * 100f);
                    int bar12 = Convert.ToInt32(barData[11] * 100f);
                    int bar13 = Convert.ToInt32(barData[12] * 100f);
                    int bar14 = Convert.ToInt32(barData[13] * 100f);
                    int bar15 = Convert.ToInt32(barData[14] * 100f);
                    int bar16 = Convert.ToInt32(barData[15] * 100f);
                    int bar17 = Convert.ToInt32(barData[16] * 100f);
                    int bar18 = Convert.ToInt32(barData[17] * 100f);
                    int bar19 = Convert.ToInt32(barData[18] * 100f);
                    int bar20 = Convert.ToInt32(barData[19] * 100f);
                    graphics.FillRectangle(brush, 0 * width / 20f + 0.5f, height - bar1, width / 20 - 1, bar1);
                    graphics.FillRectangle(brush, 1 * width / 20f + 0.5f, height - bar2, width / 20 - 1, bar2);
                    graphics.FillRectangle(brush, 2 * width / 20f + 0.5f, height - bar3, width / 20 - 1, bar3);
                    graphics.FillRectangle(brush, 3 * width / 20f + 0.5f, height - bar4, width / 20 - 1, bar4);
                    graphics.FillRectangle(brush, 4 * width / 20f + 0.5f, height - bar5, width / 20 - 1, bar5);
                    graphics.FillRectangle(brush, 5 * width / 20f + 0.5f, height - bar6, width / 20 - 1, bar6);
                    graphics.FillRectangle(brush, 6 * width / 20f + 0.5f, height - bar7, width / 20 - 1, bar7);
                    graphics.FillRectangle(brush, 7 * width / 20f + 0.5f, height - bar8, width / 20 - 1, bar8);
                    graphics.FillRectangle(brush, 8 * width / 20f + 0.5f, height - bar9, width / 20 - 1, bar9);
                    graphics.FillRectangle(brush, 9 * width / 20f + 0.5f, height - bar10, width / 20 - 1, bar10);
                    graphics.FillRectangle(brush, 10 * width / 20f + 0.5f, height - bar11, width / 20 - 1, bar11);
                    graphics.FillRectangle(brush, 11 * width / 20f + 0.5f, height - bar12, width / 20 - 1, bar12);
                    graphics.FillRectangle(brush, 12 * width / 20f + 0.5f, height - bar13, width / 20 - 1, bar13);
                    graphics.FillRectangle(brush, 13 * width / 20f + 0.5f, height - bar14, width / 20 - 1, bar14);
                    graphics.FillRectangle(brush, 14 * width / 20f + 0.5f, height - bar15, width / 20 - 1, bar15);
                    graphics.FillRectangle(brush, 15 * width / 20f + 0.5f, height - bar16, width / 20 - 1, bar16);
                    graphics.FillRectangle(brush, 16 * width / 20f + 0.5f, height - bar17, width / 20 - 1, bar17);
                    graphics.FillRectangle(brush, 17 * width / 20f + 0.5f, height - bar18, width / 20 - 1, bar18);
                    graphics.FillRectangle(brush, 18 * width / 20f + 0.5f, height - bar19, width / 20 - 1, bar19);
                    graphics.FillRectangle(brush, 19 * width / 20f + 0.5f, height - bar20, width / 20 - 1, bar20);
                    this.pictureBox1.BackgroundImage = bmp;
                    graphics.Dispose();
                    SetProgressSong();
                }
                System.Threading.Thread.Sleep(40);
            }
        }
        private void SetProgressSong()
        {
            try
            {
                if (!setprogresssong)
                {
                    currenttime = audioFileReader.CurrentTime.TotalSeconds;
                    trackBar11.Value = (int)(currenttime * 1000f / totaltime);
                }
            }
            catch { }
        }
    }
}
/// <summary>
/// Very simple sample provider supporting adjustable gain
/// </summary>
public class VolumeStereoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;

    /// <summary>
    /// Allows adjusting the volume left channel, 1.0f = full volume
    /// </summary>
    public float VolumeLeft { get; set; }

    /// <summary>
    /// Allows adjusting the volume right channel, 1.0f = full volume
    /// </summary>
    public float VolumeRight { get; set; }

    /// <summary>
    /// Initializes a new instance of VolumeStereoSampleProvider
    /// </summary>
    /// <param name="source">Source sample provider, must be stereo</param>
    public VolumeStereoSampleProvider(ISampleProvider source)
    {
        this.source = source;
        VolumeLeft = Form1.volumeleft;
        VolumeRight = Form1.volumeright;
    }

    /// <summary>
    /// WaveFormat
    /// </summary>
    public NAudio.Wave.WaveFormat WaveFormat => source.WaveFormat;

    /// <summary>
    /// Reads samples from this sample provider
    /// </summary>
    /// <param name="buffer">Sample buffer</param>
    /// <param name="offset">Offset into sample buffer</param>
    /// <param name="sampleCount">Number of samples desired</param>
    /// <returns>Number of samples read</returns>
    public int Read(float[] buffer, int offset, int sampleCount)
    {
        int samplesRead = source.Read(buffer, offset, sampleCount);

        for (int n = 0; n < sampleCount; n += 2)
        {
            buffer[offset + n] *= VolumeLeft;
            buffer[offset + n + 1] *= VolumeRight;
        }

        return samplesRead;
    }
}
namespace WinformsVisualization.Visualization
{
    /// <summary>
    ///     BasicSpectrumProvider
    /// </summary>
    public class BasicSpectrumProvider : FftProvider, ISpectrumProvider
    {
        public readonly int _sampleRate;
        public readonly List<object> _contexts = new List<object>();

        public BasicSpectrumProvider(int channels, int sampleRate, FftSize fftSize)
            : base(channels, fftSize)
        {
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException("sampleRate");
            _sampleRate = sampleRate;
        }

        public int GetFftBandIndex(float frequency)
        {
            int fftSize = (int)FftSize;
            double f = _sampleRate / 2.0;
            // ReSharper disable once PossibleLossOfFraction
            return (int)((frequency / f) * (fftSize / 2));
        }

        public bool GetFftData(float[] fftResultBuffer, object context)
        {
            if (_contexts.Contains(context))
                return false;

            _contexts.Add(context);
            GetFftData(fftResultBuffer);
            return true;
        }

        public override void Add(float[] samples, int count)
        {
            base.Add(samples, count);
            if (count > 0)
                _contexts.Clear();
        }

        public override void Add(float left, float right)
        {
            base.Add(left, right);
            _contexts.Clear();
        }
    }
}
namespace WinformsVisualization.Visualization
{
    public interface ISpectrumProvider
    {
        bool GetFftData(float[] fftBuffer, object context);
        int GetFftBandIndex(float frequency);
    }
}
namespace WinformsVisualization.Visualization
{
    internal class GradientCalculator
    {
        public Color[] _colors;

        public GradientCalculator()
        {
        }

        public GradientCalculator(params Color[] colors)
        {
            _colors = colors;
        }

        public Color[] Colors
        {
            get { return _colors ?? (_colors = new Color[] { }); }
            set { _colors = value; }
        }

        public Color GetColor(float perc)
        {
            if (_colors.Length > 1)
            {
                int index = Convert.ToInt32((_colors.Length - 1) * perc - 0.5f);
                float upperIntensity = (perc % (1f / (_colors.Length - 1))) * (_colors.Length - 1);
                if (index + 1 >= Colors.Length)
                    index = Colors.Length - 2;

                return Color.FromArgb(
                    255,
                    (byte)(_colors[index + 1].R * upperIntensity + _colors[index].R * (1f - upperIntensity)),
                    (byte)(_colors[index + 1].G * upperIntensity + _colors[index].G * (1f - upperIntensity)),
                    (byte)(_colors[index + 1].B * upperIntensity + _colors[index].B * (1f - upperIntensity)));
            }
            return _colors.FirstOrDefault();
        }
    }
}
namespace WinformsVisualization.Visualization
{
    public class LineSpectrum : SpectrumBase
    {
        public int _barCount;
        public double _barSpacing;
        public double _barWidth;
        public Size _currentSize;

        public LineSpectrum(FftSize fftSize)
        {
            FftSize = fftSize;
        }

        [Browsable(false)]
        public double BarWidth
        {
            get { return _barWidth; }
        }

        public double BarSpacing
        {
            get { return _barSpacing; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");
                _barSpacing = value;
                UpdateFrequencyMapping();

                RaisePropertyChanged("BarSpacing");
                RaisePropertyChanged("BarWidth");
            }
        }

        public int BarCount
        {
            get { return _barCount; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("value");
                _barCount = value;
                SpectrumResolution = value;
                UpdateFrequencyMapping();

                RaisePropertyChanged("BarCount");
                RaisePropertyChanged("BarWidth");
            }
        }

        [BrowsableAttribute(false)]
        public Size CurrentSize
        {
            get { return _currentSize; }
            set
            {
                _currentSize = value;
                RaisePropertyChanged("CurrentSize");
            }
        }

        public Bitmap CreateSpectrumLine(Size size, Brush brush, Color background, bool highQuality)
        {
            if (!UpdateFrequencyMappingIfNessesary(size))
                return null;

            var fftBuffer = new float[(int)FftSize];

            //get the fft result from the spectrum provider
            if (SpectrumProvider.GetFftData(fftBuffer, this))
            {
                using (var pen = new Pen(brush, (float)_barWidth))
                {
                    var bitmap = new Bitmap(size.Width, size.Height);

                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        PrepareGraphics(graphics, highQuality);
                        graphics.Clear(background);

                        CreateSpectrumLineInternal(graphics, pen, fftBuffer, size);
                    }

                    return bitmap;
                }
            }
            return null;
        }

        public Bitmap CreateSpectrumLine(Size size, Color color1, Color color2, Color background, bool highQuality)
        {
            if (!UpdateFrequencyMappingIfNessesary(size))
                return null;

            using (
                Brush brush = new LinearGradientBrush(new RectangleF(0, 0, (float)_barWidth, size.Height), color2,
                    color1, LinearGradientMode.Vertical))
            {
                return CreateSpectrumLine(size, brush, background, highQuality);
            }
        }

        public void CreateSpectrumLineInternal(Graphics graphics, Pen pen, float[] fftBuffer, Size size)
        {
            int height = size.Height;
            //prepare the fft result for rendering 
            SpectrumPointData[] spectrumPoints = CalculateSpectrumPoints(height, fftBuffer);

            //connect the calculated points with lines
            for (int i = 0; i < spectrumPoints.Length; i++)
            {
                SpectrumPointData p = spectrumPoints[i];
                int barIndex = p.SpectrumPointIndex;
                double xCoord = BarSpacing * (barIndex + 1) + (_barWidth * barIndex) + _barWidth / 2;

                var p1 = new PointF((float)xCoord, height);
                var p2 = new PointF((float)xCoord, height - (float)p.Value - 1);

                graphics.DrawLine(pen, p1, p2);
            }
        }

        public override void UpdateFrequencyMapping()
        {
            _barWidth = Math.Max(((_currentSize.Width - (BarSpacing * (BarCount + 1))) / BarCount), 0.00001);
            base.UpdateFrequencyMapping();
        }

        public bool UpdateFrequencyMappingIfNessesary(Size newSize)
        {
            if (newSize != CurrentSize)
            {
                CurrentSize = newSize;
                UpdateFrequencyMapping();
            }

            return newSize.Width > 0 && newSize.Height > 0;
        }

        public void PrepareGraphics(Graphics graphics, bool highQuality)
        {
            if (highQuality)
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.CompositingQuality = CompositingQuality.AssumeLinear;
                graphics.PixelOffsetMode = PixelOffsetMode.Default;
                graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            }
            else
            {
                graphics.SmoothingMode = SmoothingMode.HighSpeed;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.PixelOffsetMode = PixelOffsetMode.None;
                graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            }
        }
        public float[] GetSpectrumPoints(float height, float[] fftBuffer)
        {
            SpectrumPointData[] dats = CalculateSpectrumPoints(height, fftBuffer);
            float[] res = new float[dats.Length];
            for (int i = 0; i < dats.Length; i++)
            {
                res[i] = (float)dats[i].Value;
            }

            return res;
        }
    }
}
namespace WinformsVisualization.Visualization
{
    public class SpectrumBase : INotifyPropertyChanged
    {
        public const int ScaleFactorLinear = 9;
        public const int ScaleFactorSqr = 2;
        public const double MinDbValue = -90;
        public const double MaxDbValue = 0;
        public const double DbScale = (MaxDbValue - MinDbValue);

        public int _fftSize;
        public bool _isXLogScale;
        public int _maxFftIndex;
        public int _maximumFrequency = 20000;
        public int _maximumFrequencyIndex;
        public int _minimumFrequency = 20; //Default spectrum from 20Hz to 20kHz
        public int _minimumFrequencyIndex;
        public ScalingStrategy _scalingStrategy;
        public int[] _spectrumIndexMax;
        public int[] _spectrumLogScaleIndexMax;
        public ISpectrumProvider _spectrumProvider;

        public int SpectrumResolution;
        public bool _useAverage;

        public int MaximumFrequency
        {
            get { return _maximumFrequency; }
            set
            {
                if (value <= MinimumFrequency)
                {
                    throw new ArgumentOutOfRangeException("value",
                        "Value must not be less or equal the MinimumFrequency.");
                }
                _maximumFrequency = value;
                UpdateFrequencyMapping();

                RaisePropertyChanged("MaximumFrequency");
            }
        }

        public int MinimumFrequency
        {
            get { return _minimumFrequency; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");
                _minimumFrequency = value;
                UpdateFrequencyMapping();

                RaisePropertyChanged("MinimumFrequency");
            }
        }

        [BrowsableAttribute(false)]
        public ISpectrumProvider SpectrumProvider
        {
            get { return _spectrumProvider; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                _spectrumProvider = value;

                RaisePropertyChanged("SpectrumProvider");
            }
        }

        public bool IsXLogScale
        {
            get { return _isXLogScale; }
            set
            {
                _isXLogScale = value;
                UpdateFrequencyMapping();
                RaisePropertyChanged("IsXLogScale");
            }
        }

        public ScalingStrategy ScalingStrategy
        {
            get { return _scalingStrategy; }
            set
            {
                _scalingStrategy = value;
                RaisePropertyChanged("ScalingStrategy");
            }
        }

        public bool UseAverage
        {
            get { return _useAverage; }
            set
            {
                _useAverage = value;
                RaisePropertyChanged("UseAverage");
            }
        }

        [BrowsableAttribute(false)]
        public FftSize FftSize
        {
            get { return (FftSize)_fftSize; }
            set
            {
                if ((int)Math.Log((int)value, 2) % 1 != 0)
                    throw new ArgumentOutOfRangeException("value");

                _fftSize = (int)value;
                _maxFftIndex = _fftSize / 2 - 1;

                RaisePropertyChanged("FFTSize");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void UpdateFrequencyMapping()
        {
            _maximumFrequencyIndex = Math.Min(_spectrumProvider.GetFftBandIndex(MaximumFrequency) + 1, _maxFftIndex);
            _minimumFrequencyIndex = Math.Min(_spectrumProvider.GetFftBandIndex(MinimumFrequency), _maxFftIndex);

            int actualResolution = SpectrumResolution;

            int indexCount = _maximumFrequencyIndex - _minimumFrequencyIndex;
            double linearIndexBucketSize = Math.Round(indexCount / (double)actualResolution, 3);

            _spectrumIndexMax = _spectrumIndexMax.CheckBuffer(actualResolution, true);
            _spectrumLogScaleIndexMax = _spectrumLogScaleIndexMax.CheckBuffer(actualResolution, true);

            double maxLog = Math.Log(actualResolution, actualResolution);
            for (int i = 1; i < actualResolution; i++)
            {
                int logIndex =
                    (int)((maxLog - Math.Log((actualResolution + 1) - i, (actualResolution + 1))) * indexCount) +
                    _minimumFrequencyIndex;

                _spectrumIndexMax[i - 1] = _minimumFrequencyIndex + (int)(i * linearIndexBucketSize);
                _spectrumLogScaleIndexMax[i - 1] = logIndex;
            }

            if (actualResolution > 0)
            {
                _spectrumIndexMax[_spectrumIndexMax.Length - 1] =
                    _spectrumLogScaleIndexMax[_spectrumLogScaleIndexMax.Length - 1] = _maximumFrequencyIndex;
            }
        }

        public virtual SpectrumPointData[] CalculateSpectrumPoints(double maxValue, float[] fftBuffer)
        {
            var dataPoints = new List<SpectrumPointData>();

            double value0 = 0, value = 0;
            double lastValue = 0;
            double actualMaxValue = maxValue;
            int spectrumPointIndex = 0;

            for (int i = _minimumFrequencyIndex; i <= _maximumFrequencyIndex; i++)
            {
                switch (ScalingStrategy)
                {
                    case ScalingStrategy.Decibel:
                        value0 = (((20 * Math.Log10(fftBuffer[i])) - MinDbValue) / DbScale) * actualMaxValue;
                        break;
                    case ScalingStrategy.Linear:
                        value0 = (fftBuffer[i] * ScaleFactorLinear) * actualMaxValue;
                        break;
                    case ScalingStrategy.Sqrt:
                        value0 = ((Math.Sqrt(fftBuffer[i])) * ScaleFactorSqr) * actualMaxValue;
                        break;
                }

                bool recalc = true;

                value = Math.Max(0, Math.Max(value0, value));

                while (spectrumPointIndex <= _spectrumIndexMax.Length - 1 &&
                       i ==
                       (IsXLogScale
                           ? _spectrumLogScaleIndexMax[spectrumPointIndex]
                           : _spectrumIndexMax[spectrumPointIndex]))
                {
                    if (!recalc)
                        value = lastValue;

                    if (value > maxValue)
                        value = maxValue;

                    if (_useAverage && spectrumPointIndex > 0)
                        value = (lastValue + value) / 2.0;

                    dataPoints.Add(new SpectrumPointData { SpectrumPointIndex = spectrumPointIndex, Value = value });

                    lastValue = value;
                    value = 0.0;
                    spectrumPointIndex++;
                    recalc = false;
                }

                //value = 0;
            }

            return dataPoints.ToArray();
        }

        public void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null && !String.IsNullOrEmpty(propertyName))
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        [DebuggerDisplay("{Value}")]
        public struct SpectrumPointData
        {
            public int SpectrumPointIndex;
            public double Value;
        }
    }
}
namespace WinformsVisualization.Visualization
{
    public enum ScalingStrategy
    {
        Decibel,
        Linear,
        Sqrt
    }
}