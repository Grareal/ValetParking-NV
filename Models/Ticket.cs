using System;
using System.Drawing;
using System.Drawing.Printing;
using QRCoder;

namespace AppValetParking.Models
{
    //DISEÑO DEL TICKET
    public class Ticket
    {
        public string FOLIO { get; set; } = string.Empty;
        public string KEY { get; set; } = string.Empty;
        public string NAME { get; set; } = string.Empty;
        public string ROOM { get; set; } = string.Empty;
        public string TYPE { get; set; } = string.Empty;
        public string OBS { get; set; } = string.Empty;
        public string TROOM { get; set; } = string.Empty; // Tablet/Host
        public string PRINTERS { get; set; } = string.Empty; // puede ser "IMP1;IMP2"
        public string HOTEL { get; set; } = string.Empty;

        private Font font = new Font("Courier New", 10);

        public void Print()
        {
            if (string.IsNullOrWhiteSpace(PRINTERS))
                throw new Exception("No se ha especificado la impresora");

            // Si vienen múltiples impresoras separadas por ';'
            var printers = PRINTERS.Split(';').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (!printers.Any())
                throw new Exception("Lista de impresoras vacía");

            foreach (var printer in printers)
            {
                PrintToSinglePrinter(printer);
            }
        }

        private void PrintToSinglePrinter(string printerName)
        {
            PrintDocument pd = new PrintDocument();
            pd.DocumentName = "VALET " + this.FOLIO;

            pd.PrinterSettings.PrinterName = printerName;

            // ancho ticket 80mm
            pd.DefaultPageSettings.PaperSize = new PaperSize("Ticket", 315, 800);

            pd.PrintController = new StandardPrintController();
            pd.PrintPage += new PrintPageEventHandler(this.PrintPage);

            pd.Print();
            pd.Dispose();
        }

        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            float y = 10;
            float x = 5;
            float width = e.PageBounds.Width - 10;
            float lineHeight = font.GetHeight(e.Graphics) + 4;

            StringFormat center = new StringFormat() { Alignment = StringAlignment.Center };

            // Encabezado
            string titulo = $"*** {HOTEL} ***"; ;
            e.Graphics.DrawString(titulo.ToUpper(),
                new Font("Courier New", 12, FontStyle.Bold),
                Brushes.Black,
                new RectangleF(x, y, width, lineHeight),
                center);

            y += lineHeight + 4;
            e.Graphics.DrawString(DateTime.Now.ToString("dd/MM/yyyy hh:mm tt"), font, Brushes.Black, x, y);
            y += lineHeight;

            e.Graphics.DrawString($"RESERVA: {FOLIO}", font, Brushes.Black, x, y);
            y += lineHeight;

            e.Graphics.DrawString($"Nombre: {NAME}", font, Brushes.Black, x, y);
            y += lineHeight;

            e.Graphics.DrawString($"Habitación: {ROOM}", font, Brushes.Black, x, y);
            y += lineHeight;
            if (!string.IsNullOrWhiteSpace(TYPE))
            {
                e.Graphics.DrawString($"Tipo: {TYPE}", font, Brushes.Black, x, y);
                y += lineHeight;
            }


            y += 5;

            // Comentarios
            if (!string.IsNullOrWhiteSpace(OBS))
            {
                e.Graphics.DrawString("Comentarios:", font, Brushes.Black, x, y);
                y += lineHeight;

                int pos = 0;
                int chunk = 28;
                while (pos < OBS.Length)
                {
                    var part = OBS.Substring(pos, Math.Min(chunk, OBS.Length - pos));
                    e.Graphics.DrawString(part, font, Brushes.Black, x, y);
                    y += lineHeight;
                    pos += chunk;
                }
            }

            y += 5;

            // ----------------------------
            // 🔹 CÓDIGO QR CON EL FOLIO
            // ----------------------------
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(FOLIO, QRCodeGenerator.ECCLevel.Q);
                using (QRCode qrCode = new QRCode(qrCodeData))
                using (Bitmap qrBitmap = qrCode.GetGraphic(5))
                {
                    float qrSize = 120;
                    float qrX = (width - qrSize) / 2;
                    e.Graphics.DrawImage(qrBitmap, qrX, y, qrSize, qrSize);
                    y += qrSize + 10;
                }
            }

            

        }

    }
}