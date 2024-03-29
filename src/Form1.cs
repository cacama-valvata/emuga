using System.Text;

namespace emuga
{
    public partial class Form1 : Form
    {
        public SongInfo? Song = null;

        public Form1()
        {
            InitializeComponent();
        }

        private void OpenButton_Click(object sender, EventArgs e)
        {
            if (OpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                StatusLabel.Text = "MOD file loaded. Please wait while it is being processed...";
                Song = new SongInfo(OpenFileDialog.FileName);
                StatusLabel.Text = "MOD file loaded. Not playing.";
                SongLabel.Text = Song.SongName;
                FileLabel.Text = Path.GetFileName(OpenFileDialog.FileName);

                PlayButton.Enabled = true;

                for (int i = 0; i < tableLayoutPanel3.Controls.Count; i++)
                {
                    for (int j = 0; j < tableLayoutPanel3.Controls[i].Controls.Count; j++)
                    {
                        tableLayoutPanel3.Controls[i].Controls[j].Text = $"{i}:{j}";
                    }
                }
            }
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void PlayButton_Click(object sender, EventArgs e)
        {
            PauseButton.Enabled = true;
            StopButton.Enabled = true;
            RestartButton.Enabled = true;
            PlayButton.Enabled = false;

            StatusLabel.Text = "Playing MOD file...";
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            PauseButton.Enabled = false;
            PlayButton.Enabled = true;

            StatusLabel.Text = "Paused...";
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            PauseButton.Enabled = false;
            StopButton.Enabled = false;
            RestartButton.Enabled = false;
            PlayButton.Enabled = true;

            StatusLabel.Text = "MOD file loaded. Not playing.";
        }

        private void RestartButton_Click(object sender, EventArgs e)
        {
            PauseButton.Enabled = true;
            StopButton.Enabled = true;
            RestartButton.Enabled = true;
            PlayButton.Enabled = false;

            // Go back to the beginning and start playing
            StatusLabel.Text = "Playing MOD file...";
        }
    }

    public class SongInfo
    {
        public string SongName { get; set; }
        public int SongLength { get; set; }
        public int[] PatternPositions { get; set; }

        public int NumSamples { get; set; }     // maximum, will be either 15 or 31 for MOD; 1-indexed
        public Sample[] Samples { get; set; }

        public int NumPatterns { get; set; }
        public NotePerChannel[,,] Patterns { get; set; }    // channel, pattern, position

