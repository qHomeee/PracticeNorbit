using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using PdfiumViewer;

namespace AutoPrint.Services
{
    public class PrintService
    {
        private readonly string _printer;
        public string PrinterName => _printer;

        public PrintService(string printer)
        {
            _printer = printer;
        }

        public void PrintFile(string file)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException($"Файл не найден: {file}");

            string ext = Path.GetExtension(file).ToLowerInvariant();
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                    PrintImage(file);
                    break;
                case ".pdf":
                    PrintPdf(file);
                    break;
                case ".docx":
                    PrintDocx(file);
                    break;
                case ".xlsx":
                case ".xls":
                case ".xlsm":
                case ".xlsb":
                    PrintXlsx(file);
                    break;
                default:
                    throw new NotSupportedException($"Неподдерживаемый формат: {ext}");
            }
        }

        private void PrintImage(string file)
        {
            using var pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = _printer;
            string capturedFile = file;
            pd.PrintPage += (s, e) =>
            {
                using var img = Image.FromFile(capturedFile);
                if (e.Graphics != null)
                    e.Graphics.DrawImage(img, e.MarginBounds);
            };
            pd.Print();
        }

        private void PrintPdf(string file)
        {
            using var document = PdfDocument.Load(file);
            using var printDocument = document.CreatePrintDocument();
            printDocument.PrinterSettings.PrinterName = _printer;
            printDocument.DocumentName = System.IO.Path.GetFileName(file);
            printDocument.PrintController = new StandardPrintController();
            printDocument.Print();
        }

        private void PrintDocx(string file)
        {
            dynamic? wordApp = null;
            dynamic? doc = null;
            try
            {
                Type wordType = Type.GetTypeFromProgID("Word.Application")
                    ?? throw new InvalidOperationException("Microsoft Word не установлен.");

                wordApp = Activator.CreateInstance(wordType)
                    ?? throw new InvalidOperationException("Не удалось запустить Microsoft Word.");

                wordApp.Visible = false;
                wordApp.DisplayAlerts = 0;
                wordApp.ActivePrinter = _printer;
                doc = wordApp.Documents.Open(FileName: file, ReadOnly: true, AddToRecentFiles: false);
                doc.PrintOut(Background: false);

                int timeout = 60;
                while (wordApp.BackgroundPrintingStatus > 0 && timeout-- > 0)
                    System.Threading.Thread.Sleep(1000);
            }
            finally
            {
                SafeCloseDoc(ref doc);
                SafeQuitApp(ref wordApp);
            }
        }

        private void PrintXlsx(string file)
        {
            dynamic? excelApp = null;
            dynamic? wb = null;
            try
            {
                Type excelType = Type.GetTypeFromProgID("Excel.Application")
                    ?? throw new InvalidOperationException("Microsoft Excel не установлен.");

                excelApp = Activator.CreateInstance(excelType)
                    ?? throw new InvalidOperationException("Не удалось запустить Microsoft Excel.");

                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;
                excelApp.ActivePrinter = _printer;
                wb = excelApp.Workbooks.Open(file, ReadOnly: true);
                wb.PrintOut(ActivePrinter: _printer);
            }
            finally
            {
                SafeCloseWorkbook(ref wb);
                SafeQuitApp(ref excelApp);
            }
        }

        private static void SafeCloseDoc(ref dynamic? doc)
        {
            if (doc == null) return;
            try { doc.Close(SaveChanges: false); } catch { }
            finally { ReleaseComObject(doc); doc = null; }
        }

        private static void SafeCloseWorkbook(ref dynamic? wb)
        {
            if (wb == null) return;
            try { wb.Close(SaveChanges: false); } catch { }
            finally { ReleaseComObject(wb); wb = null; }
        }

        private static void SafeQuitApp(ref dynamic? app)
        {
            if (app == null) return;
            try { app.Quit(SaveChanges: false); } catch { }
            finally { ReleaseComObject(app); app = null; }
        }

        private static void ReleaseComObject(object? comObject)
        {
            if (comObject == null) return;
            try
            {
                if (Marshal.IsComObject(comObject))
                    Marshal.FinalReleaseComObject(comObject);
            }
            catch { }
        }
    }
}
