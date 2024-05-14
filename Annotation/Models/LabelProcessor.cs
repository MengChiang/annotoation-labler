using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Annotation.Dtos;

namespace Annotation.Models
{
    public class LabelRecord
    {
        public string Id { get; set; }
        public List<string> UserLabels;
        public List<string> UserSubLabels;

        public LabelRecord()
        {
            UserLabels = new List<string>();
            UserSubLabels = new List<string>();
        }
    }

    public class LabelProcessor
    {
        Dictionary<string, int> charPositions;

        List<LabelRecord> userLabelRecords;

        private readonly List<LabelOption> _labelOptions;

        public enum LabelType
        {
            Label,
            SubLabel
        }

        public LabelProcessor(List<LabelOption> labelOptions, string encodingPath)
        {
            _labelOptions = labelOptions;
            var content = File.ReadAllText(encodingPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            charPositions = JsonSerializer.Deserialize<Dictionary<string, int>>(content, options)!;
            ClearUserData();
        }

        /// <summary>
        /// 清除使用者資料
        /// </summary>
        public void ClearUserData()
        {
            userLabelRecords = [];

            var labelRecord = new LabelRecord
            {
                Id = "default"
            };

            foreach (var label in _labelOptions)
            {
                labelRecord.UserLabels.Add(label.Value);
                foreach (var subLabel in label.SubLabels)
                {
                    labelRecord.UserSubLabels.Add(subLabel.Value);
                }
            }

            userLabelRecords.Add(labelRecord);
        }

        /// <summary>
        /// 查詢編碼的位元
        /// </summary>
        /// <param name="labelName"></param>
        /// <returns></returns>
        private int QueryPoistion(string labelName)
        {
            return charPositions[labelName];
        }

        /// <summary>
        /// 查詢標記是父類別還是子類別
        /// </summary>
        /// <param name="labelName"></param>
        /// <returns></returns>
        public LabelType QueryLabelType(string labelName)
        {
            var defaultLabelReocrd = userLabelRecords.ElementAt(0);
            if (defaultLabelReocrd.UserLabels.Any(item => item == labelName))
                return LabelType.Label;
            else
                return LabelType.SubLabel;
        }

        /// <summary>
        /// 依照子標籤查詢父標籤以及其它的子標籤
        /// </summary>
        /// <param name="labelName"></param>
        /// <param name="childenLabels"></param>
        /// <returns></returns>
        public string QueryParentLabel(string labelName, out List<string> childenLabels)
        {
            childenLabels = null;

            var parentLabelOption = _labelOptions
                .Where(category => category.SubLabels.Exists(subCategory => subCategory.Value == labelName))
                .First();

            childenLabels = parentLabelOption.SubLabels
                .Where(subLabel => subLabel.Value != labelName)
                .Select(item => item.Value).ToList();


            return parentLabelOption.Value;
        }

        /// <summary>
        /// 依照檔案及標籤名稱新增標記物件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="labelName"></param>
        public void AddLabel(string id, string labelName)
        {
            LabelRecord labelRecord = null;

            if (userLabelRecords.Any(label => label.Id == id))
            {
                labelRecord = userLabelRecords.First(label => label.Id == id);
            }
            else
            {
                labelRecord = new LabelRecord
                {
                    Id = id,
                    UserLabels = [],
                    UserSubLabels = [],
                };
                userLabelRecords.Add(labelRecord);
            }

            if (QueryLabelType(labelName) == LabelType.Label)
            {
                labelRecord.UserLabels.Add(labelName);
            }
            else
            {
                //子標記新增，若父標記不存在，則同步新增父類別
                var parentLabel = QueryParentLabel(labelName, out _);
                if (!labelRecord.UserLabels.Contains(parentLabel))
                    labelRecord.UserLabels.Add(parentLabel);

                labelRecord.UserSubLabels.Add(labelName);
            }
        }

        /// <summary>
        /// 依照檔案id移除該標記資料
        /// </summary>
        /// <param name="id"></param>
        /// <param name="labelName"></param>
        public void RemoveLabel(string id, string labelName)
        {
            if (userLabelRecords.Any(label => label.Id == id))
            {
                var labelRecord = userLabelRecords.First(label => label.Id == id);
                if (QueryLabelType(labelName) == LabelType.Label)
                {
                    labelRecord.UserLabels.Remove(labelName);
                }
                else
                {
                    var parentLabel = QueryParentLabel(labelName, out List<string> childenLabels);

                    if (!labelRecord.UserSubLabels.Exists(item => childenLabels.Contains(item)))
                    {
                        RemoveLabel(id, parentLabel);
                    }

                    labelRecord.UserSubLabels.Remove(labelName);
                }
            }
        }

        /// <summary>
        /// 指定現有id輸出標記Encoding
        /// </summary>
        /// <param name="id"></param>
        /// <param name="labelType"></param>
        /// <returns></returns>
        public string EncodingLabel(string id, LabelType labelType)
        {
            var labelRecord = userLabelRecords.FirstOrDefault(label => label.Id == id);

            var defaultLabelReocrd = userLabelRecords.ElementAt(0);
            var count = (labelType == LabelType.Label) ? defaultLabelReocrd.UserLabels.Count : defaultLabelReocrd.UserSubLabels.Count;

            var encodingLabel = new string('0', count).ToCharArray();

            if (labelRecord != null)
            {
                var labels = (labelType == LabelType.Label) ? labelRecord.UserLabels : labelRecord.UserSubLabels;
                foreach (var label in labels)
                {
                    var index = QueryPoistion(label);
                    encodingLabel[index] = '1';
                }
            }

            return new string(encodingLabel);
        }

        /// <summary>
        /// 依照標記的類別將
        /// </summary>
        /// <param name="id"></param>
        /// <param name="labelName"></param>
        /// <returns></returns>
        public string EncodingLabel(string id, string labelName)
        {
            var encodingLabel = string.Empty;

            if (QueryLabelType(labelName) == LabelType.Label)
            {
                encodingLabel = EncodingLabel(id, LabelType.Label);
            }
            else
            {
                encodingLabel = EncodingLabel(id, LabelType.SubLabel);
            }

            return new string(encodingLabel);
        }

        /// <summary>
        /// 如果值等於1則找出原始標籤名稱
        /// </summary>
        /// <param name="label"></param>
        /// <returns></returns>
        public List<string> DecodeLabel(string label)
        {
            var userLabels = new List<string>();

            var index = 0;
            foreach (var item in label)
            {
                if (item == '1')
                {
                    var labelValue = charPositions.Keys.ElementAt(index);

                    userLabels.Add(labelValue);
                }

                index++;
            }

            return userLabels;
        }

        /// <summary>
        /// 載入前次標籤資料
        /// </summary>
        /// <param name="annotations"></param>
        public void LoadLabels(List<LabelAnnotation> annotations)
        {
            foreach (var annotation in annotations)
            {
                var id = annotation.Id;

                var reocrd = userLabelRecords.FirstOrDefault(label => label.Id == id);
                if (reocrd == null)
                {
                    reocrd = new LabelRecord
                    {
                        Id = id,
                        UserLabels = [],
                        UserSubLabels = [],
                    };

                    userLabelRecords.Add(reocrd);
                }

                var labels = DecodeLabel(annotation.LabelEncoding);
                var subLabel = DecodeLabel(annotation.SubLabelEncoding);

                reocrd.UserLabels.AddRange(labels);
                reocrd.UserSubLabels.AddRange(subLabel);
            }
        }

        /// <summary>
        /// 取得指定的標籤物件
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public LabelRecord GetLabelAnnotation(string id)
        {
            return userLabelRecords.FirstOrDefault(label => label.Id == id);
        }

        /// <summary>
        /// 輸出完整標記資料
        /// </summary>
        /// <returns></returns>
        public string OutputAnnotationLabels()
        {
            var content = new StringBuilder();

            foreach (var item in userLabelRecords)
            {
                if (item.Id == "default")
                    continue;

                var labelEncoding = EncodingLabel(item.Id, LabelType.Label);
                var subLabelEncoding = EncodingLabel(item.Id, LabelType.SubLabel);

                content.AppendLine($"{item.Id},{labelEncoding},{subLabelEncoding}");
            }

            return content.ToString();
        }

        /// <summary>
        /// 輸出數量總計
        /// </summary>
        /// <returns></returns>
        public string OutputSummary()
        {
            var content = new StringBuilder();

            Dictionary<string, int> userLabelCounts = [];
            Dictionary<string, int> userSubLabelCounts = [];

            foreach (var item in userLabelRecords)
            {
                if (item.Id == "default")
                    continue;

                foreach (var userLabel in item.UserLabels)
                {
                    if (userLabelCounts.ContainsKey(userLabel))
                    {
                        userLabelCounts[userLabel]++;
                    }
                    else
                    {
                        userLabelCounts.Add(userLabel, 1);
                    }
                }

                foreach (var userSubLabel in item.UserSubLabels)
                {
                    if (userSubLabelCounts.ContainsKey(userSubLabel))
                    {
                        userSubLabelCounts[userSubLabel]++;
                    }
                    else
                    {
                        userSubLabelCounts.Add(userSubLabel, 1);
                    }
                }
            }

            foreach (var item in userLabelCounts)
            {
                var chineseLabelName = _labelOptions
                    .Where(option => option.Value == item.Key)
                    .Select(option => option.Label.Zh).FirstOrDefault();

                content.AppendLine($"[{chineseLabelName}]:{item.Value}");
            }

            content.AppendLine($"");

            foreach (var item in userSubLabelCounts)
            {
                var subLabel = _labelOptions
                    .Where(option => option.SubLabels.Any(sublabel => sublabel.Value == item.Key))
                    .Select(option => option.SubLabels.FirstOrDefault(ob => ob.Value == item.Key)).FirstOrDefault();
                

                content.AppendLine($"[{subLabel.Label.Zh}]:{item.Value}");
            }

            return content.ToString();
        }
    }
}
