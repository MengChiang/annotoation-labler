using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.Configuration;
using Annotation.Dtos;
using System.Windows.Media;
using Annotation.Models;
using System.Globalization;
using CsvHelper;
using System.Windows.Input;
using Microsoft.VisualBasic.FileIO;

namespace Annotation
{
    public partial class MainWindow : Window
    {
        private readonly AppSettings _appSettings;
        private LabelProcessor _labelProcessor;

        private string _tsvFolderPath;
        private string _previousAnnotationPath;

        public MainWindow()
        {
            _appSettings = GetAppSettings();

            var labelOptions = GetLabelOptions();

            InitializeComponent();

            InitializeFileListBox();

            InitializeLabelButtons(labelOptions);

            _labelProcessor = GetLabelProcessor(labelOptions);
        }


        /// <summary>
        /// 載入Tsv原始資料的按鈕事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TsvLoadBtn_Click(object sender, RoutedEventArgs e)
        {
            //ClearAllStatuses();

            var openFileDialog = new OpenFileDialog
            {
                Filter = "TSV files (*.tsv)|*.tsv|CSV files (*.csv)|*.csv",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                if (openFileDialog.FileNames.Length > 0)
                {
                    textArea.Text = "";
                }

                LoadFileListBoxItems(openFileDialog.FileNames);
            }
        }

        /// <summary>
        /// 載入標記資料的按鈕事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AnnotationLoadBtn_Click(object sender, RoutedEventArgs e)
        {
            var annotationLabels = _labelProcessor.OutputAnnotationLabels();
            if (annotationLabels != string.Empty)
            {
                var result = MessageBox.Show("Current annotation data will be removed. Continue?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }
            var openFileDialog = new OpenFileDialog
            {
                Filter = "csv file (*.csv)|*.csv",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _labelProcessor.ClearUserData();
                _previousAnnotationPath = openFileDialog.FileName;

                var labelAnnotations = GetPreviousLabelAnnotations(_previousAnnotationPath);

                //只載入存於FileListBox檔案的標記資料
                foreach (var item in FileListBox.Items)
                {
                    var fileListItem = (item as FileListItem)!;
                    if (!labelAnnotations.Any(label => label.Id == fileListItem.Name))
                    {
                        labelAnnotations.RemoveAll(label => label.Id == fileListItem.Name);
                    }
                }

                _labelProcessor.LoadLabels(labelAnnotations);

                //顯示當下被選擇該筆的資料
                var selectedItemName = GetCurrentSelectedItemName();
                if (selectedItemName != string.Empty)
                {
                    var labelRecord = _labelProcessor.GetLabelAnnotation(selectedItemName);
                    SetButtonStatuses(labelRecord);
                }
            }
        }

        /// <summary>
        /// 儲存標記資料的按鈕事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = _previousAnnotationPath,
                FileName = "annotation",
                AddExtension = true,
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                var annotationLabels = _labelProcessor.OutputAnnotationLabels();
                File.WriteAllText(saveFileDialog.FileName, annotationLabels);
            }
        }

        /// <summary>
        /// 主類別按鈕們的事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LabelBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleButtonAndProcess(sender);
        }

