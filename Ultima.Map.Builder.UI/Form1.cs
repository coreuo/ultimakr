using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Ultima.Map.Builder.UI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var mlPath = GetMLPath();

            if (mlPath != null)
            {
                textBox1.Text = $"{mlPath}\\map0.mul";
                textBox2.Text = $"{mlPath}\\staidx0.mul";
                textBox3.Text = $"{mlPath}\\statics0.mul";
                textBox4.Text = $"{mlPath}\\radarcol.mul";

                openFileDialog1.InitialDirectory = mlPath;
                openFileDialog2.InitialDirectory = mlPath;
                openFileDialog3.InitialDirectory = mlPath;
                openFileDialog4.InitialDirectory = mlPath;
            }

            var krPath = GetKRPath();

            if (krPath != null)
            {
                textBox5.Text = $"{krPath}\\facet1.uop";

                openFileDialog5.InitialDirectory = krPath;
            }
        }

        private string GetMLPath()
        {
            var key = GetMLX86Key();

            if (key == null)
                key = GetMLX64Key();

            if (key == null)
                return null;

            var value = key.GetValue("InstallLocation");

            if (value == null || !(value is string))
                return null;

            var path = value as string;

            return path;
        }

        private string GetKRPath()
        {
            var key = GetKRX86Key();

            if (key == null)
                key = GetKRX64Key();

            if (key == null)
                return null;

            var value = key.GetValue("InstallLocation");

            if (value == null || !(value is string))
                return null;

            var path = value as string;

            return path;
        }

        private RegistryKey GetMLX86Key()
        {
            try
            {
                return Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{DF7B213D-2065-41ED-BB51-7A3EED31EA7B}");
            }
            catch
            {
                return null;
            }
        }

        private RegistryKey GetMLX64Key()
        {
            try
            {
                return Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{DF7B213D-2065-41ED-BB51-7A3EED31EA7B}");
            }
            catch
            {
                return null;
            }
        }

        private RegistryKey GetKRX86Key()
        {
            try
            {
                return Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{4771CB1E-1653-4860-B095-F4D80AD923DB}");
            }
            catch
            {
                return null;
            }
        }

        private RegistryKey GetKRX64Key()
        {
            try
            {
                return Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{4771CB1E-1653-4860-B095-F4D80AD923DB}");
            }
            catch
            {
                return null;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = openFileDialog1.FileName;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (openFileDialog2.ShowDialog() == DialogResult.OK)
            {
                textBox2.Text = openFileDialog2.FileName;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (openFileDialog3.ShowDialog() == DialogResult.OK)
            {
                textBox3.Text = openFileDialog3.FileName;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (openFileDialog4.ShowDialog() == DialogResult.OK)
            {
                textBox4.Text = openFileDialog4.FileName;
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (openFileDialog5.ShowDialog() == DialogResult.OK)
            {
                textBox5.Text = openFileDialog5.FileName;
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            this.groupBox1.Enabled = false;

            this.groupBox2.Enabled = false;

            this.groupBox3.Enabled = false;

            this.Update();

            new Thread(() =>
            {
                var stopWatch = new Stopwatch();

                stopWatch.Start();

                try
                {
                    if (!File.Exists(this.textBox1.Text)) throw new FileNotFoundException("Please select valid map*.mul file.");
                    if (!File.Exists(this.textBox2.Text)) throw new FileNotFoundException("Please select valid staidx*.mul file.");
                    if (!File.Exists(this.textBox3.Text)) throw new FileNotFoundException("Please select valid statics*.mul file.");
                    if (!File.Exists(this.textBox4.Text)) throw new FileNotFoundException("Please select valid radarcol*.mul file.");
                    if (!File.Exists(this.textBox5.Text)) throw new FileNotFoundException("Please select valid facet*.uop file.");

                    UpdateProgress("Loading KR map...", 0);

                    var temp = Path.GetTempPath() + Guid.NewGuid() + ".uop";

                    File.Copy(this.textBox5.Text, temp);

                    File.Delete(this.textBox5.Text);

                    using var inputStream = File.OpenRead(temp);

                    using var reader = new BinaryReader(inputStream);

                    using var outputStream = File.OpenWrite(this.textBox5.Text);

                    using var writer = new BinaryWriter(outputStream);

                    UltimaMap.Import(reader, writer, this.textBox1.Text, this.textBox2.Text, this.textBox3.Text, this.textBox4.Text, this.checkBox1.Checked, p => UpdateProgress("Converting map...", p));

                    stopWatch.Stop();

                    DoneMessage(stopWatch.Elapsed);
                }
                catch (Exception exception)
                {
                    File.WriteAllText("exception.txt", exception.ToString());

                    ErrorMessage(exception);
                }

            }).Start();

            this.Update();
        }

        private delegate void UpdateProgressCallback(string status, int progress);

        private void UpdateProgress(string status, int progress)
        {
            var callback = new UpdateProgressCallback((s, p) =>
            {
                label6.Text = s;

                progressBar1.Value = p;

                Update();
            });

            Invoke(callback, status, progress);
        }

        private delegate void ErrorCallback(Exception e);

        private void ErrorMessage(Exception e)
        {
            var callback = new ErrorCallback(e =>
            {
                MessageBox.Show(e.ToString(), "Exception thrown", MessageBoxButtons.OK, MessageBoxIcon.Error);

                this.groupBox1.Enabled = true;

                this.groupBox2.Enabled = true;

                this.groupBox3.Enabled = true;

                progressBar1.Value = 0;

                this.label6.Text = "Ready to convert.";
            });

            Invoke(callback, e);
        }

        private delegate void DoneCallback(TimeSpan timeSpan);

        private void DoneMessage(TimeSpan timeSpan)
        {
            var callback = new DoneCallback(t =>
            {
                MessageBox.Show($"It took {t} to complete.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.groupBox1.Enabled = true;

                this.groupBox2.Enabled = true;

                this.groupBox3.Enabled = true;

                progressBar1.Value = 0;

                this.label6.Text = "Ready to convert.";
            });

            Invoke(callback, timeSpan);
        }
    }
}
