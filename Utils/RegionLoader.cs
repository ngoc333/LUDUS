using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml.Linq;

namespace LUDUS.Utils {
    public class RegionInfo {
        public string Name { get; set; }
        public Rectangle Rect { get; set; }
    }

    public static class RegionLoader {
        public static List<RegionInfo> LoadPresetRegions(string xmlPath) {
            var list = new List<RegionInfo>();
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                return list;

            var doc = XDocument.Load(xmlPath);
            foreach (var xe in doc.Root.Elements("Region")) {
                var nameAttr = xe.Attribute("Name");
                var xAttr = xe.Attribute("X");
                var yAttr = xe.Attribute("Y");
                var wAttr = xe.Attribute("Width");
                var hAttr = xe.Attribute("Height");
                if (nameAttr == null || xAttr == null || yAttr == null || wAttr == null || hAttr == null)
                    continue;

                list.Add(new RegionInfo {
                    Name = nameAttr.Value,
                    Rect = new Rectangle(
                        int.Parse(xAttr.Value),
                        int.Parse(yAttr.Value),
                        int.Parse(wAttr.Value),
                        int.Parse(hAttr.Value)
                    )
                });
            }

            return list;
        }
    }
}
