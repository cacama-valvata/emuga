using System.Text;

namespace emuga
{
    public partial class Form1 : Form
    {
        public string? FileName = null;
        public SongInfo? Song = null;

        public Form1()
        {
            InitializeComponent();
        }

        private void OpenButton_Click(object sender, EventArgs e)
        {
            if (OpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                FileName = OpenFileDialog.FileName;

                StatusLabel.Text = "MOD file loaded. Please wait while it is being processed...";
                Song = new SongInfo(FileName);

                StatusLabel.Text = Song.PrintFileResults();
            }
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            Close();
        }
    }

    public class SongInfo
    {
        public string SongName { get; set; }
        public int SongLength { get; set; }
        public int[] PatternPositions { get; set; }

        public int NumSamples { get; set; }     // maximum, will be either 15 or 31 for MOD
        public Sample[] Samples { get; set; }

        public int NumPatterns { get; set; }
        public Pattern[] Patterns { get; set; }

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
                Samples = new Sample[NumSamples];
                for (int i = 0; i < NumSamples; i++)    // REDO THIS
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
                    NumPatterns = Math.Max(NumPatterns, PatternPositions[i]);
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

                // continue
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

            for (int i = 0; i < NumSamples; i++)
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

    public class Pattern
    {
        public NotePerChannel[] Channel1 { get; set; }
        public NotePerChannel[] Channel2 { get; set; }
        public NotePerChannel[] Channel3 { get; set; }
        public NotePerChannel[] Channel4 { get; set; }
    }

    public class NotePerChannel
    {
        // why would you split the singular byte that holds the sample information, mod???
        public int SampleNumber { get; set; }
        public int Pitch { get; set; }      // called the Period; with finetuning = 0
        public int[] Effect { get; set; }    // length of array will always be 3

    }
}