        public SongInfo(string filename)
        {
            // establish number of samples we can have:
            // seek to 1080, look for MOD magic bytes "M.K."
            using (FileStream lookforMK = File.OpenRead(filename))
            {
                byte[] buffer = new byte[4];
                lookforMK.Seek(1080, SeekOrigin.Begin);
                lookforMK.Read(buffer, 0, 4);
                NumSamples = String.Equals("M.K.", Encoding.UTF8.GetString(buffer)) ? 31 : 15;
            }

            // open file again, start populating information
            using (FileStream modfile = File.OpenRead(filename))
            {
                // the buffer we will be using to read all the data from the file
                byte[]? buffer = null;

                // Get songname
                buffer = new byte[20];
                modfile.Read(buffer, 0, 20);
                SongName = Encoding.UTF8.GetString(buffer[..Array.IndexOf(buffer, Convert.ToByte(0))]);

                // Get info of each sample
                Samples = new Sample[NumSamples + 1];
                for (int i = 1; i < NumSamples + 1; i++)
                {
                    buffer = new byte[30];
                    modfile.Read(buffer, 0, 30);
                    Samples[i] = new Sample(buffer);
                }

                // Get song-length in number of patterns
                SongLength = modfile.ReadByte();

                // One byte that set to 127
                _ = modfile.ReadByte();

                // Get pattern positions and number of different patterns
                buffer = new byte[128];
                modfile.Read(buffer, 0, 128);

                NumPatterns = 0;

                PatternPositions = new int[SongLength];
                for (int i = 0; i < SongLength; i++)
                {
                    PatternPositions[i] = buffer[i];
                    NumPatterns = Math.Max(NumPatterns, PatternPositions[i] + 1);
                }

                // M.K.
                if (NumSamples == 31)
                {
                    byte[] mk = new byte[4];
                    modfile.Read(mk, 0, 4);
                    if (Encoding.UTF8.GetString(mk) != "M.K.")
                    {
                        // It should never get here because we reached M.K. earlier, so it'd be in the same spot
                        Environment.Exit(1);
                    }
                }

                // Get pattern information
                Patterns = new NotePerChannel[4, NumPatterns, 64];
                for (int i = 0; i < NumPatterns; i++)
                {
                    buffer = new byte[1024];
                    modfile.Read(buffer, 0, 1024);
                    
                    for (int j = 0; j < 64; j++)
                    {
                        for (int k = 0; k < 4; k++)
                        {
                            byte[] notechannelbuffer = new byte[4];
                            Array.Copy(buffer, (16 * i) + (4 * j), notechannelbuffer, 0, 4);
                            Patterns[k, i, j] = new NotePerChannel(notechannelbuffer);
                        }
                    }
                }

                // Get the binary sample audio
                for (int i = 1; i < NumSamples + 1; i++)
                {
                    buffer = new byte[Samples[i].SampleLength * 2];
                    modfile.Read(buffer, 0, Samples[i].SampleLength * 2);
                    Samples[i].SampleAudio = buffer;
                }

                // End of file
            }
        }

        public string PrintFileResults()
        {
            string results = string.Empty;

            results += "SongName = " + SongName + Environment.NewLine;
            results += "SongLength = " + SongLength + " patterns" + Environment.NewLine;
            results += "NumSamples = " + NumSamples + Environment.NewLine;
            results += "NumPatterns = " + NumPatterns + Environment.NewLine;
            results += "PatternPositions = ";
            for (int i = 0; i < SongLength; i++)
            {
                results += PatternPositions[i] + " ";
            }
            results += Environment.NewLine + Environment.NewLine;

            for (int i = 1; i < 2 /*NumSamples + 1*/; i++)
            {
                results += $"Sample {i}:" + Environment.NewLine;
                results += "SampleName = " + Samples[i].SampleName + Environment.NewLine;
                results += "SampleLength = " + Samples[i].SampleLength + " words" + Environment.NewLine;
                results += "SampleFinetune = " + Samples[i].SampleFinetune + Environment.NewLine;
                results += "SampleVolume = " + Samples[i].SampleVolume + Environment.NewLine;
                results += "RepeatOffset = " + Samples[i].RepeatOffset + " words" + Environment.NewLine;
                results += "RepeatLength = " + Samples[i].RepeatLength + " words" + Environment.NewLine;
                results += Environment.NewLine;
            }

            for (int i = 0; i < 1 /*NumPatterns*/; i++) 
            {
                results += $"Pattern {i}:" + Environment.NewLine;
                for (int j = 0; j < 64; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        results += $"{Patterns[k, i, j].Pitch}-{Patterns[k, i, j].SampleNumber}-{Patterns[k, i, j].Effect[0]}\t";
                    }
                    results += Environment.NewLine;
                }
                results += Environment.NewLine;
            }

            return results;
        }
    }

    /* Component classes for SongInfo */

    public class Sample
    {
        public string SampleName { get; set; }
        public int SampleLength { get; set; }    // Stored as words, n * 2
        public int SampleFinetune { get; set; }  // -8..+7
        public int SampleVolume { get; set; }    // 0..64
        public int RepeatOffset { get; set; }    // Stored as words,  n * 2
        public int RepeatLength { get; set; }     // Stored as words, n * 2
        public byte[]? SampleAudio { get; set; }

