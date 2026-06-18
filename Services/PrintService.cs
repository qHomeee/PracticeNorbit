using System.Drawing;
using System.Drawing.Printing;

namespace AutoPrint.Services
{
    public class PrintService
    {
        private readonly string _printer;

        public PrintService(string printer)
        {
            _printer = printer;
        }

        public void PrintImage(string file)
        {
            PrintDocument pd = new();

            pd.PrinterSettings.PrinterName = _printer;

            pd.PrintPage += (s, e) =>
            {
                using var img = Image.FromFile(file);
                e.Graphics.DrawImage(img, e.MarginBounds);
            };

            pd.Print();
        }
    }
}