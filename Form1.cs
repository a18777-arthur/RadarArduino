using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace RadarArduino
{
    public partial class Form1 : Form
    {
        private SerialPort serialPort;
        private readonly StringBuilder serialBuffer = new StringBuilder();
        private readonly int[] mapaDistancias = new int[181];

        private int anguloAtual = 0;
        private int distanciaAtual = 0;
        private const int AlcanceMaximoCm = 40; // Distância máxima exibida no radar (em cm)

        private ComboBox cmbPortas;
        private Button btnAtualizar;
        private Button btnLigar;
        private Label lblLeitura;
        private RadarPanel painelRadar;

        public Form1()
        {
            ConfigurarInterface();
            CarregarPortasCOM();
        }

        private void ConfigurarInterface()
        {
            Text = "Sonar Radar 180° - Arduino";
            Size = new Size(920, 580);
            MinimumSize = new Size(720, 460);
            BackColor = Color.FromArgb(12, 16, 14);
            StartPosition = FormStartPosition.CenterScreen;

            Panel barraTopo = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(20, 28, 24),
                Padding = new Padding(12, 10, 12, 10)
            };

            Label lblPorta = new Label
            {
                Text = "Porta COM:",
                ForeColor = Color.FromArgb(140, 255, 160),
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 17)
            };

            cmbPortas = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Consolas", 10F),
                Location = new Point(105, 13),
                Width = 110
            };

            btnAtualizar = new Button
            {
                Text = "↻",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(35, 50, 42),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(222, 12),
                Size = new Size(34, 28)
            };
            btnAtualizar.Click += (s, e) => CarregarPortasCOM();

            btnLigar = new Button
            {
                Text = "Ligar",
                Font = new Font("Consolas", 10F, FontStyle.Bold),
                ForeColor = Color.Black,
                BackColor = Color.FromArgb(50, 220, 100),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(266, 12),
                Size = new Size(105, 28)
            };
            btnLigar.Click += BtnLigar_Click;

            lblLeitura = new Label
            {
                Text = "Ângulo: ---°   |   Distância: --- cm",
                ForeColor = Color.FromArgb(80, 255, 120),
                Font = new Font("Consolas", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(400, 16)
            };

            barraTopo.Controls.AddRange(new Control[] { lblPorta, cmbPortas, btnAtualizar, btnLigar, lblLeitura });

            painelRadar = new RadarPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            painelRadar.Paint += PainelRadar_Paint;
            painelRadar.Resize += (s, e) => painelRadar.Invalidate();

            Controls.Add(painelRadar);
            Controls.Add(barraTopo);

            FormClosing += (s, e) => DesligarPorta();
        }

        private void CarregarPortasCOM()
        {
            cmbPortas.Items.Clear();
            string[] portas = SerialPort.GetPortNames();
            Array.Sort(portas);
            cmbPortas.Items.AddRange(portas);
            if (cmbPortas.Items.Count > 0)
                cmbPortas.SelectedIndex = 0;
        }

        private void BtnLigar_Click(object sender, EventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                DesligarPorta();
                return;
            }

            if (cmbPortas.SelectedItem == null)
            {
                MessageBox.Show("Nenhuma porta COM encontrada. Liga o Arduino por USB e clica em ↻.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                serialPort = new SerialPort(cmbPortas.SelectedItem.ToString(), 9600);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();

                btnLigar.Text = "Desligar";
                btnLigar.BackColor = Color.FromArgb(230, 75, 75);
                btnLigar.ForeColor = Color.White;
                cmbPortas.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao abrir a porta série: " + ex.Message,
                    "Erro de Ligação", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DesligarPorta()
        {
            if (serialPort != null)
            {
                try
                {
                    if (serialPort.IsOpen)
                        serialPort.Close();
                    serialPort.Dispose();
                }
                catch { }
                serialPort = null;
            }

            btnLigar.Text = "Ligar";
            btnLigar.BackColor = Color.FromArgb(50, 220, 100);
            btnLigar.ForeColor = Color.Black;
            cmbPortas.Enabled = true;
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string dados = serialPort.ReadExisting();
                serialBuffer.Append(dados);

                string conteudo = serialBuffer.ToString();
                int indicePonto;

                // O Arduino envia blocos terminados em '.' no formato "angulo,distancia."
                while ((indicePonto = conteudo.IndexOf('.')) >= 0)
                {
                    string pacote = conteudo.Substring(0, indicePonto).Trim();
                    conteudo = conteudo.Substring(indicePonto + 1);

                    string[] partes = pacote.Split(',');
                    if (partes.Length == 2 &&
                        int.TryParse(partes[0], out int angulo) &&
                        int.TryParse(partes[1], out int distancia))
                    {
                        if (angulo >= 0 && angulo <= 180)
                        {
                            anguloAtual = angulo;
                            distanciaAtual = distancia;
                            mapaDistancias[angulo] = distancia;

                            BeginInvoke(new Action(() =>
                            {
                                string textoDist = (distancia > 0 && distancia <= AlcanceMaximoCm)
                                    ? $"{distancia} cm"
                                    : "Fora de alcance";

                                lblLeitura.Text = $"Ângulo: {anguloAtual,3}°   |   Distância: {textoDist}";
                                painelRadar.Invalidate();
                            }));
                        }
                    }
                }

                serialBuffer.Clear();
                serialBuffer.Append(conteudo);
            }
            catch { }
        }

        private void PainelRadar_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = painelRadar.ClientSize.Width;
            int h = painelRadar.ClientSize.Height;

            int cx = w / 2;
            int cy = h - 38;
            int raioMax = Math.Min((w / 2) - 40, h - 75);

            if (raioMax <= 20) return;

            using (Pen penGrelha = new Pen(Color.FromArgb(35, 165, 65), 1.5f))
            using (Pen penEixo = new Pen(Color.FromArgb(25, 115, 45), 1f))
            using (Font fonteEscala = new Font("Consolas", 9F))
            using (Brush brushTexto = new SolidBrush(Color.FromArgb(90, 230, 120)))
            {
                // 1. Arcos concêntricos de distância (10, 20, 30, 40 cm)
                int divisoes = 4;
                for (int i = 1; i <= divisoes; i++)
                {
                    int r = (raioMax * i) / divisoes;
                    g.DrawArc(penGrelha, cx - r, cy - r, r * 2, r * 2, 180, 180);

                    int cm = (AlcanceMaximoCm * i) / divisoes;
                    g.DrawString($"{cm}cm", fonteEscala, brushTexto, cx + r - 28, cy + 6);
                }

                // 2. Linhas radiais de 30° em 30°
                for (int a = 0; a <= 180; a += 30)
                {
                    double rad = a * Math.PI / 180.0;
                    int x = cx + (int)(raioMax * Math.Cos(rad));
                    int y = cy - (int)(raioMax * Math.Sin(rad));
                    g.DrawLine(penEixo, cx, cy, x, y);

                    int lx = cx + (int)((raioMax + 18) * Math.Cos(rad)) - 14;
                    int ly = cy - (int)((raioMax + 18) * Math.Sin(rad)) - 8;
                    g.DrawString($"{a}°", fonteEscala, brushTexto, lx, ly);
                }

                // 3. Pontos de obstáculos detetados ao longo da varredura
                for (int a = 0; a <= 180; a++)
                {
                    int d = mapaDistancias[a];
                    if (d > 0 && d <= AlcanceMaximoCm)
                    {
                        double rad = a * Math.PI / 180.0;
                        double rObj = (d / (double)AlcanceMaximoCm) * raioMax;
                        int ox = cx + (int)(rObj * Math.Cos(rad));
                        int oy = cy - (int)(rObj * Math.Sin(rad));

                        // Destaca mais forte perto do feixe atual
                        int diferenca = Math.Abs(a - anguloAtual);
                        int alpha = Math.Max(70, 255 - (diferenca * 3));

                        using (Brush brushObj = new SolidBrush(Color.FromArgb(alpha, 255, 55, 55)))
                        {
                            g.FillEllipse(brushObj, ox - 4, oy - 4, 8, 8);
                        }
                    }
                }

                // 4. Feixe principal do sonar no ângulo atual
                double radAtual = anguloAtual * Math.PI / 180.0;
                int fx = cx + (int)(raioMax * Math.Cos(radAtual));
                int fy = cy - (int)(raioMax * Math.Sin(radAtual));

                using (Pen penFeixe = new Pen(Color.FromArgb(110, 255, 140), 2.5f))
                {
                    g.DrawLine(penFeixe, cx, cy, fx, fy);
                }
            }
        }
    }

    // Painel com DoubleBuffered ativado para animação suave sem piscar
    public class RadarPanel : Panel
    {
        public RadarPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}