        public Sample(byte[] buffer)
        {
            // Get sample name
            SampleName = Encoding.UTF8.GetString(buffer[..Array.IndexOf(buffer, Convert.ToByte(0))]);

            // Get numeric settings for the sample

            int idx_samplelen = 22; //2
            int idx_finetune = 24;  //1
            int idx_volume = 25;    //1
            int idx_repeat = 26;    //2
            int idx_replen = 28;    //2

            // Some values are two bytes; need to control for endianness
            // MOD files are big-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
                idx_samplelen = 30 - (idx_samplelen + 2);
                idx_finetune = 30 - (idx_finetune + 1);
                idx_volume = 30 - (idx_volume + 1);
                idx_repeat = 30 - (idx_repeat + 2);
                idx_replen = 30 - (idx_replen + 2);
            }

            // Extract data from buffer
            SampleLength = BitConverter.ToInt16(buffer, idx_samplelen);
            SampleFinetune = buffer[idx_finetune];
            SampleVolume = buffer[idx_volume];
            RepeatOffset = BitConverter.ToInt16(buffer, idx_repeat);
            RepeatLength = BitConverter.ToInt16(buffer, idx_replen);

            SampleAudio = null; // Is read and set later
        }
    }

    public class NotePerChannel
    {
        public int SampleNumber { get; set; }
        public int Pitch { get; set; }      // called the Period; with finetuning = 0
        public string PitchDisplay { get; set; }
        public int[] Effect { get; set; }    // length of array will always be 3

        public NotePerChannel(byte[] buffer)
        {
            // why would you split the singular byte that holds the sample information, mod???
            SampleNumber = (buffer[0] & 0xf0) | ((buffer[2] & 0xf0) >> 4);

            byte[] pitch_bytes = new byte[] { (byte)(buffer[0] & 0x0f), buffer[1] };
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(pitch_bytes);
            }
            Pitch = BitConverter.ToInt16(pitch_bytes);

            Effect = new int[3];
            Effect[0] = buffer[2] & 0x0f;
            Effect[1] = (buffer[3] & 0xf0) >> 4;
            Effect[2] = buffer[3] & 0x0f;

            PitchDisplay = TranslatePitch(Pitch);
        }

        static string TranslatePitch (int pitch)    // this would be best as a binary search
        {
            switch(pitch)
            {
                case 0:
                    return "   ";
                case <= 113:
                    return "B-3";
                case <= 120:
                    return "A#3";
                case <= 127:
                    return "A-3";
                case <= 135:
                    return "G#3";
                case <= 143:
                    return "G-3";
                case <= 151:
                    return "F#3";
                case <= 160:
                    return "F-3";
                case <= 170:
                    return "E-3";
                case <= 180:
                    return "D#3";
                case <= 190:
                    return "D-3";
                case <= 202:
                    return "C#3";
                case <= 214:
                    return "C-3";
                case <= 226:
                    return "B-2";
                case <= 240:
                    return "A#2";
                case <= 254:
                    return "A-2";
                case <= 269:
                    return "G#2";
                case <= 285:
                    return "G-2";
                case <= 302:
                    return "F#2";
                case <= 320:
                    return "F-2";
                case <= 339:
                    return "E-2";
                case <= 360:
                    return "D#2";
                case <= 381:
                    return "D-2";
                case <= 404:
                    return "C#2";
                case <= 428:
                    return "C-2";
                case <= 453:
                    return "B-1";
                case <= 480:
                    return "A#1";
                case <= 508:
                    return "A-1";
                case <= 538:
                    return "G#1";
                case <= 570:
                    return "G-1";
                case <= 604:
                    return "F#1";
                case <= 640:
                    return "F-1";
                case <= 678:
                    return "E-1";
                case <= 720:
                    return "D#1";
                case <= 762:
                    return "D-1";
                case <= 808:
                    return "C#1";
                case <= 856:
                    return "C-1";
                default:
                    return "   ";
            }
        }
    }
}