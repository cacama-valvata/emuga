namespace emuga
{
    public partial class Form1 : Form
    {
        public string? FileName = null;

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
            }
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}