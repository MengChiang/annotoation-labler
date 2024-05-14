using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Annotation.Dtos
{
    public class LabelAnnotation
    {
        [Index(0)]
        public string Id { get; set; }
        [Index(1)]
        public string LabelEncoding { get; set; }
        [Index(2)]
        public string SubLabelEncoding { get; set; }
    }
}
