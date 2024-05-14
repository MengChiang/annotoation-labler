using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Annotation.Dtos
{
    public class Label
    {
        public string Zh { get; set; }
        public string En { get; set; }
    }

    public class SubLabel
    {
        public string Id { get; set; }
        public Label Label { get; set; }
        public string Value { get; set; }
    }

    public class LabelOption
    {
        public string Id { get; set; }
        public Label Label { get; set; }
        public string Value { get; set; }
        public List<SubLabel> SubLabels { get; set; }
    }
}
