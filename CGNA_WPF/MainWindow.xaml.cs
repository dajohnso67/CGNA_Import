using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace CGNA_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        List<CsvColumns> modifiedRecords { get; set; }
        string saveFileName { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            searchText.Text = "Search Inventory...";
            searchText.GotFocus += SearchText_GotFocus;
            searchText.LostFocus += SearchText_LostFocus;
            Rules.RulesConfig = JObject.Parse(File.ReadAllText("RulesConfiguration.json"));
            modifiedRecords = new List<CsvColumns>();
        }

        public void SearchText_GotFocus(object sender, EventArgs e)
        {
            if (searchText.Text == "Search Inventory...") {
                searchText.Text = string.Empty;
            }
        }

        public void SearchText_LostFocus(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(searchText.Text.Trim())) {
                searchText.Text = "Search Inventory...";
            }
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true) {

                string inputFile = File.ReadAllText(openFileDialog.FileName);
                using (TextReader reader = new StringReader(inputFile))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        csv.Configuration.IgnoreBlankLines = true;
                        csv.Configuration.IgnoreQuotes = false;
                        csv.Configuration.BadDataFound = x =>
                        {
                            x.Record = null;
                        };

                        for(var i = 0; i < 8; i++) 
                        {
                            csv.Read();
                        }

                        csv.Configuration.RegisterClassMap<GTHMap>();
                        List<CsvColumns> csvFile = csv.GetRecords<CsvColumns>().ToList();
                        DataContext = ApplyRules(csvFile);
                    }
                }
            }
        }

        private List<CsvColumns> ApplyRules(List<CsvColumns> csvFile) {

            List<string> deleted = new List<string>();

            foreach (var item in csvFile)
            {
                if (item.PartNumber.EndsWith("-"))
                {
                    item.PartNumber = item.PartNumber.Trim() + item.Description.Trim();
                    item.Description = string.Empty;
                }

                foreach (var contain in Rules.Contains)
                {

                    if (item.PartNumber.ToLower().Contains(contain.ToLower()))
                    {
                        deleted.Add(item.PartNumber);
                    }
                }

                foreach (var starts in Rules.StartsWith)
                {
                    if (item.PartNumber.ToLower().StartsWith(starts.ToLower()))
                    {
                        deleted.Add(item.PartNumber);
                    }
                }

                foreach (KeyValuePair<string, double> kv in Rules.CustomPrice)
                {
                    if (item.PartNumber.ToLower().Contains(kv.Key.ToLower()))
                    {
                        item.Price = kv.Value;
                    }
                }
            }

            foreach (var d in deleted)
            {
                csvFile.RemoveAll(c => c.PartNumber.ToLower() == d.ToLower());
            }

            csvFile = csvFile.OrderBy(c => c.PartNumber).Select(v => v).ToList();

            return csvFile;
        }

        private void btnSaveFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV file (*.csv)|*.csv";
            saveFileDialog.DefaultExt = ".csv";

            if (saveFileDialog.ShowDialog() == true) {

                saveFileName = saveFileDialog.FileName;

                var _directory = Path.GetDirectoryName(saveFileName);

                using (var sw = new StreamWriter(saveFileName))
                {
                    var writer = new CsvWriter(sw, CultureInfo.InvariantCulture);
                    List<CsvColumns> list = DataContext as List<CsvColumns>;
                    writer.WriteRecords(list);
                }

                if (modifiedRecords.Count() > 0) {

                    var fileMonth = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(DateTime.Now.Month).ToUpper();

                    using (var sw = new StreamWriter($"{_directory}\\ChangeLog_{fileMonth}_{DateTime.Now.Day}_{DateTime.Now.Year}.csv"))
                    {
                        var writer = new CsvWriter(sw, CultureInfo.InvariantCulture);
                        writer.WriteRecords(modifiedRecords);
                    }
                }
            }
        }

        private void btnSendFile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(saveFileName) && Path.GetExtension(saveFileName).ToLower().Contains("csv"))
            {

                MessageBoxResult messageBoxResult = MessageBox.Show($"Please confirm that you are ready to send this file {saveFileName}", "Confirm FTP Upload", MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    try
                    {
                        string userName = "gth";
                        string password = "gthCGNA123";
                        string Uri = "ftp://216.14.34.228/GTH.csv";

                        FtpWebRequest request = (FtpWebRequest)WebRequest.Create(Uri);
                        request.Credentials = new NetworkCredential(userName, password);
                        request.Method = WebRequestMethods.Ftp.UploadFile;

                        using Stream fileStream = File.OpenRead(saveFileName);
                        using Stream ftpStream = request.GetRequestStream();
                        fileStream.CopyTo(ftpStream);

                        MessageBox.Show($"{saveFileName} has been sent.", "File Upload Complete");
                    }
                    catch (Exception ex) {
                        MessageBox.Show(ex.Message, "File Upload Error");
                    }
                    
                }
            }
            else {
                MessageBox.Show("No file has been saved", "File Upload Error");
            }
        }

        private void myDataGridView_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void myDataGridView_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
        }

        private void myDataGridView_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {

            var _row = (CsvColumns)e.Row.Item;
            bool hasChange = false;

            switch (e.Column.SortMemberPath) {

                case "PartNumber":
                    if (_row.PartNumber.ToLower() != ((TextBox)e.EditingElement).Text.ToLower()) {
                        _row.PartNumber = ((TextBox)e.EditingElement).Text;
                        hasChange = true;
                    }
                        
                    break;
                case "Description":
                    if (_row.Description.ToLower() != ((TextBox)e.EditingElement).Text.ToLower()) {
                        _row.Description = ((TextBox)e.EditingElement).Text;
                        hasChange = true;
                    }
                        
                    break;
                case "OnHand":
                    if (_row.OnHand != Convert.ToInt32(((TextBox)e.EditingElement).Text)) {
                        _row.OnHand = Convert.ToInt32(((TextBox)e.EditingElement).Text);
                        hasChange = true;
                    }
                        
                    break;
                case "Price":
                    if (_row.Price != Convert.ToDouble(((TextBox)e.EditingElement).Text)) {
                        _row.Price = Convert.ToDouble(((TextBox)e.EditingElement).Text);
                        hasChange = true;
                    }
                        
                    break;
            }

            if (hasChange) {
                modifiedRecords.Add(_row);
            }
            
        }

        private void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext != null) {

                List<CsvColumns> _context = DataContext as List<CsvColumns>;
                var filtered = _context.Where(p => 
                    p.PartNumber.ToLower().Contains(((TextBox)sender).Text.ToLower()) || 
                    p.Description.ToLower().Contains(((TextBox)sender).Text.ToLower())
                    ).ToList();

                myDataGridView.ItemsSource = filtered;
            }
        }
    }

    public class CsvColumns
    {
        [CsvHelper.Configuration.Attributes.Name("PART #")]
        public string PartNumber { get; set; }

        [CsvHelper.Configuration.Attributes.Name("DESCRIPTION")]
        public string Description { get; set; }

        [CsvHelper.Configuration.Attributes.Name("ONHAND")]
        public int? OnHand { get; set; }

        [CsvHelper.Configuration.Attributes.Name("PRICE")]
        public double Price { get; set; }
    }

    public class GTHMap : ClassMap<CsvColumns>
    {
        public GTHMap()
        {
            Map(m => m.PartNumber).Index(0);
            Map(m => m.Description).Index(1);
            Map(m => m.OnHand).Index(2);
            Map(m => m.Price).Index(4);
        }
    }


    public static class Rules
    {
        public static JObject RulesConfig;

        public static Dictionary<string, double> CustomPrice {

            get {

                var Items = RulesConfig["CustomPrice"].ToObject<Dictionary<string, double>>();
                return Items;
            }
        }

        public static List<string> StartsWith {

            get
            {
                var Items = RulesConfig["StartsWith"].ToObject<List<string>>();
                return Items;
            }
        }

        public static List<string> Contains {

            get
            {
                var Items = RulesConfig["Contains"].ToObject<List<string>>();
                return Items;
            }
        }
    }
}
