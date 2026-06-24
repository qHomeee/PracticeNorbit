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
                    throw new NotSupportedException($"Unsupported file type: {ext}");
            }
        }

        private void PrintImage(string file)
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

        private void PrintPdf(string file)
        {
            using var document = PdfDocument.Load(file);
            using var printDocument = document.CreatePrintDocument();
            printDocument.PrinterSettings.PrinterName = _printer;
            printDocument.DocumentName = Path.GetFileName(file);
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
                    ?? throw new InvalidOperationException("Microsoft Word не установлен или COM-регистрация Word недоступна.");

                wordApp = Activator.CreateInstance(wordType)
                    ?? throw new InvalidOperationException("Не удалось запустить Microsoft Word.");

                wordApp.Visible = true;
                wordApp.DisplayAlerts = 0;
                wordApp.ActivePrinter = _printer;
                doc = wordApp.Documents.Open(FileName: file, ReadOnly: true, AddToRecentFiles: false);
                doc.Activate();
                wordApp.Activate();
                wordApp.ActiveDocument.PrintOut(Background: false);
            }
            finally
            {
                if (doc != null)
                {
                    try
                    {
                        doc.Close(SaveChanges: false);
                    }
                    finally
                    {
                        ReleaseComObject(doc);
                    }
                }

                if (wordApp != null)
                {
                    try
                    {
                        wordApp.Quit(SaveChanges: false);
                    }
                    finally
                    {
                        ReleaseComObject(wordApp);
                    }
                }
            }
        }

        private void PrintXlsx(string file)
        {
            dynamic? excelApp = null;
            dynamic? wb = null;
            try
            {
                Type excelType = Type.GetTypeFromProgID("Excel.Application")
                    ?? throw new InvalidOperationException("Microsoft Excel не установлен или COM-регистрация Excel недоступна.");

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
                if (wb != null)
                {
                    try
                    {
                        wb.Close(SaveChanges: false);
                    }
                    finally
                    {
                        ReleaseComObject(wb);
                    }
                }

                if (excelApp != null)
                {
                    try
                    {
                        excelApp.Quit();
                    }
                    finally
                    {
                        ReleaseComObject(excelApp);
                    }
                }
            }
        }

        private static void ReleaseComObject(object? comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
                Marshal.FinalReleaseComObject(comObject);
        }
    }
}
