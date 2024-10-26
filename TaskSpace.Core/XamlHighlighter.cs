using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TaskSpace.Core.Matchers;

namespace TaskSpace.Core {
    public class XamlHighlighter {
        public string Highlight(IEnumerable<StringPart> stringParts) {
            if(stringParts == null || !stringParts.Any()) {
                return string.Empty;
            }

            XDocument xDocument = new XDocument(new XElement("Root"));
            foreach(StringPart stringPart in stringParts) {
                if(stringPart.IsMatch) {
                    xDocument.Root.Add(new XElement("Bold", stringPart.Value));
                }
                else {
                    xDocument.Root.Add(new XText(stringPart.Value));
                }
            }
            string retVal = string.Join(string.Empty, xDocument.Root.Nodes().Select(x => x.ToString()).ToArray());
            return retVal;
        }
    }
}