        /// <summary>
        /// 次類別按鈕們的事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SubLabelBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleButtonAndProcess(sender);
        }



        /// <summary>
        /// FileListBox被選取的事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileListBox.SelectedItem != null)
            {
                var selectedItem = (FileListItem)FileListBox.SelectedItem;
                var filePath = Path.Combine(selectedItem.DirectoryName, selectedItem.Name);

                if (filePath.Contains(".tsv"))
                {
                    textArea.Text = File.ReadAllText(filePath);
                }
                else
                {
                    var resultList = new List<string>();
                    var parser = new TextFieldParser(filePath);
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(",");
                    while (!parser.EndOfData)
                    {
                        string[] fields = parser.ReadFields();
                        if (fields != null && fields.Length > 3)
                        {
                            resultList.Add(fields[3]);
                        }
                    }
                    textArea.Text = string.Join(Environment.NewLine, resultList);
                }

                ClearLabelTexts();
                ClearButtonBackground();

                var selectedItemName = GetCurrentSelectedItemName();
                if (selectedItemName != string.Empty)
                {
                    var labelRecord = _labelProcessor.GetLabelAnnotation(selectedItemName);
                    SetButtonStatuses(labelRecord);
                }
            }
        }

        /// <summary>
        /// 改變按鈕的樣式狀態、新增或移除資料，
        /// 並輸出該資料檔的標記編碼
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        private string ToggleButtonAndProcess(object sender)
        {
            var labelText = string.Empty;

            var button = sender as Button;

            var selectedItemName = GetCurrentSelectedItemName();
            if (button != null && selectedItemName != string.Empty)
            {
                var labelValue = Path.GetFileNameWithoutExtension(button.Name);

                if (button.Background == Brushes.LightGreen)
                {
                    button.Background = SystemColors.ControlBrush;
                    _labelProcessor.RemoveLabel(selectedItemName, labelValue);
                }
                else
                {
                    button.Background = Brushes.LightGreen;
                    _labelProcessor.AddLabel(selectedItemName, labelValue);
                }

                var labelRecord = _labelProcessor.GetLabelAnnotation(selectedItemName);
                SetButtonStatuses(labelRecord);
            }

            return labelText;
        }

        /// <summary>
        /// 重設所有物件、按鈕、及文字的狀態
        /// </summary>
        private void ClearAllStatuses()
        {
            _labelProcessor.ClearUserData();
            FileListBox.Items.Clear();
            ClearButtonBackground();

            ClearLabelTexts();
        }

        /// <summary>
        /// 重設主類別及次類別的即時編碼訊息
        /// </summary>
        private void ClearLabelTexts()
        {
            CategoryLabel.Content = string.Empty;
            SubCategoryLabel.Content = string.Empty;
        }

        /// <summary>
        /// 重設按鈕的背景樣式
        /// </summary>
        private void ClearButtonBackground()
        {
            foreach (var item in CategoryPanel.Children)
            {
                if (item.GetType() == typeof(Button))
                {
                    var button = (Button)item;

                    button.Background = SystemColors.ControlBrush;
                }
            }

            foreach (var item in SubCategoryPanel.Children)
            {
                if (item.GetType() == typeof(Button))
                {
                    var button = (Button)item;

                    button.Background = SystemColors.ControlBrush;
                }
            }
        }

        /// <summary>
        /// 依照既有資料或載入資料，設定Buttons的顯示狀態
        /// </summary>
        /// <param name="labelRecord"></param>
        private void SetButtonStatuses(LabelRecord labelRecord)
        {
            if (labelRecord != null)
            {
                foreach (var item in CategoryPanel.Children)
                {
                    if (item.GetType() == typeof(Button))
                    {
                        var button = (Button)item;

                        if (labelRecord.UserLabels.Contains(button.Name))
                        {
                            button.Background = Brushes.LightGreen;
                        }
                        else
                        {
                            button.Background = SystemColors.ControlBrush;
                        }
                    }
                }


                foreach (var item in SubCategoryPanel.Children)
                {
                    if (item.GetType() == typeof(Button))
                    {
                        var button = (Button)item;

                        if (labelRecord.UserSubLabels.Contains(button.Name))
                        {
                            button.Background = Brushes.LightGreen;
                        }
                        else
                        {
                            button.Background = SystemColors.ControlBrush;
                        }
                    }
                }
            }

            var selectedItemName = GetCurrentSelectedItemName();
            if (selectedItemName != string.Empty)
            {
                var labelEncoding = _labelProcessor.EncodingLabel(selectedItemName, LabelProcessor.LabelType.Label);
                var subLabelEncoding = _labelProcessor.EncodingLabel(selectedItemName, LabelProcessor.LabelType.SubLabel);

                CategoryLabel.Content = labelEncoding;
                SubCategoryLabel.Content = subLabelEncoding;

                WriteAnnotationFile($"{labelEncoding},{subLabelEncoding}");

                WriteSummaryFile();
            }
        }


        /// <summary>
        /// 從設定檔載入主類別、次類別的設定資料，並產生Buttons，
        /// 並綁定每個Button的按鈕事件。
        /// </summary>
        /// <param name="labelOptions"></param>
        private void InitializeLabelButtons(List<LabelOption> labelOptions)
        {
            var rowIndex = 0;
            foreach (var labelOption in labelOptions)
            {
                var button = new Button
                {
                    Content = $"  {labelOption.Id} {labelOption.Label.Zh}",
                    Name = labelOption.Value,
                    Margin = new Thickness(0, 0, 10, 23 * labelOption.SubLabels.Count),
                    Height = 50,
                    Width = 100,
                    FontSize = 14,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                };

                button.Click += LabelBtn_Click;

                CategoryPanel.Children.Add(button);

                InitializeSubLabelButtons(labelOption);

                rowIndex++;
            }
        }

        /// <summary>
        /// 產生並綁定每個次類別的Button按鈕事件。
        /// </summary>
        /// <param name="labelConfig"></param>
        /// <param name="subRowIndex"></param>
        private void InitializeSubLabelButtons(LabelOption labelConfig)
        {
            var index = 0;
            foreach (var subLabel in labelConfig.SubLabels)
            {
                var button = new Button
                {
                    Content = $"  {subLabel.Id} {subLabel.Label.Zh}",
                    Name = subLabel.Value,
                    Margin = new Thickness(0, 0, 10, 5),
                    FontSize = 14,
                    Height = 23,
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };

                button.Click += SubLabelBtn_Click;

                SubCategoryPanel.Children.Add(button);

                index++;

                if (index == labelConfig.SubLabels.Count)
                {
                    var separator = new Separator
                    {
                        Margin = new Thickness(0, 0, 0, (5 * (10 - labelConfig.SubLabels.Count))),
                        Opacity = 0,
                    };

                    SubCategoryPanel.Children.Add(separator);
                }
            }
        }

        /// <summary>
        /// 初始化FileListBox，若預設目錄存在，則預先載入所有資料
        /// </summary>
        private void InitializeFileListBox()
        {
            var folderPath = _appSettings.DefaultFolderPath;

            FileListBox.DisplayMemberPath = "Name";
            FileListBox.PreviewKeyDown += FileListBox_PreviewKeyDown;


            if (Directory.Exists(folderPath))
            {
                var existFileNames = Directory.GetFiles(folderPath).Where(file => file.EndsWith("tsv")).ToArray();
                LoadFileListBoxItems(existFileNames);
            }
        }

        /// <summary>
        /// 初始化鍵盤WASD的對應
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.W)
            {
                if (FileListBox.SelectedIndex > 0)
                {
                    FileListBox.SelectedIndex--;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.S)
            {
                if (FileListBox.SelectedIndex < FileListBox.Items.Count - 1)
                {
                    FileListBox.SelectedIndex++;
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// 載入專案設定檔
        /// </summary>
        /// <returns></returns>
        private static AppSettings GetAppSettings()
        {
            return new AppSettings
            {
                LabelOptionPath = ConfigurationManager.AppSettings["LabelOptionPath"]!,
                EncodingPositionPath = ConfigurationManager.AppSettings["EncodingPositionPath"]!,
                DefaultFolderPath = ConfigurationManager.AppSettings["DefaultFolderPath"]!
            };
        }

        /// <summary>
        /// 載入按鈕的設定檔
        /// </summary>
        /// <returns></returns>
        private List<LabelOption> GetLabelOptions()
        {
            var labelOptions = File.ReadAllText(_appSettings.LabelOptionPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };

            return JsonSerializer.Deserialize<List<LabelOption>>(labelOptions, options)!;
        }

        /// <summary>
        /// 產生並初始化標記處理器
        /// </summary>
        /// <param name="labelOptions"></param>
        /// <returns></returns>
        private LabelProcessor GetLabelProcessor(List<LabelOption> labelOptions)
        {
            LabelProcessor labelProcessor = null;
            var encodingPosition = _appSettings.EncodingPositionPath;
            if (File.Exists(encodingPosition))
            {
                labelProcessor = new LabelProcessor(labelOptions, encodingPosition);
            }

            return labelProcessor;
        }

        /// <summary>
        /// 從檔案取得先前標記的資料
        /// </summary>
        /// <param name="previousAnnotationPath"></param>
        /// <returns></returns>
        private List<LabelAnnotation> GetPreviousLabelAnnotations(string previousAnnotationPath)
        {
            List<LabelAnnotation> labelAnnotations = [];

            using var reader = new StreamReader(previousAnnotationPath);
            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
            };
            using var csv = new CsvReader(reader, config);

            return labelAnnotations = csv.GetRecords<LabelAnnotation>().ToList();
        }

        /// <summary>
        /// 取當當下選擇的檔案名稱
        /// </summary>
        /// <returns></returns>
        private string GetCurrentSelectedItemName()
        {
            var selectItemName = string.Empty;
            if (FileListBox.SelectedItem != null)
            {
                var selectedItem = (FileListItem)FileListBox.SelectedItem;
                selectItemName = selectedItem.Name;
            }
            return selectItemName;
        }

        /// <summary>
        /// 將檔案名稱載入到FileListBox
        /// </summary>
        /// <param name="loadFileNames"></param>
        private void LoadFileListBoxItems(string[] loadFileNames)
        {
            var loadFileItems = (from item in loadFileNames
                                 select new FileListItem
                                 {
                                     Name = Path.GetFileName(item),
                                     DirectoryName = Path.GetDirectoryName(item)!
                                 }).ToList();

            foreach (var item in FileListBox.Items)
            {
                var existedItem = item as FileListItem;

                if (loadFileItems.Any(item => item.Name == existedItem!.Name))
                {
                    loadFileItems.RemoveAll(item => item.Name == existedItem!.Name);
                }
            }

            loadFileItems.ForEach(item => { FileListBox.Items.Add(item); });
        }

        /// <summary>
        /// 依最新的要求每次按下按鈕就要輸出檔案
        /// </summary>
        /// <param name="labelText"></param>
        private void WriteAnnotationFile(string labelText)
        {
            var fileName = Path.ChangeExtension(GetCurrentSelectedItemName(), "txt");
            if (!string.IsNullOrEmpty(fileName))
            {
                var filePath = Path.Combine(_appSettings.DefaultFolderPath, fileName);
                if (!Directory.Exists(_appSettings.DefaultFolderPath))
                    Directory.CreateDirectory(_appSettings.DefaultFolderPath);

                File.WriteAllText(filePath, labelText);
            }
        }

        /// <summary>
        /// 依要求輸出目前的所有標記資料總數
        /// </summary>
        private void WriteSummaryFile()
        {
            var filePath = Path.Combine(_appSettings.DefaultFolderPath, "summary.txt");
            if (!Directory.Exists(_appSettings.DefaultFolderPath))
                Directory.CreateDirectory(_appSettings.DefaultFolderPath);

            File.WriteAllText(filePath, _labelProcessor.OutputSummary());
        }
    }
}